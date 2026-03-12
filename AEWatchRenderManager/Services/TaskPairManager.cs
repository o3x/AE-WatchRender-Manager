using AEWatchRenderManager.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AEWatchRenderManager.Services
{
    // Date: Thu Mar 12 10:11:00 JST 2026
    // Version: 1.5.0
    public class TaskPairManager
    {
        public ObservableCollection<RenderTaskPair> Tasks { get; } = new();

        public async Task SyncWithDirectoriesAsync(string[] subDirs)
        {
            var currentRcfPaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in subDirs)
            {
                try
                {
                    // フォルダ内の *_RCF.txt を探す
                    var rcfFiles = Directory.GetFiles(dir, "*_RCF.txt");
                    foreach (var rcf in rcfFiles)
                    {
                        currentRcfPaths.Add(rcf);
                        AddOrUpdateRcfTask(rcf);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            // 存在しなくなったタスクのクリーンアップ
            var toRemove = Tasks.Where(t => !currentRcfPaths.Contains(t.RcfFilePath)).ToList();
            foreach (var t in toRemove)
            {
                Application.Current.Dispatcher.Invoke(() => Tasks.Remove(t));
            }

            await Task.CompletedTask;
        }

        private void AddOrUpdateRcfTask(string rcfPath)
        {
            var existingTask = Tasks.FirstOrDefault(t => string.Equals(t.RcfFilePath, rcfPath, StringComparison.OrdinalIgnoreCase));
            if (existingTask == null)
            {
                var newTask = new RenderTaskPair(rcfPath);
                Application.Current.Dispatcher.Invoke(() => Tasks.Add(newTask));
            }
        }

        public void RemoveTask(string filePath)
        {
            // プロジェクトフォルダ自体または RCFファイルが削除された場合
            var task = Tasks.FirstOrDefault(t => 
                string.Equals(t.RcfFilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.ProjectFolderPath, filePath, StringComparison.OrdinalIgnoreCase));

            if (task != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() => Tasks.Remove(task));
            }
        }
    }
}
