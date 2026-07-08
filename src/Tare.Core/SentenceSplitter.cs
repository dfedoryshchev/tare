using System.Text.RegularExpressions;

namespace Tare.Core;

/// <summary>
/// Splits prose block text into sentences. It breaks on a <c>.</c> / <c>!</c> / <c>?</c>
/// terminator that is followed by whitespace and a capital letter (or the end of the block),
/// but a <c>.</c> inside a decimal (<c>3.5</c>) or closing a known abbreviation (<c>Dr.</c>,
/// <c>U.S.</c>, <c>e.g.</c>) is not treated as a boundary. Offsets stay aligned to the source
/// so each sentence round-trips via <c>source.Substring(StartChar, EndChar - StartChar)</c>.
/// </summary>
public static partial class SentenceSplitter
{
    // Common abbreviations whose trailing period is not a sentence end. Dotted acronyms and
    // Latin abbreviations (U.S., e.g., i.e., a.m.) are matched by pattern instead of listed.
    private static readonly HashSet<string> Abbreviations = new(StringComparer.Ordinal)
    {
        "dr.", "mr.", "mrs.", "ms.", "prof.", "sr.", "jr.", "st.",
        "vs.", "etc.", "no.", "fig.", "al.", "inc.", "ltd.", "co.", "cf.",
    };

    public static IReadOnlyList<Sentence> Split(Block block)
    {
        var text = block.Text;
        var sentences = new List<Sentence>();
        var cursor = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('.' or '!' or '?'))
            {
                continue;
            }

            if (text[i] == '.' && IsAbbreviationOrDecimal(text, i))
            {
                continue;
            }

            var next = i + 1;
            while (next < text.Length && char.IsWhiteSpace(text[next]))
            {
                next++;
            }

            var isBoundary = next >= text.Length || char.IsUpper(text[next]);
            if (!isBoundary)
            {
                continue;
            }

            Emit(block, cursor, i + 1, sentences);
            cursor = next;
            i = next - 1;
        }

        Emit(block, cursor, text.Length, sentences);
        return sentences;
    }

    private static void Emit(Block block, int relStart, int relEnd, List<Sentence> sink)
    {
        var text = block.Text;

        // trim surrounding whitespace while keeping the offsets aligned to the source
        while (relStart < relEnd && char.IsWhiteSpace(text[relStart]))
        {
            relStart++;
        }

        while (relEnd > relStart && char.IsWhiteSpace(text[relEnd - 1]))
        {
            relEnd--;
        }

        if (relEnd <= relStart)
        {
            return;
        }

        sink.Add(new Sentence(
            block.Index,
            text.Substring(relStart, relEnd - relStart),
            block.StartChar + relStart,
            block.StartChar + relEnd));
    }

    // A '.' that sits between two digits (a decimal) or closes a known / dotted abbreviation
    // is not a sentence boundary.
    private static bool IsAbbreviationOrDecimal(string text, int i)
    {
        if (i > 0 && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
        {
            return true;
        }

        var start = i;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        var token = text.Substring(start, i - start + 1).ToLowerInvariant();
        return Abbreviations.Contains(token) || DottedAcronym().IsMatch(token);
    }

    // Repeated single-letter-plus-dot groups: "u.s.", "e.g.", "i.e.", "a.m.".
    [GeneratedRegex(@"^(?:[a-z]\.)+$")]
    private static partial Regex DottedAcronym();
}
