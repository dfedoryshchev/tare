using System.Text.RegularExpressions;

namespace Tare.Core;

/// <summary>
/// Citation hygiene, not truth. A specific claim is "grounded" when it - or an adjacent
/// sentence in the same block, since citations often trail the claim - carries a source
/// signal: a link, a footnote marker, an inline citation, a named authority, or a study
/// reference. It does NOT check that the source exists or actually supports the claim; those
/// are later layers. Known blind spot by design: a link-dressed but unsupported claim scores
/// grounded here.
/// </summary>
public static partial class GroundingSignal
{
    private const int MinClaimsForReliability = 3;

    /// <summary>Returns a short reason if the text carries a source signal; null otherwise.</summary>
    public static string? Detect(string text)
    {
        if (LinkRule().IsMatch(text))
        {
            return "carries a link or URL";
        }

        if (FootnoteRule().IsMatch(text))
        {
            return "carries a footnote marker";
        }

        if (InlineCiteRule().IsMatch(text))
        {
            return "carries an inline citation";
        }

        if (AuthorityRule().IsMatch(text))
        {
            return "names an authority or study";
        }

        return null;
    }

    /// <summary>
    /// Evaluates every specific claim in a block. A claim is grounded when its own sentence,
    /// or an immediately adjacent sentence in the same block, carries a source signal.
    /// </summary>
    public static IReadOnlyList<GroundingResult> Evaluate(Block block)
    {
        var sentences = SentenceSplitter.Split(block);
        var results = new List<GroundingResult>();

        for (var i = 0; i < sentences.Count; i++)
        {
            var kinds = ClaimExtractor.Classify(sentences[i].Text);
            if (kinds.Count == 0)
            {
                continue;
            }

            var (grounded, reason) = Ground(sentences, i);
            results.Add(new GroundingResult(new Claim(sentences[i], kinds), grounded, reason));
        }

        return results;
    }

    /// <summary>
    /// Aggregates per-claim verdicts into the grounding-gap metric. <paramref name="minClaims"/>
    /// (from config) sets how few claims count as too few for the ratio to be reliable.
    /// </summary>
    public static GroundingGap Aggregate(
        IReadOnlyList<GroundingResult> results, int minClaims = MinClaimsForReliability)
    {
        var total = results.Count;
        if (total == 0)
        {
            return new GroundingGap(0, 0, 0.0, LowReliability: true);
        }

        var ungrounded = results.Count(r => !r.Grounded);
        return new GroundingGap(total, ungrounded, (double)ungrounded / total, total < minClaims);
    }

    private static (bool Grounded, string Reason) Ground(IReadOnlyList<Sentence> sentences, int index)
    {
        if (Detect(sentences[index].Text) is { } here)
        {
            return (true, here);
        }

        if (index > 0 && Detect(sentences[index - 1].Text) is { } before)
        {
            return (true, "preceding sentence " + before);
        }

        if (index + 1 < sentences.Count && Detect(sentences[index + 1].Text) is { } after)
        {
            return (true, "following sentence " + after);
        }

        return (false, "no source signal near the claim");
    }

    [GeneratedRegex(@"\]\([^)]*\)|https?://", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRule();

    [GeneratedRegex(@"\[\^[^\]]+\]")]
    private static partial Regex FootnoteRule();

    // (Author, 2024) - parens carrying a year - or a bracketed numeric reference like [12].
    [GeneratedRegex(@"\([^)]*\b(19|20)\d{2}\)|\[\d+\]")]
    private static partial Regex InlineCiteRule();

    // Named authorities and study references. "studies show" is deliberately NOT here: that is
    // a claim trigger (the vague appeal), whereas grounding wants a concrete, named source.
    [GeneratedRegex(
        @"\b(WHO|CDC|NIH|FDA|NASA|OECD|a study|the study|published in|et al\.?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex AuthorityRule();
}
