# Changelog

All notable changes to EnergyControl for Lenovo are documented here.

## [Unreleased]

- Organize the desktop interface into battery, performance, keyboard and diagnostics tabs.
- Split device aggregation, layout and UI operations into separate modules.
- Fix backlight actions staying disabled after refresh and reject unknown switch states.
- Refresh bilingual documentation with a dependency matrix, command reference and GUI screenshot.
- Verify all tabs and capability transitions at multiple window sizes and DPI scales.

## [0.1.0-preview.1] - 2026-09-18

### Added

- Single portable `EnergyControl.exe` for GUI and CLI use.
- Direct `EnergyDrv` charge mode read/write path with command whitelist, capability checks, and post-write verification.
- Runtime-optional Lenovo Addin and Power RPC reflection adapters.
- Experimental custom charge threshold UI and CLI with graceful unavailable/read-only states.
- Bilingual documentation, GPL-3.0-only licensing, diagnostics, privacy and security guidance.
- Protocol simulation tests, CLI safety checks, DPI layout tests, dependency scanning, and prerelease packaging workflow.

### Notes

- This is an unsigned preview release and may trigger Windows SmartScreen.
- No Lenovo private DLL, CI stub, reverse-engineering tool, or debug log is included in the release ZIP.
