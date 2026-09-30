using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace LockIn.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private static string LogPath
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LOCK-IN");
            Directory.CreateDirectory(root);
            return Path.Combine(root, "error.log");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        MessageBox.Show(
            "LOCK-IN hit an unexpected error. The details were saved locally to error.log. " +
            "If a focus session was active, reopening LOCK-IN will restore it automatically.",
            "LOCK-IN",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(1);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log(e.Exception);
        e.SetObserved();
    }

    private static void Log(Exception exception)
    {
        try
        {
            File.AppendAllText(
                LogPath,
                $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
