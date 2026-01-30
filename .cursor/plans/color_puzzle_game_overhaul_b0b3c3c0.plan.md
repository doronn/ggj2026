---
name: Color Puzzle Game Overhaul
overview: "Complete overhaul of the color puzzle game: migrate from RGB to RYB color system with Black as advanced mechanic, implement multi-mask activation with residue system, add bots with pathfinding, exploding barrels, multi-level navigation with persistent inventory, and build a comprehensive Unity Editor level creation tool."
todos:
  - id: phase1-color
    content: Refactor ColorType.cs to RYB system with Orange/Green/Purple/Black
    status: completed
  - id: phase1-inventory
    content: Overhaul MaskInventory for multi-toggle and residue calculation
    status: completed
  - id: phase1-barrier
    content: Update ColorBarrier for residue consumption system
    status: completed
  - id: phase2-barrel
    content: Implement ExplodingBarrel with color-based explosion mechanics
    status: completed
  - id: phase2-bot
    content: Create Bot system (controller, inventory, path following, interactions)
    status: completed
  - id: phase2-hidden
    content: Implement HiddenBlock reveal-on-touch system
    status: completed
  - id: phase2-drop
    content: Add mask drop mechanic and DroppedMask entity
    status: completed
  - id: phase3-leveldata
    content: Create LevelData, EntranceExitLink, LevelSaveData structures
    status: completed
  - id: phase3-manager
    content: Implement LevelManager for multi-level navigation
    status: completed
  - id: phase3-portal
    content: Create Portal system with bidirectional linking
    status: completed
  - id: phase4-editor
    content: Build Level Editor window with layer-based painting
    status: completed
  - id: phase4-botpath
    content: Add Bot Path editing tools to Level Editor
    status: completed
  - id: phase4-portal-link
    content: Add Portal linking tools to Level Editor
    status: completed
  - id: phase5-save
    content: Implement CheckpointManager with snapshot capture/restore system
    status: completed
  - id: phase6-camera
    content: Setup fixed orthographic camera with letterboxing
    status: completed
  - id: phase6-ui
    content: Update HUD for mask toggle states and RYB colors
    status: completed
  - id: phase7-setup
    content: Regenerate GameSetupWindow and delete old assets
    status: completed
  - id: phase8-levels
    content: Create tutorial levels and complex example level
    status: completed
isProject: false
---

# Color Puzzle Game Overhaul

## Phase 1: Core Color System Refactor

### 1.1 RYB Color Type ([`Assets/Scripts/Core/ColorType.cs`](Assets/Scripts/Core/ColorType.cs))

Replace RGB flags with RYB:

```csharp
[Flags]
public enum ColorType
{
    None   = 0,
    Red    = 1 << 0,  // 1
    Yellow = 1 << 1,  // 2
    Blue   = 1 << 2,  // 4
    
    // Secondaries
    Orange = Red | Yellow,     // 3
    Green  = Yellow | Blue,    // 6
    Purple = Red | Blue,       // 5
    
    // Tertiary
    Black  = Red | Yellow | Blue // 7
}
```

Update `ToColor()` to map RYB to visual Unity colors (Orange=#FF8000, Green=#00FF00, Purple=#800080, Black=#000000).

### 1.2 Mask Inventory Overhaul ([`Assets/Scripts/Core/MaskInventory.cs`](Assets/Scripts/Core/MaskInventory.cs))

- Add `bool[] _activeSlots` for multi-mask toggle
- Add methods: `ToggleMask(int slot)`, `GetCombinedActiveColor()`, `TransformMask(int slot, ColorType newColor)`
- Implement residue calculation: `CalculateOptimalSubtraction(ColorType required)` returns which masks contribute what
- Add `DropMask(int slot)` returning a `MaskPickup` to spawn

### 1.3 Barrier Residue System ([`Assets/Scripts/Gameplay/ColorBarrier.cs`](Assets/Scripts/Gameplay/ColorBarrier.cs))

- Change from single-mask consumption to multi-mask residue
- Call `inventory.CalculateOptimalSubtraction(requiredColor)` 
- Transform each contributing mask in-place via `TransformMask()`

---

## Phase 2: New Game Elements

### 2.1 Exploding Barrels (`Assets/Scripts/Gameplay/ExplodingBarrel.cs`)

- `ColorType barrelColor` - the color that triggers explosion
- Dual collider system (like barriers): blocks without matching mask
- On matching mask entry:
  - If player: Trigger `CheckpointManager.RestoreCheckpoint()` (respawn at last checkpoint)
  - If bot: Subtract barrel color from bot, drop remaining masks, destroy bot
- Destroy barrel after explosion

### 2.2 Bot System (`Assets/Scripts/Gameplay/Bot/`)

- **BotController.cs**: Movement along path, same physics as player
- **BotInventory.cs**: 3 slots, no duplicates, handles partial pickups with residue drops
- **BotPathData.cs** (ScriptableObject): List of waypoints, loop/ping-pong modes
- **BotMaskInteraction.cs**: 
  - Pick up masks (only missing colors, drop residue)
  - Pass through barriers (same rules as player)
  - Explode in barrels (drop masks, destroy self)
  - Regenerate initial color when passing matching color blocks
- **Bot-Player Collision**: Bot stops permanently until player moves away

### 2.3 Hidden Areas (`Assets/Scripts/Gameplay/HiddenBlock.cs`)

- Dark gray visual (not true black)
- No collision blocking
- On trigger enter: Disable renderer and collider, revealing area beneath
- Optional: Play reveal VFX/SFX

### 2.4 Dropped Mask (`Assets/Scripts/Gameplay/DroppedMask.cs`)

- Spawned at grid position when player drops
- Same pickup logic as `MaskPickup`
- Persists in level save data

---

## Phase 3: Level System Architecture

### 3.1 Level Data Structures (`Assets/Scripts/Level/Data/`)

- **LevelData.cs** (ScriptableObject): Grid data, entrance/exit pairs, bot configs
- **EntranceExitLink.cs** (ScriptableObject): Shared asset linking two portals via GUIDs
- **BotPathData.cs** (ScriptableObject): Waypoint list per bot
- **LevelSaveData.cs**: Runtime JSON - bot states, collected masks, destroyed barrels

### 3.2 Level Manager (`Assets/Scripts/Level/LevelManager.cs`)

- Load/unload levels dynamically in single scene
- Track current level index
- `TransitionToLevel(int index, Guid entranceId)` - carry inventory
- `ResetLevel()` - restore from initial state
- Save/load level-specific data to JSON

### 3.3 World/Level Scene Structure

- Single "World" scene with `LevelManager`
- Levels loaded as prefab instances or generated from `LevelData`
- Entrance/Exit portals trigger level transitions

### 3.4 Portal System (`Assets/Scripts/Gameplay/Portal.cs`)

- Reference to `EntranceExitLink` asset
- `bool isCheckpoint` - configurable per portal, triggers checkpoint save on entry
- On player enter:

  1. If `isCheckpoint`: Call `CheckpointManager.CaptureCheckpoint()`
  2. Call `LevelManager.TransitionToLevel()` with linked portal's level/entrance

---

## Phase 4: Level Editor Tool

### 4.1 Editor Window (`Assets/Editor/LevelEditorWindow.cs`)

- Unity EditorWindow: `Window > Breaking Hue > Level Editor`
- 32x32 grid visual with layer tabs
- Paint/erase tools for each element type
- **Layers**:

  1. Ground (floor tiles)
  2. Walls (solid blocks)
  3. Barriers (color-coded)
  4. Pickups (mask spawns)
  5. Barrels (color-coded)
  6. Bots (with initial color)
  7. Bot Paths (waypoint chains)
  8. Portals (entrance/exit pairs)
  9. Hidden Areas

### 4.2 Bot Path Editor

- Click to place waypoints
- Connect waypoints to form path
- Assign path to bot
- Generates `BotPathData` ScriptableObject

### 4.3 Portal Linking Tool

- Create `EntranceExitLink` assets
- Assign entrance/exit portals to links
- **Checkpoint toggle** per portal (configurable in editor)
- Visual connection lines in editor
- Visual indicator for checkpoint portals

### 4.4 Level Export

- Save as `LevelData` ScriptableObject
- Generate prefab variant for runtime instantiation
- Auto-generate on scene load option

---

## Phase 5: Checkpoint Save System

### 5.1 Checkpoint Architecture

- **Checkpoint-based saves** (not continuous)
- Each portal/entrance has `bool isCheckpoint` config in Level Editor
- On reaching a checkpoint portal: Capture full game state snapshot
- On player death (barrel explosion): Restore to last checkpoint snapshot exactly

### 5.2 Checkpoint Snapshot (`Assets/Scripts/Save/CheckpointSnapshot.cs`)

Captures ENTIRE game state at checkpoint moment:

- **Player state**: Position, inventory (all masks + active states)
- **Current level**: Level ID, entrance used
- **Level state for ALL visited levels**:
  - Collected mask positions (removed from level)
  - Destroyed barrel positions
  - Bot states (position, inventory, path progress, alive/dead)
  - Revealed hidden areas
  - Dropped mask positions

### 5.3 Checkpoint Manager (`Assets/Scripts/Save/CheckpointManager.cs`)

- `CheckpointSnapshot _lastCheckpoint` - in-memory snapshot
- `CaptureCheckpoint()` - called when player enters checkpoint portal
- `RestoreCheckpoint()` - called on player death, fully restores game state
- Serializes to JSON for persistent saves (quit/resume game)

### 5.4 Portal Checkpoint Config

- Portal ScriptableObject gains `bool isCheckpoint` field
- Level Editor shows checkpoint toggle per portal
- Visual indicator in-game for checkpoint portals (optional)

### 5.5 Death/Respawn Flow

1. Player enters barrel with matching mask
2. Barrel explodes, triggers `CheckpointManager.RestoreCheckpoint()`
3. Unload current level state
4. Restore snapshot: player position, inventory, all level states
5. Reload checkpoint level with restored state
6. Player resumes from checkpoint portal position

---

## Phase 6: UI and Camera

### 6.1 Camera System ([`Assets/Scripts/Camera/`](Assets/Scripts/Camera/))

- Fixed orthographic camera
- Calculate orthographic size to fit 32x32 grid
- Letterbox/pillarbox black bars for aspect ratio mismatch

### 6.2 HUD Updates ([`Assets/Scripts/UI/GameHUDController.cs`](Assets/Scripts/UI/GameHUDController.cs))

- Show active/inactive state per mask slot (toggle visual)
- Update slot colors for RYB system
- Scale UI with screen size (already using PanelSettings)

### 6.3 Input for New Actions

- Drop mask: New input action
- Toggle mask active: Modify existing equip to toggle
- Prevent dropping more than one mask on a single tile. if the player is inside a mask trigger, an additional dropped mask will pick up the mask that is already placed in his trigger.
- A mask will not be re-picked up until the player exits the trigger area and then re-enters it.

---

## Phase 7: Regenerate Setup Tools

### 7.1 Delete Old Generated Files

- Remove old tutorial level PNG
- Remove outdated prefabs using RGB colors
- Clear old documentation

### 7.2 New GameSetupWindow (`Assets/Editor/GameSetupWindow.cs`)

Complete rewrite of the editor tool to support all new systems:

#### 7.2.1 Materials Generation (RYB-based)

**Structural Materials:**

- `M_Wall.mat` - White, opaque, emission 0.5
- `M_Player.mat` - Light grey, opaque, emission 0.3
- `M_Floor.mat` - Dark grey (#252530), opaque, no emission
- `M_Hidden.mat` - Dark grey (#404040, NOT black), opaque

**Barrier Materials (Transparent for phasing effect):**

- `M_Barrier_Red.mat` - Red (#FF0000), transparent, emission 1.0
- `M_Barrier_Yellow.mat` - Yellow (#FFFF00), transparent, emission 1.0
- `M_Barrier_Blue.mat` - Blue (#0000FF), transparent, emission 1.0
- `M_Barrier_Orange.mat` - Orange (#FF8000), transparent, emission 1.0
- `M_Barrier_Green.mat` - Green (#00FF00), transparent, emission 1.0
- `M_Barrier_Purple.mat` - Purple (#800080), transparent, emission 1.0
- `M_Barrier_Black.mat` - Black (#101010), transparent, emission 0.5

**Pickup Materials (Opaque, high emission for visibility):**

- `M_Pickup_Red.mat`, `M_Pickup_Yellow.mat`, `M_Pickup_Blue.mat` - emission 1.5

**Barrel Materials (Opaque, distinct from barriers, hazard look):**

- `M_Barrel_Red.mat`, `M_Barrel_Yellow.mat`, `M_Barrel_Blue.mat` - emission 0.8, slightly darker base

**Entity Materials:**

- `M_Bot.mat` - Distinct color (cyan or metallic grey), emission 0.5
- `M_Portal.mat` - Bright green/blue swirl effect, emission 2.0
- `M_Portal_Checkpoint.mat` - Gold/amber variant for checkpoint portals, emission 2.5
- `M_DroppedMask.mat` - Same as pickups but slightly dimmer

#### 7.2.2 Prefab Generation

**Existing Prefabs (Updated):**

| Prefab | Components | Changes from Current |

|--------|------------|---------------------|

| Wall | BoxCollider, MeshRenderer | No changes |

| Player | PlayerController, Rigidbody, CapsuleCollider | Add drop mask action handling |

| ColorBarrier | ColorBarrier, dual colliders | Updated for residue system |

| MaskPickup | MaskPickup, trigger collider | No structural changes |

**New Prefabs:**

| Prefab | Components | Visual |

|--------|------------|--------|

| ExplodingBarrel | ExplodingBarrel, dual colliders (like barrier) | 0.8x0.8x0.8 cube with hazard material |

| Bot | BotController, BotInventory, Rigidbody, CapsuleCollider | Capsule similar to player, distinct color |

| Portal | Portal, BoxCollider (trigger), MeshRenderer | Flat vertical quad or ring mesh |

| HiddenBlock | HiddenBlock, BoxCollider (trigger) | 1x1x1 cube, dark grey material |

| DroppedMask | DroppedMask, SphereCollider (trigger) | Same as MaskPickup visual |

#### 7.2.3 Scene Generation

**MainMenu.unity** (Keep existing structure):

- Camera, UIDocument with MainMenu.uxml, MainMenuController
- Update to load "World" scene instead of "Game"

**World.unity** (Replaces Game.unity):

```
World Scene Hierarchy:
├── Main Camera (orthographic, sized for 32x32 grid)
├── Directional Light
├── SceneContext (Zenject)
│   └── WorldInstaller (new installer for world-level bindings)
├── LevelManager
├── CheckpointManager
├── HUD (UIDocument + GameHUDController)
└── LevelContainer (empty transform, levels instantiated here)
```

**Camera Configuration for 32x32 Grid:**

```csharp
camera.orthographic = true;
camera.orthographicSize = 16f; // Half the grid height
camera.transform.position = new Vector3(16, 20, 16); // Centered on 32x32
camera.transform.rotation = Quaternion.Euler(90, 0, 0);
```

#### 7.2.4 New Installer: WorldInstaller

Create `Assets/Scripts/Installers/WorldInstaller.cs`:

- Bind `MaskInventory` (singleton, persists across level transitions)
- Bind `LevelManager` (from hierarchy)
- Bind `CheckpointManager` (from hierarchy)
- Bind `GameHUDController` (from hierarchy)

#### 7.2.5 UI Assets

- Update `MainMenu.uxml` - Change "PLAY" to load "World" scene
- Update `HUD.uxml` - Add active/inactive toggle indicators per slot
- Keep `PanelSettings.asset` - Already configured for scaling

#### 7.2.6 Helper Methods to Add

```csharp
// Generate material for any RYB color
private void CreateColorMaterial(string name, ColorType colorType, bool transparent, float emission)
{
    Color color = ColorTypeExtensions.ToColor(colorType); // Uses updated RYB ToColor()
    CreateMaterial(name, color, emission, transparent);
}

// Generate all color variants for a prefab type
private void GenerateColorVariantMaterials(string prefix, bool transparent, float emission)
{
    foreach (ColorType color in new[] { ColorType.Red, ColorType.Yellow, ColorType.Blue,
                                         ColorType.Orange, ColorType.Green, ColorType.Purple,
                                         ColorType.Black })
    {
        CreateColorMaterial($"{prefix}_{color}", color, transparent, emission);
    }
}
```

#### 7.2.7 Editor Window UI Updates

Add new sections to the window:

- **Phase indicator**: Show which phase of setup is complete
- **Cleanup section**: Button to delete old RGB assets
- **Verification section**: Check that all required scripts exist before generating

#### 7.2.8 Build Settings

Update to include:

- MainMenu (index 0)
- World (index 1)

Remove old "Game" scene from build settings if it exists.

#### 7.2.9 Documentation Regeneration

Update `GameSystemGuide.md` to cover:

- New RYB color system and mixing rules
- Multi-mask toggle mechanics
- Residue system explanation
- Bot behavior documentation
- Barrel hazard mechanics
- Portal/checkpoint system
- Level Editor usage guide

---

## Phase 8: Example Level Set

### 8.1 Tutorial Levels (3-4 levels)

- Level 1: Basic movement, single color barrier
- Level 2: Multiple masks, barrier sequence
- Level 3: Bots introduction, simple path
- Level 4: Barrel mechanics, using bots to clear barrels

### 8.2 Advanced Level (1 level)

- All mechanics combined
- Multiple entrance/exits
- Hidden areas with secrets
- Black barrier requiring color combination
- Complex bot paths
- Residue puzzle (need specific leftover color)

---

## Key Files to Create/Modify

**New Files:**

- `Assets/Scripts/Gameplay/ExplodingBarrel.cs`
- `Assets/Scripts/Gameplay/Bot/BotController.cs`
- `Assets/Scripts/Gameplay/Bot/BotInventory.cs`
- `Assets/Scripts/Gameplay/Bot/BotPathData.cs`
- `Assets/Scripts/Gameplay/HiddenBlock.cs`
- `Assets/Scripts/Gameplay/Portal.cs`
- `Assets/Scripts/Gameplay/DroppedMask.cs`
- `Assets/Scripts/Level/LevelManager.cs`
- `Assets/Scripts/Level/Data/LevelData.cs`
- `Assets/Scripts/Level/Data/EntranceExitLink.cs`
- `Assets/Scripts/Save/CheckpointManager.cs`
- `Assets/Scripts/Save/CheckpointSnapshot.cs`
- `Assets/Editor/LevelEditorWindow.cs`

**Modified Files:**

- `Assets/Scripts/Core/ColorType.cs` - RYB system
- `Assets/Scripts/Core/MaskInventory.cs` - Multi-toggle, residue
- `Assets/Scripts/Gameplay/ColorBarrier.cs` - Residue consumption
- `Assets/Scripts/Gameplay/PlayerController.cs` - Drop mask action
- `Assets/Scripts/UI/GameHUDController.cs` - Active toggle visuals
- `Assets/Editor/GameSetupWindow.cs` - Complete regeneration

**Delete (handled by GameSetupWindow cleanup):**

- `Assets/Levels/Tutorial.png` - Old RGB-based level
- `Assets/Materials/M_Barrier.mat` - Old single barrier material (replaced by color variants)
- `Assets/Materials/M_Pickup.mat` - Old single pickup material (replaced by color variants)
- `Assets/Materials/M_Exit.mat` - Merged into Portal materials
- `Assets/Prefabs/ExitGoal.prefab` - Replaced by Portal system
- `Assets/Scenes/Game.unity` - Replaced by World.unity
- `Assets/Documentation/GameSystemGuide.md` - Regenerated with new content

**Keep (still valid):**

- `Assets/Materials/M_Wall.mat` - No changes needed
- `Assets/Materials/M_Player.mat` - No changes needed
- `Assets/Materials/M_Floor.mat` - No changes needed
- `Assets/Prefabs/Wall.prefab` - No changes needed
- `Assets/Prefabs/Player.prefab` - Will be updated in place
- `Assets/UI/PanelSettings.asset` - Still valid
- `Assets/UI/HUD.uxml` - Will be updated in place
- `Assets/UI/HUD.uss` - Will be updated in place
- `Assets/Scenes/MainMenu.unity` - Will be updated to load World scene