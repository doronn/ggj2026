# UI Toolkit Migration

## Rule
All maintained `_Game` UI uses Unity UI Toolkit.

## Canonical sources
- `UXML` for structure
- `USS` for styles
- controller/binder C# for logic

## Folder layout
- `Assets/_Game/UI/Runtime/UXML`
- `Assets/_Game/UI/Runtime/USS`
- `Assets/_Game/UI/Editor/UXML`
- `Assets/_Game/UI/Editor/USS`

## Current maintained panels
- `ClientWorldHud`
- `PersistentMainMenu`
- `BootstrapStatus`
- `UiPlanIntegrationWindow`
- `ManualTestRunnerWindow`
- `PivotFoundationSetupWindow`
- `HudStateBrowserWindow`

## Runtime HUD structure
The maintained runtime HUD is operations-first with contextual inspection:

- top world bar
  - world id, tick, simulation state, save state, worker status, blocker state, telemetry state, overlay state
- left operations rail
  - simulation, view, floors, edges, modules, landmarks, cleanup, world
- right context inspector
  - tabs: `Context`, `Tasks`, `Entities`, `World`
- bottom event strip
  - current tool instruction, hover summary, last action, blockers, warnings

The current maintained asset remains:
- `Assets/_Game/UI/Runtime/UXML/ClientWorldHud.uxml`
- `Assets/_Game/UI/Runtime/USS/ClientWorldHud.uss`

It is treated as the shell document for the richer human/agent interface model.

## Runtime UI architecture
- structure in `UXML`
- theme/layout/components in `USS`
- state projection in read models built by `GameQueryService`
- action routing through `GameActionService`
- orchestration by `ClientWorldHudController`

Do not place gameplay rules directly in UI button callbacks.

## Context card rule
Context views must be driven by prioritized selection resolution:
1. selected entity
2. selected module
3. selected landmark
4. selected stack
5. selected edge
6. selected or hovered cell
7. world fallback

Each context view must explain blocked/unavailable actions in plain language.

## Input rules
- UI Toolkit is the authoritative owner of interactive UI input.
- World selection, painting, and placement must not execute while the pointer is interacting with runtime HUD surfaces.
- Use `RuntimeInputCaptureService` and do not bypass it with raw input handling.
- Selecting a tool must leave the world immediately paint-ready once the pointer leaves the UI. No follow-up click on blank UI space is acceptable.

## Readability and accessibility rules
- use icon + text or text-only, never icon-only
- use explicit short labels
- do not rely on color alone for meaning
- keep contextual explanations short and literal:
  - `Requires enclosed room`
  - `Missing 4 Wood`
  - `No source`
  - `Tile blocked by large pile`

## Command parity rule
All maintained player-facing actions must exist in the shared action model and be usable from:
- runtime HUD
- contextual inspector
- MCP gameplay interface

Do not add a maintained gameplay verb only to a local UI button or only to MCP.

## Deprecation rule
- Do not add new `OnGUI`-based maintained UI.
- If a maintained `_Game` editor/runtime window still uses IMGUI, migrate it before extending it.
