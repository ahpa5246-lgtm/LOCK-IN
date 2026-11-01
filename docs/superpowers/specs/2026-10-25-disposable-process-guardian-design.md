# Disposable Process Guardian Design

**Date:** 2026-10-25  
**Repository:** `ahpa5246-lgtm/LOCK-IN`  
**Branch:** `codex/focused-desktop-workspaces`

## Intent

Prove that LOCK-IN can terminate one genuinely running, disallowed Windows desktop process while preserving the current safety rules and without scanning or harming unrelated runner applications.

## Goals

- Launch a disposable WPF window owned entirely by the test.
- Exercise the production guardian's eligibility, allow-list and termination path against that exact process.
- Prove the process exits and `ProcessBlocked` reports its normalized name.
- Keep the existing full Windows build, package and installer pipeline green.

## Non-goals

- Do not scan or kill every interactive process on the CI runner.
- Do not test a user's installed application or unsaved data.
- Do not change the always-allowed list or Strict Mode semantics.
- Do not claim close-resistance, restart/resume, screenshots or visual comparison are verified by this milestone.
- Do not add external packages, deploy, merge or publish.

## Current behavior

`ProcessGuardian.Scan` enumerates all processes in the current Windows session, ignores the guardian process and non-window processes, normalizes names, checks `ProtectionRules`, calls `Process.Kill(entireProcessTree: true)`, then emits `ProcessBlocked`. That behavior is production-relevant but cannot be safely targeted by the existing test suite.

## Approaches considered

1. **Inject fake processes.** Deterministic and portable, but it would only prove a mock was marked killed; it would not close the documented actual-process gap.
2. **Run the existing full session scan in CI.** Exercises the real loop, but could terminate an unrelated interactive process owned by the Windows runner.
3. **Extract the existing single-process decision/kill path and target a disposable WPF PowerShell window.** Selected. The test creates one visible window, passes only that owned `Process` to the production path, verifies its termination and always cleans it up.

## Design

Extract an internal `TryBlockProcess(Process process, FocusSession session, int currentSessionId)` method from the body of `Scan`. `Scan` continues enumerating and handling the same exception classes; it delegates each candidate to this method, so production semantics stay unchanged.

Grant the existing `LockIn.Core.Tests` assembly access to the internal method. The Windows test writes a temporary PowerShell script that opens a small WPF window, starts `powershell.exe` without a console, waits with a bounded timeout for a non-zero `MainWindowHandle`, and invokes the targeted production method. It asserts the method reports a block, the process exits within five seconds, and the event name is `powershell`. A `finally` block kills any surviving helper and deletes the script.

## Safety and failure handling

- The test never calls the global scan loop.
- The only process passed to the kill path is the process returned by the test's own `Process.Start`.
- Startup and window readiness have bounded timeouts.
- Cleanup runs whether assertions pass or fail.
- CI failure is the correct outcome if the hosted Windows environment cannot create an interactive WPF window; the limitation will be recorded rather than bypassed.

## Acceptance

A test-only commit first fails because the targeted production method does not exist. After the minimal extraction, GitHub Actions on `windows-latest` must build with zero warnings/errors, pass the complete test suite including the real disposable-process termination, publish the self-contained `win-x64` app, build the Inno Setup installer and upload the existing artifact. Exact run and artifact evidence is recorded in `docs/DEVELOPMENT_REVIEW.md`.

## Remaining boundary

Close resistance, saved Strict-session restart/resume, real 3/720-minute WPF entry, keyboard navigation, 1100px/760px screenshots and approved original art remain separate milestones.
