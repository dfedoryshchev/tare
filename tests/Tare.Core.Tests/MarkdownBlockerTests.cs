using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

public class MarkdownBlockerTests
{
    [Fact]
    public void Splits_paragraphs_on_blank_lines()
    {
        var blocks = MarkdownBlocker.Parse("First block line one.\nStill first.\n\nSecond block.\n");

        Assert.Equal(2, blocks.Count);
        Assert.Equal("First block line one.\nStill first.", blocks[0].Text);
        Assert.Equal("Second block.", blocks[1].Text);
        Assert.All(blocks, b => Assert.Equal(BlockKind.Paragraph, b.Kind));
    }

    [Fact]
    public void Tracks_one_based_line_spans()
    {
        var blocks = MarkdownBlocker.Parse("alpha\n\n\nbeta\n");

        Assert.Equal(2, blocks.Count);
        Assert.Equal(1, blocks[0].StartLine);
        Assert.Equal(1, blocks[0].EndLine);
        Assert.Equal(4, blocks[1].StartLine);
    }

    [Fact]
    public void Ignores_whitespace_only_input()
    {
        Assert.Empty(MarkdownBlocker.Parse("\n\n   \n"));
    }

    [Fact]
    public void Captures_heading_with_level()
    {
        var blocks = MarkdownBlocker.Parse("## A Heading\n");

        var heading = Assert.Single(blocks);
        Assert.Equal(BlockKind.Heading, heading.Kind);
        Assert.Equal(2, heading.HeadingLevel);
        Assert.False(heading.IsProse);
    }

    [Fact]
    public void Treats_fenced_code_as_one_non_prose_block_including_inner_blank_lines()
    {
        var blocks = MarkdownBlocker.Parse(
            "Intro para.\n\n```csharp\nvar x = 1;\n\nstill code\n```\n\nAfter.\n");

        Assert.Equal(3, blocks.Count);
        Assert.Equal(BlockKind.Paragraph, blocks[0].Kind);

        var fence = blocks[1];
        Assert.Equal(BlockKind.CodeFence, fence.Kind);
        Assert.False(fence.IsProse);
        Assert.StartsWith("```csharp", fence.Text);
        Assert.Contains("var x = 1;", fence.Text);
        Assert.Contains("still code", fence.Text); // inner blank line did not split the fence

        Assert.Equal(BlockKind.Paragraph, blocks[2].Kind);
        Assert.Equal("After.", blocks[2].Text);
    }

    [Fact]
    public void Separates_each_list_item_into_its_own_block()
    {
        var blocks = MarkdownBlocker.Parse("- one\n- two\n3. three\n");

        Assert.Equal(3, blocks.Count);
        Assert.All(blocks, b => Assert.Equal(BlockKind.ListItem, b.Kind));
        Assert.All(blocks, b => Assert.True(b.IsProse));
        Assert.Equal("- one", blocks[0].Text);
        Assert.Equal("3. three", blocks[2].Text);
    }

    [Fact]
    public void Associates_prose_with_the_nearest_preceding_heading()
    {
        var blocks = MarkdownBlocker.Parse(
            "# Title\n\nBody paragraph here.\n\n## Section\n\n- a point\n");

        Assert.Equal("Title", blocks[1].Heading);     // paragraph under Title
        Assert.Equal(BlockKind.Paragraph, blocks[1].Kind);
        Assert.Equal("Section", blocks[3].Heading);   // list item under Section
        Assert.Equal(BlockKind.ListItem, blocks[3].Kind);
        Assert.Null(blocks[0].Heading);               // the heading itself
    }

    [Fact]
    public void Char_offsets_round_trip_to_the_source_text()
    {
        const string source = "# Title\n\nA paragraph\nspanning two lines.\n\n- item one\n- item two\n";
        var blocks = MarkdownBlocker.Parse(source);

        Assert.NotEmpty(blocks);
        foreach (var block in blocks)
        {
            Assert.Equal(block.Text, source.Substring(block.StartChar, block.EndChar - block.StartChar));
        }
    }

    [Fact]
    public void Does_not_mistake_a_decimal_for_an_ordered_list_item()
    {
        var blocks = MarkdownBlocker.Parse("Latency dropped to 3.5% of the prior cost.\n");

        var only = Assert.Single(blocks);
        Assert.Equal(BlockKind.Paragraph, only.Kind);
    }
}
