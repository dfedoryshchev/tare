using System.Text.Json;
using Xunit;

namespace Tare.Core.Tests;

public class JsonReportTests
{
    [Fact]
    public void Serializes_score_band_and_findings_with_stable_names()
    {
        var result = Analyzer.Analyze(
            "# Results\n\n" +
            "Engagement increased by 80% last year.\n");

        var json = JsonReport.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("score", out _));
        Assert.Equal(result.Band.ToString(), root.GetProperty("band").GetString());

        var findings = root.GetProperty("findings");
        Assert.Equal(result.Findings.Count, findings.GetArrayLength());

        var first = findings[0];
        Assert.Equal(result.Findings[0].RuleId, first.GetProperty("ruleId").GetString());
        Assert.Equal(result.Findings[0].Severity.ToString(), first.GetProperty("severity").GetString());
        Assert.True(first.TryGetProperty("blockIndex", out _));
        Assert.True(first.TryGetProperty("startLine", out _));
        Assert.True(first.TryGetProperty("message", out _));
    }
}
