using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Shared.Services
{
    public class SyncManager : IDisposable
    {
        private readonly string _dataDir;
        private readonly ILogger<SyncManager> _logger;
        private FileSystemWatcher? _watcher;
        private Timer? _periodicTimer;
        private readonly List<Action> _callbacks = new();
        private readonly object _callbackLock = new();
        private bool _disposed = false;
        private DateTime _lastSaveTime = DateTime.MinValue;

        public SyncManager(string dataDir = "Data")
        {
            _dataDir = dataDir;
            _logger = LoggingService.CreateLogger<SyncManager>();
            _logger.LogInformation("SyncManager initialized with data directory: {DataDir}", _dataDir);
        }

        public void AddSyncCallback(Action callback)
        {
            lock (_callbackLock)
            {
                if (!_callbacks.Contains(callback))
                {
                    _callbacks.Add(callback);
                    _logger.LogInformation("Added sync callback. Total callbacks: {Count}", _callbacks.Count);
                }
            }
        }

        public void RemoveSyncCallback(Action callback)
        {
            lock (_callbackLock)
            {
                _callbacks.Remove(callback);
                _logger.LogInformation("Removed sync callback. Total callbacks: {Count}", _callbacks.Count);
            }
        }

        public void StartSync()
        {
            if (_disposed)
                return;

            try
            {
                // Start file monitoring
                StartFileMonitoring();
                
                // Disable periodic sync to prevent overriding user capacity changes
                // File monitoring provides sufficient real-time sync
                // _periodicTimer = new Timer(OnPeriodicSync, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
                
                _logger.LogInformation("Started synchronization");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start sync");
            }
        }

        public void StopSync()
        {
            try
            {
                _periodicTimer?.Dispose();
                _periodicTimer = null;
                
                _watcher?.Dispose();
                _watcher = null;
                
                _logger.LogInformation("Stopped synchronization");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop sync");
            }
        }

        private void StartFileMonitoring()
        {
            try
            {
                if (!Directory.Exists(_dataDir))
                {
                    Directory.CreateDirectory(_dataDir);
                }

                _watcher = new FileSystemWatcher(_dataDir)
                {
                    Filter = "*.json",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileCreated;
                _watcher.Deleted += OnFileDeleted;
                _watcher.Renamed += OnFileRenamed;

                _logger.LogInformation("Started file monitoring for directory: {DataDir}", _dataDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start file monitoring");
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_disposed || string.IsNullOrEmpty(e.FullPath) || !e.FullPath.EndsWith(".json"))
                return;

            // Check if this is a real modification (not just a backup)
            if (e.FullPath.Contains("backup", StringComparison.OrdinalIgnoreCase))
                return;

            // Debounce rapid modifications (0.5 seconds)
            var currentTime = DateTime.Now;
            if ((currentTime - _lastSaveTime).TotalSeconds < 0.5)
                return;

            _logger.LogInformation("File modified: {FilePath}", e.FullPath);
            TriggerSync();
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (_disposed || string.IsNullOrEmpty(e.FullPath) || !e.FullPath.EndsWith(".json"))
                return;

            if (e.FullPath.Contains("backup", StringComparison.OrdinalIgnoreCase))
                return;

            _logger.LogInformation("File created: {FilePath}", e.FullPath);
            TriggerSync();
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (_disposed || string.IsNullOrEmpty(e.FullPath) || !e.FullPath.EndsWith(".json"))
                return;

            _logger.LogInformation("File deleted: {FilePath}", e.FullPath);
            TriggerSync();
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_disposed || string.IsNullOrEmpty(e.FullPath) || !e.FullPath.EndsWith(".json"))
                return;

            _logger.LogInformation("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            TriggerSync();
        }

        private void OnPeriodicSync(object? state)
        {
            if (_disposed)
                return;

            _logger.LogInformation("Periodic sync triggered");
            TriggerSync();
        }

        public void TriggerSync()
        {
            if (_disposed)
                return;

            try
            {
                List<Action> callbacksToExecute;
                lock (_callbackLock)
                {
                    callbacksToExecute = new List<Action>(_callbacks);
                }

                _logger.LogInformation("Triggering sync with {Count} callbacks", callbacksToExecute.Count);

                foreach (var callback in callbacksToExecute)
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in sync callback");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sync callback execution");
            }
        }

        public void ForceSync()
        {
            _logger.LogInformation("Forcing sync");
            TriggerSync();
        }

        public void NotifySave()
        {
            _lastSaveTime = DateTime.Now;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopSync();
            
            lock (_callbackLock)
            {
                _callbacks.Clear();
            }

            _logger.LogInformation("SyncManager disposed");
        }
    }
}
