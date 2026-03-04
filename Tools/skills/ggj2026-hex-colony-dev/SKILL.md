---
name: ggj2026-hex-colony-dev
description: Implement and evolve the maintained `_Game` runtime for the hex-colony simulation. Use when changing gameplay systems, contracts, persistence, telemetry-aware world mutations, room or occupancy logic, construction, logistics, tests, or other maintained 4X/RTS code under `Assets/_Game`.
---

# GGJ2026 Hex Colony Dev

Keep gameplay work inside `_Game` and keep it testable.

## Workflow

1. Read `references/runtime-boundaries.md`.
2. Confirm the task belongs to `Assets/_Game`, not legacy `Assets/Scripts`.
3. Change the smallest maintained runtime surface that matches the feature:
   - `Shared` for contracts and DTOs
   - `Shell` for local orchestration and interactive flows
   - `Simulation` for ECS-facing runtime pieces
4. Preserve the current physical-world direction:
   - layered cells and edges
   - modules on floors
   - landmarks as special footprints
   - units and stacks as physical world state
5. Add or update edit-mode tests for behavior.
6. Add play-mode coverage for integrated user-facing flows when relevant.
7. Update guided manual scenarios if the feature changes milestone behavior.
8. If the feature introduces or changes a player-facing verb, expose it through:
   - `GameActionService`
   - `GameQueryService`
   - HUD action surfaces
   - MCP gameplay action/query paths
9. For defense or progression slices, verify the full player loop, not just the data shape:
   - queue action
   - advance simulation
   - resulting world mutation
   - blocker/explanation updates
   - persistence/test coverage
10. Keep onboarding current:
   - if a core verb, blocker explanation, or early-player expectation changes, update onboarding and docs in the same pass
11. When adding presentation juice:
   - prefer readable movement/work feedback over decorative effects
   - keep unit state understandable from both the world view and HUD/MCP summaries
12. When changing construction or path access rules:
   - preserve normal enclosure steps
   - only block the final placement that would strand an important reachable work area
   - update the corresponding manual QA scenario

## Guardrails

- Do not add new maintained gameplay code under `Assets/Scripts`.
- Do not bypass telemetry on new mutating shell actions.
- Do not revert to single-hex “structure means building” assumptions for colony spaces.
- Respect UI input blocking when adding interactive world tools.
- Do not add a maintained gameplay action only to a local UI callback.
- If the slice changes colony readiness or threat behavior, update the world-tab summaries and blocker language at the same time.
- If the slice changes core player understanding, update onboarding and guided manual scenarios in the same pass.
- If the slice changes what a click selects, update hover/selection labels, contextual actions, and MCP selection/query parity together.

## References

- Read `references/runtime-boundaries.md` for the current architecture, rules, and common edit targets.
