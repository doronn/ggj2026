# ADR-0001: Persistent World Foundation

## Status
Accepted

## Context
- The repository currently contains a small single-player puzzle/action runtime.
- The pivot requires a persistent hex-world survival 4X/RTS with 500+ units, AI factions, large-scale battles, and future dedicated-server online play.
- Existing runtime files under `Assets/Scripts` and `Assets/Scenes` are actively changing and cannot be destructively restructured mid-stream.

## Decision
- Introduce the new runtime under `Assets/_Game`.
- Freeze the existing runtime behind a legacy boundary and stop adding new 4X systems there.
- Use a hybrid model:
  - Zenject and UniTask for shell orchestration.
  - DOTS/ECS for scalable simulation.
  - Shared contracts in `Assets/_Game/Shared`.
- Add editor automation to generate the new pivot scenes and config assets instead of hand-editing legacy scenes.

## Consequences
- The repo can evolve incrementally without blocking ongoing legacy changes.
- The new runtime can be delegated across multiple developers and AI agents with clean folder and assembly ownership.
- Scene migration becomes explicit and repeatable through tooling rather than one-off manual edits.
