# Session Telemetry

## Purpose
Maintained `_Game` sessions now write structured telemetry so agents and developers can inspect what happened without replaying the session manually.

## Location
- `Logs/Telemetry/session-latest`
- `Logs/Telemetry/session-YYYYMMDD-HHMMSS`

## Files
- `session.json`
  - session metadata and summary
- `actions.jsonl`
  - gameplay and lifecycle actions
- `ui-actions.jsonl`
  - UI-triggered actions, including HUD panel and control source ids for human actions
- `checks.jsonl`
  - invariant checks and validation results
- `snapshots/*.json`
  - pre/post world snapshots captured by the telemetry service
- `diffs/*.json`
  - semantic world diffs keyed to action ids
- `manual-test.json`
  - present when a guided manual run is linked to the current session

## Action lifecycle
1. A world-mutating command begins an action scope.
2. The pre-action world snapshot is captured.
3. The command executes.
4. The post-action world snapshot is captured.
5. A semantic diff is written.
6. Invariant checks run and append records to `checks.jsonl`.

## Correlation model
Records are keyed by:
- `sessionId`
- `actionId`
- `uiSource.panelId`
- `uiSource.elementId`
- entity and world ids already present in shared contracts

Additional human/agent interface correlation fields:
- `logicalActionId`
  - shared gameplay action id used by both HUD and MCP
- `contextSummary`
  - short contextual snapshot recorded with the action

The telemetry layer now also stores human/agent parity records for:
- HUD-triggered gameplay actions
- MCP gameplay queries
- MCP gameplay actions
- blocked/unavailable action explanations

## Human + MCP parity rule
If an action is blocked for a human or an MCP caller, the explicit blocker reason must be recorded and queryable.

Examples:
- `Requires Residential floor`
- `Requires enclosed room`
- `No reachable source`
- `Target edge is required`

## Analysis order
1. `session.json`
2. `actions.jsonl`
3. `ui-actions.jsonl`
4. `checks.jsonl`
5. `diffs/`
6. raw runtime log if needed

## Server-facing intent
This telemetry pipeline is also the first version of an audit/event stream for future authoritative server integration. Keep outputs semantic and machine-readable.
