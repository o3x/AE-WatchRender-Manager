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

        [ObservableProperty]
        private string _moveTargetPath = string.Empty;

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
                if (task.Status != RenderStatus.Completed && task.Status != RenderStatus.Failed)
                {
                    await StatusAnalyzer.AnalyzeAsync(task);
                }
            }
        }

        [RelayCommand]
        private void BrowseMoveTarget()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "移動先のルートフォルダを選択してください"
            };
            if (dialog.ShowDialog() == true)
            {
                MoveTargetPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void DeleteTask(System.Collections.IList? items)
        {
            if (items == null || items.Count == 0) return;
            var tasks = items.Cast<RenderTaskPair>().ToList();

            var result = System.Windows.MessageBox.Show(
                $"{tasks.Count}件の監視アイテムをごみ箱へ移動しますか？\n(プロジェクトフォルダ全体が対象となります)",
                "削除の確認",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
                
            if (result != System.Windows.MessageBoxResult.Yes) return;

            foreach (var task in tasks)
            {
                if (string.IsNullOrEmpty(task.ProjectFolderPath)) continue;
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
                    System.Windows.MessageBox.Show($"削除エラー ({task.ProjectName}): {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void MoveTask(System.Collections.IList? items)
        {
            if (items == null || items.Count == 0) return;
            if (string.IsNullOrEmpty(MoveTargetPath) || !Directory.Exists(MoveTargetPath))
            {
                System.Windows.MessageBox.Show("有効な移動先フォルダが設定されていません。\n上部の「移動先フォルダ」を指定してください。", "操作エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var tasks = items.Cast<RenderTaskPair>().ToList();
            var result = System.Windows.MessageBox.Show(
                $"{tasks.Count}件の監視アイテムを以下のフォルダへ移動しますか？\n\n移動先: {MoveTargetPath}\n(プロジェクトフォルダ全体の移動)",
                "移動の確認",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
                
            if (result != System.Windows.MessageBoxResult.Yes) return;

            foreach (var task in tasks)
            {
                if (string.IsNullOrEmpty(task.ProjectFolderPath)) continue;
                try
                {
                    var folderName = Path.GetFileName(task.ProjectFolderPath);
                    var targetPath = Path.Combine(MoveTargetPath, folderName);
                    
                    if (Directory.Exists(task.ProjectFolderPath))
                    {
                        Directory.Move(task.ProjectFolderPath, targetPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"移動エラー ({task.ProjectName}): {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                        var txtPath = Path.Combine(targetDir, $"{folderName}レポート.txt");
                        var htmlName = ""; // HTMLは生成させない
                        
                        var txtContent = $"レポート作成日 : \r\n\t{DateTime.Now:yyyy/MM/dd\tH:mm:ss}\r\n\r\nプロジェクト名 : {Path.GetFileName(file)}\r\n\r\n収集されたソースファイル先 : \r\n\t{targetDir}\r\n\r\n収集されたソースファイル : なし\r\n\r\n収集されたコンポジション :  \r\n\tコンポ 1\r\n\t\r\n収集されたファイルの数 :  0\r\n\r\n収集されたファイルのサイズ :  0 KB\r\n\r\nレンダリングプラグイン:\r\n\tクラシック3D\r\n\t\r\n";
                        File.WriteAllText(txtPath, txtContent);
                        File.WriteAllText(rcfPath, $"After Effects 13.2v1 Render Control File\r\nmax_machines=99\r\nnum_machines=0\r\ninit=0\r\nhtml_init=0\r\nhtml_name=\"{htmlName}\"\r\n");
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
