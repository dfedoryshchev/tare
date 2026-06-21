namespace Tare.Core;

/// <summary>
/// Why a sentence reads as a *specific claim* - the kind of assertion a reader would expect
/// a source to back. A single sentence can match several kinds at once.
/// </summary>
public enum ClaimKind
{
    /// <summary>A number, percentage, or money amount.</summary>
    Number,

    /// <summary>A year or month - a datable assertion.</summary>
    Date,

    /// <summary>A cause/effect claim (reduces, increases, leads to, because).</summary>
    Causal,

    /// <summary>A comparison or superlative (faster, more, best).</summary>
    Comparative,

    /// <summary>An appeal to authority (studies show, according to, experts say).</summary>
    Authority,
}
