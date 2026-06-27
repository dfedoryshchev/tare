namespace Tare.Core;

/// <summary>
/// Per-block density verdict: how much a prose block restates its nearest heading or the
/// previous paragraph, how much novel content it carries, and which filler phrases it uses.
/// <see cref="Flagged"/> is set when overlap is high and novelty low, or when enough filler
/// phrases appear.
/// </summary>
public sealed record DensityResult(
    int BlockIndex,
    double HeadingOverlap,
    double PrevOverlap,
    double NovelRatio,
    IReadOnlyList<string> FillerHits,
    bool Flagged);
