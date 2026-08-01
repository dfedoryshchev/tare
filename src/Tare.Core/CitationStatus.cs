namespace Tare.Core;

/// <summary>
/// The outcome of checking that a cited source exists. The split between <see cref="Dead"/>
/// and <see cref="Unreachable"/> is the one that matters: only a definite answer from the
/// server is evidence about the draft, and everything else - a timeout, a 500, a DNS
/// failure, a host the checker declines to touch - says something about the run rather than
/// about the writing. Folding those into "dead link" would charge the author for the
/// network's bad days, which is the false-positive habit this tool exists to avoid.
/// </summary>
public enum CitationStatus
{
    /// <summary>The server answered for the cited URL.</summary>
    Resolves,

    /// <summary>The server answered that there is nothing there (404 or 410).</summary>
    Dead,

    /// <summary>No usable answer: a transport failure, a timeout, or a server-side error.</summary>
    Unreachable,

    /// <summary>Never fetched, because <see cref="CitationPolicy"/> declined the URL.</summary>
    Skipped,
}
