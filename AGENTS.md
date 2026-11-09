# Project Guide

## Overview
- Unity project: `ggj2026`
- Unity version in active use: `6000.3.10f1`
- The repository currently contains two parallel tracks:
  - Legacy `Breaking Hue` puzzle/action runtime under `Assets/Scripts`, `Assets/Scenes`, and related editor tooling.
  - New persistent hex-world 4X/RTS pivot foundation under `Assets/_Game`.

## Current product state
- The 4X/RTS pivot is in foundation stage, not production gameplay-complete.
- The active new scene flow is:
  - `Assets/Scenes/Bootstrap.unity`
  - `Assets/Scenes/PersistentMainMenu.unity`
  - `Assets/Scenes/ClientWorld.unity`
  - `Assets/Scenes/ServerWorld.unity`
  - `Assets/Scenes/Tools.unity`
- Build settings were intentionally switched to those scenes. Legacy `MainMenu`, `World`, and `EndGame` remain in the repo but are disabled in build settings.

## Architecture boundaries

### New runtime
- `Assets/_Game/Shared`
  - Shared contracts, IDs, hex math, world DTOs, persistence, economy, units, logistics, networking contracts.
- `Assets/_Game/Shell`
  - Bootstrap, scene navigation, camera, shell UI, local session orchestration, runtime diagnostics.
- `Assets/_Game/Simulation`
  - ECS-first systems and authoring components for simulation scaffolding.
- `Assets/_Game/Authoring`
  - ScriptableObjects, editor setup tools, compile/report tooling.
- `Assets/_Game/Networking`
  - Session/bootstrap scaffolding and future online hooks.
- `Assets/_Game/Presentation`
  - Presentation bridge layer.
- `Assets/_Game/Tests`
  - Edit mode and play mode tests for new runtime contracts, UI, telemetry, and persistence.

### Legacy runtime
- `Assets/Scripts`
  - Existing Breaking Hue gameplay and systems.
- `Assets/Editor`
  - Existing legacy/editor migration tooling.
- `Assets/Legacy/BreakingHue`
  - Explicit legacy boundary and notes.

## Rules for future agents
- Do not add new 4X gameplay code under `Assets/Scripts`.
- Put new 4X/RTS systems under `Assets/_Game`.
- All maintained UI must use Unity UI Toolkit.
- Do not add new `OnGUI` runtime or editor UI under maintained `_Game` workflows.
- Inspect `UXML`, `USS`, and controller files first when working on UI behavior.
- Use the UI installer workflow instead of wiring maintained `_Game` `UIDocument` scenes by hand.
- The human HUD and MCP gameplay control surface are one command/state model. Add gameplay verbs through the shared action/query layer, not directly to a single button or MCP handler.
- New gameplay actions must have action-service parity:
  - HUD button or contextual action
  - MCP gameplay action route
  - explainable availability/blocker reason
  - telemetry record
- Prefer exposing new parity actions through the shared action model plus `unity_game_execute_action` first, then add dedicated MCP convenience tools only when they materially improve workflow.
- Contextual and blocker explanations are mandatory. If an action is disabled or blocked, the HUD and MCP path must expose the reason in plain language.
- Read `Logs/Telemetry/session-latest`, the live HUD state, and MCP query outputs before assuming a player report is purely gameplay confusion.
- The runtime now includes a first-session onboarding flow.
- When changing HUD structure, blocker language, core verbs, or contextual surfaces, update onboarding steps and explanatory copy in the same pass.
- World interaction should stay human-readable first:
  - show world-space labels for the current hover/selection when they improve clarity
  - keep selected targets visually stronger than hover targets
  - prefer contextual `Next` actions over permanently showing every possible tool at once
  - preserve exact HUD/MCP parity for the same contextual actions
- Wall placement rules should prevent only the final inaccessible seal, not normal step-by-step enclosure work.
  - multi-tile rooms enclosed by walls are valid
  - the guard should block only “no access remains” cases
  - if that rule changes, update manual QA and onboarding copy in the same pass
- Onboarding rules:
  - hide irrelevant operation groups and inspector tabs until the guide reaches them
  - keep the guide skippable for the current session
  - keep the guide restartable from the shared action layer
  - keep explanatory text short, plain-language, and region-specific
  - preserve HUD/MCP parity for onboarding actions and visible state
- Presentation should continue getting more alive over time, but stay readable:
  - units should animate between hexes instead of snapping
  - working units should show visible activity
  - added juice must not obscure exact world state or tile occupancy
- Treat `Assets/Scripts` as legacy unless:
  - the user explicitly asks for legacy fixes,
  - compatibility work is needed for current Unity,
  - or warnings/errors in legacy code block the whole project from compiling.
- Do not revert unrelated user changes. This repo may be dirty in both legacy and pivot paths.
- Prefer changing the smallest viable surface when fixing compile issues in legacy code.

## Repo-local skill pack
- The project maintains repo-local Codex skill drafts under:
  - `Tools/skills`
- These are not automatically installed into `$CODEX_HOME/skills`, so they may not appear in the session skill list by default.
- If a task clearly matches one of these skill names, open its local `SKILL.md` directly and follow it:
  - `Tools/skills/ggj2026-unity-control`
  - `Tools/skills/ggj2026-log-triage`
  - `Tools/skills/ggj2026-hex-colony-dev`
  - `Tools/skills/ggj2026-manual-qa`
  - `Tools/skills/ggj2026-ui-toolkit`
  - `Tools/skills/ggj2026-legacy-maintenance`
- Validate the repo-local skill pack with:
  - `Tools/skills/validate-repo-skills.sh`

## Important runtime files
- `Assets/_Game/Shell/World/OfflineWorldBootstrap.cs`
  - Loads or creates the local persistent world and builds debug presentation.
- `Assets/_Game/Shell/World/LocalWorldDebugRenderer.cs`
  - Hex debug renderer for the generated world.
- `Assets/_Game/Shell/Diagnostics/RuntimeLogRecorder.cs`
  - Mirrors runtime logs into the repo.
- `Assets/_Game/Shell/Diagnostics/SessionTelemetryService.cs`
  - Writes structured session telemetry, snapshots, diffs, and UI action records.
- `Assets/_Game/Shell/World/GameActionService.cs`
  - Shared human/agent gameplay command layer. Use it for maintained player actions instead of wiring gameplay straight into one UI controller.
- `Assets/_Game/Shell/World/GameQueryService.cs`
  - Shared HUD/MCP read-model builder for world bar, context inspector, blockers, entity summaries, and available actions.
  - Also owns task urgency ordering and the world-tab production-chain summary (`Ore -> Metal -> Weapons`).
  - Also owns the world-tab defense summary, defender-order visibility, `Train Soldier` readiness, and plain-language raid pressure reporting.
  - Also owns contextual `Next` actions, hover guidance, and the text players read before they click.
- `Assets/_Game/Shell/World/OnboardingFlowService.cs`
  - Shared onboarding state, step progression, per-step operation-group/tab visibility, and panel tooltip copy.
- `Assets/_Game/Shell/World/LocalColonyGameplayService.cs`
  - Current colony simulation, including growth, construction priority, cancellation, and material reclaim behavior.
  - Also owns plain-language space activation and module-status explanations used by both HUD and MCP.
  - Also owns soldier training readiness, conversion of workforce into local defenders, and the first timed raid/loot pressure loop.
  - Also owns the wall-access guard that prevents sealing an important room or work area with no remaining opening.
- `Assets/_Game/Shell/World/ClientWorldHudController.cs`
  - Runtime HUD composition root, world-space labels, and the selection/hover presentation layer.
- `Assets/_Game/Shell/World/LocalWorldDebugRenderer.cs`
  - World-space selection visuals, unit emphasis, wall/door rendering, and edge/cell hover feedback.
- `Assets/_Game/Authoring/Editor/UiPlanInstallerService.cs`
  - Creates/refreshes the UI plan manifest and wires maintained `_Game` UI Toolkit documents into scenes.
- `Assets/_Game/Authoring/Editor/PolygonAssetIntegrationWindow.cs`
  - UI Toolkit editor window for importing selected Polygon `.unitypackage` packs from `/Users/doronnacash/polygon assets`, repairing incompatible legacy shaders/materials for URP, and auto-building `WorldVisualPrefabLibrary` mappings for runtime placeholder replacement.
- `Assets/_Game/Authoring/Editor/HudStateBrowserWindow.cs`
  - UI Toolkit editor window for comparing the live human HUD state with the shared action/query surface used by MCP.
- `Assets/_Game/Authoring/Editor/Mcp/UnityEditorMcpBridge.cs`
  - Live Unity editor bridge for the project-owned MCP server. It processes file-backed control requests under `Logs/McpBridge`.
- `Assets/_Game/Authoring/Editor/Mcp/UnityEditorMcpCommandLine.cs`
  - Batch command-line entry points used by the MCP server when the live editor bridge is not the right control path.
- `Assets/_Game/Authoring/Editor/PivotFoundationSetupWindow.cs`
  - Generates the pivot scenes and config asset.
- `Assets/_Game/Networking/Runtime/Ggj2026WorldBootstrap.cs`
  - Project bootstrap. It currently creates a local world instead of auto-starting NetCode client/server worlds.

## Logging and diagnostics

### Runtime logs
- Repo-local runtime log:
  - `Logs/PersistentWorld/runtime-latest.log`
- Session runtime logs:
  - `Logs/PersistentWorld/runtime-YYYYMMDD-HHMMSS.log`
- These are written automatically by `RuntimeLogRecorder`.

### Structured telemetry
- Repo-local telemetry root:
  - `Logs/Telemetry`
- Latest session folder:
  - `Logs/Telemetry/session-latest`
- Timestamped session folders:
  - `Logs/Telemetry/session-YYYYMMDD-HHMMSS`
- Important files:
  - `session.json`
  - `actions.jsonl`
  - `ui-actions.jsonl`
  - `checks.jsonl`
  - `snapshots/*.json`
  - `diffs/*.json`
  - `manual-test.json` when a guided manual run is linked to the session
- Read these before relying only on console output. They are the canonical record for `_Game` sessions.

### MCP bridge and server artifacts
- Live editor bridge root:
  - `Logs/McpBridge`
- Important files:
  - `Logs/McpBridge/status.json`
  - `Logs/McpBridge/requests/*.json`
  - `Logs/McpBridge/responses/*.json`
- The project-owned MCP server is launched with:
  - `Tools/run-unity-mcp-server.sh`
- Server implementation:
  - `Tools/mcp/unity_project_mcp_server.py`
- Prefer the MCP server for project control tasks when integrating with an MCP-capable client:
  - project status
  - compile
  - tests
  - UI plan apply/preview
  - scene open
  - play mode enter/exit
  - manual test discovery and guided manual test execution
  - gameplay HUD/context/blocker queries
  - gameplay action execution
- `Logs/McpBridge/status.json` must be owned by the main Unity editor process, not asset-import worker processes.
- Live `playmode` MCP test runs can legitimately report `recovered from editor state`; that means Unity dropped the callback during the play-mode return path, but the bridge recovered the NUnit XML result successfully.
- Live guided manual test runs can be driven through the MCP bridge:
  - list scenarios
  - start a scenario
  - record step outcomes
  - finish the run
- Seed guided manual test scenarios are created automatically under `Assets/_Game/Authoring/ManualTests` when the manual test system is first queried and none exist yet.
- The guided manual scenario set should include onboarding and presentation sanity whenever the first-session experience or movement/work feedback changes materially.
- Gameplay MCP commands are player-facing by default. Keep admin/setup/testing commands separate in docs, telemetry interpretation, and future tool additions.
- Current shared onboarding verbs:
  - `StartOnboarding`
  - `PreviousOnboardingStep`
  - `NextOnboardingStep`
  - `SkipOnboarding`
- Current interaction expectations:
  - players should be able to click directly on the world and understand what will be selected
  - players should be able to see what is currently selected without reading the inspector first
  - hover/selection labels and contextual actions should explain the next useful action
  - operations rail groups should stay progressively disclosed:
    - keep `Simulation`/`View`/`Next` visible
    - reveal `Floors`/`Edges`/`Modules`/`Landmarks`/`Cleanup` only when context or active tool makes them relevant
  - MCP gameplay queries should expose the same selection/context state the human HUD is using
- Current shared defense verb:
  - `Train Soldier`
- Current shared defense-order verbs:
  - `Rally Defenders`
  - `Hold Defenders`
  - `Perimeter Defenders`
  - `Defend Here` (selected tile / targeted cell)
- Current defense read model expectations:
  - world card shows barracks readiness, training status, current defender order, soldiers, threat summary, raid state, and weapons availability
  - blockers surface plain-language reasons such as missing barracks, missing weapons, no recruitable workforce, or an active raid
  - entity summaries should include plain-language assignment/idle reasons that can be read from both HUD state and MCP queries

### Compile logs
- Repo-local compile log:
  - `Logs/Diagnostics/compile-latest.log`
- Session compile logs:
  - `Logs/Diagnostics/compile-YYYYMMDD-HHMMSS.log`
- These are written by the editor compile tool in `Assets/_Game/Authoring/Editor/ProjectCompilationTools.cs`.
- The tool now auto-captures normal Unity compilations as well, not only the explicit menu command.
- `compile-latest.log` may initially contain a placeholder line before the first real compile is captured.

### Fallback editor log
- Use when the repo-local compile log is missing or incomplete:
  - `~/Library/Logs/Unity/Editor.log`

## Compile and validation workflow

### Preferred in-editor workflow
Run:
- `Window > GGJ2026 > Diagnostics > Refresh Assets And Recompile`
- `Window > GGJ2026 > UI > Apply Planned UI` when scenes/UI bindings may be stale
- `Window > GGJ2026 > QA > Manual Test Runner` for guided milestone validation

Then read:
1. `Logs/Diagnostics/compile-latest.log`
2. `Logs/Telemetry/session-latest/session.json` and `Logs/Telemetry/session-latest/actions.jsonl` after Play Mode
3. `Logs/PersistentWorld/runtime-latest.log` for raw Unity logs

### Batchmode workflow
Callable method:
- `Ggj2026.Game.Authoring.Editor.ProjectCompilationTools.RefreshAssetsAndRecompileFromCommandLine`

Use it with Unity `-executeMethod` only when Unity is not already open on the project. The method is intended to:
- refresh assets,
- request a clean script recompilation,
- write compile reports,
- and exit batchmode with `0` on success or `1` on compile errors.

### Batch test workflow
- Preferred repo-owned wrapper:
  - `Tools/run-unity-tests.sh --platform editmode`
  - `Tools/run-unity-tests.sh --platform playmode`
- Output artifacts:
  - `Logs/TestResults/editmode-results.xml`
  - `Logs/TestResults/editmode-batch.log`
  - `Logs/TestResults/playmode-results.xml`
  - `Logs/TestResults/playmode-batch.log`
- The wrapper also writes timestamped copies alongside the latest files.
- Important Unity quirk:
  - do not add `-quit` to command-line Unity test runs in this repo; the installed Unity Test Framework package skips command-line test execution when `-quit` is present.
- If batch tests fail before producing XML, inspect:
  - `Logs/TestResults/<platform>-batch.log`
  - then `~/Library/Logs/Unity/Editor.log`

### MCP workflow
- Start the server:
  - `Tools/run-unity-mcp-server.sh`
- Use the MCP server as the primary control plane for continued project work when an MCP client is available.
- The server will:
  - prefer the live editor bridge for scene/play-mode/editor tasks when the project is open,
  - prefer the live editor bridge for edit-mode and play-mode test runs when the project is open,
  - fall back to batch Unity commands for compile, UI-install, and test tasks when the bridge is unavailable.
- Use `unity_polygon_visual_maintenance` for noninteractive Polygon asset upkeep:
  - optional `.unitypackage` import from `/Users/doronnacash/polygon assets`,
  - material/shader repair,
  - visual library rebuild,
  - assignment to loaded `LocalWorldDebugRenderer` components.
- For gameplay work, query first:
  - `unity_game_hud_state`
  - `unity_game_selection`
  - `unity_game_list_actions`
  - `unity_game_blockers`
- Then execute through the shared gameplay action surface rather than screen-driving Unity.
- The batch test fallback still requires this project editor instance to be closed.

### Live editor limitations
- A second Unity batchmode instance cannot open the same project while the editor already has it open.
- macOS UI automation may be blocked by Accessibility permissions, so an external agent may be unable to click Unity menu items programmatically.
- If both constraints apply, do not assume the compile tool can be triggered from the terminal. In that case:
  - wait for the open editor to auto-recompile after file edits, or
  - have the user trigger `Window > GGJ2026 > Diagnostics > Refresh Assets And Recompile`,
  - then read `Logs/Diagnostics/compile-latest.log` or fall back to `~/Library/Logs/Unity/Editor.log`.

## NetCode and ECS notes
- The project includes Entities and NetCode packages, but online play is not active yet.
- Scenes include `OverrideAutomaticNetcodeBootstrap` components.
- The project also uses the custom bootstrap `Ggj2026WorldBootstrap` to avoid accidental client/server world creation during the current offline foundation phase.
- If NetCode batched tick warnings appear during offline work, treat that as a bootstrap/world-creation regression.

## Known current expectations
- `_Game` foundation should compile cleanly.
- Local persistent world flow should be tested from `Bootstrap`.
- If runtime fails after compile:
  - inspect `Logs/Telemetry/session-latest` first,
  - then inspect `Logs/PersistentWorld/runtime-latest.log`,
  - then inspect `~/Library/Logs/Unity/Editor.log` if needed.

## Testing policy
- Every new behavioral system needs edit mode tests.
- Every integrated runtime flow needs play mode coverage.
- Significant milestone features also need guided manual test scenarios under `Assets/_Game/Authoring/ManualTests`.
- UI interactions must never leak through to world interactions behind them; add regression tests for this when touching HUD/input code.
- When validating test health from the terminal, prefer `Tools/run-unity-tests.sh` over ad hoc Unity CLI invocations so result/log paths stay consistent.

## Useful docs
- `Assets/_Game/README.md`
- `Docs/ADR/ADR-0001-persistent-world-foundation.md`
- `Docs/PERSISTENT_WORLD_IMPLEMENTATION.md`
- `Docs/PERSISTENT_WORLD_LOGGING.md`
- `Docs/COMPILATION_WORKFLOW.md`
- `Docs/SESSION_TELEMETRY.md`
- `Docs/TEST_STRATEGY.md`
- `Docs/MANUAL_TESTING.md`
- `Docs/UI_TOOLKIT_MIGRATION.md`
- `Docs/UI_INSTALLER_WORKFLOW.md`
- `Docs/MCP_SERVER_WORKFLOW.md`

## Recommended agent behavior
1. Check whether the task belongs to legacy `Assets/Scripts` or new `_Game`.
2. If validating project health, trigger the compile workflow and read `Logs/Diagnostics/compile-latest.log`.
3. If UI bindings may have changed, run the UI installer workflow and inspect `Logs/Diagnostics/ui-install-latest.json`.
4. After Play Mode, read `Logs/Telemetry/session-latest` before raw logs.
5. Only after that, inspect `Logs/PersistentWorld/runtime-latest.log`.
6. When Unity editor tooling itself is broken, use `~/Library/Logs/Unity/Editor.log` as the source of truth.
7. If Unity is already open and automation is blocked, keep working from repo files and logs instead of waiting on impossible terminal-driven recompiles.
