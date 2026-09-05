namespace Tare.Core;

/// <summary>
/// Pure orchestrator: turns a markdown document into an <see cref="AnalysisResult"/>. It parses
/// blocks, runs the grounding and density signals (with the facts-cannot-be-filler override),
/// blends their results into a single score with a band, and emits a span-level finding for
/// each problem. Rendering stays in the CLI; this returns data only.
/// <para>
/// <see cref="AnalyzeAsync"/> adds the one optional layer on top: a caller that has an
/// <see cref="IClaimVerifier"/> gets the cited sources read as well. It is strictly additive -
/// it runs after the deterministic pass, it cannot move the score, and every way it can fail
/// leaves the report the synchronous call would have produced. Core stays pure either way:
/// the question and the vocabulary of answers live here, whoever answers it does not.
/// </para>
/// <para>
/// A verification finding is <see cref="Severity.Info"/>, below the ungrounded-claim warning.
/// The deterministic tier was calibrated against a labeled corpus and this one has no corpus
/// behind it, so it does not get to speak louder than the rules that do.
/// </para>
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

    /// <summary>
    /// Analyses <paramref name="source"/> exactly as <see cref="Analyze(string, TareOptions?)"/>
    /// does, then asks <paramref name="verifier"/> whether the sources those claims cite
    /// actually back them, and reports the ones that do not.
    /// <para>
    /// The deterministic report is produced first and is the value that comes back: the score
    /// and the band are carried over rather than recomputed, and the verifier can only append
    /// a finding. So a verifier that is absent, refuses, fails, times out or cannot decide
    /// leaves this identical to the synchronous call - the fallback is the shape of the method
    /// rather than a branch inside it.
    /// </para>
    /// <para>
    /// Pass <see cref="NoClaimVerifier.Instance"/> to keep the optional half switched off.
    /// </para>
    /// </summary>
    public static async Task<AnalysisResult> AnalyzeAsync(
        string source,
        IClaimVerifier verifier,
        TareOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var deterministic = Analyze(source, options);

        var findings = new List<Finding>(deterministic.Findings);
        var reported = false;

        foreach (var (claim, citation) in Cited(source))
        {
            var verification = await Verify(verifier, claim, citation, cancellationToken);
            if (Unsupported(verification.Support) is not { } verdict)
            {
                continue;
            }

            findings.Add(new Finding(
                RuleIds.UnsupportedCitation, Severity.Info, citation.BlockIndex,
                LineOf(source, citation.StartChar), LineOf(source, citation.EndChar),
                citation.StartChar, citation.EndChar,
                $"{verdict}: {citation.Url} - {verification.Reason}"));
            reported = true;
        }

        if (!reported)
        {
            return deterministic;
        }

        findings.Sort(Order);
        return deterministic with { Findings = findings };
    }

    /// <summary>
    /// The claim/citation pairs worth asking about: a specific claim and a URL inside that
    /// same sentence. Narrow on purpose - the writer put that link in that sentence, so the
    /// document itself asserted the pair, and nothing here has to guess which of a paragraph's
    /// links was meant to carry which number. It is also exactly the blind spot
    /// <see cref="GroundingSignal"/> documents: a link is enough to be counted grounded, and
    /// this is the only layer that reads what is on the other end of it.
    /// <para>
    /// Pairs come back in document order, because a verifier's call budget is spent in the
    /// order it is asked and a run that exhausts one should truncate reproducibly.
    /// </para>
    /// </summary>
    private static IReadOnlyList<(Claim Claim, Citation Citation)> Cited(string source)
    {
        var citations = CitationExtractor.Extract(source);
        if (citations.Count == 0)
        {
            return [];
        }

        // A second pass over the same pure functions rather than a value threaded out of the
        // deterministic one. It costs a re-parse and it buys the guarantee above: the optional
        // layer cannot reshape a pass it does not take part in.
        var pairs = new List<(Claim, Citation)>();
        foreach (var block in MarkdownBlocker.Parse(source))
        {
            if (!block.IsProse)
            {
                continue;
            }

            foreach (var result in GroundingSignal.Evaluate(block))
            {
                var sentence = result.Claim.Sentence;
                foreach (var citation in citations)
                {
                    if (citation.BlockIndex == block.Index
                        && citation.StartChar >= sentence.StartChar
                        && citation.EndChar <= sentence.EndChar)
                    {
                        pairs.Add((result.Claim, citation));
                    }
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// Asks one question and refuses to let the answer break the run. The port asks
    /// implementations to report failure rather than throw, but an implementation is somebody
    /// else's code and the promise cannot rest on it: anything thrown here is read as no
    /// answer, and the next claim is still asked about.
    /// <para>
    /// The one exception is a cancellation the caller asked for. That is not the verifier
    /// failing, it is the caller saying stop, so it propagates - the same split both citation
    /// ports already document.
    /// </para>
    /// </summary>
    private static async Task<ClaimVerification> Verify(
        IClaimVerifier verifier, Claim claim, Citation citation, CancellationToken cancellationToken)
    {
        try
        {
            return await verifier.VerifyAsync(claim, citation, cancellationToken);
        }
        catch (Exception failure)
            when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new ClaimVerification(
                claim, citation, ClaimSupport.Unknown, "the verifier failed: " + failure.Message);
        }
    }

    /// <summary>
    /// The verdicts that are worth a line in a report, and the words they get. A supported
    /// claim is the expected case and an <see cref="ClaimSupport.Unknown"/> one is not a
    /// verdict at all; findings are problems, so neither produces one.
    /// </summary>
    private static string? Unsupported(ClaimSupport support) => support switch
    {
        ClaimSupport.Contradicted => "the cited source contradicts this claim",
        ClaimSupport.Overstated => "the cited source is narrower than this claim",
        _ => null,
    };

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

    // Deterministic report order: by block, then character offset, then rule id, then message.
    // The last key is what makes it a total order rather than an almost-total one; List.Sort is
    // not stable, so two findings that tie on everything else would otherwise come back in
    // whichever order the sort happened to leave them.
    private static int Order(Finding x, Finding y)
    {
        var byBlock = x.BlockIndex.CompareTo(y.BlockIndex);
        if (byBlock != 0)
        {
            return byBlock;
        }

        var byChar = x.StartChar.CompareTo(y.StartChar);
        if (byChar != 0)
        {
            return byChar;
        }

        var byRule = string.CompareOrdinal(x.RuleId, y.RuleId);
        return byRule != 0 ? byRule : string.CompareOrdinal(x.Message, y.Message);
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
