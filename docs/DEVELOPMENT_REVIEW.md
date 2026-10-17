# LOCK-IN: engineering review, 2026-10-18

## Scope and intent
Owner requested continued implementation and severe comparison with released products. This is the first bounded repair milestone, not a full completion claim. Local/offline architecture is retained; no new paid service or deployment.

Core journey: Session setup → allowed applications → active focus → progress/settings.

## Decision and implementation
Replace crowded two-column dashboard with four named destinations using existing working controls. Starting/restoring a session opens Focus automatically. Keep existing process guardian safety behavior and restrictions. No empty scheduling page is invented.

Alternative: decorate the existing home page while leaving workflows and state bugs unchanged. Rejected because appearance cannot repair lost results or crowded controls. Existing architecture remains intact.

## Reference research
Official feature/product pages consulted for task hierarchy and separation of primary work from setup/reference. No source, artwork, icon or layout copied:
https://getcoldturkey.com/features/
https://freedom.to/features
https://opalapp.com/

Web-text inspection is not a visual screenshot comparison. These sources are a starting shortlist, not completed visual evidence. Do not mark the comparative design gate passed. A missing or inaccessible visual reference remains an open item.

## Evidence and harsh self-review
XAML parses as XML. .NET SDK/Windows unavailable; compilation, interaction, screenshots and enforcement remain unverified. Existing Windows CI must pass. This is a draft, not visual approval.

The Senior Skills example visual gate was run and correctly returned BLOCKED_UNVERIFIED_DESIGN; its draft lacks verified asset licenses. That result is framework evidence, not a project-specific visual pass. Actual screenshots, app-commit binding, keyboard/RTL checks, and task observations are required before release readiness.

## Next acceptance boundary
Build on Windows, test preflight and strict mode, inspect 1100px and 760px windows, then commission original purposeful illustration. Arabic localization and schedules remain absent.

Rollback: revert this milestone branch before merge. Do not merge or deploy automatically. Future runs must inspect open PRs and continue this branch before creating duplicate work.
