using AEWatchRenderManager.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace AEWatchRenderManager.Services
{
    // Date: Wed Mar 11 12:44:00 JST 2026
    // Version: 1.0.0
    public class TaskPairManager
    {
        public ObservableCollection<RenderTaskPair> Tasks { get; } = new();

        public void ProcessFileChange(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".aep")
            {
                AddOrUpdateAepTask(filePath);
            }
            else if (ext == ".txt" || ext == ".htm" || ext == ".html")
            {
                MatchLogToAep(filePath);
            }
        }

        private void AddOrUpdateAepTask(string aepPath)
        {
            var existingTask = Tasks.FirstOrDefault(t => t.FilePath.Equals(aepPath, StringComparison.OrdinalIgnoreCase));
            if (existingTask == null)
            {
                var newTask = new RenderTaskPair(aepPath);
                FindExistingLogForAep(newTask);
                
                Application.Current.Dispatcher.InvokeAsync(() => Tasks.Add(newTask));
            }
            else
            {
                existingTask.LastUpdateTime = File.Exists(aepPath) ? File.GetLastWriteTime(aepPath) : DateTime.Now;
            }
        }

        private void MatchLogToAep(string logPath)
        {
            var logFileName = Path.GetFileNameWithoutExtension(logPath);
            var logDir = Path.GetDirectoryName(logPath);

            var matchedTask = Tasks.FirstOrDefault(t => 
                logFileName.StartsWith(Path.GetFileNameWithoutExtension(t.FilePath), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetDirectoryName(t.FilePath), logDir, StringComparison.OrdinalIgnoreCase));

            if (matchedTask != null)
            {
                matchedTask.LogFilePath = logPath;
                matchedTask.LastUpdateTime = File.Exists(logPath) ? File.GetLastWriteTime(logPath) : DateTime.Now;
            }
        }
        
        private void FindExistingLogForAep(RenderTaskPair task)
        {
            var dir = Path.GetDirectoryName(task.FilePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            var baseName = Path.GetFileNameWithoutExtension(task.FilePath);
            
            try
            {
                var logFiles = Directory.GetFiles(dir, $"{baseName}*.*")
                    .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToList();

                if (logFiles.Any())
                {
                    task.LogFilePath = logFiles.First();
                }
            }
            catch (IOException)
            {
                // アクセス権限等のエラーは一旦無視
            }
        }
        
        public void RemoveTask(string filePath)
        {
            var task = Tasks.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (task != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() => Tasks.Remove(task));
            }
        }
    }
}
