namespace KernelSemantico.API.Services.Rag;

public class PromptGuardService(IConfiguration configuration)
{
    private static readonly string[] DefaultBlockedPatterns =
    [
        "ignore previous instructions",
        "ignore all previous instructions",
        "disregard previous instructions",
        "system prompt",
        "developer mode",
        "jailbreak",
        "sudo",
        "bypass safety",
        "reveal hidden instructions"
    ];

    private readonly int _maxQuestionLength = configuration.GetValue<int?>("Rag:Guardrails:MaxQuestionLength") ?? 2000;
    private readonly int _maxDocumentLength = configuration.GetValue<int?>("Rag:Guardrails:MaxDocumentLength") ?? 50000;
    private readonly string[] _blockedPatterns = configuration
        .GetSection("Rag:Guardrails:BlockedPatterns")
        .Get<string[]>()
        ?.Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray()
        ?? DefaultBlockedPatterns;

    public void ValidateQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new GuardrailViolationException("GUARDRAIL_EMPTY_QUESTION", "Pergunta vazia não é permitida.");

        if (question.Length > _maxQuestionLength)
            throw new GuardrailViolationException("GUARDRAIL_QUESTION_TOO_LONG", $"Pergunta excede o limite de {_maxQuestionLength} caracteres.");

        var normalized = question.ToLowerInvariant();

        var matchedPattern = _blockedPatterns.FirstOrDefault(pattern => normalized.Contains(pattern.ToLowerInvariant()));
        if (!string.IsNullOrWhiteSpace(matchedPattern))
        {
            throw new GuardrailViolationException(
                "GUARDRAIL_PROMPT_INJECTION",
                $"Pergunta bloqueada por padrão suspeito: '{matchedPattern}'."
            );
        }
    }

    public void ValidateIngest(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new GuardrailViolationException("GUARDRAIL_EMPTY_CONTENT", "Conteúdo vazio não é permitido para ingestão.");

        if (content.Length > _maxDocumentLength)
            throw new GuardrailViolationException("GUARDRAIL_CONTENT_TOO_LARGE", $"Conteúdo excede o limite de {_maxDocumentLength} caracteres.");
    }
}
