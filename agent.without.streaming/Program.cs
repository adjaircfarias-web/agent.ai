using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

string apiKey = "SUA_API_KEY";
if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("Environment variable OPENAI_API_KEY is not set. Set it and restart the application.");
    return;
}

AnthropicClient client = new() { ApiKey = apiKey };

AIAgent agent = client.AsAIAgent(instructions: "Você ajuda pessoas a entenderem de filosofia.",
                                model: "claude-haiku-4-5", name: "Assitente de filosofia");

try
{
    Console.WriteLine("Olá, sou seu Agente de filosofia!");
    Console.Write("Faça sua pergunta: ");
    var question = Console.ReadLine();
    var response = await agent.RunAsync(question);
    Console.WriteLine(response);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Runtime error: {ex.Message}");
    throw;
}
