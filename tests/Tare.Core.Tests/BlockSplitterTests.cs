using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

public class BlockSplitterTests
{
    [Fact]
    public void Splits_on_blank_lines()
    {
        var blocks = BlockSplitter.Split("First block line one.\nStill first.\n\nSecond block.\n");

        Assert.Equal(2, blocks.Count);
        Assert.Equal("First block line one.\nStill first.", blocks[0].Text);
        Assert.Equal("Second block.", blocks[1].Text);
    }

    [Fact]
    public void Tracks_one_based_line_spans()
    {
        var blocks = BlockSplitter.Split("alpha\n\n\nbeta\n");

        Assert.Equal(2, blocks.Count);
        Assert.Equal(1, blocks[0].StartLine);
        Assert.Equal(1, blocks[0].EndLine);
        Assert.Equal(4, blocks[1].StartLine);
    }

    [Fact]
    public void Ignores_whitespace_only_input()
    {
        Assert.Empty(BlockSplitter.Split("\n\n   \n"));
    }
}
