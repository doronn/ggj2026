---
name: ggj2026-manual-qa
description: Create, run, and extend guided manual QA scenarios for ggj2026. Use when authoring `ManualTestScenarioDefinition` assets, running milestone sanity checks, recording step outcomes through the editor or MCP bridge, or verifying that gameplay and UI changes are covered by manual test prompts and artifacts.
---

# GGJ2026 Manual QA

Use guided manual tests for milestone sanity, not ad hoc memory.

## Workflow

1. Read `references/manual-test-flow.md`.
2. Prefer existing scenarios under `Assets/_Game/Authoring/ManualTests`.
3. If coverage is missing, add or extend a scenario asset instead of writing one-off notes.
4. Make each step explicit:
   - scene
   - action
   - expected result
   - sanity checks
5. Include UI-comprehension and blocker-discoverability checks when user-facing interface changes land.
6. Include onboarding checks when the guide, HUD structure, or first-session experience changes:
   - guide appears
   - irrelevant options stay hidden
   - skip works
   - restart works
   - copy explains each interface region clearly
7. Include human-vs-agent parity checks when gameplay actions or contextual UI change.
8. For defense slices, include both defended and undefended outcomes when practical:
   - readiness before threat
   - threat/raid countdown visibility
   - defender order visibility
   - success case
   - failure/loot case
9. Run through the editor window or MCP bridge.
10. Verify artifacts land under `Logs/Telemetry/manual-tests` and, when applicable, `Logs/Telemetry/session-latest/manual-test.json`.
11. For interaction-clarity changes, include:
   - hover preview text
   - selected world label clarity
   - contextual `Next` action relevance
   - access-safe enclosure behavior

## Guardrails

- Manual tests do not replace automated tests.
- Add manual scenarios for significant milestone-facing features.
- Keep steps short and observable.
- Prefer telemetry-verifiable expectations when possible.
- If the HUD and MCP disagree about available actions or blocker reasons, treat that as a real regression.
- If a scenario depends on simulation time, state roughly how many ticks or seconds the tester should wait.
- Presentation changes should be tested as readability, not only aesthetics:
  - movement is visible
  - work state is visible
  - effects do not hide where a unit actually is

## References

- Read `references/manual-test-flow.md` for asset shape, execution paths, and current seeded scenarios.
