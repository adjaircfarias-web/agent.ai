# agent.ai — Agente sem Streaming

Aplicação console que demonstra a forma mais simples de usar um agente de IA: enviar uma pergunta e aguardar a resposta **completa** antes de exibi-la.

## O que faz

Cria um assistente de filosofia que recebe uma pergunta do usuário e retorna a resposta inteira de uma só vez.

```
Olá, sou seu Agente de filosofia!
Faça sua pergunta: O que é o imperativo categórico de Kant?

[resposta completa exibida aqui após o modelo terminar de gerar]
```

## Código comentado

```csharp
// 1. Cliente Anthropic com a chave de API
AnthropicClient client = new() { ApiKey = apiKey };

// 2. Converte o cliente em um AIAgent com instruções e modelo definidos
AIAgent agent = client.AsAIAgent(
    instructions: "Você ajuda pessoas a entenderem de filosofia.",
    model: "claude-haiku-4-5",
    name: "Assitente de filosofia");

// 3. Executa o agente e aguarda a resposta completa
var response = await agent.RunAsync(question);
Console.WriteLine(response);
```

## Conceitos aplicados

### `AnthropicClient`
Classe principal do SDK `Microsoft.Agents.AI.Anthropic`. Representa a conexão com a API da Anthropic. Requer uma `ApiKey` para autenticação.

### `.AsAIAgent()`
Método de extensão que converte o `AnthropicClient` em um `AIAgent` da abstração `Microsoft.Agents.AI`. Aceita:
- **`instructions`**: o system prompt — define a personalidade e o escopo do agente.
- **`model`**: o modelo de linguagem a usar (ex: `claude-haiku-4-5`).
- **`name`**: nome identificador do agente.

### `AIAgent`
Abstração de alto nível do Microsoft Agent Framework. Encapsula a lógica de envio de mensagens e gerenciamento de contexto, permitindo trocar o provedor de IA (Anthropic, OpenAI, etc.) sem mudar o código de negócio.

### `RunAsync(string input)`
Envia a mensagem do usuário ao modelo e retorna a resposta **completa** como `string` após o modelo terminar toda a geração. Adequado para respostas curtas onde a latência total não é crítica.

### Quando usar esta abordagem
- Respostas curtas ou de latência previsível
- Quando o resultado precisa ser processado inteiramente antes de exibir (ex: parsing de JSON)
- Simplicidade de implementação é prioridade

## Dependências

```xml
<PackageReference Include="Microsoft.Agents.AI.Anthropic" Version="1.0.0-rc6" />
```
