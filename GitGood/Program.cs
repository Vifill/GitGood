using System.Text;
using McpDotNet.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Spectre.Console;

namespace GitGood;

class Program
{
    static async Task Main(string[] args)
    {
        // Setup configuration
        var configManager = new ConfigurationManager();
        var appConfig = configManager.LoadConfig();

        // Handle config command
        if (args.Length > 0 && args[0].Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            appConfig.OpenAi.ApiKey = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [green]OpenAI API key[/]:").Secret());
            appConfig.OpenAi.ChatModelId = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your OpenAI Chat Model ID (default 'gpt-4o'):")
                    .DefaultValue("gpt-4o"));
            appConfig.OpenAi.ReasoningEffort = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your OpenAI Reasoning Effort - used for o3 (default 'medium'):")
                    .DefaultValue("medium"));
            appConfig.Github.PAT = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [blue]GitHub PAT[/]:").Secret());

            configManager.SaveConfig(appConfig);
            AnsiConsole.MarkupLine("[green]Configuration updated successfully.[/]");
            return;
        }

        // Prompt for any missing configuration
        bool updated = false;
        if (string.IsNullOrWhiteSpace(appConfig.OpenAi.ApiKey))
        {
            appConfig.OpenAi.ApiKey = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [green]OpenAI API key[/]:").Secret());
            updated = true;
        }
        if (string.IsNullOrWhiteSpace(appConfig.Github.PAT))
        {
            appConfig.Github.PAT = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [blue]GitHub PAT[/]:").Secret());
            updated = true;
        }
        if (string.IsNullOrWhiteSpace(appConfig.OpenAi.ChatModelId))
        {
            appConfig.OpenAi.ChatModelId = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your OpenAI Chat Model ID (default 'gpt-4o'):")
                    .DefaultValue("gpt-4o"));
            updated = true;
        }
        if (string.IsNullOrWhiteSpace(appConfig.OpenAi.ReasoningEffort))
        {
            appConfig.OpenAi.ReasoningEffort = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your OpenAI Reasoning Effort (default 'medium'):")
                    .DefaultValue("medium"));
            updated = true;
        }
        if (updated)
        {
            configManager.SaveConfig(appConfig);
        }

        // Initialize configuration for Kernel
        string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string configDir = Path.Combine(appDataDir, ".gitgood");
        Directory.CreateDirectory(configDir);
        string configPath = Path.Combine(configDir, "config.json");

        var config = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Initialize Kernel
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
            AnsiConsole.MarkupLine("[red]Please provide a valid OpenAI:ApiKey (or OPENAI__ApiKey env variable)[/]");
            return;
        }

        Kernel kernel = builder.Build();

        // Initialize MCP clients
        var mcpClientGit = await McpDotNetExtensions.GetGitToolsAsync().ConfigureAwait(false);
        var mcpClientGitHub = await McpDotNetExtensions.GetGitHubToolsAsync(config["Github:PAT"]!).ConfigureAwait(false);

        // Display available tools
        await DisplayAvailableToolsAsync(mcpClientGit, mcpClientGitHub);

        // Register plugins
        await RegisterPluginsAsync(kernel, mcpClientGit, mcpClientGitHub);

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        // Handle commit command
        if (args.Length > 0 && args[0].Equals("commit", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Error: Organization parameter is missing. Usage: gitgood commit <OrgName>[/]");
                return;
            }

            string org = args[1];
            var commitHandler = new CommitCommandHandler();
            await commitHandler.HandleAsync(org, mcpClientGit, mcpClientGitHub, chatCompletionService, kernel);
            return;
        }

        // Start interactive chat
        await StartInteractiveChatAsync(kernel, chatCompletionService);
    }

    static async Task DisplayAvailableToolsAsync(IMcpClient gitClient, IMcpClient githubClient)
    {
        var toolsGit = await gitClient.ListToolsAsync().ConfigureAwait(false);
        var toolsGitHub = await githubClient.ListToolsAsync().ConfigureAwait(false);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[yellow]Available Tools[/]")
            .AddColumn("[green]Source[/]")
            .AddColumn("[cyan]Tool Name[/]")
            .AddColumn("[grey]Description[/]");

        foreach (var tool in toolsGit.Tools)
        {
            table.AddRow(
                new Text("Git"),
                new Markup($"[green]{tool.Name ?? ""}[/]"),
                new Text(tool.Description ?? "")
            );
        }

        foreach (var tool in toolsGitHub.Tools)
        {
            table.AddRow(
                new Text("GitHub"),
                new Markup($"[blue]{tool.Name ?? ""}[/]"),
                new Text(tool.Description ?? "")
            );
        }

        AnsiConsole.Write(table);
    }

    static async Task RegisterPluginsAsync(Kernel kernel, IMcpClient gitClient, IMcpClient githubClient)
    {
        var gitFunctions = await gitClient.MapToFunctionsAsync().ConfigureAwait(false);
        var githubFunctions = await githubClient.MapToFunctionsAsync().ConfigureAwait(false);

        // Use explicit plugin registration methods
        var gitPlugin = KernelPluginFactory.CreateFromFunctions("Git", gitFunctions);
        var githubPlugin = KernelPluginFactory.CreateFromFunctions("GitHub", githubFunctions);

        kernel.Plugins.Add(gitPlugin);
        kernel.Plugins.Add(githubPlugin);
    }

    static async Task StartInteractiveChatAsync(Kernel kernel, IChatCompletionService chatCompletionService)
    {
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
                // Pass null for the PromptExecutionSettings parameter
                await foreach (var message in chatCompletionService.GetStreamingChatMessageContentsAsync(
                                   chatHistory,
                                   null,
                                   kernel))
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
    }
}