using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// The second citation port, exercised entirely inside the process. Nothing here reaches a
/// network or a model: the point of the port is that the core can be asked the question and
/// can hand back an answer without knowing who answers it.
/// </summary>
public class ClaimVerifierTests
{
    private static Claim Claimed(string text) =>
        new(new Sentence(0, text, 0, text.Length), new[] { ClaimKind.Number });

    private static Citation Cited(string url) => new(url, 0, 0, url.Length);

    [Fact]
    public void An_answer_nobody_gave_is_unknown()
    {
        // The zero value has to be the one that says nothing. A verification that was never
        // performed must not read as approval of the claim.
        Assert.Equal(ClaimSupport.Unknown, default(ClaimSupport));
    }

    [Fact]
    public async Task No_verifier_answers_unknown()
    {
        var verification = await NoClaimVerifier.Instance.VerifyAsync(
            Claimed("Throughput rose 40% after the change."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Unknown, verification.Support);
        Assert.NotEmpty(verification.Reason);
    }

    [Fact]
    public async Task No_verifier_hands_back_the_claim_and_the_citation_it_was_given()
    {
        var claim = Claimed("Throughput rose 40% after the change.");
        var citation = Cited("https://example.com/report");

        var verification = await NoClaimVerifier.Instance.VerifyAsync(claim, citation);

        Assert.Same(claim, verification.Claim);
        Assert.Same(citation, verification.Citation);
    }

    [Fact]
    public async Task No_verifier_honours_a_cancelled_token()
    {
        // Same contract as the existing source port: an answer is an outcome, a cancellation
        // the caller asked for still propagates.
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NoClaimVerifier.Instance.VerifyAsync(
            Claimed("Throughput rose 40% after the change."),
            Cited("https://example.com/report"),
            cancelled.Token));
    }

    [Fact]
    public async Task A_verifier_written_outside_the_core_satisfies_the_port()
    {
        // The whole reason the port exists. This stub stands where the model-backed adapter
        // will stand, and the core needs nothing from it but the answer.
        IClaimVerifier verifier = new StubVerifier(ClaimSupport.Overstated);

        var verification = await verifier.VerifyAsync(
            Claimed("Throughput tripled."), Cited("https://example.com/report"));

        Assert.Equal(ClaimSupport.Overstated, verification.Support);
    }

    private sealed class StubVerifier(ClaimSupport support) : IClaimVerifier
    {
        public Task<ClaimVerification> VerifyAsync(
            Claim claim, Citation citation, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClaimVerification(claim, citation, support, "stubbed"));
    }
}
