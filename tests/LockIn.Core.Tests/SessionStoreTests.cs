using LockIn.Core.Models;
using LockIn.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.Core.Tests;

[TestClass]
public sealed class SessionStoreTests
{
    [TestMethod]
    public async Task LoadActive_RestoresStrictSessionAndAllowedApplications()
    {
        var root = Path.Combine(Path.GetTempPath(), "lock-in-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var store = new SessionStore(root);
            var now = DateTimeOffset.UtcNow;
            await store.SaveAsync(new FocusSession
            {
                MissionName = "Exam study",
                StartedAtUtc = now,
                EndsAtUtc = now.AddMinutes(50),
                StrictMode = true,
                AllowedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Code.EXE"
                }
            });

            var restored = store.LoadActive();

            Assert.IsNotNull(restored);
            Assert.IsTrue(restored.StrictMode);
            Assert.AreEqual("Exam study", restored.MissionName);
            CollectionAssert.AreEquivalent(
                new[] { "Code" },
                restored.AllowedProcessNames.ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void LoadActive_WhenSessionFileIsTemporarilyLocked_DoesNotCrashStartup()
    {
        var root = Path.Combine(Path.GetTempPath(), "lock-in-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sessionPath = Path.Combine(root, "active-session.json");
        File.WriteAllText(sessionPath, "{}");

        try
        {
            var store = new SessionStore(root);
            using var lockedSession = new FileStream(
                sessionPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            Assert.IsNull(store.LoadActive());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
