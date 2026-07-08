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

    [Fact]
    public void Keeps_a_decimal_with_a_percent_sign_in_one_sentence()
    {
        var sentences = SentenceSplitter.Split(Prose("Adoption grew 3.5% this quarter. It held.\n"));

        Assert.Equal(2, sentences.Count);
        Assert.Equal("Adoption grew 3.5% this quarter.", sentences[0].Text);
    }

    [Fact]
    public void Does_not_split_a_dotted_acronym()
    {
        var sentences = SentenceSplitter.Split(Prose("The U.S. economy grew last year. Prices rose sharply.\n"));

        Assert.Equal(2, sentences.Count);
        Assert.Equal("The U.S. economy grew last year.", sentences[0].Text);
    }

    [Fact]
    public void Does_not_split_a_title_abbreviation()
    {
        var sentences = SentenceSplitter.Split(Prose("Dr. Smith shared the results today. Everyone agreed.\n"));

        Assert.Equal(2, sentences.Count);
        Assert.Equal("Dr. Smith shared the results today.", sentences[0].Text);
    }

    [Fact]
    public void Does_not_split_a_latin_abbreviation_before_a_capital()
    {
        var sentences = SentenceSplitter.Split(Prose("We use a linter, e.g. Roslyn, on every build. It catches bugs.\n"));

        Assert.Equal(2, sentences.Count);
        Assert.Equal("We use a linter, e.g. Roslyn, on every build.", sentences[0].Text);
    }
}
