namespace KernelSemantico.API.Services.Rag;

public class InMemoryRagStore
{
    private readonly object _sync = new();
    private readonly List<DocumentChunk> _chunks = [];

    public int UpsertDocument(string documentId, IEnumerable<DocumentChunk> chunks)
    {
        lock (_sync)
        {
            _chunks.RemoveAll(x => x.DocumentId.Equals(documentId, StringComparison.OrdinalIgnoreCase));

            var newChunks = chunks.ToList();
            _chunks.AddRange(newChunks);

            return newChunks.Count;
        }
    }

    public IReadOnlyList<DocumentChunk> GetAll()
    {
        lock (_sync)
        {
            return _chunks.ToList();
        }
    }
}

public record DocumentChunk(
    string DocumentId,
    string Title,
    int ChunkIndex,
    string Content
);