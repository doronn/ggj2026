# UI Surface

## Maintained runtime panels

- `Assets/_Game/UI/Runtime/UXML/ClientWorldHud.uxml`
- `Assets/_Game/UI/Runtime/UXML/PersistentMainMenu.uxml`
- `Assets/_Game/UI/Runtime/UXML/BootstrapStatus.uxml`

## Maintained editor UI

- `Assets/_Game/UI/Editor/UXML/ManualTestRunnerWindow.uxml`
- `Assets/_Game/UI/Editor/UXML/UiPlanIntegrationWindow.uxml`
- `Assets/_Game/UI/Editor/UXML/PivotFoundationSetupWindow.uxml`

## Key controllers and services

- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Assets/_Game/Shell/World/ClientWorldHudController.cs`
- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Assets/_Game/Shell/UI/RuntimeInputCaptureService.cs`
- `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Assets/_Game/Authoring/Editor/UiPlanInstallerService.cs`

## Installer path

- Editor window:
  - `Window > GGJ2026 > UI > Apply Planned UI`
- Latest report:
  - `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Diagnostics/ui-install-latest.json`

## Mandatory regression focus

- UI clicks must not leak into world interaction.
- Required named elements must still bind correctly.
- UIDocuments must continue to use the maintained panel settings path.
