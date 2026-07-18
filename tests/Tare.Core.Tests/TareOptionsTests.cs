using System.Text.Json;
using Xunit;

namespace Tare.Core.Tests;

public class TareOptionsTests
{
    [Fact]
    public void FromJson_applies_overrides_and_keeps_defaults_for_omitted_fields()
    {
        var options = TareOptions.FromJson(
            """{ "weights": { "grounding": 0.9 }, "bands": { "slop": 0.7 } }""");

        Assert.Equal(0.9, options.GroundingWeight); // overridden
        Assert.Equal(0.7, options.SlopAt);          // overridden
        Assert.Equal(0.4, options.DensityWeight);   // omitted -> default survives
        Assert.Equal(0.2, options.WatchAt);         // omitted -> default survives
        Assert.Equal(2, options.MinFillerHits);
    }

    [Fact]
    public void FromJson_on_an_empty_object_equals_the_defaults()
    {
        Assert.Equal(TareOptions.Default, TareOptions.FromJson("{}"));
    }

    [Fact]
    public void FromJson_on_malformed_input_throws()
    {
        Assert.Throws<JsonException>(() => TareOptions.FromJson("{ not json"));
    }

    [Fact]
    public void Custom_band_thresholds_reclassify_a_document()
    {
        const string doc = "Engagement increased by 80% last year.\n";

        // default thresholds land a lone ungrounded claim in Slop (score 0.6 >= 0.5)
        Assert.Equal(Band.Slop, Analyzer.Analyze(doc).Band);

        // raising the slop cutoff above 0.6 demotes the same document to Watch
        var lenient = TareOptions.FromJson("""{ "bands": { "slop": 0.8 } }""");
        Assert.Equal(Band.Watch, Analyzer.Analyze(doc, lenient).Band);
    }

    [Fact]
    public void Custom_weights_change_the_score()
    {
        const string doc = "Engagement increased by 80% last year.\n";

        var baseline = Analyzer.Analyze(doc).Score;
        var heavier = Analyzer.Analyze(doc, TareOptions.FromJson("""{ "weights": { "grounding": 1.0 } }""")).Score;

        Assert.True(heavier > baseline);
    }

    [Fact]
    public void Extra_filler_phrases_from_config_are_detected()
    {
        const string doc = "As we all know, the plan simply moves forward from here.\n";

        // the phrase is not in the built-in lexicon, so nothing fires by default
        Assert.DoesNotContain(RuleIds.Filler, Analyzer.Analyze(doc).Findings.Select(f => f.RuleId));

        // one config-supplied phrase plus a built-in one clears the min-filler-hits bar
        var extended = TareOptions.FromJson(
            """{ "density": { "minFillerHits": 1 }, "filler": ["as we all know"] }""");
        Assert.Contains(RuleIds.Filler, Analyzer.Analyze(doc, extended).Findings.Select(f => f.RuleId));
    }

    [Fact]
    public void Config_filler_extends_rather_than_replaces_the_builtin_lexicon()
    {
        var options = TareOptions.FromJson("""{ "filler": ["brand new filler"] }""");

        Assert.Contains("it is important to note", options.Filler); // built-in kept
        Assert.Contains("brand new filler", options.Filler);        // config added
    }

    [Fact]
    public void Config_min_claims_drives_grounding_reliability()
    {
        // two claims, one grounded: reliable under a min of 2, unreliable under the default of 3
        var block = MarkdownBlocker.Parse(
            "Costs fell 20% per https://example.com. Spring was pleasant. Revenue doubled in Q3.\n")[0];
        var results = GroundingSignal.Evaluate(block);

        Assert.False(GroundingSignal.Aggregate(results, minClaims: 2).LowReliability);
        Assert.True(GroundingSignal.Aggregate(results).LowReliability);
    }
}
