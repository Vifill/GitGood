using System.Text.Json.Serialization;

namespace GitGood
{
    public class Issue
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        public override string ToString() => $"#{Number}: {Title}";
    }
}
