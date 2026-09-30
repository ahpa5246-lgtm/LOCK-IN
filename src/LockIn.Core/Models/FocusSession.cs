namespace LockIn.Core.Models;

public sealed class FocusSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MissionName { get; set; } = "Focus mission";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public bool StrictMode { get; set; }
    public HashSet<string> AllowedProcessNames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int PlannedMinutes =>
        Math.Max(1, (int)Math.Round((EndsAtUtc - StartedAtUtc).TotalMinutes, MidpointRounding.AwayFromZero));

    public bool IsActive(DateTimeOffset nowUtc) => nowUtc < EndsAtUtc;

    public TimeSpan Remaining(DateTimeOffset nowUtc)
    {
        var remaining = EndsAtUtc - nowUtc;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public void Validate()
    {
        MissionName = MissionName.Trim();
        if (string.IsNullOrWhiteSpace(MissionName))
        {
            throw new InvalidOperationException("A mission name is required.");
        }

        if (EndsAtUtc <= StartedAtUtc)
        {
            throw new InvalidOperationException("The session end time must be after the start time.");
        }

        if (EndsAtUtc - StartedAtUtc > TimeSpan.FromHours(12))
        {
            throw new InvalidOperationException("A single focus session cannot exceed 12 hours.");
        }

        AllowedProcessNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AllowedProcessNames = AllowedProcessNames
            .Select(NormalizeProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
