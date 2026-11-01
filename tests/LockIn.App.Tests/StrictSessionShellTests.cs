using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
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

            var focusScrollViewer = (ScrollViewer)window.FindName("ActiveContentScrollViewer");
            var countdown = (System.Windows.Controls.TextBlock)window.FindName("CountdownText");
            Assert.IsTrue(focusScrollViewer.ViewportHeight > 0, "The narrow Focus workspace must have a measured scroll viewport.");
            Assert.IsTrue(focusScrollViewer.ScrollableHeight > 0, "The short window must scroll the active workspace instead of clipping its contents.");
            var countdownBounds = countdown.TransformToAncestor(focusScrollViewer).TransformBounds(new Rect(countdown.RenderSize));
            Assert.IsTrue(
                countdownBounds.Top >= -1 && countdownBounds.Bottom <= focusScrollViewer.ViewportHeight + 1,
                $"The countdown must be fully visible before scrolling; bounds={countdownBounds}, viewport={focusScrollViewer.ViewportHeight}.");

            focusScrollViewer.ScrollToEnd();
            Assert.IsTrue(
                PumpDispatcherUntil(
                    () => focusScrollViewer.VerticalOffset >= focusScrollViewer.ScrollableHeight - 1,
                    TimeSpan.FromSeconds(2)),
                "The operator must be able to reach the end of the active Focus workspace.");
            var blockedLog = (ListBox)window.FindName("BlockedLogList");
            var blockedLogBounds = blockedLog.TransformToAncestor(focusScrollViewer).TransformBounds(new Rect(blockedLog.RenderSize));
            Assert.IsTrue(
                blockedLogBounds.Top >= -1 && blockedLogBounds.Bottom <= focusScrollViewer.ViewportHeight + 1,
                $"The recently blocked log must be reachable after scrolling; bounds={blockedLogBounds}, viewport={focusScrollViewer.ViewportHeight}.");

            focusScrollViewer.ScrollToTop();
            Assert.IsTrue(
                PumpDispatcherUntil(() => focusScrollViewer.VerticalOffset <= 1, TimeSpan.FromSeconds(2)),
                "The Focus workspace must return to the countdown at the top.");
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


    [STATestMethod]
    [Timeout(25_000)]
    public void NonStrictSessionRequiresConfirmationAndEndsWithoutReward()
    {
        Assert.IsTrue(OperatingSystem.IsWindows(), "The real WPF shell acceptance is Windows-only.");

        var root = Path.Combine(Path.GetTempPath(), $"lock-in-nonstrict-shell-{Guid.NewGuid():N}");
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
                EndsAtUtc = now.AddMinutes(20),
                StrictMode = false
            };
            new SessionStore(root).SaveAsync(session).GetAwaiter().GetResult();

            var dialogs = new ScriptedUserDialog(MessageBoxResult.No, MessageBoxResult.Yes);
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

            var activePanel = (Grid)window.FindName("ActivePanel");
            var idlePanel = (Grid)window.FindName("IdlePanel");
            var endSession = (Button)window.FindName("EndSessionButton");
            Assert.AreEqual(Visibility.Visible, activePanel.Visibility);
            Assert.AreEqual(Visibility.Collapsed, idlePanel.Visibility);
            Assert.AreEqual(Visibility.Visible, endSession.Visibility);
            Assert.IsTrue(endSession.IsEnabled);
            Assert.IsTrue(File.Exists(sessionPath), "The active non-strict session must be persisted.");

            var evidenceDirectory = Environment.GetEnvironmentVariable("LOCKIN_UI_EVIDENCE_DIR");
            if (string.IsNullOrWhiteSpace(evidenceDirectory))
            {
                evidenceDirectory = Path.Combine(root, "ui-evidence");
            }

            Directory.CreateDirectory(evidenceDirectory);
            CaptureClientArea(window, Path.Combine(evidenceDirectory, "non-strict-session-1100x760.png"));

            var focusScrollViewer = (ScrollViewer)window.FindName("ActiveContentScrollViewer");
            Assert.IsTrue(focusScrollViewer.ScrollableHeight > 0, "The non-strict action must remain reachable in the scrollable Focus workspace.");
            focusScrollViewer.ScrollToEnd();
            Assert.IsTrue(
                PumpDispatcherUntil(
                    () => focusScrollViewer.VerticalOffset >= focusScrollViewer.ScrollableHeight - 1,
                    TimeSpan.FromSeconds(2)),
                "The operator must be able to scroll to the non-strict end-session action.");
            var endSessionBounds = endSession.TransformToAncestor(focusScrollViewer).TransformBounds(new Rect(endSession.RenderSize));
            Assert.IsTrue(
                endSessionBounds.Top >= -1 && endSessionBounds.Bottom <= focusScrollViewer.ViewportHeight + 1,
                $"The end-session action must be fully visible after scrolling; bounds={endSessionBounds}, viewport={focusScrollViewer.ViewportHeight}.");
            Assert.IsTrue(endSession.Focus(), "The visible end-session action must accept keyboard focus.");
            CaptureClientArea(window, Path.Combine(evidenceDirectory, "non-strict-session-end-action-1100x760.png"));
            focusScrollViewer.ScrollToTop();
            Assert.IsTrue(PumpDispatcherUntil(() => focusScrollViewer.VerticalOffset <= 1, TimeSpan.FromSeconds(2)));

            endSession.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual(1, dialogs.Calls.Count);
            Assert.AreEqual(MessageBoxButton.YesNo, dialogs.Calls[0].Buttons);
            StringAssert.Contains(dialogs.Calls[0].Message, "It will not award XP");
            Assert.AreEqual(Visibility.Visible, activePanel.Visibility, "Choosing No must keep the session active.");
            Assert.IsTrue(File.Exists(sessionPath), "Choosing No must preserve the active session record.");

            endSession.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual(2, dialogs.Calls.Count);
            Assert.AreEqual(Visibility.Collapsed, activePanel.Visibility);
            Assert.AreEqual(Visibility.Visible, idlePanel.Visibility);
            Assert.AreEqual("READY", ((System.Windows.Controls.TextBlock)window.FindName("HeaderStatusText")).Text);
            Assert.IsFalse(File.Exists(sessionPath), "Confirmed early end must clear the active session record.");

            var stats = new StatsStore(root).Load();
            Assert.AreEqual(0, stats.SessionsCompleted, "An early-ended session must not count as completed.");
            Assert.AreEqual(0, stats.Xp, "An early-ended session must not award XP.");
        }
        finally
        {
            if (window is not null && window.IsVisible)
            {
                var activePanel = (Grid)window.FindName("ActivePanel");
                var endSession = (Button)window.FindName("EndSessionButton");
                if (activePanel.Visibility == Visibility.Visible && endSession.Visibility == Visibility.Visible)
                {
                    endSession.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (activePanel.Visibility == Visibility.Visible)
                    {
                        endSession.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                }

                forceClose = true;
                window.Close();
            }

            if (File.Exists(sessionPath))
            {
                File.Delete(sessionPath);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }


    [STATestMethod]
    [Timeout(45_000)]
    public void RealWpfDurationFieldStartsInclusiveBoundariesAndRejectsValuesOutsideThem()
    {
        Assert.IsTrue(OperatingSystem.IsWindows(), "The real WPF shell acceptance is Windows-only.");
        EnsureApplicationResources();

        var validCases = new[]
        {
            (Text: "3", Minutes: 3, Width: 1100d, Height: 760d, Evidence: "duration-3-minute-desktop-1100x760.png"),
            (Text: "720", Minutes: 720, Width: 760d, Height: 680d, Evidence: "duration-720-minute-narrow-760x680.png"),
        };

        foreach (var testCase in validCases)
        {
            var root = Path.Combine(Path.GetTempPath(), $"lock-in-duration-{testCase.Minutes}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var sessionPath = Path.Combine(root, "active-session.json");
            MainWindow? window = null;

            try
            {
                var dialogs = new ScriptedUserDialog(MessageBoxResult.Yes);
                var guardian = new ProcessGuardian(
                    "lock-in-duration-test",
                    TimeSpan.FromMilliseconds(50),
                    static () => Array.Empty<Process>());

                window = new MainWindow(root, guardian, dialogs)
                {
                    Width = testCase.Width,
                    Height = testCase.Height,
                };
                window.Show();
                window.UpdateLayout();

                var mission = (TextBox)window.FindName("MissionTextBox");
                var duration = (TextBox)window.FindName("DurationMinutesTextBox");
                var start = (Button)window.FindName("StartMissionButton");
                mission.Text = $"Boundary test — {testCase.Minutes} minutes";
                Assert.IsTrue(duration.Focus(), "The real duration TextBox must accept keyboard focus.");
                duration.SelectAll();
                duration.Text = testCase.Text;

                start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                var activePanel = (Grid)window.FindName("ActivePanel");
                Assert.IsTrue(
                    PumpDispatcherUntil(() => activePanel.Visibility == Visibility.Visible, TimeSpan.FromSeconds(5)),
                    $"The WPF Start Mission action did not create the {testCase.Minutes}-minute session.");

                var idlePanel = (Grid)window.FindName("IdlePanel");
                var workspaceTabs = (TabControl)window.FindName("WorkspaceTabs");
                var storedSession = new SessionStore(root).LoadActive();
                Assert.IsNotNull(storedSession, "The real WPF action must persist the active session.");
                Assert.AreEqual(testCase.Minutes, storedSession!.PlannedMinutes);
                Assert.AreEqual(2, workspaceTabs.SelectedIndex, "Starting a session must navigate to the Focus workspace.");
                Assert.AreEqual(Visibility.Collapsed, idlePanel.Visibility);

                var countdown = (TextBlock)window.FindName("CountdownText");
                Assert.IsTrue(
                    TimeSpan.TryParse(countdown.Text, CultureInfo.InvariantCulture, out var remaining),
                    $"The real countdown should be parseable; actual text was '{countdown.Text}'.");
                Assert.IsTrue(
                    remaining.TotalSeconds > testCase.Minutes * 60 - 5 &&
                    remaining.TotalSeconds <= testCase.Minutes * 60,
                    $"The countdown should start near {testCase.Minutes} minutes; actual value was {remaining}.");

                if (testCase.Minutes == 720)
                {
                    var scrollViewer = (ScrollViewer)window.FindName("ActiveContentScrollViewer");
                    var countdownBounds = countdown.TransformToAncestor(scrollViewer).TransformBounds(new Rect(countdown.RenderSize));
                    Assert.IsTrue(scrollViewer.ViewportHeight > 0, "The narrow Focus workspace must have a measured viewport.");
                    Assert.IsTrue(
                        countdownBounds.Top >= -1 && countdownBounds.Bottom <= scrollViewer.ViewportHeight + 1,
                        $"The 12-hour countdown must remain visible in the 760×680 window; bounds={countdownBounds}, viewport={scrollViewer.ViewportHeight}.");
                }

                var evidenceDirectory = Environment.GetEnvironmentVariable("LOCKIN_UI_EVIDENCE_DIR");
                if (string.IsNullOrWhiteSpace(evidenceDirectory))
                {
                    evidenceDirectory = Path.Combine(root, "ui-evidence");
                }

                CaptureClientArea(window, Path.Combine(evidenceDirectory, testCase.Evidence));

                var endSession = (Button)window.FindName("EndSessionButton");
                Assert.AreEqual(Visibility.Visible, endSession.Visibility, "A non-strict boundary test must expose the real early-end action.");
                endSession.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(Visibility.Collapsed, activePanel.Visibility);
                Assert.AreEqual(Visibility.Visible, idlePanel.Visibility);
                Assert.IsFalse(File.Exists(sessionPath), "The test must cleanly end the session without leaving persisted state.");
                Assert.AreEqual(1, dialogs.Calls.Count);
                Assert.AreEqual(MessageBoxButton.YesNo, dialogs.Calls[0].Buttons);
                window.Close();
                window = null;
            }
            finally
            {
                if (window is not null && window.IsVisible)
                {
                    var endSession = (Button)window.FindName("EndSessionButton");
                    if (endSession.Visibility == Visibility.Visible && endSession.IsEnabled)
                    {
                        endSession.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }

                    window.Close();
                }

                if (File.Exists(sessionPath))
                {
                    File.Delete(sessionPath);
                }

                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        foreach (var invalidValue in new[] { "2", "721" })
        {
            var root = Path.Combine(Path.GetTempPath(), $"lock-in-invalid-duration-{invalidValue}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var sessionPath = Path.Combine(root, "active-session.json");
            MainWindow? window = null;

            try
            {
                var dialogs = new RecordingUserDialog();
                var guardian = new ProcessGuardian(
                    "lock-in-invalid-duration-test",
                    TimeSpan.FromMilliseconds(50),
                    static () => Array.Empty<Process>());

                window = new MainWindow(root, guardian, dialogs)
                {
                    Width = 1100,
                    Height = 760,
                };
                window.Show();
                window.UpdateLayout();

                var duration = (TextBox)window.FindName("DurationMinutesTextBox");
                duration.Focus();
                duration.SelectAll();
                duration.Text = invalidValue;
                ((Button)window.FindName("StartMissionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.AreEqual(1, dialogs.Calls.Count, "The real WPF shell must show the validation error.");
                StringAssert.Contains(dialogs.Calls[0].Message, FocusDuration.ValidationMessage);
                Assert.AreEqual(Visibility.Visible, ((Grid)window.FindName("IdlePanel")).Visibility);
                Assert.AreEqual(Visibility.Collapsed, ((Grid)window.FindName("ActivePanel")).Visibility);
                Assert.IsFalse(File.Exists(sessionPath), "Invalid input must not persist an active session.");
                window.Close();
                window = null;
            }
            finally
            {
                if (window is not null && window.IsVisible)
                {
                    window.Close();
                }

                if (File.Exists(sessionPath))
                {
                    File.Delete(sessionPath);
                }

                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
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
        var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);

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


    private sealed class ScriptedUserDialog : LockIn.App.Services.IUserDialog
    {
        private readonly Queue<MessageBoxResult> _answers;

        public ScriptedUserDialog(params MessageBoxResult[] answers)
        {
            _answers = new Queue<MessageBoxResult>(answers);
        }

        public List<DialogCall> Calls { get; } = [];

        public MessageBoxResult Show(
            string message,
            string title,
            MessageBoxButton buttons,
            MessageBoxImage image)
        {
            Calls.Add(new DialogCall(message, title, buttons, image));
            return _answers.Count > 0 ? _answers.Dequeue() : MessageBoxResult.Yes;
        }
    }

    private sealed record DialogCall(
        string Message,
        string Title,
        MessageBoxButton Buttons,
        MessageBoxImage Image);
}
