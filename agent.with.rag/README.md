# agent.with.rag

Agente de IA com pipeline **RAG (Retrieval-Augmented Generation)** implementado do zero em .NET 10. Demonstra como combinar recuperação de informações por similaridade vetorial (TF-IDF) com geração de respostas fundamentadas usando a API da Anthropic.

A base de conhecimento contém documentos sobre filósofos clássicos (Kant, Hegel, Platão, Descartes e Nietzsche), todos em português.

---

## Arquitetura do Pipeline

```
┌──────────────────────────────────────────────────────────┐
│                    INDEXAÇÃO (uma vez)                   │
│                                                          │
│  Base de Conhecimento                                    │
│  (5 documentos de filosofia)                             │
│           │                                              │
│           ▼                                              │
│       Chunking                                           │
│  (1 chunk por documento)                                 │
│           │                                              │
│           ▼                                              │
│    Indexação TF-IDF                                      │
│  tokenizar → remover stopwords → normalizar acentos      │
│  → calcular TF×IDF → normalizar L2                       │
│           │                                              │
│           ▼                                              │
│   Índice Vetorial [5 vetores]                            │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│                  CONSULTA (por pergunta)                 │
│                                                          │
│  Pergunta do usuário                                     │
│           │                                              │
│           ▼                                              │
│   Vetorização da query                                   │
│  (mesmo pipeline TF-IDF)                                 │
│           │                                              │
│           ▼                                              │
│   Recuperação Top-K                                      │
│  CosineSimilarity via TensorPrimitives                   │
│  → ordena por score → retorna top 2                      │
│           │                                              │
│           ▼                                              │
│   Injeção de Contexto                                    │
│  prompt aumentado = sistema + chunks + pergunta          │
│           │                                              │
│           ▼                                              │
│   Geração (Anthropic claude-haiku-4-5)                   │
│  RunStreamingAsync → resposta fundamentada               │
└──────────────────────────────────────────────────────────┘
```

---

## Como Funciona

### 1. Base de Conhecimento
Cinco documentos didáticos em português sobre filósofos clássicos, carregados diretamente no código como strings. Em produção, viriam de arquivos, banco de dados ou APIs.

### 2. Chunking
Cada documento é tratado como um único chunk, identificado por `Id`, `Texto` e `Origem` (e.g., `documento_0`). Para textos maiores, o chunking seria feito por tokens ou sentenças.

### 3. Indexação TF-IDF
- **Tokenização**: lowercase, remoção de pontuação, comprimento mínimo de 3 caracteres
- **Remoção de stopwords**: lista de ~120 palavras comuns em português
- **Normalização de acentos**: decomposição Unicode (FormD) para tratar variações como `ã` e `a`
- **TF (Term Frequency)**: frequência relativa do termo no chunk
- **IDF (Inverse Document Frequency)**: `log(N / df)` — termos raros recebem peso maior
- **Normalização L2**: cada vetor é normalizado para `||v|| = 1`, tornando a similaridade cosseno equivalente ao produto escalar

### 4. Recuperação Top-K
A query do usuário passa pelo mesmo pipeline de tokenização e vetorização. A similaridade cosseno é calculada entre a query e todos os chunks usando `TensorPrimitives.CosineSimilarity<float>`, que usa instruções SIMD (AVX/SSE) automaticamente no .NET 10. Os 2 chunks com maior score são retornados (`TopK = 2`).

### 5. Injeção de Contexto
O prompt enviado ao modelo é aumentado com os chunks recuperados. O sistema instrui o modelo a responder **exclusivamente com base no contexto fornecido**, reduzindo alucinações e garantindo rastreabilidade por fonte.

### 6. Geração com Streaming
O `AIAgent` encapsula o `AnthropicClient` e faz a chamada via `RunStreamingAsync`, imprimindo a resposta em tempo real conforme os tokens chegam.

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Chave de API da Anthropic

---

## Configuração

Abra `Program.cs` e substitua o valor da variável `apiKey` pela sua chave:

```csharp
// Linha ~243
string apiKey = "SUA_API_KEY";
```

Alternativamente, você pode ler de uma variável de ambiente:

```csharp
string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY não definida.");
```

---

## Como Executar

Na raiz do projeto `agent.with.rag`:

```bash
dotnet run
```

O programa irá:
1. Indexar a base de conhecimento (exibindo cada etapa no console)
2. Solicitar uma pergunta ao usuário
3. Recuperar os chunks mais relevantes (exibindo scores)
4. Gerar e transmitir a resposta fundamentada

---

## Como Testar

### Perguntas que devem funcionar bem

Estas perguntas estão cobertas pela base de conhecimento e devem retornar respostas fundamentadas:

| Pergunta | Filósofo esperado |
|---|---|
| `O que é o imperativo categórico?` | Kant |
| `Explique a dialética de Hegel` | Hegel |
| `O que é a alegoria da caverna?` | Platão |
| `O que significa cogito ergo sum?` | Descartes |
| `O que Nietzsche quis dizer com Deus está morto?` | Nietzsche |
| `O que é a dúvida cartesiana?` | Descartes |
| `O que é o Aufhebung?` | Hegel |
| `O que são as Ideias ou Formas de Platão?` | Platão |
| `O que é a vontade de poder?` | Nietzsche |

### O que verificar

1. **Scores de recuperação**: o console exibe os chunks recuperados e seus scores. Perguntas bem alinhadas com a base devem retornar scores acima de `0.3`.

2. **Fundamentação**: a resposta deve citar apenas informações presentes nos chunks recuperados. Se o modelo disser que "não encontrou na base de conhecimento", é sinal de que o retrieval não encontrou chunks relevantes.

3. **Atribuição de fonte**: a resposta menciona a origem (`[documento_X]`), permitindo rastrear de onde veio a informação.

### Pergunta fora da base (comportamento esperado)

```
Quem foi Albert Einstein?
```

Nenhum chunk sobre física ou Einstein existe na base. O modelo deve responder que não encontrou a informação na base de conhecimento — validando que o grounding está funcionando.

---

## Constantes Configuráveis

| Constante | Localização | Valor padrão | Descrição |
|---|---|---|---|
| `TopK` | `Program.cs` linha ~199 | `2` | Número de chunks recuperados |
| `model` | `Program.cs` linha ~248 | `"claude-haiku-4-5"` | Modelo da Anthropic |
| `apiKey` | `Program.cs` linha ~243 | `"SUA_API_KEY"` | Chave de API |

---

## Dependências

```xml
<PackageReference Include="Microsoft.Agents.AI.Anthropic" Version="1.0.0-rc6" />
```

Esta única dependência traz transitivamente:
- `Microsoft.Extensions.AI` — abstrações do agente
- `System.Numerics.Tensors` — cálculo de similaridade cosseno com SIMD
