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
    /// Builds a client configured for citation checks: a short timeout, a capped redirect
    /// chain, and an honest user agent so a maintainer reading their logs can tell what this
    /// traffic is.
    /// </summary>
    public static HttpClaimSource Create(TimeSpan? timeout = null)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = MaxRedirects,
        };

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
            var response = await Send(HttpMethod.Head, citation.Url, cancellationToken);

            // Plenty of servers, and most CDNs in front of them, answer HEAD with a refusal
            // and the same URL with a GET. Treating that refusal as an answer about the page
            // would report live sources as broken, so it costs one more request to be sure.
            if (RefusesHead(response.StatusCode))
            {
                response.Dispose();
                response = await Send(HttpMethod.Get, citation.Url, cancellationToken);
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

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, CancellationToken cancellationToken) =>
        client.SendAsync(
            new HttpRequestMessage(method, url), HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    private static bool RefusesHead(HttpStatusCode status) =>
        status is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented or HttpStatusCode.Forbidden;

    private static string Describe(HttpStatusCode status) => (int)status switch
    {
        >= 200 and < 300 => $"resolves (http {(int)status})",
        404 or 410 => $"the server says there is nothing there (http {(int)status})",
        _ => $"no usable answer (http {(int)status})",
    };
}
