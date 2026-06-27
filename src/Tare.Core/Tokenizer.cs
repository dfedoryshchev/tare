using System.Text.RegularExpressions;

namespace Tare.Core;

/// <summary>
/// Reduces prose to comparable content tokens: lowercased, punctuation-stripped, stopwords
/// removed, and lightly stemmed (trailing -ing/-ed/-es/-s). Deliberately crude - enough for
/// the overlap and novelty signals without pulling in an NLP dependency.
/// </summary>
public static partial class Tokenizer
{
    public static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        foreach (Match match in WordRule().Matches(text.ToLowerInvariant()))
        {
            var word = match.Value;
            if (word.Length < 2 || Stopwords.Contains(word))
            {
                continue;
            }

            tokens.Add(Stem(word));
        }

        return tokens;
    }

    // Strip a single trailing inflection, longest first. Length-guarded so short words are
    // left whole; this is intentionally lossy (see the density signal's known shortcuts).
    private static string Stem(string word)
    {
        foreach (var suffix in Suffixes)
        {
            if (word.Length > suffix.Length + 2 && word.EndsWith(suffix, StringComparison.Ordinal))
            {
                return word[..^suffix.Length];
            }
        }

        return word;
    }

    private static readonly string[] Suffixes = { "ing", "ed", "es", "s" };

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "and", "or", "but", "of", "to", "in", "on", "for", "with", "as",
        "at", "by", "be", "is", "are", "was", "were", "been", "it", "its", "this", "that",
        "these", "those", "from", "into", "we", "you", "they", "he", "she", "our", "your",
        "their", "his", "her", "them", "us", "me", "can", "will", "would", "should", "could",
        "may", "might", "must", "do", "does", "did", "has", "have", "had", "not", "no", "so",
        "if", "then", "than", "too", "very", "just", "also", "about", "there", "here", "when",
    };

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex WordRule();
}
