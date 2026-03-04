# Persistent World Implementation Notes

## Implemented in this foundation pass
- `_Game` folder layout and assembly boundaries.
- Shared contracts for world generation, persistence, economy, jobs, combat, logistics, networking, and backend descriptors.
- Deterministic local world generation and JSON snapshot persistence scaffolding.
- Shell bootstrap services, top-down strategic camera, and offline world bootstrap flow.
- ECS bootstrap, workplace productivity, and reservation-expiry systems.
- Editor setup tooling to generate the pivot scenes and core config asset.
- Edit mode tests for hex math, deterministic generation, and snapshot round-tripping.

## Deferred to later milestones
- Full ECS world conversion and chunk streaming.
- Production UI screens for the new menu and HUD.
- NetCode ghosts, replication, and dedicated-server hosting.
- Large-scale pathfinding, combat resolution, AI planners, and background simulation.
- Content assets, authoring inspectors, and balance tuning.
