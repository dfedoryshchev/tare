using System.Globalization;
using System.Text;
using Tare.Core;

namespace Tare.Cli;

/// <summary>
/// Renders a <see cref="BenchReport"/>: the headline ratios first, then only the cases that
/// need a human. A run where nothing moved should print a handful of lines - if bench is
/// noisy on a good day nobody will read it on a bad one.
/// </summary>
public static class BenchReporter
{
    public static string Render(BenchReport report)
    {
        var sb = new StringBuilder();
        var cases = report.Outcomes.Count;

        sb.Append("corpus: ").Append(cases).Append(" cases, ")
            .Append(Bench.Rules.Count).Append(" rules\n\n");

        sb.Append("  precision  ").Append(Pct(report.Precision))
            .Append("   (").Append(report.TruePositives).Append(" hit, ")
            .Append(report.FalsePositives).Append(" false)\n");
        sb.Append("  recall     ").Append(Pct(report.Recall))
            .Append("   (").Append(report.FalseNegatives).Append(" missed)\n");
        sb.Append("  fp rate    ").Append(Pct(report.FalsePositiveRate))
            .Append("   (of ").Append(report.FalsePositives + report.TrueNegatives)
            .Append(" that should stay quiet)\n");
        sb.Append("  f1         ").Append(Pct(report.F1)).Append('\n');
        sb.Append("  bands      ").Append(report.BandsMatched).Append('/').Append(cases).Append('\n');

        var gaps = report.Outcomes.Where(o => o.Case.KnownGap is not null).ToList();
        if (gaps.Count > 0)
        {
            sb.Append("\nknown gaps (not counted against the run)\n");
            foreach (var gap in gaps)
            {
                sb.Append("  ").Append(gap.Case.File)
                    .Append("  labeled ").Append(gap.Case.Band)
                    .Append(", scored ").Append(gap.ActualBand)
                    .Append(' ').Append(Score(gap.Score)).Append('\n');
            }
        }

        var regressions = report.Regressions;
        if (regressions.Count == 0)
        {
            sb.Append("\nno regressions\n");
            return sb.ToString();
        }

        sb.Append('\n').Append(regressions.Count).Append(" regression(s)\n");
        foreach (var outcome in regressions)
        {
            sb.Append("  ").Append(outcome.Case.File).Append('\n');
            if (!outcome.BandMatched && outcome.Case.KnownGap is null)
            {
                sb.Append("    band      labeled ").Append(outcome.Case.Band)
                    .Append(", scored ").Append(outcome.ActualBand)
                    .Append(' ').Append(Score(outcome.Score)).Append('\n');
            }

            if (outcome.FalsePositives.Count > 0)
            {
                sb.Append("    fired     ").Append(string.Join(", ", outcome.FalsePositives))
                    .Append(" - not labeled\n");
            }

            if (outcome.Missed.Count > 0)
            {
                sb.Append("    silent    ").Append(string.Join(", ", outcome.Missed))
                    .Append(" - labeled but never fired\n");
            }
        }

        return sb.ToString();
    }

    private static string Pct(double value) =>
        (value * 100).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(5) + "%";

    private static string Score(double value) =>
        "(" + value.ToString("0.00", CultureInfo.InvariantCulture) + ")";
}
