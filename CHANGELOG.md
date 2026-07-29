# Changelog

All notable changes to JFlightShaker C.E are documented here.

The project follows [Semantic Versioning](https://semver.org/).

## [0.5.0-beta.1] - 2026-07-28

### Added

- Redesigned multilingual interface.
- Per-effect preview, tooltip, percentage and real-time Active status.
- Independent left/right shaker output and stereo roll response.
- Missile, Pitch & Roll and High-G effects.
- Controller and keyboard Mute binding with Trigger and Hold modes.
- Generic live device tester and X56 profile.
- Automatic DirectInput reconnection and audio connection indicator.
- Per-effect reset defaults.
- Automatic malformed-configuration recovery and local diagnostic logging.
- Automated tests for critical effect calculations.
- GitHub community files, CI and release tooling.

### Changed

- X56 dual throttle levers now contribute independently: 50% each.
- Pitch & Roll strength scales moderately with throttle.
- Throttle ducks progressively under higher-priority effects.
- High-G uses pitch rather than roll and includes hysteresis.

### Fixed

- DirectInput slider detection and X56 axis mappings.
- Throttle midpoint/full-range regressions.
- Missile tail muting other effects after completion.
- Empty configuration crash on Start.
- WPF window icon startup failure.
- Device test button/hat numbering and X56 rotary mappings.

## [0.4.0] - 2026-07-25

- First Community Edition build with branded icon and complete core effect set.
