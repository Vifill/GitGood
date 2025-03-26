namespace ModelContextProtocol;

public class AppConfig
{
    public OpenAIConfig OpenAI { get; set; } = new();
    public GithubConfig Github { get; set; } = new();
}
public class OpenAIConfig
{
    public string ApiKey { get; set; } = "";
    public string ChatModelId { get; set; } = "gpt-4o";
    public string ReasoningEffort { get; set; } = "high";
    // You can add more fields if needed, e.g. ReasoningEffort
}
public class GithubConfig
{
    public string PAT { get; set; } = "";
}