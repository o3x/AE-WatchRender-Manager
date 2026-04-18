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
    // Date: Sat Apr 18 09:28:17 JST 2026
    // Version: 1.16.19
    public class TaskPairManager
    {
        public ObservableCollection<RenderTaskPair> Tasks { get; } = new();

        public async Task SyncWithDirectoriesAsync(string[] subDirs)
        {
            // I/O をバックグラウンドスレッドで実行
            var currentRcfPaths = await Task.Run(() =>
            {
                var paths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var dir in subDirs)
                {
                    try
                    {
                        foreach (var rcf in Directory.GetFiles(dir, "*_RCF.txt"))
                            paths.Add(rcf);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                return paths;
            });

            // UI 操作は Dispatcher 経由で実行
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 新規タスクの追加
                foreach (var rcf in currentRcfPaths)
                    AddOrUpdateRcfTask(rcf);

                // 存在しなくなったタスクのクリーンアップ
                var toRemove = Tasks.Where(t => !currentRcfPaths.Contains(t.RcfFilePath)).ToList();
                foreach (var t in toRemove)
                    Tasks.Remove(t);
            });
        }

        private void AddOrUpdateRcfTask(string rcfPath)
        {
            var existingTask = Tasks.FirstOrDefault(t => string.Equals(t.RcfFilePath, rcfPath, StringComparison.OrdinalIgnoreCase));
            if (existingTask == null)
            {
                Tasks.Add(new RenderTaskPair(rcfPath));
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
