using System.ComponentModel;
using System.Diagnostics;
using LockIn.Core.Models;

namespace LockIn.Core.Services;

public sealed record BlockedProcessEvent(string ProcessName, DateTimeOffset OccurredAtLocal);

public sealed class ProcessGuardian
{
    private readonly string _ownProcessName;
    private readonly TimeSpan _scanInterval;

    public event Action<BlockedProcessEvent>? ProcessBlocked;

    public ProcessGuardian(string? ownProcessName = null, TimeSpan? scanInterval = null)
    {
        _ownProcessName = FocusSession.NormalizeProcessName(
            ownProcessName ?? Process.GetCurrentProcess().ProcessName);
        _scanInterval = scanInterval ?? TimeSpan.FromMilliseconds(650);
    }

    public async Task RunAsync(FocusSession session, CancellationToken cancellationToken)
    {
        session.Validate();
        var currentSessionId = Process.GetCurrentProcess().SessionId;

        while (!cancellationToken.IsCancellationRequested && session.IsActive(DateTimeOffset.UtcNow))
        {
            Scan(session, currentSessionId);
            await Task.Delay(_scanInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    internal void Scan(FocusSession session, int currentSessionId)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.HasExited ||
                        process.Id == Environment.ProcessId ||
                        process.SessionId != currentSessionId ||
                        process.MainWindowHandle == IntPtr.Zero)
                    {
                        continue;
                    }

                    var processName = FocusSession.NormalizeProcessName(process.ProcessName);
                    if (ProtectionRules.IsAllowed(session, processName, _ownProcessName))
                    {
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    ProcessBlocked?.Invoke(new BlockedProcessEvent(processName, DateTimeOffset.Now));
                }
                catch (Win32Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }
    }
}
