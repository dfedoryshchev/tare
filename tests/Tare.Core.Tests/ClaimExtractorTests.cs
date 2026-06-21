using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

public class ClaimExtractorTests
{
    [Theory]
    [InlineData("Conversions rose by 40% last quarter.", ClaimKind.Number)]
    [InlineData("The product launched in September.", ClaimKind.Date)]
    [InlineData("Caching reduced the page latency sharply.", ClaimKind.Causal)]
    [InlineData("The new index is faster than a full scan.", ClaimKind.Comparative)]
    [InlineData("Studies show that readers skim long posts.", ClaimKind.Authority)]
    public void Flags_each_specific_claim_kind(string text, ClaimKind expected)
    {
        Assert.Contains(expected, ClaimExtractor.Classify(text));
    }

    [Fact]
    public void Does_not_flag_a_plain_sentence()
    {
        Assert.Empty(ClaimExtractor.Classify("The quiet fox walked along the river bank."));
    }

    [Fact]
    public void Returns_every_matched_kind_for_a_multi_signal_sentence()
    {
        var kinds = ClaimExtractor.Classify("Studies show a 50% increase in signups in 2020.");

        Assert.Contains(ClaimKind.Number, kinds);
        Assert.Contains(ClaimKind.Date, kinds);
        Assert.Contains(ClaimKind.Causal, kinds);
        Assert.Contains(ClaimKind.Authority, kinds);
    }

    [Fact]
    public void Extracts_only_claim_sentences_from_a_block()
    {
        var block = MarkdownBlocker.Parse("Birds sing at dawn. Revenue grew 12% in 2023.\n")[0];

        var claim = Assert.Single(ClaimExtractor.Extract(block));
        Assert.Contains("Revenue", claim.Sentence.Text);
        Assert.Contains(ClaimKind.Number, claim.Kinds);
    }
}
