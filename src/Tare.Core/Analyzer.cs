namespace Tare.Core;

/// <summary>
/// Pure orchestrator: turns a markdown document into an <see cref="AnalysisResult"/>. It parses
/// blocks, runs the grounding and density signals (with the facts-cannot-be-filler override),
/// blends their results into a single score with a band, and emits a span-level finding for
/// each problem. Rendering stays in the CLI; this returns data only.
/// </summary>
public static class Analyzer
{
    /// <summary>
    /// Analyses <paramref name="source"/> with the given <paramref name="options"/> (weights,
    /// band cutoffs, density thresholds, filler lexicon); passing none uses the calibrated
    /// defaults.
    /// </summary>
    public static AnalysisResult Analyze(string source, TareOptions? options = null)
    {
        options ??= TareOptions.Default;
        var blocks = MarkdownBlocker.Parse(source);
        var findings = new List<Finding>();

        var grounding = new List<GroundingResult>();
        foreach (var block in blocks)
        {
            if (!block.IsProse)
            {
                continue;
            }

            foreach (var result in GroundingSignal.Evaluate(block))
            {
                grounding.Add(result);
                if (result.Grounded)
                {
                    continue;
                }

                var sentence = result.Claim.Sentence;
                findings.Add(new Finding(
                    RuleIds.UngroundedClaim, Severity.Warning, block.Index,
                    LineOf(source, sentence.StartChar), LineOf(source, sentence.EndChar),
                    sentence.StartChar, sentence.EndChar,
                    "specific claim: " + result.Reason));
            }
        }

        var density = DensitySignal.Evaluate(blocks, options);
        foreach (var result in density)
        {
            if (!result.Flagged)
            {
                continue;
            }

            var block = blocks[result.BlockIndex];
            var (ruleId, message) = result.FillerHits.Count > 0
                ? (RuleIds.Filler, "filler phrasing: " + string.Join(", ", result.FillerHits))
                : (RuleIds.Restatement, "restates prior content with little novel detail");

            findings.Add(new Finding(
                ruleId, Severity.Info, block.Index,
                block.StartLine, block.EndLine, block.StartChar, block.EndChar, message));
        }

        findings.Sort(Order);
        var score = Score(grounding, density, options);
        return new AnalysisResult(score, ToBand(score, options), findings);
    }

    private static double Score(
        IReadOnlyList<GroundingResult> grounding, IReadOnlyList<DensityResult> density, TareOptions options)
    {
        var groundingGap = grounding.Count == 0
            ? 0.0
            : (double)grounding.Count(r => !r.Grounded) / grounding.Count;
        var densityRate = density.Count == 0
            ? 0.0
            : (double)density.Count(d => d.Flagged) / density.Count;
        return options.GroundingWeight * groundingGap + options.DensityWeight * densityRate;
    }

    private static Band ToBand(double score, TareOptions options) =>
        score >= options.SlopAt ? Band.Slop : score >= options.WatchAt ? Band.Watch : Band.Clean;

    // Deterministic report order: by block, then character offset, then rule id.
    private static int Order(Finding x, Finding y)
    {
        var byBlock = x.BlockIndex.CompareTo(y.BlockIndex);
        if (byBlock != 0)
        {
            return byBlock;
        }

        var byChar = x.StartChar.CompareTo(y.StartChar);
        return byChar != 0 ? byChar : string.CompareOrdinal(x.RuleId, y.RuleId);
    }

    private static int LineOf(string source, int charOffset)
    {
        var line = 1;
        var limit = Math.Min(charOffset, source.Length);
        for (var i = 0; i < limit; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
