# KernelSemantico.API

API .NET com Semantic Kernel para cenários de agente e RAG com pgvector.

## Segurança de segredos

Nunca versione chaves de API no repositório.

Configure a chave da OpenAI por uma destas opções:

1. Variável de ambiente `OPENAI_API_KEY`
2. User Secrets (`dotnet user-secrets`)
3. Arquivo local `KernelSemantico.API/appsettings.Local.json` (não versionado)

## Configuração do RAG (Fase 2 + Fase 3)

A API usa PostgreSQL com extensão pgvector para persistir embeddings.

### 1) Subir um PostgreSQL com pgvector (exemplo Docker)

```bash
docker run --name pgvector-rag -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres -e POSTGRES_DB=ragdb -p 5432:5432 -d pgvector/pgvector:pg17
```

### 2) Configurar segredos locais

Exemplo de `KernelSemantico.API/appsettings.Local.json`:

```json
{
  "OpenAI": {
    "ApiKey": "sua-chave-aqui"
  },
  "Rag": {
    "ConnectionString": "Host=localhost;Port=5432;Database=ragdb;Username=postgres;Password=postgres",
    "EmbeddingModel": "text-embedding-3-small",
    "EmbeddingDimensions": 1536,
    "VectorWeight": 0.7,
    "KeywordWeight": 0.3,
    "Guardrails": {
      "MaxQuestionLength": 2000,
      "MaxDocumentLength": 50000,
      "BlockedPatterns": [
        "ignore previous instructions",
        "system prompt",
        "jailbreak"
      ]
    }
  }
}
```

Alternativa via variável de ambiente para o banco vetorial:

- `RAG_VECTOR_DB_CONNECTION`

> A API cria/garante automaticamente tabela e índice vetorial no startup.

## Endpoints

### Agente padrão
- `POST /api/agent`

Body:

```json
{
  "message": "liste os clientes"
}
```

### Ingestão RAG
- `POST /api/rag/ingest`

Body:

```json
{
  "documentId": "politica-ferias",
  "title": "Política de Férias",
  "content": "Texto completo do documento..."
}
```

### Pergunta com RAG
- `POST /api/rag/ask`

Body:

```json
{
  "message": "quantos dias posso parcelar minhas férias?",
  "topK": 3
}
```

Resposta esperada:

```json
{
  "answer": "...",
  "sources": [
    {
      "documentId": "politica-ferias",
      "title": "Política de Férias",
      "chunkIndex": 1,
      "score": 0.8734
    }
  ]
}
```

### Avaliação RAG
- `POST /api/rag/evaluate`

Body:

```json
{
  "topK": 3,
  "cases": [
    {
      "id": "ferias-1",
      "question": "Quantos dias posso parcelar minhas férias?",
      "expectedContains": "30",
      "expectedSourceDocumentId": "politica-ferias"
    }
  ]
}
```

Resposta esperada (resumo):

```json
{
  "totalCases": 1,
  "passedCases": 1,
  "failedCases": 0,
  "blockedCases": 0,
  "averageLatencyMs": 412.75,
  "results": [
    {
      "id": "ferias-1",
      "passed": true,
      "blocked": false,
      "hasSources": true,
      "expectedTextFound": true,
      "expectedSourceFound": true,
      "latencyMs": 412.75,
      "reason": "OK",
      "answerSnippet": "...",
      "sources": []
    }
  ]
}
```

## Observabilidade (Fase 3)

A API agora publica instrumentação via `System.Diagnostics`:

- ActivitySource: `KernelSemantico.API.Rag`
- Meter: `KernelSemantico.API.Rag`

Métricas principais:
- `rag_ingest_requests_total`
- `rag_ingest_errors_total`
- `rag_ingest_duration_ms`
- `rag_ask_requests_total`
- `rag_ask_errors_total`
- `rag_guardrail_blocked_total`
- `rag_ask_duration_ms`
- `rag_retrieval_candidate_count`
- `rag_evaluate_requests_total`
- `rag_evaluate_errors_total`
- `rag_evaluate_duration_ms`

## Próximos passos recomendados

- Expor essas métricas para Prometheus/OpenTelemetry exporter
- Adicionar autorização por tenant/documento
- Adicionar avaliação contínua em CI com dataset versionado
