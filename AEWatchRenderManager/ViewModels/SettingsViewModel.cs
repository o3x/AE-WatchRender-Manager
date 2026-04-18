// SettingsViewModel.cs
// Version: 2.1.0
// Updated: Sat Apr 18 19:06:22 JST 2026

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace AEWatchRenderManager.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string _monitorPath = string.Empty;
        [ObservableProperty] private string _moveTargetPath = string.Empty;
        [ObservableProperty] private int _scanIntervalSeconds = 60;
        [ObservableProperty] private string _aerenderPath = string.Empty;
        [ObservableProperty] private bool _keepAerenderWindowOpen = false;

        [RelayCommand]
        private void BrowseMonitorFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "監視するルートフォルダを選択してください",
                InitialDirectory = Directory.Exists(MonitorPath) ? MonitorPath : null
            };
            if (dialog.ShowDialog() == true) MonitorPath = dialog.FolderName;
        }

        [RelayCommand]
        private void BrowseMoveTarget()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "移動先のルートフォルダを選択してください",
                InitialDirectory = Directory.Exists(MoveTargetPath) ? MoveTargetPath : null
            };
            if (dialog.ShowDialog() == true) MoveTargetPath = dialog.FolderName;
        }

        [RelayCommand]
        private void BrowseAerenderExe()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "aerender.exe を選択してください（バージョン不一致時のフォールバック）",
                Filter = "aerender.exe|aerender.exe|実行ファイル (*.exe)|*.exe",
                FileName = "aerender.exe"
            };
            if (dialog.ShowDialog() == true) AerenderPath = dialog.FileName;
        }
    }
}
