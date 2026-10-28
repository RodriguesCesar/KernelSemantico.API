using KernelSemantico.API.Data;
using KernelSemantico.API.Plugins;
using KernelSemantico.API.Services;
using KernelSemantico.API.Services.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

//Objeto para ler o appsetings.json
var configuration = builder.Configuration;

var openTelemetryServiceName = configuration["OpenTelemetry:ServiceName"] ?? "KernelSemantico.API";
var openTelemetryOtlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
var openTelemetryOtlpProtocolRaw = configuration["OpenTelemetry:Otlp:Protocol"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL")
    ?? "grpc";

var openTelemetryOtlpProtocol = string.Equals(openTelemetryOtlpProtocolRaw, "http/protobuf", StringComparison.OrdinalIgnoreCase)
    ? OtlpExportProtocol.HttpProtobuf
    : OtlpExportProtocol.Grpc;

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(openTelemetryServiceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("KernelSemantico.API.Rag")
            .AddOtlpExporter(options =>
            {
                if (!string.IsNullOrWhiteSpace(openTelemetryOtlpEndpoint))
                    options.Endpoint = new Uri(openTelemetryOtlpEndpoint);

                options.Protocol = openTelemetryOtlpProtocol;
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("KernelSemantico.API.Rag")
            .AddOtlpExporter(options =>
            {
                if (!string.IsNullOrWhiteSpace(openTelemetryOtlpEndpoint))
                    options.Endpoint = new Uri(openTelemetryOtlpEndpoint);

                options.Protocol = openTelemetryOtlpProtocol;
            });
    });

//SQL Server
builder.Services.AddDbContext<AppDbContext>
    (options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

//Registrar as classes de Plugin
builder.Services.AddScoped<ClientePlugin>();

//Configuração do Microsoft Kernel
builder.Services.AddScoped(sp =>
{
    var model = configuration["OpenAI:Model"];
    var apiKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    if (string.IsNullOrWhiteSpace(model))
        throw new InvalidOperationException("OpenAI:Model não configurado.");

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("OpenAI:ApiKey não configurada. Use User Secrets, appsettings.Local.json ou OPENAI_API_KEY.");

    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddOpenAIChatCompletion(
            modelId: model,
            apiKey: apiKey
        );

    var kernel = kernelBuilder.Build();

    var plugin = sp.GetRequiredService<ClientePlugin>();

    kernel.ImportPluginFromObject(plugin, "clientes");

    return kernel;
});

builder.Services.AddHttpClient<OpenAiEmbeddingService>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<RagTelemetry>();
builder.Services.AddScoped<PromptGuardService>();
builder.Services.AddScoped<IVectorStore, PgVectorStore>();
builder.Services.AddHostedService<RagVectorStoreInitializer>();

//Injeção de dependência do Agente
builder.Services.AddScoped<AgentService>();
builder.Services.AddScoped<RagService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
