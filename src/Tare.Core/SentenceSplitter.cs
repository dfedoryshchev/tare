namespace Tare.Core;

/// <summary>
/// Splits prose block text into sentences. Deliberately naive: it breaks on a
/// <c>.</c> / <c>!</c> / <c>?</c> terminator that is followed by whitespace and a capital
/// letter (or the end of the block). Offsets stay aligned to the source so each sentence
/// round-trips via <c>source.Substring(StartChar, EndChar - StartChar)</c>.
/// </summary>
public static class SentenceSplitter
{
    public static IReadOnlyList<Sentence> Split(Block block)
    {
        var text = block.Text;
        var sentences = new List<Sentence>();
        var cursor = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('.' or '!' or '?'))
            {
                continue;
            }

            // TODO: abbreviations / decimals - "e.g.", "U.S.", "Dr.", "3.5%" split wrongly here.
            var next = i + 1;
            while (next < text.Length && char.IsWhiteSpace(text[next]))
            {
                next++;
            }

            var isBoundary = next >= text.Length || char.IsUpper(text[next]);
            if (!isBoundary)
            {
                continue;
            }

            Emit(block, cursor, i + 1, sentences);
            cursor = next;
            i = next - 1;
        }

        Emit(block, cursor, text.Length, sentences);
        return sentences;
    }

    private static void Emit(Block block, int relStart, int relEnd, List<Sentence> sink)
    {
        var text = block.Text;

        // trim surrounding whitespace while keeping the offsets aligned to the source
        while (relStart < relEnd && char.IsWhiteSpace(text[relStart]))
        {
            relStart++;
        }

        while (relEnd > relStart && char.IsWhiteSpace(text[relEnd - 1]))
        {
            relEnd--;
        }

        if (relEnd <= relStart)
        {
            return;
        }

        sink.Add(new Sentence(
            block.Index,
            text.Substring(relStart, relEnd - relStart),
            block.StartChar + relStart,
            block.StartChar + relEnd));
    }
}
