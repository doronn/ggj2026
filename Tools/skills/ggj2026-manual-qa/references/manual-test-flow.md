# Manual Test Flow

## Assets

- Scenario definition type:
  - `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Assets/_Game/Authoring/ManualTests/ManualTestScenarioDefinition.cs`
- Current seeded scenarios:
  - `FoundationUiSanity.asset`
  - `PlacementAndShelterSanity.asset`

## Execution paths

- Editor window:
  - `Window > GGJ2026 > QA > Manual Test Runner`
- MCP bridge:
  - `unity_list_manual_tests`
  - `unity_manual_test_status`
  - `unity_start_manual_test`
  - `unity_record_manual_test_step`
  - `unity_finish_manual_test`

## Artifacts

- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Telemetry/manual-tests/manual-test-latest.json`
- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Telemetry/session-latest/manual-test.json`

## Good step design

- One observable action per step
- Explicit scene or setup requirement
- Expected result stated in user-visible terms
- Sanity check that helps the tester catch partial success
