using KernelSemantico.API.Data;
using KernelSemantico.API.Models;
using Microsoft.SemanticKernel;

namespace KernelSemantico.API.Plugins
{
    public class ClientePlugin (AppDbContext context)
    {
        /*
         * Função para consultar todos os clientes do banco de dados
         */
        [KernelFunction]
        public string ListarClientes()
        {
            //Consultar todos os clientes do banco de dados
            var clientes = context.Clientes.OrderBy(c => c.Nome).ToList();

            if (!clientes.Any())
                return "Nenhum cliente encontrado.";

            return string.Join("\n", clientes.Select(c => $"{c.Id} - {c.Nome} - {c.Email}"));
        }

        /*
         * Função para cadastrar um cliente no banco de dados
         */
        [KernelFunction]
        public async Task<string> CriarCliente(string nome, string email)
        {
            //Instanciando um novo cliente
            var cliente = new Cliente
            {
                Nome = nome,
                Email = email
            };

            //Salvar o cliente no banco de dados
            context.Clientes.Add(cliente);
            await context.SaveChangesAsync();

            //Retornar mensagem
            return "Cliente criado com sucesso.";
        }

        /*
         * Função para consultar clientes pelo nome
         */
        [KernelFunction]
        public string BuscarCliente(string nome)
        {
            //Consultar os clientes do banco de dados pelo nome
            var cliente = context.Clientes
                            .Where(c => c.Nome.Contains(nome))
                            .FirstOrDefault();

            if (cliente == null)
                return "Nenhum cliente encontrado.";

            return $"{cliente.Nome} - {cliente.Email}";
        }
    }
}
