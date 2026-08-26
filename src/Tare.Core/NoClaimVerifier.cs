namespace Tare.Core;

/// <summary>
/// The verifier in effect when none is configured: it asks nobody and answers
/// <see cref="ClaimSupport.Unknown"/> every time. Having it means "run the deterministic
/// rules only" is a value that can be passed around rather than a null every caller has to
/// remember to test, and it keeps the seam exercisable in a suite that never leaves the
/// process.
/// </summary>
public sealed class NoClaimVerifier : IClaimVerifier
{
    /// <summary>The one instance; it holds nothing and is safe to share.</summary>
    public static NoClaimVerifier Instance { get; } = new();

    private NoClaimVerifier()
    {
    }

    public Task<ClaimVerification> VerifyAsync(
        Claim claim, Citation citation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new ClaimVerification(claim, citation, ClaimSupport.Unknown, "no verifier configured"));
    }
}
