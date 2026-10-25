# Strict pre-flight shell acceptance design

## Intent

Prove that the real WPF session-setup shell protects an already-open application before Strict Mode starts. The acceptance journey must exercise the visible Start Mission action, production session validation, and production process eligibility rules while limiting enumeration to one disposable process created by the test.

## Goals

- Run on the existing `windows-latest` CI job with the real `MainWindow` and a real owned PowerShell/WPF helper process.
- Show the existing `Pre-flight check` message with the helper's normalized process name.
- Leave the helper alive, keep the focus panel idle, and avoid writing `active-session.json`.
- Give the shell an isolated temporary data root so the test cannot read or overwrite a real user's state.
- Preserve the default production constructor and unrestricted production enumeration.

## Non-goals

- Do not start Strict Mode, terminate the helper, test close resistance, or test restart/resume in this milestone.
- Do not automate arbitrary runner windows or call the guardian's unrestricted scan from the test.
- Do not change the allow-list, duration validation, layout, or product copy.
- Do not claim complete desktop interaction or release readiness from an in-process WPF acceptance test.

## Constraints

- Continue `codex/focused-desktop-workspaces` and draft PR #2.
- The only non-system process visible to the injected test enumeration is the exact helper PID.
- The zero-argument `MainWindow` and zero/one/two-argument `ProcessGuardian` call patterns retain their current production behavior.
- Dialog indirection must preserve the same WPF `MessageBox` title, message, buttons, and icon in production.
- No new NuGet dependency, deployment, merge, secret, or paid service.

## Approaches considered

1. **External UI Automation against the packaged executable.** This is closest to an end-user launch, but starting Strict Mode would allow the global guardian to inspect unrelated runner processes, and Windows UI Automation setup would add another dependency. Rejected for this safety-bounded milestone.
2. **Core-only pre-flight test.** This can verify process names but cannot prove that the WPF Start Mission action blocks persistence and keeps the shell idle. Rejected as insufficient.
3. **Real WPF shell with injected boundaries.** Add an internal constructor for data root, production guardian, and dialog boundary; add an optional process provider to `ProcessGuardian`. Production keeps its existing defaults, while the test supplies a fresh wrapper for only its owned helper PID. Chosen because it exercises the real shell and production rules without exposing unrelated processes.

## Acceptance contract

`StrictPreflightShellTests.PreflightLeavesOwnedDisallowedWindowOpenAndDoesNotPersistSession` runs on an STA thread. It creates and shows a disposable PowerShell WPF window, constructs `MainWindow` against a temporary data root and a production `ProcessGuardian` whose provider returns only that PID, fills the named setup controls, and raises the real Start Mission click.

The test passes only when one informational dialog titled `Pre-flight check` names `powershell.exe`, the helper remains alive, `IdlePanel` remains visible, `ActivePanel` remains collapsed, and no active-session file exists. Cleanup closes the shell, kills only the stored helper PID if needed, and removes the temporary script/root.

## Remaining work after this milestone

The next independent journey may allow the disposable process, restore a persisted Strict session, prove that active scanning terminates that one helper, reject early close, and verify restart/resume. Native packaged-executable automation, 1100px/760px screenshots, keyboard navigation, Arabic localization, schedules, and original illustration remain separate gates.
