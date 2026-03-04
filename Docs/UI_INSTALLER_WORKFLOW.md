# UI Installer Workflow

## Entry point
Use:
- `Window > GGJ2026 > UI > Apply Planned UI`

## Manifest
Canonical manifest asset:
- `Assets/_Game/UI/UiPlanManifest.asset`

It defines:
- runtime panels
- scene bindings
- controller types
- UXML/USS assets
- panel settings

## Installer behavior
The installer:
- creates or refreshes the manifest
- creates the shared runtime `PanelSettings` asset when missing
- ensures maintained `_Game` scenes have the expected `UIDocument` hosts
- assigns controller references to UXML/USS/panel settings
- is safe to re-run

## Modes
- `Dry Run`
  - previews scene/document work and writes a report
- `Apply UI`
  - performs the wiring and writes a report

## Reports
Installer reports are written to:
- `Logs/Diagnostics/ui-install-latest.json`
- `Logs/Diagnostics/ui-install-YYYYMMDD-HHMMSS.json`

## Validation expectations
After running the installer, verify:
- scenes have the expected host objects
- `UIDocument` components exist on those hosts
- controller references are assigned
- runtime panel assets are present in the manifest
