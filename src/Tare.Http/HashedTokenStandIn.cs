using Tare.Core;

namespace Tare.Http;

/// <summary>
/// A stand-in for an embedding model, and not one. It hashes the content tokens of a text into
/// a fixed number of buckets, counts them, and normalises the result, which produces something
/// with the shape of an embedding and none of the meaning: two texts score highly here when
/// they use the same words, never when they say the same thing.
/// </summary>
/// <remarks>
/// <para>
/// It exists so <see cref="EmbeddingClaimVerifier"/> can be run and read end to end without a
/// key, a bill or a network, and so the tests measure the pipeline rather than a provider's
/// availability. Using it as the actual model would be worse than useless: it would answer the
/// paraphrase case - the only case the whole approach is for - with a confident no.
/// </para>
/// <para>
/// The hash is written out by hand because the runtime's string hashing is seeded per process,
/// and a spike whose verdicts moved between runs could not be argued with in either direction.
/// Two hundred and fifty-six buckets means unrelated tokens do collide; a collision can only
/// push a score up, so a high score here is weaker evidence than it looks.
/// </para>
/// </remarks>
public sealed class HashedTokenStandIn : IEmbeddingModel
{
    /// <summary>Bucket count. Small on purpose: the vector is a stand-in, not a signal.</summary>
    private const int Dimensions = 256;

    public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The same tokenizer the density signal uses, so the stand-in is at least consistent
        // with how the rest of the tool reduces prose to comparable words.
        var vector = new float[Dimensions];
        foreach (var token in Tokenizer.Tokenize(text))
        {
            vector[Hash(token) % Dimensions] += 1f;
        }

        return Task.FromResult<IReadOnlyList<float>>(Normalised(vector));
    }

    /// <summary>FNV-1a, chosen for being short to write and stable across runs and machines.</summary>
    private static int Hash(string token)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in token)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return (int)(hash % Dimensions);
        }
    }

    /// <summary>
    /// Unit length, so a comparison is about which buckets are set rather than how long the
    /// text is. A text with no content tokens has no direction and is left at zero.
    /// </summary>
    private static float[] Normalised(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(component => (double)component * component));
        if (magnitude == 0)
        {
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }

        return vector;
    }
}
