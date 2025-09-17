using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using Shared.Services;
using DisplayApp.Utils;

namespace DisplayApp
{
    public partial class App : Application
    {
        private ILogger<App> _logger;
        private ConfigHelper _configHelper;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Initialize logging
                _logger = LoggingService.CreateLogger<App>();
                _logger.LogInformation("DisplayApp starting up");

                // Initialize configuration
                _configHelper = new ConfigHelper();
                _logger.LogInformation("Configuration loaded");

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
                MessageBox.Show($"خطا در راه‌اندازی برنامه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _logger?.LogInformation("DisplayApp shutting down");
                
                // Save configuration
                _configHelper?.SaveConfig();
                
                _logger?.LogInformation("DisplayApp shutdown complete");
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
                
                MessageBox.Show($"خطای غیرمنتظره: {exception?.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // If logging fails, just show a basic error message
                MessageBox.Show("خطای غیرمنتظره رخ داده است", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                _logger?.LogError(e.Exception, "Dispatcher unhandled exception occurred");
                
                MessageBox.Show($"خطا در رابط کاربری: {e.Exception.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Mark as handled to prevent application crash
                e.Handled = true;
            }
            catch
            {
                // If logging fails, just show a basic error message
                MessageBox.Show("خطا در رابط کاربری رخ داده است", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            }
        }
    }
}