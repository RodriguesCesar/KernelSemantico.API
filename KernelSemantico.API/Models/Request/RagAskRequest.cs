namespace KernelSemantico.API.Models.Request;

public record RagAskRequest(
    string message,
    int topK = 3
);