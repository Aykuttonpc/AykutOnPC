using Pgvector;

namespace AykutOnPC.Core.Entities;

public class KnowledgeEntry
{
    public int Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 768-dim embedding (Gemini text-embedding-004) of "Topic + Content + Keywords".
    /// Computed on Create/Update by <c>IEmbeddingService</c>. Null while RAG migration
    /// is in flight or if embedding compute failed — chat retrieval falls back to the
    /// legacy KB-stuffing path when null. HNSW index for cosine similarity search.
    /// </summary>
    public Vector? Embedding { get; set; }
}
