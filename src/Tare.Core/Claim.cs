namespace Tare.Core;

/// <summary>A sentence that matched at least one <see cref="ClaimKind"/> rule.</summary>
public sealed record Claim(Sentence Sentence, IReadOnlyList<ClaimKind> Kinds);

/// <summary>
/// What a verifier made of one <see cref="Claim"/> weighed against one <see cref="Citation"/>
/// it points at. Both are carried back so a caller that fired several questions at once can
/// tell the answers apart. <see cref="Reason"/> is the short human phrase reported next to the
/// verdict; it says why the verifier landed there, and it is the only part a reader can
/// argue with.
/// </summary>
public sealed record ClaimVerification(
    Claim Claim, Citation Citation, ClaimSupport Support, string Reason);
