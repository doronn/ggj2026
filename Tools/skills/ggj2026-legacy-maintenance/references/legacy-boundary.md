# Legacy Boundary

## Legacy paths

- `Assets/Scripts`
- `Assets/Editor`
- `Assets/Scenes` legacy scene content
- `Assets/Legacy/BreakingHue`

## Use this skill for

- compile blockers from obsolete Unity APIs
- editor tool compatibility fixes
- warnings or errors that stop the project from building or testing

## Do not use this skill for

- new 4X/RTS gameplay
- new UI Toolkit product UI
- new telemetry, MCP, or `_Game` runtime work

## Common fixes

- replace deprecated Unity object find APIs with current equivalents
- resolve namespace or asmdef breakage after package updates
- make focused compatibility fixes without altering game design
