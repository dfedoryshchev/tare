namespace Tare.Core;

/// <summary>
/// Scores the analyzer against a labeled corpus. Every rule is treated as a separate yes/no
/// prediction per document, which is what makes precision and recall mean anything here: a
/// document is not "slop or not", it is a set of rules that should or should not have fired.
/// Pure - it takes cases and the results of analysing them, and never reads a file.
/// </summary>
public static class Bench
{
    /// <summary>The rules bench measures. A new rule id belongs here the day it ships.</summary>
    public static readonly IReadOnlyList<string> Rules =
        [RuleIds.UngroundedClaim, RuleIds.Restatement, RuleIds.Filler];

    /// <summary>
    /// Pairs each case with its analysis and returns the aggregate report. The two lists are
    /// positional: <paramref name="results"/>[i] must be the analysis of <paramref name="cases"/>[i].
    /// </summary>
    public static BenchReport Score(
        IReadOnlyList<BenchCase> cases, IReadOnlyList<AnalysisResult> results)
    {
        if (cases.Count != results.Count)
        {
            throw new ArgumentException(
                $"got {results.Count} results for {cases.Count} cases", nameof(results));
        }

        var outcomes = new List<CaseOutcome>(cases.Count);
        int tp = 0, fp = 0, fn = 0, tn = 0;

        for (var i = 0; i < cases.Count; i++)
        {
            var expected = cases[i].Rules.ToHashSet(StringComparer.Ordinal);
            var fired = results[i].Findings.Select(f => f.RuleId).ToHashSet(StringComparer.Ordinal);

            foreach (var rule in Rules)
            {
                var want = expected.Contains(rule);
                var got = fired.Contains(rule);
                if (want && got) tp++;
                else if (!want && got) fp++;
                else if (want && !got) fn++;
                else tn++;
            }

            outcomes.Add(new CaseOutcome(
                cases[i],
                results[i].Band,
                results[i].Score,
                fired.Except(expected, StringComparer.Ordinal).Order().ToList(),
                expected.Except(fired, StringComparer.Ordinal).Order().ToList()));
        }

        return new BenchReport(outcomes, tp, fp, fn, tn);
    }
}

/// <summary>What the analyzer did with one case, against what the label asked for.</summary>
public sealed record CaseOutcome(
    BenchCase Case,
    Band ActualBand,
    double Score,
    IReadOnlyList<string> FalsePositives,
    IReadOnlyList<string> Missed)
{
    /// <summary>True when the band came out as labeled.</summary>
    public bool BandMatched => ActualBand == Case.Band;

    /// <summary>True when exactly the labeled rules fired - no extras, nothing missing.</summary>
    public bool RulesMatched => FalsePositives.Count == 0 && Missed.Count == 0;

    /// <summary>
    /// False positives the corpus already knows about and has not fixed yet. Anything outside
    /// this set is a new one.
    /// </summary>
    public IReadOnlyList<string> UnexpectedFalsePositives =>
        FalsePositives.Except(Case.KnownFalsePositives ?? [], StringComparer.Ordinal).ToList();

    /// <summary>
    /// A regression is a miss, a false positive the corpus has not already declared, or a
    /// band mismatch on a case that is not a known gap. Declared gaps are expected to score
    /// wrong until the gap is closed, so they are reported without failing the run - and they
    /// are still counted in precision and the false-positive rate, which is the number that
    /// has to move before the declaration can be deleted.
    /// </summary>
    public bool Regressed =>
        Missed.Count > 0
        || UnexpectedFalsePositives.Count > 0
        || (!BandMatched && Case.KnownGap is null);
}

/// <summary>
/// Corpus-wide totals. Counts are over rule predictions (cases x rules), not documents, so
/// the ratios describe the signals rather than the verdicts.
/// </summary>
public sealed record BenchReport(
    IReadOnlyList<CaseOutcome> Outcomes,
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    int TrueNegatives)
{
    /// <summary>Of the rules that fired, the share that should have.</summary>
    public double Precision => Ratio(TruePositives, TruePositives + FalsePositives);

    /// <summary>Of the rules that should have fired, the share that did.</summary>
    public double Recall => Ratio(TruePositives, TruePositives + FalseNegatives);

    /// <summary>
    /// The share of clean prose that got flagged anyway. This is the number the tool is
    /// judged on: a de-slop check that cries wolf is worse than no check.
    /// </summary>
    public double FalsePositiveRate => Ratio(FalsePositives, FalsePositives + TrueNegatives);

    /// <summary>Harmonic mean of precision and recall.</summary>
    public double F1 => Precision + Recall == 0
        ? 0.0
        : 2 * Precision * Recall / (Precision + Recall);

    /// <summary>Documents whose band came out as labeled.</summary>
    public int BandsMatched => Outcomes.Count(o => o.BandMatched);

    /// <summary>Documents parked as known gaps, excluded from the pass/fail verdict.</summary>
    public int KnownGaps => Outcomes.Count(o => o.Case.KnownGap is not null);

    /// <summary>Documents that broke their label in a way the corpus does not already excuse.</summary>
    public IReadOnlyList<CaseOutcome> Regressions =>
        Outcomes.Where(o => o.Regressed).ToList();

    // An undefined ratio (nothing in the denominator) reports as 1.0: nothing was got wrong.
    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 1.0 : (double)numerator / denominator;
}
