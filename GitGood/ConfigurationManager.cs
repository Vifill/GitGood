using System;
using System.IO;
using System.Text.Json;

namespace GitGood
{
    public class ConfigurationManager
    {
        private readonly string _configPath;

        public ConfigurationManager()
        {
            var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configDir = Path.Combine(appDataDir, ".gitgood");
            Directory.CreateDirectory(configDir);
            _configPath = Path.Combine(configDir, "config.json");
        }

        public AppConfig LoadConfig()
        {
            AppConfig appConfig;
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
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
            return appConfig;
        }

        public void SaveConfig(AppConfig appConfig)
        {
            var json = JsonSerializer.Serialize(appConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }

        public bool IsConfigUpdated(AppConfig appConfig)
        {
            return !string.IsNullOrWhiteSpace(appConfig.OpenAi.ApiKey) &&
                   !string.IsNullOrWhiteSpace(appConfig.Github.PAT) &&
                   !string.IsNullOrWhiteSpace(appConfig.OpenAi.ChatModelId) &&
                   !string.IsNullOrWhiteSpace(appConfig.OpenAi.ReasoningEffort);
        }
    }
}
