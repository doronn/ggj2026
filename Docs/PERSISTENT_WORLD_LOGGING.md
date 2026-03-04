# Persistent World Logging

Runtime playtest logs are mirrored into the repo so they can be inspected without copying Unity Console output.

Structured telemetry is also written for maintained `_Game` sessions. Raw runtime logs are useful, but they are not the only source of truth anymore.

## Latest log
- `Logs/PersistentWorld/runtime-latest.log`

## Session logs
- `Logs/PersistentWorld/runtime-YYYYMMDD-HHMMSS.log`

## Structured telemetry
- `Logs/Telemetry/session-latest/session.json`
- `Logs/Telemetry/session-latest/actions.jsonl`
- `Logs/Telemetry/session-latest/ui-actions.jsonl`
- `Logs/Telemetry/session-latest/checks.jsonl`
- `Logs/Telemetry/session-latest/snapshots/`
- `Logs/Telemetry/session-latest/diffs/`

## Captured events
- Unity log messages, warnings, and errors.
- Stack traces when Unity provides them.
- Scene changes during play mode.
- Structured UI/gameplay actions with correlated state diffs.
- Invariant checks after mutating actions.
- Manual test artifacts when a guided manual run is active.

## Notes
- The recorder installs automatically before the first scene loads.
- Structured telemetry is the canonical source for maintained `_Game` session analysis.
- Runtime logs complement the Unity Console; they do not replace editor logs or package manager logs.
- Editor shortcuts:
  - `Window > GGJ2026 > Runtime Logs > Reveal Latest Log`
  - `Window > GGJ2026 > Runtime Logs > Copy Latest Log Path`
