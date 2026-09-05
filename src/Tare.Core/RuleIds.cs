namespace Tare.Core;

/// <summary>
/// Stable identifiers for the rules that raise findings. They are part of the tool's contract -
/// suppression keys and SARIF rule ids build on them later - so an existing id never changes
/// meaning.
/// </summary>
public static class RuleIds
{
    /// <summary>A specific claim carries no nearby source signal.</summary>
    public const string UngroundedClaim = "GROUND001";

    /// <summary>A prose block restates its heading or a prior paragraph with little novel content.</summary>
    public const string Restatement = "DENSITY001";

    /// <summary>A prose block leans on filler phrases.</summary>
    public const string Filler = "FILLER001";

    /// <summary>A cited source was read and does not back the claim it is cited for.</summary>
    public const string UnsupportedCitation = "CITE001";
}
