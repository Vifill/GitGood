using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpDotNet.Client;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitGood
{
    public class KernelService
    {
        private readonly string _configPath;
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;
        private IMcpClient _mcpClientGit;
        private IMcpClient _mcpClientGitHub;

        public KernelService(string configPath, string githubPat)
        {
            _configPath = configPath;

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
                throw new InvalidOperationException("Please provide a valid OpenAI:ApiKey (or OPENAI__ApiKey env variable)");
            }

            _kernel = builder.Build();
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            // Initialize MCP clients (async constructor pattern with Task.Run)
            var initTask = Task.Run(async () =>
            {
                _mcpClientGit = await McpDotNetExtensions.GetGitToolsAsync().ConfigureAwait(false);
                _mcpClientGitHub = await McpDotNetExtensions.GetGitHubToolsAsync(githubPat).ConfigureAwait(false);
            });
            initTask.Wait(); // Wait for async initialization to complete
        }

        public async Task DisplayAvailableToolsAsync()
        {
            var toolsGit = await _mcpClientGit.ListToolsAsync().ConfigureAwait(false);
            var toolsGitHub = await _mcpClientGitHub.ListToolsAsync().ConfigureAwait(false);

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

        public async Task RegisterPluginsAsync()
        {
            var gitFunctions = await _mcpClientGit.MapToFunctionsAsync().ConfigureAwait(false);
            var githubFunctions = await _mcpClientGitHub.MapToFunctionsAsync().ConfigureAwait(false);

            // Use explicit plugin registration methods
            var gitPlugin = KernelPluginFactory.CreateFromFunctions("Git", gitFunctions);
            var githubPlugin = KernelPluginFactory.CreateFromFunctions("GitHub", githubFunctions);

            _kernel.Plugins.Add(gitPlugin);
            _kernel.Plugins.Add(githubPlugin);
        }
    }
}