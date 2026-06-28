using Xunit;

namespace Tare.Core.Tests;

public class DensitySignalTests
{
    private static IReadOnlyList<DensityResult> Evaluate(string source) =>
        DensitySignal.Evaluate(MarkdownBlocker.Parse(source));

    [Fact]
    public void Flags_a_paragraph_that_restates_the_previous_one()
    {
        var results = Evaluate(
            "Our system caches responses to cut latency and load.\n\n" +
            "Our system caches responses to cut latency and load.\n");

        Assert.False(results[0].Flagged); // the first paragraph has nothing to restate
        Assert.True(results[1].Flagged);
        Assert.True(results[1].PrevOverlap >= 0.5);
        Assert.True(results[1].NovelRatio <= 0.35);
    }

    [Fact]
    public void Reports_high_heading_overlap_when_a_block_echoes_its_heading()
    {
        var results = Evaluate("# Performance caching strategy\n\nPerformance caching strategy explained.\n");

        Assert.True(Assert.Single(results).HeadingOverlap >= 0.5);
    }

    [Fact]
    public void Flags_a_block_leaning_on_filler_phrases()
    {
        var result = Assert.Single(
            Evaluate("It is important to note that, at the end of the day, things move on.\n"));

        Assert.Equal(2, result.FillerHits.Count);
        Assert.True(result.Flagged);
    }

    [Fact]
    public void Leaves_a_substantive_paragraph_unflagged()
    {
        var result = Assert.Single(Evaluate("The migration moved forty tables onto the new schema.\n"));

        Assert.False(result.Flagged);
        Assert.Empty(result.FillerHits);
    }

    [Fact]
    public void Does_not_flag_filler_phrasing_that_carries_a_fact()
    {
        var result = Assert.Single(
            Evaluate("It is important to note that, at the end of the day, revenue grew 40%.\n"));

        Assert.False(result.Flagged);
        Assert.True(result.FactOverride);
    }

    [Fact]
    public void Still_flags_filler_phrasing_with_no_fact()
    {
        var result = Assert.Single(
            Evaluate("It is important to note that, at the end of the day, things move on.\n"));

        Assert.True(result.Flagged);
        Assert.False(result.FactOverride);
    }

    [Fact]
    public void Scores_only_prose_blocks()
    {
        var results = Evaluate("# Heading\n\nA real paragraph here.\n\n```\ncode();\n```\n");

        Assert.Single(results);
    }
}
