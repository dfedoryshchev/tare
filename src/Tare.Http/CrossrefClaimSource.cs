using System.Net;
using Tare.Core;

namespace Tare.Http;

/// <summary>
/// Asks Crossref whether a cited DOI is a real registered work. Same contract as
/// <see cref="HttpClaimSource"/> - existence only, never what the work says - but pointed at
/// the registry instead of the publisher, because the registry is the thing that is supposed
/// to still answer once the publisher's URL has rotted.
/// </summary>
/// <remarks>
/// WORK IN PROGRESS, and the blocker is the rate limit rather than the lookup.
/// <para>
/// A single lookup is correct and covered by tests. Checking a document is not: Crossref
/// answers <c>429</c> once a client goes too fast, and this adapter has no throttle, no
/// backoff and no retry - so a draft citing twenty works turns into twenty requests fired as
/// fast as the caller loops, most of them refused. Those refusals are reported
/// <see cref="CitationStatus.Unreachable"/>, which is the honest answer and also a useless
/// one: the author learns nothing about their citations, only about our request rate.
/// </para>
/// <para>
/// Three things have to land before this can be wired into the analyzer, and they are why
/// it is not exported anywhere yet:
/// </para>
/// <list type="number">
/// <item>a shared throttle across a run, since the limit is per client and not per call;</item>
/// <item>honouring <c>Retry-After</c> with a bounded backoff instead of surfacing it as prose;</item>
/// <item>the polite pool - Crossref grants a higher limit to traffic carrying a contact
/// <c>mailto</c>, which means a config surface for the author's address and a decision about
/// sending it at all.</item>
/// </list>
/// <para>
/// Until then the one-at-a-time path stays honest and the batch path stays unbuilt, which is
/// the right way round: a checker that quietly reported rate-limited works as unverified
/// would be exactly the false-positive habit this tool exists to avoid.
/// </para>
/// </remarks>
public sealed class CrossrefClaimSource(HttpClient client) : IClaimSource
{
    private const string WorksEndpoint = "https://api.crossref.org/works/";

    /// <summary>A registry lookup is a nicety too; it never gets to hang a run.</summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Builds a client configured for registry lookups. The user agent is deliberately
    /// identifiable: Crossref asks callers to say who they are, and anonymous traffic gets
    /// the tighter limit.
    /// </summary>
    public static CrossrefClaimSource Create(TimeSpan? timeout = null)
    {
        var configured = new HttpClient { Timeout = timeout ?? DefaultTimeout };
        configured.DefaultRequestHeaders.UserAgent.ParseAdd("tare/0.1 (+citation-check)");
        return new CrossrefClaimSource(configured);
    }

    public async Task<CitationCheck> CheckAsync(Citation citation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The port speaks in citations, so a caller can hand this source the same list it
        // hands the HTTP one; anything that is not a DOI is simply not our question.
        var dois = DoiExtractor.Extract(citation.Url);
        if (dois.Count != 1 || dois[0].Value.Length != citation.Url.Trim().Length)
        {
            return new CitationCheck(citation, CitationStatus.Skipped, "not fetched: not a doi");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, WorksEndpoint + dois[0].Value);
            request.Headers.UserAgent.ParseAdd("tare/0.1 (+citation-check)");

            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            return new CitationCheck(citation, Classify(response.StatusCode), Describe(response));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException)
        {
            return new CitationCheck(citation, CitationStatus.Unreachable, "no response: " + failure.Message);
        }
    }

    private static CitationStatus Classify(HttpStatusCode status) =>
        (int)status is >= 200 and < 300 ? CitationStatus.Resolves
            : status is HttpStatusCode.NotFound or HttpStatusCode.Gone ? CitationStatus.Dead
            : CitationStatus.Unreachable;

    private static string Describe(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            // Surfaced as prose on purpose for now: nothing acts on it yet, and a number
            // nobody reads is worse than a sentence someone does. Point 2 of the remarks.
            var after = response.Headers.TryGetValues("Retry-After", out var values)
                ? values.FirstOrDefault()
                : null;
            return after is null
                ? "rate limited by the registry, so this says nothing about the work"
                : $"rate limited by the registry, which asked for {after}s";
        }

        return status switch
        {
            >= 200 and < 300 => $"the registry has this work (http {status})",
            404 or 410 => $"the registry has no such work (http {status})",
            _ => $"no usable answer (http {status})",
        };
    }
}
