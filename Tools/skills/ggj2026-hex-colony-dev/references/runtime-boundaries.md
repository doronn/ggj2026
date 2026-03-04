# Runtime Boundaries

## Maintained code

- Shared contracts: `Assets/_Game/Shared`
- Shell/runtime orchestration: `Assets/_Game/Shell`
- Editor/runtime tooling: `Assets/_Game/Authoring`
- Tests: `Assets/_Game/Tests`

## Core current model

- Hex cells with layered state
- Edge features for walls and doors
- Space purpose from floors
- Function from modules
- Landmarks only for special civic/defensive footprints
- Physical stacks and units
- Structured telemetry around mutating shell actions

## Common gameplay files

- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Assets/_Game/Shell/World/LocalColonyGameplayService.cs`
- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Assets/_Game/Shell/World/DeterministicHexWorldGenerator.cs`
- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Assets/_Game/Shared/Economy/StructureCatalog.cs`
- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Assets/_Game/Shared/World`

## Test expectation

- Edit-mode tests are mandatory for behavioral logic.
- Play-mode tests are mandatory for integrated UI/runtime flows.
- Guided manual scenarios are required for milestone-facing behavior changes.
