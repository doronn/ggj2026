# Human + Agent Interface

## Purpose
`ggj2026` now treats the human HUD and the MCP gameplay interface as two views over one command/state model.

The rule is simple:
- if a human can do it through maintained UI, an agent should be able to do it through MCP
- if an agent can do it as a normal player action, the human HUD should expose the same action model
- blocked actions must explain themselves in both places

## Onboarding
The project now has a first-session onboarding flow.

Current onboarding rules:
- it starts automatically for a fresh player session
- it highlights one interface region at a time
- it hides operation groups and inspector tabs that are not relevant yet
- it can be skipped for the current session
- it can be restarted later from the shared action surface
- it must stay compatible with the same action/query model used by MCP

The onboarding currently teaches:
- what the top bar is for
- what the command panel is for
- what the inspector is for
- what the event strip is for
- the current core colony loop at a high level
- how world-space labels identify hover and selection targets
- that the contextual `Next` actions should be preferred before opening every tool family

When expanding the core loop, keep onboarding current for:
- room activation
- population growth
- production chain
- defense and raid pressure
- future rival-faction pressure

## Runtime HUD structure

### Top world bar
Always visible:
- world id
- tick
- realtime simulation state
- save state
- worker state
- primary blocker
- telemetry state
- overlay mode
- derived resource chips

### Left operations rail
Persistent global operations:
- `Simulation`
- `View`
- `Floors`
- `Edges`
- `Modules`
- `Landmarks`
- `Cleanup`
- `World`

Primary verbs stay visible and do not move based on current selection.

The default rail should stay quiet:
- keep `Simulation` and `View` always visible
- prefer a contextual `Next` group for the immediately useful actions
- reveal broader tool families when the current context or active tool makes them relevant

### Right context inspector
Tabs:
- `Context`
- `Tasks`
- `Entities`
- `World`

Context priority:
1. selected entity
2. selected module
3. selected landmark
4. selected stack
5. selected edge
6. selected or hovered cell
7. world fallback

Task cards are actionable, not informational only. Current maintained task-card verbs:
- `Raise Priority`
- `Lower Priority`
- `Cancel Build`

Current maintained world-level defense verb:
- `Train Soldier`

Current maintained world-level defense-order verbs:
- `Rally Defenders`
- `Hold Defenders`

Current maintained contextual defense verb:
- `Defend Here`

Task cards are also urgency-sorted:
- higher construction priority first
- then near-complete work
- then lower-missing-material tasks

### Bottom event strip
Shows:
- current tool guidance
- hover summary
- last action result
- blocker summary
- warnings

## World-space labels and selection clarity
The player should not need to guess what a click will target.

Current rules:
- a hover target may show a small world-space label
- the current selection should show a stronger world-space label
- selected units should be emphasized more strongly than hovered units
- selected edges and cells should be clearer than hover-only highlights
- when a tool is active, the hover label should describe what a click will do or why it is blocked

Current selection priority:
1. unit/entity under the cursor
2. world object on the cell
3. cell itself
4. nearest edge through the alternate edge-selection path

Good click guidance examples:
- `Click selects Worker`
- `Click selects Storage tile`
- `Click: Build Wall`
- `Blocked: Workers cannot reach this tile`

## Presentation and motion
The world should feel alive without sacrificing clarity.

Current presentation rules:
- units animate between hexes instead of snapping
- moving units use a small hop arc and squash/stretch so travel direction is readable
- active work uses visible bob/pulse so a working unit is distinguishable from an idle one
- presentation effects must never make tile occupancy ambiguous
- visual juice must support comprehension, not replace blocker/context information

## Action model
Maintained player actions go through the shared action layer:
- `GameActionService`
- `GameActionEnvelope`
- `GameActionDescriptor`
- `GameActionResult`

Action sources:
- `HumanUi`
- `McpAgent`
- `ManualQa`
- `System`

Actions are categorized for both HUD grouping and MCP inspection:
- simulation
- view
- floors
- edges
- modules
- landmarks
- cleanup
- world
- entities
- context

## Contextual inspector model
The contextual inspector is built from shared read models:
- `GameSelectionState`
- `ContextInspectorViewModel`
- `ContextCardViewModel`
- `WorldBarState`
- `EventStripState`

Use `GameQueryService` for maintained read models instead of rebuilding summaries ad hoc.

The `World` tab currently includes:
- overall colony state
- top blockers
- production chain status for `Ore -> Metal -> Weapons`
- defense status for `Barracks -> Soldiers -> Threat -> Raid`

The `Context` tab should expose:
- `Activation` for tiles/spaces, including planned sheltered rooms
- `Module status` for built modules, including missing inputs and inactive reasons
- current room/space purpose for grouped multi-tile spaces
- context actions that are valid for the exact selection instead of a generic verb list

## Room enclosure and wall-guard rules
The current simulation supports multi-tile enclosed spaces.

Rules:
- adjacent tiles of the same purpose may form one room/space
- walls may be added step by step while enclosing a room
- the wall guard should block only the final placement that would leave an important room/work area with no remaining opening
- doors must remain visually and behaviorally distinct from walls

## Human vs MCP parity rules
- HUD buttons and contextual actions must route through `GameActionService`.
- MCP gameplay actions must route through the same underlying action service.
- HUD state queries and MCP queries must be built from `GameQueryService`.
- Do not add a gameplay verb only to UI or only to MCP.
- Selection feedback and contextual availability must also stay in parity:
  - if the HUD shows a target/action/blocked reason, MCP should be able to query the same state
  - if MCP can execute a normal player action, the HUD should expose the same command model

## Explainability rules
Disabled or failed actions must expose literal reasons.

Good examples:
- `Requires Residential floor`
- `Requires enclosed room`
- `Missing 4 Wood`
- `No source`
- `Needs enclosed Workshop room`
- `Waiting for Ore x2`
- `Requires active Barracks`
- `Missing 1 Weapons`
- `No recruitable worker available`
- `Raiders attacking in 3 ticks.`
- `Raid struck and looted 4 Wood.`
- `Rallying at (0,0,0).`
- `Holding (3,-1,-2).`
- `Defending (4,-1,-3).`
- `Target cell is required`

Bad examples:
- `Invalid state`
- `Unavailable`
- `Cannot do that`

## Current MCP gameplay query set
- `unity_game_hud_state`
- `unity_game_world_summary`
- `unity_game_selection`
- `unity_game_hover`
- `unity_game_list_entities`
- `unity_game_list_actions`
- `unity_game_action_schema`
- `unity_game_blockers`
- `unity_game_recent_events`

## Current MCP gameplay action set
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

Construction-management actions currently exposed through the shared action model:
- `IncreaseConstructionPriority`
- `DecreaseConstructionPriority`
- `CancelConstruction`

Defense-management action currently exposed through the shared action model:
- `TrainSoldier`
- `RallyDefenders`
- `HoldDefenders`
- `DefendArea`

Onboarding actions currently exposed through the shared action model:
- `StartOnboarding`
- `PreviousOnboardingStep`
- `NextOnboardingStep`
- `SkipOnboarding`

These are intended to be invoked either from HUD task cards or through `unity_game_execute_action` with `targetConstructionId`.
`TrainSoldier` is intended to be invoked from the world/defense card or through `unity_game_execute_action` without a cell target.
`RallyDefenders` and `HoldDefenders` are intended to be invoked from the world/defense card or through `unity_game_execute_action` without a cell target.
`DefendArea` is intended to be invoked from a selected tile context card or through `unity_game_execute_action` with `targetCell`.
The current training flow consumes one recruit and one weapons stack up front, then materializes a soldier at the colony rally point when training completes.

## Admin/test command separation
Normal player actions should stay distinct from admin/test/setup commands.

Examples of admin/test commands:
- fixture setup/reset
- camera focus
- snapshot capture
- debug resource seeding

These should be clearly documented and not treated as normal player verbs.

## Developer tools
- `Window > GGJ2026 > QA > HUD State Browser`
  - inspect live HUD state and action JSON
- `Window > GGJ2026 > QA > Manual Test Runner`
  - guided human validation

Use the HUD State Browser to compare what a human sees with what MCP gameplay queries should return.
Use the Manual Test Runner to validate onboarding comprehension and presentation sanity after interface changes.
Use both after interaction changes to verify:
- contextual `Next` actions are sensible
- world-space labels are readable without obscuring the map
- wall and door visuals are distinguishable
- selection state is obvious before and after clicks
