using System.Net;
using Tare.Core;
using Tare.Http;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// Every test here runs against a stubbed handler: the adapter is the only thing in the
/// solution that can reach the network, so nothing in the suite is allowed to actually go
/// there. A test that needed a live host would be measuring someone else's uptime.
/// </summary>
public class HttpClaimSourceTests
{
    private static Citation Cited(string url) => new(url, 0, 0, url.Length);

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    [Fact]
    public async Task Resolves_a_url_that_answers_a_head_request()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new HttpClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Resolves, check.Status);
        Assert.Equal(HttpMethod.Head, Assert.Single(handler.Requests).Method);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task Marks_a_missing_page_as_dead(HttpStatusCode status)
    {
        var source = new HttpClaimSource(new HttpClient(new StubHandler(_ => new HttpResponseMessage(status))));

        var check = await source.CheckAsync(Cited("https://example.com/gone"));

        Assert.Equal(CitationStatus.Dead, check.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Retries_with_get_when_head_is_refused(HttpStatusCode refusal)
    {
        var handler = new StubHandler(r => new HttpResponseMessage(
            r.Method == HttpMethod.Head ? refusal : HttpStatusCode.OK));
        var source = new HttpClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Resolves, check.Status);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
    }

    [Fact]
    public async Task Reports_a_server_error_as_unreachable_rather_than_dead()
    {
        var source = new HttpClaimSource(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
        Assert.Contains("500", check.Reason);
    }

    [Fact]
    public async Task Reports_a_transport_failure_as_unreachable()
    {
        var source = new HttpClaimSource(new HttpClient(
            new StubHandler(_ => throw new HttpRequestException("no such host"))));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
    }

    [Fact]
    public async Task Reports_a_timeout_as_unreachable()
    {
        var source = new HttpClaimSource(new HttpClient(
            new StubHandler(_ => throw new TaskCanceledException("timed out"))));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
    }

    [Fact]
    public async Task Skips_a_url_the_policy_rejects_without_sending_anything()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new HttpClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("http://169.254.169.254/latest/meta-data/"));

        Assert.Equal(CitationStatus.Skipped, check.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Refuses_a_redirect_hop_the_policy_rejects()
    {
        var handler = new StubHandler(_ => Redirect("http://169.254.169.254/latest/meta-data/"));
        var source = new HttpClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Skipped, check.Status);
        Assert.Contains("link-local", check.Reason);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Follows_a_redirect_to_a_public_url()
    {
        var handler = new StubHandler(r => r.RequestUri!.AbsoluteUri == "https://example.com/report"
            ? Redirect("https://cdn.example.org/report")
            : new HttpResponseMessage(HttpStatusCode.OK));
        var source = new HttpClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Resolves, check.Status);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://cdn.example.org/report", handler.Requests[1].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Resolves_a_relative_redirect_against_the_url_it_came_from()
    {
        var handler = new StubHandler(r => r.RequestUri!.AbsolutePath == "/report"
            ? Redirect("/moved/report")
            : new HttpResponseMessage(HttpStatusCode.OK));
        var source = new HttpClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Resolves, check.Status);
        Assert.Equal("https://example.com/moved/report", handler.Requests[1].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Stops_at_the_hop_limit_instead_of_following_a_chain_forever()
    {
        var hop = 0;
        var handler = new StubHandler(_ => Redirect($"https://example.com/hop{++hop}"));
        var source = new HttpClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
        Assert.Equal(6, handler.Requests.Count);
    }

    [Fact]
    public async Task Refuses_to_follow_a_redirect_that_drops_https_for_http()
    {
        var handler = new StubHandler(_ => Redirect("http://example.com/report"));
        var source = new HttpClaimSource(new HttpClient(handler));

        var check = await source.CheckAsync(Cited("https://example.com/report"));

        Assert.Equal(CitationStatus.Unreachable, check.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Hands_back_the_citation_it_was_given()
    {
        var citation = new Citation("https://example.com/report", 3, 120, 146);
        var source = new HttpClaimSource(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var check = await source.CheckAsync(citation);

        Assert.Same(citation, check.Citation);
    }

    [Fact]
    public async Task Honours_a_cancelled_token_before_reaching_the_network()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new HttpClaimSource(new HttpClient(handler));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => source.CheckAsync(Cited("https://example.com/report"), cancelled.Token));
        Assert.Empty(handler.Requests);
    }
}
