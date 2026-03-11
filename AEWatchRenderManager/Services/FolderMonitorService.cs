using System;
using System.IO;

namespace AEWatchRenderManager.Services
{
    // Date: Wed Mar 11 12:42:00 JST 2026
    // Version: 1.0.0
    public class FolderMonitorService : IDisposable
    {
        private FileSystemWatcher? _watcher;

        public event EventHandler<FileSystemEventArgs>? FileCreated;
        public event EventHandler<FileSystemEventArgs>? FileDeleted;
        public event EventHandler<FileSystemEventArgs>? FileChanged;
        public event EventHandler<RenamedEventArgs>? FileRenamed;

        public void StartMonitoring(string path)
        {
            StopMonitoring();

            if (!Directory.Exists(path))
                return;

            _watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.Size,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _watcher.Created += (s, e) => FileCreated?.Invoke(this, e);
            _watcher.Deleted += (s, e) => FileDeleted?.Invoke(this, e);
            _watcher.Changed += (s, e) => FileChanged?.Invoke(this, e);
            _watcher.Renamed += (s, e) => FileRenamed?.Invoke(this, e);
        }

        public void StopMonitoring()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
