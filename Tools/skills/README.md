# Repo-local Skills

This folder contains repo-local Codex skill drafts for the `ggj2026` project.

They are intended to capture the project-specific workflows that now exist in the repo:

- `ggj2026-unity-control`
  - Use the project-owned MCP server, live Unity bridge, and approved batch wrappers.
- `ggj2026-log-triage`
  - Read compile, runtime, telemetry, bridge, and test artifacts in the correct order.
- `ggj2026-hex-colony-dev`
  - Work on maintained `_Game` gameplay, persistence, telemetry-aware mutations, and tests.
- `ggj2026-manual-qa`
  - Author and run guided manual test scenarios through the editor or MCP bridge.
- `ggj2026-ui-toolkit`
  - Maintain UI Toolkit runtime/editor UI, installer wiring, and input-capture rules.
- `ggj2026-legacy-maintenance`
  - Make minimal compatibility or compile fixes in legacy `Assets/Scripts` and `Assets/Editor`.

## Structure

Each skill folder contains:

- `SKILL.md`
- `agents/openai.yaml`
- `references/*.md`

## Validation

Use the repo-local validator:

- `Tools/skills/validate-repo-skills.sh`

This validator is intentionally shell-based so it does not depend on external Python packages.

## Installation note

These skills live in the repo so future agents can inspect and reuse them even if they are not installed globally into `$CODEX_HOME/skills`.
