using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace KernelSemantico.API.Services.Rag;

public class OpenAiEmbeddingService(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Texto para embedding não pode ser vazio.", nameof(text));

        var apiKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI:ApiKey não configurada para geração de embeddings.");

        var model = configuration["Rag:EmbeddingModel"] ?? "text-embedding-3-small";

        using var request = new HttpRequestMessage(HttpMethod.Post, "embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = JsonSerializer.Serialize(new
        {
            model,
            input = text
        });

        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao gerar embedding na OpenAI. Status={(int)response.StatusCode}. Body={body}");

        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("data", out var dataElement) || dataElement.GetArrayLength() == 0)
            throw new InvalidOperationException("Resposta de embeddings da OpenAI sem dados.");

        var embeddingArray = dataElement[0].GetProperty("embedding");
        var result = new float[embeddingArray.GetArrayLength()];

        var i = 0;
        foreach (var value in embeddingArray.EnumerateArray())
        {
            result[i++] = value.GetSingle();
        }

        return result;
    }
}
