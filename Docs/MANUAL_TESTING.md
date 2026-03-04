# Manual Testing

## Runner
Use:
- `Window > GGJ2026 > QA > Manual Test Runner`
- `Window > GGJ2026 > QA > HUD State Browser` when comparing the live human HUD with the shared action/query surface exposed to MCP

The runner is a UI Toolkit editor window that loads scenarios from:
- `Assets/_Game/Authoring/ManualTests`

## Output
Manual runs write:
- `Logs/Telemetry/manual-tests/manual-test-latest.json`
- `Logs/Telemetry/manual-tests/manual-test-YYYYMMDD-HHMMSS.json`
- `Logs/Telemetry/session-latest/manual-test.json` when a live telemetry session is present

## Workflow
1. Choose a scenario.
2. Start the run.
3. Execute the prompted step in Unity.
4. Mark the result as `Pass`, `Fail`, `Blocked`, or `Skipped`.
5. Leave notes when behavior is unclear.
6. Finish the run to persist the artifact.

For interface-focused scenarios:
1. inspect the live runtime HUD
2. inspect the same state in `HUD State Browser`
3. if relevant, compare the same state through MCP gameplay queries
4. record any mismatch between:
   - selection/context
   - available actions
   - blocker explanations
   - last action/result wording

For world-interaction scenarios, also verify:
1. what the hover label says before clicking
2. what becomes selected after clicking
3. whether the contextual `Next` actions match the selected target
4. whether wall placement blocks only the final inaccessible seal, not normal enclosure steps
5. whether doors remain visually distinct from walls in the world view

## Scenario expectations
Each step should define:
- required scene
- instruction
- expected result
- sanity checks
- whether telemetry evidence is expected
- whether a screenshot is required

## Current seeded scenarios
- `foundation-ui-sanity`
- `placement-and-shelter-sanity`
- `human-agent-parity-sanity`
- `barracks-and-raid-sanity`
- `onboarding-and-presentation-sanity`
- `selection-and-access-guard-sanity`

These cover:
- baseline UI presence
- input blocking
- placement/shelter rules
- human HUD and MCP parity
- barracks readiness, defender orders, soldier training, and raid outcome visibility
- onboarding comprehension, skip/restart behavior, and readable movement/work animation
- selection clarity, world-space labels, and the room-access wall guard
- contextual `Next` actions and low-clutter target progression
