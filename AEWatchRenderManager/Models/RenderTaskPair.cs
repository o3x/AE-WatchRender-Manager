using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace AEWatchRenderManager.Models
{
    // Date: Wed Mar 11 12:42:00 JST 2026
    // Version: 1.0.0
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
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private RenderStatus _status = RenderStatus.Pending;

        [ObservableProperty]
        private string _statusText = "未処理";

        [ObservableProperty]
        private string? _logFilePath;

        [ObservableProperty]
        private DateTime _lastUpdateTime;

        public RenderTaskPair(string aepPath)
        {
            FilePath = aepPath;
            FileName = Path.GetFileName(aepPath);
            LastUpdateTime = File.Exists(aepPath) ? File.GetLastWriteTime(aepPath) : DateTime.Now;
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
