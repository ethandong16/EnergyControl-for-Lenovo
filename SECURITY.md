# Security policy

## Supported versions

Only the latest preview on the `main` branch is supported during the preview period.

## Reporting a vulnerability

Please do not publish an exploitable driver or privilege-escalation issue in a public issue. Use GitHub's private security advisory flow for this repository, or contact the maintainers privately before disclosure.

Include the affected version, Windows build, device model, reproduction steps, and whether the issue requires an installed Lenovo component. Do not attach private Lenovo binaries or personal diagnostic logs.

EnergyControl requests no administrator elevation and has no telemetry or network reporting. Driver writes are restricted to a small known command allowlist and are always guarded by `--apply` in CLI mode or a GUI confirmation.
