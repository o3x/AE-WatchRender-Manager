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
        
        [ObservableProperty]
        private int _scanIntervalSeconds = 3;

        public System.Collections.ObjectModel.ObservableCollection<RenderTaskPair> Tasks => _taskManager.Tasks;

        private readonly TaskPairManager _taskManager;
        private readonly DispatcherTimer _scanTimer;

        public MainViewModel()
        {
            _taskManager = new TaskPairManager();

            _scanTimer = new DispatcherTimer();
            _scanTimer.Tick += async (s, e) => await ScanMonitorFolderAsync();
        }

        [RelayCommand]
        private void StartMonitoring()
        {
            if (Directory.Exists(MonitorPath))
            {
                _scanTimer.Interval = TimeSpan.FromSeconds(ScanIntervalSeconds <= 0 ? 3 : ScanIntervalSeconds);
                _scanTimer.Start();
                WindowTitle = $"AE WatchRender Manager - 監視中: {MonitorPath}";
                
                // 初回スキャンを即座に実行
                _ = ScanMonitorFolderAsync();
            }
        }

        [RelayCommand]
        private void StopMonitoring()
        {
            _scanTimer.Stop();
            WindowTitle = "AE WatchRender Manager";
        }

        private async Task ScanMonitorFolderAsync()
        {
            if (string.IsNullOrEmpty(MonitorPath) || !Directory.Exists(MonitorPath)) return;

            // 監視パスの第一階層にあるサブフォルダを総走査
            var subDirs = Directory.GetDirectories(MonitorPath);
            await _taskManager.SyncWithDirectoriesAsync(subDirs);
            
            // ステータスの更新処理を走らせる
            var currentTasks = Tasks.ToList();
            foreach (var task in currentTasks)
            {
                if (task.Status != RenderStatus.Completed && task.Status != RenderStatus.Error)
                {
                    await StatusAnalyzer.AnalyzeAsync(task);
                }
            }
        }

        // Deleted UpdateStatusesAsync        [RelayCommand]
        private void DeleteTask(RenderTaskPair? task)
        {
            if (task == null || string.IsNullOrEmpty(task.ProjectFolderPath)) return;
            try
            {
                if (Directory.Exists(task.ProjectFolderPath))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                        task.ProjectFolderPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"削除エラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void MoveTask(RenderTaskPair? task)
        {
            if (task == null || string.IsNullOrEmpty(task.ProjectFolderPath)) return;

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "移動先の親フォルダを選択してください"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var folderName = Path.GetFileName(task.ProjectFolderPath);
                    var targetPath = Path.Combine(dialog.FolderName, folderName);
                    
                    if (Directory.Exists(task.ProjectFolderPath))
                    {
                        Directory.Move(task.ProjectFolderPath, targetPath);
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
                        var baseName = Path.GetFileNameWithoutExtension(file);
                        var folderName = baseName;
                        var targetDir = Path.Combine(MonitorPath, folderName);
                        
                        int counter = 1;
                        while (Directory.Exists(targetDir))
                        {
                            folderName = $"{baseName}_{counter}";
                            targetDir = Path.Combine(MonitorPath, folderName);
                            counter++;
                        }
                        
                        Directory.CreateDirectory(targetDir);
                        
                        var targetAepPath = Path.Combine(targetDir, Path.GetFileName(file));
                        File.Copy(file, targetAepPath, true);
                        
                        var rcfPath = Path.Combine(targetDir, $"{folderName}_RCF.txt");
                        var txtPath = Path.Combine(targetDir, $"{folderName}.txt");
                        var htmlName = $"{folderName}.htm";
                        
                        File.WriteAllText(txtPath, string.Empty);
                        File.WriteAllText(rcfPath, $"After Effects Render Control File\nmax_machines=5\nnum_machines=0\ninit=0\nhtml_init=1\nhtml_name=\"{htmlName}\"");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"ドロップ処理エラー: {Path.GetFileName(file)}\n{ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}
