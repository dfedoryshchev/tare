namespace Tare.Http;

/// <summary>
/// Turns a piece of text into a vector. It is the one thing <see cref="EmbeddingClaimVerifier"/>
/// cannot do for itself, so it is the one thing that gets an interface: everything else in the
/// spike - fetching, reading the page, cutting it into passages, comparing - is ordinary code
/// that runs offline and free.
/// </summary>
/// <remarks>
/// <para>
/// It lives here rather than in <c>Tare.Core</c> for the same reason the claim sources do. A
/// vector provider is either a network call or a downloaded model; either way it is machinery
/// the pure engine is not allowed to know about, and the core already has the port it needs in
/// <see cref="Tare.Core.IClaimVerifier"/>.
/// </para>
/// <para>
/// Nothing in this repository implements it against a real provider, and that is deliberate: a
/// spike that ships an API key path has stopped being a spike. <see cref="HashedTokenStandIn"/>
/// makes the pipeline runnable offline without pretending to be a model, and the tests write
/// their own vectors. A real deployment would need a hosted embedding endpoint, a key to reach
/// it, a batching call so one document is not hundreds of round trips, and a per-call cost
/// somebody has agreed to pay.
/// </para>
/// </remarks>
public interface IEmbeddingModel
{
    /// <summary>
    /// Embeds one text. The width of the returned vector is the model's business, but it has
    /// to be the same for every text, since the caller compares them against each other.
    /// </summary>
    Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
