using System.Text.Json;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// Runs the analyzer over the labeled corpus. The labels come from reading each case, not
/// from recording what the analyzer said, so these tests are the place a scoring change gets
/// argued with. A case carrying a <c>knownGap</c> is one the code currently gets wrong on
/// purpose: its band is not asserted, but it still has to fire the rules it is labeled with,
/// which stops a "fix" that simply silences the case from passing.
/// </summary>
public class CorpusTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var c in Corpus.Load())
        {
            data.Add(c.File);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case_lands_in_its_labeled_band(string file)
    {
        var c = Corpus.Single(file);
        var result = Analyzer.Analyze(File.ReadAllText(Corpus.PathOf(file)));

        if (c.KnownGap is not null)
        {
            Assert.NotEqual(c.Band, result.Band.ToString());
            return;
        }

        Assert.Equal(c.Band, result.Band.ToString());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case_fires_exactly_its_labeled_rules(string file)
    {
        var c = Corpus.Single(file);
        var result = Analyzer.Analyze(File.ReadAllText(Corpus.PathOf(file)));

        var fired = result.Findings.Select(f => f.RuleId).Distinct().Order().ToList();
        Assert.Equal(c.Rules.Order().ToList(), fired);
    }

    [Fact]
    public void Corpus_covers_every_band_and_every_rule()
    {
        var cases = Corpus.Load();

        Assert.Equal(
            new[] { "Clean", "Slop", "Watch" },
            cases.Select(c => c.Band).Distinct().Order().ToArray());
        Assert.Equal(
            new[] { RuleIds.Restatement, RuleIds.Filler, RuleIds.UngroundedClaim }.Order().ToArray(),
            cases.SelectMany(c => c.Rules).Distinct().Order().ToArray());
    }

    [Fact]
    public void Every_labeled_rule_id_is_a_real_rule()
    {
        var known = new[] { RuleIds.UngroundedClaim, RuleIds.Restatement, RuleIds.Filler };

        foreach (var c in Corpus.Load())
        {
            Assert.All(c.Rules, id => Assert.Contains(id, known));
        }
    }
}

/// <summary>Reads <c>corpus/manifest.json</c> and the case files beside it.</summary>
internal static class Corpus
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // The test binary runs from bin/<config>/<tfw>; the corpus lives at the repository root.
    internal static string Root { get; } = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "corpus"));

    private static readonly Lazy<IReadOnlyList<CorpusCase>> Cases = new(() =>
    {
        var manifest = Path.Combine(Root, "manifest.json");
        var doc = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifest), Options);
        return doc?.Cases ?? throw new InvalidOperationException($"empty corpus manifest: {manifest}");
    });

    internal static IReadOnlyList<CorpusCase> Load() => Cases.Value;

    internal static CorpusCase Single(string file) =>
        Load().Single(c => c.File == file);

    internal static string PathOf(string file) => Path.Combine(Root, "cases", file);

    private sealed record Manifest(List<CorpusCase> Cases);
}

internal sealed record CorpusCase(
    string File,
    string Band,
    List<string> Rules,
    string? KnownGap,
    string? Note);
