# agent.ai — Estudos com Microsoft.Agents.AI

Repositório de estudos sobre criação de agentes de IA com o SDK **Microsoft.Agents.AI**, integrando ao modelo **Claude** da Anthropic via a biblioteca `Microsoft.Agents.AI.Anthropic`.

## Projetos

| Projeto | Descrição |
|---|---|
| [`agent.ai`](./agent.ai/) | Agente simples com resposta completa (sem streaming) |
| [`agent.with.streaming`](./agent.with.streaming/) | Agente com resposta em streaming token a token |

Ambos implementam um **assistente de filosofia** em português como contexto de aprendizado. O objetivo é comparar as duas abordagens de consumo de respostas da API — resposta única vs. streaming incremental.

## Stack

- **.NET 10.0** (C# com top-level statements)
- **Microsoft.Agents.AI.Anthropic** `1.0.0-rc6`
- **Claude Haiku 4.5** como modelo de linguagem

## Como executar

1. Substitua `"SUA_API_KEY"` pela sua chave da API da Anthropic em `Program.cs` de cada projeto.
2. Execute o projeto desejado:

```bash
# Sem streaming
cd agent.ai
dotnet run

# Com streaming
cd agent.with.streaming
dotnet run
```

## Conceitos cobertos

- Inicialização do `AnthropicClient` e conversão para `AIAgent`
- Definição de instruções de sistema (system prompt) e nome do agente
- Resposta completa via `RunAsync()`
- Resposta em streaming via `RunStreamingAsync()` com `await foreach`
