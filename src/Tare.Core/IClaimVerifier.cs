namespace Tare.Core;

/// <summary>
/// The port for "does the cited source actually back this claim?". Its counterpart
/// <see cref="IClaimSource"/> answers whether the source exists; this one answers what it
/// says, which is the harder, slower and optional half - the one an implementation may have
/// to pay a model or a fetch for.
/// <para>
/// It is a separate port for exactly that reason. The deterministic rules are cheap, offline
/// and always run; a single interface answering both questions would have made the cheap half
/// depend on the expensive one, and an analysis that cannot run without a network is not the
/// tool this is. The core owns the question and the vocabulary of answers; whoever answers it
/// lives outside, and the core never learns how.
/// </para>
/// </summary>
public interface IClaimVerifier
{
    /// <summary>
    /// Weighs one claim against one source it cites. Implementations report failure as
    /// <see cref="ClaimSupport.Unknown"/> rather than throwing - a model that will not answer
    /// is an outcome, not an error, and the deterministic verdict has to survive it - but a
    /// cancellation requested by the caller does propagate.
    /// </summary>
    Task<ClaimVerification> VerifyAsync(
        Claim claim, Citation citation, CancellationToken cancellationToken = default);
}
