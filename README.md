# KernelSemantico.API

API .NET com Semantic Kernel para cenários de agente e base de RAG.

## Segurança de segredos

Nunca versione chaves de API no repositório.

Configure a chave da OpenAI por uma destas opções:

1. Variável de ambiente `OPENAI_API_KEY`
2. User Secrets (`dotnet user-secrets`)
3. Arquivo local `KernelSemantico.API/appsettings.Local.json` (não versionado)

Exemplo de `appsettings.Local.json`:

```json
{
  "OpenAI": {
    "ApiKey": "sua-chave-aqui"
  }
}
```

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
      "chunkIndex": 1
    }
  ]
}
```

## Próximos passos recomendados

- Substituir o store em memória por banco vetorial (pgvector, Qdrant ou Azure AI Search)
- Adicionar embeddings e busca híbrida
- Adicionar avaliação automatizada (groundedness, latência, custo)
