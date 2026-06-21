using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

public class SentenceSplitterTests
{
    private static Block Prose(string text) => MarkdownBlocker.Parse(text)[0];

    [Fact]
    public void Splits_on_terminator_followed_by_a_capital()
    {
        var sentences = SentenceSplitter.Split(Prose("First sentence. Second one! Third one?\n"));

        Assert.Equal(3, sentences.Count);
        Assert.Equal("First sentence.", sentences[0].Text);
        Assert.Equal("Second one!", sentences[1].Text);
        Assert.Equal("Third one?", sentences[2].Text);
    }

    [Fact]
    public void Treats_a_block_without_a_terminator_as_one_sentence()
    {
        var sentences = SentenceSplitter.Split(Prose("no trailing period here\n"));

        var only = Assert.Single(sentences);
        Assert.Equal("no trailing period here", only.Text);
    }

    [Fact]
    public void Char_offsets_round_trip_to_the_source_text()
    {
        const string source = "Alpha beta gamma. Delta epsilon done.\n";
        var sentences = SentenceSplitter.Split(Prose(source));

        Assert.NotEmpty(sentences);
        foreach (var sentence in sentences)
        {
            Assert.Equal(
                sentence.Text,
                source.Substring(sentence.StartChar, sentence.EndChar - sentence.StartChar));
        }
    }

    [Fact]
    public void Does_not_split_a_decimal_or_lowercase_continuation()
    {
        // a lowercase letter after the period is not a boundary for the naive splitter
        var sentences = SentenceSplitter.Split(Prose("the rate is 3.5 percent today\n"));

        Assert.Single(sentences);
    }
}
