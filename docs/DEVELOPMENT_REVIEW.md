# LOCK-IN: engineering review, 2026-10-18

## Scope and intent
Owner requested continued implementation and severe comparison with released products. This is the first bounded repair milestone, not a full completion claim. Local/offline architecture is retained; no new paid service or deployment.

Core journey: Session setup → allowed applications → active focus → progress/settings.

## Decision and implementation
Replace crowded two-column dashboard with four named destinations using existing working controls. Starting/restoring a session opens Focus automatically. Keep existing process guardian safety behavior and restrictions. No empty scheduling page is invented.

The persistence follow-up covers the existing crash/restart promise rather than adding another screen. A saved Strict session is exercised through the real `SessionStore`, including its normalized application allow-list. If Windows temporarily denies access to `active-session.json`, startup now treats the session as unavailable instead of propagating `IOException` and terminating the app; the store still attempts its existing quarantine path, which safely tolerates the same file lock.

The custom-duration stage replaces fixed presets with direct minute entry while preserving the owner-approved inclusive 3–720 minute boundary. Parsing and validation now live in `LockIn.Core.Services.FocusDuration`; the WPF start flow calls that tested parser instead of duplicating untestable window-local rules.

Alternative: decorate the existing home page while leaving workflows and state bugs unchanged. Rejected because appearance cannot repair lost results or crowded controls. Existing architecture remains intact.

## Reference research
Official feature/product pages consulted for task hierarchy and separation of primary work from setup/reference. No source, artwork, icon or layout copied:
https://getcoldturkey.com/features/
https://freedom.to/features
https://opalapp.com/

Web-text inspection is not a visual screenshot comparison. These sources are a starting shortlist, not completed visual evidence. Do not mark the comparative design gate passed. A missing or inaccessible visual reference remains an open item.

## Evidence and harsh self-review
The existing Windows CI already restores, builds, tests, publishes a self-contained Windows x64 application and builds the Inno Setup installer. Run 39078795715 passed all of those steps for the workspace split and produced the 126,380,504-byte `LOCK-IN-windows` artifact with recorded SHA-256 digest `9a4201454b84e5d76a4f10bfb4b2e1a2cabbb3bf3a6e3ccc380211739954f9ac`.

The locked-session regression was isolated in test-only run 39108261432: the solution built with zero warnings/errors, 11 tests passed, and the new test failed at `SessionStore.LoadActive` with `System.IO.IOException` from `File.ReadAllText`. After the bounded fix and Strict-session round-trip test, Windows run 39108399806 built with zero warnings/errors and passed all 13 tests (0 failed, 0 skipped). The same run published the self-contained `win-x64` application, compiled the Inno Setup installer and uploaded the 126,414,611-byte `LOCK-IN-windows` artifact with SHA-256 digest `e6f69525fb44be1892cdec61ef62b2550f9675d164ab5309d93358639b3e7d74`. CI compilation is not actual desktop interaction: process termination, close resistance, keyboard navigation, screenshots and responsive window states remain unverified.

The user-defined duration validation followed a separate RED→GREEN cycle. Test-only run 39182180128 restored dependencies, compiled `LockIn.Core` and `LockIn.App`, then failed the solution build with two expected `CS0103` errors because the new boundary tests referenced the not-yet-created `FocusDuration` parser. GREEN run 39187683419 on code commit `7f7bf36` built Release with 0 warnings and 0 errors, passed 20/20 tests (0 failed, 0 skipped), published the self-contained `win-x64` app, built the Inno Setup installer, and uploaded artifact 12157859604 (`LOCK-IN-windows`, 126,422,852 bytes, SHA-256 `790cb37d65fe7a961237c559c8d56f5460096789eaef4bf4d72fd67d47aafaac`). The parser tests cover accepted 3, 25 and 720 minute values plus empty, non-numeric, 2 and 721 minute rejection. The current evidence proves the validation contract and package build, not real WPF text entry or countdown behavior at the two boundary values. Actual process termination, close resistance, keyboard navigation, 1100px/760px screenshots and comparative visual review remain unresolved. GitHub's Node 20 action-runtime deprecation warning remains a maintenance item rather than a product-test failure.

The disposable-process guardian stage used a targeted real-process harness rather than a mocked kill or an unrestricted system scan. Test-only run 40173174627 failed at compile time with the expected `CS1061` because the new test referenced the not-yet-created internal `TryBlockProcess` seam. GREEN run 40175893878 on code commit `19a3c1d` built Release with 0 warnings and 0 errors, then passed 21/21 tests (0 failed, 0 skipped) on Windows Server 2025. The new test launched an owned PowerShell STA process with a visible WPF window, passed only that process through Strict Mode protection, observed the `ProcessBlocked` event, and proved the exact disposable process exited. The same run published the self-contained `win-x64` application, compiled `LOCK-IN-Setup.exe`, and uploaded artifact 12592290761 (`LOCK-IN-windows`, 126,488,721 bytes, SHA-256 `1cae9db60b4cac95b1f271340082cc6d2add362abd6ee9908373304d3fc7861f`). This verifies real targeted termination without risking unrelated runner processes; it does not verify full desktop scanning, user-visible preflight, close resistance, or restart/resume.

The Senior Skills example visual gate was run and correctly returned BLOCKED_UNVERIFIED_DESIGN; its draft lacks verified asset licenses. That result is framework evidence, not a project-specific visual pass. Actual screenshots, app-commit binding, keyboard/RTL checks, and task observations are required before release readiness.

## Next acceptance boundary
Exercise user-visible preflight and active Strict Mode scanning against a disposable Windows application, then verify close resistance and reopen/resume through the real desktop shell. Inspect 1100px and 760px windows, then commission original purposeful illustration. Arabic localization and schedules remain absent.

Rollback: revert this milestone branch before merge. Do not merge or deploy automatically. Future runs must inspect open PRs and continue this branch before creating duplicate work.
