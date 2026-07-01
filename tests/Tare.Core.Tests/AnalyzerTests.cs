using Xunit;

namespace Tare.Core.Tests;

public class AnalyzerTests
{
    [Fact]
    public void Reports_ungrounded_claim_and_filler_with_spans()
    {
        var result = Analyzer.Analyze(
            "# Results\n\n" +
            "Engagement increased by 80% last year.\n\n" +
            "It is important to note that, at the end of the day, nothing changes.\n");

        var ruleIds = result.Findings.Select(f => f.RuleId).ToList();
        Assert.Contains(RuleIds.UngroundedClaim, ruleIds);
        Assert.Contains(RuleIds.Filler, ruleIds);
        Assert.All(result.Findings, f => Assert.True(f.EndChar > f.StartChar));
        Assert.Equal(Band.Slop, result.Band);
    }

    [Fact]
    public void Scores_more_ungrounded_claims_worse()
    {
        var cleaner = Analyzer.Analyze(
            "Revenue rose 20% according to the WHO report.\n\nCosts fell 15% last quarter.\n");
        var worse = Analyzer.Analyze(
            "Revenue rose 20% last month.\n\nCosts fell 15% last quarter.\n");

        Assert.True(worse.Score > cleaner.Score);
    }

    [Fact]
    public void Rates_a_grounded_document_clean_with_no_findings()
    {
        var result = Analyzer.Analyze("Adoption grew 30% per https://example.com/report.\n");

        Assert.Equal(Band.Clean, result.Band);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Orders_findings_by_block_then_offset()
    {
        var result = Analyzer.Analyze(
            "# Results\n\n" +
            "Engagement increased by 80% last year.\n\n" +
            "It is important to note that, at the end of the day, nothing changes.\n");

        var keys = result.Findings.Select(f => (f.BlockIndex, f.StartChar)).ToList();
        Assert.Equal(keys.OrderBy(k => k.BlockIndex).ThenBy(k => k.StartChar).ToList(), keys);
    }
}
