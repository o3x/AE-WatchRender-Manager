using AEWatchRenderManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace AEWatchRenderManager.Services
{
    // Date: Sat Apr 25 07:42:46 JST 2026
    // Version: 1.16.20
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
                // @problem: ステータス確定後に early return していたため TryUpdateOutputPathAsync が呼ばれず、
                //           レンダリング完了後も OutputFolderPath が空のままになり「レンダリング先を表示」がグレーアウトしていた。
                // @solution: Completed/Failed/Suspended の各ブランチで TryUpdateOutputPathAsync を呼んでから return する。
                if (rcfContent.Contains("(Finished", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Completed;
                    if (string.IsNullOrEmpty(task.OutputFolderPath))
                        await TryUpdateOutputPathAsync(task);
                    return;
                }
                if (rcfContent.Contains("(Error", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Failed;
                    if (string.IsNullOrEmpty(task.OutputFolderPath))
                        await TryUpdateOutputPathAsync(task);
                    return;
                }
                if (rcfContent.Contains("(Suspended", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = RenderStatus.Suspended;
                    if (string.IsNullOrEmpty(task.OutputFolderPath))
                        await TryUpdateOutputPathAsync(task);
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
                    logContent.Contains("レンダリングが完了しました", StringComparison.OrdinalIgnoreCase) ||
                    logContent.Contains("収集されたファイルの数 :", StringComparison.OrdinalIgnoreCase))
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

                // 出力先パスの特定試行（完了時またはログがある場合）
                await TryUpdateOutputPathAsync(task);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[StatusAnalyzer] IO例外（レンダリング中と判定）: {ex.Message}");
                task.Status = RenderStatus.Rendering;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StatusAnalyzer] 予期しない例外: {ex}");
                task.Status = RenderStatus.Failed;
            }
        }

        /// <summary>
        /// パス文字列中の AE 未解決変数 [compName] を実際のコンポジション名に置換する。
        /// </summary>
        /// <remarks>
        /// @problem: AE の特定バージョンは item*.htm の出力パスに [compName] を
        ///           コンポジション名で置換せず literal のまま書き出すことがある。
        /// @solution: 同じ item*.htm の &lt;H3&gt; タグに
        ///            「レンダリングアイテムN, 「{コンポ名}」」の形式でコンポ名が
        ///            記録されているため、そこから取得して置換する。
        ///            日本語 AE の「」と英語 AE の "" に両対応する。
        /// </remarks>
        private static string ResolveCompName(string path, string htmlContent)
        {
            if (!path.Contains("[compName]", StringComparison.OrdinalIgnoreCase))
                return path;

            // @problem: <meta http-equiv="Content-Type" ...> の "Content-Type" が先にマッチしてしまう。
            // @solution: まず <H3> タグの内容だけを切り出し、その中で 「コンポ名」 または "CompName" を探す。
            var h3Match = Regex.Match(htmlContent, @"<H3[^>]*>\s*(.*?)\s*</H3>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!h3Match.Success) return path;

            var h3Content = h3Match.Groups[1].Value;
            var m = Regex.Match(h3Content, @"[「""]([^」""]{1,256})[」""]");
            if (!m.Success) return path;

            var compName = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(compName)) return path;

            return Regex.Replace(path, @"\[compName\]", compName, RegexOptions.IgnoreCase);
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
            catch (IOException ex) { Debug.WriteLine($"[FindReportFile] IO例外: {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { Debug.WriteLine($"[FindReportFile] アクセス拒否: {ex.Message}"); }
            return null;
        }

        private static async Task ParseReportFileAsync(RenderTaskPair task)
        {
            if (string.IsNullOrEmpty(task.HtmlLogFilePath)) return;
            try
            {
                using var fs = new FileStream(task.HtmlLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, System.Text.Encoding.GetEncoding("shift-jis"));
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
            catch (IOException ex) { Debug.WriteLine($"[ParseReportFile] IO例外: {ex.Message}"); }
            catch (Exception ex) { Debug.WriteLine($"[ParseReportFile] 予期しない例外: {ex}"); }
        }

        /// <summary>
        /// ({ProjectName}_00_Logs)/machines.htm を読み取り、参加 PC 名一覧を MachineNames に反映する。
        /// machines.htm は AE が生成する Shift-JIS ファイル。
        /// <!-- Insert Machines Start --> ～ <!-- Insert Machines End --> 間の &lt;A&gt; タグのテキストを抽出する。
        /// </summary>
        private static async Task ParseMachinesAsync(RenderTaskPair task)
        {
            try
            {
                var logsDir = Path.Combine(task.ProjectFolderPath, $"({task.ProjectName}_00_Logs)");
                if (!Directory.Exists(logsDir))
                {
                    var dirs = Directory.GetDirectories(task.ProjectFolderPath, "*_Logs)");
                    logsDir = dirs.FirstOrDefault() ?? string.Empty;
                }
                if (string.IsNullOrEmpty(logsDir) || !Directory.Exists(logsDir)) return;

                var machinesFile = Path.Combine(logsDir, "machines.htm");
                if (!File.Exists(machinesFile)) return;

                string content;
                using (var fs = new FileStream(machinesFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, System.Text.Encoding.GetEncoding("shift-jis")))
                {
                    content = await sr.ReadToEndAsync();
                }

                var section = Regex.Match(content,
                    @"<!-- Insert Machines Start -->(.*?)<!-- Insert Machines End -->",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!section.Success) return;

                var names = new System.Collections.Generic.List<string>();
                foreach (Match m in Regex.Matches(section.Groups[1].Value,
                    @"<A\b[^>]*>\s*(\S+)\s*</A>", RegexOptions.IgnoreCase))
                {
                    var name = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }

                var joined = string.Join(", ", names);
                Application.Current.Dispatcher.Invoke(() => task.MachineNames = joined);
            }
            catch (IOException ex) { Debug.WriteLine($"[ParseMachines] IO例外: {ex.Message}"); }
            catch (Exception ex) { Debug.WriteLine($"[ParseMachines] 予期しない例外: {ex}"); }
        }

        private static async Task TryUpdateOutputPathAsync(RenderTaskPair task)
        {
            await ParseMachinesAsync(task);

            try
            {
                // (Logs)フォルダ内の item*.htm を探す
                var logsDir = Path.Combine(task.ProjectFolderPath, $"({task.ProjectName}_00_Logs)");
                if (!Directory.Exists(logsDir))
                {
                    // フォルダ名に一致するものがない場合、(Logs)で終わるフォルダを探す
                    var dirs = Directory.GetDirectories(task.ProjectFolderPath, "*_Logs)");
                    logsDir = dirs.FirstOrDefault();
                }

                if (logsDir != null && Directory.Exists(logsDir))
                {
                    var itemFiles = Directory.GetFiles(logsDir, "item*.htm");
                    foreach (var itemFile in itemFiles)
                    {
                        using var fs = new FileStream(itemFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs, System.Text.Encoding.GetEncoding("shift-jis"));
                        var content = await sr.ReadToEndAsync();

                        // AE が生成する item*.htm には2種類のフォーマットがある。
                        //
                        // [Format A] <LI> 直後に完全パスが1行
                        //   <LI>
                        //   C:\tmp\コンポ 1\コンポ 1_[####].[fileExtension]
                        //   → outDir = C:\tmp\コンポ 1
                        //
                        // [Format B] <A>タグ内にベースパス、</A>後にサブパス＋ファイル名
                        //   <LI>
                        //   <A TARGET="_THE_ITEM" HREF="...">
                        //   D:\AAA\_render
                        //   </A>
                        //   コンポ 1\コンポ 1_[####].[fileextension]
                        //   → outDir = D:\AAA\_render\コンポ 1
                        //
                        // Format B を先に試み、マッチしなければ Format A へフォールバックする。

                        string? outputDir = null;

                        // --- Format B ---
                        var aMatch = Regex.Match(content,
                            @"<A\b[^>]*>\s*([A-Za-z]:\\[^\r\n<]+?)\s*</A>\s*([^\r\n<]*)",
                            RegexOptions.IgnoreCase);
                        if (aMatch.Success)
                        {
                            var basePath = aMatch.Groups[1].Value.Trim();
                            var subRaw   = ResolveCompName(aMatch.Groups[2].Value.Trim(), content);
                            if (!string.IsNullOrEmpty(subRaw))
                            {
                                var subDir = Path.GetDirectoryName(subRaw);
                                outputDir = string.IsNullOrEmpty(subDir)
                                    ? basePath
                                    : Path.Combine(basePath, subDir);
                            }
                            else
                            {
                                outputDir = basePath;
                            }
                        }

                        // --- Format A (フォールバック) ---
                        if (outputDir == null)
                        {
                            var liMatch = Regex.Match(content,
                                @"<LI>\s*([A-Za-z]:\\[^\r\n<]+)",
                                RegexOptions.IgnoreCase);
                            if (liMatch.Success)
                            {
                                var fullPath = ResolveCompName(liMatch.Groups[1].Value.Trim(), content);
                                outputDir = Path.GetDirectoryName(fullPath);
                            }
                        }

                        if (outputDir == null) continue;

                        // @problem: Directory.Exists でパスの実在を確認していたため、
                        //           ネットワークドライブや別マシンへの出力パス等、
                        //           現在アクセスできないパスがすべてスキップされていた。
                        // @solution: OutputFolderPath はあくまで「どこに出力されたか」の記録。
                        //            絶対パスであることだけ確認し、実在確認は行わない。
                        //            フォルダを開く操作（OpenDisplayPath/ShowRenderDestination）側で
                        //            Directory.Exists を確認してユーザーに通知する。
                        if (!Path.IsPathFullyQualified(outputDir))
                        {
                            Debug.WriteLine($"[TryUpdateOutputPath] 相対パスを拒否: {outputDir}");
                            continue;
                        }
                        Application.Current.Dispatcher.Invoke(() => task.OutputFolderPath = outputDir);
                        return; // ひとつ見つかれば終了
                    }
                }
            }
            catch (IOException ex) { Debug.WriteLine($"[TryUpdateOutputPath] IO例外: {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { Debug.WriteLine($"[TryUpdateOutputPath] アクセス拒否: {ex.Message}"); }
            catch (Exception ex) { Debug.WriteLine($"[TryUpdateOutputPath] 予期しない例外: {ex}"); }
        }
    }
}
