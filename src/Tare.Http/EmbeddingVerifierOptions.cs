namespace Tare.Http;

/// <summary>
/// The knobs of the similarity spike. Every one of them is a guess, and the first of them is
/// the guess the whole approach rests on.
/// </summary>
/// <remarks>
/// Here rather than in <c>TareOptions</c> for the same reason as <see cref="CrossrefOptions"/>:
/// <c>TareOptions</c> tunes the deterministic analyzer and is calibrated against the labeled
/// corpus, and putting an uncalibrated number next to calibrated ones would borrow authority
/// these have not earned.
/// </remarks>
public sealed record EmbeddingVerifierOptions
{
    /// <summary>
    /// The cosine score at or above which a passage is called support for the claim.
    /// <para>
    /// It is arbitrary. Nothing measured it: there is no labeled set of claim-and-source pairs
    /// in this repository, so the number was picked because it sits above where unrelated
    /// prose lands and below where a restatement lands in a handful of tried examples. That is
    /// not calibration, and the deterministic thresholds it sits beside were earned against a
    /// corpus. Moving it trades missed support against invented support with nothing to say
    /// which way is better.
    /// </para>
    /// </summary>
    public double SupportedAt { get; init; } = 0.72;

    /// <summary>
    /// How many words a passage holds. Long enough that a sentence keeps the context around
    /// it, short enough that one relevant line is not drowned by the page it sits on - a
    /// tension the window has no way to resolve, only to trade.
    /// </summary>
    public int PassageWords { get; init; } = 60;

    /// <summary>
    /// How far apart passages start. Smaller than <see cref="PassageWords"/> so a sentence
    /// straddling a boundary still lands whole in some window, which costs a near-doubling of
    /// the passages - and so of the calls.
    /// </summary>
    public int PassageStride { get; init; } = 30;

    /// <summary>
    /// The most passages one claim is compared against. A cost ceiling, not a quality choice:
    /// without it a long page silently turns one claim into hundreds of embedding calls. With
    /// it, anything past the ceiling is simply never looked at, and the verdict says nothing
    /// about that part of the source.
    /// </summary>
    public int MaxPassages { get; init; } = 40;

    /// <summary>
    /// How much of the fetched body is read. It caps what gets analysed, not what crosses the
    /// wire - the body is already in memory by the time it is trimmed - so it is a bound on
    /// the model bill rather than on the download.
    /// </summary>
    public int MaxCharacters { get; init; } = 200_000;

    /// <summary>The defaults, used whenever no options are supplied.</summary>
    public static EmbeddingVerifierOptions Default { get; } = new();
}
