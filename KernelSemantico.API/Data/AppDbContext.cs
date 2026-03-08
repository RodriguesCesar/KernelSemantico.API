using KernelSemantico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KernelSemantico.API.Data
{
    public class AppDbContext : DbContext
    {
        //Método construtor para injeção de dependência (configurações do banco de dados)
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }

        //Propriedade para executar operações no banco com Cliente
        public DbSet<Cliente> Clientes { get; set; }

        //Mapeamento das entidades como tabelas do banco de dados (ORM - Object Relational Mapping)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cliente>(entity => {
                entity.ToTable("CLIENTE"); //nome da tabela
                entity.HasKey(c => c.Id); //chave primária
                entity.Property(c => c.Id).HasColumnName("ID"); //campo id
                entity.Property(c => c.Nome).HasColumnName("NOME"); //campo nome
                entity.Property(c => c.Email).HasColumnName("EMAIL"); //campo email
            });
        }
    }
}
