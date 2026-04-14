using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace AEWatchRenderManager;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;

    // @problem: 二重起動時にアラートを出すだけでは UX が悪い
    // @solution: Win32 API で既存プロセスのウィンドウをアクティブ化し、
    //            自身はサイレントに終了する
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SwRestore = 9;

    protected override void OnStartup(StartupEventArgs e)
    {
        // @problem: .NET 8 では shift-jis 等の追加エンコーディングがデフォルト無効
        // @solution: アプリ起動時に一度だけ CodePagesEncodingProvider を登録する
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        const string mutexName = "AEWatchRenderManager_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            ActivateExistingInstance();
            Application.Current.Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    /// <summary>
    /// 既に起動しているインスタンスのウィンドウをフォアグラウンドに移す。
    /// 最小化されている場合は復元してからアクティブ化する。
    /// </summary>
    private static void ActivateExistingInstance()
    {
        var current = Process.GetCurrentProcess();

        // @problem: プロセス名だけで照合すると、同名の別プログラムに
        //           ウィンドウを乗っ取られる恐れがある（スプーフィング）。
        // @solution: 実行ファイルのフルパスが一致するプロセスのみ対象にする。
        //            MainModule アクセスは権限不足で失敗することがあるため try-catch で保護。
        string? currentPath = null;
        try { currentPath = current.MainModule?.FileName; }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"MainModule access failed: {ex.Message}"); }

        var existing = Process.GetProcessesByName(current.ProcessName)
            .FirstOrDefault(p =>
            {
                if (p.Id == current.Id) return false;
                if (currentPath == null) return true; // パス取得不可の場合は名前一致で代用
                try
                {
                    return string.Equals(p.MainModule?.FileName, currentPath,
                        StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            });

        if (existing == null) return;

        IntPtr hWnd = existing.MainWindowHandle;
        if (hWnd == IntPtr.Zero) return;

        ShowWindow(hWnd, SwRestore);
        SetForegroundWindow(hWnd);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mutex != null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
