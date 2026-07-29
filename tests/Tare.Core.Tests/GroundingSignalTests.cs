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
    public void Grounds_a_claim_attributed_in_plain_prose()
    {
        var results = GroundingSignal.Evaluate(Prose(
            "The hit rate is 87 percent, which is measured by the counters exported to the "
            + "metrics backend. Latency fell to 90ms, according to the same dashboard.\n"));

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Grounded));
    }

    [Fact]
    public void Grounds_a_claim_from_a_trailing_witness_clause()
    {
        var result = Assert.Single(GroundingSignal.Evaluate(Prose(
            "The cause was a poison message, as the redelivery counter in the dashboard shows.\n")));

        Assert.True(result.Grounded);
    }

    [Fact]
    public void Grounds_a_claim_from_a_sentence_saying_where_the_record_lives()
    {
        var results = GroundingSignal.Evaluate(Prose(
            "We moved 5 percent of traffic, then 50 percent the next day. The dashboard link "
            + "is in the handover document.\n"));

        Assert.True(results[0].Grounded);
    }

    [Fact]
    public void Leaves_a_vague_appeal_to_a_crowd_ungrounded()
    {
        // "according to the experts" attributes to nothing anyone can open, which is why it is
        // a claim trigger rather than a source
        var result = Assert.Single(GroundingSignal.Evaluate(Prose(
            "Onboarding takes 40% longer than it should, according to the experts.\n")));

        Assert.False(result.Grounded);
    }

    [Fact]
    public void Skips_a_sentence_whose_only_specific_content_is_a_clock_time()
    {
        // the writer was watching the system; there is no citation for having been there
        Assert.Empty(GroundingSignal.Evaluate(Prose("Paged at 02:14. Depth back to normal by 02:51.\n")));
    }

    [Fact]
    public void Still_demands_a_source_for_a_statistic_that_happens_to_carry_a_clock_time()
    {
        var result = Assert.Single(
            GroundingSignal.Evaluate(Prose("Queue depth hit 40,000 by 02:51.\n")));

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
