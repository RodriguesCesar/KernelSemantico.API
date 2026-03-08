using KernelSemantico.API.Data;
using KernelSemantico.API.Plugins;
using KernelSemantico.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

//Objeto para ler o appsetings.json
var configuration = builder.Configuration;

//SQL Server
builder.Services.AddDbContext<AppDbContext>
    (options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

//Registrar as classes de Plugin
builder.Services.AddScoped<ClientePlugin>();

//Configuração do Microsoft Kernel
builder.Services.AddScoped(sp => {
    
    var model = configuration["OpenAI:Model"];
    var apiKey = configuration["OpenAI:ApiKey"];

    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddOpenAIChatCompletion(
            modelId : model, //Modelo da LLM da OpenAI
            apiKey: apiKey //Chave de autenticação
        );

    var kernel = kernelBuilder.Build();

    var plugin = sp.GetRequiredService<ClientePlugin>();

    kernel.ImportPluginFromObject(plugin, "clientes");

    return kernel;
});

//Injeção de dependência do Agente
builder.Services.AddScoped<AgentService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
