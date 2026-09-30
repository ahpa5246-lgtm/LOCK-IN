namespace LockIn.Core.Models;

public sealed class UserStats
{
    public int TotalFocusMinutes { get; set; }
    public int SessionsCompleted { get; set; }
    public int Xp { get; set; }
    public int CurrentStreak { get; set; }
    public DateOnly? LastCompletedDate { get; set; }
    public HashSet<string> CompletedSessionIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ApplyCompletedSession(FocusSession session, DateTimeOffset completedAtLocal)
    {
        CompletedSessionIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!CompletedSessionIds.Add(session.Id))
        {
            return false;
        }

        var minutes = session.PlannedMinutes;
        TotalFocusMinutes += minutes;
        SessionsCompleted++;
        Xp += minutes * 10;

        var completedDate = DateOnly.FromDateTime(completedAtLocal.Date);
        if (LastCompletedDate is null)
        {
            CurrentStreak = 1;
        }
        else if (LastCompletedDate == completedDate)
        {
        }
        else if (LastCompletedDate.Value.AddDays(1) == completedDate)
        {
            CurrentStreak++;
        }
        else
        {
            CurrentStreak = 1;
        }

        LastCompletedDate = completedDate;

        if (CompletedSessionIds.Count > 200)
        {
            CompletedSessionIds = CompletedSessionIds.TakeLast(100)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }
}
