using System.Text.RegularExpressions;

namespace Tare.Core;

/// <summary>
/// Citation hygiene, not truth. A specific claim is "grounded" when it - or an adjacent
/// sentence in the same block, since citations often trail the claim - carries a source
/// signal: a link, a footnote marker, an inline citation, a named authority, a study
/// reference, or plain prose attribution. It does NOT check that the source exists or
/// actually supports the claim; those are later layers. Known blind spot by design: a
/// link-dressed but unsupported claim scores grounded here.
/// <para>
/// The prose-attribution rules exist because the first four signals all assume the writer
/// reaches for markup or for an institution's name. Clipped operational writing and the
/// formal register taught in English-as-a-second-language writing both attribute in
/// ordinary words instead ("according to the dashboard", "as recorded in the export", "per
/// the handover document"), and reading only the markup marks that prose down for the way
/// it sounds rather than for what it leaves unsourced. That is precisely the failure this
/// analyzer is supposed to be blind to.
/// </para>
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

        if (AttributionRule().IsMatch(text) || WitnessClauseRule().IsMatch(text))
        {
            return "attributes the claim to a named record";
        }

        if (SourceLocationRule().IsMatch(text))
        {
            return "says where the record can be found";
        }

        return null;
    }

    /// <summary>
    /// Evaluates every specific claim in a block. A claim is grounded when its own sentence,
    /// or an immediately adjacent sentence in the same block, carries a source signal.
    /// Claims exempted by <see cref="IsSelfReportedObservation"/> are left out of the list
    /// entirely rather than reported either way.
    /// </summary>
    public static IReadOnlyList<GroundingResult> Evaluate(Block block)
    {
        var sentences = SentenceSplitter.Split(block);
        var results = new List<GroundingResult>();

        for (var i = 0; i < sentences.Count; i++)
        {
            var kinds = ClaimExtractor.Classify(sentences[i].Text);
            if (kinds.Count == 0 || IsSelfReportedObservation(sentences[i].Text))
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

    /// <summary>
    /// Self-reported observation: the one claim class grounding deliberately does not apply to.
    /// <para>
    /// An incident log is a record of what the writer saw while they were watching the system
    /// ("paged at 02:14", "depth back to normal by 02:51"). There is no source to cite for
    /// having been there; the writer is the instrument, and the log is the primary record
    /// rather than a retelling of one. Asking for a citation turns every incident log, lab
    /// notebook and changelog into an ungrounded document, which punishes the most specific
    /// writing there is for being specific. So the answer is no: grounding is a check on
    /// borrowed facts, and a first-hand observation has not borrowed anything.
    /// </para>
    /// <para>
    /// "The writer was there" is not detectable from the text, so the rule is narrower than
    /// the argument: the only specific content in the sentence is a wall-clock time. A
    /// timestamp says when something happened, it does not quantify the world, and there is
    /// nothing in it for a source to support. The moment the sentence carries anything else
    /// the claim rules recognise - a statistic, a causal assertion, an appeal to authority -
    /// it is an ordinary claim again and grounding applies in full, so "queue depth hit
    /// 40,000 by 02:51" is not exempt and "paged at 02:14" is.
    /// </para>
    /// <para>
    /// Exempt claims leave the metric rather than counting as grounded. The grounding gap is
    /// the share of citable claims that cite nothing; recording a timestamp as "grounded"
    /// would assert that a source was found when none was ever wanted.
    /// </para>
    /// </summary>
    public static bool IsSelfReportedObservation(string text) =>
        ClockTimeRule().IsMatch(text)
        && ClaimExtractor.Classify(ClockTimeRule().Replace(text, " ")).Count == 0;

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

    // Prose attribution, leading form: an attributing marker pointing at a particular record.
    // "according to the same dashboard", "per the handover document", "as recorded in the
    // export", "measured by the counters exported to the metrics backend". The determiner is
    // required, so the referent has to be a thing the reader could go and open rather than a
    // gesture at the world, and the lookahead then drops the vague appeals ("according to the
    // experts", "per the research") for the same reason "studies show" is kept out of
    // AuthorityRule: a crowd is not a source. This does not verify that the named record says
    // what the claim says - neither does a URL, and that is what the later layers are for.
    [GeneratedRegex(
        @"\b(?:according to|as per|per|measured by|recorded by|reported by|taken from|sourced from|drawn from|as (?:\w+ ){0,3}?(?:recorded|reported|measured|logged|captured|documented|published|listed|described|written|set out|shown)(?: (?:in|by|on|at))?)\s+(?:the|our|its|their|this|that|these|those|my|his|her)\b(?![^.]{0,24}\b(?:experts?|studies|research|scientists?|sources?|analysts?|literature)\b)",
        RegexOptions.IgnoreCase)]
    private static partial Regex AttributionRule();

    // Prose attribution, trailing form: the record is named after the statement rather than
    // before it - "as the redelivery counter in the dashboard shows". Same determiner
    // requirement and same exclusion of vague appeals as AttributionRule; the clause is capped
    // so it cannot run the length of a paragraph and pick up an unrelated verb.
    [GeneratedRegex(
        @"\bas (?:the|our|its|their|this|that)\b(?![^.]{0,24}\b(?:experts?|studies|research|scientists?|sources?|analysts?|literature)\b)[^.,;]{0,60}? (?:shows?|showed|records?|recorded|indicates?|indicated|says?|said|reports?|reported|confirms?|confirmed)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex WitnessClauseRule();

    // The third prose shape: no attributing verb at all, just a pointer to where the record
    // lives. "The dashboard link is in the handover document." A reader knows exactly where to
    // check, which is the whole job of a citation, and the writing that uses this shape is the
    // writing that has a handover document rather than a bibliography.
    [GeneratedRegex(
        @"\b(?:links?|figures?|numbers?|data|records?|exports?|logs?|dashboards?|readings?|measurements?|breakdowns?|screenshots?|transcripts?|evidence)\b[^.]{0,30}?\b(?:is|are|lives?|sits?|can be found|is available|are available)\s+(?:in|on|at|under)\s+(?:the|our|its|their|this|that)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SourceLocationRule();

    // A wall-clock time: 02:14, 14:08, 09:31:07. Used only by IsSelfReportedObservation, which
    // explains why a timestamp is not the kind of number a source can back.
    [GeneratedRegex(@"\b\d{1,2}:\d{2}(?::\d{2})?\b")]
    private static partial Regex ClockTimeRule();
}
