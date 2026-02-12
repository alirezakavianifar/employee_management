using System;
using System.IO;
using Newtonsoft.Json;

namespace Shared.Utils
{
    /// <summary>
    /// Reads and writes the shared UI language setting (SharedData/Config/language.json)
    /// so both ManagementApp and DisplayApp use the same language.
    /// </summary>
    public static class LanguageConfigHelper
    {
        /// <summary>
        /// Returns the SharedData directory to use for resources and language config.
        /// Prefers AppConfigHelper.Config.DataDirectory; falls back to relative paths from app base.
        /// </summary>
        public static string? GetSharedDataDirectory()
        {
            try
            {
                var config = AppConfigHelper.Config;
                if (!string.IsNullOrEmpty(config.DataDirectory) && Directory.Exists(config.DataDirectory))
                    return config.DataDirectory;
            }
            catch { }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "SharedData")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "SharedData")),
            };
            foreach (var full in candidates)
            {
                if (Directory.Exists(full))
                    return full;
            }
            return null;
        }
        public const string LanguageEn = "en";
        // Persian language support removed
        // public const string LanguageFa = "fa";

        private const string ConfigFolderName = "Config";
        private const string LanguageFileName = "language.json";

        /// <summary>
        /// Gets the path to language.json. Uses config.DataDirectory if available;
        /// otherwise returns null (caller should use fallback resolution).
        /// </summary>
        public static string? GetLanguageConfigPath(string? sharedDataDirectory)
        {
            if (string.IsNullOrEmpty(sharedDataDirectory))
                return null;

            var configDir = Path.Combine(sharedDataDirectory, ConfigFolderName);
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, LanguageFileName);
        }

        /// <summary>
        /// Reads the current language from the config file. Always returns "en".
        /// </summary>
        public static string GetCurrentLanguage(string? sharedDataDirectory)
        {
            return LanguageEn;
        }

        /// <summary>
        /// Writes the language to the config file. Always writes "en".
        /// </summary>
        public static void SetCurrentLanguage(string? sharedDataDirectory, string language)
        {
            var path = GetLanguageConfigPath(sharedDataDirectory);
            if (string.IsNullOrEmpty(path))
                return;

            var obj = new LanguageConfig { Language = LanguageEn };
            var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            File.WriteAllText(path, json, System.Text.Encoding.UTF8);
        }

        private class LanguageConfig
        {
            [JsonProperty("language")]
            public string Language { get; set; } = LanguageEn;
        }
    }
}
