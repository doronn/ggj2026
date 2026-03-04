# Log Order

## Compile failures

1. `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Diagnostics/compile-latest.log`
2. `/Users/doronnacash/Library/Logs/Unity/Editor.log`

## Runtime behavior in `_Game`

1. `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Telemetry/session-latest/session.json`
2. `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Telemetry/session-latest/actions.jsonl`
3. `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Telemetry/session-latest/checks.jsonl`
4. `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/PersistentWorld/runtime-latest.log`

## Test failures

1. Requested NUnit XML in `Logs/TestResults`
2. Bridge response or status
3. `Editor.log`

## MCP and control issues

1. `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/McpBridge/status.json`
2. `Logs/McpBridge/responses/*.json`
3. `Editor.log`

## Common false positives

- `Multiple unexpected errors happened while running tests.` can be post-success cleanup noise for live play-mode tests if recovered NUnit XML is clean.
- Old `runtime-latest.log` content may describe a previous session; compare session ids and timestamps.
- `All compiler errors have to be fixed before you can enter playmode!` can be stale after a later successful compile; verify with `compile-latest.log`.
