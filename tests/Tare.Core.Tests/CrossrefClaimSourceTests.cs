using System.Net;
using Tare.Core;
using Tare.Http;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// Offline like every other adapter test: a stubbed handler stands in for Crossref, so the
/// suite never depends on their uptime or their rate limiter.
/// </summary>
public class CrossrefClaimSourceTests
{
    private static Citation Cited(string url) => new(url, 0, 0, url.Length);

    [Fact]
    public async Task Resolves_a_doi_that_crossref_knows()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new CrossrefClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.Equal(CitationStatus.Resolves, check.Status);
    }

    [Fact]
    public async Task Asks_crossref_for_the_work_by_doi()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new CrossrefClaimSource(new HttpClient(handler));

        await source.CheckAsync(Cited("10.1038/nphys1170"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.crossref.org/works/10.1038/nphys1170", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Marks_a_doi_crossref_does_not_have_as_dead()
    {
        var source = new CrossrefClaimSource(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

        var check = await source.CheckAsync(Cited("10.1038/nope"));

        Assert.Equal(CitationStatus.Dead, check.Status);
    }

    [Fact]
    public async Task A_rate_limited_lookup_is_unreachable_not_dead()
    {
        // The whole point of the Dead/Unreachable split. Crossref answering "slow down"
        // says nothing about whether the work exists, and charging the author for it would
        // invent dead citations out of our own request rate.
        var source = new CrossrefClaimSource(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))));

        var check = await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
        Assert.Contains("rate", check.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reports_how_long_crossref_asked_us_to_wait()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.Add("Retry-After", "30");
            return response;
        });
        var source = new CrossrefClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.Contains("30", check.Reason);
    }

    [Fact]
    public async Task Identifies_itself_so_crossref_can_see_who_is_calling()
    {
        // Crossref's polite pool keys off a real user agent; anonymous traffic gets the
        // tighter limit. This only pins that we send one - the mailto half is not done.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new CrossrefClaimSource(new HttpClient(handler));

        await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.NotEmpty(Assert.Single(handler.Requests).Headers.UserAgent);
    }

    [Fact]
    public async Task Declines_anything_that_is_not_a_doi()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new CrossrefClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Skipped, check.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_transport_failure_is_unreachable()
    {
        var source = new CrossrefClaimSource(
            new HttpClient(new StubHandler(_ => throw new HttpRequestException("dns is having a day"))));

        var check = await source.CheckAsync(Cited("10.1038/nphys1170"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
    }

    [Fact]
    public async Task Honours_a_cancelled_token_before_asking()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new CrossrefClaimSource(new HttpClient(handler));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => source.CheckAsync(Cited("10.1038/nphys1170"), cancelled.Token));
        Assert.Empty(handler.Requests);
    }
}
