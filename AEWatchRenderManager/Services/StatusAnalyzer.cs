using AEWatchRenderManager.Models;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

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
                task.Status = RenderStatus.Queued;
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
                if (htmlNameMatch.Success && !string.IsNullOrWhiteSpace(htmlNameMatch.Groups[1].Value))
                {
                    var logName = htmlNameMatch.Groups[1].Value;
                    task.HtmlLogFilePath = Path.Combine(task.ProjectFolderPath, logName);
                }
                else
                {
                    // html_name="" の場合はレポートファイル(*_レポート.txt など)を探す
                    task.HtmlLogFilePath = FindReportFile(task.ProjectFolderPath, task.ProjectName);
                }

                // text/html レポートからプロジェクト名などをパースする (html_name=""対策)
                if (!string.IsNullOrEmpty(task.HtmlLogFilePath) && File.Exists(task.HtmlLogFilePath))
                {
                    await ParseReportFileAsync(task);
                }

                // 2. 最確なステータス判定（_RCF.txt の内容を最優先）
                if (rcfContent.Contains("(Finished", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Completed;
                    return;
                }
                if (rcfContent.Contains("(Error", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Failed;
                    return;
                }
                if (rcfContent.Contains("(Suspended", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Suspended;
                    return;
                }
                if (rcfContent.Contains("(Pending", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Pending;
                    return;
                }
                if (rcfContent.Contains("(Queued", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Queued;
                    return;
                }

                // 3. その他、レガシー・ハイブリッド判定
                // init=0 の場合は待機中(Queued)
                if (task.InitStatus == 0)
                {
                    task.Status = RenderStatus.Queued;
                    return;
                }

                // init=1 だがログファイルがない場合は、処理開始（レンダリング中）と判定
                if (string.IsNullOrEmpty(task.HtmlLogFilePath) || !File.Exists(task.HtmlLogFilePath))
                {
                    task.Status = RenderStatus.Rendering;
                    return;
                }

                // ログファイルの中身をパースして詳細ステータス判定
                string logContent = string.Empty;
                try
                {
                    using var lfs = new FileStream(task.HtmlLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var lsr = new StreamReader(lfs);
                    logContent = await lsr.ReadToEndAsync();
                }
                catch (IOException)
                {
                    // ログがロック中の場合はレンダリング中
                    task.Status = RenderStatus.Rendering;
                    return;
                }

                if (logContent.Contains("Finished rendering", StringComparison.OrdinalIgnoreCase) ||
                    logContent.Contains("終了しました", StringComparison.OrdinalIgnoreCase) ||
                    logContent.Contains("Rendering completed", StringComparison.OrdinalIgnoreCase) || 
                    logContent.Contains("収集されたファイルの数 :", StringComparison.OrdinalIgnoreCase)) // 収集完了等も完了とするか要検討
                {
                    task.Status = RenderStatus.Completed;
                }
                else if (logContent.Contains("エラー", StringComparison.OrdinalIgnoreCase) || 
                         logContent.Contains("Error", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Failed;
                }
                else
                {
                    // ログはあるが完了やエラー表記がない場合は処理中
                    task.Status = RenderStatus.Rendering;
                }
            }
            catch (IOException)
            {
                task.Status = RenderStatus.Rendering;
            }
            catch (Exception)
            {
                task.Status = RenderStatus.Failed;
            }
        }

        private static string? FindReportFile(string dir, string projectName)
        {
            try
            {
                // *_レポート.txt を優先的に探す
                var files = Directory.GetFiles(dir, "*_レポート.txt");
                if (files.Length > 0) return files[0];

                // それ以外で .txt または .htm があればそれを使う
                var anyTxt = Directory.GetFiles(dir, "*.txt").Where(f => !f.EndsWith("_RCF.txt", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (anyTxt != null) return anyTxt;
            }
            catch { }
            return null;
        }

        private static async Task ParseReportFileAsync(RenderTaskPair task)
        {
            if (string.IsNullOrEmpty(task.HtmlLogFilePath)) return;
            try
            {
                using var fs = new FileStream(task.HtmlLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var content = await sr.ReadToEndAsync();

                // "プロジェクト名 : aaaaaa.aep" の解析 と更新
                var projMatch = Regex.Match(content, @"プロジェクト名\s*:\s*(.+)\.aep", RegexOptions.IgnoreCase);
                if (projMatch.Success)
                {
                    var parsedName = projMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(parsedName))
                    {
                        // UI表示上で、プロジェクト名が RCF ファイル名と異なる場合は更新する
                        Application.Current.Dispatcher.Invoke(() => task.ProjectName = parsedName);
                    }
                }
            }
            catch { }
        }
    }
}
