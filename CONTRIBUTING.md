# Contributing

Thank you for helping improve EnergyControl for Lenovo.

## Development rules

- Target Windows 10/11 x64 and .NET Framework 4.8.
- Do not commit or distribute Lenovo proprietary DLLs, extracted firmware, private symbols, or reverse-engineering tooling.
- Keep direct charging and experimental optional features isolated so an Addin/RPC failure cannot disable the stable path.
- Keep all writes behind the explicit `--apply` check and preserve read-after-write verification.
- Add or update simulated protocol tests instead of touching real hardware in CI.
- Do not add telemetry, network calls, background services, auto-start, or an installer without a separate design decision.

## Local checks

```powershell
.\build.ps1 -Clean
.\tests\run-tests.ps1
.\verify-layout.ps1
```

The layout script covers narrow windows and 150%/200% DPI. A real device may be used for manual read-only diagnostics, but automated tests must not perform driver writes.

## Pull requests

Explain the user-visible behavior, compatibility assumptions, and test commands. If a change affects the release payload, show that the final ZIP contains only the intended portable files and includes a SHA-256 checksum.
