using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Ytec.WifiCapsule.App;

public partial class App : System.Windows.Application
{
#if !(UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW)
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
#endif
#if UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW
    private bool _captureStarted;
#endif

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        UiLanguage.Initialize(e.Args);
        var testMode = e.Args.Any(
            argument => argument.Equals(
                "--test-mode",
                StringComparison.OrdinalIgnoreCase));
#if UI_TEST_BUILD
        testMode = true;
#endif
#if UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW
        testMode = true;
#endif
#if UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW
        var createdNew = true;
#else
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\YtecWifiCapsule.SingleInstance",
            createdNew: out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
#endif
        if (!createdNew)
        {
            MessageBox.Show(
                UiLanguage.Text("AppAlreadyRunning"),
                "Y-TEC Wi-Fi Capsule",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += HandleUnhandledException;
        var window = new MainWindow(testMode);
#if UI_TEST_BUILD
        var screens = Forms.Screen.AllScreens;
        if (screens.Length >= 3)
        {
            var area = screens[2].WorkingArea;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = area.Left + Math.Max(0, (area.Width - window.Width) / 2);
            window.Top = area.Top + Math.Max(0, (area.Height - window.Height) / 2);
        }
#endif
#if UI_TEST_CAPTURE_WIDE
        window.Width = 1120;
        window.Height = 720;
        window.ContentRendered += CaptureWideScreenshots;
#elif UI_TEST_CAPTURE_NARROW
        window.Width = 375;
        window.Height = 720;
        window.ContentRendered += CaptureNarrowScreenshot;
#endif
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
#if !(UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW)
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
#endif
        base.OnExit(e);
    }

    private static void HandleUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
#if UI_TEST_BUILD
        File.WriteAllText(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ui-test-error.txt"),
            e.Exception.ToString());
#endif
        MessageBox.Show(
            UiLanguage.Text("UnexpectedError"),
            "Y-TEC Wi-Fi Capsule",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Current.Shutdown(1);
    }

#if UI_TEST_CAPTURE_WIDE
    private async void CaptureWideScreenshots(
        object? sender,
        EventArgs e)
    {
        if (_captureStarted || sender is not MainWindow window)
        {
            return;
        }

        _captureStarted = true;
        try
        {
            await Task.Delay(1500);
            var captureDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "captures",
                UiLanguage.Code);
            Directory.CreateDirectory(captureDirectory);
            var warmupPath = Path.Combine(
                captureDirectory,
                "capture-warmup.png");
            window.CaptureToPng(warmupPath);
            await Task.Delay(300);
            File.Delete(warmupPath);
            window.CaptureToPng(
                Path.Combine(
                    captureDirectory,
                    "main-backup.png"));
            await Task.Delay(300);
            window.CaptureToPng(
                Path.Combine(
                    captureDirectory,
                    "main-backup.png"));
            await window.PrepareRestoreCaptureAsync();
            await Task.Delay(300);
            window.CaptureToPng(
                Path.Combine(
                    captureDirectory,
                    "main-restore.png"));
            window.Close();
            Shutdown();
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "capture-error.txt"),
                exception.ToString());
            window.Close();
            Shutdown(1);
        }
    }
#endif

#if UI_TEST_CAPTURE_NARROW
    private async void CaptureNarrowScreenshot(
        object? sender,
        EventArgs e)
    {
        if (_captureStarted || sender is not MainWindow window)
        {
            return;
        }

        _captureStarted = true;
        try
        {
            await Task.Delay(1500);
            var captureDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "captures",
                UiLanguage.Code);
            Directory.CreateDirectory(captureDirectory);
            var warmupPath = Path.Combine(
                captureDirectory,
                "capture-warmup.png");
            window.CaptureToPng(warmupPath);
            await Task.Delay(300);
            File.Delete(warmupPath);
            window.CaptureToPng(
                Path.Combine(
                    captureDirectory,
                    "narrow-backup.png"));
            await Task.Delay(300);
            window.CaptureToPng(
                Path.Combine(
                    captureDirectory,
                    "narrow-backup.png"));
            await window.PrepareRestoreCaptureAsync();
            await Task.Delay(300);
            window.CaptureToPng(
                Path.Combine(
                    captureDirectory,
                    "narrow-restore.png"));
            window.Close();
            Shutdown();
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "capture-error.txt"),
                exception.ToString());
            window.Close();
            Shutdown(1);
        }
    }
#endif
}
