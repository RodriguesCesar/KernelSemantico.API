using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace KernelSemantico.API.Services
{
    public class AgentService(Kernel kernel)
    {
        /*
         *  Método para executar o agente
         */
        public async Task<string> Ask(string message)
        {
            return await AskInternal(message, null);
        }

        public async Task<string> AskWithContext(string message, string? context)
        {
            return await AskInternal(message, context);
        }

        private async Task<string> AskInternal(string message, string? context)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("A mensagem é obrigatória.", nameof(message));

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
                    Quando receber contexto de documentos, use esse contexto como fonte principal.
                    Se a pergunta não puder ser respondida com o contexto recebido, diga claramente que faltam dados.
                """);

            if (!string.IsNullOrWhiteSpace(context))
                history.AddSystemMessage($"""
                    Contexto recuperado para responder ao usuário:
                    {context}
                    """);

            //Adicionando a mensagem que será enviada para a IA
            history.AddUserMessage(message);

            //Executando o prompt
            var result = await chat.GetChatMessageContentAsync(history,
                new OpenAIPromptExecutionSettings //Prompt da OpenAI
                {
                    //Passar os plugins (KernelFuncion)
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                }, kernel);

            //Retornar a resposta da IA
            return result.Content ?? string.Empty;
        }
    }
}
