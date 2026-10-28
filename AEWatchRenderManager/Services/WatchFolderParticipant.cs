// WatchFolderParticipant.cs
// Version: 2.0.5
// Updated: Tue Oct 27 17:42:09 JST 2026

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AEWatchRenderManager.Services
{
    /// <summary>
    /// 監視フォルダ内のキュー済み RCF を検出し、aerender を使って自動レンダリングするワーカー。
    /// Start/Stop でバックグラウンドループを制御する。
    /// </summary>
    public class WatchFolderParticipant
    {
        /// <summary>ステータス文字列が変化したときに通知される。文字列は UI スレッド以外から発火する。</summary>
        public event Action<string>? StatusChanged;

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        private CancellationTokenSource? _cts;
        private static readonly string MachineName = Environment.MachineName;
        private static readonly Regex InitPattern = new(@"init=(\d+)", RegexOptions.Compiled);

        // ロックファイルがこの時間より古ければ停止したマシンのロックとみなして無視する
        private const int StaleLockMinutes = 30;

        public void Start(string monitorPath, string? userAerenderPath, int pollIntervalSeconds = 10)
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _ = Task.Run(() => RunLoopAsync(monitorPath, userAerenderPath, pollIntervalSeconds, ct), ct);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        // ─────────────────────────────────────────────────────────
        // メインループ
        // ─────────────────────────────────────────────────────────

        private async Task RunLoopAsync(
            string monitorPath, string? userAerenderPath, int pollIntervalSeconds, CancellationToken ct)
        {
            ReportStatus("待機中...");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var rcfPath = FindQueuedRcf(monitorPath);
                    if (rcfPath != null)
                    {
                        await TryClaimAndRenderAsync(rcfPath, userAerenderPath, ct);
                    }
                    else
                    {
                        ReportStatus($"待機中 — 次回スキャン {DateTime.Now.AddSeconds(pollIntervalSeconds):HH:mm:ss}");
                        await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WatchFolderParticipant] ループ例外: {ex.Message}");
                    try { await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), ct); }
                    catch (OperationCanceledException) { break; }
                }
            }
            ReportStatus("停止");
        }

        // ─────────────────────────────────────────────────────────
        // キュー済み RCF の検索
        // ─────────────────────────────────────────────────────────

        private static string? FindQueuedRcf(string monitorPath)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(monitorPath))
                {
                    foreach (var rcf in Directory.GetFiles(dir, "*_RCF.txt"))
                    {
                        if (IsLocked(dir)) continue;
                        var content = ReadRcfContent(rcf);
                        if (content != null && IsQueued(content)) return rcf;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WatchFolderParticipant.FindQueuedRcf] {ex.Message}");
            }
            return null;
        }

        /// <summary>RCF が Queued 状態かどうかを判定する。StatusAnalyzer と同じ優先順位。</summary>
        private static bool IsQueued(string content)
        {
            if (content.Contains("(Finished",  StringComparison.OrdinalIgnoreCase)) return false;
            if (content.Contains("(Error",     StringComparison.OrdinalIgnoreCase)) return false;
            if (content.Contains("(Suspended", StringComparison.OrdinalIgnoreCase)) return false;
            if (content.Contains("(Pending",   StringComparison.OrdinalIgnoreCase)) return false;
            if (content.Contains("(Rendering", StringComparison.OrdinalIgnoreCase)) return false;

            var initMatch = InitPattern.Match(content);
            if (initMatch.Success && initMatch.Groups[1].Value == "0") return true;
            if (content.Contains("(Queued", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ─────────────────────────────────────────────────────────
        // ロックファイル管理
        // ─────────────────────────────────────────────────────────

        private static bool IsLocked(string projectDir)
        {
            try
            {
                foreach (var lf in Directory.GetFiles(projectDir, "*_RCF.lock"))
                {
                    var age = DateTime.Now - File.GetLastWriteTime(lf);
                    if (age.TotalMinutes < StaleLockMinutes) return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WatchFolderParticipant.IsLocked] {ex.Message}");
            }
            return false;
        }

        /// <summary>自分より優先される（マシン名が辞書順で小さい）別マシンの有効なロックがあるか。</summary>
        private static bool HasPriorityRival(string projectDir, string ownLockFile)
        {
            try
            {
                foreach (var lf in Directory.GetFiles(projectDir, "*_RCF.lock"))
                {
                    if (string.Equals(lf, ownLockFile, StringComparison.OrdinalIgnoreCase)) continue;
                    if ((DateTime.Now - File.GetLastWriteTime(lf)).TotalMinutes >= StaleLockMinutes) continue;

                    // マシン名にも「_」を使えるため、ファイル名ではなくロック本文の1行目から読む
                    var rivalName = (ReadRcfContent(lf) ?? string.Empty).Split('\n')[0].Trim();
                    if (rivalName.Length > 0 &&
                        string.Compare(rivalName, MachineName, StringComparison.OrdinalIgnoreCase) < 0) return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WatchFolderParticipant.HasPriorityRival] {ex.Message}");
            }
            return false;
        }

        private static string GetLockFilePath(string rcfPath)
        {
            var dir = Path.GetDirectoryName(rcfPath)!;
            var name = GetProjectName(rcfPath);
            return Path.Combine(dir, $"{MachineName}_{name}_RCF.lock");
        }

        private static string GetProjectName(string rcfPath)
        {
            var fn = Path.GetFileName(rcfPath);
            return fn.EndsWith("_RCF.txt", StringComparison.OrdinalIgnoreCase)
                ? fn[..^8]
                : Path.GetFileNameWithoutExtension(rcfPath);
        }

        // ─────────────────────────────────────────────────────────
        // クレーム & レンダリング
        // ─────────────────────────────────────────────────────────

        private async Task TryClaimAndRenderAsync(
            string rcfPath, string? userAerenderPath, CancellationToken ct)
        {
            var projectName = GetProjectName(rcfPath);
            var projectDir  = Path.GetDirectoryName(rcfPath)!;
            // GetLockFilePath も内部で GetProjectName を呼ぶため、計算済みの名前で直接組み立てる
            var lockFile    = Path.Combine(projectDir, $"{MachineName}_{projectName}_RCF.lock");

            // ロックファイルを作成してクレームを宣言
            try
            {
                File.WriteAllText(lockFile, $"{MachineName}\r\n{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WatchFolderParticipant] ロックファイル作成失敗 ({projectName}): {ex.Message}");
                return;
            }

            try
            {
                // ロック後に少し待って競合チェック（別プロセスが同時にロックを作った場合の猶予）
                await Task.Delay(200, ct);

                // @problem: ロックファイル名にマシン名が入るため、CreateNew では別マシンとの同時取得を
                //           排他できず、2台が同時に Queued を見て両方が走り出す恐れがある。
                // @solution: 待機後に他マシンの新しいロックを探し、マシン名が辞書順で小さい側を勝者にする
                //            （全マシンが同じ規則で判断するので、必ず1台だけが残る）。
                if (HasPriorityRival(projectDir, lockFile))
                {
                    ReportStatus($"スキップ（別マシンが先に取得）: {projectName}");
                    return;
                }

                var content = ReadRcfContent(rcfPath);
                if (content == null || !IsQueued(content))
                {
                    ReportStatus($"スキップ（別マシンが先に取得）: {projectName}");
                    return;
                }

                // AEP パスを特定
                var aepPath = Path.Combine(projectDir, projectName + ".aep");
                if (!File.Exists(aepPath))
                {
                    // 名前が一致しない場合は AEP が1つだけのときに限って採用する（複数あると別のファイルを実行しかねない）
                    var candidates = Directory.GetFiles(projectDir, "*.aep");
                    if (candidates.Length == 1) aepPath = candidates[0];
                }
                if (!File.Exists(aepPath))
                {
                    ReportStatus($"エラー: AEP ファイルが見つかりません ({projectName})");
                    return;
                }

                // aerender のパス解決（AEP バージョン一致 → ユーザー設定 → 最新インストール済みの順）
                var aepMajor = AerenderPathResolver.ReadAepMajorVersion(aepPath);
                var aerenderPath =
                    (aepMajor > 0 ? AerenderPathResolver.FindForVersion(aepMajor) : null)
                    ?? (AerenderPathResolver.IsAerenderExe(userAerenderPath) ? userAerenderPath : null)
                    ?? AerenderPathResolver.FindNewest();

                if (aerenderPath == null)
                {
                    ReportStatus("エラー: aerender.exe が見つかりません。設定で指定してください。");
                    return;
                }

                // RCF を Rendering 状態に更新
                var renderingLine = $"machine0=(Rendering {DateTime.Now:HH:mm:ss}) {MachineName} (1/1)";
                UpdateRcfStatus(rcfPath, content, renderingLine, claimInit: true);
                ReportStatus($"レンダリング中: {projectName}");

                // aerender 実行（最小化で起動）
                // @problem: /K（ウィンドウを残す）で起動すると WaitForExitAsync がユーザーの
                //           手動クローズまで返らずワーカーが機能停止する。手動クローズ時の cmd
                //           の ExitCode（通常0）で成否判定するため aerender の失敗も見逃す。
                // @solution: ワーカー自動参加では常に /C で起動する。/K は右クリック手動実行
                //            （fire-and-forget、MainViewModel.RenderWithAerender）専用とする。
                bool success = await RunAerenderAsync(aerenderPath, aepPath, ct);

                // RCF を完了/エラー状態に更新
                var finalContent = ReadRcfContent(rcfPath) ?? content;
                if (success)
                {
                    var finLine = $"machine0=(Finished {DateTime.Now:HH:mm:ss}) {MachineName} (1/1)";
                    UpdateRcfStatus(rcfPath, finalContent, finLine, claimInit: false);
                    ReportStatus($"完了: {projectName}");
                }
                else
                {
                    var errLine = $"machine0=(Error {DateTime.Now:HH:mm:ss}) {MachineName} (1/1)";
                    UpdateRcfStatus(rcfPath, finalContent, errLine, claimInit: false);
                    ReportStatus($"エラー: {projectName}");
                }
            }
            finally
            {
                try { File.Delete(lockFile); } catch { }
            }
        }

        // ─────────────────────────────────────────────────────────
        // aerender 実行
        // ─────────────────────────────────────────────────────────

        private static async Task<bool> RunAerenderAsync(
            string aerenderPath, string aepPath, CancellationToken ct)
        {
            // 最小化で起動して作業の邪魔にならないようにする。
            // @problem: cmd.exe 経由だと、ファイル名の %VAR% がクォート内でも展開される。
            // @solution: cmd を介さず aerender.exe を直接起動する（ウィンドウは完了後に自動で閉じる）。
            //            UseShellExecute=true は WindowStyle=Minimized を効かせるためで、cmd は通らない。
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = aerenderPath,
                    Arguments = $"-project \"{aepPath}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                }
            };

            try
            {
                proc.Start();
                try
                {
                    await proc.WaitForExitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    // cmd.exe だけでなく子プロセス（aerender 本体）も含めて終了させる
                    if (!proc.HasExited) try { proc.Kill(entireProcessTree: true); } catch { }
                    throw;
                }
                return proc.ExitCode == 0;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WatchFolderParticipant.RunAerender] {ex.Message}");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────
        // RCF ファイル操作
        // ─────────────────────────────────────────────────────────

        private static string? ReadRcfContent(string rcfPath)
        {
            try
            {
                using var fs = new FileStream(rcfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WatchFolderParticipant.ReadRcfContent] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// RCF ファイルに machineStatusLine を書き込む。
        /// claimInit=true のとき init=0 → 1、num_machines を 1 に更新する。
        /// 既存の machine0= 行は置き換える。
        /// </summary>
        private static void UpdateRcfStatus(
            string rcfPath, string currentContent, string machineStatusLine, bool claimInit)
        {
            try
            {
                var lines = currentContent
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .ToList();

                // 既存の machineN= 行を削除
                lines.RemoveAll(l => Regex.IsMatch(l, @"^machine\d+=", RegexOptions.IgnoreCase));

                if (claimInit)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].StartsWith("init=0",        StringComparison.Ordinal))
                            lines[i] = "init=1";
                        else if (lines[i].StartsWith("num_machines=", StringComparison.Ordinal))
                            lines[i] = "num_machines=1";
                    }
                }

                // 末尾の空行を除去してから新しい machine 行を追加
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                    lines.RemoveAt(lines.Count - 1);
                lines.Add(machineStatusLine);

                File.WriteAllText(rcfPath, string.Join("\r\n", lines) + "\r\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WatchFolderParticipant.UpdateRcfStatus] {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────
        // ユーティリティ
        // ─────────────────────────────────────────────────────────

        private void ReportStatus(string status)
        {
            Debug.WriteLine($"[WatchFolderParticipant] {status}");
            StatusChanged?.Invoke(status);
        }
    }
}
