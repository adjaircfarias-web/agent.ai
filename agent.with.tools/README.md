# agent.with.tools — Agente com Tool Use / Function Calling

Aplicação console que demonstra o conceito mais poderoso de agentes de IA: **Tool Use** (também chamado de _Function Calling_). O modelo decide autonomamente quando e como chamar funções C# externas para enriquecer sua resposta — sem que o código de negócio precise orquestrar essa lógica.

O agente é um assistente de filosofia que consulta uma "API" (simulada) para buscar dados sobre filósofos antes de responder.

---

## O que acontece na prática

```
[TOOL] Ferramenta registrada: ConsultarApiFilosofia

Olá! Sou seu Assistente de Filosofia com suporte a Tools.
Faça sua pergunta: O que Kant defendia sobre a moral?

[TOOL] Agente invocou: ConsultarApiFilosofia("Kant")
[TOOL] API respondeu com dados sobre "kant" (~600ms simulados).

Immanuel Kant defendia que a moral deve ser baseada na razão pura...
[resposta em streaming com base nos dados retornados pela ferramenta]
```

O modelo leu a pergunta, identificou que precisava de dados sobre Kant, chamou a ferramenta, recebeu os dados e só então gerou a resposta — tudo de forma autônoma.

---

## Conceitos aplicados

### 1. Tool Use / Function Calling

Tool Use é o mecanismo pelo qual um LLM pode invocar funções externas durante a geração de uma resposta. O modelo não tem acesso direto aos dados — ele **solicita** a chamada da ferramenta ao framework, que executa o método C# e devolve o resultado para o modelo continuar.

O ponto central: **o modelo decide quando chamar a ferramenta**, baseado na descrição fornecida no registro. O código de aplicação apenas define o que a ferramenta faz.

---

### 2. A ferramenta: `ConsultarApiFilosofia()`

Método estático que simula uma chamada HTTP real:

```csharp
static async Task<string> ConsultarApiFilosofia(string filosofo)
{
    await Task.Delay(600); // simula latência de rede

    var base_dados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["kant"]      = "Immanuel Kant (1724–1804) — ...",
        ["hegel"]     = "Georg Wilhelm Friedrich Hegel (1770–1831) — ...",
        ["platão"]    = "Platão (428–348 a.C.) — ...",
        // ...
    };

    foreach (var (chave, dados) in base_dados)
    {
        if (filosofo.Contains(chave, StringComparison.OrdinalIgnoreCase) ||
            chave.Contains(filosofo, StringComparison.OrdinalIgnoreCase))
            return dados.Trim();
    }

    return $"Filósofo \"{filosofo}\" não encontrado na base de dados da API.";
}
```

- **`Task.Delay(600)`** — simula os ~600ms de latência de uma rede real.
- **Dicionário com `StringComparer.OrdinalIgnoreCase`** — busca insensível a maiúsculas/minúsculas.
- **Fuzzy matching** — verifica se o nome passado está contido na chave, ou a chave no nome, para tolerar variações como "Immanuel Kant" vs. "Kant".
- **Retorno tipado como `string`** — o framework serializa esse retorno e o envia de volta ao modelo.

---

### 3. `AIFunctionFactory.Create()` — registrando a ferramenta

```csharp
var ferramenta = AIFunctionFactory.Create(
    ConsultarApiFilosofia,
    "ConsultarApiFilosofia",
    "Consulta a API de filosofia e retorna informações detalhadas sobre um filósofo pelo nome. " +
    "Use esta ferramenta sempre que precisar de dados sobre um filósofo.");
```

`AIFunctionFactory.Create()` (do pacote `Microsoft.Extensions.AI`) envolve o método estático em um objeto `AIFunction`. Ele usa reflection para extrair automaticamente o tipo dos parâmetros e expõe essa assinatura ao modelo.

Os três argumentos têm papéis distintos:

| Argumento | Função |
|---|---|
| `ConsultarApiFilosofia` | Delegate do método a ser executado |
| `"ConsultarApiFilosofia"` | Nome da ferramenta — como o modelo a referencia na chamada |
| A descrição longa | **Instrução em linguagem natural** para o modelo sobre quando e como usar a tool |

> A descrição é crítica. É ela que o modelo lê para decidir se deve ou não invocar a ferramenta para uma determinada pergunta.

---

### 4. Parâmetro `tools` no `AsAIAgent()`

```csharp
AIAgent agente = client.AsAIAgent(
    instructions: """
        Você é um assistente especializado em filosofia.
        Sempre que o usuário perguntar sobre um filósofo ou conceito filosófico,
        use a ferramenta ConsultarApiFilosofia para buscar os dados na API antes de responder.
        Baseie sua resposta nos dados retornados pela ferramenta.
        Se a API não encontrar o filósofo, informe gentilmente ao usuário e não tente adivinhar ou inventar informações.
        Não responda perguntas que você não encontrar dados na API.
        Responda sempre em português.
        """,
    model: "claude-haiku-4-5",
    name: "Assistente de Filosofia com Tools",
    tools: [ferramenta]);
```

O parâmetro `tools` recebe um array de `AIFunction`. O framework serializa as definições das ferramentas e as envia ao modelo em cada requisição, junto com as instruções do sistema.

O `instructions` aqui também reforça o comportamento esperado: não inventar dados e usar a ferramenta sempre que necessário.

---

### 5. O ciclo de Tool Invocation — o que acontece por baixo

Quando `RunStreamingAsync()` é chamado, o `AIAgent` gerencia todo o protocolo de function calling internamente:

```
Usuário faz pergunta
       ↓
Claude recebe: pergunta + definições das ferramentas
       ↓
Claude decide: "preciso chamar ConsultarApiFilosofia('Kant')"
       ↓
AIAgent executa: ConsultarApiFilosofia("Kant") → retorna string com dados
       ↓
Claude recebe o resultado e gera a resposta final
       ↓
Tokens chegam em streaming via await foreach
```

O código de aplicação **não precisa orquestrar nenhum desses passos** — tudo é gerenciado pelo `AIAgent`. A ferramenta pode ser chamada zero, uma ou várias vezes por resposta, conforme o modelo julgar necessário.

---

### 6. Streaming com `await foreach`

```csharp
await foreach (var fragmento in agente.RunStreamingAsync(pergunta))
    Console.Write(fragmento);
```

Mesmo padrão dos projetos anteriores (`agent.with.streaming`), mas agora o loop de streaming inclui as etapas de tool invocation internas. Os logs em azul (`[TOOL]`) são impressos diretamente dentro de `ConsultarApiFilosofia()`, permitindo observar o momento exato em que a ferramenta é chamada.

---

## Filósofos suportados

| Filósofo | Período | Conceitos principais |
|---|---|---|
| Kant | 1724–1804 | Imperativo categórico, idealismo transcendental |
| Hegel | 1770–1831 | Dialética, Geist, Aufhebung |
| Platão | 428–348 a.C. | Teoria das Formas, Alegoria da Caverna |
| Descartes | 1596–1650 | Cogito ergo sum, dualismo mente-corpo |
| Nietzsche | 1844–1900 | Vontade de poder, Übermensch, eterno retorno |
| Sócrates | 470–399 a.C. | Maiêutica, ironia socrática |
| Aristóteles | 384–322 a.C. | Silogismo, eudaimonia, potência e ato |

---

## Como executar

1. Abra `Program.cs` e substitua `"SUA_API_KEY"` pela sua chave da API da Anthropic.
2. Execute:

```bash
cd agent.with.tools
dotnet run
```

---

## Dependências

```xml
<PackageReference Include="Microsoft.Agents.AI.Anthropic" Version="1.0.0-rc6" />
```

---

## Comparativo com os outros projetos

| Projeto | Resposta | Tool Use | Conceito central |
|---|---|---|---|
| `agent.ai` | Completa (`RunAsync`) | Não | Resposta síncrona |
| `agent.with.streaming` | Streaming (`await foreach`) | Não | Streaming de tokens |
| `agent.with.tools` | Streaming (`await foreach`) | **Sim** | Function Calling |
