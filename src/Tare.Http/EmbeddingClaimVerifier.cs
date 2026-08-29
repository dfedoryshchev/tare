using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Tare.Core;

namespace Tare.Http;

/// <summary>
/// A first attempt at the support question: fetch the page a claim cites, cut it into
/// passages, embed the claim and every passage, and call the claim supported when the closest
/// passage clears a similarity threshold.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a spike, and it is kept as one.</b> It runs, it produces a verdict, and the
/// verdict is worth less than its confidence suggests. The paragraphs below are the reasons,
/// written down while they are still fresh rather than discovered later by someone trusting
/// the output.
/// </para>
/// <para>
/// <b>It cannot tell agreement from disagreement.</b> Cosine similarity measures how close two
/// texts sit, and a claim and its denial sit very close: same subject, same numbers, one word
/// of difference. Nothing in this pipeline looks at that word. So the only two answers it can
/// honestly produce are <see cref="ClaimSupport.Supported"/> and
/// <see cref="ClaimSupport.Unknown"/>, and the first of them is really "the source is about
/// this". <see cref="ClaimSupport.Contradicted"/> and <see cref="ClaimSupport.Overstated"/>
/// are unreachable from here, and those are the two answers a citation checker is worth having
/// for. The tests pin how bad this gets: measured with <see cref="HashedTokenStandIn"/>, a
/// source that denies the claim scores 0.80 against it and a source that repeats the claim
/// scores 0.79, so the denial is the better match of the two.
/// </para>
/// <para>
/// <b>The threshold is a guess.</b> See <see cref="EmbeddingVerifierOptions.SupportedAt"/>. The
/// deterministic thresholds this sits beside were tuned against a labeled corpus with a
/// measured false-positive rate; this one has neither, and a verdict with an uncalibrated cut
/// line in it is exactly the kind of confident output this tool exists to catch elsewhere.
/// </para>
/// <para>
/// <b>It is expensive per claim.</b> One claim costs one embedding call plus one per passage of
/// its source, capped by <see cref="EmbeddingVerifierOptions.MaxPassages"/>; a draft with
/// twenty claims across a dozen pages is in the hundreds of calls, and nothing is cached
/// between claims that cite the same page. That is a large recurring cost carried by every run
/// of a tool whose deterministic half is free.
/// </para>
/// <para>
/// <b>It only reads what the web will hand it as text.</b> The reader here strips tags and
/// keeps the words. A PDF, which is what a careful draft most often cites, yields nothing; a
/// page whose text arrives by script yields nothing; a page whose navigation outweighs its
/// content dilutes every passage, because there is no boilerplate removal.
/// </para>
/// <para>
/// <b>What it does earn:</b> the seam is right. The core stayed pure, the port did not change
/// shape to accommodate any of this, and the machinery below - fetch, read, window, compare -
/// is what any answer to the support question needs. If a later attempt replaces the vectors
/// and the threshold with a single bounded call that is asked the question directly, it
/// inherits everything except the part that does not work.
/// </para>
/// <para>
/// Not wired into <see cref="Analyzer"/> or the CLI, and not close to it.
/// <c>Analyzer.Analyze</c> is a synchronous static method; giving it a verifier would make it
/// async and every caller with it, which is a change to the shape of the tool and not
/// something a spike gets to decide. There is no rule id and no finding here either: this
/// emits nothing into a report.
/// </para>
/// </remarks>
public sealed partial class EmbeddingClaimVerifier(
    HttpClient client, IEmbeddingModel model, EmbeddingVerifierOptions? options = null) : IClaimVerifier
{
    private readonly EmbeddingVerifierOptions settings = options ?? EmbeddingVerifierOptions.Default;

    public async Task<ClaimVerification> VerifyAsync(
        Claim claim, Citation citation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ClaimVerification Unknown(string reason) => new(claim, citation, ClaimSupport.Unknown, reason);

        // The same gate the existing sources run. A spike that fetches is still a fetcher, and
        // the document naming the URL is untrusted input either way.
        if (CitationPolicy.Reject(citation.Url) is { } refusal)
        {
            return Unknown("not fetched: " + refusal);
        }

        try
        {
            using var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, citation.Url),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Unknown($"no usable answer (http {(int)response.StatusCode})");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!IsReadableText(mediaType))
            {
                return Unknown($"the source is {mediaType ?? "of no stated type"}, which this reads nothing out of");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var text = Readable(body.Length > settings.MaxCharacters ? body[..settings.MaxCharacters] : body);
            var passages = Passages(text);
            if (passages.Count == 0)
            {
                return Unknown("the source carried no readable text");
            }

            var target = await model.EmbedAsync(claim.Sentence.Text, cancellationToken);

            // Starts below every possible cosine rather than at zero: a real model can put two
            // texts on opposite sides, and reporting that as 0.00 would round a strong signal
            // of unrelatedness into a weak one. There is at least one passage by here, so it
            // is always replaced.
            var best = double.NegativeInfinity;
            foreach (var passage in passages)
            {
                var candidate = await model.EmbedAsync(passage, cancellationToken);
                if (candidate.Count != target.Count)
                {
                    return Unknown("the model returned vectors of different widths, so nothing was compared");
                }

                best = Math.Max(best, Cosine(target, candidate));
            }

            return new ClaimVerification(claim, citation, Verdict(best), Describe(best));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException)
        {
            return Unknown("no response: " + failure.Message);
        }
    }

    /// <summary>
    /// Below the line is <see cref="ClaimSupport.Unknown"/> and never a verdict against the
    /// author. A source that does not mention the claim is one the reader has to judge, and
    /// this has no way to tell "the source disagrees" from "the passage window cut badly" or
    /// "the model is weak here".
    /// </summary>
    private ClaimSupport Verdict(double best) =>
        best >= settings.SupportedAt ? ClaimSupport.Supported : ClaimSupport.Unknown;

    private string Describe(double best)
    {
        var score = best.ToString("F2", CultureInfo.InvariantCulture);
        var line = settings.SupportedAt.ToString("F2", CultureInfo.InvariantCulture);
        return best >= settings.SupportedAt
            ? $"the closest passage scores {score}, at or above the {line} line"
            : $"the closest passage scores {score}, below the {line} line, which is not evidence either way";
    }

    /// <summary>
    /// Overlapping windows of words. Overlapping because a claim's support is a sentence or
    /// two, and a hard cut through the middle of it leaves both halves looking irrelevant.
    /// </summary>
    private IReadOnlyList<string> Passages(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var width = Math.Max(1, settings.PassageWords);
        var stride = Math.Max(1, settings.PassageStride);

        var passages = new List<string>();
        for (var start = 0; start < words.Length && passages.Count < settings.MaxPassages; start += stride)
        {
            var take = Math.Min(width, words.Length - start);
            passages.Add(string.Join(' ', words, start, take));
            if (start + take >= words.Length)
            {
                break;
            }
        }

        return passages;
    }

    /// <summary>
    /// The angle between two vectors, which is what "similar" means here. Zero when either
    /// side has no direction at all, since a text with no content words is not close to
    /// anything.
    /// </summary>
    private static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        double dot = 0, leftLength = 0, rightLength = 0;
        for (var i = 0; i < left.Count; i++)
        {
            dot += (double)left[i] * right[i];
            leftLength += (double)left[i] * left[i];
            rightLength += (double)right[i] * right[i];
        }

        var magnitude = Math.Sqrt(leftLength) * Math.Sqrt(rightLength);
        return magnitude == 0 ? 0 : dot / magnitude;
    }

    /// <summary>
    /// Anything the reader below can make words out of. Everything else - a PDF, an image, an
    /// archive - is refused before a single call is paid for, because guessing at bytes would
    /// produce a score and a score is what a caller reads.
    /// </summary>
    private static bool IsReadableText(string? mediaType) =>
        mediaType is not null
        && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Markup to words, crudely: scripts and styles dropped whole, tags dropped, entities
    /// decoded, whitespace collapsed. It keeps the navigation, the cookie banner and the
    /// footer, all of which are text that the page really does contain and that no passage
    /// here can tell from the article.
    /// </summary>
    private static string Readable(string body)
    {
        var stripped = ScriptOrStyle().Replace(body, " ");
        stripped = Tag().Replace(stripped, " ");
        return Whitespace().Replace(WebUtility.HtmlDecode(stripped), " ").Trim();
    }

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
