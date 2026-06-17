namespace Tare.Core;

/// <summary>
/// A structural block of a markdown document. Line numbers are 1-based and inclusive;
/// character offsets are 0-based into the source, with <see cref="EndChar"/> exclusive,
/// so <c>source.Substring(StartChar, EndChar - StartChar)</c> equals <see cref="Text"/>.
/// </summary>
public sealed record Block(
    int Index,
    BlockKind Kind,
    int StartLine,
    int EndLine,
    int StartChar,
    int EndChar,
    string Text)
{
    /// <summary>Heading level (1-6) for <see cref="BlockKind.Heading"/> blocks; 0 otherwise.</summary>
    public int HeadingLevel { get; init; }

    /// <summary>Text of the nearest preceding heading, for prose blocks; null when none precedes.</summary>
    public string? Heading { get; init; }

    /// <summary>Prose is the only thing the signals score: paragraphs and list items.</summary>
    public bool IsProse => Kind is BlockKind.Paragraph or BlockKind.ListItem;
}
