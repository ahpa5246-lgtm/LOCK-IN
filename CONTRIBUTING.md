# Contributing

Thanks for improving LOCK-IN.

## Development

1. Use Windows 10/11 with the .NET 8 SDK.
2. Create a feature branch.
3. Run dotnet restore.
4. Run dotnet build -c Release.
5. Run dotnet test -c Release.
6. Open a pull request.

## Guardrails

Changes to enforcement behavior require extra care:

- never target Windows services or processes from another user session,
- never block Task Manager or accessibility/recovery tools,
- never silently elevate privileges,
- add or update tests for protection-rule changes.

## Style

The project uses nullable reference types, implicit usings, deterministic builds, and treats warnings as errors.
