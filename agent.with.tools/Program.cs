using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// ─────────────────────────────────────────────────────────────────────────────
// PASSO 1 — API simulada
//
// Método estático que imita uma chamada HTTP real:
//   • Task.Delay(600) simula a latência de rede (~600 ms)
//   • Um dicionário local faz o papel do banco de dados do servidor
//   • Retorna dados formatados sobre o filósofo, ou mensagem de "não encontrado"
// ─────────────────────────────────────────────────────────────────────────────
static async Task<string> ConsultarApiFilosofia(string filosofo)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n[TOOL] Agente invocou: ConsultarApiFilosofia(\"{filosofo}\")");
    Console.ResetColor();

    await Task.Delay(600); // simula latência de rede

    var base_dados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["kant"] = """
            Immanuel Kant (1724–1804) — filósofo alemão, pai do idealismo transcendental.
            Obras principais: Crítica da Razão Pura, Crítica da Razão Prática, Fundamentação da Metafísica dos Costumes.
            Conceitos-chave: imperativo categórico, dever moral, categorias do entendimento, fenômeno vs. nôumeno.
            O imperativo categórico em sua formulação mais conhecida: "Age apenas segundo a máxima pela qual possas
            ao mesmo tempo querer que ela se torne uma lei universal."
            """,

        ["hegel"] = """
            Georg Wilhelm Friedrich Hegel (1770–1831) — filósofo alemão, principal representante do idealismo absoluto.
            Obras principais: Fenomenologia do Espírito, Ciência da Lógica, Filosofia do Direito.
            Conceitos-chave: dialética (tese → antítese → síntese), Aufhebung (superação/conservação), Geist (Espírito),
            alienação, desenvolvimento histórico da consciência.
            """,

        ["platão"] = """
            Platão (428–348 a.C.) — filósofo grego, discípulo de Sócrates e mestre de Aristóteles.
            Obras principais: A República, O Banquete, Fédon, Mênon.
            Conceitos-chave: Teoria das Formas/Ideias, Alegoria da Caverna, amor platônico, alma tripartite,
            reminiscência (anamnese), filósofo-rei.
            """,

        ["descartes"] = """
            René Descartes (1596–1650) — filósofo e matemático francês, pai da filosofia moderna.
            Obras principais: Meditações Metafísicas, Discurso do Método, Princípios da Filosofia.
            Conceitos-chave: dúvida cartesiana (método da dúvida), cogito ergo sum ("Penso, logo existo"),
            dualismo mente-corpo (res cogitans / res extensa), racionalismo.
            """,

        ["nietzsche"] = """
            Friedrich Nietzsche (1844–1900) — filósofo alemão, crítico da moral e da metafísica tradicionais.
            Obras principais: Assim Falou Zaratustra, Além do Bem e do Mal, A Gaia Ciência, Genealogia da Moral.
            Conceitos-chave: "Deus está morto", vontade de poder (Wille zur Macht), Übermensch (super-homem),
            eterno retorno, niilismo, perspectivismo.
            """,

        ["sócrates"] = """
            Sócrates (470–399 a.C.) — filósofo grego, considerado o fundador da filosofia ocidental.
            Não deixou obras escritas; seu pensamento é conhecido pelos diálogos de Platão e Xenofonte.
            Conceitos-chave: maiêutica (arte de dar à luz ideias), ironia socrática, "Conhece-te a ti mesmo",
            virtude como conhecimento, cuidado com a alma (psyche).
            """,

        ["aristóteles"] = """
            Aristóteles (384–322 a.C.) — filósofo grego, discípulo de Platão e tutor de Alexandre, o Grande.
            Obras principais: Ética a Nicômaco, Política, Metafísica, Poética, Órganon.
            Conceitos-chave: lógica formal (silogismo), substância (ousia), potência e ato, causa formal/material/
            eficiente/final, eudaimonia (florescimento humano), virtude como meio-termo (mesotes).
            """
    };

    // Busca tolerante: verifica se a chave está contida no argumento ou vice-versa
    foreach (var (chave, dados) in base_dados)
    {
        if (filosofo.Contains(chave, StringComparison.OrdinalIgnoreCase) ||
            chave.Contains(filosofo, StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[TOOL] API respondeu com dados sobre \"{chave}\" (~600ms simulados).");
            Console.ResetColor();
            return dados.Trim();
        }
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[TOOL] API não encontrou dados para \"{filosofo}\".");
    Console.ResetColor();
    return $"Filósofo \"{filosofo}\" não encontrado na base de dados da API.";
}

// ─────────────────────────────────────────────────────────────────────────────
// PASSO 2 — Registro da ferramenta (tool)
//
// AIFunctionFactory.Create() envolve o método estático em um AIFunction.
// O nome e a descrição são usados pelo modelo para decidir QUANDO invocar a tool.
// ─────────────────────────────────────────────────────────────────────────────
var ferramenta = AIFunctionFactory.Create(
    ConsultarApiFilosofia,
    "ConsultarApiFilosofia",
    "Consulta a API de filosofia e retorna informações detalhadas sobre um filósofo pelo nome. " +
    "Use esta ferramenta sempre que precisar de dados sobre um filósofo.");

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"[TOOL] Ferramenta registrada: {ferramenta.Name}");
Console.ResetColor();

// ─────────────────────────────────────────────────────────────────────────────
// PASSO 3 — Configuração do agente com a tool
// ─────────────────────────────────────────────────────────────────────────────
string apiKey = "SUA_API_KEY";
if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "SUA_API_KEY")
{
    Console.Error.WriteLine("Defina sua chave de API em apiKey antes de executar.");
    return;
}

AnthropicClient client = new() { ApiKey = apiKey };

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

// ─────────────────────────────────────────────────────────────────────────────
// PASSO 4 — Entrada do usuário
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("\nOlá! Sou seu Assistente de Filosofia com suporte a Tools.");
Console.Write("Faça sua pergunta: ");
string pergunta = Console.ReadLine() ?? string.Empty;

if (string.IsNullOrWhiteSpace(pergunta))
{
    Console.Error.WriteLine("Pergunta não pode ser vazia.");
    return;
}

// ─────────────────────────────────────────────────────────────────────────────
// PASSO 5 — Execução com streaming
//
// O agente pode chamar a tool uma ou mais vezes antes de gerar a resposta final.
// Os logs [TOOL] são impressos dentro do próprio método ConsultarApiFilosofia.
// ─────────────────────────────────────────────────────────────────────────────
try
{
    Console.WriteLine();
    await foreach (var fragmento in agente.RunStreamingAsync(pergunta))
        Console.Write(fragmento);

    Console.WriteLine();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Erro em tempo de execução: {ex.Message}");
    throw;
}
