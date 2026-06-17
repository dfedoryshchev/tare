namespace Tare.Core;

/// <summary>
/// Splits a markdown document into structural blocks (headings, paragraphs, list items,
/// fenced code, block quotes) in a single forward pass, tracking 1-based line spans and
/// 0-based character offsets. This is a deliberate line-classifier rather than a full
/// CommonMark parser: it covers real drafts and keeps the core dependency-free. Each prose
/// block records the nearest preceding heading so later signals can score restatement.
/// </summary>
public static class MarkdownBlocker
{
    public static IReadOnlyList<Block> Parse(string source)
    {
        var lines = SplitLines(source);
        var blocks = new List<Block>();
        var index = 0;
        string? currentHeading = null;

        // Accumulator for the paragraph currently being collected (consecutive plain lines).
        var paraStart = -1;
        var paraEnd = -1;

        void FlushParagraph()
        {
            if (paraStart < 0) return;
            blocks.Add(MakeBlock(index++, BlockKind.Paragraph, lines, paraStart, paraEnd, source)
                with { Heading = currentHeading });
            paraStart = -1;
            paraEnd = -1;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Text;
            var trimmed = line.TrimStart();

            if (IsBlank(line))
            {
                FlushParagraph();
                continue;
            }

            if (IsFence(trimmed))
            {
                FlushParagraph();
                var marker = trimmed[0];
                var start = i;
                var j = i + 1;
                while (j < lines.Count && !IsFenceMarker(lines[j].Text.TrimStart(), marker))
                    j++;
                var end = j < lines.Count ? j : lines.Count - 1; // include the closing fence if present
                blocks.Add(MakeBlock(index++, BlockKind.CodeFence, lines, start, end, source));
                i = end;
                continue;
            }

            if (TryHeadingLevel(trimmed, out var level))
            {
                FlushParagraph();
                blocks.Add(MakeBlock(index++, BlockKind.Heading, lines, i, i, source)
                    with { HeadingLevel = level });
                currentHeading = trimmed.TrimStart('#').Trim();
                continue;
            }

            if (IsBlockQuote(trimmed))
            {
                FlushParagraph();
                var start = i;
                var j = i + 1;
                while (j < lines.Count && !IsBlank(lines[j].Text) && IsBlockQuote(lines[j].Text.TrimStart()))
                    j++;
                var end = j - 1;
                blocks.Add(MakeBlock(index++, BlockKind.BlockQuote, lines, start, end, source)
                    with { Heading = currentHeading });
                i = end;
                continue;
            }

            if (IsListItem(trimmed))
            {
                // Each list item is its own block; wrapped continuation lines are deferred (TODO).
                FlushParagraph();
                blocks.Add(MakeBlock(index++, BlockKind.ListItem, lines, i, i, source)
                    with { Heading = currentHeading });
                continue;
            }

            // Otherwise this line extends the current paragraph.
            if (paraStart < 0) paraStart = i;
            paraEnd = i;
        }

        FlushParagraph();
        return blocks;
    }

    // TODO: setext headings (===/--- underline), nested/indented lists, and 4-space indented
    // code blocks are not recognised yet; HTML blocks fall through to paragraphs.

    private static Block MakeBlock(
        int index, BlockKind kind,
        IReadOnlyList<(string Text, int Start, int End)> lines,
        int startLine, int endLine, string source)
    {
        var startChar = lines[startLine].Start;
        var endChar = lines[endLine].End;
        return new Block(index, kind, startLine + 1, endLine + 1, startChar, endChar,
            source.Substring(startChar, endChar - startChar));
    }

    /// <summary>
    /// Splits the source into physical lines, tracking each line's 0-based start offset and
    /// its end offset (exclusive of the line terminator and a trailing CR), so callers can
    /// map a block back to the exact source span.
    /// </summary>
    private static List<(string Text, int Start, int End)> SplitLines(string source)
    {
        var lines = new List<(string Text, int Start, int End)>();
        var pos = 0;
        var n = source.Length;

        while (pos <= n)
        {
            var nl = source.IndexOf('\n', pos);
            if (nl < 0)
            {
                if (pos < n)
                {
                    var end = n;
                    if (end > pos && source[end - 1] == '\r') end--;
                    lines.Add((source.Substring(pos, end - pos), pos, end));
                }
                break;
            }

            var lineEnd = nl;
            if (lineEnd > pos && source[lineEnd - 1] == '\r') lineEnd--;
            lines.Add((source.Substring(pos, lineEnd - pos), pos, lineEnd));
            pos = nl + 1;
        }

        return lines;
    }

    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static bool IsFence(string trimmed) =>
        trimmed.StartsWith("```", StringComparison.Ordinal) ||
        trimmed.StartsWith("~~~", StringComparison.Ordinal);

    private static bool IsFenceMarker(string trimmed, char marker) =>
        trimmed.StartsWith(new string(marker, 3), StringComparison.Ordinal);

    private static bool TryHeadingLevel(string trimmed, out int level)
    {
        level = 0;
        var hashes = 0;
        while (hashes < trimmed.Length && trimmed[hashes] == '#') hashes++;
        if (hashes is >= 1 and <= 6 && (hashes == trimmed.Length || trimmed[hashes] == ' '))
        {
            level = hashes;
            return true;
        }
        return false;
    }

    private static bool IsBlockQuote(string trimmed) => trimmed.StartsWith(">", StringComparison.Ordinal);

    private static bool IsListItem(string trimmed)
    {
        if (trimmed.Length >= 2 && trimmed[0] is '-' or '*' or '+' && trimmed[1] == ' ')
            return true;

        // Ordered list: one or more digits, then '.' or ')', then a space.
        var digits = 0;
        while (digits < trimmed.Length && char.IsDigit(trimmed[digits])) digits++;
        return digits > 0
            && digits + 1 < trimmed.Length
            && trimmed[digits] is '.' or ')'
            && trimmed[digits + 1] == ' ';
    }
}
