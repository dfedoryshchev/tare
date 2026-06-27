using Xunit;

namespace Tare.Core.Tests;

public class TokenizerTests
{
    [Fact]
    public void Lowercases_and_drops_stopwords()
    {
        var tokens = Tokenizer.Tokenize("The Strategy and the Plan");

        Assert.Equal(new[] { "strategy", "plan" }, tokens);
    }

    [Fact]
    public void Light_stems_a_plural()
    {
        Assert.Equal("system", Assert.Single(Tokenizer.Tokenize("Systems")));
        Assert.Equal("box", Assert.Single(Tokenizer.Tokenize("boxes")));
    }
}
