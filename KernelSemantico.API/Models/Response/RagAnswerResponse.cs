namespace KernelSemantico.API.Models.Response;

public record SourceReference(
    string documentId,
    string title,
    int chunkIndex,
    double score
);

public record RagAnswerResponse(
    string answer,
    IReadOnlyList<SourceReference> sources
);
