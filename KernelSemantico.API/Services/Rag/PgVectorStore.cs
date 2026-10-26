using System.Globalization;
using Npgsql;

namespace KernelSemantico.API.Services.Rag;

public class PgVectorStore(IConfiguration configuration, ILogger<PgVectorStore> logger) : IVectorStore
{
    private readonly string _connectionString = configuration["Rag:ConnectionString"]
                                                ?? Environment.GetEnvironmentVariable("RAG_VECTOR_DB_CONNECTION")
                                                ?? throw new InvalidOperationException("Rag:ConnectionString não configurada. Use appsettings.Local.json/User Secrets ou RAG_VECTOR_DB_CONNECTION.");

    private readonly int _embeddingDimensions = configuration.GetValue<int?>("Rag:EmbeddingDimensions") ?? 1536;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            CREATE EXTENSION IF NOT EXISTS vector;

            CREATE TABLE IF NOT EXISTS rag_chunks (
                document_id TEXT NOT NULL,
                title TEXT NOT NULL,
                chunk_index INTEGER NOT NULL,
                content TEXT NOT NULL,
                embedding VECTOR({_embeddingDimensions}) NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                PRIMARY KEY (document_id, chunk_index)
            );

            CREATE INDEX IF NOT EXISTS ix_rag_chunks_embedding
            ON rag_chunks USING hnsw (embedding vector_cosine_ops);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("Schema pgvector garantido em rag_chunks com dimensão {Dimensions}", _embeddingDimensions);
    }

    public async Task<int> UpsertDocumentChunksAsync(string documentId, IReadOnlyList<VectorChunkInput> chunks, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("documentId obrigatório.", nameof(documentId));

        if (chunks.Count == 0)
            return 0;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = new NpgsqlCommand("DELETE FROM rag_chunks WHERE document_id = @documentId", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("documentId", documentId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO rag_chunks (document_id, title, chunk_index, content, embedding)
            VALUES (@documentId, @title, @chunkIndex, @content, CAST(@embedding AS VECTOR))
            """;

        foreach (var chunk in chunks)
        {
            if (chunk.Embedding.Length != _embeddingDimensions)
            {
                throw new InvalidOperationException($"Dimensão de embedding inválida para o chunk {chunk.ChunkIndex}. Esperado={_embeddingDimensions}, recebido={chunk.Embedding.Length}");
            }

            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("documentId", chunk.DocumentId);
            insertCommand.Parameters.AddWithValue("title", chunk.Title);
            insertCommand.Parameters.AddWithValue("chunkIndex", chunk.ChunkIndex);
            insertCommand.Parameters.AddWithValue("content", chunk.Content);
            insertCommand.Parameters.AddWithValue("embedding", ToVectorLiteral(chunk.Embedding));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return chunks.Count;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(float[] queryEmbedding, int candidateCount, CancellationToken cancellationToken = default)
    {
        if (queryEmbedding.Length != _embeddingDimensions)
            throw new InvalidOperationException($"Dimensão da query embedding inválida. Esperado={_embeddingDimensions}, recebido={queryEmbedding.Length}");

        var limit = Math.Clamp(candidateCount, 1, 100);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string searchSql = """
            SELECT
                document_id,
                title,
                chunk_index,
                content,
                1 - (embedding <=> CAST(@queryEmbedding AS VECTOR)) AS vector_score
            FROM rag_chunks
            ORDER BY embedding <=> CAST(@queryEmbedding AS VECTOR)
            LIMIT @limit
            """;

        var result = new List<RetrievedChunk>();

        await using var command = new NpgsqlCommand(searchSql, connection);
        command.Parameters.AddWithValue("queryEmbedding", ToVectorLiteral(queryEmbedding));
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new RetrievedChunk(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetDouble(4)
            ));
        }

        return result;
    }

    private static string ToVectorLiteral(float[] values)
    {
        return $"[{string.Join(',', values.Select(x => x.ToString("G9", CultureInfo.InvariantCulture)))}]";
    }
}
