// =============================================================================
// agent.with.rag — Demonstração de RAG (Retrieval-Augmented Generation)
//
// Pipeline:
//   1. Carregar documentos em memória (base de conhecimento)
//   2. Dividir em chunks
//   3. Indexar com TF-IDF para recuperação por palavra-chave
//   4. Receber pergunta do usuário
//   5. Recuperar os K chunks mais relevantes (similaridade de cosseno)
//   6. Injetar contexto no prompt do agente
//   7. Gerar resposta via AnthropicClient + Microsoft.Agents.AI
// =============================================================================

using System.Numerics.Tensors;   // TensorPrimitives.CosineSimilarity — dep. transitiva
using System.Text;
using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// ── PASSO 1: BASE DE CONHECIMENTO ────────────────────────────────────────────
// Documentos simulados sobre filosofia. Em produção, estes viriam de arquivos,
// banco de dados ou APIs. Aqui ficam em memória para clareza didática.

var documentos = new[]
{
    // Documento 0 — Kant
    """
    O imperativo categórico é o conceito central da ética de Immanuel Kant.
    Kant afirma que devemos agir somente segundo a máxima que possamos, ao mesmo
    tempo, querer que se torne uma lei universal. Diferente dos imperativos
    hipotéticos, que dependem de um objetivo desejado, o imperativo categórico
    exige que a ação moral seja válida incondicionalmente, independentemente de
    consequências. A fórmula da humanidade, segunda formulação do imperativo,
    ordena que tratemos a humanidade — em nossa pessoa e na dos outros — sempre
    como fim em si mesmo, jamais apenas como meio.
    """,

    // Documento 1 — Hegel
    """
    A dialética de Georg Wilhelm Friedrich Hegel descreve o movimento do
    pensamento e da realidade por meio de três momentos: tese, antítese e síntese.
    Para Hegel, toda ideia contém em si a semente de sua própria negação. A tensão
    entre a ideia inicial (tese) e sua negação (antítese) é resolvida numa síntese
    superior que supera e conserva ambos os momentos. Esse processo, que Hegel
    chama de Aufhebung (superação), é o motor da história do Espírito Absoluto.
    A Fenomenologia do Espírito traça a jornada da consciência desde a percepção
    sensível até o saber absoluto.
    """,

    // Documento 2 — Platão
    """
    A Alegoria da Caverna, apresentada no Livro VII de A República de Platão,
    descreve prisioneiros acorrentados no fundo de uma caverna que só conseguem
    ver sombras projetadas na parede. Ao ser libertado, um prisioneiro sobe ao
    mundo exterior e enxerga as coisas reais iluminadas pelo sol. Para Platão,
    a caverna representa o mundo sensível das aparências, e a saída simboliza a
    ascensão da alma ao mundo inteligível das Formas ou Ideias. O sol é a metáfora
    do Bem supremo, que ilumina todo conhecimento verdadeiro. O filósofo que
    retorna à caverna para libertar os demais representa o papel político da
    educação filosófica.
    """,

    // Documento 3 — Descartes
    """
    René Descartes, em suas Meditações Metafísicas, aplica a dúvida metódica
    como instrumento para encontrar uma verdade indubitável. Ao duvidar de tudo
    — dos sentidos, do mundo externo, até da existência do próprio corpo —,
    Descartes percebe que o único ato que não pode ser colocado em dúvida é o
    próprio ato de duvidar. Daí surge o cogito: "penso, logo existo" (cogito,
    ergo sum). Essa certeza imediata e indubitável torna-se o ponto arquimediano
    de toda a filosofia cartesiana. A partir do cogito, Descartes reconstrói o
    conhecimento, provando a existência de Deus e, por meio d'Ele, a confiabilidade
    do mundo externo.
    """,

    // Documento 4 — Nietzsche
    """
    Friedrich Nietzsche proclama que "Deus está morto" na obra A Gaia Ciência,
    significando não um argumento teológico, mas o colapso dos valores
    transcendentes que sustentavam a moral ocidental. O niilismo, para Nietzsche,
    é a consequência inevitável desse vácuo de sentido. A resposta nietzschiana
    ao niilismo é a transvaloração de todos os valores: criar novos valores a
    partir da afirmação da vida e da vontade de poder. O conceito de Übermensch
    (super-homem) representa o ser humano capaz de criar seu próprio sentido sem
    depender de fundamentos externos. O eterno retorno é outro conceito central:
    a ideia de que deveríamos agir como se cada momento fosse repetir-se
    infinitamente.
    """
};

// ── PASSO 2: CHUNKING ─────────────────────────────────────────────────────────
// Cada documento já é um chunk neste exemplo (parágrafos bem delimitados).
// Em produção, textos longos seriam divididos por tamanho de tokens ou sentenças.
// Chunk carrega id, texto e origem para rastreabilidade na resposta final.

var chunks = documentos
    .Select((texto, i) => new Chunk(
        Id: i,
        Texto: texto.Trim(),
        Origem: $"documento_{i}"))
    .ToArray();

Console.WriteLine($"[RAG] {chunks.Length} chunks carregados na base de conhecimento.\n");

// ── PASSO 3: INDEXAÇÃO TF-IDF ─────────────────────────────────────────────────
// TF-IDF (Term Frequency × Inverse Document Frequency) representa cada chunk
// como um vetor de pesos por palavra. Quanto mais rara a palavra no corpus e
// mais frequente no chunk, maior o peso.

// Stopwords básicas em português. Declaradas aqui para serem capturadas pelo
// closure de Tokenizar abaixo (local functions static não aceitam closures).
var stopwords = new HashSet<string>
{
    "a", "ao", "as", "com", "da", "das", "de", "do", "dos", "e", "em",
    "ela", "ele", "eles", "elas", "entre", "essa", "esse", "eu",
    "isso", "ja", "mais", "mas", "na", "nas", "nao", "no", "nos", "o",
    "os", "ou", "para", "pela", "pelas", "pelo", "pelos", "por", "que",
    "se", "sem", "seu", "seus", "sua", "suas", "tambem", "toda", "todo",
    "todos", "um", "uma", "uns", "umas"
};

// Tokeniza e filtra o texto, removendo acentos, pontuação e stopwords.
// NormalizationForm.FormD decompõe 'ã' → 'a' + combining-tilde.
// Ao remover NonSpacingMark, "máxima" e "maxima" mapeiam ao mesmo token.
string[] Tokenizar(string texto)
{
    var sb = new StringBuilder();
    foreach (char c in texto.Normalize(NormalizationForm.FormD))
    {
        var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
        if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
            sb.Append(char.IsLetter(c) || char.IsWhiteSpace(c) ? char.ToLowerInvariant(c) : ' ');
    }
    return sb.ToString()
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(t => t.Length > 2 && !stopwords.Contains(t))
        .ToArray();
}

// TF = frequência relativa do termo no chunk (evita favorecer chunks mais longos).
static Dictionary<string, float> CalcularTF(string[] tokens)
{
    var freq = new Dictionary<string, int>();
    foreach (var t in tokens)
        freq[t] = freq.GetValueOrDefault(t) + 1;
    return freq.ToDictionary(kv => kv.Key, kv => (float)kv.Value / tokens.Length);
}

// Tokeniza todos os chunks e constrói o vocabulário global.
var todosTokens = chunks.Select(c => Tokenizar(c.Texto)).ToArray();
var vocabulario = todosTokens.SelectMany(t => t).Distinct().Order().ToArray();
var vocabIndex  = vocabulario.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);

Console.WriteLine($"[RAG] Vocabulário TF-IDF: {vocabulario.Length} termos únicos.\n");

// df[i] = número de chunks que contêm o termo i.
// IDF = log(N / df): termos raros têm IDF alto (mais discriminativos).
var df = new int[vocabulario.Length];
foreach (var tokens in todosTokens)
    foreach (var termo in tokens.Distinct())
        if (vocabIndex.TryGetValue(termo, out int idx))
            df[idx]++;

float N = chunks.Length;
var idf = df.Select(d => d > 0 ? MathF.Log(N / d) : 0f).ToArray();

// Gera vetor TF-IDF com normalização L2 para um conjunto de tokens.
// L2: ||vec|| = 1 → CosineSimilarity equivale ao produto escalar.
float[] CriarVetor(string[] tokens)
{
    var tf  = CalcularTF(tokens);
    var vec = new float[vocabulario.Length];
    foreach (var (termo, peso) in tf)
        if (vocabIndex.TryGetValue(termo, out int idx))
            vec[idx] = peso * idf[idx];

    float norma = MathF.Sqrt(vec.Sum(x => x * x));
    if (norma > 0)
        for (int i = 0; i < vec.Length; i++)
            vec[i] /= norma;
    return vec;
}

// indice[i] = vetor TF-IDF normalizado do chunk i, pronto para busca.
var indice = todosTokens.Select(CriarVetor).ToArray();

// ── PASSO 4: ENTRADA DO USUÁRIO ───────────────────────────────────────────────

Console.WriteLine("Olá! Sou seu Assistente de Filosofia com RAG.");
Console.Write("Faça sua pergunta: ");
string pergunta = Console.ReadLine() ?? string.Empty;

if (string.IsNullOrWhiteSpace(pergunta))
{
    Console.Error.WriteLine("Nenhuma pergunta fornecida.");
    return;
}

// ── PASSO 5: RECUPERAÇÃO TOP-K ────────────────────────────────────────────────
// Converte a pergunta para o mesmo espaço TF-IDF e ranqueia os chunks via
// TensorPrimitives.CosineSimilarity (System.Numerics.Tensors — dep. transitiva).
// TensorPrimitives usa intrinsics AVX/SSE automaticamente no .NET 10.

const int TopK = 2;

var vetorQuery = CriarVetor(Tokenizar(pergunta));

var scores = indice
    .Select((vetorChunk, i) => (
        Chunk: chunks[i],
        Score: TensorPrimitives.CosineSimilarity<float>(vetorQuery, vetorChunk)))
    .OrderByDescending(x => x.Score)
    .Take(TopK)
    .ToArray();

// Log educacional: exibe quais chunks foram recuperados e seus scores.
Console.WriteLine($"\n[RAG] Chunks recuperados (Top-{TopK}):");
foreach (var (chunk, score) in scores)
    Console.WriteLine($"  • {chunk.Origem} | score={score:F4} | {chunk.Texto[..Math.Min(60, chunk.Texto.Length)]}...");

// ── PASSO 6: INJEÇÃO DE CONTEXTO ─────────────────────────────────────────────
// Monta um prompt aumentado fornecendo os trechos recuperados ao agente.
// O system prompt instrui o modelo a usar somente esse contexto — padrão
// "grounded generation" que reduz alucinações e ancora a resposta na fonte.

var contexto = new StringBuilder();
contexto.AppendLine("Use EXCLUSIVAMENTE os trechos abaixo para responder à pergunta do usuário.");
contexto.AppendLine("Se a resposta não estiver nos trechos, diga que não encontrou na base de conhecimento.");
contexto.AppendLine();
contexto.AppendLine("=== CONTEXTO RECUPERADO ===");
foreach (var (chunk, _) in scores)
{
    contexto.AppendLine($"[{chunk.Origem}]");
    contexto.AppendLine(chunk.Texto);
    contexto.AppendLine();
}
contexto.AppendLine("=== FIM DO CONTEXTO ===");

string promptAumentado = $"{contexto}\nPergunta: {pergunta}";

Console.WriteLine("\n[RAG] Contexto injetado no prompt. Gerando resposta...\n");

// ── PASSO 7: GERAÇÃO COM ANTHROPIC ───────────────────────────────────────────
// Mesmo padrão dos projetos irmãos: AnthropicClient → AsAIAgent → RunStreamingAsync.
// Usa streaming pois o prompt aumentado (contexto + pergunta) pode ser longo.

string apiKey = "SUA_API_KEY";
if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "SUA_API_KEY")
{
    Console.Error.WriteLine("Substitua 'SUA_API_KEY' pela sua chave da API da Anthropic em Program.cs.");
    return;
}

AnthropicClient client = new() { ApiKey = apiKey };

AIAgent agente = client.AsAIAgent(
    instructions: """
        Você é um assistente especializado em filosofia.
        Responda SOMENTE com base nos trechos de contexto fornecidos pelo usuário.
        Cite o documento de origem (ex: documento_0) quando possível.
        Seja objetivo e didático, adequado para estudantes de filosofia.
        """,
    model: "claude-haiku-4-5",
    name: "Assistente de Filosofia RAG");

try
{
    await foreach (var fragmento in agente.RunStreamingAsync(promptAumentado))
        Console.Write(fragmento);

    Console.WriteLine("\n");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Erro na geração: {ex.Message}");
    throw;
}

// ── TIPOS ─────────────────────────────────────────────────────────────────────
// Declarações de tipo vêm após todos os top-level statements (regra do C#).

// Representa um trecho da base de conhecimento com rastreabilidade de origem.
record Chunk(int Id, string Texto, string Origem);
