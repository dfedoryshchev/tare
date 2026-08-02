using System.Text.RegularExpressions;

namespace Tare.Core;

/// <summary>
/// Pulls the DOIs a document cites out of its prose. A DOI is the one citation shape worth
/// treating separately from a URL: it is supposed to outlive the page it points at, so a
/// registry can answer "does this work exist" long after the publisher has reorganised
/// their site and the link has rotted.
/// <para>
/// Recognises the three spellings that occur in practice - a bare <c>10.x/y</c>, a
/// <c>doi:</c> prefix, and a doi.org / dx.doi.org resolver URL - and normalises all of them
/// to the bare lowercase identifier. Fenced code is skipped for the same reason
/// <see cref="CitationExtractor"/> skips it: a DOI in a shell snippet is an argument, not a
/// source the author is standing behind.
/// </para>
/// </summary>
public static partial class DoiExtractor
{
    /// <summary>Characters a sentence leaves stuck to the end of an identifier.</summary>
    private const string TrailingPunctuation = ".,;:!?'\")]}";

    public static IReadOnlyList<Doi> Extract(string source)
    {
        var dois = new List<Doi>();
        foreach (var block in MarkdownBlocker.Parse(source))
        {
            if (block.Kind == BlockKind.CodeFence)
            {
                continue;
            }

            foreach (Match match in DoiRule().Matches(block.Text))
            {
                var identifier = match.Groups["doi"].Value.TrimEnd(TrailingPunctuation.ToCharArray());
                if (identifier.Length == 0)
                {
                    continue;
                }

                var start = block.StartChar + match.Index;
                dois.Add(new Doi(
                    identifier.ToLowerInvariant(), block.Index, start, start + match.Value.TrimEnd(TrailingPunctuation.ToCharArray()).Length));
            }
        }

        return dois;
    }

    /// <summary>
    /// The distinct works cited, in first-appearance order. One lookup per work is the
    /// point: a document citing the same paper six times should cost one request, not six,
    /// which matters more here than anywhere else because the registry rate-limits.
    /// </summary>
    public static IReadOnlyList<string> Distinct(IEnumerable<Doi> dois)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var doi in dois)
        {
            if (seen.Add(doi.Value))
            {
                order.Add(doi.Value);
            }
        }

        return order;
    }

    // The registrant prefix is always `10.` followed by four or more digits, then a slash and
    // a suffix - that shape is what separates a DOI from a version number or a ratio. The
    // optional resolver/`doi:` prefix is matched so the span covers what the reader sees,
    // while the `doi` group captures only the identifier itself.
    [GeneratedRegex(
        @"(?:https?://(?:dx\.)?doi\.org/|\bdoi:\s*)?(?<doi>10\.\d{4,}(?:\.\d+)*/[^\s""<>]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DoiRule();
}
