# Architecture

LOCK-IN is deliberately small and auditable.

## LockIn.Core

- FocusSession defines and validates a focus mission.
- SessionStore persists active session state atomically.
- ProcessGuardian watches interactive applications in the current Windows session.
- ProtectionRules keeps a conservative Windows recovery allow-list.
- StatsStore records completed missions locally and idempotently.

## LockIn.App

The WPF desktop shell provides mission setup, app selection, countdown, boss-health visualization, Strict Mode close interception, optional per-user startup registration, local crash logging, and session restore.

## Design principle

The application is intentionally user-mode and conservative. It is designed to add commitment friction without changing Windows security policy or taking administrative control away from the device owner.

## Extension points

Future versions can add browser site allow-listing, reusable profiles, local notifications, signed releases, and an Android companion while keeping the same local-first design.
