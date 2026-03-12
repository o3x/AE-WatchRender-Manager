using AEWatchRenderManager.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;

namespace AEWatchRenderManager.Services
{
    // Date: Thu Mar 12 10:11:00 JST 2026
    // Version: 1.5.0
    public class TaskPairManager
    {
        public ObservableCollection<RenderTaskPair> Tasks { get; } = new();

        public void ProcessFileChange(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            
            // _RCF.txtのみを監視のトリガーとする
            if (fileName.EndsWith("_RCF.txt", StringComparison.OrdinalIgnoreCase))
            {
                AddOrUpdateRcfTask(filePath);
            }
            // ログファイル(.htm / .html)の更新はタイマー周期に任せるか、既存タスクに関連付け
            else if (fileName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) || 
                     fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                // RCF以外で、かつ対応するプロジェクトフォルダ内のファイル変更なら更新時間を叩く
                var dir = Path.GetDirectoryName(filePath);
                var task = Tasks.FirstOrDefault(t => string.Equals(t.ProjectFolderPath, dir, StringComparison.OrdinalIgnoreCase));
                if (task != null)
                {
                    task.LastUpdateTime = DateTime.Now;
                }
            }
        }

        private void AddOrUpdateRcfTask(string rcfPath)
        {
            var existingTask = Tasks.FirstOrDefault(t => string.Equals(t.RcfFilePath, rcfPath, StringComparison.OrdinalIgnoreCase));
            if (existingTask == null)
            {
                var newTask = new RenderTaskPair(rcfPath);
                Application.Current.Dispatcher.InvokeAsync(() => Tasks.Add(newTask));
            }
            else
            {
                existingTask.LastUpdateTime = File.Exists(rcfPath) ? File.GetLastWriteTime(rcfPath) : DateTime.Now;
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
