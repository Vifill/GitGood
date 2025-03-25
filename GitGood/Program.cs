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

// Create both MCP clients
var mcpClientGit = await McpDotNetExtensions.GetGitToolsAsync().ConfigureAwait(false);
var mcpClientGitHub = await McpDotNetExtensions.GetGitHubToolsAsync(config["Github:PAT"]).ConfigureAwait(false);

// Retrieve and list tools from both servers
var toolsGit = await mcpClientGit.ListToolsAsync().ConfigureAwait(false);
var toolsGitHub = await mcpClientGitHub.ListToolsAsync().ConfigureAwait(false);

Console.WriteLine("Local Git Tools:");
foreach (var tool in toolsGit.Tools) Console.WriteLine($"{tool.Name}: {tool.Description}");

Console.WriteLine("\nGitHub Tools:");
foreach (var tool in toolsGitHub.Tools) Console.WriteLine($"{tool.Name}: {tool.Description}");

// Add both tool sets as separate plugins
var gitFunctions = await mcpClientGit.MapToFunctionsAsync().ConfigureAwait(false);
var githubFunctions = await mcpClientGitHub.MapToFunctionsAsync().ConfigureAwait(false);
kernel.Plugins.AddFromFunctions("Git", gitFunctions);
kernel.Plugins.AddFromFunctions("GitHub", githubFunctions);

// Enable automatic function calling
var executionSettings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
#pragma warning disable SKEXP0010
    ReasoningEffort = config["OpenAI:ReasoningEffort"] ?? "medium",
#pragma warning restore SKEXP0010
};

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

var currentDirectory = Directory.GetCurrentDirectory();
ChatHistory chatHistory = new ChatHistory(
    $"You're a git helper. You have access to both local git and GitHub via MCP servers. " +
    $"Use {currentDirectory} as the repo_path when making local git calls.");

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
