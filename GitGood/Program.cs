using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol;


var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

// Prepare and build kernel
var builder = Kernel.CreateBuilder();
builder.Services.AddLogging(c =>
{
    c.AddDebug();
    c.AddConsole();
    c.SetMinimumLevel(LogLevel.Trace);
});

if (config["OpenAI:ApiKey"] is not null)
{
    builder.Services.AddOpenAIChatCompletion(
        serviceId: "openai",
        modelId: config["OpenAI:ChatModelId"] ?? "gpt-4o",
        apiKey: config["OpenAI:ApiKey"]!);
}
else
{
    Console.Error.WriteLine("Please provide a valid OpenAI:ApiKey (or OPENAI__ApiKey env variable)");
    return;
}

Kernel kernel = builder.Build();

// Create an MCPClient for the GitHub server
var mcpClient = await McpDotNetExtensions.GetGitHubToolsAsync(config["Github:PAT"]).ConfigureAwait(false);

// Retrieve the list of tools available on the GitHub server
var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// Add the MCP tools as Kernel functions
var functions = await mcpClient.MapToFunctionsAsync().ConfigureAwait(false);
kernel.Plugins.AddFromFunctions("GitHub", functions);

// Enable automatic function calling
var executionSettings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    ReasoningEffort = config["OpenAI:ReasoningEffort"] ?? "medium",
};

// Test using GitHub tools
var prompt = "Can you see what issues I have assigned?";

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

ChatHistory chatHistory = new ChatHistory();

Console.WriteLine("Hi what can I do for you today?");

while (true)
{
    chatHistory.AddUserMessage(Console.ReadLine() ?? " ");

    string answer = "";
    await foreach (var message in chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel))
    {
        Console.Write(message);
        answer += message;
    }
    Console.WriteLine();

    chatHistory.AddAssistantMessage(answer);
}