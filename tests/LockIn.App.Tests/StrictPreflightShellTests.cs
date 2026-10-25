using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LockIn.App;
using LockIn.App.Services;
using LockIn.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.App.Tests;

[TestClass]
public sealed class StrictPreflightShellTests
{
    [STATestMethod]
    [Timeout(25_000)]
    public async Task PreflightLeavesOwnedDisallowedWindowOpenAndDoesNotPersistSession()
    {
        Assert.IsTrue(OperatingSystem.IsWindows(), "The real WPF shell acceptance is Windows-only.");

        var root = Path.Combine(Path.GetTempPath(), $"lock-in-shell-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(root, "target.ps1");
        Directory.CreateDirectory(root);
        Process? helper = null;
        MainWindow? window = null;

        await File.WriteAllTextAsync(
            scriptPath,
            """
            Add-Type -AssemblyName PresentationFramework
            $window = [Windows.Window]::new()
            $window.Title = "LOCK-IN disposable pre-flight target"
            $window.Width = 320
            $window.Height = 180
            $window.WindowStartupLocation = "CenterScreen"
            [void]$window.ShowDialog()
            """);

        try
        {
            helper = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -STA -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new AssertFailedException("Unable to start the disposable WPF process.");

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!helper.HasExited && helper.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
                helper.Refresh();
            }

            Assert.IsFalse(helper.HasExited, "The disposable process exited before pre-flight verification.");
            Assert.AreNotEqual(IntPtr.Zero, helper.MainWindowHandle, "The disposable process never created a window.");

            _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var dialogs = new RecordingUserDialog();
            var helperPid = helper.Id;
            var guardian = new ProcessGuardian(
                "lock-in-test",
                TimeSpan.FromMilliseconds(50),
                () => new[] { Process.GetProcessById(helperPid) });

            window = new MainWindow(root, guardian, dialogs);
            window.Show();

            ((TextBox)window.FindName("MissionTextBox")).Text = "Safe pre-flight acceptance";
            ((TextBox)window.FindName("DurationMinutesTextBox")).Text = "3";
            ((CheckBox)window.FindName("StrictModeCheckBox")).IsChecked = true;
            ((Button)window.FindName("StartMissionButton")).RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual(1, dialogs.Calls.Count);
            Assert.AreEqual("Pre-flight check", dialogs.Calls[0].Title);
            StringAssert.Contains(dialogs.Calls[0].Message, "powershell.exe");
            Assert.IsFalse(helper.HasExited, "Pre-flight must not terminate an app that may contain unsaved work.");
            Assert.AreEqual(Visibility.Visible, ((Grid)window.FindName("IdlePanel")).Visibility);
            Assert.AreEqual(Visibility.Collapsed, ((Grid)window.FindName("ActivePanel")).Visibility);
            Assert.IsFalse(File.Exists(Path.Combine(root, "active-session.json")));
        }
        finally
        {
            window?.Close();

            if (helper is not null)
            {
                if (!helper.HasExited)
                {
                    helper.Kill(entireProcessTree: true);
                    helper.WaitForExit(5_000);
                }

                helper.Dispose();
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class RecordingUserDialog : IUserDialog
    {
        public List<DialogCall> Calls { get; } = [];

        public MessageBoxResult Show(
            string message,
            string title,
            MessageBoxButton buttons,
            MessageBoxImage image)
        {
            Calls.Add(new DialogCall(message, title, buttons, image));
            return MessageBoxResult.OK;
        }
    }

    private sealed record DialogCall(
        string Message,
        string Title,
        MessageBoxButton Buttons,
        MessageBoxImage Image);
}
