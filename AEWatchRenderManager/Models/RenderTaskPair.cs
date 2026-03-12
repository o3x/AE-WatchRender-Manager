using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace AEWatchRenderManager.Models
{
    // Date: Thu Mar 12 10:10:00 JST 2026
    // Version: 1.5.0
    public enum RenderStatus
    {
        Queued,    // 待機中
        Rendering, // レンダリング中
        Completed, // 完了
        Failed,    // エラー
        Suspended, // 一時停止中
        Pending    // 保留中
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
        private RenderStatus _status = RenderStatus.Queued;

        [ObservableProperty]
        private string _statusText = "Queued";

        [ObservableProperty]
        private string _rowBackgroundColor = "White";

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
                RenderStatus.Queued => "Queued",
                RenderStatus.Rendering => "Rendering",
                RenderStatus.Completed => "Completed",
                RenderStatus.Failed => "Failed",
                RenderStatus.Suspended => "Suspended",
                RenderStatus.Pending => "Pending",
                _ => "Unknown"
            };

            RowBackgroundColor = value switch
            {
                RenderStatus.Queued => "White",
                RenderStatus.Rendering => "LightGreen",
                RenderStatus.Completed => "LightBlue",
                RenderStatus.Failed => "LightPink",
                RenderStatus.Suspended => "LightGray",
                RenderStatus.Pending => "Moccasin",
                _ => "White"
            };
        }
    }
}
