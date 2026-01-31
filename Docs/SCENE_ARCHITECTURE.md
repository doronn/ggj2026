# Breaking Hue - Scene Architecture Documentation

This document describes the scene organization, required GameObjects, and setup procedures for Breaking Hue.

## Table of Contents
- [Overview](#overview)
- [Scene List](#scene-list)
- [MainMenu Scene](#mainmenu-scene)
- [World Scene](#world-scene)
- [Scene Transitions](#scene-transitions)
- [Dependency Injection Setup](#dependency-injection-setup)
- [Troubleshooting](#troubleshooting)

---

## Overview

Breaking Hue uses a two-scene architecture:

1. **MainMenu** - Title screen, settings, save management
2. **World** - Main gameplay scene where all levels are loaded

Levels are **not** separate scenes. Instead, level content is dynamically generated within the World scene from `LevelData` ScriptableObjects.

### Design Rationale

- **Persistent Services** - DI container, managers stay alive in World scene
- **Fast Level Switching** - No scene load for level transitions
- **Checkpoint Simplicity** - All state in single scene context
- **Memory Efficiency** - Destroy/spawn only what changes

---

## Scene List

| Scene | File | Purpose |
|-------|------|---------|
| MainMenu | `Assets/Scenes/MainMenu.unity` | Title screen, menu navigation |
| World | `Assets/Scenes/World.unity` | Gameplay, all levels |

### Build Settings

Configure in File > Build Settings:

```
Scenes In Build:
  0: Scenes/MainMenu    (first scene loaded)
  1: Scenes/World
```

---

## MainMenu Scene

### Required GameObjects

```
MainMenu (Scene)
├── Main Camera
│   └── AudioListener
│
├── EventSystem
│   └── StandaloneInputModule
│
├── UIDocument (Main Menu)
│   ├── UIDocument component
│   │   └── Source Asset: MainMenu.uxml
│   └── MainMenuController
│
└── [Optional] Background visuals
```

### MainMenuController Setup

```csharp
// Required on UIDocument GameObject
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "World";
}
```

### UI Requirements

- **UIDocument** with `MainMenu.uxml` assigned
- **Panel Settings** asset assigned to UIDocument
- Buttons: Play, Reset Progress, Quit (configurable)

### No Zenject in MainMenu

The MainMenu scene does **not** use Zenject. UI controllers use standard Unity patterns.

---

## World Scene

### Required GameObjects

```
World (Scene)
├── SceneContext                    [REQUIRED - Zenject]
│   ├── SceneContext component
│   │   └── Mono Installers: [GameInstaller]
│   └── GameInstaller component
│
├── Main Camera
│   ├── Camera component
│   ├── AudioListener
│   └── GameCamera component
│
├── Managers
│   ├── GameManager
│   │   └── GameManager component
│   │
│   ├── LevelManager
│   │   ├── LevelManager component
│   │   └── GameConfig reference
│   │
│   ├── CheckpointManager
│   │   └── CheckpointManager component
│   │
│   └── InputManager
│       └── InputManager component
│
├── UI
│   ├── GameHUD
│   │   ├── UIDocument (HUD.uxml)
│   │   └── GameHUDController
│   │
│   ├── PauseMenu
│   │   ├── UIDocument (PauseMenu.uxml)
│   │   └── PauseMenuController
│   │
│   └── ControlsBar
│       ├── UIDocument (ControlsBar.uxml)
│       └── ControlsBarController
│
├── Lighting
│   └── Directional Light
│
└── LevelContent                    [Dynamic - populated at runtime]
    ├── Ground/
    ├── Walls/
    ├── Barriers/
    ├── Pickups/
    └── ...
```

### Component Configuration

#### SceneContext (Zenject)
```
SceneContext:
  - Mono Installers: [GameInstaller]
  - Parent New Objects Under Context: ✓
```

#### GameInstaller
```csharp
// Attached to SceneContext or separate GameObject
public class GameInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<MaskInventory>().AsSingle().NonLazy();
        Container.Bind<LevelGenerator>().FromComponentInHierarchy().AsSingle();
        Container.Bind<GameManager>().FromComponentInHierarchy().AsSingle();
        // ... other bindings
    }
}
```

#### GameCamera
```
GameCamera:
  - Target: [assigned at runtime to Player]
  - Distance: 12
  - Height Angle: 60
  - Rotation Speed: 120
  - Mouse Sensitivity: 0.3
```

#### LevelManager
```
LevelManager:
  - Game Config: [GameConfig.asset]
  - Level Content Parent: [LevelContent transform]
```

#### UI Documents
Each UI document requires:
- UIDocument component with UXML source
- Corresponding controller script
- Panel Settings reference

---

## Scene Transitions

### MainMenu → World

```csharp
// MainMenuController.cs
public void OnPlayClicked()
{
    SceneManager.LoadScene("World");
}
```

The World scene's `LevelManager.Start()` automatically loads `GameConfig.startingLevel`.

### World → MainMenu

```csharp
// GameManager.cs or PauseMenuController.cs
public void ReturnToMenu()
{
    Time.timeScale = 1f;  // Reset if paused
    SceneManager.LoadScene("MainMenu");
}
```

### Level Transitions (Within World)

Levels don't require scene changes. `LevelManager` handles transitions:

```csharp
// LevelManager.cs
public void TransitionToLevel(EntranceExitLink link, string sourcePortalId)
{
    // 1. Clear current level content
    ClearLevel();
    
    // 2. Find destination level from link
    var destination = link.GetDestination(sourcePortalId);
    var nextLevel = _gameConfig.FindLevelById(destination.levelId);
    
    // 3. Generate new level
    LoadLevel(nextLevel);
    
    // 4. Position player at destination portal
    SpawnPlayerAtPortal(destination.portalId);
}
```

---

## Dependency Injection Setup

### Zenject Configuration

Breaking Hue uses **Scene-scoped** Zenject (no ProjectContext).

#### Required Setup

1. **SceneContext GameObject**
   - Add to World scene root
   - Attach `SceneContext` component
   - Attach `GameInstaller` component
   - Drag `GameInstaller` to SceneContext's Mono Installers list

2. **Component Bindings**
   GameInstaller binds these types:

   | Binding | Type | Source |
   |---------|------|--------|
   | MaskInventory | Singleton | Created by container |
   | LevelGenerator | Single | FromComponentInHierarchy |
   | GameManager | Single | FromComponentInHierarchy |
   | LevelManager | Single | FromComponentInHierarchy |
   | CheckpointManager | Single | FromComponentInHierarchy |
   | InputManager | Single | FromComponentInHierarchy |
   | GameCamera | Single | FromComponentInHierarchy |
   | GameHUDController | Single | FromComponentInHierarchy |
   | PauseMenuController | Single | FromComponentInHierarchy |
   | ControlsBarController | Single | FromComponentInHierarchy |
   | InputIconProvider | Single | FromComponentInHierarchy |

3. **Injection in Components**
   ```csharp
   public class MyComponent : MonoBehaviour
   {
       private MaskInventory _inventory;
       
       [Inject]
       public void Construct(MaskInventory inventory)
       {
           _inventory = inventory;
       }
   }
   ```

### Fallback Resolution

For runtime-instantiated objects (spawned via `Instantiate()`), use fallback:

```csharp
private void Start()
{
    if (_inventory == null)
    {
        var sceneContext = FindObjectOfType<SceneContext>();
        _inventory = sceneContext?.Container?.TryResolve<MaskInventory>();
    }
}
```

---

## Setting Up a New Scene

### Creating World Scene from Scratch

1. **Create Scene**
   - File > New Scene (Empty)
   - Save as `World.unity` in `Assets/Scenes/`

2. **Add Zenject Context**
   - GameObject > Zenject > Scene Context
   - Rename to "SceneContext"
   - Add `GameInstaller` component
   - Drag GameInstaller to SceneContext's Mono Installers

3. **Add Camera**
   - GameObject > Camera
   - Add `GameCamera` component
   - Add `AudioListener` component

4. **Add Managers Empty**
   - Create empty "Managers" GameObject
   - Create children: GameManager, LevelManager, CheckpointManager, InputManager
   - Add corresponding script components
   - Assign GameConfig to LevelManager

5. **Add UI Empty**
   - Create empty "UI" GameObject
   - Create children: GameHUD, PauseMenu, ControlsBar
   - Add UIDocument + Controller to each
   - Assign UXML sources

6. **Add Lighting**
   - GameObject > Light > Directional Light
   - Configure for top-down visibility

7. **Add Level Content Parent**
   - Create empty "LevelContent" GameObject
   - Assign to LevelManager.levelContentParent

8. **Save and Test**
   - Save scene
   - Add to Build Settings
   - Test in Play mode

---

## Troubleshooting

### Common Issues

#### "Assert failed: SceneContext has not been initialized yet"
**Cause:** Code running before Zenject initializes
**Fix:** Move code from `Awake()` to `Start()` or use `[Inject]`

#### "Could not resolve type 'MaskInventory'"
**Cause:** Missing binding or SceneContext not configured
**Fix:**
1. Verify SceneContext exists in scene
2. Verify GameInstaller is in Mono Installers list
3. Verify binding exists in GameInstaller.InstallBindings()

#### Player not spawning
**Cause:** LevelManager not configured
**Fix:**
1. Assign GameConfig to LevelManager
2. Verify GameConfig.startingLevel is set
3. Verify GameConfig.prefabs.playerPrefab assigned

#### UI not appearing
**Cause:** UIDocument misconfigured
**Fix:**
1. Verify UXML source assigned
2. Verify PanelSettings assigned
3. Check UIDocument is on active GameObject
4. Verify controller component attached

#### Camera not following player
**Cause:** GameCamera.Target not assigned
**Fix:** GameCamera automatically finds "Player" tagged object. Ensure:
1. Player prefab has "Player" tag
2. GameCamera.FindTarget() runs after player spawns

### Validation Checklist

Use this checklist when setting up World scene:

- [ ] SceneContext exists
- [ ] GameInstaller attached to SceneContext
- [ ] GameInstaller in Mono Installers list
- [ ] Main Camera has GameCamera component
- [ ] GameManager component in scene
- [ ] LevelManager with GameConfig assigned
- [ ] CheckpointManager component in scene
- [ ] InputManager component in scene
- [ ] All three UI documents (HUD, Pause, Controls)
- [ ] Each UI has controller component
- [ ] LevelContent parent exists
- [ ] Directional Light configured
- [ ] Scene added to Build Settings

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Assets/Scenes/MainMenu.unity` | Menu scene |
| `Assets/Scenes/World.unity` | Gameplay scene |
| `Assets/Scripts/Installers/GameInstaller.cs` | Zenject bindings |
| `Assets/Scripts/Level/LevelManager.cs` | Level loading/transitions |
| `Assets/Scripts/Core/GameManager.cs` | Game state, scene transitions |

---

*Last updated: January 2026*
