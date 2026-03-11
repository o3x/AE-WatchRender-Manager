using AEWatchRenderManager.Models;
using AEWatchRenderManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AEWatchRenderManager.ViewModels
{
    // Date: Wed Mar 11 12:47:00 JST 2026
    // Version: 1.1.0
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _windowTitle = "AE WatchRender Manager";

        [ObservableProperty]
        private string _monitorPath = string.Empty;

        public System.Collections.ObjectModel.ObservableCollection<RenderTaskPair> Tasks => _taskManager.Tasks;

        private readonly FolderMonitorService _monitorService;
        private readonly TaskPairManager _taskManager;
        private readonly DispatcherTimer _statusUpdateTimer;

        public MainViewModel()
        {
            _monitorService = new FolderMonitorService();
            _taskManager = new TaskPairManager();

            _monitorService.FileCreated += (s, e) => _taskManager.ProcessFileChange(e.FullPath);
            _monitorService.FileChanged += (s, e) => _taskManager.ProcessFileChange(e.FullPath);
            
            _monitorService.FileDeleted += (s, e) => _taskManager.RemoveTask(e.FullPath);
            _monitorService.FileRenamed += (s, e) => 
            {
                _taskManager.RemoveTask(e.OldFullPath);
                _taskManager.ProcessFileChange(e.FullPath);
            };

            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _statusUpdateTimer.Tick += async (s, e) => await UpdateStatusesAsync();
        }

        [RelayCommand]
        private void StartMonitoring()
        {
            if (Directory.Exists(MonitorPath))
            {
                _monitorService.StartMonitoring(MonitorPath);
                _statusUpdateTimer.Start();
                WindowTitle = $"AE WatchRender Manager - 監視中: {MonitorPath}";
            }
        }

        [RelayCommand]
        private void StopMonitoring()
        {
            _monitorService.StopMonitoring();
            _statusUpdateTimer.Stop();
            WindowTitle = "AE WatchRender Manager";
        }

        private async Task UpdateStatusesAsync()
        {
            var currentTasks = Tasks.ToList();
            foreach (var task in currentTasks)
            {
                if (task.Status != RenderStatus.Completed && task.Status != RenderStatus.Error)
                {
                    await StatusAnalyzer.AnalyzeAsync(task);
                }
            }
        }

        [RelayCommand]
        private void DeleteTask(RenderTaskPair? task)
        {
            if (task == null) return;
            try
            {
                if (File.Exists(task.FilePath)) File.Delete(task.FilePath);
                if (!string.IsNullOrEmpty(task.LogFilePath) && File.Exists(task.LogFilePath)) File.Delete(task.LogFilePath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"削除エラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void MoveTask(RenderTaskPair? task)
        {
            if (task == null) return;

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "移動先フォルダを選択してください"
            };

            if (dialog.ShowDialog() == true)
            {
                var targetDir = dialog.FolderName;
                try
                {
                    var newAepPath = Path.Combine(targetDir, task.FileName);
                    if (File.Exists(task.FilePath)) File.Move(task.FilePath, newAepPath, true);

                    if (!string.IsNullOrEmpty(task.LogFilePath) && File.Exists(task.LogFilePath))
                    {
                        var newLogPath = Path.Combine(targetDir, Path.GetFileName(task.LogFilePath));
                        File.Move(task.LogFilePath, newLogPath, true);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"移動エラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void DropFiles(string[]? files)
        {
            if (files == null || string.IsNullOrEmpty(MonitorPath) || !Directory.Exists(MonitorPath)) return;

            foreach (var file in files)
            {
                try
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".aep")
                    {
                        var dest = Path.Combine(MonitorPath, Path.GetFileName(file));
                        File.Copy(file, dest, true);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"コピーエラー: {Path.GetFileName(file)}\n{ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}
