using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Utils;

namespace DisplayApp
{
    public partial class App : Application
    {
        private ILogger<App> _logger;
        private static string? _sharedDataDirectory;
        private static FileSystemWatcher? _languageConfigWatcher;
        private static FileSystemWatcher? _overridesWatcher;

        internal static string? SharedDataDirectory => _sharedDataDirectory;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Initialize logging
                _logger = LoggingService.CreateLogger<App>();
                _logger.LogInformation("DisplayApp starting up");

                // Load localized resources and apply language (RTL/LTR)
                LoadLocalizedResources();
                ApplyFlowDirection();

                StartLanguageConfigWatcher();
                StartOverridesWatcher();
                _logger.LogInformation("Localized resources loaded");

                // Set up global exception handling
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                DispatcherUnhandledException += OnDispatcherUnhandledException;

                // Initialize the main window
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
                ApplyFlowDirection(); // Apply RTL/LTR now that MainWindow is set

                _logger.LogInformation("DisplayApp started successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during application startup");
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")}: {ex.Message}", 
                    ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }

            base.OnStartup(e);
        }

        private void LoadLocalizedResources()
        {
            try
            {
                _sharedDataDirectory = LanguageConfigHelper.GetSharedDataDirectory();
                if (string.IsNullOrEmpty(_sharedDataDirectory))
                {
                    _logger?.LogWarning("SharedData directory not found");
                    return;
                }
                var language = LanguageConfigHelper.GetCurrentLanguage(_sharedDataDirectory);

                ResourceManager.LoadResourcesForLanguageWithOverrides(_sharedDataDirectory, language);
                ResourceBridge.Instance.CurrentLanguage = language;
                _logger?.LogInformation($"Loaded resources for language: {language} from: {_sharedDataDirectory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load localized resources");
            }
        }

        internal static void ApplyFlowDirection()
        {
            var lang = ResourceBridge.Instance.CurrentLanguage;
            var flow = lang == LanguageConfigHelper.LanguageFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Current.Dispatcher.Invoke(() =>
            {
                if (Current.MainWindow != null)
                    Current.MainWindow.FlowDirection = flow;
            });
        }

        private void StartLanguageConfigWatcher()
        {
            if (string.IsNullOrEmpty(_sharedDataDirectory))
                return;
            var configPath = LanguageConfigHelper.GetLanguageConfigPath(_sharedDataDirectory);
            if (string.IsNullOrEmpty(configPath))
                return;
            var configDir = Path.GetDirectoryName(configPath);
            var configFileName = Path.GetFileName(configPath);
            if (string.IsNullOrEmpty(configDir) || string.IsNullOrEmpty(configFileName))
                return;
            try
            {
                _languageConfigWatcher?.Dispose();
                _languageConfigWatcher = new FileSystemWatcher(configDir, configFileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };
                _languageConfigWatcher.Changed += OnLanguageConfigChanged;
                _languageConfigWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not start language config watcher");
            }

        }

        private void StartOverridesWatcher()
        {
            if (string.IsNullOrEmpty(_sharedDataDirectory))
                return;
            
            try
            {
                _overridesWatcher?.Dispose();
                _overridesWatcher = new FileSystemWatcher(_sharedDataDirectory, "custom_overrides.xml")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
                };
                _overridesWatcher.Changed += OnOverridesChanged;
                _overridesWatcher.Created += OnOverridesChanged;
                _overridesWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not start overrides watcher");
            }
        }

        private void OnOverridesChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Add a small delay to ensure file write is complete
                System.Threading.Thread.Sleep(100);
                
                Current.Dispatcher.Invoke(() =>
                {
                    LoadLocalizedResources();
                    // Force UI update by toggling language or just verifying resources are reloaded
                    // ResourceBridge properties validation should trigger updates if values changed
                    ResourceBridge.Instance.NotifyLanguageChanged(); 
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error reloading overrides");
            }
        }

        private void OnLanguageConfigChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_sharedDataDirectory))
                    return;
                var newLang = LanguageConfigHelper.GetCurrentLanguage(_sharedDataDirectory);
                if (newLang == ResourceBridge.Instance.CurrentLanguage)
                    return;
                ResourceManager.LoadResourcesForLanguage(_sharedDataDirectory, newLang);
                Current.Dispatcher.Invoke(() =>
                {
                    ResourceBridge.Instance.CurrentLanguage = newLang;
                    ApplyFlowDirection();
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error applying language change from file");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _logger?.LogInformation("DisplayApp shutting down");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during application shutdown");
            }

            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                _logger?.LogError(exception, "Unhandled exception occurred");
                
                MessageBox.Show($"Unexpected error: {exception?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // If logging fails, just show a basic error message
                MessageBox.Show("An unexpected error occurred", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                _logger?.LogError(e.Exception, "Dispatcher unhandled exception occurred");
                
                MessageBox.Show($"UI Error: {e.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Mark as handled to prevent application crash
                e.Handled = true;
            }
            catch
            {
                // If logging fails, just show a basic error message
                MessageBox.Show("A UI error has occurred", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            }
        }
    }
}