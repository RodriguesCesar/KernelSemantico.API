# KernelSemantico.API

[![CI](https://github.com/RodriguesCesar/KernelSemantico.API/actions/workflows/ci.yml/badge.svg)](https://github.com/RodriguesCesar/KernelSemantico.API/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Release](https://img.shields.io/github/v/release/RodriguesCesar/KernelSemantico.API?display_name=tag)](https://github.com/RodriguesCesar/KernelSemantico.API/releases)

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
    "ConnectionString": "Host=localhost;Port=5432;Database=ragdb;Username=postgres;******",
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
  },
  "OpenTelemetry": {
    "ServiceName": "KernelSemantico.API",
    "Otlp": {
      "Endpoint": "http://localhost:4317",
      "Protocol": "grpc"
    }
  }
}
```

Alternativa por variáveis de ambiente:

- `RAG_VECTOR_DB_CONNECTION`
- `OTEL_EXPORTER_OTLP_ENDPOINT`
- `OTEL_EXPORTER_OTLP_PROTOCOL` (`grpc` ou `http/protobuf`)

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

## Observabilidade (Fase 3 + OpenTelemetry)

A API publica instrumentação via `System.Diagnostics` e exporta via OTLP.

Sinais exportados:
- Traces (`AspNetCore`, `HttpClient`, `KernelSemantico.API.Rag`)
- Metrics (`AspNetCore`, `HttpClient`, `Runtime`, `KernelSemantico.API.Rag`)

Métricas RAG principais:
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

### Stack local de observabilidade

Foi adicionado um stack local em `docker-compose.observability.yml` com:

- OTel Collector (OTLP receiver + export de traces e métricas)
- Jaeger (visualização de traces)
- Prometheus (coleta de métricas)
- Grafana (dashboards)

Subir stack:

```bash
docker compose -f docker-compose.observability.yml up -d
```

Acessos locais:

- Grafana: `http://localhost:3000` (admin/admin)
- Jaeger: `http://localhost:16686`
- Prometheus: `http://localhost:9090`

Dashboard provisionado automaticamente:

- `KernelSemantico / RAG Overview`
- Arquivo: `observability/grafana/dashboards/rag-overview.json`

> Se o stack já estiver rodando, reinicie o serviço do Grafana para forçar o re-load das provisões.

## CI/CD (GitHub Actions)

Status atual: **pipeline de CI implementada** em `.github/workflows/ci.yml`.

A pipeline executa em `push` e `pull_request` para `master` (além de `workflow_dispatch`) com os passos:

1. `dotnet restore` da solução
2. `dotnet build --configuration Release --no-restore`
3. `dotnet test` para projetos de teste detectados automaticamente
   - se não houver projeto de teste no repositório, a etapa de testes é pulada sem falha

## Próximos passos recomendados

- Adicionar autorização por tenant/documento
- Adicionar avaliação contínua em CI com dataset versionado
- Publicar dashboard customizado no Grafana para SLOs do RAG
