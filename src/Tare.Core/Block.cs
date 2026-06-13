namespace Tare.Core;

/// <summary>
/// A contiguous chunk of source text, separated from its neighbours by a blank line.
/// Line numbers are 1-based and inclusive.
/// </summary>
public sealed record Block(int Index, int StartLine, int EndLine, string Text);
