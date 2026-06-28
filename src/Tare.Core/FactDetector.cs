using System.Text.RegularExpressions;

namespace Tare.Core;

/// <summary>
/// Detects whether a block carries a concrete, checkable fact - a number, a date, or a proper
/// noun used mid-sentence. Reuses the specific-claim number/date rules and adds a crude
/// capitalized-token check. This backs the facts-cannot-be-filler override: a block with real
/// content should never be dismissed as pure filler.
/// </summary>
public static partial class FactDetector
{
    public static bool HasConcreteFact(Block block) => HasConcreteFact(block.Text);

    public static bool HasConcreteFact(string text)
    {
        var kinds = ClaimExtractor.Classify(text);
        if (kinds.Contains(ClaimKind.Number) || kinds.Contains(ClaimKind.Date))
        {
            return true;
        }

        return HasProperNoun(text);
    }

    // A capitalized word that is not sentence-initial reads as a named entity. Crude by design
    // (sentence-initial capitals and title-case can still slip through); tightened later if the
    // fairness corpus shows false positives.
    private static bool HasProperNoun(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var sentenceStart = true;

        foreach (var raw in words)
        {
            var word = raw.Trim(Trim);
            if (!sentenceStart && ProperNoun().IsMatch(word))
            {
                return true;
            }

            sentenceStart = raw.EndsWith('.') || raw.EndsWith('!') || raw.EndsWith('?');
        }

        return false;
    }

    private static readonly char[] Trim = { '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'' };

    [GeneratedRegex(@"^[A-Z][a-z]+")]
    private static partial Regex ProperNoun();
}
