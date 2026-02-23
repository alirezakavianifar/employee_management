using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Shared.Utils
{
    /// <summary>
    /// Static resource manager for loading and accessing localized UI strings.
    /// Loads strings from an XML file and provides access via string keys.
    /// </summary>
    public static class ResourceManager
    {
        private static Dictionary<string, string> _strings = new Dictionary<string, string>();
        private static bool _isLoaded = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Loads resources from the specified XML file.
        /// </summary>
        /// <param name="filePath">Path to the resources.xml file</param>
        public static void LoadResources(string filePath)
        {
            lock (_lock)
            {
                _strings.Clear();
                
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[ResourceManager] Resources file not found: {filePath}");
                    _isLoaded = false;
                    return;
                }

                try
                {
                    var doc = XDocument.Load(filePath);
                    var root = doc.Root;

                    if (root == null || root.Name != "resources")
                    {
                        Console.WriteLine("[ResourceManager] Invalid resources file format");
                        _isLoaded = false;
                        return;
                    }

                    foreach (var element in root.Elements("string"))
                    {
                        var key = element.Attribute("key")?.Value;
                        var value = element.Value;

                        if (!string.IsNullOrEmpty(key))
                        {
                            _strings[key] = value;
                        }
                    }

                    _isLoaded = true;
                    Console.WriteLine($"[ResourceManager] Loaded {_strings.Count} strings from resources");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ResourceManager] Error loading resources: {ex.Message}");
                    _isLoaded = false;
                }
            }
        }

        /// <summary>
        /// Gets a localized string by key.
        /// Checks custom overrides first, then falls back to base resources.
        /// </summary>
        /// <param name="key">The string key</param>
        /// <param name="fallback">Fallback value if key not found (defaults to empty string)</param>
        /// <returns>The localized string or fallback</returns>
        public static string GetString(string key, string fallback = "")
        {
            if (string.IsNullOrEmpty(key))
                return fallback;

            // Check custom overrides first
            var customValue = CustomOverrideManager.GetOverride(key);
            if (customValue != null)
                return customValue;

            lock (_lock)
            {
                if (_strings.TryGetValue(key, out var value))
                {
                    return value;
                }
            }

            // Return fallback if key not found
            return string.IsNullOrEmpty(fallback) ? $"[{key}]" : fallback;
        }

        /// <summary>
        /// Gets a localized string with format arguments.
        /// </summary>
        /// <param name="key">The string key</param>
        /// <param name="args">Format arguments</param>
        /// <returns>The formatted localized string</returns>
        public static string GetFormattedString(string key, params object[] args)
        {
            var template = GetString(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        /// <summary>
        /// Checks if resources have been loaded.
        /// </summary>
        public static bool IsLoaded => _isLoaded;

        /// <summary>
        /// Gets the count of loaded strings.
        /// </summary>
        public static int StringCount
        {
            get
            {
                lock (_lock)
                {
                    return _strings.Count;
                }
            }
        }

        /// <summary>
        /// Checks if a specific key exists.
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key exists</returns>
        public static bool HasKey(string key)
        {
            lock (_lock)
            {
                return _strings.ContainsKey(key);
            }
        }

        /// <summary>
        /// Reloads resources from the last loaded file path.
        /// </summary>
        public static void Reload(string filePath)
        {
            LoadResources(filePath);
        }

        /// <summary>
        /// Loads resources for the given language from the shared data directory.
        /// "en" loads resources.xml, "fa" loads resources.fa.xml. Other values default to en.
        /// </summary>
        public static void LoadResourcesForLanguage(string sharedDataDirectory, string language)
        {
            var fileName = (language?.Trim().ToLowerInvariant() == "fa") ? "resources.fa.xml" : "resources.xml";
            var path = System.IO.Path.Combine(sharedDataDirectory, fileName);
            LoadResources(path);
        }

        /// <summary>
        /// Loads resources for the given language and also loads custom overrides.
        /// </summary>
        /// <param name="sharedDataDirectory">The shared data directory containing resources</param>
        /// <param name="language">The language code ("en" or "fa")</param>
        public static void LoadResourcesForLanguageWithOverrides(string sharedDataDirectory, string language)
        {
            LoadResourcesForLanguage(sharedDataDirectory, language);
            var overridesPath = System.IO.Path.Combine(sharedDataDirectory, "custom_overrides.xml");
            CustomOverrideManager.LoadOverrides(overridesPath);
        }

        /// <summary>
        /// Gets all loaded resource keys (does not include custom overrides).
        /// </summary>
        /// <returns>Collection of all resource keys</returns>
        public static IEnumerable<string> GetAllKeys()
        {
            lock (_lock)
            {
                return new List<string>(_strings.Keys);
            }
        }

        /// <summary>
        /// Gets all loaded resources as key-value pairs (does not include custom overrides).
        /// Useful for displaying in a customization UI.
        /// </summary>
        /// <returns>Dictionary copy of all base resources</returns>
        public static Dictionary<string, string> GetAllResources()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(_strings);
            }
        }
    }
}
