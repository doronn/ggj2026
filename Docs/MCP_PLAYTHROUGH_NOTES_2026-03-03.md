# MCP Playthrough Notes - March 3, 2026

## Scope
This note records a live MCP-driven playthrough against the open Unity editor project state.

The goal was to act as a player:
- reset the local world,
- build out the starter colony,
- use realtime simulation,
- manage resources,
- push the current implementation as far as it would go,
- and identify which planned features are still missing.

## Starting State
- Scene: `ClientWorld`
- World: `0000000000000001`
- Tick: `0`
- Workers: `6`
- Starting resources:
  - `Wood 18`
  - `Stone 14`
- Starter functional spaces:
  - sheltered residential room
  - sheltered storage room
  - roadway tile
- Starter blocked space:
  - open `Workshop` tile with a `Workbench` module on `(-1,1,0)`

Nearby resource nodes from the live snapshot:
- Wood:
  - `(3,-2)` with yield `34`
  - `(4,-1)` with yield `32`
- Stone:
  - `(-3,2)` with yield `26`
- Ore:
  - `(-4,1)` with yield `23`
- Food:
  - `(2,3)` with yield `19`

## What Was Played

### Run 1: realtime and coordinate validation
Actions:
- entered play mode through MCP
- reset world
- painted a `Residential` floor at `(4,0,-4)`
- started realtime simulation

Observed:
- realtime advanced from `Tick 0` to `Tick 2`
- worker state moved from `0 busy / 6 idle` to `6 busy / 0 idle`
- blockers changed to wood demand for the queued construction

Result:
- MCP gameplay control and realtime simulation are now operational.

### Run 2: base improvement attempt
Actions:
- queued a `Door` on edge `(-1,1) <-> (0,1)`
- queued `Wall` on:
  - `(-1,1) <-> (-2,1)`
  - `(-1,1) <-> (-2,2)`
  - `(-1,1) <-> (-1,2)`
- queued a `Bed` on residential tile `(-1,0)`
- started realtime simulation

Observed over time:
- `Tick 0` -> `Tick 28`
- workers became fully busy
- wood was consumed, then replenished by harvesting from nearby wood nodes
- node counts decreased while stockpile later recovered

Representative state progression:
- after queueing:
  - blocker: `Wood: Need 25. Free 18, staged 0, nodes 66.`
- around `Tick 12`:
  - `Wood 9`
  - `4 busy / 2 idle`
- around `Tick 18`:
  - `Wood 4`
  - `6 busy / 0 idle`
- around `Tick 28`:
  - `Wood 28`
  - blocker: `Wood: Need 6. Free 0, staged 5, nodes 42.`

Result:
- workers successfully:
  - harvested wood,
  - hauled staged materials,
  - advanced queued construction over realtime.

### Run 3: long idle confirmation
Actions:
- let realtime continue without new queued work

Observed:
- tick advanced normally
- workers stayed idle when no actionable queue existed
- stockpiles stayed unchanged

Result:
- idle behavior is correct when there is no valid work.

## What Worked
- live MCP control over gameplay actions
- play mode entry/exit through MCP
- realtime simulation
- floor painting and edge building through MCP
- resource harvesting
- hauling and staging
- blocker reporting in the world bar
- telemetry and diff capture during play

## Confirmed Missing or Incomplete Features

### 1. Unit growth / recruitment is not implemented
The request was to create more units and grow the colony materially. That is not supported yet.

Current behavior:
- population is effectively fixed to existing unit count
- housing capacity can be derived, but no actual civilian growth / spawn loop is present
- no recruitment or training pipeline produces new worker or soldier units

Impact:
- colony scale cannot truly increase yet
- a "robust empire" is not achievable in the current implementation

### 2. Full room-to-production progression is incomplete in practical play
The starter `Workbench` exists on a `Workshop` floor, but the path from:
- enclosing the room,
- finishing all queued shell pieces,
- activating the workshop,
- feeding ore/wood,
- producing metal,
- then building a `WeaponBench` chain,
is not smooth enough yet to count as operational colony progression in normal play.

Impact:
- the economy loop exists in code, but is not yet a reliable player-facing progression path.

### 3. Context-card action parity is still broken
Direct MCP actions work, but contextual action descriptors shown in the inspector still carry envelopes with:
- `hasTargetCell = false`
- or equivalent missing target flags

Impact:
- the human contextual UI is not truly actionable yet
- human UI and MCP parity is still incomplete

### 4. No empire-level expansion systems yet
Missing from actual playable runtime:
- territory / influence gameplay
- multi-colony expansion
- policy / zoning control beyond direct painting
- logistics overlays and management tools
- economic planning UI

Impact:
- current play is a local colony sandbox, not a growing 4X colony empire.

### 5. No combat or rivals
Not operational yet:
- squad production
- soldier conversion/training
- enemy AI factions in active play
- raids
- defensive gameplay
- conquest

Impact:
- the strategy layer is still non-military and non-adversarial.

### 6. No meaningful barracks loop
The data model includes barracks-related floors/modules, but there is not yet a real player-facing:
- barracks buildout
- troop creation
- command structure
- readiness loop

### 7. No player-usable higher-level automation tools
Workers act automatically, but the player cannot yet control:
- build priority in a meaningful UI way
- work categories
- explicit logistics priorities
- resource routing policies

Impact:
- colony management depth is still shallow.

## Practical Conclusion
The current runtime is now good enough for:
- MCP gameplay control
- realtime observation
- basic construction
- resource harvesting and hauling
- telemetry-based debugging

It is not yet good enough for:
- genuine colony growth,
- robust production chains,
- troop generation,
- or a true empire progression loop.

## Recommended Next Development Targets
1. Implement real unit/population growth or training so colony scale can increase.
2. Finish the room activation and production chain loop so `Workshop -> Metal -> Weaponry -> Weapons` is achievable in normal play.
3. Fix contextual action parity so the human UI and MCP interface expose the same actionable envelopes.
4. Add explicit construction priority / work management controls.
5. Build the first barracks + soldier + defense loop.
