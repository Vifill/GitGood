﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol;
using Spectre.Console;
using System.Text;
using System.Text.Json;
using GitGood;

string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
string configDir = Path.Combine(home, ".gitgood");
Directory.CreateDirectory(configDir);
string configPath = Path.Combine(configDir, "config.json");

AppConfig appConfig;
if (File.Exists(configPath))
{
    try
    {
        string json = File.ReadAllText(configPath);
        appConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }
    catch
    {
        appConfig = new AppConfig();
    }
}
else
{
    appConfig = new AppConfig();
}

if (args.Length > 0 && args[0].Equals("config", StringComparison.OrdinalIgnoreCase))
{
    appConfig.OpenAI.ApiKey = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your [green]OpenAI API key[/]:").Secret());
    appConfig.OpenAI.ChatModelId = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your OpenAI Chat Model ID (default 'gpt-4o'):")
            .DefaultValue("gpt-4o"));
    appConfig.OpenAI.ReasoningEffort = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your OpenAI Reasoning Effort (default 'medium'):")
            .DefaultValue("medium"));
    appConfig.Github.PAT = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your [blue]GitHub PAT[/]:").Secret());

    string newJson = JsonSerializer.Serialize(appConfig, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(configPath, newJson);
    AnsiConsole.MarkupLine("[green]Configuration updated successfully.[/]");
    return;
}

bool updated = false;
if (string.IsNullOrWhiteSpace(appConfig.OpenAI.ApiKey))
{
    appConfig.OpenAI.ApiKey = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your [green]OpenAI API key[/]:").Secret());
    updated = true;
}
if (string.IsNullOrWhiteSpace(appConfig.Github.PAT))
{
    appConfig.Github.PAT = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your [blue]GitHub PAT[/]:").Secret());
    updated = true;
}
if (string.IsNullOrWhiteSpace(appConfig.OpenAI.ChatModelId))
{
    appConfig.OpenAI.ChatModelId = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your OpenAI Chat Model ID (default 'gpt-4o'):")
            .DefaultValue("gpt-4o"));
    updated = true;
}
if (string.IsNullOrWhiteSpace(appConfig.OpenAI.ReasoningEffort))
{
    appConfig.OpenAI.ReasoningEffort = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your OpenAI Reasoning Effort (default 'medium'):")
            .DefaultValue("medium"));
    updated = true;
}
if (updated)
{
    string newJson = JsonSerializer.Serialize(appConfig, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(configPath, newJson);
}

var config = new ConfigurationBuilder()
    .AddJsonFile(configPath, optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var builder = Kernel.CreateBuilder();
builder.Services.AddLogging(c =>
{
    c.AddDebug();
    c.AddConsole();
    c.SetMinimumLevel(LogLevel.Warning);
});

if (!string.IsNullOrWhiteSpace(config["OpenAI:ApiKey"]))
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

var mcpClientGit = await McpDotNetExtensions.GetGitToolsAsync().ConfigureAwait(false);
var mcpClientGitHub = await McpDotNetExtensions.GetGitHubToolsAsync(config["Github:PAT"]!).ConfigureAwait(false);

var toolsGit = await mcpClientGit.ListToolsAsync().ConfigureAwait(false);
var toolsGitHub = await mcpClientGitHub.ListToolsAsync().ConfigureAwait(false);

var table = new Table()
    .Border(TableBorder.Rounded)
    .Title("[yellow]Available Tools[/]")
    .AddColumn("[green]Source[/]")
    .AddColumn("[cyan]Tool Name[/]")
    .AddColumn("[grey]Description[/]");

foreach (var tool in toolsGit.Tools)
    table.AddRow("Git", $"[green]{tool.Name ?? ""}[/]", tool.Description ?? "");
foreach (var tool in toolsGitHub.Tools)
    table.AddRow("GitHub", $"[blue]{tool.Name ?? ""}[/]", tool.Description ?? "");

AnsiConsole.Write(table);

var gitFunctions = await mcpClientGit.MapToFunctionsAsync().ConfigureAwait(false);
var githubFunctions = await mcpClientGitHub.MapToFunctionsAsync().ConfigureAwait(false);
kernel.Plugins.AddFromFunctions("Git", gitFunctions);
kernel.Plugins.AddFromFunctions("GitHub", githubFunctions);

var executionSettings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
};

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

if (args.Length > 0 && args[0].Equals("commit", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        AnsiConsole.MarkupLine("[red]Error: Organization parameter is missing. Usage: gitgood commit <OrgName>[/]");
        return;
    }
    string org = args[1];
    AnsiConsole.MarkupLine($"[yellow]Fetching assigned issues for organization '{org}'...[/]");
    var issuesResponse = await mcpClientGitHub.CallToolAsync("search_issues", new Dictionary<string, object>
    {
        { "q", $"org:{org} is:issue is:open assignee:@me" }
    });
    var issuesText = issuesResponse.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
    List<Issue> issues = new List<Issue>();
    try
    {
        if (string.IsNullOrWhiteSpace(issuesText))
        {
            AnsiConsole.MarkupLine("[red]No issues were returned from the API.[/]");
            return;
        }
        string trimmed = issuesText.TrimStart();
        if (trimmed.StartsWith("{"))
        {
            using JsonDocument doc = JsonDocument.Parse(issuesText);
            if (doc.RootElement.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
            {
                issues = items.Deserialize<List<Issue>>() ?? new List<Issue>();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Unexpected JSON format for issues.[/]");
                return;
            }
        }
        else if (trimmed.StartsWith("["))
        {
            issues = JsonSerializer.Deserialize<List<Issue>>(issuesText) ?? new List<Issue>();
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Unexpected JSON format for issues.[/]");
            return;
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error parsing issues: {Markup.Escape(ex.Message)}[/]");
        return;
    }
    if (issues.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]No open issues found.[/]");
        return;
    }
    var selectedIssue = AnsiConsole.Prompt(
        new SelectionPrompt<Issue>()
            .Title("Select an issue to connect this commit to:")
            .PageSize(10)
            .AddChoices(issues)
            .UseConverter(issue => Markup.Escape($"#{issue.Number}: {issue.Title}")));
    
    AnsiConsole.MarkupLine("[yellow]Fetching staged changes...[/]");
    var changesResponse = await mcpClientGit.CallToolAsync("get_staged_changes", new Dictionary<string, object>() 
    {
        { "repo_path", Directory.GetCurrentDirectory() }
    });
    var changes = changesResponse.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
    if (string.IsNullOrWhiteSpace(changes))
    {
        AnsiConsole.MarkupLine("[red]No staged changes found.[/]");
        return;
    }
    
    AnsiConsole.MarkupLine("[yellow]Summarizing changes...[/]");
    string promptTextForSummary = $"Generate a brief, imperative commit message summarizing the diff:\n{changes}";
    string summary = "";
    await foreach (var message in chatCompletionService.GetStreamingChatMessageContentsAsync(new ChatHistory(promptTextForSummary), executionSettings, kernel))
    {
        summary += message;
    }
    
    string commitMessage = $"Closing #{selectedIssue.Number}. {summary}";
    AnsiConsole.MarkupLine($"[green]Commit message generated:[/]\n{commitMessage}");
    AnsiConsole.MarkupLine($"Run the following command:\n[blue]git commit -m \"{commitMessage}\"[/]");
    return;
}

AnsiConsole.Write(new Rule("[yellow]GitGood Assistant[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine("[grey]Type 'exit' to quit[/]\n");

string currentDirectory = Directory.GetCurrentDirectory();
ChatHistory chatHistory = new ChatHistory($"You're a git helper. You have access to both local git and GitHub via MCP servers. Use {currentDirectory} as the repo_path when making local git calls.");

while (true)
{
    var input = AnsiConsole.Prompt(new TextPrompt<string>("[bold blue]❯ [/]").ValidationErrorMessage("[red]Please enter a question[/]"));
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;
    chatHistory.AddUserMessage(input);
    AnsiConsole.MarkupLine($"[grey]User:[/] {input}");
    var assistantResponse = new StringBuilder();
    var initialPanel = new Panel(new Markup("[green]Starting...[/]"))
    {
        Border = BoxBorder.Rounded,
        Padding = new Padding(1, 1)
    };
    await AnsiConsole.Live(initialPanel).StartAsync(async ctx =>
    {
        await foreach (var message in chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel))
        {
            assistantResponse.Append(message);
            var panel = new Panel(new Markup($"[green]{Markup.Escape(assistantResponse.ToString())}[/]"))
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 1)
            };
            ctx.UpdateTarget(panel);
            ctx.Refresh();
        }
    });
    AnsiConsole.WriteLine();
    chatHistory.AddAssistantMessage(assistantResponse.ToString());
}
