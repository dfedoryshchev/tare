using System.Net;
using System.Text;
using System.Text.Json;
using Tare.Core;
using Tare.Http;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// The optional layer, wired. Everything here runs inside the process: the stubs stand where
/// a model-backed verifier stands, and the two tests that use the real adapter give it stubbed
/// transports, so no key, bill or network is involved.
/// <para>
/// Most of what follows asserts an absence, which is deliberate. The promise this layer is
/// built around is that a verifier which is missing, refuses, fails or cannot decide leaves
/// the deterministic report exactly as it was; an absence is only a promise once something
/// fails when it breaks.
/// </para>
/// </summary>
public class AnalyzerVerificationTests
{
    // One claim citing a source inside its own sentence, one claim citing nothing, one URL no
    // claim sits on, and one inside a fence. The last two are the pairs that must never be asked.
    private const string Draft =
        "# Rollout\n\n" +
        "Error rates dropped 40% in the first week per https://example.org/incidents.\n\n" +
        "Churn fell 12% over the quarter.\n\n" +
        "The archive index is at https://example.org/about.\n\n" +
        "```\n" +
        "curl https://example.org/sample\n" +
        "```\n";

    // Two cited claims, so a failure on one can be shown not to swallow the other.
    private const string TwoCited =
        "Error rates dropped 40% in the first week per https://example.org/incidents.\n\n" +
        "Churn fell 12% over the quarter per https://example.org/churn.\n";

    /// <summary>The one line the optional layer is allowed to have added.</summary>
    private static Finding Verification(AnalysisResult result) =>
        Assert.Single(result.Findings, finding => finding.RuleId == RuleIds.UnsupportedCitation);

    private static void Unmoved(AnalysisResult deterministic, AnalysisResult verified)
    {
        Assert.Equal(deterministic.Score, verified.Score);
        Assert.Equal(deterministic.Band, verified.Band);
        Assert.Equal(deterministic.Findings, verified.Findings);
    }

    [Fact]
    public async Task No_verifier_configured_leaves_the_deterministic_report_alone()
    {
        var verified = await Analyzer.AnalyzeAsync(Draft, NoClaimVerifier.Instance);

        Unmoved(Analyzer.Analyze(Draft), verified);
    }

    [Fact]
    public async Task A_verifier_that_cannot_decide_leaves_the_deterministic_report_alone()
    {
        var verified = await Analyzer.AnalyzeAsync(Draft, new Answering(ClaimSupport.Unknown));

        Unmoved(Analyzer.Analyze(Draft), verified);
    }

    [Fact]
    public async Task A_verifier_that_throws_leaves_the_deterministic_report_alone()
    {
        // The port asks implementations not to throw, and an implementation is somebody
        // else's code. The promise cannot rest on it keeping its side of the bargain.
        var verified = await Analyzer.AnalyzeAsync(
            Draft, new Throwing(new InvalidOperationException("the adapter fell over")));

        Unmoved(Analyzer.Analyze(Draft), verified);
    }

    [Fact]
    public async Task A_verifier_that_runs_out_of_time_leaves_the_deterministic_report_alone()
    {
        // A client's own timeout surfaces as a cancellation the caller never asked for. It is
        // a fact about the run, so it reads as no answer rather than as an aborted analysis.
        var verified = await Analyzer.AnalyzeAsync(
            Draft, new Throwing(new TaskCanceledException("the service did not answer in time")));

        Unmoved(Analyzer.Analyze(Draft), verified);
    }

    [Fact]
    public async Task A_supporting_verdict_adds_nothing_to_the_report()
    {
        // Findings are problems. A source that backs its claim is the expected case and the
        // deterministic rules already said everything there was to say about it.
        var verified = await Analyzer.AnalyzeAsync(Draft, new Answering(ClaimSupport.Supported));

        Unmoved(Analyzer.Analyze(Draft), verified);
    }

    [Fact]
    public async Task A_failing_verifier_is_still_asked_about_the_next_claim()
    {
        var verifier = new Throwing(new InvalidOperationException("the adapter fell over"));

        await Analyzer.AnalyzeAsync(TwoCited, verifier);

        Assert.Equal(2, verifier.Asked);
    }

    [Fact]
    public async Task A_contradicted_claim_is_reported_without_moving_the_score()
    {
        var deterministic = Analyzer.Analyze(Draft);

        var verified = await Analyzer.AnalyzeAsync(Draft, new Answering(ClaimSupport.Contradicted));

        // The whole design in one assertion: the layer may add a line to the report, and it
        // may not touch the number the deterministic rules earned.
        Assert.Equal(deterministic.Score, verified.Score);
        Assert.Equal(deterministic.Band, verified.Band);
        Assert.All(deterministic.Findings, finding => Assert.Contains(finding, verified.Findings));

        var added = Verification(verified);
        Assert.Contains("contradicts", added.Message);
        Assert.Contains("https://example.org/incidents", added.Message);
    }

    [Fact]
    public async Task An_overstated_claim_is_reported_as_the_source_being_narrower()
    {
        var verified = await Analyzer.AnalyzeAsync(Draft, new Answering(ClaimSupport.Overstated));

        var added = Verification(verified);
        Assert.Contains("narrower", added.Message);
    }

    [Fact]
    public async Task A_reported_verification_points_at_the_citation_it_read()
    {
        var verified = await Analyzer.AnalyzeAsync(Draft, new Answering(ClaimSupport.Contradicted));

        var added = Verification(verified);
        Assert.Equal(
            "https://example.org/incidents",
            Draft.Substring(added.StartChar, added.EndChar - added.StartChar));
        Assert.Equal(3, added.StartLine);
    }

    [Fact]
    public async Task A_reported_verification_carries_the_reason_it_was_given()
    {
        var verified = await Analyzer.AnalyzeAsync(Draft, new Answering(ClaimSupport.Contradicted));

        var added = Verification(verified);
        Assert.Contains("stubbed", added.Message);
    }

    [Fact]
    public async Task A_reported_verification_stays_below_the_calibrated_rules()
    {
        // The deterministic tier was earned against a labeled corpus; this one has no corpus
        // behind it, so it does not get to speak louder than the rules that do.
        var verified = await Analyzer.AnalyzeAsync(Draft, new Answering(ClaimSupport.Contradicted));

        var added = Verification(verified);
        Assert.Equal(Severity.Info, added.Severity);
    }

    [Fact]
    public async Task Only_a_citation_inside_a_claim_sentence_is_asked_about()
    {
        // The pairing is the narrow one: the writer put that URL in that sentence, so the
        // document itself asserted the pair. A URL in a sentence that claims nothing, and a
        // URL in a code fence, were never offered as backing for anything.
        var verifier = new Answering(ClaimSupport.Unknown);

        await Analyzer.AnalyzeAsync(Draft, verifier);

        var pair = Assert.Single(verifier.Asked);
        Assert.Equal("https://example.org/incidents", pair.Url);
        Assert.StartsWith("Error rates dropped 40%", pair.Claim);
    }

    [Fact]
    public async Task Claims_are_asked_about_in_document_order()
    {
        // The budget is spent in the order a reader meets the citations, so a run that runs
        // out part way through is reproducible rather than arbitrary.
        var verifier = new Answering(ClaimSupport.Unknown);

        await Analyzer.AnalyzeAsync(TwoCited, verifier);

        Assert.Equal(
            new[] { "https://example.org/incidents", "https://example.org/churn" },
            verifier.Asked.Select(pair => pair.Url));
    }

    [Fact]
    public async Task A_document_with_nothing_to_verify_asks_nobody()
    {
        var verifier = new Answering(ClaimSupport.Unknown);

        await Analyzer.AnalyzeAsync("Churn fell 12% over the quarter.\n", verifier);

        Assert.Empty(verifier.Asked);
    }

    [Fact]
    public async Task Verification_findings_keep_the_report_in_deterministic_order()
    {
        var verified = await Analyzer.AnalyzeAsync(Draft, new Answering(ClaimSupport.Contradicted));

        var keys = verified.Findings.Select(f => (f.BlockIndex, f.StartChar, f.RuleId)).ToList();
        Assert.Equal(
            keys.OrderBy(k => k.BlockIndex).ThenBy(k => k.StartChar)
                .ThenBy(k => k.RuleId, StringComparer.Ordinal).ToList(),
            keys);
    }

    [Fact]
    public async Task A_cancellation_the_caller_asked_for_still_propagates()
    {
        // The one failure that is not the verifier's: the caller said stop, so stopping is
        // the answer rather than a report they did not wait for.
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Analyzer.AnalyzeAsync(Draft, NoClaimVerifier.Instance, null, cancelled.Token));
    }

    [Fact]
    public async Task The_model_adapter_with_no_key_asks_nobody_and_changes_nothing()
    {
        // End to end through the real adapter, which is the state every install starts in.
        var pages = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var api = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var verifier = new ClaudeClaimVerifier(
            new HttpClient(pages), new HttpClient(api), apiKey: null);

        var verified = await Analyzer.AnalyzeAsync(Draft, verifier);

        Unmoved(Analyzer.Analyze(Draft), verified);
        Assert.Empty(pages.Requests);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task The_model_adapter_refused_by_the_service_changes_nothing()
    {
        // It really did fetch and really did ask; the service said no, and the report is the
        // one the deterministic rules produced.
        var pages = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<p>Error rates fell in the first week.</p>", Encoding.UTF8, "text/html"),
        });
        var api = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });
        var verifier = new ClaudeClaimVerifier(
            new HttpClient(pages), new HttpClient(api), "local-test-key");

        var verified = await Analyzer.AnalyzeAsync(Draft, verifier);

        Unmoved(Analyzer.Analyze(Draft), verified);
        Assert.Single(pages.Requests);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task The_model_adapter_reading_a_denial_reports_it_without_moving_the_score()
    {
        // The full path, offline: a fetched page, a bounded call, a verdict, a finding - and a
        // score that is still the one the deterministic rules earned.
        var deterministic = Analyzer.Analyze(Draft);
        var pages = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<p>Error rates did not drop in the first week.</p>", Encoding.UTF8, "text/html"),
        });
        var api = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Answer("contradicted"), Encoding.UTF8, "application/json"),
        });
        var verifier = new ClaudeClaimVerifier(
            new HttpClient(pages), new HttpClient(api), "local-test-key");

        var verified = await Analyzer.AnalyzeAsync(Draft, verifier);

        Assert.Equal(deterministic.Score, verified.Score);
        Assert.Equal(deterministic.Band, verified.Band);
        var added = Verification(verified);
        Assert.Contains("the page says the drop did not happen", added.Message);
    }

    /// <summary>One answer in the wire shape the service documents.</summary>
    private static string Answer(string verdict) =>
        JsonSerializer.Serialize(new
        {
            id = "msg_offline",
            type = "message",
            role = "assistant",
            model = "claude-opus-5",
            stop_reason = "end_turn",
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(
                        new { verdict, reason = "the page says the drop did not happen" }),
                },
            },
        });

    /// <summary>Answers the same way every time and records what it was asked.</summary>
    private sealed class Answering(ClaimSupport support) : IClaimVerifier
    {
        public List<(string Claim, string Url)> Asked { get; } = new();

        public Task<ClaimVerification> VerifyAsync(
            Claim claim, Citation citation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Asked.Add((claim.Sentence.Text, citation.Url));
            return Task.FromResult(new ClaimVerification(claim, citation, support, "stubbed"));
        }
    }

    /// <summary>The badly-behaved implementation the port asked not to exist.</summary>
    private sealed class Throwing(Exception failure) : IClaimVerifier
    {
        public int Asked { get; private set; }

        public Task<ClaimVerification> VerifyAsync(
            Claim claim, Citation citation, CancellationToken cancellationToken = default)
        {
            Asked++;
            throw failure;
        }
    }
}
