using AEWatchRenderManager.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AEWatchRenderManager.Services
{
    // Date: Wed Mar 11 12:45:00 JST 2026
    // Version: 1.0.0
    public static class StatusAnalyzer
    {
        public static async Task AnalyzeAsync(RenderTaskPair task)
        {
            if (string.IsNullOrEmpty(task.LogFilePath) || !File.Exists(task.LogFilePath))
            {
                task.Status = RenderStatus.Pending;
                return;
            }

            try
            {
                // AEがレンダリング中で書き込みロックされている場合でも読めるよう FileShare.ReadWrite を指定
                using var fs = new FileStream(task.LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var content = await sr.ReadToEndAsync();

                // 完了判定 ("Finished rendering" または RCFの "終了しました")
                if (content.Contains("Finished rendering", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("終了しました", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("Rendering completed", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Completed;
                }
                else if (content.Contains("エラー", StringComparison.OrdinalIgnoreCase) || 
                         content.Contains("Error", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Error;
                }
                else
                {
                    task.Status = RenderStatus.Processing;
                }
            }
            catch (IOException)
            {
                // ロックされて読み込めない場合はレンダリング中と判定
                task.Status = RenderStatus.Processing;
            }
            catch (Exception)
            {
                task.Status = RenderStatus.Error;
            }
        }
    }
}
