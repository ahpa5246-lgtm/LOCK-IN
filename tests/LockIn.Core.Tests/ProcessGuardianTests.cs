using System.Diagnostics;
using LockIn.Core.Models;
using LockIn.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.Core.Tests;

[TestClass]
public sealed class ProcessGuardianTests
{
    [TestMethod]
    [Timeout(20_000)]
    public async Task TargetedGuardianTerminatesOwnedDisallowedWindowProcess()
    {
        Assert.IsTrue(OperatingSystem.IsWindows(), "The real process guardian is a Windows-only integration.");

        var scriptPath = Path.Combine(
            Path.GetTempPath(),
            $"lock-in-guardian-{Guid.NewGuid():N}.ps1");
        Process? helper = null;

        await File.WriteAllTextAsync(
            scriptPath,
            """
            Add-Type -AssemblyName PresentationFramework
            $window = [Windows.Window]::new()
            $window.Title = "LOCK-IN disposable guardian target"
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

            var windowDeadline = DateTime.UtcNow.AddSeconds(10);
            while (!helper.HasExited &&
                   helper.MainWindowHandle == IntPtr.Zero &&
                   DateTime.UtcNow < windowDeadline)
            {
                await Task.Delay(100);
                helper.Refresh();
            }

            Assert.IsFalse(helper.HasExited, "The disposable process exited before guardian verification.");
            Assert.AreNotEqual(
                IntPtr.Zero,
                helper.MainWindowHandle,
                "The disposable process never created an interactive window.");

            var now = DateTimeOffset.UtcNow;
            var session = new FocusSession
            {
                MissionName = "Disposable guardian verification",
                StartedAtUtc = now.AddMinutes(-1),
                EndsAtUtc = now.AddMinutes(5),
                StrictMode = true
            };
            var events = new List<BlockedProcessEvent>();
            var guardian = new ProcessGuardian("lock-in-test");
            guardian.ProcessBlocked += events.Add;

            var blocked = guardian.TryBlockProcess(
                helper,
                session,
                Process.GetCurrentProcess().SessionId);

            Assert.IsTrue(blocked, "The targeted disallowed process was not blocked.");
            Assert.IsTrue(helper.WaitForExit(5_000), "The targeted process did not exit.");
            CollectionAssert.AreEqual(
                new[] { "powershell" },
                events.Select(item => item.ProcessName).ToArray());
        }
        finally
        {
            if (helper is not null)
            {
                if (!helper.HasExited)
                {
                    helper.Kill(entireProcessTree: true);
                    helper.WaitForExit(5_000);
                }

                helper.Dispose();
            }

            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
            }
        }
    }
}
