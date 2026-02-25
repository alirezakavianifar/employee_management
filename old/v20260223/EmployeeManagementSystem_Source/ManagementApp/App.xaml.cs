using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Utils;

namespace ManagementApp
{
    public partial class App : System.Windows.Application
    {
        private static readonly ILogger<App> Logger = LoggingService.CreateLogger<App>();
        private static string? _sharedDataDirectory;
        private static FileSystemWatcher? _languageConfigWatcher;

        /// <summary>SharedData directory used for resources and language config. Used by Settings to switch language.</summary>
        internal static string? SharedDataDirectory => _sharedDataDirectory;

        protected override void OnStartup(StartupEventArgs e)
        {
            Views.SplashWindow? splash = null;
            try
            {
                // Setup Georgian culture and LTR support
                SetupGeorgianCulture();
                
                // Setup logging
                LoggingService.ConfigureLogging();
                Logger.LogInformation("Starting Management Application");
                Logger.LogInformation("Georgian culture setup completed");
                
                // Load localized resources and apply language (RTL/LTR)
                LoadLocalizedResources();
                ApplyFlowDirection();
                StartLanguageConfigWatcher();
                Logger.LogInformation("Localized resources loaded");
                
                // Set up global exception handling to catch silent failures
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                DispatcherUnhandledException += OnDispatcherUnhandledException;
                
                base.OnStartup(e);

                // Require password before showing the application
                if (!VerifyStartupPassword())
                {
                    Shutdown(0);
                    return;
                }

                // Show splash screen immediately so users see loading feedback
                splash = new Views.SplashWindow();
                splash.Show();
                Logger.LogInformation("Splash screen shown");

                // Force the splash to render before blocking the UI thread with MainWindow init
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                
                // Create and show MainWindow (heavy initialization)
                try
                {
                    var mainWindow = new Views.MainWindow();
                    MainWindow = mainWindow; // Set as the main window
                    mainWindow.Show();
                    mainWindow.Activate(); // Bring window to front
                    mainWindow.Focus(); // Focus the window
                    ApplyFlowDirection(); // Apply RTL/LTR now that MainWindow is set
                    Logger.LogInformation("MainWindow created and shown successfully");
                }
                catch (Exception windowEx)
                {
                    Logger?.LogError(windowEx, "Failed to create or show MainWindow");
                    var errorMessage = $"Error creating main window:\n\n{windowEx.Message}\n\nDetails:\n{windowEx}";
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }
                finally
                {
                    // Always close splash when done (success or error)
                    splash?.Close();
                }
                
                Logger.LogInformation("Management Application started successfully");
            }
            catch (Exception ex)
            {
                splash?.Close();
                Logger?.LogError(ex, "Failed to start Management Application");
                var errorMessage = $"Error starting application:\n\n{ex.Message}\n\nDetails:\n{ex}";
                MessageBox.Show(errorMessage, "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void SetupGeorgianCulture()
        {
            try
            {
                // Set Georgian culture for the application
                var georgianCulture = new CultureInfo("en-US");
                CultureInfo.DefaultThreadCurrentCulture = georgianCulture;
                CultureInfo.DefaultThreadCurrentUICulture = georgianCulture;
                Thread.CurrentThread.CurrentCulture = georgianCulture;
                Thread.CurrentThread.CurrentUICulture = georgianCulture;
                
                // Logging will be configured later, so don't log here
            }
            catch (Exception ex)
            {
                // Logging not configured yet, so just continue
                // Error will be logged after ConfigureLogging is called
            }
        }

        /// <summary>
        /// Shows the password dialog at startup. Returns true if the correct password
        /// was entered, false if the user cancelled (in which case the app should exit).
        /// Re-prompts on wrong password until cancelled or correct.
        /// </summary>
        private bool VerifyStartupPassword()
        {
            var expectedPassword = Shared.Utils.AppConfigHelper.Config.AdminPassword;

            // If no password is configured, skip the prompt
            if (string.IsNullOrEmpty(expectedPassword))
                return true;

            // Prevent WPF from shutting down when the dialog window closes
            // (default ShutdownMode=OnLastWindowClose would trigger shutdown after first dialog)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var dialog = new Views.PasswordDialog(expectedPassword);
            var result = dialog.ShowDialog();

            // Restore normal shutdown behaviour now that the main window will open
            ShutdownMode = ShutdownMode.OnLastWindowClose;

            if (result == true)
            {
                Logger.LogInformation("Startup password verified successfully");
                return true;
            }

            // User cancelled
            return false;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Logger.LogInformation("Shutting down Management Application");
                base.OnExit(e);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during application shutdown");
            }
        }

        private void LoadLocalizedResources()
        {
            try
            {
                _sharedDataDirectory = LanguageConfigHelper.GetSharedDataDirectory();
                if (string.IsNullOrEmpty(_sharedDataDirectory))
                {
                    Logger.LogWarning("SharedData directory not found");
                    return;
                }
                var language = LanguageConfigHelper.GetCurrentLanguage(_sharedDataDirectory);
                ResourceManager.LoadResourcesForLanguageWithOverrides(_sharedDataDirectory, language);
                ResourceBridge.Instance.CurrentLanguage = language;
                Logger.LogInformation($"Loaded resources for language: {language} from: {_sharedDataDirectory}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load localized resources");
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
                Logger.LogWarning(ex, "Could not start language config watcher");
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
                    ResourceBridge.Instance.NotifyLanguageChanged();
                    ApplyFlowDirection();
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error applying language change from file");
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                Logger?.LogError(exception, "Unhandled exception occurred");
                
                var errorMessage = $"Unexpected error:\n\n{exception?.Message}\n\nDetails:\n{exception}";
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                Logger?.LogError(e.Exception, "Dispatcher unhandled exception occurred");
                
                var errorMessage = $"UI error:\n\n{e.Exception.Message}\n\nDetails:\n{e.Exception}";
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Mark as handled to prevent application crash, but log it
                e.Handled = true;
            }
            catch
            {
                // If logging fails, just show a basic error message
                MessageBox.Show("A UI error occurred", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            }
        }
    }
}
