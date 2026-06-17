namespace Tare.Core;

/// <summary>
/// The structural role of a <see cref="Block"/> in a markdown document. Only prose blocks
/// (<see cref="Paragraph"/> and <see cref="ListItem"/>) are scored by the signals; headings
/// give context, and code/quotes are never treated as prose.
/// </summary>
public enum BlockKind
{
    Paragraph,
    Heading,
    ListItem,
    CodeFence,
    BlockQuote,
    Other,
}
