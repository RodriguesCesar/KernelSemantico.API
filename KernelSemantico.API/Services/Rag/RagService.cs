using System.Diagnostics;
using System.Text;
using KernelSemantico.API.Models.Request;
using KernelSemantico.API.Models.Response;
using KernelSemantico.API.Services;

namespace KernelSemantico.API.Services.Rag;

public class RagService(
    AgentService agentService,
    IVectorStore vectorStore,
    OpenAiEmbeddingService embeddingService,
    PromptGuardService promptGuardService,
    RagTelemetry telemetry,
    IConfiguration configuration)
{
    private readonly double _vectorWeight = configuration.GetValue<double?>("Rag:VectorWeight") ?? 0.7;
    private readonly double _keywordWeight = configuration.GetValue<double?>("Rag:KeywordWeight") ?? 0.3;

    public async Task<int> Ingest(IngestDocumentRequest request, CancellationToken cancellationToken = default)
    {
        telemetry.IngestRequests.Add(1);
        var stopwatch = Stopwatch.StartNew();

        using var activity = telemetry.ActivitySource.StartActivity("rag.ingest", ActivityKind.Internal);
        activity?.SetTag("rag.document_id", request.documentId);

        try
        {
            promptGuardService.ValidateIngest(request.content);

            var chunkContents = ChunkDocument(request.content);
            activity?.SetTag("rag.chunks.count", chunkContents.Count);

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
        catch (GuardrailViolationException)
        {
            telemetry.GuardrailBlocked.Add(1);
            throw;
        }
        catch
        {
            telemetry.IngestErrors.Add(1);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            telemetry.IngestDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<RagAnswerResponse> Ask(RagAskRequest request, CancellationToken cancellationToken = default)
    {
        telemetry.AskRequests.Add(1);
        var stopwatch = Stopwatch.StartNew();

        using var activity = telemetry.ActivitySource.StartActivity("rag.ask", ActivityKind.Internal);
        activity?.SetTag("rag.top_k", request.topK);

        try
        {
            promptGuardService.ValidateQuestion(request.message);

            var topK = Math.Clamp(request.topK, 1, 10);

            var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.message, cancellationToken);
            var candidates = await vectorStore.SearchAsync(queryEmbedding, Math.Max(topK * 5, 15), cancellationToken);

            telemetry.RetrievalCandidateCount.Record(candidates.Count);
            activity?.SetTag("rag.candidates.count", candidates.Count);

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
        catch (GuardrailViolationException)
        {
            telemetry.GuardrailBlocked.Add(1);
            throw;
        }
        catch
        {
            telemetry.AskErrors.Add(1);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            telemetry.AskDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<RagEvaluateResponse> Evaluate(RagEvaluateRequest request, CancellationToken cancellationToken = default)
    {
        telemetry.EvaluateRequests.Add(1);
        var totalStopwatch = Stopwatch.StartNew();

        using var activity = telemetry.ActivitySource.StartActivity("rag.evaluate", ActivityKind.Internal);
        activity?.SetTag("rag.evaluate.cases", request.cases.Count);

        try
        {
            var results = new List<RagEvaluationCaseResult>(request.cases.Count);

            foreach (var testCase in request.cases)
            {
                var caseStopwatch = Stopwatch.StartNew();

                try
                {
                    var response = await Ask(new RagAskRequest(testCase.question, request.topK), cancellationToken);

                    var answer = response.answer ?? string.Empty;
                    var expectedTextFound = !string.IsNullOrWhiteSpace(testCase.expectedContains)
                                            && answer.Contains(testCase.expectedContains, StringComparison.OrdinalIgnoreCase);

                    var expectedSourceFound = string.IsNullOrWhiteSpace(testCase.expectedSourceDocumentId)
                                              || response.sources.Any(s => s.documentId.Equals(testCase.expectedSourceDocumentId, StringComparison.OrdinalIgnoreCase));

                    var hasSources = response.sources.Count > 0;
                    var passed = expectedTextFound && expectedSourceFound && hasSources;

                    results.Add(new RagEvaluationCaseResult(
                        testCase.id,
                        passed,
                        blocked: false,
                        hasSources,
                        expectedTextFound,
                        expectedSourceFound,
                        latencyMs: caseStopwatch.Elapsed.TotalMilliseconds,
                        reason: passed ? "OK" : "Resposta não atingiu os critérios esperados.",
                        answerSnippet: BuildSnippet(answer),
                        sources: response.sources
                    ));
                }
                catch (GuardrailViolationException ex)
                {
                    results.Add(new RagEvaluationCaseResult(
                        testCase.id,
                        passed: false,
                        blocked: true,
                        hasSources: false,
                        expectedTextFound: false,
                        expectedSourceFound: false,
                        latencyMs: caseStopwatch.Elapsed.TotalMilliseconds,
                        reason: $"Bloqueado por guardrail: {ex.Code}",
                        answerSnippet: string.Empty,
                        sources: []
                    ));
                }
            }

            var blockedCases = results.Count(x => x.blocked);
            var passedCases = results.Count(x => x.passed);
            var failedCases = results.Count - passedCases;
            var averageLatency = results.Count == 0 ? 0 : results.Average(x => x.latencyMs);

            return new RagEvaluateResponse(
                totalCases: results.Count,
                passedCases,
                failedCases,
                blockedCases,
                averageLatencyMs: Math.Round(averageLatency, 2),
                results
            );
        }
        catch
        {
            telemetry.EvaluateErrors.Add(1);
            throw;
        }
        finally
        {
            totalStopwatch.Stop();
            telemetry.EvaluateDurationMs.Record(totalStopwatch.Elapsed.TotalMilliseconds);
        }
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

    private static string BuildSnippet(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return string.Empty;

        const int maxLength = 240;
        return answer.Length <= maxLength
            ? answer
            : answer[..maxLength] + "...";
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
