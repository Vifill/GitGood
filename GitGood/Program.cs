using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol;
using Spectre.Console;
using System.Text;


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

// Display tools in a table
var table = new Table()
    .Border(TableBorder.Rounded)
    .Title("[yellow]Available Tools[/]")
    .AddColumn("[green]Source[/]")
    .AddColumn("[cyan]Tool Name[/]")
    .AddColumn("[grey]Description[/]");

foreach (var tool in toolsGit.Tools) table.AddRow("Git", $"[green]{tool.Name}[/]", tool.Description);
foreach (var tool in toolsGitHub.Tools) table.AddRow("GitHub", $"[blue]{tool.Name}[/]", tool.Description);

AnsiConsole.Write(table);

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

AnsiConsole.Write(new Rule("[yellow]GitGood Assistant[/]").RuleStyle("grey").LeftAligned());
AnsiConsole.MarkupLine("[grey]Type 'exit' to quit[/]\n");

var currentDirectory = Directory.GetCurrentDirectory();
ChatHistory chatHistory = new ChatHistory(
    $"You're a git helper. You have access to both local git and GitHub via MCP servers. " +
    $"Use {currentDirectory} as the repo_path when making local git calls.");

while (true)
{
    var input = AnsiConsole.Prompt(
        new TextPrompt<string>("[bold blue]❯ [/]")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Please enter a question[/]")
    );
    
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    chatHistory.AddUserMessage(input);
    AnsiConsole.MarkupLine($"[grey]User:[/] {input}");

    var assistantResponse = new StringBuilder();
    await AnsiConsole.Status()
        .StartAsync("Thinking...", async ctx =>
        {
            await foreach (var message in chatCompletionService.GetStreamingChatMessageContentsAsync(
                               chatHistory, executionSettings, kernel))
            {
                assistantResponse.Append(message);
                AnsiConsole.Markup($"[green]{message}[/]");
            }
        });
    
    AnsiConsole.WriteLine();
    chatHistory.AddAssistantMessage(assistantResponse.ToString());
}
