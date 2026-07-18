namespace Tare.Core;

/// <summary>
/// Flags low-substance prose: paragraphs that restate the nearest heading or the previous
/// paragraph, carry little novel content, or lean on filler phrases. Deterministic and
/// per-block; it needs the whole block sequence because novelty is measured against
/// everything seen so far and restatement against the previous prose block.
/// </summary>
public static class DensitySignal
{
    /// <summary>Evaluates every prose block with the default thresholds.</summary>
    public static IReadOnlyList<DensityResult> Evaluate(IReadOnlyList<Block> blocks) =>
        Evaluate(blocks, TareOptions.Default);

    /// <summary>
    /// Evaluates every prose block in document order (non-prose blocks are skipped) using the
    /// supplied thresholds and filler lexicon.
    /// </summary>
    public static IReadOnlyList<DensityResult> Evaluate(IReadOnlyList<Block> blocks, TareOptions options)
    {
        var results = new List<DensityResult>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<string>? prevProse = null;

        foreach (var block in blocks)
        {
            if (!block.IsProse)
            {
                continue;
            }

            var tokens = Tokenizer.Tokenize(block.Text);
            var headingOverlap = Jaccard(tokens, Tokenizer.Tokenize(block.Heading ?? string.Empty));
            var prevOverlap = prevProse is null ? 0.0 : Jaccard(tokens, prevProse);
            var novelRatio = NovelRatio(tokens, seen);
            var fillerHits = FillerLexicon.Hits(block.Text, options.Filler);

            var restates = Math.Max(headingOverlap, prevOverlap) >= options.HighOverlap
                && novelRatio <= options.LowNovelty;
            var flaggedByDensity = restates || fillerHits.Count >= options.MinFillerHits;

            // A block carrying a concrete fact is never pure filler, however stock its phrasing.
            // The override protects the filler verdict only; an ungrounded number is still a
            // grounding-gap finding elsewhere.
            var factOverride = flaggedByDensity && FactDetector.HasConcreteFact(block);
            var flagged = flaggedByDensity && !factOverride;

            results.Add(new DensityResult(
                block.Index, headingOverlap, prevOverlap, novelRatio, fillerHits, flagged, factOverride));

            foreach (var token in tokens)
            {
                seen.Add(token);
            }

            prevProse = tokens;
        }

        return results;
    }

    private static double Jaccard(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return 0.0;
        }

        var setA = new HashSet<string>(a, StringComparer.Ordinal);
        var setB = new HashSet<string>(b, StringComparer.Ordinal);
        var intersection = setA.Count(setB.Contains);
        var union = setA.Count + setB.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    private static double NovelRatio(IReadOnlyList<string> tokens, HashSet<string> seen)
    {
        if (tokens.Count == 0)
        {
            return 0.0;
        }

        var distinct = new HashSet<string>(tokens, StringComparer.Ordinal);
        var novel = distinct.Count(token => !seen.Contains(token));
        return (double)novel / distinct.Count;
    }
}
