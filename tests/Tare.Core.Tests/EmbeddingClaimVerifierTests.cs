using System.Net;
using System.Text;
using Tare.Core;
using Tare.Http;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// The similarity spike, exercised end to end without leaving the process: a stubbed handler
/// stands in for the page and an in-process model stands in for the embedding provider, so the
/// pipeline is measured and nobody's uptime or bill is.
/// <para>
/// Half of these tests are here to record what the approach cannot do. A test that pins a
/// wrong answer is not approval of it; it is the evidence for the paragraphs in
/// <see cref="EmbeddingClaimVerifier"/> that say this is a spike.
/// </para>
/// </summary>
public class EmbeddingClaimVerifierTests
{
    private static Claim Claimed(string text) =>
        new(new Sentence(0, text, 0, text.Length), new[] { ClaimKind.Number });

    private static Citation Cited(string url) => new(url, 0, 0, url.Length);

    private static StubHandler Page(string html, string mediaType = "text/html") =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, mediaType),
        });

    /// <summary>
    /// A model whose vectors are written by the test. It stands where a hosted embedding
    /// endpoint would stand, and it records what it was asked to embed so a test can count the
    /// calls a real provider would have charged for.
    /// </summary>
    private sealed class ScriptedModel(Func<string, float[]> vectors) : IEmbeddingModel
    {
        public List<string> Embedded { get; } = new();

        public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Embedded.Add(text);
            return Task.FromResult<IReadOnlyList<float>>(vectors(text));
        }
    }

    [Fact]
    public async Task Reports_supported_when_the_source_repeats_the_claim()
    {
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(Page("<p>Throughput rose 40% after the change, according to the load tests.</p>")),
            new HashedTokenStandIn());

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Supported, verification.Support);
        Assert.Contains("closest passage", verification.Reason);
    }

    [Fact]
    public async Task Reports_unknown_when_no_passage_comes_close()
    {
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(Page("<p>The harbour forecast is unchanged: light winds, rain by the weekend.</p>")),
            new HashedTokenStandIn());

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Contains("below", verification.Reason);
    }

    [Fact]
    public async Task Cannot_tell_a_denial_from_a_match()
    {
        // The finding that matters most, and it is a failure. The source flatly denies the
        // claim and the spike answers Supported, because a similarity score measures how close
        // two texts sit, and there is no step anywhere in this pipeline that separates
        // agreement from disagreement. The denial scores 0.80 here; the source that actually
        // repeats the claim, in the first test above, scores 0.79.
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(Page("<p>Latency did not fall by 30% after the rollout.</p>")),
            new HashedTokenStandIn());

        var verification = await verifier.VerifyAsync(
            Claimed("Latency fell by 30% after the rollout."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Supported, verification.Support);
    }

    [Fact]
    public async Task A_lexical_stand_in_misses_a_paraphrase()
    {
        // The stand-in earns its name here: same fact, different words, and it sees nothing -
        // 0.14, which is one hash collision rather than any reading of the sentence. This is
        // the one case a real embedding provider is bought for.
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(Page("<p>Requests served per second climbed by two fifths once the patch shipped.</p>")),
            new HashedTokenStandIn());

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
    }

    [Fact]
    public async Task A_model_that_places_a_paraphrase_near_the_claim_reaches_supported()
    {
        // Same texts as the test above, and the only thing that changed is who made the
        // vectors. The pipeline is not the weak part; the stand-in is.
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(Page("<p>Requests served per second climbed by two fifths once the patch shipped.</p>")),
            new ScriptedModel(_ => new[] { 1f, 0f }));

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Supported, verification.Support);
    }

    [Fact]
    public async Task Embeds_the_claim_once_and_every_passage_once()
    {
        // What one claim costs: one call for the claim plus one per window of the source.
        // Eight words, windows four wide two apart, is three windows and four calls - for a
        // single claim against a single short page.
        var model = new ScriptedModel(_ => new[] { 1f, 0f });
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(Page("<p>one two three four five six seven eight</p>")),
            model,
            EmbeddingVerifierOptions.Default with { PassageWords = 4, PassageStride = 2 });

        await verifier.VerifyAsync(Claimed("Throughput rose 40%."), Cited("https://example.com/report"));

        Assert.Equal(4, model.Embedded.Count);
        Assert.Equal("Throughput rose 40%.", model.Embedded[0]);
        Assert.Equal("one two three four", model.Embedded[1]);
        Assert.Equal("three four five six", model.Embedded[2]);
        Assert.Equal("five six seven eight", model.Embedded[3]);
    }

    [Fact]
    public async Task Reports_unknown_when_the_source_is_not_text()
    {
        // Most of what a careful draft cites is a PDF, and a PDF is bytes this reads nothing
        // out of. It costs no embedding call to find that out.
        var model = new ScriptedModel(_ => new[] { 1f, 0f });
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(Page("%PDF-1.7", "application/pdf")), model);

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/paper.pdf"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Contains("application/pdf", verification.Reason);
        Assert.Empty(model.Embedded);
    }

    [Fact]
    public async Task Reports_unknown_when_the_page_is_missing()
    {
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))),
            new HashedTokenStandIn());

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/gone"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Contains("404", verification.Reason);
    }

    [Fact]
    public async Task Reports_a_transport_failure_as_unknown()
    {
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(new StubHandler(_ => throw new HttpRequestException("no such host"))),
            new HashedTokenStandIn());

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
    }

    [Fact]
    public async Task Skips_a_url_the_policy_rejects_without_sending_anything()
    {
        // The spike fetches, so it inherits the same gate the existing source runs: a draft is
        // untrusted input and its links are not ours to follow anywhere.
        var handler = Page("<p>anything</p>");
        var model = new ScriptedModel(_ => new[] { 1f, 0f });
        var verifier = new EmbeddingClaimVerifier(new HttpClient(handler), model);

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."),
            Cited("http://169.254.169.254/latest/meta-data/"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.Empty(handler.Requests);
        Assert.Empty(model.Embedded);
    }

    [Fact]
    public async Task Hands_back_the_claim_and_the_citation_it_was_given()
    {
        var claim = Claimed("Throughput rose 40% after the change.");
        var citation = new Citation("https://example.com/report", 3, 120, 146);
        var verifier = new EmbeddingClaimVerifier(
            new HttpClient(Page("<p>Throughput rose 40% after the change.</p>")), new HashedTokenStandIn());

        var verification = await verifier.VerifyAsync(claim, citation);

        Assert.Same(claim, verification.Claim);
        Assert.Same(citation, verification.Citation);
    }

    [Fact]
    public async Task Honours_a_cancelled_token_before_reaching_the_network()
    {
        var handler = Page("<p>anything</p>");
        var verifier = new EmbeddingClaimVerifier(new HttpClient(handler), new HashedTokenStandIn());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifier.VerifyAsync(
            Claimed("Throughput rose 40% after the change."),
            Cited("https://example.com/report"),
            cancelled.Token));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_stand_in_gives_the_same_text_the_same_vector_every_run()
    {
        // Its hash is written out by hand rather than taken from the runtime, whose string
        // hashing is seeded per process. A spike whose verdict moved between runs would be
        // unarguable either way.
        var model = new HashedTokenStandIn();

        var first = await model.EmbedAsync("Throughput rose 40% after the change.");
        var second = await model.EmbedAsync("Throughput rose 40% after the change.");

        Assert.Equal(first, second);
    }
}
