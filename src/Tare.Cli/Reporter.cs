using System.Globalization;
using System.Text;
using Tare.Core;

namespace Tare.Cli;

/// <summary>
/// Renders an <see cref="AnalysisResult"/> as a human-readable console report: a one-line
/// verdict, then findings grouped by block with a clickable <c>file:line</c> for each so a
/// reader can jump straight to the span. Formatting lives here, not in Core - the same
/// <see cref="AnalysisResult"/> feeds the future --json and web views.
/// </summary>
public static class Reporter
{
    public static string Render(string path, AnalysisResult result)
    {
        var sb = new StringBuilder();
        var score = result.Score.ToString("0.00", CultureInfo.InvariantCulture);
        sb.Append(path).Append("  -  ").Append(result.Band).Append(" (score ").Append(score).Append(")\n\n");

        if (result.Findings.Count == 0)
        {
            sb.Append("  no findings\n\n");
        }
        else
        {
            var block = -1;
            foreach (var finding in result.Findings)
            {
                if (finding.BlockIndex != block)
                {
                    block = finding.BlockIndex;
                    sb.Append("  block ").Append(block).Append('\n');
                }

                sb.Append("    ")
                    .Append(path).Append(':').Append(finding.StartLine)
                    .Append("  ").Append(finding.Severity.ToString().ToLowerInvariant())
                    .Append("  ").Append(finding.RuleId)
                    .Append("  ").Append(finding.Message)
                    .Append('\n');
            }

            sb.Append('\n');
        }

        sb.Append(result.Findings.Count).Append(" finding(s), band ").Append(result.Band).Append('\n');
        return sb.ToString();
    }
}
