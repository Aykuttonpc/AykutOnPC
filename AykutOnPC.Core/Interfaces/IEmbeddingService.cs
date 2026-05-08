namespace AykutOnPC.Core.Interfaces;

/// <summary>
/// Generates dense vector embeddings for text — used by the RAG pipeline to
/// turn KB entries and user queries into points in semantic space so cosine
/// similarity can rank "what's relevant" without keyword matching.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>Embed a single piece of text. Returns null on transport/API failure.</summary>
    Task<float[]?> GenerateAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Embed many texts sequentially, surfacing per-item null on failure so a
    /// failed entry doesn't poison the whole backfill. Spaces requests so the
    /// embedding API's per-minute quota isn't tripped.
    /// </summary>
    Task<IReadOnlyList<float[]?>> GenerateBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
