using System.Text.Json;
using Xunit;

namespace Tare.Core.Tests;

public class BenchTests
{
    private static AnalysisResult Result(Band band, params string[] ruleIds) =>
        new(0.5, band, ruleIds
            .Select(id => new Finding(id, Severity.Warning, 0, 1, 1, 0, 1, "x"))
            .ToList());

    [Fact]
    public void Counts_a_labeled_rule_that_fires_as_a_hit()
    {
        var report = Bench.Score(
            [new BenchCase("a.md", Band.Slop, [RuleIds.UngroundedClaim])],
            [Result(Band.Slop, RuleIds.UngroundedClaim)]);

        Assert.Equal(1, report.TruePositives);
        Assert.Equal(0, report.FalsePositives);
        Assert.Equal(0, report.FalseNegatives);
        // the two rules that were neither labeled nor fired are the quiet ones
        Assert.Equal(2, report.TrueNegatives);
        Assert.Equal(1.0, report.Precision);
        Assert.Equal(1.0, report.Recall);
        Assert.Equal(0.0, report.FalsePositiveRate);
    }

    [Fact]
    public void Counts_an_unlabeled_rule_that_fires_as_a_false_positive()
    {
        var report = Bench.Score(
            [new BenchCase("a.md", Band.Clean, [])],
            [Result(Band.Clean, RuleIds.Filler)]);

        Assert.Equal(1, report.FalsePositives);
        Assert.Equal(0.0, report.Precision);
        // one of the three quiet-by-default rules spoke up
        Assert.Equal(1.0 / 3, report.FalsePositiveRate, 6);
        Assert.Equal([RuleIds.Filler], report.Outcomes[0].FalsePositives);
    }

    [Fact]
    public void Counts_a_labeled_rule_that_stays_silent_as_a_miss()
    {
        var report = Bench.Score(
            [new BenchCase("a.md", Band.Slop, [RuleIds.Restatement])],
            [Result(Band.Slop)]);

        Assert.Equal(1, report.FalseNegatives);
        Assert.Equal(0.0, report.Recall);
        Assert.Equal([RuleIds.Restatement], report.Outcomes[0].Missed);
    }

    [Fact]
    public void A_band_mismatch_is_a_regression()
    {
        var report = Bench.Score(
            [new BenchCase("a.md", Band.Slop, [])],
            [Result(Band.Clean)]);

        Assert.Single(report.Regressions);
        Assert.False(report.Outcomes[0].BandMatched);
        Assert.Equal(0, report.BandsMatched);
    }

    [Fact]
    public void A_known_gap_survives_its_band_mismatch()
    {
        var report = Bench.Score(
            [new BenchCase("a.md", Band.Slop, [RuleIds.Filler], KnownGap: "density is capped")],
            [Result(Band.Watch, RuleIds.Filler)]);

        Assert.Empty(report.Regressions);
        Assert.Equal(1, report.KnownGaps);
    }

    [Fact]
    public void A_known_gap_still_has_to_fire_its_labeled_rules()
    {
        // otherwise the gap could be "closed" by muting the rule instead of fixing the score
        var report = Bench.Score(
            [new BenchCase("a.md", Band.Slop, [RuleIds.Filler], KnownGap: "density is capped")],
            [Result(Band.Watch)]);

        Assert.Single(report.Regressions);
    }

    [Fact]
    public void Reports_perfect_ratios_for_an_empty_denominator()
    {
        // nothing labeled and nothing fired means nothing was got wrong, not a divide by zero
        var report = Bench.Score(
            [new BenchCase("a.md", Band.Clean, [])],
            [Result(Band.Clean)]);

        Assert.Equal(1.0, report.Precision);
        Assert.Equal(1.0, report.Recall);
        Assert.Equal(0.0, report.FalsePositiveRate);
        Assert.Equal(1.0, report.F1);
    }

    [Fact]
    public void Rejects_a_result_count_that_does_not_match_the_cases()
    {
        Assert.Throws<ArgumentException>(() => Bench.Score(
            [new BenchCase("a.md", Band.Clean, []), new BenchCase("b.md", Band.Clean, [])],
            [Result(Band.Clean)]));
    }

    [Fact]
    public void Parses_a_manifest_including_band_names_and_known_gaps()
    {
        var cases = BenchCase.FromJson("""
            { "cases": [
              { "file": "a.md", "band": "Watch", "rules": ["GROUND001"], "note": "n" },
              { "file": "b.md", "band": "Slop", "rules": [], "knownGap": "capped" }
            ] }
            """);

        Assert.Equal(2, cases.Count);
        Assert.Equal(Band.Watch, cases[0].Band);
        Assert.Equal([RuleIds.UngroundedClaim], cases[0].Rules);
        Assert.Null(cases[0].KnownGap);
        Assert.Equal("capped", cases[1].KnownGap);
        Assert.Empty(cases[1].Rules);
    }

    [Fact]
    public void Rejects_a_manifest_with_no_cases()
    {
        Assert.Throws<JsonException>(() => BenchCase.FromJson("""{ "cases": [] }"""));
    }

    [Fact]
    public void Scores_the_real_corpus_without_regressions()
    {
        var cases = BenchCase.FromJson(File.ReadAllText(Path.Combine(Corpus.Root, "manifest.json")));
        var results = cases
            .Select(c => Analyzer.Analyze(File.ReadAllText(Corpus.PathOf(c.File))))
            .ToList();

        var report = Bench.Score(cases, results);

        Assert.Empty(report.Regressions);
        Assert.Equal(1, report.KnownGaps);
    }
}
