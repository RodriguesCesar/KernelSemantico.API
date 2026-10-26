namespace KernelSemantico.API.Models.Request;

public record IngestDocumentRequest(
    string documentId,
    string title,
    string content
);