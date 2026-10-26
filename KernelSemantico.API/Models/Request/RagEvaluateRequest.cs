namespace KernelSemantico.API.Models.Request;

public record RagEvaluationCaseRequest(
    string id,
    string question,
    string expectedContains,
    string? expectedSourceDocumentId = null
);

public record RagEvaluateRequest(
    IReadOnlyList<RagEvaluationCaseRequest> cases,
    int topK = 3
);
