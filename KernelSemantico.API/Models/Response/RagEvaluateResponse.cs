namespace KernelSemantico.API.Models.Response;

public record RagEvaluationCaseResult(
    string id,
    bool passed,
    bool blocked,
    bool hasSources,
    bool expectedTextFound,
    bool expectedSourceFound,
    double latencyMs,
    string reason,
    string answerSnippet,
    IReadOnlyList<SourceReference> sources
);

public record RagEvaluateResponse(
    int totalCases,
    int passedCases,
    int failedCases,
    int blockedCases,
    double averageLatencyMs,
    IReadOnlyList<RagEvaluationCaseResult> results
);
