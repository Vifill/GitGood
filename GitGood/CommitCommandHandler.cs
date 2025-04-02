using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Spectre.Console.Advanced;

namespace GitGood
{
    public class CommitCommandHandler
    {
        public async Task HandleAsync(string org, IMcpClient gitClient, IMcpClient gitHubClient, IChatCompletionService chatCompletionService, Kernel kernel)
        {
            // Check if we're in a git repository
            string repoRootPath = await FindGitRepositoryRootAsync();
            if (string.IsNullOrEmpty(repoRootPath))
            {
                AnsiConsole.MarkupLine("[red]Error: Not inside a git repository.[/]");
                AnsiConsole.MarkupLine("[yellow]Please navigate to a git repository and try again.[/]");
                return;
            }
            
            // Change to the repository root directory
            string originalDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(repoRootPath);
                AnsiConsole.MarkupLine($"[grey]Working in git repository: {repoRootPath}[/]");
            
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
                    { "repo_path", repoRootPath }
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
                AnsiConsole.MarkupLineInterpolated($"[grey]{changes}[/]");
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

                string gitCommand = $"git commit -m \"{commitMessage}\"";
                AnsiConsole.MarkupLine($"[blue]Command: {gitCommand}[/]");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("What would you like to do with this command?")
                        .PageSize(3)
                        .AddChoices(
                            "Execute it directly",
                            "Copy to clipboard",
                            "Do nothing"
                        )
                );

                switch (choice)
                {
                    case "Execute it directly":
                        AnsiConsole.MarkupLine("[yellow]Executing git commit...[/]");
                        try
                        {
                            var processInfo = new ProcessStartInfo
                            {
                                FileName = "git",
                                Arguments = $"commit -m \"{commitMessage.Replace("\"", "\\\"")}\"",
                                WorkingDirectory = repoRootPath,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };

                            var process = Process.Start(processInfo);
                            if (process == null)
                            {
                                throw new Exception("Failed to start git process");
                            }

                            string output = await process.StandardOutput.ReadToEndAsync();
                            string error = await process.StandardError.ReadToEndAsync();
                            await process.WaitForExitAsync();

                            if (process.ExitCode == 0)
                            {
                                AnsiConsole.MarkupLine("\n[green]Commit successful![/]");
                                if (!string.IsNullOrWhiteSpace(output))
                                {
                                    AnsiConsole.MarkupInterpolated($"\n{output}");
                                }
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"\n[red]Commit failed with exit code {process.ExitCode}[/]");
                                if (!string.IsNullOrWhiteSpace(error))
                                {
                                    AnsiConsole.MarkupInterpolated($"\n{error}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"\n[red]Error executing commit: {ex.Message}[/]");
                        }
                        break;

                    case "Copy to clipboard":
                        try
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                await CopyToClipboardWindowsAsync(gitCommand);
                            }
                            else if (OperatingSystem.IsMacOS())
                            {
                                await CopyToClipboardMacOSAsync(gitCommand);
                            }
                            else if (OperatingSystem.IsLinux())
                            {
                                await CopyToClipboardLinuxAsync(gitCommand);
                            }
                            else
                            {
                                throw new PlatformNotSupportedException("Clipboard operations not supported on this platform.");
                            }

                            AnsiConsole.MarkupLine("\n[green]Command copied to clipboard![/]");
                            AnsiConsole.MarkupLine("[yellow]Paste it into your terminal with Ctrl+V (Windows/Linux) or Cmd+V (macOS)[/]");
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"\n[red]Failed to copy to clipboard: {ex.Message}[/]");
                        }
                        break;

                    case "Do nothing":
                    default:
                        AnsiConsole.MarkupLine("[grey]Command not executed or copied. You can run it manually.[/]");
                        break;
                }
            }
            finally
            {
                // Restore the original directory
                if (originalDirectory != repoRootPath)
                {
                    Directory.SetCurrentDirectory(originalDirectory);
                }
            }
        }

        private async Task CopyToClipboardWindowsAsync(string text)
        {
            var tempScript = Path.GetTempFileName() + ".ps1";
            try
            {
                await File.WriteAllTextAsync(tempScript,
                    $"Set-Clipboard -Value \"{text.Replace("\"", "`\"")}\"");

                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{tempScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        throw new Exception($"PowerShell exited with code {process.ExitCode}: {error}");
                    }
                }
            }
            finally
            {
                if (File.Exists(tempScript))
                {
                    File.Delete(tempScript);
                }
            }
        }

        private async Task CopyToClipboardMacOSAsync(string text)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "pbcopy",
                UseShellExecute = false,
                RedirectStandardInput = true
            };

            var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.StandardInput.WriteAsync(text);
                process.StandardInput.Close();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"pbcopy exited with code {process.ExitCode}");
                }
            }
        }

        private async Task CopyToClipboardLinuxAsync(string text)
        {
            // Try xclip first, then xsel
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "xclip",
                    Arguments = "-selection clipboard",
                    UseShellExecute = false,
                    RedirectStandardInput = true
                };

                var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.StandardInput.WriteAsync(text);
                    process.StandardInput.Close();
                    await process.WaitForExitAsync();
                    return;
                }
            }
            catch
            {
                // Try xsel if xclip fails
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "xsel",
                        Arguments = "--clipboard --input",
                        UseShellExecute = false,
                        RedirectStandardInput = true
                    };

                    var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        await process.StandardInput.WriteAsync(text);
                        process.StandardInput.Close();
                        await process.WaitForExitAsync();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Both xclip and xsel failed. Please install one of these utilities.", ex);
                }
            }
        }

        /// <summary>
        /// Finds the root directory of the git repository by looking for the .git directory
        /// </summary>
        /// <returns>The path to the git repository root, or null if not in a repository</returns>
        private async Task<string> FindGitRepositoryRootAsync()
        {
            try
            {
                // Use git rev-parse to find the repository root
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --show-toplevel",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = Process.Start(processInfo);
                if (process == null)
                {
                    return null;
                }

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    // Trim the output to get the exact path
                    return output.Trim();
                }
            }
            catch
            {
                // If any error occurs, assume we're not in a git repository
            }

            return null;
        }
    }
}
