using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using McpDotNet.Client;
using Spectre.Console;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GitGood
{
    public class CommitCommandHandler
    {
        public async Task HandleAsync(string org, IMcpClient gitClient, IMcpClient gitHubClient, IChatCompletionService chatCompletionService, Kernel kernel)
        {
            AnsiConsole.MarkupLine($"[yellow]Fetching assigned issues for organization '{org}'...[/]");
            var issuesResponse = await gitHubClient.CallToolAsync("search_issues", new Dictionary<string, object>
            {
                { "q", $"org:{org} is:issue is:open assignee:@me" }
            });

            // Get the first content element that matches the filter
            var issuesText = "";
            foreach (var content in issuesResponse.Content)
            {
                if (content.Type == "text")
                {
                    issuesText = content.Text ?? "";
                    break;
                }
            }

            List<Issue> issues = [];
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

            string IssueConverter(Issue issue)
            {
                return Markup.Escape($"#{issue.Number}: {issue.Title}");
            }

            var selectedIssue = AnsiConsole.Prompt(
                new SelectionPrompt<Issue>()
                    .Title("Select an issue to connect this commit to:")
                    .PageSize(10)
                    .AddChoices(issues)
                    .UseConverter(IssueConverter)
            );

            AnsiConsole.MarkupLine("[yellow]Fetching staged changes...[/]");
            var changesResponse = await gitClient.CallToolAsync("git_diff_staged", new Dictionary<string, object>
            {
                { "repo_path", Directory.GetCurrentDirectory() }
            });

            // Get the first content element that is text
            var changes = "";
            foreach (var content in changesResponse.Content)
            {
                if (content.Type == "text")
                {
                    changes = content.Text ?? "";
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(changes))
            {
                AnsiConsole.MarkupLine("[red]No staged changes found.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[yellow]Summarizing changes...[/]");
            string promptTextForSummary = $"Generate a brief, imperative commit message summarizing the diff:\n{changes}";
            string summary = "";

            // Use null for the execution settings to avoid type conversion issues
            await foreach (var message in chatCompletionService.GetStreamingChatMessageContentsAsync(
                new ChatHistory(promptTextForSummary),
                null,
                kernel))
            {
                summary += message;
            }

            string commitMessage = $"Closing #{selectedIssue.Number}. {summary}";
            AnsiConsole.MarkupLine($"[green]Commit message generated:[/]\n{commitMessage}");
            AnsiConsole.MarkupLine($"Run the following command:\n[blue]git commit -m \"{commitMessage}\"[/]");
        }
    }
}
