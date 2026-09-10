using System.Net;
using Tare.Core;

namespace Tare.Http;

/// <summary>
/// The <see cref="IClaimSource"/> adapter: resolves a cited URL over HTTP. It asks for
/// headers only - a HEAD request, and a GET that stops at the response headers when a
/// server refuses HEAD - because the question is whether the page is there, not what it
/// says. Nothing here decides anything about a draft; it turns a URL into a
/// <see cref="CitationStatus"/> and hands it back.
/// <para>
/// The <see cref="HttpClient"/> is injected rather than created, which is what keeps the
/// tests offline: they hand it a stubbed handler and no request ever leaves the process.
/// <see cref="Create"/> builds the configured client for real use. The caller owns the
/// client's lifetime.
/// </para>
/// </summary>
public sealed class HttpClaimSource(HttpClient client) : IClaimSource
{
    /// <summary>A citation check is a background nicety; it never gets to hang a run.</summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Enough hops for the usual shortener-then-canonical-url chain, not enough to loop.</summary>
    private const int MaxRedirects = 5;

    /// <summary>
    /// Builds a client configured for citation checks: a short timeout, no redirect following
    /// in the transport, and an honest user agent so a maintainer reading their logs can tell
    /// what this traffic is.
    /// </summary>
    public static HttpClaimSource Create(TimeSpan? timeout = null)
    {
        // The transport would follow redirects without asking CitationPolicy again, which is
        // how a public URL that 302s to a link-local address gets fetched anyway. The chain is
        // walked in Follow instead, where the policy sees every hop.
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };

        var configured = new HttpClient(handler) { Timeout = timeout ?? DefaultTimeout };
        configured.DefaultRequestHeaders.UserAgent.ParseAdd("tare/0.1 (+citation-check)");
        return new HttpClaimSource(configured);
    }

    public async Task<CitationCheck> CheckAsync(Citation citation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (CitationPolicy.Reject(citation.Url) is { } refusal)
        {
            return new CitationCheck(citation, CitationStatus.Skipped, "not fetched: " + refusal);
        }

        try
        {
            var (response, hopRefusal) = await Follow(HttpMethod.Head, citation.Url, cancellationToken);

            // Plenty of servers, and most CDNs in front of them, answer HEAD with a refusal
            // and the same URL with a GET. Treating that refusal as an answer about the page
            // would report live sources as broken, so it costs one more request to be sure.
            if (response is { } head && RefusesHead(head.StatusCode))
            {
                head.Dispose();
                (response, hopRefusal) = await Follow(HttpMethod.Get, citation.Url, cancellationToken);
            }

            if (response is null)
            {
                return new CitationCheck(citation, CitationStatus.Skipped, "not fetched: " + hopRefusal);
            }

            using (response)
            {
                return new CitationCheck(
                    citation,
                    CitationOutcome.FromHttpStatus((int)response.StatusCode),
                    Describe(response.StatusCode));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException)
        {
            // Includes the client's own timeout, which surfaces as a cancellation the caller
            // never asked for.
            return new CitationCheck(citation, CitationStatus.Unreachable, "no response: " + failure.Message);
        }
    }

    /// <summary>
    /// Walks the redirect chain a hop at a time so <see cref="CitationPolicy"/> gets a say at
    /// every one of them. Returns the response the chain came to rest on, or the policy's
    /// reason for declining to take a hop - in which case nothing was sent to that target.
    /// The walk gives up after <see cref="MaxRedirects"/> hops and hands back whatever it is
    /// holding, which is still a redirect and so is never read as an answer about the page.
    /// </summary>
    private async Task<(HttpResponseMessage? Response, string? Refusal)> Follow(
        HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var current = new Uri(url);
        var response = await Send(method, current, cancellationToken);

        for (var hop = 0; hop < MaxRedirects; hop++)
        {
            if (NextHop(response, current) is not { } next)
            {
                break;
            }

            if (CitationPolicy.Reject(next.AbsoluteUri) is { } refusal)
            {
                response.Dispose();
                return (null, refusal);
            }

            // A redirect out of https into http would finish the chain in the clear, so the
            // walk stops on the redirect rather than following it down.
            if (current.Scheme == Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttps)
            {
                break;
            }

            response.Dispose();
            current = next;
            response = await Send(method, current, cancellationToken);
        }

        return (response, null);
    }

    private static Uri? NextHop(HttpResponseMessage response, Uri from) =>
        IsRedirect(response.StatusCode)
        && response.Headers.Location is { } location
        && Uri.TryCreate(from, location, out var target)
            ? target
            : null;

    private Task<HttpResponseMessage> Send(HttpMethod method, Uri url, CancellationToken cancellationToken) =>
        client.SendAsync(
            new HttpRequestMessage(method, url), HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    private static bool IsRedirect(HttpStatusCode status) =>
        (int)status is 300 or 301 or 302 or 303 or 307 or 308;

    private static bool RefusesHead(HttpStatusCode status) =>
        status is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented or HttpStatusCode.Forbidden;

    private static string Describe(HttpStatusCode status) => (int)status switch
    {
        >= 200 and < 300 => $"resolves (http {(int)status})",
        404 or 410 => $"the server says there is nothing there (http {(int)status})",
        _ => $"no usable answer (http {(int)status})",
    };
}
