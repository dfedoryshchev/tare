namespace Tare.Core;

/// <summary>
/// The port for "does this cited source exist?". The core defines the question and the
/// vocabulary of answers; fetching belongs to an adapter, so <c>Tare.Core</c> keeps its
/// promise of doing no IO and every rule around it stays testable offline.
/// <para>
/// Existence only. Whether the source actually supports the claim is a separate, bounded
/// question that gets its own port later; a checker that answered both would make the cheap
/// deterministic half depend on the expensive optional one.
/// </para>
/// </summary>
public interface IClaimSource
{
    /// <summary>
    /// Checks one citation. Implementations report failure as a <see cref="CitationCheck"/>
    /// rather than throwing - an unreachable host is an outcome, not an error - but a
    /// cancellation requested by the caller does propagate.
    /// </summary>
    Task<CitationCheck> CheckAsync(Citation citation, CancellationToken cancellationToken = default);
}
