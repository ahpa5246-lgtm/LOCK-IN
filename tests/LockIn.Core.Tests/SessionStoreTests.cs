using LockIn.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.Core.Tests;

[TestClass]
public sealed class SessionStoreTests
{
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
