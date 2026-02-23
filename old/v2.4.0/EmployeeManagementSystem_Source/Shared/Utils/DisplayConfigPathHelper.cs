using System;
using System.IO;

namespace Shared.Utils
{
    /// <summary>
    /// Provides a single canonical location for the display configuration file
    /// so that both ManagementApp and DisplayApp read/write the same JSON.
    /// </summary>
    public static class DisplayConfigPathHelper
    {
        private const string ConfigFolderName = "Config";
        private const string DisplayConfigFileName = "display_config.json";

        /// <summary>
        /// Returns the canonical path to display_config.json based on AppConfigHelper.Config.DataDirectory.
        /// Ensures the containing Config folder exists.
        /// </summary>
        public static string GetDisplayConfigPath()
        {
            var config = AppConfigHelper.Config;

            // Place the display config alongside other shared data under a Config subfolder.
            var configDir = Path.Combine(config.DataDirectory, ConfigFolderName);

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            return Path.Combine(configDir, DisplayConfigFileName);
        }

        /// <summary>
        /// Returns legacy locations where display_config.json might exist from
        /// older versions of the app. Callers can optionally migrate from these
        /// paths into the canonical shared path.
        /// </summary>
        public static string[] GetLegacyCandidatePaths()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            return new[]
            {
                Path.Combine(baseDir, "Config", DisplayConfigFileName),
                Path.Combine(baseDir, "..", "..", "DisplayApp", "Config", DisplayConfigFileName),
                Path.Combine(baseDir, "..", "DisplayApp", "Config", DisplayConfigFileName),
            };
        }
    }
}

