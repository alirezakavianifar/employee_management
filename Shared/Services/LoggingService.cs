using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System;

namespace Shared.Services
{
    public static class LoggingService
    {
        private static ILoggerFactory? _loggerFactory;

        public static void ConfigureLogging()
        {
            if (_loggerFactory != null)
                return;

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddProvider(new FileLoggerProvider("Data/Logs/management_app.log"));
                builder.SetMinimumLevel(LogLevel.Information);
            });

            _loggerFactory = serviceCollection.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        }

        public static ILogger<T> CreateLogger<T>()
        {
            if (_loggerFactory == null)
                ConfigureLogging();
            
            return _loggerFactory!.CreateLogger<T>();
        }

        public static ILogger CreateLogger(string categoryName)
        {
            if (_loggerFactory == null)
                ConfigureLogging();
            
            return _loggerFactory!.CreateLogger(categoryName);
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;

        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath;
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _filePath);
        }

        public void Dispose()
        {
        }
    }

    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;

        public FileLogger(string categoryName, string filePath)
        {
            _categoryName = categoryName;
            _filePath = filePath;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {_categoryName}: {message}";
            
            if (exception != null)
            {
                logEntry += $"\nException: {exception}";
            }

            try
            {
                File.AppendAllText(_filePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore file write errors
            }
        }
    }
}
