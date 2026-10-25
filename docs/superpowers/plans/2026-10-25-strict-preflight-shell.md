# Strict Pre-flight Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Verify the real WPF Start Mission action refuses to begin or persist a Strict session while one owned, disallowed desktop process is open.

**Architecture:** Keep production defaults intact, but add narrow constructor injection for the app data root and dialog boundary, plus an optional process provider in `ProcessGuardian`. The Windows-only acceptance test uses the real WPF shell and production guardian against exactly one disposable PowerShell/WPF process.

**Tech Stack:** .NET 8, WPF, MSTest 3.6.2, Windows PowerShell/WPF, GitHub Actions `windows-latest`

**Spec:** `docs/superpowers/specs/2026-10-25-strict-preflight-shell-design.md`

## Global Constraints

- Continue `codex/focused-desktop-workspaces` and PR #2; do not create duplicate work.
- The test may enumerate or terminate only the process it creates and holds by PID.
- Do not run the unrestricted guardian scan from the acceptance test.
- Preserve production process enumeration and MessageBox behavior through the existing public constructors.
- Add no NuGet dependency and change no allow-list, timing, layout, or copy.
- Keep the PR draft; do not merge, deploy, publish a release, or use secrets.

## Review Focus

- Helper never creates a window: fail within ten seconds and clean up the exact PID.
- Process provider returns a disposed instance: return a fresh `Process.GetProcessById` wrapper on every call.
- Dialog path accidentally starts a session: assert both panel state and absence of `active-session.json`.
- Test boundary leaks into production behavior: verify default constructors still use LocalAppData, WPF MessageBox, and `Process.GetProcesses()`.
- Test failure strands windows or files: cleanup closes the shell, kills the exact helper tree, and deletes the script/root.

---

### Task 1: Strict pre-flight shell acceptance

**Files:**
- Create: `tests/LockIn.App.Tests/LockIn.App.Tests.csproj`
- Create: `tests/LockIn.App.Tests/StrictPreflightShellTests.cs`
- Modify: `LOCK-IN.sln`
- Modify: `src/LockIn.Core/Services/ProcessGuardian.cs`
- Create: `src/LockIn.App/Services/IUserDialog.cs`
- Create: `src/LockIn.App/Properties/AssemblyInfo.cs`
- Modify: `src/LockIn.App/MainWindow.xaml.cs`
- Modify: `docs/DEVELOPMENT_REVIEW.md`

**Interfaces:**
- Consumes: `FocusDuration.ParseMinutes`, `FocusSession.Validate`, `ProtectionRules.IsAllowed`, `SessionStore.SaveAsync`, and named WPF setup/panel elements.
- Produces: `IUserDialog.Show(string, string, MessageBoxButton, MessageBoxImage)`, internal `MainWindow(string dataRoot, ProcessGuardian guardian, IUserDialog dialogs)`, and optional `Func<IReadOnlyList<Process>> processProvider` in the existing `ProcessGuardian` constructor.

- [ ] **Step 1: Write the failing Windows acceptance test**

Create `PreflightLeavesOwnedDisallowedWindowOpenAndDoesNotPersistSession`. Start a 320×180 PowerShell/WPF helper, wait at most ten seconds for its window, construct the real shell with an isolated root, a recording dialog, and a production guardian limited to fresh wrappers for the helper PID. Fill mission/duration/Strict controls and raise the Start button click. Assert one `Pre-flight check` dialog names `powershell.exe`, the helper remains alive, idle/active panels are unchanged, and no session file exists. Always clean up.

- [ ] **Step 2: Verify RED remotely**

Run GitHub Actions CI on the test-only commit. Expected: restore reaches the new project, then build fails because `IUserDialog`, the injectable `ProcessGuardian` provider, and injectable `MainWindow` constructor do not exist. Existing production files must be unchanged.

- [ ] **Step 3: Add the minimal boundaries**

Add `IUserDialog` with the exact WPF MessageBox signature and a production implementation. Chain the existing `MainWindow()` to the new internal constructor with LocalAppData, default guardian, and production dialog. Add the optional process provider to `ProcessGuardian`, defaulting to `Process.GetProcesses`, and use it in both pre-flight discovery and scans.

- [ ] **Step 4: Route existing dialogs without changing behavior**

Replace direct `MessageBox.Show` calls in `MainWindow` with the injected boundary, preserving every current message, title, button set, icon, and result branch. Add `InternalsVisibleTo("LockIn.App.Tests")`.

- [ ] **Step 5: Verify GREEN**

Run GitHub Actions CI. Expected: Release build has zero warnings/errors; the full Core and App test suites pass; publish, installer build, and artifact upload succeed. The new test must report the helper alive after pre-flight and must never scan unrelated runner processes.

- [ ] **Step 6: Inspect and document evidence**

Record RED/GREEN run IDs, test counts, implementation commit, artifact ID/size/SHA-256, explicit limitations, and PR state in `docs/DEVELOPMENT_REVIEW.md` and the draft PR body.

- [ ] **Step 7: Commit strategy**

Keep design/plan, test-only RED, minimal production implementation, and evidence documentation as separate commits on the existing branch.
