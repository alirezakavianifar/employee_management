using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Shared.Utils
{
    /// <summary>
    /// Manages custom text overrides that allow end-users to customize UI terminology.
    /// Overrides are stored separately from base resources and take precedence when looking up strings.
    /// </summary>
    public static class CustomOverrideManager
    {
        private static Dictionary<string, string> _overrides = new Dictionary<string, string>();
        private static readonly object _lock = new object();
        private static string? _lastLoadedPath;

        /// <summary>
        /// Gets a custom override value for the specified key.
        /// </summary>
        /// <param name="key">The resource key</param>
        /// <returns>The override value, or null if no override exists</returns>
        public static string? GetOverride(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            lock (_lock)
            {
                return _overrides.TryGetValue(key, out var value) ? value : null;
            }
        }

        /// <summary>
        /// Sets a custom override for the specified key.
        /// </summary>
        /// <param name="key">The resource key to override</param>
        /// <param name="value">The custom value</param>
        public static void SetOverride(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            lock (_lock)
            {
                _overrides[key] = value;
            }
        }

        /// <summary>
        /// Removes a custom override for the specified key.
        /// </summary>
        /// <param name="key">The resource key to remove override for</param>
        /// <returns>True if the override was removed, false if it didn't exist</returns>
        public static bool RemoveOverride(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            lock (_lock)
            {
                return _overrides.Remove(key);
            }
        }

        /// <summary>
        /// Gets all current overrides.
        /// </summary>
        /// <returns>A copy of the overrides dictionary</returns>
        public static Dictionary<string, string> GetAllOverrides()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(_overrides);
            }
        }

        /// <summary>
        /// Clears all custom overrides.
        /// </summary>
        public static void ClearAllOverrides()
        {
            lock (_lock)
            {
                _overrides.Clear();
            }
        }

        /// <summary>
        /// Gets the count of custom overrides.
        /// </summary>
        public static int OverrideCount
        {
            get
            {
                lock (_lock)
                {
                    return _overrides.Count;
                }
            }
        }

        /// <summary>
        /// Checks if an override exists for the specified key.
        /// </summary>
        /// <param name="key">The resource key to check</param>
        /// <returns>True if an override exists</returns>
        public static bool HasOverride(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            lock (_lock)
            {
                return _overrides.ContainsKey(key);
            }
        }

        /// <summary>
        /// Loads overrides from an XML file.
        /// </summary>
        /// <param name="filePath">Path to the overrides XML file</param>
        public static void LoadOverrides(string filePath)
        {
            lock (_lock)
            {
                _overrides.Clear();
                _lastLoadedPath = filePath;

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[CustomOverrideManager] No overrides file found at: {filePath}");
                    return;
                }

                try
                {
                    var doc = XDocument.Load(filePath);
                    var root = doc.Root;

                    if (root == null || root.Name != "overrides")
                    {
                        Console.WriteLine("[CustomOverrideManager] Invalid overrides file format");
                        return;
                    }

                    foreach (var element in root.Elements("string"))
                    {
                        var key = element.Attribute("key")?.Value;
                        var value = element.Value;

                        if (!string.IsNullOrEmpty(key))
                        {
                            _overrides[key] = value;
                        }
                    }

                    Console.WriteLine($"[CustomOverrideManager] Loaded {_overrides.Count} overrides");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CustomOverrideManager] Error loading overrides: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Saves current overrides to an XML file.
        /// </summary>
        /// <param name="filePath">Path to save the overrides XML file</param>
        public static void SaveOverrides(string filePath)
        {
            lock (_lock)
            {
                try
                {
                    var doc = new XDocument(
                        new XDeclaration("1.0", "utf-8", null),
                        new XElement("overrides",
                            new XComment(" Custom text overrides - Edit values here or use the Management App Settings ")
                        )
                    );

                    foreach (var kvp in _overrides)
                    {
                        doc.Root!.Add(new XElement("string",
                            new XAttribute("key", kvp.Key),
                            kvp.Value
                        ));
                    }

                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    doc.Save(filePath);
                    _lastLoadedPath = filePath;
                    Console.WriteLine($"[CustomOverrideManager] Saved {_overrides.Count} overrides to {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CustomOverrideManager] Error saving overrides: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Saves overrides to the last loaded path.
        /// </summary>
        public static void SaveOverrides()
        {
            if (string.IsNullOrEmpty(_lastLoadedPath))
            {
                throw new InvalidOperationException("No overrides file path set. Call LoadOverrides first or use SaveOverrides(path).");
            }
            SaveOverrides(_lastLoadedPath);
        }

        /// <summary>
        /// Gets the path where overrides were last loaded from or saved to.
        /// </summary>
        public static string? LastLoadedPath => _lastLoadedPath;

        /// <summary>
        /// Applies a preset that changes "Supervisor" terminology to "Foreman".
        /// </summary>
        public static void ApplySupervisorToForemanPreset()
        {
            lock (_lock)
            {
                _overrides["display_supervisor"] = "Foreman: {0}";
                _overrides["display_no_supervisor"] = "No Foreman";
                _overrides["label_shift_supervisors"] = "Shift Foremen (required):";
                _overrides["label_morning_supervisor"] = "Morning Shift Foreman (required):";
                _overrides["label_afternoon_supervisor"] = "Afternoon Shift Foreman (required):";
                _overrides["label_night_supervisor"] = "Night Shift Foreman (required):";
                _overrides["hint_drag_drop_supervisor"] = "Drag and drop to assign foreman";
                _overrides["supervisor_not_assigned"] = "Foreman: Not assigned";
            }
        }
    }
}
