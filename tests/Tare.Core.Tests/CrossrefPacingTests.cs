using System.Net;
using Tare.Core;
using Tare.Http;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// The three things the adapter's remarks named as the gate on checking a whole document:
/// a shared throttle, a bounded <c>Retry-After</c> backoff, and the polite pool. Offline
/// like every other adapter test, and it never really sleeps - the pause is substituted so
/// the suite can assert what was waited for instead of waiting for it.
/// </summary>
public class CrossrefPacingTests
{
    private static Citation Cited(string url) => new(url, 0, 0, url.Length);

    private sealed class Pauses
    {
        public List<TimeSpan> Taken { get; } = new();

        public Task Record(TimeSpan delay, CancellationToken cancellationToken)
        {
            Taken.Add(delay);
            return Task.CompletedTask;
        }
    }

    private static CrossrefOptions Instant(Pauses pauses) =>
        CrossrefOptions.Default with { Wait = pauses.Record };

    [Fact]
    public async Task A_single_lookup_waits_for_nothing()
    {
        var pauses = new Pauses();
        var source = new CrossrefClaimSource(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            Instant(pauses));

        await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.Empty(pauses.Taken);
    }

    [Fact]
    public async Task A_second_lookup_is_spaced_out_from_the_first()
    {
        // The limit is per client, not per call, so the throttle has to live on the source
        // and be shared by every lookup a run makes through it.
        var pauses = new Pauses();
        var source = new CrossrefClaimSource(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            Instant(pauses));

        await source.CheckAsync(Cited("10.1038/nphys1170"));
        await source.CheckAsync(Cited("10.1038/nphys1171"));

        var waited = Assert.Single(pauses.Taken);
        Assert.InRange(waited, TimeSpan.FromTicks(1), CrossrefOptions.Default.MinInterval);
    }

    [Fact]
    public async Task A_rate_limited_lookup_is_retried_after_the_registry_says_when()
    {
        var pauses = new Pauses();
        var replies = new Queue<HttpResponseMessage>();
        var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        limited.Headers.Add("Retry-After", "2");
        replies.Enqueue(limited);
        replies.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var handler = new StubHandler(_ => replies.Dequeue());
        var source = new CrossrefClaimSource(
            new HttpClient(handler),
            CrossrefOptions.Default with { Wait = pauses.Record, MinInterval = TimeSpan.Zero });

        var check = await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.Equal(CitationStatus.Resolves, check.Status);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(TimeSpan.FromSeconds(2), pauses.Taken);
    }

    [Fact]
    public async Task It_gives_up_after_a_bounded_number_of_attempts()
    {
        var pauses = new Pauses();
        var handler = new StubHandler(_ =>
        {
            var reply = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            reply.Headers.Add("Retry-After", "1");
            return reply;
        });
        var options = CrossrefOptions.Default with { Wait = pauses.Record, MinInterval = TimeSpan.Zero };
        var source = new CrossrefClaimSource(new HttpClient(handler), options);

        var check = await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
        Assert.Equal(options.MaxAttempts, handler.Requests.Count);
    }

    [Fact]
    public async Task A_retry_after_longer_than_the_cap_is_not_waited_out()
    {
        // Bounded, in the remarks' words. A registry asking for a quarter of an hour is not
        // something a citation check should sit through; it is an Unreachable answer now.
        var pauses = new Pauses();
        var handler = new StubHandler(_ =>
        {
            var reply = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            reply.Headers.Add("Retry-After", "900");
            return reply;
        });
        var source = new CrossrefClaimSource(
            new HttpClient(handler),
            CrossrefOptions.Default with { Wait = pauses.Record, MinInterval = TimeSpan.Zero });

        var check = await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
        Assert.Single(handler.Requests);
        Assert.Empty(pauses.Taken);
    }

    [Fact]
    public async Task A_configured_contact_joins_the_polite_pool()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new CrossrefClaimSource(
            new HttpClient(handler),
            CrossrefOptions.Default with { ContactEmail = "someone@example.org" });

        await source.CheckAsync(Cited("10.1038/nphys1170"));

        var request = Assert.Single(handler.Requests);
        Assert.Contains("mailto=someone%40example.org", request.RequestUri!.Query);
    }

    [Fact]
    public async Task No_contact_configured_sends_no_address()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new CrossrefClaimSource(new HttpClient(handler));

        await source.CheckAsync(Cited("10.1038/nphys1170"));

        var request = Assert.Single(handler.Requests);
        Assert.DoesNotContain("mailto", request.RequestUri!.ToString());
    }
}
