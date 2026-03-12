using AEWatchRenderManager.Models;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AEWatchRenderManager.Services
{
    // Date: Thu Mar 12 10:13:00 JST 2026
    // Version: 1.5.0
    public static class StatusAnalyzer
    {
        public static async Task AnalyzeAsync(RenderTaskPair task)
        {
            if (string.IsNullOrEmpty(task.RcfFilePath) || !File.Exists(task.RcfFilePath))
            {
                task.Status = RenderStatus.Pending;
                return;
            }

            try
            {
                // 1. RCFファイルをパース
                string rcfContent = string.Empty;
                using (var fs = new FileStream(task.RcfFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    rcfContent = await sr.ReadToEndAsync();
                }

                // initフラグの読み取り
                var initMatch = Regex.Match(rcfContent, @"init=(\d+)");
                if (initMatch.Success && int.TryParse(initMatch.Groups[1].Value, out int initVal))
                {
                    task.InitStatus = initVal;
                }

                // html_nameの読み取り
                var htmlNameMatch = Regex.Match(rcfContent, @"html_name=""([^""]+)""");
                if (htmlNameMatch.Success)
                {
                    var logName = htmlNameMatch.Groups[1].Value;
                    task.HtmlLogFilePath = Path.Combine(task.ProjectFolderPath, logName);
                }

                // 2. ステータス判定（ハイブリッド）
                // init=0 の場合は未処理
                if (task.InitStatus == 0)
                {
                    task.Status = RenderStatus.Pending;
                    return;
                }

                // init=1 だがログファイルがない場合は、処理開始（処理中）と判定
                if (string.IsNullOrEmpty(task.HtmlLogFilePath) || !File.Exists(task.HtmlLogFilePath))
                {
                    task.Status = RenderStatus.Processing;
                    return;
                }

                // ログファイルの中身をパースして詳細判定
                string htmlContent = string.Empty;
                try
                {
                    using var fs = new FileStream(task.HtmlLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    htmlContent = await sr.ReadToEndAsync();
                }
                catch (IOException)
                {
                    // ログがロック中の場合はレンダリング中
                    task.Status = RenderStatus.Processing;
                    return;
                }

                if (htmlContent.Contains("Finished rendering", StringComparison.OrdinalIgnoreCase) ||
                    htmlContent.Contains("終了しました", StringComparison.OrdinalIgnoreCase) ||
                    htmlContent.Contains("Rendering completed", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Completed;
                }
                else if (htmlContent.Contains("エラー", StringComparison.OrdinalIgnoreCase) || 
                         htmlContent.Contains("Error", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Error;
                }
                else
                {
                    // htmlはあるが完了やエラー表記がない場合は処理中
                    task.Status = RenderStatus.Processing;
                }
            }
            catch (IOException)
            {
                // RCF自体をロック中の場合
                task.Status = RenderStatus.Processing;
            }
            catch (Exception)
            {
                task.Status = RenderStatus.Error;
            }
        }
    }
}
