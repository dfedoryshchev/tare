namespace Tare.Core;

/// <summary>
/// The full analysis of a document: an overall <see cref="Score"/> in [0, 1] (0 is clean,
/// 1 is heavy slop), the <see cref="Band"/> that score falls in, and every span-level
/// <see cref="Finding"/> in deterministic order (by block, then character offset).
/// </summary>
public sealed record AnalysisResult(double Score, Band Band, IReadOnlyList<Finding> Findings);
