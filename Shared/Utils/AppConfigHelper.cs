using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Shared.Utils
{
    public class AppConfigHelper
    {
        private static readonly Lazy<AppConfig> _config = new Lazy<AppConfig>(LoadConfig);

        public static AppConfig Config => _config.Value;

        private static AppConfig LoadConfig()
        {
            try
            {
                // Try multiple possible locations for the config file
                var possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "app_config.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Config", "app_config.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Shared", "Config", "app_config.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Shared", "Config", "app_config.json"),
                    "app_config.json"
                };

                string? configPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        configPath = path;
                        break;
                    }
                }

                if (configPath == null)
                {
                    Console.WriteLine("Config file not found in any expected location. Using default configuration.");
                    return CreateDefaultConfig();
                }

                Console.WriteLine($"Loading configuration from: {configPath}");

                var json = File.ReadAllText(configPath, System.Text.Encoding.UTF8);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);

                if (config == null)
                {
                    Console.WriteLine("Failed to deserialize configuration file");
                    return CreateDefaultConfig();
                }

                // Ensure directories exist
                EnsureDirectoriesExist(config);

                Console.WriteLine($"Configuration loaded successfully. Data directory: {config.DataDirectory}");
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                return CreateDefaultConfig();
            }
        }

        private static AppConfig CreateDefaultConfig()
        {
            // Use the absolute path to the shared data directory
            var sharedDataPath = @"D:\projects\New folder (8)\SharedData";
            var defaultConfig = new AppConfig
            {
                DataDirectory = sharedDataPath,
                ReportsDirectory = Path.Combine(sharedDataPath, "Reports"),
                ImagesDirectory = Path.Combine(sharedDataPath, "Images"),
                LogsDirectory = Path.Combine(sharedDataPath, "Logs"),
                SyncEnabled = true,
                SyncIntervalSeconds = 30
            };

            EnsureDirectoriesExist(defaultConfig);
            Console.WriteLine($"Using default configuration. Data directory: {defaultConfig.DataDirectory}");
            return defaultConfig;
        }

        private static void EnsureDirectoriesExist(AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(config.DataDirectory);
                Directory.CreateDirectory(config.ReportsDirectory);
                Directory.CreateDirectory(config.ImagesDirectory);
                Directory.CreateDirectory(config.LogsDirectory);
                Console.WriteLine("Ensured all data directories exist");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating data directories: {ex.Message}");
            }
        }
    }

    public class AppConfig
    {
        public string DataDirectory { get; set; } = string.Empty;
        public string ReportsDirectory { get; set; } = string.Empty;
        public string ImagesDirectory { get; set; } = string.Empty;
        public string LogsDirectory { get; set; } = string.Empty;
        public bool SyncEnabled { get; set; } = true;
        public int SyncIntervalSeconds { get; set; } = 30;
    }
}
