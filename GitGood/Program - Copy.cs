//using McpDotNet.Extensions.AI;
//using Microsoft.Extensions.AI;
//using Microsoft.Extensions.Configuration;
//using Microsoft.SemanticKernel;
//using Microsoft.SemanticKernel.ChatCompletion;
//using Microsoft.SemanticKernel.Connectors.OpenAI;
//using ModelContextProtocol;
//using OpenAI;
//using OpenAI.Chat;
//using ChatMessage = Microsoft.Extensions.AI.ChatMessage;


//var config = new ConfigurationBuilder()
//    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
//    .AddUserSecrets<Program>()
//    .AddEnvironmentVariables()
//    .Build();

//if (config["OpenAI:ApiKey"] is null)
//{
//    Console.Error.WriteLine("Please provide a valid OpenAI:ApiKey (or OPENAI__ApiKey env variable)");
//    return;
//}

//// Chat client
//IChatClient client =
//    new OpenAIClient(config["OpenAI:ApiKey"]!).AsChatClient(config["OpenAI:ChatModelId"] ?? "o3-mini");
//IChatClient chatClient = new ChatClientBuilder(client)
//    .UseFunctionInvocation()
//    .Build();

//// Create an MCPClient for the GitHub server
//var mcpClient = await McpDotNetExtensions.GetGitHubToolsAsync().ConfigureAwait(false);
//var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
//var mappedTools = tools.Tools.Select(t => t.ToAITool(mcpClient)).ToList();

//ChatCompletionOptions options = new()
//{
//    ReasoningEffortLevel = config["OpenAI:ReasoningEffort"] ?? "Medium"
//};

//ChatOptions chatOptions = new()
//{
//    Tools = mappedTools
//};

//IList<ChatMessage> messages =
//    [
//        new(ChatRole.System, "You are a helpful assistant, helping us test MCP server functionality."),
//    ];
//// If MCP server provides instructions, add them as an additional system message (you could also add it as a content part)
//if (!string.IsNullOrEmpty(mcpClient.ServerInstructions))
//{
//    messages.Add(new(ChatRole.System, mcpClient.ServerInstructions));
//}

//ChatHistory chatHistory = new ChatHistory();

//Console.WriteLine("Hi what can I do for you today?");

//while (true)
//{
//    chatHistory.AddUserMessage(Console.ReadLine() ?? " ");

//    string answer = "";
//    await foreach (var message in chatClient.GetStreamingResponseAsync(messages, chatOptions))
//    {
//        Console.Write(message);
//        answer += message;
//    }
    
//    chatHistory.AddAssistantMessage(answer);
//}



////await foreach(var message in kernel.InvokePromptStreamingAsync(, new(executionSettings)))
////{
////    Console.Write(message);
////}

//Console.Read();

////Console.WriteLine($"\n\n{prompt}\n{result}");