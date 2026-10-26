namespace KernelSemantico.API.Services.Rag;

public class GuardrailViolationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
