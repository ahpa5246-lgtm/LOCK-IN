using LockIn.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.Core.Tests;

[TestClass]
public sealed class UserStatsTests
{
    [TestMethod]
    public void CompletedSessionAwardsXpOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new FocusSession
        {
            Id = "session-1",
            MissionName = "Study",
            StartedAtUtc = now,
            EndsAtUtc = now.AddMinutes(50)
        };
        var stats = new UserStats();

        Assert.IsTrue(stats.ApplyCompletedSession(session, DateTimeOffset.Now));
        Assert.IsFalse(stats.ApplyCompletedSession(session, DateTimeOffset.Now));
        Assert.AreEqual(500, stats.Xp);
        Assert.AreEqual(50, stats.TotalFocusMinutes);
        Assert.AreEqual(1, stats.SessionsCompleted);
    }

    [TestMethod]
    public void ConsecutiveDatesIncreaseStreak()
    {
        var stats = new UserStats();
        var now = DateTimeOffset.Now;

        stats.ApplyCompletedSession(
            new FocusSession
            {
                Id = "one",
                MissionName = "One",
                StartedAtUtc = now,
                EndsAtUtc = now.AddMinutes(25)
            },
            now);

        stats.ApplyCompletedSession(
            new FocusSession
            {
                Id = "two",
                MissionName = "Two",
                StartedAtUtc = now.AddDays(1),
                EndsAtUtc = now.AddDays(1).AddMinutes(25)
            },
            now.AddDays(1));

        Assert.AreEqual(2, stats.CurrentStreak);
    }
}
