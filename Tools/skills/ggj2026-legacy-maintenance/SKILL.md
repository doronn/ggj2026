---
name: ggj2026-legacy-maintenance
description: Maintain the legacy Breaking Hue code only when it still affects the ggj2026 project. Use when fixing compile blockers, obsolete Unity API usage, or compatibility issues under `Assets/Scripts` or `Assets/Editor`, while preserving the boundary that new 4X/RTS gameplay belongs under `Assets/_Game`.
---

# GGJ2026 Legacy Maintenance

Touch legacy only when it still matters to project health.

## Workflow

1. Read `references/legacy-boundary.md`.
2. Confirm the issue is actually in legacy code and still blocks compile, tooling, or compatibility.
3. Make the smallest viable fix.
4. Prefer API modernizations and compile fixes over behavior changes.
5. Re-run compile or tests to ensure the legacy fix did not spill into `_Game`.

## Guardrails

- Do not add new 4X gameplay in legacy folders.
- Do not expand old systems just because they are already there.
- Do not rewrite broad legacy surfaces when a targeted compatibility fix is enough.
- Keep the `_Game` runtime as the primary maintained product path.

## References

- Read `references/legacy-boundary.md` for the do-not-cross rules and common legacy fix patterns.
