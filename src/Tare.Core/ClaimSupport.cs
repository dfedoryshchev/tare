namespace Tare.Core;

/// <summary>
/// What a verifier concluded about a claim and the source it points at. This is the second
/// citation question and it is strictly downstream of the first: asking whether a source
/// backs a claim only makes sense once <see cref="CitationStatus.Resolves"/> has said the
/// source is there at all.
/// <para>
/// <see cref="Unknown"/> is deliberately the zero value. An answer nobody gave must not
/// default to approval, and it plays the same part here that
/// <see cref="CitationStatus.Unreachable"/> plays one layer down: it describes the run, not
/// the writing, and never counts against the author.
/// </para>
/// </summary>
public enum ClaimSupport
{
    /// <summary>No usable answer: none was asked for, or the verifier could not give one.</summary>
    Unknown,

    /// <summary>The source says what the claim says it says.</summary>
    Supported,

    /// <summary>The source is on the subject and says the opposite.</summary>
    Contradicted,

    /// <summary>The source is on the subject but weaker than the claim made of it.</summary>
    Overstated,
}
