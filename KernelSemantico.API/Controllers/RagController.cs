using KernelSemantico.API.Models.Request;
using KernelSemantico.API.Services.Rag;
using Microsoft.AspNetCore.Mvc;

namespace KernelSemantico.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RagController(RagService ragService, ILogger<RagController> logger) : ControllerBase
{
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.documentId) || string.IsNullOrWhiteSpace(request.title) || string.IsNullOrWhiteSpace(request.content))
            return BadRequest("documentId, title e content são obrigatórios.");

        try
        {
            var chunks = await ragService.Ingest(request, cancellationToken);
            return Ok(new { message = "Documento ingerido com sucesso.", chunks });
        }
        catch (GuardrailViolationException ex)
        {
            logger.LogWarning("Guardrail bloqueou ingestão. Code={Code}; Reason={Reason}", ex.Code, ex.Message);
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] RagAskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.message))
            return BadRequest("message é obrigatório.");

        try
        {
            var response = await ragService.Ask(request, cancellationToken);
            return Ok(response);
        }
        catch (GuardrailViolationException ex)
        {
            logger.LogWarning("Guardrail bloqueou pergunta RAG. Code={Code}; Reason={Reason}", ex.Code, ex.Message);
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] RagEvaluateRequest request, CancellationToken cancellationToken)
    {
        if (request.cases == null || request.cases.Count == 0)
            return BadRequest("cases deve conter pelo menos um item.");

        var response = await ragService.Evaluate(request, cancellationToken);
        return Ok(response);
    }
}
