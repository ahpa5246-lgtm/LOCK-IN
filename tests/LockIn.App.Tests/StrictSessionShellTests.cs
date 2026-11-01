using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LockIn.App;
using LockIn.Core.Models;
using LockIn.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.App.Tests;

[TestClass]
public sealed class StrictSessionShellTests
{
    [STATestMethod]
    [Timeout(40_000)]
    public void RestoredStrictSessionShowsFocusWorkspaceAndRejectsCloseUntilExpiry()
    {
        Assert.IsTrue(OperatingSystem.IsWindows(), "The real WPF shell acceptance is Windows-only.");

        var root = Path.Combine(Path.GetTempPath(), $"lock-in-session-shell-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sessionPath = Path.Combine(root, "active-session.json");
        MainWindow? window = null;
        var forceClose = false;

        try
        {
            EnsureApplicationResources();

            var now = DateTimeOffset.UtcNow;
            var session = new FocusSession
            {
                MissionName = "Review the report",
                StartedAtUtc = now.AddMinutes(-1),
                EndsAtUtc = now.AddSeconds(12),
                StrictMode = true
            };
            new SessionStore(root).SaveAsync(session).GetAwaiter().GetResult();

            var dialogs = new RecordingUserDialog();
            var guardian = new ProcessGuardian(
                "lock-in-test",
                TimeSpan.FromMilliseconds(50),
                static () => Array.Empty<Process>());

            window = new MainWindow(root, guardian, dialogs);
            window.Closing += (_, args) =>
            {
                if (forceClose)
                {
                    args.Cancel = false;
                }
            };

            window.Width = 1100;
            window.Height = 760;
            window.Show();
            window.UpdateLayout();

            Assert.AreEqual(Visibility.Collapsed, ((Grid)window.FindName("IdlePanel")).Visibility);
            Assert.AreEqual(Visibility.Visible, ((Grid)window.FindName("ActivePanel")).Visibility);
            Assert.AreEqual("Review the report", ((System.Windows.Controls.TextBlock)window.FindName("ActiveMissionText")).Text);
            Assert.AreEqual("STRICT MODE", ((System.Windows.Controls.TextBlock)window.FindName("ActiveModeText")).Text);
            Assert.AreEqual("LOCKED IN", ((System.Windows.Controls.TextBlock)window.FindName("HeaderStatusText")).Text);
            Assert.AreEqual(Visibility.Collapsed, ((System.Windows.Controls.Button)window.FindName("EndSessionButton")).Visibility);
            Assert.IsFalse(((System.Windows.Controls.Button)window.FindName("StartMissionButton")).IsEnabled);
            Assert.IsTrue(File.Exists(sessionPath), "The restored active session must remain persisted.");

            var evidenceDirectory = Environment.GetEnvironmentVariable("LOCKIN_UI_EVIDENCE_DIR");
            if (string.IsNullOrWhiteSpace(evidenceDirectory))
            {
                evidenceDirectory = Path.Combine(root, "ui-evidence");
            }

            Directory.CreateDirectory(evidenceDirectory);
            CaptureClientArea(window, Path.Combine(evidenceDirectory, "strict-session-desktop-1100x760.png"));

            window.Width = 760;
            window.Height = 680;
            window.UpdateLayout();
            CaptureClientArea(window, Path.Combine(evidenceDirectory, "strict-session-narrow-760x680.png"));

            window.Close();

            Assert.IsTrue(window.IsVisible, "Strict Mode must reject a user close while the session is active.");
            Assert.IsTrue(
                dialogs.Calls.Any(call =>
                    call.Title == "LOCK-IN" &&
                    call.Message.Contains("Strict Mode is active", StringComparison.Ordinal)),
                "The operator must receive the actual Strict Mode close explanation.");

            Assert.IsTrue(
                PumpDispatcherUntil(() => !File.Exists(sessionPath), TimeSpan.FromSeconds(20)),
                "The restored session did not expire and clear its persisted record.");
            Assert.AreEqual(Visibility.Visible, ((Grid)window.FindName("IdlePanel")).Visibility);
            Assert.AreEqual(Visibility.Collapsed, ((Grid)window.FindName("ActivePanel")).Visibility);

            var stats = new StatsStore(root).Load();
            Assert.AreEqual(1, stats.SessionsCompleted);
            Assert.AreEqual(10, stats.Xp);
            Assert.IsTrue(
                dialogs.Calls.Any(call => call.Message.StartsWith("Mission complete.", StringComparison.Ordinal)),
                "Natural expiry must report completion through the real UI dialog boundary.");

            window.Close();
            Assert.IsFalse(window.IsVisible, "The window should close after the strict session has expired.");
        }
        finally
        {
            if (window is not null && window.IsVisible)
            {
                forceClose = true;
                window.Close();
            }

            if (File.Exists(sessionPath))
            {
                PumpDispatcherUntil(() => !File.Exists(sessionPath), TimeSpan.FromSeconds(20));
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void EnsureApplicationResources()
    {
        var application = Application.Current as LockIn.App.App;
        if (application is null)
        {
            Assert.IsNull(
                Application.Current,
                "The test host must not create a different WPF Application before LOCK-IN initializes its resources.");
            application = new LockIn.App.App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }

        if (!application.Resources.Contains("BackgroundBrush"))
        {
            application.InitializeComponent();
        }

        Assert.IsTrue(
            application.Resources.Contains("BackgroundBrush"),
            "The real App.xaml resource dictionary must be loaded before the shell is constructed.");
    }

    private static void CaptureClientArea(Window window, string path)
    {
        window.UpdateLayout();
        var visual = window.Content as Visual
            ?? throw new AssertFailedException("The WPF window has no renderable client visual.");
        var size = ((FrameworkElement)visual).RenderSize;
        var width = Math.Max(1, (int)Math.Ceiling(size.Width));
        var height = Math.Max(1, (int)Math.Ceiling(size.Height));

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var stream = File.Create(path))
        {
            encoder.Save(stream);
        }

        Assert.IsTrue(new FileInfo(path).Length > 0, $"The UI evidence image was not written: {path}");
    }

    private static bool PumpDispatcherUntil(Func<bool> condition, TimeSpan timeout)
    {
        if (condition())
        {
            return true;
        }

        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow + timeout;
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(25)
        };
        timer.Tick += (_, _) =>
        {
            if (condition() || DateTime.UtcNow >= deadline)
            {
                timer.Stop();
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        return condition();
    }

    private sealed class RecordingUserDialog : LockIn.App.Services.IUserDialog
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
