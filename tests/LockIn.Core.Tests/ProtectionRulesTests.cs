using LockIn.Core.Models;
using LockIn.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.Core.Tests;

[TestClass]
public sealed class ProtectionRulesTests
{
    [DataTestMethod]
    [DataRow("explorer")]
    [DataRow("taskmgr.exe")]
    [DataRow("Narrator")]
    [DataRow("SecurityHealthSystray")]
    public void CriticalAndRecoveryProcessesAreAlwaysAllowed(string processName)
    {
        Assert.IsTrue(ProtectionRules.IsAlwaysAllowed(processName, "LOCK-IN"));
    }

    [TestMethod]
    public void ExplicitAllowedAppIsAllowed()
    {
        var session = new FocusSession
        {
            AllowedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "code"
            }
        };

        Assert.IsTrue(ProtectionRules.IsAllowed(session, "code.exe", "LOCK-IN"));
    }

    [TestMethod]
    public void UnlistedInteractiveAppIsNotAllowedByRule()
    {
        var session = new FocusSession();

        Assert.IsFalse(ProtectionRules.IsAllowed(session, "steam", "LOCK-IN"));
    }
}
