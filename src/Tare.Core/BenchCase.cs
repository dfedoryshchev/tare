using System.Text.Json;

namespace Tare.Core;

/// <summary>
/// One labeled corpus case: the document's file name, the band a reader would give it, and
/// the set of rule ids expected to fire somewhere in it. <see cref="KnownGap"/> holds the
/// reason a case is currently scored wrong on purpose - its band is reported but not counted
/// as a regression, while its rules still are. <see cref="KnownFalsePositives"/> does the
/// same job one level down, for a rule that fires on a case where it should not: the firing
/// is excused from the pass/fail verdict but still counted in the metrics, so the
/// false-positive rate keeps telling the truth about the tool.
/// </summary>
public sealed record BenchCase(
    string File,
    Band Band,
    IReadOnlyList<string> Rules,
    string? KnownGap = null,
    string? Note = null,
    IReadOnlyList<string>? KnownFalsePositives = null)
{
    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// Binds a corpus manifest document. Like <see cref="TareOptions.FromJson"/> this takes
    /// the config <em>text</em>, never a path, so the core keeps its no-IO promise; the CLI
    /// reads the file and hands the string over. Throws <see cref="JsonException"/> on
    /// malformed input rather than benching against a half-parsed corpus.
    /// </summary>
    public static IReadOnlyList<BenchCase> FromJson(string json)
    {
        var doc = JsonSerializer.Deserialize<ManifestDto>(json, ParseOptions);
        var cases = doc?.Cases;
        if (cases is null || cases.Count == 0)
        {
            throw new JsonException("corpus manifest has no cases");
        }

        return cases
            .Select(c => new BenchCase(
                c.File ?? throw new JsonException("corpus case is missing a file name"),
                c.Band,
                c.Rules ?? [],
                c.KnownGap,
                c.Note,
                c.KnownFalsePositives ?? []))
            .ToList();
    }

    private sealed record ManifestDto
    {
        public List<CaseDto>? Cases { get; init; }
    }

    private sealed record CaseDto
    {
        public string? File { get; init; }
        public Band Band { get; init; }
        public List<string>? Rules { get; init; }
        public string? KnownGap { get; init; }
        public string? Note { get; init; }
        public List<string>? KnownFalsePositives { get; init; }
    }
}
