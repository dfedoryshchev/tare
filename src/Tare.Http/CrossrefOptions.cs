namespace Tare.Http;

/// <summary>
/// How hard <see cref="CrossrefClaimSource"/> is allowed to lean on the registry: how far
/// apart its lookups are spaced, how long it will wait when asked to slow down, and whether
/// it says who it is.
/// </summary>
/// <remarks>
/// These live here rather than in <c>TareOptions</c> on purpose. <c>TareOptions</c> tunes the
/// analyzer and binds a config string without touching the outside world; pacing is a fact
/// about one adapter's traffic, and putting it in the core would drag the network into a
/// project that deliberately has none.
/// </remarks>
public sealed record CrossrefOptions
{
    /// <summary>
    /// Smallest gap between two lookups through the same source. Deliberately conservative
    /// rather than tuned to a published number: the registry does not promise a rate, and the
    /// cost of being slightly too polite is a slower check, while the cost of being too quick
    /// is a document's worth of answers that describe our request rate instead of its
    /// citations.
    /// </summary>
    public TimeSpan MinInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How many times one citation is asked about before the answer is "no usable answer".
    /// Counts the first attempt, so the default is one try and two retries.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Longest <c>Retry-After</c> that is actually waited out. A registry asking for longer
    /// than this is answered with <see cref="Tare.Core.CitationStatus.Unreachable"/> now,
    /// rather than parking the run - which is what makes the backoff bounded.
    /// </summary>
    public TimeSpan MaxRetryWait { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// A contact address for the polite pool. Null means anonymous traffic and the tighter
    /// limit that comes with it. It is opt-in because it leaves the machine: an address in a
    /// config file is a small thing to send, but it is not ours to send by default.
    /// </summary>
    public string? ContactEmail { get; init; }

    /// <summary>
    /// How a pause is taken. Substituted in tests so the suite can assert what was waited for
    /// without waiting for it.
    /// </summary>
    public Func<TimeSpan, CancellationToken, Task> Wait { get; init; } =
        static (delay, cancellationToken) => Task.Delay(delay, cancellationToken);

    /// <summary>The defaults, used whenever no options are supplied.</summary>
    public static CrossrefOptions Default { get; } = new();
}
