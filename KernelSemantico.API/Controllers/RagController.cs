using KernelSemantico.API.Models.Request;
using KernelSemantico.API.Services.Rag;
using Microsoft.AspNetCore.Mvc;

namespace KernelSemantico.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RagController(RagService ragService) : ControllerBase
{
    [HttpPost("ingest")]
    public IActionResult Ingest([FromBody] IngestDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.documentId) || string.IsNullOrWhiteSpace(request.title) || string.IsNullOrWhiteSpace(request.content))
            return BadRequest("documentId, title e content são obrigatórios.");

        var chunks = ragService.Ingest(request);
        return Ok(new { message = "Documento ingerido com sucesso.", chunks });
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] RagAskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.message))
            return BadRequest("message é obrigatório.");

        var response = await ragService.Ask(request);
        return Ok(response);
    }
}
