namespace Tare.Core;

/// <summary>
/// A DOI a draft cites, plus the span it occupies. <see cref="Value"/> is the bare
/// identifier (<c>10.1038/nphys1170</c>) with any <c>doi:</c> prefix or resolver URL
/// stripped, lowercased because DOIs are case-insensitive by spec - so two spellings of one
/// work compare equal instead of costing two lookups and reading as two citations.
/// <para>
/// Offsets are 0-based into the original source with <see cref="EndChar"/> exclusive,
/// matching <see cref="Citation"/>. The span covers the identifier as written, prefix
/// included, because that is what a reader has to find on the page.
/// </para>
/// </summary>
public sealed record Doi(string Value, int BlockIndex, int StartChar, int EndChar);
