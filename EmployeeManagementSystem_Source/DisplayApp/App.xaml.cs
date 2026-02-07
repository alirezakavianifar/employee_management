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

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Initialize logging
                _logger = LoggingService.CreateLogger<App>();
                _logger.LogInformation("DisplayApp starting up");

                // Load localized resources
                LoadLocalizedResources();
                _logger.LogInformation("Localized resources loaded");

                // Set up global exception handling
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                DispatcherUnhandledException += OnDispatcherUnhandledException;

                // Initialize the main window
                var mainWindow = new MainWindow();
                mainWindow.Show();

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
                // Get the path to SharedData folder (relative to app location)
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                var sharedDataPath = Path.GetFullPath(Path.Combine(appPath, "..", "..", "..", "SharedData", "resources.xml"));
                
                // If not found, try from development structure
                if (!File.Exists(sharedDataPath))
                {
                    sharedDataPath = Path.GetFullPath(Path.Combine(appPath, "..", "..", "..", "..", "SharedData", "resources.xml"));
                }
                
                // If still not found, try absolute path for development
                if (!File.Exists(sharedDataPath))
                {
                    sharedDataPath = @"E:\projects\employee_management_csharp\SharedData\resources.xml";
                }
                
                if (File.Exists(sharedDataPath))
                {
                    ResourceManager.LoadResources(sharedDataPath);
                    _logger?.LogInformation($"Loaded resources from: {sharedDataPath}");
                }
                else
                {
                    _logger?.LogWarning($"Resources file not found at: {sharedDataPath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load localized resources");
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