using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Shared.Utils
{
    public class AppConfigHelper
    {
        private static readonly Lazy<AppConfig> _config = new Lazy<AppConfig>(LoadConfig);
        private static string? _configFilePath;
        private static AppConfig? _currentConfig;
        private static FileSystemWatcher? _configWatcher;

        public static AppConfig Config => _config.Value;
        
        public static event Action<AppConfig>? ConfigurationChanged;

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

                _configFilePath = configPath;
                Console.WriteLine($"Loading configuration from: {configPath}");

                var json = File.ReadAllText(configPath, System.Text.Encoding.UTF8);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);
                
                // Setup config file watcher
                SetupConfigFileWatcher(configPath);

                if (config == null)
                {
                    Console.WriteLine("Failed to deserialize configuration file");
                    return CreateDefaultConfig();
                }

                // Convert relative paths to absolute paths based on executable location
                // The relative paths in config are relative to the executable, not the config file
                var executableDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(executableDir))
                {
                    config.DataDirectory = Path.GetFullPath(Path.Combine(executableDir, config.DataDirectory));
                    config.ReportsDirectory = Path.GetFullPath(Path.Combine(executableDir, config.ReportsDirectory));
                    config.ImagesDirectory = Path.GetFullPath(Path.Combine(executableDir, config.ImagesDirectory));
                    config.LogsDirectory = Path.GetFullPath(Path.Combine(executableDir, config.LogsDirectory));
                }

                // Ensure directories exist
                EnsureDirectoriesExist(config);

                _currentConfig = config;
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
            // Try multiple possible locations for SharedData directory
            var executableDir = AppDomain.CurrentDomain.BaseDirectory;
            var possiblePaths = new[]
            {
                // For deployment: relative to executable (most common case)
                Path.Combine(executableDir, "..", "SharedData"),
                // For portable deployment: in same directory as executable
                Path.Combine(executableDir, "SharedData"),
                // For development: relative to project root
                Path.Combine(executableDir, "..", "..", "..", "SharedData"),
                // Fallback: use Documents folder for user data
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EmployeeManagement", "SharedData"),
                // Last resort: use temp directory
                Path.Combine(Path.GetTempPath(), "EmployeeManagement", "SharedData")
            };

            string? sharedDataPath = null;
            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    sharedDataPath = fullPath;
                    Console.WriteLine($"Found existing SharedData directory: {sharedDataPath}");
                    break;
                }
            }

            // If no existing directory found, use the first option and create it
            if (sharedDataPath == null)
            {
                sharedDataPath = Path.GetFullPath(possiblePaths[0]);
                Console.WriteLine($"No existing SharedData found, will create: {sharedDataPath}");
            }
            
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
            _currentConfig = defaultConfig;
            Console.WriteLine($"Using default configuration. Data directory: {defaultConfig.DataDirectory}");
            return defaultConfig;
        }

        private static void EnsureDirectoriesExist(AppConfig config)
        {
            try
            {
                // Try to create directories with proper error handling
                CreateDirectoryIfNotExists(config.DataDirectory);
                CreateDirectoryIfNotExists(config.ReportsDirectory);
                CreateDirectoryIfNotExists(config.ImagesDirectory);
                CreateDirectoryIfNotExists(config.LogsDirectory);
                Console.WriteLine("Ensured all data directories exist");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating data directories: {ex.Message}");
                // If we can't create directories, try to use a fallback location
                TryFallbackLocation(config);
            }
        }

        private static void CreateDirectoryIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine($"Created directory: {path}");
            }
        }

        private static void TryFallbackLocation(AppConfig config)
        {
            try
            {
                // Try using Documents folder as fallback
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var fallbackPath = Path.Combine(documentsPath, "EmployeeManagement", "SharedData");
                
                config.DataDirectory = fallbackPath;
                config.ReportsDirectory = Path.Combine(fallbackPath, "Reports");
                config.ImagesDirectory = Path.Combine(fallbackPath, "Images");
                config.LogsDirectory = Path.Combine(fallbackPath, "Logs");
                
                CreateDirectoryIfNotExists(config.DataDirectory);
                CreateDirectoryIfNotExists(config.ReportsDirectory);
                CreateDirectoryIfNotExists(config.ImagesDirectory);
                CreateDirectoryIfNotExists(config.LogsDirectory);
                
                Console.WriteLine($"Using fallback location: {fallbackPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fallback location also failed: {ex.Message}");
                throw;
            }
        }

        public static bool SaveConfig(AppConfig config)
        {
            try
            {
                if (string.IsNullOrEmpty(_configFilePath))
                {
                    // Try to find or create a config file in the standard location
                    var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                    Directory.CreateDirectory(configDir);
                    _configFilePath = Path.Combine(configDir, "app_config.json");
                }

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configFilePath, json, System.Text.Encoding.UTF8);
                
                Console.WriteLine($"Configuration saved to: {_configFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
                return false;
            }
        }

        public static bool UpdateDataDirectory(string newDataDirectory, bool copyExistingData = true)
        {
            try
            {
                // Validate the new directory path
                if (!ValidateDataDirectory(newDataDirectory, out string validationError))
                {
                    Console.WriteLine($"Validation error: {validationError}");
                    return false;
                }

                var config = Config;
                var oldDataDirectory = config.DataDirectory;
                
                // Clean up any existing nested structure in the old directory BEFORE moving
                if (!string.IsNullOrEmpty(oldDataDirectory) && Directory.Exists(oldDataDirectory))
                {
                    Console.WriteLine("Cleaning up any existing nested structure...");
                    var cleanupSuccess = DataCleanupHelper.CleanupNestedStructure(oldDataDirectory);
                    if (!cleanupSuccess)
                    {
                        Console.WriteLine("Warning: Failed to clean up nested structure, but continuing with move operation");
                    }
                }
                
                // Determine the final data directory path
                string finalDataDirectory;
                
                // Move existing data if requested and old directory exists
                if (copyExistingData && !string.IsNullOrEmpty(oldDataDirectory) && 
                    Directory.Exists(oldDataDirectory) && oldDataDirectory != newDataDirectory)
                {
                    Console.WriteLine($"Moving data from {oldDataDirectory} to {newDataDirectory}");
                    
                    // Check if the new directory is already a SharedData directory
                    if (Path.GetFileName(newDataDirectory).Equals("SharedData", StringComparison.OrdinalIgnoreCase))
                    {
                        // If the selected directory is already named "SharedData", use it directly
                        finalDataDirectory = newDataDirectory;
                        if (!MoveDataDirectory(oldDataDirectory, Path.GetDirectoryName(newDataDirectory) ?? newDataDirectory))
                        {
                            Console.WriteLine("Failed to move existing data");
                            return false;
                        }
                    }
                    else
                    {
                        // Create a SharedData subfolder in the selected directory
                        finalDataDirectory = Path.Combine(newDataDirectory, "SharedData");
                        // Pass the final directory path to avoid double SharedData creation
                        if (!MoveDataDirectory(oldDataDirectory, finalDataDirectory))
                        {
                            Console.WriteLine("Failed to move existing data");
                            return false;
                        }
                    }
                }
                else
                {
                    // If not copying data, use the new directory directly
                    finalDataDirectory = newDataDirectory;
                }

                // Update configuration paths
                config.DataDirectory = finalDataDirectory;
                config.ReportsDirectory = Path.Combine(finalDataDirectory, "Reports");
                config.ImagesDirectory = Path.Combine(finalDataDirectory, "Images");
                config.LogsDirectory = Path.Combine(finalDataDirectory, "Logs");

                // Ensure new directories exist
                EnsureDirectoriesExist(config);

                // Save the updated configuration
                var success = SaveConfig(config);
                
                if (success)
                {
                    _currentConfig = config;
                    // Notify subscribers about the configuration change
                    ConfigurationChanged?.Invoke(config);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating data directory: {ex.Message}");
                return false;
            }
        }

        private static bool ValidateDataDirectory(string path, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                // Check if path is empty or null
                if (string.IsNullOrWhiteSpace(path))
                {
                    errorMessage = "Folder path cannot be empty";
                    return false;
                }

                // Check if path is too long
                if (path.Length > 260)
                {
                    errorMessage = "Folder path is too long (maximum 260 characters)";
                    return false;
                }

                // Check for invalid characters
                var invalidChars = Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                {
                    errorMessage = "Folder path contains invalid characters";
                    return false;
                }

                // Try to get full path to validate
                var fullPath = Path.GetFullPath(path);
                
                // Check if it's a valid directory path (not a file)
                if (File.Exists(fullPath))
                {
                    errorMessage = "The selected path is a file, not a folder";
                    return false;
                }

                // Check if parent directory exists (if directory doesn't exist yet)
                if (!Directory.Exists(fullPath))
                {
                    var parentDir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    {
                        errorMessage = "Parent folder does not exist";
                        return false;
                    }
                }

                // Check write permissions
                try
                {
                    if (Directory.Exists(fullPath))
                    {
                        // Try to create a test file to check write permissions
                        var testFile = Path.Combine(fullPath, "test_write_permission.tmp");
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                    else
                    {
                        // Try to create the directory to check permissions
                        Directory.CreateDirectory(fullPath);
                        Directory.Delete(fullPath);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    errorMessage = "Write access to this folder is not available";
                    return false;
                }
                catch (Exception)
                {
                    errorMessage = "Error checking permissions";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating path: {ex.Message}";
                return false;
            }
        }

        public static string? GetConfigFilePath()
        {
            return _configFilePath;
        }

        public static void ReloadConfiguration()
        {
            try
            {
                if (!string.IsNullOrEmpty(_configFilePath) && File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath, System.Text.Encoding.UTF8);
                    var config = JsonConvert.DeserializeObject<AppConfig>(json);
                    
                    if (config != null)
                    {
                        EnsureDirectoriesExist(config);
                        _currentConfig = config;
                        ConfigurationChanged?.Invoke(config);
                        Console.WriteLine($"Configuration reloaded. Data directory: {config.DataDirectory}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reloading configuration: {ex.Message}");
            }
        }

        private static void SetupConfigFileWatcher(string configPath)
        {
            try
            {
                // Dispose existing watcher
                _configWatcher?.Dispose();
                
                var configDir = Path.GetDirectoryName(configPath);
                var configFileName = Path.GetFileName(configPath);
                
                if (!string.IsNullOrEmpty(configDir) && !string.IsNullOrEmpty(configFileName))
                {
                    _configWatcher = new FileSystemWatcher(configDir, configFileName)
                    {
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    
                    _configWatcher.Changed += OnConfigFileChanged;
                    Console.WriteLine($"Config file watcher setup for: {configPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up config file watcher: {ex.Message}");
            }
        }

        private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Add a small delay to ensure file is not locked
                System.Threading.Thread.Sleep(100);
                ReloadConfiguration();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling config file change: {ex.Message}");
            }
        }

        private static bool MoveDataDirectory(string sourceDir, string targetDir)
        {
            try
            {
                Console.WriteLine($"Starting data migration from {sourceDir} to {targetDir}");
                
                // Safety check: prevent moving to a subdirectory of the source
                if (targetDir.StartsWith(sourceDir, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Error: Cannot move data to a subdirectory of the source directory");
                    return false;
                }
                
                // Use the target directory directly - it's already the correct final path
                string sharedDataTargetDir = targetDir;
                
                // Create target directory if it doesn't exist
                if (!Directory.Exists(sharedDataTargetDir))
                {
                    Directory.CreateDirectory(sharedDataTargetDir);
                }

                // Get statistics before moving
                var sourceDirInfo = new DirectoryInfo(sourceDir);
                var fileCount = sourceDirInfo.GetFiles("*", SearchOption.AllDirectories).Length;
                var dirCount = sourceDirInfo.GetDirectories("*", SearchOption.AllDirectories).Length;
                
                Console.WriteLine($"Found {fileCount} files and {dirCount} directories to move");
                Console.WriteLine($"Target directory: {sharedDataTargetDir}");

                // Move files in root directory
                foreach (var file in sourceDirInfo.GetFiles())
                {
                    var targetFile = Path.Combine(sharedDataTargetDir, file.Name);
                    file.MoveTo(targetFile);
                    Console.WriteLine($"Moved file: {file.Name}");
                }

                // Move subdirectories, but skip nested structure directories
                foreach (var subDir in sourceDirInfo.GetDirectories())
                {
                    var dirName = Path.GetFileName(subDir.FullName);
                    
                    // Skip nested structure directories that might still exist
                    if (dirName.Equals("SharedData", StringComparison.OrdinalIgnoreCase) || 
                        dirName.Equals("New folder", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Skipping nested structure directory: {dirName}");
                        continue;
                    }
                    
                    // Move actual data directories (Reports, Images, Logs, etc.)
                    var targetSubDir = Path.Combine(sharedDataTargetDir, subDir.Name);
                    MoveDirectoryRecursive(subDir.FullName, targetSubDir);
                    Console.WriteLine($"Moved directory: {subDir.Name}");
                }

                // Remove the empty source directory
                if (Directory.Exists(sourceDir) && !Directory.EnumerateFileSystemEntries(sourceDir).Any())
                {
                    Directory.Delete(sourceDir);
                    Console.WriteLine($"Removed empty source directory: {sourceDir}");
                }

                Console.WriteLine($"Data migration completed successfully. Moved {fileCount} files and {dirCount} directories to {sharedDataTargetDir}.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving data directory: {ex.Message}");
                return false;
            }
        }

        private static void MoveDirectoryRecursive(string sourceDir, string targetDir)
        {
            try
            {
                // Create target directory if it doesn't exist
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var sourceDirInfo = new DirectoryInfo(sourceDir);

                // Move all files in current directory
                foreach (var file in sourceDirInfo.GetFiles())
                {
                    var targetFile = Path.Combine(targetDir, file.Name);
                    file.MoveTo(targetFile);
                }

                // Recursively move subdirectories
                foreach (var subDir in sourceDirInfo.GetDirectories())
                {
                    var targetSubDir = Path.Combine(targetDir, subDir.Name);
                    MoveDirectoryRecursive(subDir.FullName, targetSubDir);
                }

                // Remove the empty source directory
                if (Directory.Exists(sourceDir) && !Directory.EnumerateFileSystemEntries(sourceDir).Any())
                {
                    Directory.Delete(sourceDir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving directory {sourceDir}: {ex.Message}");
                throw;
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
        public string AdminPassword { get; set; } = "admin123";
    }
}
