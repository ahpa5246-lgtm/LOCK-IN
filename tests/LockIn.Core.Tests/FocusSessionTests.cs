using LockIn.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.Core.Tests;

[TestClass]
public sealed class FocusSessionTests
{
    [TestMethod]
    public void Validate_NormalizesExeSuffix()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new FocusSession
        {
            MissionName = "Study",
            StartedAtUtc = now,
            EndsAtUtc = now.AddMinutes(50),
            AllowedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "code.exe",
                "OBSIDIAN.EXE"
            }
        };

        session.Validate();

        CollectionAssert.AreEquivalent(
            new[] { "code", "OBSIDIAN" },
            session.AllowedProcessNames.ToArray());
    }

    [TestMethod]
    public void Validate_RejectsSessionsLongerThanTwelveHours()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new FocusSession
        {
            MissionName = "Too long",
            StartedAtUtc = now,
            EndsAtUtc = now.AddHours(13)
        };

        Assert.ThrowsException<InvalidOperationException>(session.Validate);
    }

    [TestMethod]
    public void Remaining_NeverReturnsNegativeTime()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new FocusSession
        {
            MissionName = "Done",
            StartedAtUtc = now.AddMinutes(-10),
            EndsAtUtc = now.AddMinutes(-1)
        };

        Assert.AreEqual(TimeSpan.Zero, session.Remaining(now));
    }
}
