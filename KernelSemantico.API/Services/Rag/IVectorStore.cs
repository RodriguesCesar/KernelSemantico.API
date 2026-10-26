namespace KernelSemantico.API.Services.Rag;

public record VectorChunkInput(
    string DocumentId,
    string Title,
    int ChunkIndex,
    string Content,
    float[] Embedding
);

public record RetrievedChunk(
    string DocumentId,
    string Title,
    int ChunkIndex,
    string Content,
    double VectorScore
);

public interface IVectorStore
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    Task<int> UpsertDocumentChunksAsync(string documentId, IReadOnlyList<VectorChunkInput> chunks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(float[] queryEmbedding, int candidateCount, CancellationToken cancellationToken = default);
}
