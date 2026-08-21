# GGJ 2026 — domain feature authority

This repository owns the Global Game Jam 2026 Unity project pinned to Unity `6000.3.5f2`. Product and system details remain in `GameMechanicsGuide.md`, `DevPlan.md`, `Docs/`, and `Assets/Documentation/`.

The Personal Platform provides backup, isolated on-demand CI, and build evidence. It does not silently upgrade Unity, packages, scenes, or assets.

## Acceptance

- The project opens using the pinned editor without serialization upgrade.
- Documented gameplay, scene, audio, controls, editor-tool, and asset-management checks pass.
- Build artifacts identify commit, Unity version, platform, dependency locks, test evidence, and hash.
