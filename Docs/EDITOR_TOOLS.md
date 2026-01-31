# Breaking Hue - Editor Tools Documentation

This document covers the Unity Editor tools available in Breaking Hue for game setup, asset management, and development workflows.

## Table of Contents
- [Overview](#overview)
- [Game Setup Window](#game-setup-window)
- [Auto Connect Window](#auto-connect-window)
- [Example Level Generator](#example-level-generator)
- [Third Person Migration Window](#third-person-migration-window)
- [Workflow Guide](#workflow-guide)

---

## Overview

Breaking Hue includes several custom Editor windows to streamline development. All tools are accessible via the **Window > Breaking Hue** menu.

| Tool | Menu Path | Purpose |
|------|-----------|---------|
| Game Setup | Window > Breaking Hue > Game Setup | Generate all game assets from scratch |
| Auto Connect | Window > Breaking Hue > Auto Connect | Connect existing assets to GameConfig |
| Example Levels | Window > Breaking Hue > Generate Example Levels | Create tutorial/demo levels |
| Third Person Migration | Window > Breaking Hue > Third Person Migration | Migrate camera system |

### File Locations

```
Assets/Editor/
├── GameSetupWindow.cs           # Full asset generation
├── AutoConnectWindow.cs         # Asset connection tool
├── ExampleLevelGenerator.cs     # Level generation
├── ThirdPersonMigrationWindow.cs # Camera migration
├── LevelEditorWindow.cs         # Level Editor (separate docs)
├── LevelEditorAction.cs         # Level Editor support
├── LevelDataSnapshot.cs         # Level Editor support
└── LevelEditorHelpWindow.cs     # Level Editor help
```

---

## Game Setup Window

**Menu:** Window > Breaking Hue > Game Setup

**File:** `Assets/Editor/GameSetupWindow.cs`

### Purpose

Generates all core game assets for a fresh Breaking Hue project. This is a one-time setup tool that creates:

- RYB color materials (barriers, pickups, barrels)
- All gameplay prefabs (Player, Barrier, Pickup, etc.)
- UI assets (UXML, USS, PanelSettings)
- Scene files (MainMenu, World)
- Example levels
- GameConfig asset
- Documentation

### When to Use

- Setting up a new Breaking Hue project
- Regenerating assets after major changes
- Creating a clean slate for testing

### Features

#### Full Setup Workflow
Runs all generation steps in sequence:
1. Creates `Assets/Materials/` folder
2. Generates all RYB color materials
3. Creates `Assets/Prefabs/` folder
4. Generates all prefabs with correct components
5. Creates `Assets/UI/` folder
6. Generates UI Toolkit assets
7. Creates `Assets/Scenes/` folder
8. Generates MainMenu and World scenes
9. Creates `Assets/Levels/` folder
10. Generates example level data
11. Creates `Assets/Config/` folder
12. Generates GameConfig.asset
13. Creates documentation files

#### Individual Steps
Each step can be run independently:
- **Generate Materials** - Creates M_Barrier_*, M_Pickup_*, M_Barrel_*, etc.
- **Generate Prefabs** - Creates Player, Barrier, Pickup, Bot, Portal, etc.
- **Generate UI** - Creates HUD.uxml, PauseMenu.uxml, ControlsBar.uxml
- **Generate Scenes** - Creates MainMenu.unity, World.unity
- **Generate Levels** - Creates example LevelData assets
- **Generate Config** - Creates GameConfig.asset

### Generated Materials

| Material | Type | Properties |
|----------|------|------------|
| M_Barrier_[Color] | Transparent | Alpha blend, emission |
| M_Pickup_[Color] | Opaque | High emission (0.8) |
| M_Barrel_[Color] | Opaque | Dimmed (80% brightness) |
| M_Player | Opaque | Grey, low emission |
| M_Bot | Opaque | Cyan, medium emission |
| M_Portal | Opaque | Cyan, high emission (2.0) |
| M_Portal_Checkpoint | Opaque | Gold, very high emission (2.5) |
| M_Wall | Opaque | White, low emission |
| M_Floor | Opaque | Dark blue-grey |
| M_Hidden | Opaque | Dark grey, minimal emission |

### Generated Prefabs

| Prefab | Components | Material |
|--------|------------|----------|
| Player.prefab | PlayerController, Rigidbody, CapsuleCollider | M_Player |
| Bot.prefab | BotController, BotInventory, Rigidbody | M_Bot |
| ColorBarrier.prefab | ColorBarrier, BoxCollider (solid + trigger) | M_Barrier_Red |
| MaskPickup.prefab | MaskPickup, BoxCollider (trigger) | M_Pickup_Red |
| DroppedMask.prefab | DroppedMask, SphereCollider (trigger) | Dynamic |
| ExplodingBarrel.prefab | ExplodingBarrel, BoxCollider (solid + trigger) | M_Barrel_Red |
| Portal.prefab | Portal, BoxCollider (trigger) | M_Portal |
| HiddenBlock.prefab | HiddenBlock, BoxCollider (trigger) | M_Hidden |
| Floor.prefab | None | M_Floor |
| Wall.prefab | BoxCollider | M_Wall |

### Usage

1. Open **Window > Breaking Hue > Game Setup**
2. Click **Run Full Setup** for complete generation
3. Or click individual buttons for specific assets
4. Check console for progress and any errors

### Notes

- Running Full Setup will **overwrite existing assets** with the same names
- Materials use URP Lit shader - ensure URP is installed
- Scenes include pre-configured SceneContext for Zenject

---

## Auto Connect Window

**Menu:** Window > Breaking Hue > Auto Connect

**File:** `Assets/Editor/AutoConnectWindow.cs`

### Purpose

Automatically connects existing assets to the GameConfig and fixes scene setup issues. Use this after manually creating or modifying assets.

### When to Use

- After creating new prefabs manually
- After creating new LevelData assets
- After creating new EntranceExitLink portal connections
- When GameConfig has missing references
- When World scene has setup issues

### Features

#### Auto-Connect Functions

1. **Populate Prefabs**
   - Finds all prefabs in `Assets/Prefabs/`
   - Matches to GameConfig.prefabs fields by name
   - E.g., `Player.prefab` → `GameConfig.prefabs.playerPrefab`

2. **Populate Levels**
   - Finds all LevelData assets in `Assets/Levels/`
   - Adds to `GameConfig.allLevels` list
   - Sets first level as `startingLevel` if not set

3. **Populate Portal Links**
   - Finds all EntranceExitLink assets
   - Adds to `GameConfig.portalLinks` list

4. **Fix World Scene**
   - Creates/configures SceneContext
   - Adds GameInstaller component
   - Configures LevelManager reference
   - Configures CheckpointManager reference

#### Validation

The window shows validation status:
- ✅ Green checkmarks for valid connections
- ❌ Red X for missing references
- **Fix** buttons for automatic repairs

### Usage

1. Open **Window > Breaking Hue > Auto Connect**
2. Review validation status
3. Click **Auto-Connect All** for full connection
4. Or click individual **Fix** buttons
5. **Save** the scene and project after changes

### Validation Checks

| Check | Validates | Fix Action |
|-------|-----------|------------|
| Prefabs Valid | All prefab slots filled | Searches Assets/Prefabs/ |
| Levels Valid | At least one level | Searches Assets/Levels/ |
| Starting Level Set | startingLevel not null | Sets first found level |
| Portal Links Valid | All links have both portals | Reports invalid links |
| Scene Context | World has SceneContext | Creates and configures |
| Game Installer | SceneContext has installer | Adds GameInstaller |

---

## Example Level Generator

**Menu:** Window > Breaking Hue > Generate Example Levels

**File:** `Assets/Editor/ExampleLevelGenerator.cs`

### Purpose

Generates example LevelData assets demonstrating game features. Useful for testing and as templates for level design.

### When to Use

- Testing game mechanics
- Learning level data structure
- Creating starting point for custom levels
- Regression testing after code changes

### Generated Levels

#### Level01_Tutorial

A simple introductory level teaching basic mechanics:
- Single color barriers (Red, Yellow, Blue)
- Corresponding pickups before each barrier
- Linear path to exit
- One checkpoint portal

#### Level02_Advanced

A complex level showcasing advanced mechanics:
- Secondary color barriers (Orange, Green, Purple)
- Multiple paths
- Exploding barrels
- Bots with paths
- Hidden areas with secrets
- Multiple portals connecting areas

### Level Structure

Each generated level includes:

```csharp
LevelData {
    levelId: "level_01_tutorial",
    levelName: "Tutorial",
    gridSize: 32x32,
    cellSize: 1.0,
    
    groundPositions: [...],      // Walkable floor
    wallPositions: [...],        // Solid walls
    barriers: [BarrierData...],  // Color barriers
    pickups: [PickupData...],    // Mask pickups
    barrels: [BarrelData...],    // Exploding barrels
    bots: [BotData...],          // Bot entities
    portals: [PortalData...],    // Teleport portals
    hiddenBlocks: [...],         // Hidden areas
    playerSpawnPosition: Vector2Int
}
```

### Usage

1. Open **Window > Breaking Hue > Generate Example Levels**
2. Click **Generate Tutorial Level** or **Generate Advanced Level**
3. Or click **Generate All** for both
4. Find generated assets in `Assets/Levels/`

### Customization

To modify generated levels:
1. Generate the level
2. Select the LevelData asset
3. Edit in Inspector or Level Editor
4. Save changes

---

## Third Person Migration Window

**Menu:** Window > Breaking Hue > Third Person Migration

**File:** `Assets/Editor/ThirdPersonMigrationWindow.cs`

### Purpose

Migrates the game from top-down 2D-style camera to third-person 3D camera system. This is a one-time migration tool.

### When to Use

- Converting existing top-down project to third-person
- Setting up third-person camera from scratch
- Fixing broken camera setup

### Migration Steps

1. **Backup Current Scene**
   - Creates timestamped backup: `World_backup_YYYYMMDD_HHMMSS.unity`

2. **Configure Camera**
   - Removes old camera setup
   - Adds GameCamera component to Main Camera
   - Configures third-person settings:
     - Distance: 12 units
     - Height Angle: 60 degrees
     - Rotation Speed: 120 deg/sec
     - Mouse Sensitivity: 0.3

3. **Update Player Prefab**
   - Ensures PlayerController is configured for 3D movement
   - Sets up camera-relative movement

4. **Create UI GameObjects**
   - Creates UIDocument GameObjects for:
     - GameHUD
     - PauseMenu
     - ControlsBar
   - Assigns UXML source assets
   - Adds controller components

5. **Setup InputManager**
   - Creates InputManager singleton if missing
   - Configures for keyboard/mouse and gamepad support

### Usage

1. **Save your current work** (backup recommended)
2. Open **Window > Breaking Hue > Third Person Migration**
3. Review what will be changed
4. Click **Run Migration**
5. Check console for any errors
6. Test camera in Play mode

### Post-Migration Checklist

- [ ] Camera follows player correctly
- [ ] Camera rotation works with mouse/right stick
- [ ] Movement is camera-relative
- [ ] UI elements display correctly
- [ ] Pause menu works
- [ ] Controls bar shows correct bindings

### Reverting Migration

If issues occur:
1. Close the scene without saving
2. Open the backup scene (`World_backup_*.unity`)
3. Rename back to `World.unity`

---

## Workflow Guide

### New Project Setup

1. **Initial Setup**
   - Create new Unity project with URP
   - Install required packages (Zenject, UniTask)
   - Open **Game Setup Window**
   - Click **Run Full Setup**

2. **Verify Setup**
   - Open **Auto Connect Window**
   - Verify all checks pass
   - Fix any issues

3. **Test**
   - Open World scene
   - Enter Play mode
   - Verify basic gameplay works

### Adding New Content

1. **Create Assets**
   - Create new prefabs/levels manually
   - Or use Level Editor for levels

2. **Connect Assets**
   - Open **Auto Connect Window**
   - Click **Auto-Connect All**
   - Save project

3. **Test**
   - Verify new content works in Play mode

### Troubleshooting

| Issue | Solution |
|-------|----------|
| Missing prefab references | Run Auto Connect > Populate Prefabs |
| Level won't load | Check GameConfig.allLevels, run Auto Connect |
| Injection errors | Verify SceneContext has GameInstaller |
| Camera not following | Run Third Person Migration |
| Materials pink/missing | Ensure URP is installed, regenerate materials |

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Assets/Editor/GameSetupWindow.cs` | Full asset generation |
| `Assets/Editor/AutoConnectWindow.cs` | Asset connection/validation |
| `Assets/Editor/ExampleLevelGenerator.cs` | Example level creation |
| `Assets/Editor/ThirdPersonMigrationWindow.cs` | Camera system migration |
| `Assets/Config/GameConfig.asset` | Central configuration |

---

*Last updated: January 2026*
