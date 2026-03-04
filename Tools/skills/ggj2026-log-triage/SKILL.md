---
name: ggj2026-log-triage
description: Diagnose compile, runtime, telemetry, MCP bridge, and Unity test issues in ggj2026. Use when something fails or behaves unexpectedly and the answer depends on reading compile logs, runtime logs, telemetry sessions, test XML, bridge status, or Editor.log in the correct order.
---

# GGJ2026 Log Triage

Use the project’s structured artifacts first and raw Unity logs second.

## Triage Order

1. Read `references/log-order.md`.
2. Classify the problem:
   - compile
   - runtime/session behavior
   - edit-mode tests
   - play-mode tests
   - MCP/bridge control
3. Read the smallest authoritative artifact first.
4. Correlate with telemetry or NUnit XML before trusting Unity console noise.
5. Escalate to `Editor.log` only when repo-local artifacts are missing, incomplete, or contradicted by editor state.

## Guardrails

- Do not treat stale log tails as the latest truth without checking timestamps.
- Distinguish actual compile failures from Unity play-mode test cleanup noise.
- For `_Game`, structured telemetry is the canonical session record.
- For bridge issues, read `status.json` before assuming the editor is idle or updated.

## References

- Read `references/log-order.md` for exact file priority and common false-positive signatures.
