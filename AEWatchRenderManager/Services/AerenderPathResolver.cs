// AerenderPathResolver.cs
// Version: 2.0.0
// Updated: Sat Apr 18 19:06:22 JST 2026

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AEWatchRenderManager.Services
{
    /// <summary>
    /// aerender.exe のパス解決と AEP バイナリヘッダー解析を担う静的ヘルパー。
    /// AEselector プロジェクトと同じバージョン判定ロジックを共有する。
    /// MainViewModel と WatchFolderParticipant の両方から利用される。
    /// </summary>
    internal static class AerenderPathResolver
    {
        private static readonly string AdobeDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Adobe");

        /// <summary>
        /// AEP メジャーバージョンに対応する aerender.exe のパスを返す。
        /// 該当バージョンがインストールされていない場合は null。
        /// </summary>
        public static string? FindForVersion(int majorVersion)
        {
            if (!Directory.Exists(AdobeDir)) return null;

            var folderName = majorVersion switch
            {
                >= 22 => $"Adobe After Effects {2000 + majorVersion}",
                >= 17 => $"Adobe After Effects {2003 + majorVersion}",
                >= 14 => $"Adobe After Effects CC {2003 + majorVersion}",
                11    => "Adobe After Effects CS6",
                10    => "Adobe After Effects CS5",
                9     => "Adobe After Effects CS4",
                _     => null
            };
            if (folderName == null) return null;

            var aerender = Path.Combine(AdobeDir, folderName, "Support Files", "aerender.exe");
            return File.Exists(aerender) ? aerender : null;
        }

        /// <summary>
        /// インストール済みの最新 aerender.exe のパスを返す。
        /// 見つからない場合は null。
        /// </summary>
        public static string? FindNewest()
        {
            if (!Directory.Exists(AdobeDir)) return null;

            return Directory.GetDirectories(AdobeDir, "Adobe After Effects*")
                .OrderByDescending(d => d)
                .Select(d => Path.Combine(d, "Support Files", "aerender.exe"))
                .FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// AEP バイナリヘッダーを解析して AE メジャーバージョン番号を返す。
        /// AEselector の GetAeVersionFromFile と同じロジック。失敗時は 0 を返す。
        /// </summary>
        public static int ReadAepMajorVersion(string aepPath)
        {
            try
            {
                // AE が AEP を開いている状態でも読めるよう FileShare.ReadWrite を使う
                using var fs = new FileStream(aepPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var br = new BinaryReader(fs);
                var b = br.ReadBytes(48);
                if (b.Length < 48) return 0;

                // マジックナンバー確認: RIFF or RIFX + "Egg!" (offset 8)
                bool isRiff = b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46;
                bool isRifx = b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x58;
                bool isEgg  = b[8] == 0x45 && b[9] == 0x67 && b[10] == 0x67 && b[11] == 0x21;
                if ((!isRiff && !isRifx) || !isEgg) return 0;

                // CS6以降: offset 0x18 == 0x68、バージョンは offset 0x24 から
                // CS5以前: バージョンは offset 0x18 から
                return b[0x18] == 0x68
                    ? ((b[0x24] << 1) & 0xF8) | ((b[0x25] >> 3) & 0x07)
                    : ((b[0x18] << 1) & 0xF8) | ((b[0x19] >> 3) & 0x07);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AerenderPathResolver.ReadAepMajorVersion] {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// aerender.exe を -version で起動してメジャーバージョン番号を取得する。
        /// フォールバック時のバージョン不一致確認にのみ使用。失敗時は 0 を返す。
        /// </summary>
        public static int GetMajorVersion(string aerenderPath)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = aerenderPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                })!;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);

                var m = Regex.Match(output, @"aerender version (\d+)\.");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int ver))
                    return ver;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AerenderPathResolver.GetMajorVersion] {ex.Message}");
            }
            return 0;
        }
    }
}
