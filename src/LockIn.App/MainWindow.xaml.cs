using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LockIn.App.Services;
using LockIn.Core.Models;
using LockIn.Core.Services;
using Microsoft.Win32;

namespace LockIn.App;

public partial class MainWindow : Window
{
    private readonly SessionStore _sessionStore;
    private readonly StatsStore _statsStore;
    private readonly ProcessGuardian _guardian;
    private readonly DispatcherTimer _uiTimer;
    private readonly HashSet<string> _allowedApps = new(StringComparer.OrdinalIgnoreCase);

    private FocusSession? _activeSession;
    private CancellationTokenSource? _guardianCancellation;
    private bool _internalStartupToggleChange;

    public MainWindow()
    {
        InitializeComponent();

        var dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LOCK-IN");
        Directory.CreateDirectory(dataRoot);

        _sessionStore = new SessionStore(dataRoot);
        _statsStore = new StatsStore(dataRoot);
        _guardian = new ProcessGuardian();
        _guardian.ProcessBlocked += OnProcessBlocked;

        _uiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _uiTimer.Tick += UiTimer_Tick;

        _internalStartupToggleChange = true;
        StartupCheckBox.IsChecked = StartupRegistrar.IsEnabled();
        _internalStartupToggleChange = false;

        RefreshStats();

        var restoredSession = _sessionStore.LoadActive();
        if (restoredSession is not null)
        {
            foreach (var app in restoredSession.AllowedProcessNames.OrderBy(x => x))
            {
                _allowedApps.Add(app);
                AllowedAppsList.Items.Add(app + ".exe");
            }

            MissionTextBox.Text = restoredSession.MissionName;
            StrictModeCheckBox.IsChecked = restoredSession.StrictMode;
            BeginEnforcement(restoredSession);
        }
    }

    private async void StartMissionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var durationMinutes = GetSelectedDuration();
            var now = DateTimeOffset.UtcNow;

            var session = new FocusSession
            {
                MissionName = MissionTextBox.Text,
                StartedAtUtc = now,
                EndsAtUtc = now.AddMinutes(durationMinutes),
                StrictMode = StrictModeCheckBox.IsChecked == true,
                AllowedProcessNames = _allowedApps.ToHashSet(StringComparer.OrdinalIgnoreCase)
            };

            session.Validate();

            var alreadyOpen = _guardian.FindDisallowedInteractiveProcessNames(session);
            if (alreadyOpen.Count > 0)
            {
                var shown = alreadyOpen.Take(10).Select(name => "• " + name + ".exe");
                var more = alreadyOpen.Count > 10
                    ? $"{Environment.NewLine}…and {alreadyOpen.Count - 10} more."
                    : string.Empty;

                MessageBox.Show(
                    "Before LOCK-IN starts, close these currently open apps or add them to the allow-list:" +
                    Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, shown) +
                    more +
                    Environment.NewLine + Environment.NewLine +
                    "This safety check prevents LOCK-IN from closing an app that may contain unsaved work.",
                    "Pre-flight check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await _sessionStore.SaveAsync(session);
            BeginEnforcement(session);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            MessageBox.Show(exception.Message, "LOCK-IN", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BeginEnforcement(FocusSession session)
    {
        _activeSession = session;
        _guardianCancellation?.Cancel();
        _guardianCancellation?.Dispose();
        _guardianCancellation = new CancellationTokenSource();

        IdlePanel.Visibility = Visibility.Collapsed;
        ActivePanel.Visibility = Visibility.Visible;
        HeaderStatusText.Text = "LOCKED IN";

        ActiveMissionText.Text = session.MissionName;
        ActiveModeText.Text = session.StrictMode ? "STRICT MODE" : "FOCUS MODE";
        EndSessionButton.Visibility = session.StrictMode ? Visibility.Collapsed : Visibility.Visible;

        SetSetupEnabled(false);
        UpdateActiveSessionUi();
        _uiTimer.Start();

        var cancellationToken = _guardianCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await _guardian.RunAsync(session, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void SetSetupEnabled(bool enabled)
    {
        MissionTextBox.IsEnabled = enabled;
        DurationComboBox.IsEnabled = enabled;
        AddAppButton.IsEnabled = enabled;
        RemoveAppButton.IsEnabled = enabled;
        StrictModeCheckBox.IsEnabled = enabled;
        StartMissionButton.IsEnabled = enabled;
    }

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        if (_activeSession is null)
        {
            return;
        }

        if (!_activeSession.IsActive(DateTimeOffset.UtcNow))
        {
            _ = CompleteSessionAsync();
            return;
        }

        UpdateActiveSessionUi();
    }

    private void UpdateActiveSessionUi()
    {
        if (_activeSession is null)
        {
            return;
        }

        var remaining = _activeSession.Remaining(DateTimeOffset.UtcNow);
        CountdownText.Text = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes:00}:{remaining.Seconds:00}";

        var totalSeconds = Math.Max(1, (_activeSession.EndsAtUtc - _activeSession.StartedAtUtc).TotalSeconds);
        BossHealthBar.Value = Math.Clamp(remaining.TotalSeconds / totalSeconds * 100.0, 0, 100);

        var allowedCount = _activeSession.AllowedProcessNames.Count;
        SessionSummaryText.Text =
            $"{_activeSession.PlannedMinutes} min · {allowedCount} allowed app{(allowedCount == 1 ? string.Empty : "s")}";
    }

    private async Task CompleteSessionAsync()
    {
        var completedSession = _activeSession;
        if (completedSession is null)
        {
            return;
        }

        _activeSession = null;
        _uiTimer.Stop();
        _guardianCancellation?.Cancel();

        await _statsStore.MarkCompletedAsync(completedSession, DateTimeOffset.Now);
        _sessionStore.Clear();

        ResetToIdle();
        RefreshStats();

        MessageBox.Show(
            $"Mission complete. +{completedSession.PlannedMinutes * 10} XP.",
            "LOCK-IN",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void EndSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSession is null || _activeSession.StrictMode)
        {
            return;
        }

        var answer = MessageBox.Show(
            "End this focus session early? It will not award XP.",
            "LOCK-IN",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        EndSessionWithoutReward();
    }

    private void EndSessionWithoutReward()
    {
        _activeSession = null;
        _uiTimer.Stop();
        _guardianCancellation?.Cancel();
        _sessionStore.Clear();
        ResetToIdle();
    }

    private void ResetToIdle()
    {
        IdlePanel.Visibility = Visibility.Visible;
        ActivePanel.Visibility = Visibility.Collapsed;
        HeaderStatusText.Text = "READY";
        SetSetupEnabled(true);
        BlockedLogList.Items.Clear();
    }

    private void AddAppButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an application to allow",
            Filter = "Applications (*.exe)|*.exe",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var file in dialog.FileNames)
        {
            var processName = FocusSession.NormalizeProcessName(Path.GetFileName(file));
            if (_allowedApps.Add(processName))
            {
                AllowedAppsList.Items.Add(processName + ".exe");
            }
        }
    }

    private void RemoveAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (AllowedAppsList.SelectedItem is not string selected)
        {
            return;
        }

        _allowedApps.Remove(FocusSession.NormalizeProcessName(selected));
        AllowedAppsList.Items.Remove(selected);
    }

    private int GetSelectedDuration()
    {
        if (DurationComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            int.TryParse(tag, out var minutes))
        {
            return minutes;
        }

        return 50;
    }

    private void OnProcessBlocked(BlockedProcessEvent blocked)
    {
        Dispatcher.BeginInvoke(() =>
        {
            BlockedLogList.Items.Insert(
                0,
                $"{blocked.OccurredAtLocal:HH:mm:ss}  {blocked.ProcessName}.exe");

            while (BlockedLogList.Items.Count > 8)
            {
                BlockedLogList.Items.RemoveAt(BlockedLogList.Items.Count - 1);
            }
        });
    }

    private void RefreshStats()
    {
        var stats = _statsStore.Load();
        TotalFocusText.Text = stats.TotalFocusMinutes >= 60
            ? $"{stats.TotalFocusMinutes / 60}h {stats.TotalFocusMinutes % 60}m"
            : $"{stats.TotalFocusMinutes} min";
        SessionsText.Text = stats.SessionsCompleted.ToString();
        XpText.Text = stats.Xp.ToString("N0");
        StreakText.Text = $"{stats.CurrentStreak} day{(stats.CurrentStreak == 1 ? string.Empty : "s")}";
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_internalStartupToggleChange)
        {
            return;
        }

        try
        {
            StartupRegistrar.SetEnabled(StartupCheckBox.IsChecked == true);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            _internalStartupToggleChange = true;
            StartupCheckBox.IsChecked = StartupRegistrar.IsEnabled();
            _internalStartupToggleChange = false;

            MessageBox.Show(
                $"Unable to change the Windows startup setting: {exception.Message}",
                "LOCK-IN",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_activeSession is null)
        {
            _guardianCancellation?.Cancel();
            return;
        }

        if (_activeSession.StrictMode && _activeSession.IsActive(DateTimeOffset.UtcNow))
        {
            e.Cancel = true;
            MessageBox.Show(
                $"Strict Mode is active until {_activeSession.EndsAtUtc.ToLocalTime():t}. " +
                "LOCK-IN will release the session automatically.",
                "LOCK-IN",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            "A focus session is active. End it and close LOCK-IN?",
            "LOCK-IN",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        EndSessionWithoutReward();
        _guardianCancellation?.Cancel();
    }
}
