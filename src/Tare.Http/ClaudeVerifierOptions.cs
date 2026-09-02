namespace Tare.Http;

/// <summary>
/// What <see cref="ClaudeClaimVerifier"/> is allowed to spend: which model it asks, how much
/// of a source it reads and shows, how long an answer may be, and how many times one run may
/// ask at all.
/// </summary>
/// <remarks>
/// <para>
/// Here rather than in <c>TareOptions</c> for the same reason as <see cref="CrossrefOptions"/>:
/// <c>TareOptions</c> tunes the deterministic analyzer and binds a config string without
/// touching the outside world, and these are facts about one adapter's traffic and bill.
/// </para>
/// <para>
/// None of these is a threshold, which is the point of the shape. The similarity attempt that
/// came before rested on a cut line nothing had measured, and picking that number was picking
/// the answer; there is no number here that a verdict turns on. What these bound is cost, and
/// a cost ceiling is allowed to be a judgement call as long as it is stated - every one below
/// says what it trades.
/// </para>
/// </remarks>
public sealed record ClaudeVerifierOptions
{
    /// <summary>
    /// The model asked. Named rather than inferred so a run is reproducible and so the bill is
    /// a decision somebody made.
    /// </summary>
    public string Model { get; init; } = "claude-opus-5";

    /// <summary>Where the request goes. Configurable for a proxy; never taken from a draft.</summary>
    public Uri Endpoint { get; init; } = new("https://api.anthropic.com/v1/messages");

    /// <summary>The API version the request is written against, sent on every call.</summary>
    public string ApiVersion { get; init; } = "2023-06-01";

    /// <summary>
    /// How hard the model is asked to work. Low by default: the question is one claim against
    /// a few pages of text, and the deterministic half of this tool is free, so a slow
    /// expensive second opinion on a gray-zone case is a poor trade. Raise it if the answers
    /// read as careless.
    /// </summary>
    public string Effort { get; init; } = "low";

    /// <summary>
    /// The ceiling on one answer. The answer itself is a verdict word and a sentence, so this
    /// is mostly headroom for the model's own reasoning; an answer that hits the ceiling
    /// arrives cut in half and is reported as no answer rather than parsed out of the wreck.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 2048;

    /// <summary>
    /// How much of the source's readable text the model is shown. It is the per-call bill in
    /// one number. Cutting a long page here means the answer describes the part that was sent,
    /// which is why anything cut off can only ever produce
    /// <see cref="Tare.Core.ClaimSupport.Unknown"/> rather than a verdict about what was not read.
    /// </summary>
    public int MaxSourceCharacters { get; init; } = 12_000;

    /// <summary>
    /// How much of the response body is read off the wire before the markup is stripped. A
    /// separate ceiling from <see cref="MaxSourceCharacters"/> and a larger one, because
    /// markup is mostly not text: this bounds what a cited page can make this process hold in
    /// memory, and the other bounds what leaves it.
    /// </summary>
    public int MaxDownloadCharacters { get; init; } = 400_000;

    /// <summary>
    /// How many times one verifier may ask, across every claim it is handed. The bill is per
    /// run, so the ceiling is too: without it a long draft quietly turns into as many paid
    /// calls as it has citations. Past the ceiling the answer is
    /// <see cref="Tare.Core.ClaimSupport.Unknown"/> with a reason that says so, so a run that
    /// hits it is visible rather than silently cheap.
    /// </summary>
    public int MaxCalls { get; init; } = 25;

    /// <summary>The defaults, used whenever no options are supplied.</summary>
    public static ClaudeVerifierOptions Default { get; } = new();
}
