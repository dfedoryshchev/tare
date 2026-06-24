using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

public class GroundingSignalTests
{
    private static Block Prose(string text) => MarkdownBlocker.Parse(text)[0];

    [Fact]
    public void Grounds_a_claim_carrying_a_link()
    {
        var result = Assert.Single(
            GroundingSignal.Evaluate(Prose("Signups rose 40% on our dashboard at https://example.com/stats.\n")));

        Assert.True(result.Grounded);
    }

    [Fact]
    public void Grounds_a_claim_from_a_following_sentence_citation()
    {
        var results = GroundingSignal.Evaluate(
            Prose("Latency dropped by 30%. The figure comes from a study published in 2019.\n"));

        Assert.True(results[0].Grounded);
        Assert.Contains("following", results[0].Reason);
    }

    [Fact]
    public void Leaves_a_bare_statistic_ungrounded()
    {
        var result = Assert.Single(
            GroundingSignal.Evaluate(Prose("Engagement increased by 80% last year.\n")));

        Assert.False(result.Grounded);
    }

    [Fact]
    public void Computes_the_grounding_gap_over_mixed_claims()
    {
        // a neutral sentence sits between the two claims so the link does not bleed across
        var block = Prose(
            "Costs fell 20% per https://example.com. Spring was pleasant that month. Revenue doubled in Q3.\n");

        var gap = GroundingSignal.Aggregate(GroundingSignal.Evaluate(block));

        Assert.Equal(2, gap.TotalClaims);
        Assert.Equal(1, gap.UngroundedClaims);
        Assert.Equal(0.5, gap.Gap);
        Assert.True(gap.LowReliability); // under the minimum claim count
    }

    [Fact]
    public void Reports_zero_gap_and_low_reliability_when_there_are_no_claims()
    {
        var gap = GroundingSignal.Aggregate(GroundingSignal.Evaluate(Prose("A calm river runs east.\n")));

        Assert.Equal(0, gap.TotalClaims);
        Assert.Equal(0.0, gap.Gap);
        Assert.True(gap.LowReliability);
    }
}
