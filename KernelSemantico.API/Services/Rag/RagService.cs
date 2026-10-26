using System.Text;
using KernelSemantico.API.Models.Request;
using KernelSemantico.API.Models.Response;
using KernelSemantico.API.Services;

namespace KernelSemantico.API.Services.Rag;

public class RagService(AgentService agentService, InMemoryRagStore store)
{
    public int Ingest(IngestDocumentRequest request)
    {
        var chunks = ChunkDocument(request.documentId, request.title, request.content)
            .Select((content, index) => new DocumentChunk(request.documentId, request.title, index + 1, content));

        return store.UpsertDocument(request.documentId, chunks);
    }

    public async Task<RagAnswerResponse> Ask(RagAskRequest request)
    {
        var topK = Math.Clamp(request.topK, 1, 10);
        var retrieved = Retrieve(request.message, topK);

        if (retrieved.Count == 0)
            return new RagAnswerResponse("Não encontrei contexto suficiente para responder com segurança.", []);

        var context = BuildContext(retrieved);
        var answer = await agentService.AskWithContext(request.message, context);

        var sources = retrieved
            .Select(x => new SourceReference(x.DocumentId, x.Title, x.ChunkIndex))
            .Distinct()
            .ToList();

        return new RagAnswerResponse(answer, sources);
    }

    private IReadOnlyList<DocumentChunk> Retrieve(string query, int topK)
    {
        var queryTerms = Tokenize(query).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (queryTerms.Count == 0)
            return [];

        return store.GetAll()
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = Tokenize(chunk.Content).Count(term => queryTerms.Contains(term))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.ChunkIndex)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();
    }

    private static string BuildContext(IReadOnlyList<DocumentChunk> chunks)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            sb.AppendLine($"[Fonte {i + 1}] DocumentId={chunk.DocumentId}; Título={chunk.Title}; Trecho={chunk.ChunkIndex}");
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var separators = new[]
        {
            ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '-', '_', '/', '\\'
        };

        return text
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 2)
            .Select(x => x.ToLowerInvariant());
    }

    private static IEnumerable<string> ChunkDocument(string documentId, string title, string content)
    {
        _ = documentId;
        _ = title;

        const int maxChunkLength = 700;
        const int overlap = 120;

        if (string.IsNullOrWhiteSpace(content))
            return [];

        if (content.Length <= maxChunkLength)
            return [content.Trim()];

        var chunks = new List<string>();
        var start = 0;

        while (start < content.Length)
        {
            var length = Math.Min(maxChunkLength, content.Length - start);
            var rawChunk = content.Substring(start, length).Trim();

            if (!string.IsNullOrWhiteSpace(rawChunk))
                chunks.Add(rawChunk);

            if (start + length >= content.Length)
                break;

            start += maxChunkLength - overlap;
        }

        return chunks;
    }
}
