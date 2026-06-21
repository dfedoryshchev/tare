namespace Tare.Core;

/// <summary>
/// A single sentence within a prose <see cref="Block"/>. Character offsets are 0-based
/// into the original source (<see cref="EndChar"/> exclusive), so
/// <c>source.Substring(StartChar, EndChar - StartChar)</c> equals <see cref="Text"/>.
/// </summary>
public sealed record Sentence(int BlockIndex, string Text, int StartChar, int EndChar);
