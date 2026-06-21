namespace Tare.Core;

/// <summary>A sentence that matched at least one <see cref="ClaimKind"/> rule.</summary>
public sealed record Claim(Sentence Sentence, IReadOnlyList<ClaimKind> Kinds);
