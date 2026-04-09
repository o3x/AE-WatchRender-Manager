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

    protected override void OnStartup(StartupEventArgs e)
    {
        // @problem: .NET 8 では shift-jis 等の追加エンコーディングがデフォルト無効
        // @solution: アプリ起動時に一度だけ CodePagesEncodingProvider を登録する
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        const string mutexName = "AEWatchRenderManager_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show("AE WatchRender Manager は既に起動しています。", "二重起動防止", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
            return;
        }

        base.OnStartup(e);
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

