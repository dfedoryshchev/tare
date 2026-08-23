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
/// <para>
/// A whole document is now checkable, which it was not when the lookup first landed. The
/// blocker then was the rate limit: twenty citations meant twenty requests fired as fast as
/// the caller looped, most of them refused, and every refusal reported
/// <see cref="CitationStatus.Unreachable"/> - honest, and useless, because the author learned
/// about our request rate rather than their citations.
/// </para>
/// <para>The three things that were named as the gate, and where each one went:</para>
/// <list type="number">
/// <item>the throttle is shared across the source rather than per call, since the limit is
/// per client - see <see cref="CrossrefOptions.MinInterval"/>;</item>
/// <item><c>Retry-After</c> is honoured with a bounded wait instead of surfaced as prose -
/// see <see cref="CrossrefOptions.MaxAttempts"/> and <see cref="CrossrefOptions.MaxRetryWait"/>;</item>
/// <item>the polite pool is opt-in through <see cref="CrossrefOptions.ContactEmail"/>.</item>
/// </list>
/// <para>
/// What has NOT changed is what a refusal means. A 429 that outlives the retries is still
/// <see cref="CitationStatus.Unreachable"/> and never <see cref="CitationStatus.Dead"/>: the
/// rate limiter is a fact about our traffic, not about the author's citation, and a checker
/// that quietly reported rate-limited works as unverified would be exactly the false-positive
/// habit this tool exists to avoid.
/// </para>
/// <para>
/// Still not wired into <see cref="Analyzer"/> or the CLI - the analyzer taking a verifier is
/// its own change, so there is no rule id and no finding here.
/// </para>
/// </remarks>
public sealed class CrossrefClaimSource : IClaimSource
{
    private const string WorksEndpoint = "https://api.crossref.org/works/";

    /// <summary>A registry lookup is a nicety too; it never gets to hang a run.</summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient client;
    private readonly CrossrefOptions options;

    // One gate per source, because the registry's limit is per client. Two lookups through
    // the same source queue behind each other; two different sources are not our problem.
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextAllowed = DateTimeOffset.MinValue;

    public CrossrefClaimSource(HttpClient client, CrossrefOptions? options = null)
    {
        this.client = client;
        this.options = options ?? CrossrefOptions.Default;
    }

    /// <summary>
    /// Builds a client configured for registry lookups. The user agent is deliberately
    /// identifiable: Crossref asks callers to say who they are, and anonymous traffic gets
    /// the tighter limit.
    /// </summary>
    public static CrossrefClaimSource Create(TimeSpan? timeout = null, CrossrefOptions? options = null)
    {
        var settings = options ?? CrossrefOptions.Default;
        var configured = new HttpClient { Timeout = timeout ?? DefaultTimeout };
        configured.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent(settings));
        return new CrossrefClaimSource(configured, settings);
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

        var target = Target(dois[0].Value);

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                await PaceAsync(cancellationToken);

                using var request = new HttpRequestMessage(HttpMethod.Get, target);
                request.Headers.UserAgent.ParseAdd(UserAgent(options));

                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    return new CitationCheck(citation, Classify(response.StatusCode), Describe(response));
                }

                // Out of attempts, or asked to wait longer than a citation check should sit
                // through. The status is the same either way; the reason is not, because
                // "we waited and it is still refusing" and "we declined to wait that long"
                // are different facts and only one of them is true at a time.
                var after = RetryAfter(response);
                if (attempt >= options.MaxAttempts)
                {
                    return new CitationCheck(citation, CitationStatus.Unreachable, Exhausted(attempt));
                }

                if (after is null || after > options.MaxRetryWait)
                {
                    return new CitationCheck(citation, CitationStatus.Unreachable, Refused(after));
                }

                await options.Wait(after.Value, cancellationToken);
            }
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

    /// <summary>
    /// Holds every lookup to <see cref="CrossrefOptions.MinInterval"/> apart. The wait is
    /// taken while holding the gate so two callers cannot both decide they are next.
    /// </summary>
    private async Task PaceAsync(CancellationToken cancellationToken)
    {
        if (options.MinInterval <= TimeSpan.Zero)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var wait = nextAllowed - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await options.Wait(wait, cancellationToken);
            }

            nextAllowed = DateTimeOffset.UtcNow + options.MinInterval;
        }
        finally
        {
            gate.Release();
        }
    }

    private Uri Target(string doi)
    {
        var url = WorksEndpoint + doi;
        // The polite pool is keyed on a contact address; Crossref accepts it either on the
        // user agent or as a query parameter, and sending both costs nothing.
        return options.ContactEmail is { Length: > 0 } contact
            ? new Uri(url + "?mailto=" + Uri.EscapeDataString(contact))
            : new Uri(url);
    }

    private static string UserAgent(CrossrefOptions options) =>
        options.ContactEmail is { Length: > 0 } contact
            ? "tare/0.1 (+citation-check; mailto:" + contact + ")"
            : "tare/0.1 (+citation-check)";

    /// <summary>
    /// The delta the registry asked for, whether it sent seconds or a date. Null when it sent
    /// nothing usable, which is treated as "do not guess" rather than as a default wait.
    /// </summary>
    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private static CitationStatus Classify(HttpStatusCode status) =>
        (int)status is >= 200 and < 300 ? CitationStatus.Resolves
            : status is HttpStatusCode.NotFound or HttpStatusCode.Gone ? CitationStatus.Dead
            : CitationStatus.Unreachable;

    /// <summary>We waited as asked, as often as we are willing to, and it is still refusing.</summary>
    private static string Exhausted(int attempts) =>
        $"still rate limited after {attempts} attempts, so this says nothing about the work";

    /// <summary>
    /// We did not wait. Either the registry named a delay longer than a citation check sits
    /// through, or it named none at all - and the number is worth printing, because it is the
    /// one thing that tells an author whether to retry the run later.
    /// </summary>
    private static string Refused(TimeSpan? after) =>
        after is null
            ? "rate limited by the registry, so this says nothing about the work"
            : $"rate limited: the registry asked for {(int)after.Value.TotalSeconds}s, "
              + "longer than this check waits, so this says nothing about the work";

    private static string Describe(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            // Reached only when a caller passes a 429 straight here; the retry loop builds
            // its own reason from the branch that actually fired.
            return Refused(RetryAfter(response));
        }

        return status switch
        {
            >= 200 and < 300 => $"the registry has this work (http {status})",
            404 or 410 => $"the registry has no such work (http {status})",
            _ => $"no usable answer (http {status})",
        };
    }
}
