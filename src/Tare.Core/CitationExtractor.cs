using System.Text.RegularExpressions;

namespace Tare.Core;

/// <summary>
/// Collects the URLs a document cites, with their spans. <see cref="GroundingSignal"/> only
/// asks whether a link is near a claim; this pulls the link out so a later layer can ask the
/// harder question - whether the thing on the other end is there at all.
/// <para>
/// Deliberately shape-blind: a markdown link target, an angle-bracket autolink and a bare
/// URL are all just a URL once extracted, and every one of them is a reader's route to the
/// source. Fenced code is skipped, because a URL in a shell snippet is an instruction rather
/// than a citation. Known limits, both cheap to live with: a URL containing brackets or
/// parentheses is clipped at the first one, and inline code spans are not treated as code.
/// </para>
/// <para>
/// Every occurrence is returned, including repeats of the same URL - the span is the point,
/// and a caller that wants one request per URL can group by <see cref="Citation.Url"/>.
/// </para>
/// </summary>
public static partial class CitationExtractor
{
    /// <summary>Characters a sentence tends to leave stuck to the end of a bare URL.</summary>
    private const string TrailingPunctuation = ".,;:!?'\"";

    public static IReadOnlyList<Citation> Extract(string source)
    {
        var citations = new List<Citation>();
        foreach (var block in MarkdownBlocker.Parse(source))
        {
            if (block.Kind == BlockKind.CodeFence)
            {
                continue;
            }

            foreach (Match match in UrlRule().Matches(block.Text))
            {
                var url = match.Value.TrimEnd(TrailingPunctuation.ToCharArray());
                var start = block.StartChar + match.Index;
                citations.Add(new Citation(url, block.Index, start, start + url.Length));
            }
        }

        return citations;
    }

    // http(s) only: those are the schemes a checker can resolve. The excluded characters are
    // the markdown delimiters a URL is usually wrapped in, so the match stops at the closing
    // paren of a link target or the closing angle bracket of an autolink.
    [GeneratedRegex(@"https?://[^\s<>()\[\]""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRule();
}
