namespace KernelSemantico.API.Services.Rag;

public class RagVectorStoreInitializer(IServiceScopeFactory serviceScopeFactory, ILogger<RagVectorStoreInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();

        await vectorStore.EnsureSchemaAsync(cancellationToken);
        logger.LogInformation("Inicialização do vector store concluída.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
