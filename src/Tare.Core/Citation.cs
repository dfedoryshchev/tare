namespace Tare.Core;

/// <summary>
/// A source a draft points at: the URL plus the span it occupies. Character offsets are
/// 0-based into the original source with <see cref="EndChar"/> exclusive, matching
/// <see cref="Block"/> and <see cref="Sentence"/>, so a report can send a reader to the
/// exact link rather than to the paragraph around it.
/// </summary>
public sealed record Citation(string Url, int BlockIndex, int StartChar, int EndChar);

/// <summary>
/// What became of one <see cref="Citation"/> when a claim source was asked whether it
/// exists. <see cref="Reason"/> is the short human phrase reported next to the status; it
/// says what happened, never what the source contains.
/// </summary>
public sealed record CitationCheck(Citation Citation, CitationStatus Status, string Reason);
