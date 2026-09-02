using System.Net;
using System.Text;
using System.Text.Json;
using Tare.Core;
using Tare.Http;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// The bounded model call, exercised end to end without leaving the process. Two stubbed
/// handlers stand in for the two things this talks to - the cited page and the model service -
/// so the request that gets built and every way an answer can fail to be usable are both
/// measured, and no key, bill or network is involved.
/// <para>
/// What these cannot cover is the service's own behaviour: nothing here has asked the real
/// endpoint anything. They pin what is sent and what is made of what comes back.
/// </para>
/// </summary>
public class ClaudeClaimVerifierTests
{
    private const string Key = "local-test-key";

    private static Claim Claimed(string text) =>
        new(new Sentence(0, text, 0, text.Length), new[] { ClaimKind.Number });

    private static Citation Cited(string url) => new(url, 0, 0, url.Length);

    private static StubHandler Page(string html, string mediaType = "text/html") =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, mediaType),
        });

    /// <summary>The wire shape of one answer, built the way the service documents it.</summary>
    private static string Message(string verdict, string reason, string stopReason) =>
        JsonSerializer.Serialize(new
        {
            id = "msg_offline",
            type = "message",
            role = "assistant",
            model = "claude-opus-5",
            stop_reason = stopReason,
            content = new[]
            {
                new { type = "text", text = JsonSerializer.Serialize(new { verdict, reason }) },
            },
            usage = new { input_tokens = 1, output_tokens = 1 },
        });

    /// <summary>
    /// Answers as the service would, and copies out the request body when a test asks for it.
    /// Copied at send time on purpose: the verifier disposes its request as soon as it has the
    /// response, so reading the content afterwards reads a disposed stream.
    /// </summary>
    private static StubHandler Answers(
        string verdict,
        string reason = "the source states it",
        string stopReason = "end_turn",
        List<string>? sent = null) =>
        new(request =>
        {
            sent?.Add(Body(request));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    Message(verdict, reason, stopReason), Encoding.UTF8, "application/json"),
            };
        });

    private static string Body(HttpRequestMessage request) =>
        request.Content is null ? string.Empty : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

    private static StubHandler Replies(HttpStatusCode status, string body = "{}") =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    private static ClaudeClaimVerifier Verifier(
        StubHandler source, StubHandler service, string? key = Key, ClaudeVerifierOptions? options = null) =>
        new(new HttpClient(source), new HttpClient(service), key, options);

    [Fact]
    public async Task Reports_supported_when_the_model_reads_the_source_as_backing_the_claim()
    {
        var verifier = Verifier(
            Page("<p>Throughput rose 40% after the change, according to the load tests.</p>"),
            Answers("supported", "the page reports the same 40% rise"));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Supported, verification.Support);
        Assert.Equal("the page reports the same 40% rise", verification.Reason);
    }

    [Fact]
    public async Task Reports_contradicted_when_the_model_reads_the_source_as_denying_the_claim()
    {
        // The verdict the similarity attempt could not reach at all. Cosine put a denial at
        // 0.80 against the claim and a restatement at 0.79, so the closest passage was the one
        // that disagreed; asking the question in words is what makes this answer available.
        var verifier = Verifier(
            Page("<p>Latency did not fall by 30% after the rollout.</p>"),
            Answers("contradicted", "the page says the fall did not happen"));

        var verification = await verifier.VerifyAsync(
            Claimed("Latency fell by 30% after the rollout."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Contradicted, verification.Support);
    }

    [Fact]
    public async Task Reports_overstated_when_the_model_reads_the_source_as_weaker_than_the_claim()
    {
        var verifier = Verifier(
            Page("<p>Latency fell by 30% on the two busiest shards during the trial week.</p>"),
            Answers("overstated", "the page measures two shards for one week, not the service"));

        var verification = await verifier.VerifyAsync(
            Claimed("Latency fell by 30% across the service."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Overstated, verification.Support);
    }

    [Fact]
    public async Task Reports_unknown_when_the_model_will_not_settle_it()
    {
        var verifier = Verifier(
            Page("<p>The harbour forecast is unchanged: light winds, rain by the weekend.</p>"),
            Answers("unknown", "the page is about the weather"));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Equal("the page is about the weather", verification.Reason);
    }

    [Fact]
    public async Task Asks_once_for_one_claim_however_long_the_source()
    {
        // The cost claim, measured. The similarity attempt paid one call for the claim plus one
        // for every window of the page, so a long source cost more; here the source is read
        // into one request and the length changes nothing.
        var source = Page("<p>" + string.Join(' ', Enumerable.Repeat("filler", 2000)) + "</p>");
        var service = Answers("unknown", "the page says nothing about it");
        var verifier = Verifier(source, service);

        await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Single(source.Requests);
        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task Sends_the_claim_and_the_source_text_and_not_the_url()
    {
        // The URL is deliberately withheld. A domain is a reputation cue, and the question is
        // what this text says, not who published it.
        var sent = new List<string>();
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths after the patch.</p>"), Answers("supported", sent: sent));

        await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        var body = Assert.Single(sent);
        Assert.Contains("Throughput rose 40% after the change.", body);
        Assert.Contains("Throughput rose by two fifths after the patch.", body);
        Assert.DoesNotContain("example.com", body);
    }

    [Fact]
    public async Task Sends_the_key_only_to_the_model_endpoint()
    {
        // Two clients rather than one, and this is the reason: the client carrying the key
        // talks to one configured address, and the client that follows a URL out of an
        // untrusted draft carries no credential at all.
        var source = Page("<p>Throughput rose by two fifths after the patch.</p>");
        var service = Answers("supported");
        var verifier = Verifier(source, service);

        await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.False(source.Requests[0].Headers.Contains("x-api-key"));
        Assert.Equal(Key, Assert.Single(service.Requests[0].Headers.GetValues("x-api-key")));
        Assert.NotEmpty(service.Requests[0].Headers.GetValues("anthropic-version"));
        Assert.Equal(ClaudeVerifierOptions.Default.Endpoint, service.Requests[0].RequestUri);
    }

    [Fact]
    public async Task Asks_for_a_json_answer_and_sends_no_sampling_knobs()
    {
        var sent = new List<string>();
        var options = ClaudeVerifierOptions.Default with { MaxOutputTokens = 777 };
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"), Answers("supported", sent: sent), options: options);

        await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        using var body = JsonDocument.Parse(Assert.Single(sent));
        var root = body.RootElement;
        Assert.Equal(options.Model, root.GetProperty("model").GetString());
        Assert.Equal(777, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(
            "json_schema",
            root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());
        Assert.False(root.TryGetProperty("temperature", out _));
        Assert.False(root.TryGetProperty("top_p", out _));
    }

    [Fact]
    public async Task Asks_nobody_when_there_is_no_key()
    {
        // The whole layer is optional, and "not configured" has to cost nothing and decide
        // nothing. Bring your own key, or the deterministic half runs alone.
        var source = Page("<p>anything</p>");
        var service = Answers("supported");
        var verifier = Verifier(source, service, key: null);

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Empty(source.Requests);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Stops_asking_once_the_run_budget_is_spent()
    {
        var source = Page("<p>Throughput rose by two fifths after the patch.</p>");
        var service = Answers("supported");
        var verifier = Verifier(source, service, options: ClaudeVerifierOptions.Default with { MaxCalls = 1 });
        var citation = Cited("https://example.com/report");

        var first = await verifier.VerifyAsync(Claimed("Throughput rose 40% after the change."), citation);
        var second = await verifier.VerifyAsync(Claimed("Latency fell 30% after the change."), citation);

        Assert.Equal(ClaimSupport.Supported, first.Support);
        Assert.Equal(ClaimSupport.Unknown, second.Support);
        Assert.Single(service.Requests);
        Assert.Single(source.Requests);
    }

    /// <summary>
    /// A source that will not answer until both callers have reached it, so a test can prove
    /// two claims were in flight together rather than hope they were.
    /// </summary>
    private sealed class Paired(CountdownEvent arrived) : HttpMessageHandler
    {
        public int Arrivals => 2 - arrived.CurrentCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            arrived.Signal();

            // Bounded so a thread pool that refuses to run the second caller fails the test
            // instead of hanging it; the arrival count below is what actually asserts the pair.
            arrived.Wait(TimeSpan.FromSeconds(5), cancellationToken);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<p>Throughput rose by two fifths after the patch.</p>", Encoding.UTF8, "text/html"),
            });
        }
    }

    [Fact]
    public async Task Two_claims_in_flight_cannot_both_spend_the_last_call()
    {
        // The budget check before the fetch is a courtesy that saves a pointless request; this
        // is the one that has to hold. Both claims are past that check before either has an
        // answer, so only the reservation taken immediately before the send can stop the
        // second one, and a plain "read it then write it" counter would let both through.
        using var arrived = new CountdownEvent(2);
        var source = new Paired(arrived);
        var service = Answers("supported");
        var verifier = new ClaudeClaimVerifier(
            new HttpClient(source),
            new HttpClient(service),
            Key,
            ClaudeVerifierOptions.Default with { MaxCalls = 1 });
        var citation = Cited("https://example.com/report");

        var verdicts = await Task.WhenAll(
            Task.Run(() => verifier.VerifyAsync(Claimed("Throughput rose 40%."), citation)),
            Task.Run(() => verifier.VerifyAsync(Claimed("Latency fell 30%."), citation)));

        Assert.Equal(2, source.Arrivals);
        Assert.Single(service.Requests);
        Assert.Single(verdicts, verdict => verdict.Support == ClaimSupport.Supported);
        Assert.Single(verdicts, verdict => verdict.Support == ClaimSupport.Unknown);
    }

    [Fact]
    public async Task Reports_unknown_when_the_service_refuses_the_key()
    {
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"), Replies(HttpStatusCode.Unauthorized));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Contains("401", verification.Reason);
    }

    [Fact]
    public async Task Says_to_run_it_again_later_when_the_service_is_busy()
    {
        // A rate limit and an overload are facts about the service, on the same reasoning the
        // registry lookup already uses: they describe the run, not the writing, so the reason
        // has to tell the author the one thing that helps.
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"), Replies((HttpStatusCode)529));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Contains("later", verification.Reason);
    }

    [Fact]
    public async Task Reports_unknown_when_the_answer_was_cut_short()
    {
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"),
            Answers("supported", "the page reports it", stopReason: "max_tokens"));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
    }

    [Fact]
    public async Task Reports_unknown_when_the_model_declines_to_answer()
    {
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"),
            Answers("supported", "the page reports it", stopReason: "refusal"));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
    }

    [Fact]
    public async Task Reports_unknown_when_the_answer_is_not_the_shape_that_was_asked_for()
    {
        var prose = JsonSerializer.Serialize(new
        {
            stop_reason = "end_turn",
            content = new[] { new { type = "text", text = "Looks about right to me." } },
        });

        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"),
            Replies(HttpStatusCode.OK, prose));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
    }

    [Fact]
    public async Task Reports_unknown_when_the_verdict_is_not_one_of_the_four()
    {
        // An unrecognised word is not a near miss to be rounded toward approval. It falls to
        // the value that says nothing, like every other answer this cannot read.
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"), Answers("probably", "hard to say"));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
    }

    [Fact]
    public async Task Reports_unknown_when_the_source_is_not_text_without_paying_for_a_call()
    {
        // Most of what a careful draft cites is a PDF, and a PDF is bytes this reads nothing
        // out of. Finding that out costs nothing.
        var service = Answers("supported");
        var verifier = Verifier(Page("%PDF-1.7", "application/pdf"), service);

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/paper.pdf"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Contains("application/pdf", verification.Reason);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Reports_unknown_when_the_source_carries_no_readable_text()
    {
        var service = Answers("supported");
        var verifier = Verifier(Page("<div><span></span></div>"), service);

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Reports_unknown_when_the_source_is_missing()
    {
        var service = Answers("supported");
        var verifier = Verifier(Replies(HttpStatusCode.NotFound), service);

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/gone"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Contains("404", verification.Reason);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Reports_a_failure_fetching_the_source_as_unknown()
    {
        var verifier = Verifier(
            new StubHandler(_ => throw new HttpRequestException("no such host")), Answers("supported"));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
    }

    [Fact]
    public async Task Reports_a_failure_reaching_the_model_as_unknown()
    {
        // The promise the whole layer rests on: whatever happens out there, the deterministic
        // verdict is what survives, and this returns an answer rather than throwing into it.
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"),
            new StubHandler(_ => throw new HttpRequestException("connection reset")));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.NotEmpty(verification.Reason);
    }

    [Fact]
    public async Task Skips_a_url_the_policy_rejects_without_sending_anything()
    {
        var source = Page("<p>anything</p>");
        var service = Answers("supported");
        var verifier = Verifier(source, service);

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."),
            Cited("http://169.254.169.254/latest/meta-data/"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Empty(source.Requests);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Caps_how_much_of_the_source_the_model_is_shown()
    {
        var sent = new List<string>();
        var verifier = Verifier(
            Page("<p>0123456789012345678 OMEGA</p>"),
            Answers("unknown", "not enough to go on", sent: sent),
            options: ClaudeVerifierOptions.Default with { MaxSourceCharacters = 20 });

        await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        var body = Assert.Single(sent);
        Assert.Contains("0123456789012345678", body);
        Assert.DoesNotContain("OMEGA", body);
    }

    [Fact]
    public async Task Caps_how_much_of_the_source_is_read_off_the_wire()
    {
        // A separate ceiling from the one above, and it is the one the similarity attempt did
        // not have: that capped what was analysed after the whole body was already in memory.
        var sent = new List<string>();
        var verifier = Verifier(
            Page("<p>" + new string('a', 40) + " OMEGA</p>"),
            Answers("unknown", "not enough to go on", sent: sent),
            options: ClaudeVerifierOptions.Default with { MaxDownloadCharacters = 30 });

        await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        var body = Assert.Single(sent);
        Assert.Contains(new string('a', 20), body);
        Assert.DoesNotContain("OMEGA", body);
    }

    [Fact]
    public async Task Collapses_the_reason_the_model_gives_back()
    {
        // The reason is printed next to a finding, and it is written by a model reading text a
        // draft pointed at. It gets to be one line of a report and nothing more.
        var verifier = Verifier(
            Page("<p>Throughput rose by two fifths.</p>"),
            Answers("supported", "  the page\n\n   reports the same rise  "));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal("the page reports the same rise", verification.Reason);
    }

    [Fact]
    public async Task Hands_back_the_claim_and_the_citation_it_was_given()
    {
        var claim = Claimed("Throughput rose 40% after the change.");
        var citation = new Citation("https://example.com/report", 3, 120, 146);
        var verifier = Verifier(Page("<p>Throughput rose by two fifths.</p>"), Answers("supported"));

        var verification = await verifier.VerifyAsync(claim, citation);

        Assert.Same(claim, verification.Claim);
        Assert.Same(citation, verification.Citation);
    }

    [Fact]
    public async Task Honours_a_cancelled_token_before_reaching_the_network()
    {
        var source = Page("<p>anything</p>");
        var service = Answers("supported");
        var verifier = Verifier(source, service);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."),
            Cited("https://example.com/report"),
            cancelled.Token));
        Assert.Empty(source.Requests);
        Assert.Empty(service.Requests);
    }
}
