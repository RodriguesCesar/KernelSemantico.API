using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace KernelSemantico.API.Services
{
    public class AgentService (Kernel kernel)
    {
        /*
         *  Método para executar o agente
         */
        public async Task<string> Ask(string message)
        {
            //Integração com a OpenAI
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            //Criando a engenharia de prompt (treinamento do LLM)
            history.AddSystemMessage("""
                    Você é um assistente de CRM.
                    Você pode usar ferramentas para:
                    - listar clientes
                    - criar clientes
                    - buscar clientes
                    Sempre use as funções disponíveis para responder.
                """);

            //Adicionando a mensagem que será enviada para a IA
            history.AddUserMessage(message);

            //Registrando os plugins do agente
            //var criarCliente = kernel.Plugins["ClientePlugin"]["CriarCliente"];
            //var listarClientes = kernel.Plugins["ClientePlugin"]["ListarClientes"];
            //var buscarCliente = kernel.Plugins["ClientePlugin"]["BuscarCliente"];

            //Executando o prompt
            var result = await chat.GetChatMessageContentAsync(history,
                new OpenAIPromptExecutionSettings //Prompt da OpenAI
                {
                    //Passar os plugins (KernelFuncion)
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                    //ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions(
                    //        criarCliente,
                    //        listarClientes,
                    //        buscarCliente
                    //   )
                }, kernel);

            //Retornar a resposta da IA
            return result.Content ?? string.Empty;
        }
    }
}
