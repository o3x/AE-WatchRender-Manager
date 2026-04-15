using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics;
using System.IO;

namespace AEWatchRenderManager.Models
{
    // Date: Wed Apr 15 11:02:12 JST 2026
    // Version: 1.16.12
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

        /// <summary>レンダリング出力先が判明しているか（コンテキストメニューの IsEnabled バインド用）</summary>
        public bool HasOutputPath => !string.IsNullOrEmpty(OutputFolderPath);

        /// <summary>
        /// パス列の表示値。
        /// レンダリング出力先が判明している場合はそちらを優先し、未判明時はプロジェクトフォルダにフォールバックする。
        /// </summary>
        public string DisplayPath => string.IsNullOrEmpty(OutputFolderPath)
            ? ProjectFolderPath
            : OutputFolderPath;

        /// <summary>パス列のツールチップ。表示中のパスが何を指すかを示す。</summary>
        public string DisplayPathTooltip => HasOutputPath
            ? $"レンダリング出力先:\n{OutputFolderPath}"
            : $"プロジェクトフォルダ（出力先未確定）:\n{ProjectFolderPath}";

        partial void OnOutputFolderPathChanged(string value)
        {
            OnPropertyChanged(nameof(HasOutputPath));
            OnPropertyChanged(nameof(DisplayPath));
            OnPropertyChanged(nameof(DisplayPathTooltip));
        }

        [ObservableProperty]
        private string? _htmlLogFilePath;

        [ObservableProperty]
        private RenderStatus _status = RenderStatus.Queued;

        [ObservableProperty]
        private string _statusText = "Queued";

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
                catch (IOException ex)
                {
                    Debug.WriteLine($"[RenderTaskPair] AEP検索IO例外: {ex.Message}");
                    AepFilePath = exactAep;
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.WriteLine($"[RenderTaskPair] AEP検索アクセス拒否: {ex.Message}");
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
        }
    }
}
