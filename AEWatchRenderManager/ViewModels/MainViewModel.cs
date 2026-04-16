using AEWatchRenderManager.Models;
using AEWatchRenderManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;

namespace AEWatchRenderManager.ViewModels
{
    // Date: Thu Apr 16 11:32:10 JST 2026
    // Version: 1.16.15
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private string _windowTitle = "AE WatchRender Manager";

        [ObservableProperty]
        private string _monitorPath = string.Empty;
        
        [ObservableProperty]
        private int _scanIntervalSeconds = 60;

        [ObservableProperty]
        private string _moveTargetPath = string.Empty;

        /// <summary>最終スキャン時刻。スキャン完了時のみ更新。StatusBar に表示する。</summary>
        [ObservableProperty]
        private string _lastScanText = "---";

        partial void OnIsMonitoringChanged(bool value)
        {
            StartMonitoringCommand.NotifyCanExecuteChanged();
            StopMonitoringCommand.NotifyCanExecuteChanged();
        }

        partial void OnMonitorPathChanged(string value) => SaveSettings();
        partial void OnMoveTargetPathChanged(string value) => SaveSettings();
        partial void OnScanIntervalSecondsChanged(int value)
        {
            if (_scanTimer != null && _scanTimer.IsEnabled)
            {
                _scanTimer.Interval = TimeSpan.FromSeconds(value <= 0 ? 3 : value);
            }
            SaveSettings();
        }

        public System.Collections.ObjectModel.ObservableCollection<RenderTaskPair> Tasks => _taskManager.Tasks;

        /// <summary>ソート対応ビュー。ListView はこちらにバインドする。</summary>
        public ICollectionView TasksView { get; }

        private string _currentSortColumn = string.Empty;
        private ListSortDirection _currentSortDirection = ListSortDirection.Ascending;

        private readonly TaskPairManager _taskManager;
        private readonly DispatcherTimer _scanTimer;
        private bool _isScanning = false;

        public MainViewModel()
        {
            _taskManager = new TaskPairManager();

            // 設定のロード
            var settings = SettingsService.Load();
            _monitorPath = settings.MonitorPath;
            _moveTargetPath = settings.MoveTargetPath;
            _scanIntervalSeconds = settings.ScanIntervalSeconds;

            _scanTimer = new DispatcherTimer();
            _scanTimer.Tick += async (s, e) => await ScanMonitorFolderAsync();

            TasksView = CollectionViewSource.GetDefaultView(_taskManager.Tasks);

            UpdateWindowTitle();
        }

        private void SaveSettings()
        {
            SettingsService.Save(new AppSettings
            {
                MonitorPath = MonitorPath,
                MoveTargetPath = MoveTargetPath,
                ScanIntervalSeconds = ScanIntervalSeconds
            });
        }

        private void UpdateWindowTitle()
        {
            if (_scanTimer != null && _scanTimer.IsEnabled)
            {
                WindowTitle = $"AE WatchRender Manager [監視中: {MonitorPath}]";
            }
            else
            {
                WindowTitle = "AE WatchRender Manager";
            }
        }

        private bool CanStartMonitoring() => !IsMonitoring;

        [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
        private void StartMonitoring()
        {
            if (string.IsNullOrEmpty(MonitorPath) || !Directory.Exists(MonitorPath))
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "監視するルートフォルダを選択してください"
                };
                if (dialog.ShowDialog() == true)
                {
                    MonitorPath = dialog.FolderName;
                }
                else
                {
                    // キャンセルされたら何もしない
                    return;
                }
            }

            if (Directory.Exists(MonitorPath))
            {
                _scanTimer.Interval = TimeSpan.FromSeconds(ScanIntervalSeconds <= 0 ? 3 : ScanIntervalSeconds);
                _scanTimer.Start();
                IsMonitoring = true;
                UpdateWindowTitle();
                
                // 初回スキャンを即座に実行
                // @problem: fire-and-forget の例外は握り潰されて診断不能になる
                // @solution: ContinueWith(OnlyOnFaulted) でデバッグログに残す
                _ = ScanMonitorFolderAsync().ContinueWith(
                    t => Debug.WriteLine($"[ScanMonitorFolder] 非同期例外: {t.Exception}"),
                    System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private bool CanStopMonitoring() => IsMonitoring;

        [RelayCommand(CanExecute = nameof(CanStopMonitoring))]
        private void StopMonitoring()
        {
            _scanTimer.Stop();
            IsMonitoring = false;
            UpdateWindowTitle();
        }

        [RelayCommand]
        private async Task ScanNow()
        {
            await ScanMonitorFolderAsync();
            if (_scanTimer.IsEnabled)
            {
                _scanTimer.Stop();
                _scanTimer.Start();
            }
        }

        [RelayCommand]
        private void BrowseMonitorFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "監視するルートフォルダを選択してください"
            };
            if (dialog.ShowDialog() == true)
            {
                MonitorPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void SetScanCycle()
        {
            var dialog = new Views.ScanCycleDialog(ScanIntervalSeconds)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                ScanIntervalSeconds = dialog.ResultSeconds;
            }
        }

        [RelayCommand]
        private void SortByColumn(string propertyName)
        {
            if (_currentSortColumn == propertyName)
            {
                _currentSortDirection = _currentSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _currentSortColumn = propertyName;
                _currentSortDirection = ListSortDirection.Ascending;
            }

            TasksView.SortDescriptions.Clear();
            TasksView.SortDescriptions.Add(new SortDescription(propertyName, _currentSortDirection));
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var task in Tasks)
            {
                task.IsSelected = true;
            }
        }

        [RelayCommand]
        private void SelectCompleted()
        {
            foreach (var task in Tasks)
            {
                task.IsSelected = (task.Status == RenderStatus.Completed);
            }
        }

        [RelayCommand]
        private void ShowAbout()
        {
            System.Windows.MessageBox.Show(
                "AE WatchRender Manager\nVersion 1.16.15\n\nAfter Effectsの監視フォルダーを管理するためのツールです。\n\nCopyright © 2026 OHYAMA Yoshihisa\nLicensed under the Apache License, Version 2.0",
                "バージョン情報",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private async void TriggerImmediateScan()
        {
            if (!_scanTimer.IsEnabled) return;

            // タイマーを一度止めて、スキャン後に再開（リセット）
            _scanTimer.Stop();
            await ScanMonitorFolderAsync();
            _scanTimer.Start();
        }



        private async Task ScanMonitorFolderAsync()
        {
            if (string.IsNullOrEmpty(MonitorPath) || !Directory.Exists(MonitorPath)) return;
            if (_isScanning) return;

            try
            {
                _isScanning = true;

                // 監視パスの第一階層にあるサブフォルダを総走査 (IO処理はTask.Runで実行)
                var subDirs = await Task.Run(() => Directory.GetDirectories(MonitorPath));
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

                LastScanText = DateTime.Now.ToString("HH:mm:ss");
            }
            finally
            {
                _isScanning = false;
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

            TriggerImmediateScan();
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
                $"{tasks.Count}件の監視アイテムを以下のフォルダへ移動しますか？\n\n移動先:\n{MoveTargetPath}\n\n(プロジェクトフォルダ全体の移動)",
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
                    // @problem: folderName に "../" 等が含まれると MoveTargetPath の外へ脱出できる（パストラバーサル）
                    // @solution: Path.GetFullPath で正規化してから StartsWith でベースパス内に収まるか検証する
                    var targetPath = Path.GetFullPath(Path.Combine(MoveTargetPath, folderName));
                    var targetBase = Path.GetFullPath(MoveTargetPath);
                    if (!targetPath.StartsWith(targetBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(targetPath, targetBase, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[MoveTask] パストラバーサルを検出したためスキップ: {targetPath}");
                        continue;
                    }

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

            TriggerImmediateScan();
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
                        // @problem: "After Effects Render Control File"（バージョン文字列なし）では
                        //           AEの監視フォルダーがRCFを正しく認識しない。
                        // @solution: "After Effects 13.2v1 Render Control File" という固有の
                        //            バージョン文字列が必須。AEはこの1行目をマジックナンバーとして
                        //            パースしており、省略・変更するとRCFと認識されない。
                        File.WriteAllText(rcfPath, $"After Effects 13.2v1 Render Control File\r\nmax_machines=99\r\nnum_machines=0\r\ninit=0\r\nhtml_init=0\r\nhtml_name=\"{htmlName}\"\r\n");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"ドロップ処理エラー: {Path.GetFileName(file)}\n{ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }

            TriggerImmediateScan();
        }

        [RelayCommand]
        private void OpenRenderInfo(System.Collections.IList? items)
        {
            if (items == null || items.Count == 0) return;
            var task = items.Cast<RenderTaskPair>().FirstOrDefault();
            
            if (task != null && !string.IsNullOrEmpty(task.HtmlLogFilePath) && File.Exists(task.HtmlLogFilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(task.HtmlLogFilePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"情報表示エラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("HTMLログファイルが見つかりません。\n(テキストログの場合は開けません)", "情報", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void ShowAepFile(System.Collections.IList? items)
        {
            if (items == null || items.Count == 0) return;
            var task = items.Cast<RenderTaskPair>().FirstOrDefault();
            
            if (task != null && !string.IsNullOrEmpty(task.AepFilePath) && File.Exists(task.AepFilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{task.AepFilePath}\"") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"フォルダ展開エラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("対象のAEPファイルが見つかりません。", "情報", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void OpenDisplayPath(RenderTaskPair? task)
        {
            if (task == null) return;

            // DisplayPath と同じ優先順位: 出力先フォルダ → プロジェクトフォルダ
            var path = task.HasOutputPath ? task.OutputFolderPath : task.ProjectFolderPath;

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                System.Windows.MessageBox.Show(
                    "フォルダが見つかりません。",
                    "情報", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"フォルダを開けませんでした: {ex.Message}", "エラー",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ShowRenderDestination(System.Collections.IList? items)
        {
            if (items == null || items.Count == 0) return;
            var task = items.Cast<RenderTaskPair>().FirstOrDefault();
            if (task == null) return;

            // OutputFolderPath（レンダリング出力先）を優先して開く
            var targetPath = !string.IsNullOrEmpty(task.OutputFolderPath) && Directory.Exists(task.OutputFolderPath)
                ? task.OutputFolderPath
                : null;

            if (targetPath == null)
            {
                System.Windows.MessageBox.Show(
                    "レンダリング先フォルダが見つかりません。\nレンダリングがまだ開始されていない可能性があります。",
                    "情報", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"フォルダ展開エラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
