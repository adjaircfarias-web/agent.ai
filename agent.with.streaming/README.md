# agent.with.streaming — Agente com Streaming

Aplicação console que demonstra o uso de **streaming** em agentes de IA: os tokens da resposta são exibidos incrementalmente conforme o modelo os gera, sem esperar a geração completa.

## O que faz

O mesmo assistente de filosofia do projeto `agent.ai`, porém a resposta aparece progressivamente na tela — palavra por palavra, à medida que o modelo produz o conteúdo.

```
Olá, sou seu Agente de filosofia!
Faça sua pergunta: O que é dialética em Hegel?

[tokens aparecem aqui um a um, em tempo real]
```

## Código comentado

```csharp
// 1. Inicialização idêntica ao projeto sem streaming
AnthropicClient client = new() { ApiKey = apiKey };

AIAgent agent = client.AsAIAgent(
    instructions: "Você ajuda pessoas a entenderem de filosofia.",
    model: "claude-haiku-4-5",
    name: "Assitente de filosofia");

// 2. RunStreamingAsync retorna IAsyncEnumerable<string>
//    await foreach consome cada fragmento conforme chega
await foreach (var update in agent.RunStreamingAsync(question))
    Console.WriteLine(update);
```

## Conceitos aplicados

### `RunStreamingAsync(string input)`
Diferentemente de `RunAsync()`, este método retorna um `IAsyncEnumerable<string>`. Em vez de bloquear até a resposta completa, ele produz fragmentos (chunks) à medida que o modelo os gera via Server-Sent Events (SSE) na API da Anthropic.

### `IAsyncEnumerable<T>`
Interface do C# que representa uma sequência assíncrona de valores. Permite iterar sobre dados que chegam ao longo do tempo sem bloquear a thread. É o padrão moderno para streams em .NET.

### `await foreach`
Sintaxe do C# para consumir um `IAsyncEnumerable<T>`. A cada iteração:
1. Aguarda assincronamente o próximo fragmento do stream
2. Executa o corpo do loop com esse fragmento
3. Repete até o stream ser encerrado pelo servidor

```csharp
await foreach (var update in agent.RunStreamingAsync(question))
    Console.WriteLine(update); // executado para cada fragmento recebido
```

### Por que streaming melhora a experiência

| Aspecto | Sem Streaming | Com Streaming |
|---|---|---|
| Primeira resposta visível | Após geração completa | Nos primeiros tokens |
| Latência percebida | Alta | Baixa |
| Implementação | Mais simples | Levemente mais complexa |
| Ideal para | Respostas curtas / processamento posterior | Respostas longas / UX conversacional |

### Quando usar esta abordagem
- Respostas longas onde o usuário pode começar a ler antes do fim
- Interfaces conversacionais (chat) onde a fluidez é importante
- Quando a latência percebida importa mais que a latência real

## Dependências

```xml
<PackageReference Include="Microsoft.Agents.AI.Anthropic" Version="1.0.0-rc6" />
```
