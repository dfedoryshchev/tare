namespace Tare.Core;

/// <summary>
/// The pre-publish decision: given the <see cref="Band"/> a document earned and the bar its
/// caller set, does the run go through or stop? This is a judgement about the draft, so it
/// lives beside the bands it reads rather than in the front-end that prints them.
/// <para>
/// The numbers are the tool's contract with whatever script runs it, and they exist to keep
/// two answers apart. <see cref="Blocked"/> means tare ran, read the draft and refused it.
/// <see cref="Error"/> means tare never got as far as an opinion: an argument it could not
/// parse, a file that is not there, a config it could not read. A build that cannot tell "your
/// prose is thin" from "your config is malformed" points the wrong person at the wrong fix, so
/// the gate got a code of its own instead of reusing the failure code already in use.
/// </para>
/// </summary>
/// <remarks>
/// This returns a plain int rather than anything process-shaped. Which number a process exits
/// with is the front-end's business; which answer the draft earned is this file's. Same split
/// as <see cref="CitationOutcome"/> one layer down, where a status number crosses the seam and
/// the type that carried it does not.
/// </remarks>
public static class PublishGate
{
    /// <summary>The draft met the bar, or no bar was set.</summary>
    public const int Passed = 0;

    /// <summary>tare could not run far enough to have an opinion about the draft.</summary>
    public const int Error = 1;

    /// <summary>The draft was read and is at or above the band the caller refuses to publish.</summary>
    public const int Blocked = 2;

    /// <summary>
    /// Whether <paramref name="band"/> is at or above <paramref name="bar"/>, which is what
    /// makes a bar mean "at least this sloppy". The comparison leans on <see cref="Band"/>
    /// being declared cleanest-first; a band added out of order would quietly change what
    /// every bar means without touching a line here, so the tests enumerate all nine pairs
    /// and assert the declaration order itself.
    /// </summary>
    public static bool Trips(Band band, Band bar) => band >= bar;

    /// <summary>
    /// The exit code for an analysis that completed. A null <paramref name="bar"/> is the
    /// default and means no gate at all: the report is printed and the run succeeds whatever
    /// the band, so a caller who passes nothing sees exactly the behaviour they saw before
    /// the option existed.
    /// </summary>
    public static int ExitCode(Band band, Band? bar) =>
        bar is { } threshold && Trips(band, threshold) ? Blocked : Passed;
}
