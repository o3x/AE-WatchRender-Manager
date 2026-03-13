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

    // Date: Fri Mar 13 11:55:00 JST 2026
    // Version: 1.7.0
    public partial class RenderTaskPair : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private string _projectFolderPath = string.Empty;

        [ObservableProperty]
        private string _rcfFilePath = string.Empty;

        [ObservableProperty]
        private string _aepFilePath = string.Empty;

        [ObservableProperty]
        private string _outputFolderPath = string.Empty;

        [ObservableProperty]
        private string? _htmlLogFilePath;

        [ObservableProperty]
        private RenderStatus _status = RenderStatus.Queued;

        [ObservableProperty]
        private string _statusText = "Queued";

        [ObservableProperty]
        private string _rowForegroundColor = "Black";

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

            // AEPパスの決定ロジック強化
            var exactAep = Path.Combine(ProjectFolderPath, ProjectName + ".aep");
            if (File.Exists(exactAep))
            {
                AepFilePath = exactAep;
            }
            else
            {
                // フォールバック: フォルダ内の唯一のAEP、または最初に見つかったAEP
                try
                {
                    var aepFiles = Directory.GetFiles(ProjectFolderPath, "*.aep");
                    AepFilePath = aepFiles.FirstOrDefault() ?? exactAep;
                }
                catch
                {
                    AepFilePath = exactAep;
                }
            }
            
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

            RowForegroundColor = value switch
            {
                RenderStatus.Queued => "Black",
                RenderStatus.Rendering => "Green",
                RenderStatus.Completed => "Blue",
                RenderStatus.Failed => "Red",
                RenderStatus.Suspended => "Gray",
                RenderStatus.Pending => "DarkOrange",
                _ => "Black"
            };
        }
    }
}
