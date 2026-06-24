namespace Tare.Core;

/// <summary>The grounding verdict for a single specific <see cref="Claim"/>.</summary>
public sealed record GroundingResult(Claim Claim, bool Grounded, string Reason);

/// <summary>
/// Aggregate citation-hygiene metric: the share of specific claims that carry no source
/// signal. <see cref="Gap"/> is 0 when there are no claims; <see cref="LowReliability"/> is
/// set when there are too few claims for the ratio to mean much.
/// </summary>
public sealed record GroundingGap(int TotalClaims, int UngroundedClaims, double Gap, bool LowReliability);
