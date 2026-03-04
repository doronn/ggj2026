# Breaking Hue Legacy Boundary

The original puzzle/action runtime remains the reference implementation for legacy content.

Current legacy runtime locations:
- `Assets/Scripts`
- `Assets/Scenes/World.unity`
- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/EndGame.unity`
- `Assets/Levels`
- `Assets/Prefabs`

## Guardrail
- Do not place new persistent-world 4X/RTS runtime code in the legacy folders.
- Migrate systems intentionally into `Assets/_Game` instead of extending the old scene flow.
- Preserve existing files while the pivot is under active development, especially while user changes remain uncommitted.
