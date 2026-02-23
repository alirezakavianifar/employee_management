using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace DisplayApp.Utils
{
    public class ConfigHelper
    {
        private readonly ILogger<ConfigHelper> _logger;
        private readonly string _configPath;
        private Dictionary<string, object> _config;

        public ConfigHelper(string configPath)
        {
            _configPath = configPath;
            _logger = LoggingService.CreateLogger<ConfigHelper>();
            _config = new Dictionary<string, object>();
            LoadConfig();
        }

        public void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var jsonString = File.ReadAllText(_configPath);
                    var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) ?? new Dictionary<string, object>();
                    // Convert JsonElement objects to proper Dictionary<string, object> recursively
                    _config = ConvertJsonElementsToDictionaries(deserialized);
                    _logger.LogInformation("Configuration loaded from {ConfigPath}", _configPath);
                }
                else
                {
                    _logger.LogWarning("Configuration file not found: {ConfigPath}. Using default values.", _configPath);
                    SetDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration from {ConfigPath}", _configPath);
                SetDefaultConfig();
            }
        }

        private Dictionary<string, object> ConvertJsonElementsToDictionaries(Dictionary<string, object> dict)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                if (kvp.Value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        // Recursively convert nested objects
                        var nestedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                        result[kvp.Key] = nestedDict != null ? ConvertJsonElementsToDictionaries(nestedDict) : new Dictionary<string, object>();
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        // Handle arrays
                        var list = new List<object>();
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                var itemDict = JsonSerializer.Deserialize<Dictionary<string, object>>(item.GetRawText());
                                list.Add(itemDict != null ? ConvertJsonElementsToDictionaries(itemDict) : new Dictionary<string, object>());
                            }
                            else
                            {
                                // Extract primitive array items based on type
                                var value = item.ValueKind switch
                                {
                                    JsonValueKind.String => item.GetString(),
                                    JsonValueKind.Number => item.TryGetInt64(out var longVal) ? longVal : (object)item.GetDouble(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    JsonValueKind.Null => null,
                                    _ => item.GetRawText()
                                };
                                list.Add(value);
                            }
                        }
                        result[kvp.Key] = list;
                    }
                    else
                    {
                        // Primitive values - extract based on type
                        result[kvp.Key] = jsonElement.ValueKind switch
                        {
                            JsonValueKind.String => jsonElement.GetString(),
                            JsonValueKind.Number => jsonElement.TryGetInt64(out var longVal) ? longVal : (object)jsonElement.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => jsonElement.GetRawText()
                        };
                    }
                }
                else if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    // Already a dictionary, recursively convert
                    result[kvp.Key] = ConvertJsonElementsToDictionaries(nestedDict);
                }
                else
                {
                    // Already a primitive value
                    result[kvp.Key] = kvp.Value;
                }
            }
            return result;
        }

        public void SaveConfig()
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, jsonString);
                _logger.LogInformation("Configuration saved to {ConfigPath}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration to {ConfigPath}", _configPath);
            }
        }

        private void SetDefaultConfig()
        {
            _config = new Dictionary<string, object>
            {
                {
                    "display", new Dictionary<string, object>
                    {
                        { "fullscreen", true },
                        { "refreshInterval", 30 },
                        { "theme", "dark" },
                        { "fontSize", 14 },
                        { "language", "fa" },
                        { "backgroundColor", "#1a1a1a" },
                        // Visibility settings for Display sections
                        { "showPerformanceChart", true },
                        { "showAiRecommendation", true }
                    }
                },
                {
                    "data", new Dictionary<string, object>
                    {
                        { "syncInterval", 30 },
                        { "autoBackup", true },
                        { "backupRetentionDays", 30 },
                        { "dataPath", "Data" }
                    }
                },
                {
                    "charts", new Dictionary<string, object>
                    {
                        { "chartTheme", "default" },
                        { "persianFonts", true },
                        { "chartQuality", "high" },
                        { "autoRefresh", true }
                    }
                },
                {
                    "ui", new Dictionary<string, object>
                    {
                        { "rtlLayout", true },
                        { "persianNumbers", true },
                        { "showTooltips", true },
                        { "animationSpeed", "normal" }
                    }
                }
            };
        }

        public T GetValue<T>(string section, string key, T defaultValue = default(T))
        {
            try
            {
                if (_config.TryGetValue(section, out var sectionObj) && sectionObj is Dictionary<string, object> sectionDict)
                {
                    if (sectionDict.TryGetValue(key, out var valueObj))
                    {
                        if (valueObj is JsonElement jsonElement)
                        {
                            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                        }
                        else if (valueObj is T directValue)
                        {
                            return directValue;
                        }
                        else
                        {
                            return (T)Convert.ChangeType(valueObj, typeof(T));
                        }
                    }
                }
                
                _logger.LogWarning("Configuration key not found: {Section}.{Key}. Using default value: {DefaultValue}", section, key, defaultValue);
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration value: {Section}.{Key}", section, key);
                return defaultValue;
            }
        }

        public void SetValue<T>(string section, string key, T value)
        {
            try
            {
                if (!_config.TryGetValue(section, out var sectionObj) || !(sectionObj is Dictionary<string, object> sectionDict))
                {
                    sectionDict = new Dictionary<string, object>();
                    _config[section] = sectionDict;
                }

                sectionDict[key] = value;
                _logger.LogInformation("Configuration value set: {Section}.{Key} = {Value}", section, key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting configuration value: {Section}.{Key}", section, key);
            }
        }

        // Convenience methods for common configuration values
        public int GetRefreshInterval() => GetValue<int>("display", "refreshInterval", 30);
        public bool GetFullscreen() => GetValue<bool>("display", "fullscreen", true);
        public string GetTheme() => GetValue<string>("display", "theme", "dark");
        public int GetFontSize() => GetValue<int>("display", "fontSize", 14);
        public string GetLanguage() => GetValue<string>("display", "language", "fa");
        public string GetDataPath() => GetValue<string>("data", "dataPath", "Data");
        public int GetSyncInterval() => GetValue<int>("data", "syncInterval", 30);
        public bool GetAutoBackup() => GetValue<bool>("data", "autoBackup", true);
        public int GetBackupRetentionDays() => GetValue<int>("data", "backupRetentionDays", 30);
        public string GetChartTheme() => GetValue<string>("charts", "chartTheme", "default");
        public bool GetPersianFonts() => GetValue<bool>("charts", "persianFonts", true);
        public string GetChartQuality() => GetValue<string>("charts", "chartQuality", "high");
        public bool GetAutoRefresh() => GetValue<bool>("charts", "autoRefresh", true);
        public bool GetRtlLayout() => GetValue<bool>("ui", "rtlLayout", true);
        public bool GetPersianNumbers() => GetValue<bool>("ui", "persianNumbers", true);
        public bool GetShowTooltips() => GetValue<bool>("ui", "showTooltips", true);
        public string GetAnimationSpeed() => GetValue<string>("ui", "animationSpeed", "normal");
        public string GetBackgroundColor() => GetValue<string>("display", "backgroundColor", "#1a1a1a");
        public bool GetShowPerformanceChart() => GetValue<bool>("display", "showPerformanceChart", true);
        public bool GetShowAiRecommendation() => GetValue<bool>("display", "showAiRecommendation", true);

        public void SetRefreshInterval(int value) => SetValue("display", "refreshInterval", value);
        public void SetFullscreen(bool value) => SetValue("display", "fullscreen", value);
        public void SetTheme(string value) => SetValue("display", "theme", value);
        public void SetFontSize(int value) => SetValue("display", "fontSize", value);
        public void SetLanguage(string value) => SetValue("display", "language", value);
        public void SetBackgroundColor(string value) => SetValue("display", "backgroundColor", value);
        public void SetShowPerformanceChart(bool value) => SetValue("display", "showPerformanceChart", value);
        public void SetShowAiRecommendation(bool value) => SetValue("display", "showAiRecommendation", value);
        public void SetDataPath(string value) => SetValue("data", "dataPath", value);
        public void SetSyncInterval(int value) => SetValue("data", "syncInterval", value);
        public void SetAutoBackup(bool value) => SetValue("data", "autoBackup", value);
        public void SetBackupRetentionDays(int value) => SetValue("data", "backupRetentionDays", value);
        public void SetChartTheme(string value) => SetValue("charts", "chartTheme", value);
        public void SetPersianFonts(bool value) => SetValue("charts", "persianFonts", value);
        public void SetChartQuality(string value) => SetValue("charts", "chartQuality", value);
        public void SetAutoRefresh(bool value) => SetValue("charts", "autoRefresh", value);
        public void SetRtlLayout(bool value) => SetValue("ui", "rtlLayout", value);
        public void SetPersianNumbers(bool value) => SetValue("ui", "persianNumbers", value);
        public void SetShowTooltips(bool value) => SetValue("ui", "showTooltips", value);
        public void SetAnimationSpeed(string value) => SetValue("ui", "animationSpeed", value);
    }
}
