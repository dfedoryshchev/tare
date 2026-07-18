using System.Text.Json;

namespace Tare.Core;

/// <summary>
/// The knobs that used to be scattered as private constants across the signals and the
/// scorer: the two score weights, the band cutoffs, the density thresholds, the minimum
/// claim count for a reliable grounding gap, and any extra filler phrases. Passing an
/// options value into <see cref="Analyzer.Analyze(string, TareOptions?)"/> makes tuning a
/// data change, not a recompile - and keeps <c>Tare.Core</c> pure: this binds a config
/// <em>string</em>, never touching the filesystem. The CLI reads <c>tare.json</c> and hands
/// the text to <see cref="FromJson"/>; that separation mirrors <see cref="JsonReport"/> on
/// the way out.
/// </summary>
public sealed record TareOptions
{
    /// <summary>Share of the score driven by the grounding gap.</summary>
    public double GroundingWeight { get; init; } = 0.6;

    /// <summary>Share of the score driven by the density/restatement rate.</summary>
    public double DensityWeight { get; init; } = 0.4;

    /// <summary>Score at or above which a document lands in <see cref="Band.Watch"/>.</summary>
    public double WatchAt { get; init; } = 0.2;

    /// <summary>Score at or above which a document lands in <see cref="Band.Slop"/>.</summary>
    public double SlopAt { get; init; } = 0.5;

    /// <summary>Heading/previous-paragraph overlap at or above which a block reads as a restatement.</summary>
    public double HighOverlap { get; init; } = 0.5;

    /// <summary>Novel-content ratio at or below which a restating block is flagged.</summary>
    public double LowNovelty { get; init; } = 0.35;

    /// <summary>Number of filler-phrase hits at or above which a block is flagged on filler alone.</summary>
    public int MinFillerHits { get; init; } = 2;

    /// <summary>Fewest claims for a grounding gap to be treated as reliable.</summary>
    public int MinClaimsForReliability { get; init; } = 3;

    /// <summary>The filler lexicon in effect: the built-in phrases plus any from config.</summary>
    public IReadOnlyList<string> Filler { get; init; } = FillerLexicon.Default;

    /// <summary>The calibrated defaults, used whenever no config is supplied.</summary>
    public static TareOptions Default { get; } = new();

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Binds a <c>tare.json</c> document onto the defaults: any field the config omits keeps
    /// its default, so a partial config is enough to nudge one threshold. A <c>filler</c>
    /// array extends the built-in lexicon (lower-cased to match); it never replaces it.
    /// Throws <see cref="JsonException"/> on malformed input - the CLI turns that into a
    /// clean error, rather than analysing with half-applied settings.
    /// </summary>
    public static TareOptions FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<ConfigDto>(json, ParseOptions);
        if (dto is null)
        {
            return Default;
        }

        var d = Default;
        var filler = dto.Filler is { Count: > 0 }
            ? d.Filler.Concat(dto.Filler.Select(p => p.ToLowerInvariant())).ToList()
            : d.Filler;

        return d with
        {
            GroundingWeight = dto.Weights?.Grounding ?? d.GroundingWeight,
            DensityWeight = dto.Weights?.Density ?? d.DensityWeight,
            WatchAt = dto.Bands?.Watch ?? d.WatchAt,
            SlopAt = dto.Bands?.Slop ?? d.SlopAt,
            HighOverlap = dto.Density?.HighOverlap ?? d.HighOverlap,
            LowNovelty = dto.Density?.LowNovelty ?? d.LowNovelty,
            MinFillerHits = dto.Density?.MinFillerHits ?? d.MinFillerHits,
            MinClaimsForReliability = dto.Grounding?.MinClaims ?? d.MinClaimsForReliability,
            Filler = filler,
        };
    }

    // Nullable mirror of the tare.json schema: a missing section or field stays null and the
    // corresponding default survives the merge above.
    private sealed record ConfigDto
    {
        public WeightsDto? Weights { get; init; }
        public BandsDto? Bands { get; init; }
        public DensityDto? Density { get; init; }
        public GroundingDto? Grounding { get; init; }
        public List<string>? Filler { get; init; }
    }

    private sealed record WeightsDto
    {
        public double? Grounding { get; init; }
        public double? Density { get; init; }
    }

    private sealed record BandsDto
    {
        public double? Watch { get; init; }
        public double? Slop { get; init; }
    }

    private sealed record DensityDto
    {
        public double? HighOverlap { get; init; }
        public double? LowNovelty { get; init; }
        public int? MinFillerHits { get; init; }
    }

    private sealed record GroundingDto
    {
        public int? MinClaims { get; init; }
    }
}
