using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace AEWatchRenderManager.Models
{
    // Date: Thu Mar 12 10:10:00 JST 2026
    // Version: 1.5.0
    public enum RenderStatus
    {
        Pending,   // 未処理
        Processing,// 処理中
        Completed, // 完了
        Error      // エラー
    }

    public partial class RenderTaskPair : ObservableObject
    {
        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private string _projectFolderPath = string.Empty;

        [ObservableProperty]
        private string _rcfFilePath = string.Empty;

        [ObservableProperty]
        private string _aepFilePath = string.Empty;

        [ObservableProperty]
        private string? _htmlLogFilePath;

        [ObservableProperty]
        private RenderStatus _status = RenderStatus.Pending;

        [ObservableProperty]
        private string _statusText = "未処理";

        [ObservableProperty]
        private DateTime _lastUpdateTime;

        // RCFファイルの内容
        public int InitStatus { get; set; } = 0;

        public RenderTaskPair(string rcfPath)
        {
            RcfFilePath = rcfPath;
            ProjectFolderPath = Path.GetDirectoryName(rcfPath) ?? string.Empty;
            
            // 例: "MyProject_RCF.txt" -> "MyProject"
            var fileName = Path.GetFileName(rcfPath);
            if (fileName.EndsWith("_RCF.txt", StringComparison.OrdinalIgnoreCase))
            {
                ProjectName = fileName.Substring(0, fileName.Length - 8);
            }
            else
            {
                ProjectName = Path.GetFileNameWithoutExtension(rcfPath);
            }

            AepFilePath = Path.Combine(ProjectFolderPath, ProjectName + ".aep");
            LastUpdateTime = File.Exists(rcfPath) ? File.GetLastWriteTime(rcfPath) : DateTime.Now;
        }

        partial void OnStatusChanged(RenderStatus value)
        {
            StatusText = value switch
            {
                RenderStatus.Pending => "未処理",
                RenderStatus.Processing => "処理中",
                RenderStatus.Completed => "完了",
                RenderStatus.Error => "エラー",
                _ => "不明"
            };
        }
    }
}
