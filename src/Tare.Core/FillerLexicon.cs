namespace Tare.Core;

/// <summary>
/// A conservative list of filler phrases - stock transitions that add words without content.
/// Kept short on purpose; the fairness corpus (later) guards against false positives, and a
/// block with a concrete fact is protected by the facts-cannot-be-filler override.
/// </summary>
public static class FillerLexicon
{
    private static readonly string[] Phrases =
    {
        "it is important to note",
        "in today's world",
        "at the end of the day",
        "when it comes to",
        "needless to say",
        "it goes without saying",
        "the fact of the matter is",
        "in the world of",
    };

    /// <summary>Returns the filler phrases present in the text (lowercased match), in order.</summary>
    public static IReadOnlyList<string> Hits(string text)
    {
        var lower = text.ToLowerInvariant();
        var hits = new List<string>();
        foreach (var phrase in Phrases)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal))
            {
                hits.Add(phrase);
            }
        }

        return hits;
    }
}
