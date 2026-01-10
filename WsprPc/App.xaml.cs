using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WsprPc;

public partial class App : System.Windows.Application
{
    private string? _logPath;
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "TapScribe-PC-SingleInstance-Mutex", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("TapScribe körs redan.", "TapScribe", MessageBoxButton.OK, MessageBoxImage.Information);
            Current.Shutdown();
            return;
        }

        base.OnStartup(e);
        _logPath = InitLogPath();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var splash = new SplashWindow();
        splash.Show();

        Dispatcher.InvokeAsync(() =>
        {
            var main = new MainWindow();
            MainWindow = main;
            main.Show();
            try
            {
                splash.Close();
            }
            catch
            {
            }
        }, DispatcherPriority.Background);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "UI");
        ShowFatalError(e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex, "AppDomain");
            ShowFatalError(ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception, "Task");
        ShowFatalError(e.Exception);
        e.SetObserved();
    }

    private static string InitLogPath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dataDir = Path.Combine(baseDir, "TapScribe");
        string logDir = Path.Combine(dataDir, "logs");
        Directory.CreateDirectory(logDir);
        return Path.Combine(logDir, "app.log");
    }

    private void LogException(Exception ex, string source)
    {
        if (string.IsNullOrWhiteSpace(_logPath))
            return;

        try
        {
            string line = $"[{DateTimeOffset.Now:u}] {source}: {ex}\n";
            File.AppendAllText(_logPath, line);
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private void ShowFatalError(Exception ex)
    {
        try
        {
            string logInfo = string.IsNullOrWhiteSpace(_logPath) ? "" : $"\n\nLogg: {_logPath}";
            System.Windows.MessageBox.Show(
                $"Något gick fel när TapScribe startade.\n{ex.Message}{logInfo}",
                "TapScribe - Fel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Ignore UI failures.
        }
    }
}
