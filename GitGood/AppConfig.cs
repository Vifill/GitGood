namespace GitGood;

public class AppConfig
{
    public OpenAiConfig OpenAi { get; set; } = new();
    public GithubConfig Github { get; set; } = new();
}
public class OpenAiConfig
{
    public string ApiKey { get; set; } = "";
    public string ChatModelId { get; set; } = "gpt-4o";
    public string ReasoningEffort { get; set; } = "high";
}
public class GithubConfig
{
    public string PAT { get; set; } = "";
    public string DefaultOrg { get; set; } = "";
}