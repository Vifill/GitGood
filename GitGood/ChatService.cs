using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Spectre.Console;
using System.Text;

namespace GitGood
{
    public class ChatService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;

        public ChatService(Kernel kernel, IChatCompletionService chatCompletionService)
        {
            _kernel = kernel;
            _chatCompletionService = chatCompletionService;
        }

        public async Task StartInteractiveChatAsync()
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
                    await foreach (var message in _chatCompletionService.GetStreamingChatMessageContentsAsync(
                        chatHistory,
                        null,
                        _kernel))
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
}