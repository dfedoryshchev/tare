namespace Tare.Core;

/// <summary>
/// A conservative list of filler phrases - stock transitions that add words without content.
/// Kept short on purpose; the fairness corpus (later) guards against false positives, and a
/// block with a concrete fact is protected by the facts-cannot-be-filler override.
/// </summary>
public static class FillerLexicon
{
    /// <summary>
    /// The built-in filler phrases (all lower-case). Config can extend this list via
    /// <see cref="TareOptions.Filler"/>, but the default set is always present.
    /// </summary>
    public static readonly IReadOnlyList<string> Default = new[]
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
    public static IReadOnlyList<string> Hits(string text, IReadOnlyList<string> phrases)
    {
        var lower = text.ToLowerInvariant();
        var hits = new List<string>();
        foreach (var phrase in phrases)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal))
            {
                hits.Add(phrase);
            }
        }

        return hits;
    }
}
