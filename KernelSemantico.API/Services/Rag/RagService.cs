using System.Text;
using KernelSemantico.API.Models.Request;
using KernelSemantico.API.Models.Response;
using KernelSemantico.API.Services;

namespace KernelSemantico.API.Services.Rag;

public class RagService(
    AgentService agentService,
    IVectorStore vectorStore,
    OpenAiEmbeddingService embeddingService,
    IConfiguration configuration)
{
    private readonly double _vectorWeight = configuration.GetValue<double?>("Rag:VectorWeight") ?? 0.7;
    private readonly double _keywordWeight = configuration.GetValue<double?>("Rag:KeywordWeight") ?? 0.3;

    public async Task<int> Ingest(IngestDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var chunkContents = ChunkDocument(request.content);

        var chunks = new List<VectorChunkInput>(chunkContents.Count);
        for (var i = 0; i < chunkContents.Count; i++)
        {
            var content = chunkContents[i];
            var embedding = await embeddingService.GenerateEmbeddingAsync(content, cancellationToken);
            chunks.Add(new VectorChunkInput(
                request.documentId,
                request.title,
                i + 1,
                content,
                embedding
            ));
        }

        return await vectorStore.UpsertDocumentChunksAsync(request.documentId, chunks, cancellationToken);
    }

    public async Task<RagAnswerResponse> Ask(RagAskRequest request, CancellationToken cancellationToken = default)
    {
        var topK = Math.Clamp(request.topK, 1, 10);

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.message, cancellationToken);
        var candidates = await vectorStore.SearchAsync(queryEmbedding, Math.Max(topK * 5, 15), cancellationToken);

        if (candidates.Count == 0)
            return new RagAnswerResponse("Não encontrei contexto suficiente para responder com segurança.", []);

        var ranked = RankHybrid(request.message, candidates, topK);
        var context = BuildContext(ranked);

        var answer = await agentService.AskWithContext(request.message, context);

        var sources = ranked
            .Select(x => new SourceReference(x.DocumentId, x.Title, x.ChunkIndex, Math.Round(x.HybridScore, 4)))
            .ToList();

        return new RagAnswerResponse(answer, sources);
    }

    private IReadOnlyList<RankedChunk> RankHybrid(string query, IReadOnlyList<RetrievedChunk> candidates, int topK)
    {
        var queryTerms = Tokenize(query).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var (vectorWeight, keywordWeight) = NormalizeWeights(_vectorWeight, _keywordWeight);

        var ranked = candidates
            .Select(chunk =>
            {
                var contentTerms = Tokenize(chunk.Content).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var keywordHits = queryTerms.Count == 0
                    ? 0
                    : queryTerms.Count(term => contentTerms.Contains(term));

                var keywordScore = queryTerms.Count == 0
                    ? 0
                    : (double)keywordHits / queryTerms.Count;

                var hybridScore = (chunk.VectorScore * vectorWeight) + (keywordScore * keywordWeight);

                return new RankedChunk(
                    chunk.DocumentId,
                    chunk.Title,
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.VectorScore,
                    keywordScore,
                    hybridScore
                );
            })
            .OrderByDescending(x => x.HybridScore)
            .ThenByDescending(x => x.VectorScore)
            .ThenBy(x => x.ChunkIndex)
            .Take(topK)
            .ToList();

        return ranked;
    }

    private static (double vectorWeight, double keywordWeight) NormalizeWeights(double vectorWeight, double keywordWeight)
    {
        if (vectorWeight < 0 || keywordWeight < 0)
            return (0.7, 0.3);

        var total = vectorWeight + keywordWeight;
        if (total <= 0)
            return (0.7, 0.3);

        return (vectorWeight / total, keywordWeight / total);
    }

    private static string BuildContext(IReadOnlyList<RankedChunk> chunks)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            sb.AppendLine($"[Fonte {i + 1}] DocumentId={chunk.DocumentId}; Título={chunk.Title}; Trecho={chunk.ChunkIndex}; Score={chunk.HybridScore:F4}");
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static IReadOnlyList<string> ChunkDocument(string content)
    {
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

    private sealed record RankedChunk(
        string DocumentId,
        string Title,
        int ChunkIndex,
        string Content,
        double VectorScore,
        double KeywordScore,
        double HybridScore
    );
}
