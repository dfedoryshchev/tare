using System.Text.RegularExpressions;

namespace Tare.Core;

/// <summary>
/// Flags "specific claims" inside prose: sentences carrying numbers, dates, causal or
/// comparative assertions, or appeals to authority - the sentences a reader expects a
/// source to back. Deterministic, English-oriented rules; a sentence with no rule hit is
/// not a claim. The rule set lives here so it can later be made configurable.
/// </summary>
public static partial class ClaimExtractor
{
    public static IReadOnlyList<Claim> Extract(Block block)
    {
        var claims = new List<Claim>();
        foreach (var sentence in SentenceSplitter.Split(block))
        {
            var kinds = Classify(sentence.Text);
            if (kinds.Count > 0)
            {
                claims.Add(new Claim(sentence, kinds));
            }
        }

        return claims;
    }

    public static IReadOnlyList<ClaimKind> Classify(Sentence sentence) => Classify(sentence.Text);

    public static IReadOnlyList<ClaimKind> Classify(string text)
    {
        var kinds = new List<ClaimKind>();
        if (NumberRule().IsMatch(text))
        {
            kinds.Add(ClaimKind.Number);
        }

        if (DateRule().IsMatch(text))
        {
            kinds.Add(ClaimKind.Date);
        }

        if (CausalRule().IsMatch(text))
        {
            kinds.Add(ClaimKind.Causal);
        }

        if (ComparativeRule().IsMatch(text))
        {
            kinds.Add(ClaimKind.Comparative);
        }

        if (AuthorityRule().IsMatch(text))
        {
            kinds.Add(ClaimKind.Authority);
        }

        return kinds;
    }

    [GeneratedRegex(@"\d|%|\$")]
    private static partial Regex NumberRule();

    // Years plus full month names. Common-word collisions (may, march, august) are left out
    // here; abbreviated months are deferred with the decimal/abbreviation handling.
    [GeneratedRegex(
        @"\b(19|20)\d{2}\b|\b(january|february|april|june|july|september|october|november|december)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DateRule();

    [GeneratedRegex(
        @"\b(cause[sd]?|reduce[sd]?|increase[sd]?|decrease[sd]?|improve[sd]?|lead(s|ing)? to|result(s|ed|ing)? in|due to|because)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex CausalRule();

    [GeneratedRegex(
        @"\b(more|less|fewer|faster|slower|better|worse|best|worst|most|least|greater|higher|lower|\w+er than)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ComparativeRule();

    [GeneratedRegex(
        @"\b(studies show|study shows|research (suggests|shows)|experts? say|scientists? (say|found)|according to)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex AuthorityRule();
}
