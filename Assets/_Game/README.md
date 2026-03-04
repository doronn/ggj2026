# _Game Foundation

This folder is the new runtime foundation for the persistent hex-world 4X/RTS pivot.

## Boundaries
- `Shared/`: stable contracts, coordinates, save/network DTOs, and cross-stream interfaces.
- `Shell/`: bootstrap, scene navigation, UI shell, camera, and local session orchestration.
- `Simulation/`: ECS-first runtime state and systems for colony, logistics, and combat simulation.
- `Authoring/`: ScriptableObject assets, setup tools, and editor automation for the new runtime.
- `Networking/`: network/session orchestration and collision-resolution scaffolding.
- `Presentation/`: entity presentation bridges and scene-facing proxies.
- `Tests/`: edit mode and play mode verification for the new runtime.

## Rules
- Do not add new 4X gameplay code under `Assets/Scripts`.
- Treat the existing Breaking Hue runtime as legacy reference until it is fully migrated.
- Keep all cross-stream contracts in `Assets/_Game/Shared`.
