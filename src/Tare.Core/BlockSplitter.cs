namespace Tare.Core;

/// <summary>
/// Splits a document into blocks on blank lines.
/// This is the seed of the parser; a markdown-aware model (headings, lists,
/// code fences, block quotes) replaces the blank-line heuristic in a later pass.
/// </summary>
public static class BlockSplitter
{
    public static IReadOnlyList<Block> Split(string source)
    {
        var blocks = new List<Block>();
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var current = new List<string>();
        var startLine = 0;
        var index = 0;

        void Flush(int endLineExclusive)
        {
            if (current.Count == 0) return;
            var text = string.Join("\n", current).Trim();
            if (text.Length > 0)
                blocks.Add(new Block(index++, startLine + 1, endLineExclusive, text));
            current.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                Flush(i);
            }
            else
            {
                if (current.Count == 0) startLine = i;
                current.Add(lines[i]);
            }
        }

        Flush(lines.Length);
        return blocks;
    }
}
