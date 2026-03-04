---
name: ggj2026-unity-control
description: Control the ggj2026 Unity project through the project-owned MCP server, live editor bridge, and approved batch wrappers. Use when the task needs compile requests, edit-mode or play-mode tests, scene open/play-mode control, UI plan apply, guided manual test execution, or checking bridge/editor state for this project.
---

# GGJ2026 Unity Control

Prefer the project-owned MCP control plane over ad hoc Unity commands.

## Workflow

1. Read `references/control-surface.md` for the current control commands and artifact paths.
2. Start from project status.
3. If the editor is open, prefer the live bridge for:
   - compile
   - tests
   - scene open
   - play mode enter/exit
   - guided manual test execution
   - gameplay HUD/context queries
   - gameplay action execution
4. If the editor is closed, use the project batch wrappers instead of inventing fresh Unity CLI invocations.
5. For gameplay automation, query first:
   - HUD state
   - selection
   - available actions
   - blockers
   - recent events when validating player-facing state transitions
6. After any compile or test action, inspect the repo-local artifacts before falling back to raw editor logs.
7. When validating a new gameplay slice, prefer a short MCP scenario that proves:
   - action can be issued
   - simulation advances
   - expected blocker/action state changes
   - telemetry reflects the same result
8. For new shared gameplay verbs, validate both:
   - `unity_game_execute_action`
   - the corresponding HUD/query availability state
9. For onboarding or interface work, validate the full MCP loop:
   - `unity_game_hud_state`
   - `StartOnboarding`
   - `NextOnboardingStep`
   - `SkipOnboarding`
   - confirm the HUD state changes after each step
10. For selection or interaction work, validate:
   - `unity_game_hover`
   - `unity_game_selection`
   - `unity_game_list_actions`
   - and at least one real gameplay action from the returned context

## Guardrails

- Do not start a second batch Unity process against the same project while the editor is already open.
- Prefer `Tools/run-unity-tests.sh` over hand-written Unity test commands when batch mode is required.
- Treat `Logs/McpBridge/status.json` as the live bridge source of truth.
- Expect live play-mode MCP test runs to sometimes report recovery wording after Unity’s cleanup bug; that is acceptable if the recovered NUnit XML is clean.
- Do not act blindly through MCP gameplay commands without checking the current context/action surface first.
- If the bridge heartbeat is stale, confirm the editor process state before trusting gameplay automation results.
- Treat action/query parity drift as a real regression, not only a tooling issue.

## Use the Right Path

- Use MCP first for routine project control.
- Use gameplay MCP queries/actions instead of screen-driving the editor for maintained player interactions.
- Use Unity menu items only when the bridge or batch fallback is unavailable.
- Use `~/Library/Logs/Unity/Editor.log` only after the repo-local compile, test, runtime, or telemetry artifacts are insufficient.

## References

- Read `references/control-surface.md` for the exact commands, bridge tools, and known recovery behaviors.
