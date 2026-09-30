using LockIn.Core.Models;

namespace LockIn.Core.Services;

public static class ProtectionRules
{
    private static readonly HashSet<string> AlwaysAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "dwm", "sihost", "ctfmon", "taskmgr", "taskhostw",
        "RuntimeBroker", "ApplicationFrameHost", "ShellExperienceHost",
        "StartMenuExperienceHost", "SearchHost", "SearchApp", "TextInputHost",
        "SecurityHealthSystray", "LockApp", "LogonUI", "winlogon", "csrss",
        "smss", "services", "lsass", "wininit", "fontdrvhost", "audiodg",
        "SystemSettings", "osk", "Narrator", "Magnify"
    };

    public static bool IsAlwaysAllowed(string? processName, string? lockInProcessName = null)
    {
        var normalized = FocusSession.NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(lockInProcessName) &&
            normalized.Equals(FocusSession.NormalizeProcessName(lockInProcessName), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AlwaysAllowed.Contains(normalized);
    }

    public static bool IsAllowed(FocusSession session, string processName, string? lockInProcessName = null)
    {
        var normalized = FocusSession.NormalizeProcessName(processName);
        return IsAlwaysAllowed(normalized, lockInProcessName) ||
               session.AllowedProcessNames.Contains(normalized);
    }
}
