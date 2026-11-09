# MCP Server Workflow

## Purpose
The project-owned MCP server is the control plane for Unity project operations that need to be automated cleanly:

- project status inspection
- compile
- edit-mode and play-mode tests
- UI Toolkit plan apply/preview
- scene open
- play mode enter/exit
- manual test discovery and guided execution
- direct access to compile/runtime/test/telemetry artifacts through MCP resources
- gameplay HUD/context/blocker queries
- gameplay action execution through the same command layer the human HUD uses
- defense/training actions and threat summaries through the same player-facing action/query path
- first raid-pressure summaries through the same player-facing action/query path

## Components

### External MCP server
- Launcher:
  - `Tools/run-unity-mcp-server.sh`
- Implementation:
  - `Tools/mcp/unity_project_mcp_server.py`

This is a stdio JSON-RPC MCP server with no third-party runtime dependency. It exposes tools and resources around the repo’s existing Unity workflows.

### Live Unity editor bridge
- Implementation:
  - `Assets/_Game/Authoring/Editor/Mcp/UnityEditorMcpBridge.cs`
- Artifacts:
  - `Logs/McpBridge/status.json`
  - `Logs/McpBridge/requests/*.json`
  - `Logs/McpBridge/responses/*.json`

When the editor has this project open, the bridge becomes the preferred path for:
- scene control
- play mode control
- live compile request
- live edit-mode and play-mode test execution through Unity TestRunner API
- live UI plan apply/preview
- manual test listing and guided step execution
- live gameplay HUD queries
- live gameplay action execution

Operational notes:
- only the main Unity editor process owns `Logs/McpBridge/status.json`; asset-import worker processes are ignored by the bridge
- live play-mode test runs may return `playmode tests finished (recovered from editor state)` when Unity drops callback state during the play-mode transition
- that recovery path is expected on this project because Unity's transitive `com.unity.test-framework.performance` cleanup currently throws after successful test execution in the open editor

### Batch fallbacks
When the live editor bridge is unavailable or not appropriate, the MCP server falls back to:

- compile:
  - `Ggj2026.Game.Authoring.Editor.ProjectCompilationTools.RefreshAssetsAndRecompileFromCommandLine`
- UI install:
  - `Ggj2026.Game.Authoring.Editor.Mcp.UnityEditorMcpCommandLine.ApplyUiPlanFromCommandLine`
- tests:
  - `Tools/run-unity-tests.sh --platform editmode`
  - `Tools/run-unity-tests.sh --platform playmode`

## Tool behavior

### `unity_project_status`
Returns:
- project root
- Unity binary path
- live bridge availability
- latest compile summary
- latest edit-mode summary
- latest play-mode summary
- latest telemetry session metadata
- canonical artifact paths

### `unity_compile`
Modes:
- `auto`
- `bridge`
- `batch`

Behavior:
- `auto` prefers the live editor bridge when available.
- `batch` runs Unity headless compile directly.

### `unity_run_tests`
Arguments:
- `platform`: `editmode` or `playmode`
- `filter`: optional test filter

Behavior:
- `auto` prefers the live editor bridge when available
- the live bridge runs tests through Unity TestRunner API against the open editor instance
- live `playmode` runs may complete through bridge recovery instead of a direct callback if Unity drops the callback during the transition back from play mode
- when the live bridge is unavailable, the server falls back to `Tools/run-unity-tests.sh`
- the batch fallback requires this project editor instance to be closed

### `unity_apply_ui_plan`
Arguments:
- `dryRun`
- `mode`: `auto`, `bridge`, or `batch`

Behavior:
- prefers the live bridge when available
- otherwise uses the command-line helper
- noninteractive mode rejects unsaved open scenes instead of prompting

### `unity_polygon_visual_maintenance`
Live bridge only.

Arguments:
- `importPackages`
- `repairMaterials`
- `refreshVisualLibrary`
- `assignLibraryToRenderers`
- `sourceRoot`

Behavior:
- runs noninteractive Polygon maintenance inside the open editor
- optional package import from `/Users/doronnacash/polygon assets` (or `sourceRoot`)
- repairs unsupported legacy Polygon shaders/materials to URP-compatible lit shader
- rebuilds `WorldVisualPrefabLibrary`
- assigns the library to all loaded `LocalWorldDebugRenderer` components

### `unity_open_scene`
Live bridge only.

### `unity_play_mode`
Live bridge only.

Actions:
- `enter`
- `exit`

### `unity_list_manual_tests`
Live bridge only.

### `unity_manual_test_status`
Live bridge only.

Returns the current guided manual test run state, including the current step prompt and expectation.

### `unity_start_manual_test`
Live bridge only.

Arguments:
- `scenarioId`

Behavior:
- starts the requested guided manual scenario through the editor-side execution service
- seeds the built-in foundation scenarios first if no scenario assets exist yet

### `unity_record_manual_test_step`
Live bridge only.

Arguments:
- `result`: `Pass`, `Fail`, `Blocked`, or `Skipped`
- `note`: optional tester note

Behavior:
- records the current step result
- advances the active guided run

### `unity_finish_manual_test`
Live bridge only.

Behavior:
- finalizes the active guided run
- writes `manual-test.json` artifacts into `Logs/Telemetry`

### `unity_tail_log`
Supported kinds:
- `compile`
- `runtime`
- `editmode`
- `playmode`
- `telemetry-actions`
- `telemetry-ui-actions`

## Gameplay query tools
These read the same command/state model that backs the runtime HUD:

- `unity_game_hud_state`
- `unity_game_world_summary`
- `unity_game_selection`
- `unity_game_hover`
- `unity_game_list_entities`
- `unity_game_list_actions`
- `unity_game_action_schema`
- `unity_game_execute_action`
- `unity_game_blockers`
- `unity_game_recent_events`

Recommended query order for agents:
1. `unity_game_hud_state`
2. `unity_game_selection`
3. `unity_game_list_actions`
4. `unity_game_blockers`
5. then execute a gameplay action

Recommended interaction validation through MCP after HUD or selection changes:
1. `unity_game_hud_state`
2. `unity_game_hover`
3. `unity_game_selection`
4. `unity_game_list_actions`
5. confirm the current hover text, selected target, and `Next`-style actions all agree

Current defense-relevant query expectations:
- the world summary/HUD state should expose barracks readiness
- blockers should expose soldier-training failures in plain language
- blockers and defense summaries should expose active raid pressure in plain language
- recent events should include successful or failed `Train Soldier` attempts

## Gameplay action tools
Player-facing action tools:

- `unity_game_execute_action`
- `unity_game_select_cell`
- `unity_game_select_edge`
- `unity_game_select_entity`
- `unity_game_set_tool`
- `unity_game_clear_tool`
- `unity_game_apply_brush`
- `unity_game_apply_edge_stroke`
- `unity_game_place_module`
- `unity_game_place_landmark`
- `unity_game_clear_cell`
- `unity_game_clear_edge`
- `unity_game_simulation_control`
- `unity_game_save_world`
- `unity_game_reset_world`
- `unity_game_context_explain`
- `unity_game_force_inspector`

Use `unity_game_execute_action` when a newly added shared gameplay action does not yet have a dedicated convenience tool. This is the preferred compatibility path for future HUD/MCP parity work, because it executes the same `GameActionEnvelope` shape the HUD uses.

Current shared action example:
- `TrainSoldier`
  - no cell target required
  - resolves against the current colony
  - consumes one recruit and one weapons stack when queued
  - returns the same readiness reason the HUD shows, such as missing barracks, missing weapons, or no recruitable worker
- `RallyDefenders`
  - no cell target required
  - sends defenders to the colony rally point
  - future defenders inherit the same order
- `HoldDefenders`
  - no cell target required
  - keeps defenders at their current held position
- `PerimeterDefenders`
  - no cell target required
  - distributes defenders across perimeter approach lanes
- `DefendArea`
  - requires `targetCell`
  - sends defenders to hold the selected tile
  - future defenders inherit the same order

Current shared onboarding actions:
- `StartOnboarding`
  - restarts the guide from step one
- `PreviousOnboardingStep`
  - moves to the previous guide step
- `NextOnboardingStep`
  - advances to the next guide step
- `SkipOnboarding`
  - hides the guide for the current session

Action responses include:
- `actionId`
- source
- success/failure
- explanation
- affected object ids when available
- telemetry action id
- diff path when available

Recommended onboarding validation through MCP:
1. `unity_game_hud_state`
2. `unity_game_execute_action` with `StartOnboarding`
3. `unity_game_hud_state` and verify `onboarding.isActive`
4. `unity_game_execute_action` with `NextOnboardingStep`
5. `unity_game_hud_state` and verify `onboarding.progressText` / visible operation groups
6. `unity_game_execute_action` with `SkipOnboarding`
7. `unity_game_hud_state` and verify `onboarding.isActive == false`

Recommended access-guard validation through MCP:
1. select the target work cell with `unity_game_select_cell`
2. list actions with `unity_game_list_actions`
3. enqueue normal enclosure walls with `unity_game_apply_edge_stroke` or `unity_game_execute_action`
4. attempt the final closing wall
5. confirm the response explains that the final wall would seal access, while earlier walls were accepted

## Admin/setup separation
Keep player-facing gameplay actions separate from admin/test/setup commands. Do not rely on unrestricted raw world mutation as the normal MCP interface.

Future admin/setup commands should stay clearly labeled, for example:
- fixture setup/reset
- camera focus
- snapshot capture
- resource seeding

## MCP resources
The server exposes project artifacts as MCP resources:

- `unity://project/status`
- `unity://compile/latest`
- `unity://runtime-log/latest`
- `unity://tests/editmode/latest`
- `unity://tests/playmode/latest`
- `unity://telemetry/session-latest/session`
- `unity://telemetry/session-latest/actions`
- `unity://telemetry/session-latest/summary`
- `unity://mcp/bridge/status`

## Safety rules

### Dirty scenes
The live editor bridge rejects:
- scene changes
- play-mode entry with scene switch
- noninteractive UI plan apply

when open scenes have unsaved changes.

### Batch tests
Batch tests are intentionally conservative:
- if the editor is open on this project, `Tools/run-unity-tests.sh` exits early
- this avoids Unity lockfile and test-runner conflicts

### Command-line tests
Do not add `-quit` to Unity Test Framework command-line test runs in this project. The installed package skips command-line execution when `-quit` is present.

## Validation

### Python MCP tests
Run:
```bash
python3 -m unittest /Users/doronnacash/RiderProjects/adventure-game/ggj2026/Tools/mcp/tests/test_unity_project_mcp_server.py
```

### Unity tests
Run:
```bash
/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Tools/run-unity-tests.sh --platform editmode
/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Tools/run-unity-tests.sh --platform playmode
```

## Future extensions
Likely next additions:
- runtime scene fixture setup commands
- telemetry session browsing and diff summarization tools
