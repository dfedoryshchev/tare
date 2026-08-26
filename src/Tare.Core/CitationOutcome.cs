namespace Tare.Core;

/// <summary>
/// Turns the status an adapter got into what it means for the draft. Both adapters had their
/// own copy of this mapping, which put a judgement about the author's writing in two places
/// that only exist to move bytes: whether a 404 is the author's problem and a 500 is ours is
/// the core's call, and now there is one of it.
/// </summary>
/// <remarks>
/// It takes a plain status number rather than a status type on purpose. The number is a fact
/// an adapter carries across the seam; the type that carried it belongs to the transport, and
/// putting a transport type in a core signature is how a caller ends up needing one too.
/// </remarks>
public static class CitationOutcome
{
    /// <summary>
    /// Maps an HTTP status to a <see cref="CitationStatus"/>. Only a definite answer from the
    /// server - 404 or 410 - is evidence about the citation; everything else, including a
    /// refusal to serve us, is <see cref="CitationStatus.Unreachable"/>.
    /// </summary>
    public static CitationStatus FromHttpStatus(int status) =>
        status is >= 200 and < 300 ? CitationStatus.Resolves
            : status is 404 or 410 ? CitationStatus.Dead
            : CitationStatus.Unreachable;
}
