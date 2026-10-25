# Disposable Process Guardian Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove LOCK-IN terminates one owned, disallowed Windows desktop process through its production guardian path without scanning unrelated applications.

**Architecture:** Extract the current per-process eligibility, allow-list and kill sequence into one internal method reused by the existing global scan. A Windows-only MSTest creates a disposable WPF PowerShell window and passes exactly that returned `Process` to the production method.

**Tech Stack:** .NET 8, C#, MSTest 3.6.2, Windows PowerShell/WPF, GitHub Actions `windows-latest`

**Spec:** `docs/superpowers/specs/2026-10-25-disposable-process-guardian-design.md`

## Global Constraints

- Continue `codex/focused-desktop-workspaces` and PR #2; do not create duplicate work.
- The test may kill only a process it created and holds by PID.
- Never call the guardian's global scan loop from the integration test.
- Add no NuGet dependency and change no allow-list or Strict Mode behavior.
- Bound helper startup and exit waits and always clean up the helper and temporary script.
- Keep the PR draft; do not merge, deploy or publish a release.
- Record exact RED, GREEN and artifact evidence in `docs/DEVELOPMENT_REVIEW.md`.

## Review Focus

- Helper window never appears: fail within ten seconds and clean up.
- Target process exits before the assertion reads its event: collect the event before invoking the guardian.
- Helper remains alive after a failed assertion: `finally` kills the exact stored PID tree.
- An allowed process must not be terminated: existing `ProtectionRules` remains the sole decision source.
- Production scan behavior drifts during extraction: `Scan` must delegate to the same targeted method and preserve its existing exception boundary.

---

### Task 1: Targeted real-process termination

**Files:**
- Create: `tests/LockIn.Core.Tests/ProcessGuardianTests.cs`
- Create: `src/LockIn.Core/Properties/AssemblyInfo.cs`
- Modify: `src/LockIn.Core/Services/ProcessGuardian.cs`
- Modify: `docs/DEVELOPMENT_REVIEW.md`

**Interfaces:**
- Consumes: `ProtectionRules.IsAllowed(FocusSession, string, string?)` and the existing `ProcessBlocked` event.
- Produces: internal `bool ProcessGuardian.TryBlockProcess(Process process, FocusSession session, int currentSessionId)`, reused by `Scan`.

- [ ] **Step 1: Write the failing Windows test**

Create `TargetedGuardianTerminatesOwnedDisallowedWindowProcess`. The test writes a temporary PowerShell script that opens a 320×180 WPF window, starts `powershell.exe` with `-NoProfile -STA -ExecutionPolicy Bypass`, waits at most ten seconds for `MainWindowHandle != IntPtr.Zero`, subscribes to `ProcessBlocked`, and calls `TryBlockProcess` with the helper process and the current session ID.

Assert:
- the method returns `true`;
- the helper exits within five seconds;
- exactly one event is raised with normalized name `powershell`.

Use `finally` to kill the exact helper process tree if it survives and delete the temporary script.

- [ ] **Step 2: Verify RED remotely**

Run: GitHub Actions CI on the test-only commit.  
Expected: restore succeeds, then build fails because `ProcessGuardian.TryBlockProcess` is missing. No production file changes precede this run.

- [ ] **Step 3: Expose the test boundary**

Create `src/LockIn.Core/Properties/AssemblyInfo.cs` with `InternalsVisibleTo("LockIn.Core.Tests")`.

- [ ] **Step 4: Extract the minimal production method**

Implement:

```csharp
internal bool TryBlockProcess(
    Process process,
    FocusSession session,
    int currentSessionId)
```

The method returns `false` unless `ShouldInspect` accepts the exact process and `ProtectionRules.IsAllowed` rejects its normalized name. Otherwise it calls `Kill(entireProcessTree: true)`, raises one `BlockedProcessEvent`, and returns `true`. Keep the existing exception handling around each call in `Scan`.

- [ ] **Step 5: Verify GREEN**

Run: GitHub Actions CI.  
Expected: Release build has zero warnings/errors; the complete suite passes including the disposable WPF process test; publish, installer build and artifact upload succeed.

- [ ] **Step 6: Inspect and document evidence**

Record test count, exact RED/GREEN run IDs, implementation commit, artifact ID/size/SHA-256, and the remaining close-resistance, restart/resume, WPF-boundary-input, keyboard, screenshot and art gaps in `docs/DEVELOPMENT_REVIEW.md`. Confirm PR #2 remains open, draft and unmerged.

- [ ] **Step 7: Commit**

Keep the test-only RED, production implementation, and evidence documentation as separate bounded commits on the existing branch.
