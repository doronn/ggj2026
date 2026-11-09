---
name: ggj2026-ui-toolkit
description: Work on maintained ggj2026 UI Toolkit assets, controllers, and wiring. Use when editing UXML, USS, UIDocument controllers, runtime input capture, UI plan installer behavior, or editor windows that must stay on UI Toolkit instead of IMGUI.
---

# GGJ2026 UI Toolkit

Treat UI Toolkit as the only maintained UI stack.

## Workflow

1. Read `references/ui-surface.md`.
2. Start from the relevant UXML, USS, and controller together.
3. Keep structure in UXML, presentation in USS, and behavior in the controller/binder.
4. Preserve the maintained HUD composition model:
   - top world bar
   - operations rail
   - context inspector
   - event strip
   - onboarding overlay card
5. If scene wiring may change, use the UI plan installer path instead of manual scene poking.
6. Preserve the input-capture rule: HUD interaction must block world mutation.
7. Keep the human HUD and MCP gameplay interface on the same action/query model.
8. Keep onboarding readable:
   - highlight one region at a time
   - hide irrelevant options until they matter
   - support skip and restart through the shared action layer
9. Add or update tests for UI binding, context rendering, onboarding visibility, and input blocking.
10. Preserve world-space clarity:
   - hover labels should preview what a click will target
   - selected labels should make the current target obvious
   - contextual `Next` actions should reduce permanent tool clutter

## Guardrails

- Do not add new maintained `OnGUI` UI.
- Do not bypass `RuntimeInputCaptureService`.
- Do not hand-wire maintained scene UI when the installer should own that wiring.
- Do not add gameplay verbs directly to one button without `GameActionService` parity.
- Contextual cards must explain blocked states in short readable language.
- Keep copy accessible: short labels, explicit wording, not color-only meaning.
- Prefer contextual action surfacing over always-visible exhaustive tool lists.
- Keep operations rail progressively disclosed:
  - always keep simulation/view/context guidance reachable
  - show floors/edges/modules/landmarks/cleanup only when relevant to current context or active tool
- Keep a readable fallback path when runtime stylesheet binding is missing:
  - avoid unreadable default button/text colors
  - keep minimum button size and contrast in controller fallback styles
- Added motion/juice must support readability:
  - movement should feel alive but land clearly on a tile
  - work animation should show activity without hiding state
- Update UI-related manual test scenarios when interaction changes are user-facing.

## References

- Read `references/ui-surface.md` for the current panel set, installer path, and binding rules.
