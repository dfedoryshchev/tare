namespace Tare.Core;

/// <summary>
/// One reported problem, tied to a source span so a reader can jump straight to it. Line
/// numbers are 1-based; character offsets are 0-based with <see cref="EndChar"/> exclusive,
/// matching <see cref="Block"/> and <see cref="Sentence"/>.
/// </summary>
public sealed record Finding(
    string RuleId,
    Severity Severity,
    int BlockIndex,
    int StartLine,
    int EndLine,
    int StartChar,
    int EndChar,
    string Message);
