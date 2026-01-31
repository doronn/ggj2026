# Breaking Hue - Complete Game Mechanics & Code Guide

This document provides a comprehensive reference for understanding the game mechanics, code architecture, and dependencies for **Breaking Hue**.

---

## Table of Contents

1. [Game Overview](#game-overview)
2. [Color System](#color-system)
3. [Player Mechanics](#player-mechanics)
4. [Game Objects](#game-objects)
5. [Bot System](#bot-system)
6. [Level System](#level-system)
7. [Save/Checkpoint System](#savecheckpoint-system)
8. [Code Architecture](#code-architecture)
9. [Dependencies](#dependencies)
10. [File Structure](#file-structure)

---

## Game Overview

**Breaking Hue** is a 3D top-down color-based puzzle game where players collect and combine colored "masks" to pass through color-coded barriers. The game uses a **RYB (Red-Yellow-Blue)** subtractive color mixing system.

### Core Gameplay Loop

1. Collect colored mask pickups
2. Activate/combine masks to match barrier colors
3. Pass through barriers (consumes colors, leaves residue)
4. Manage limited inventory (3 slots)
5. Avoid hazards (exploding barrels)
6. Reach the exit goal

---

## Color System

### File: `Assets/Scripts/Core/ColorType.cs`

The game uses a **flags-based enum** for colors, enabling bitwise combination.

### Primary Colors

| Color | Flag Value | Binary |
|-------|------------|--------|
| None | 0 | 000 |
| Red | 1 | 001 |
| Yellow | 2 | 010 |
| Blue | 4 | 100 |

### Secondary Colors (Combinations)

| Color | Composition | Flag Value |
|-------|-------------|------------|
| Orange | Red + Yellow | 3 (1+2) |
| Green | Yellow + Blue | 6 (2+4) |
| Purple | Red + Blue | 5 (1+4) |
| Black | Red + Yellow + Blue | 7 (1+2+4) |

### Key Extension Methods

```csharp
// ColorTypeExtensions in ColorType.cs

// Convert Unity Color to ColorType
ColorType.FromColor(Color color)

// Convert ColorType to Unity Color for rendering
colorType.ToColor()

// Check if one color contains another
inventory.Contains(required)  // Returns true if all required flags are present

// Subtract colors (residue calculation)
source.Subtract(toRemove)  // Returns source & ~toRemove

// Add colors
source.Add(toAdd)  // Returns source | toAdd

// Check barrier passage
ColorTypeExtensions.CanPassThrough(combinedMaskColor, barrierColor)

// Utility methods
colorType.GetDisplayName()      // Human-readable name
colorType.GetPrimaryComponents() // Get only R/Y/B flags
colorType.CountPrimaries()       // Count of primary colors (0-3)
colorType.IsPrimary()           // Is it R, Y, or B only?
colorType.IsSecondary()         // Is it O, G, or P?
colorType.IsBlack()             // Contains all three primaries?
```

### Color Mixing Rules

- Colors combine via **bitwise OR**: `Red | Blue = Purple`
- Colors subtract via **bitwise AND NOT**: `Purple & ~Red = Blue`
- To pass a barrier: `(playerColor & barrierColor) == barrierColor`

---

## Player Mechanics

### File: `Assets/Scripts/Gameplay/PlayerController.cs`

Top-down 3D movement using Rigidbody physics with Unity's New Input System.

### Movement

- **Input**: WASD, Arrow Keys, or Gamepad Left Stick
- **Physics**: Rigidbody-based with configurable speed, acceleration, deceleration
- **Grid Alignment**: Mask dropping aligns to grid cells

### Controls

| Input | Action |
|-------|--------|
| WASD / Arrows / Left Stick | Movement |
| 1 / Numpad 1 | Toggle Slot 1 |
| 2 / Numpad 2 | Toggle Slot 2 |
| 3 / Numpad 3 | Toggle Slot 3 |
| 0 / Numpad 0 / ` | Deactivate all masks |
| Q / Gamepad West | Drop first active mask |

### Key Properties

```csharp
[SerializeField] private float moveSpeed = 5f;
[SerializeField] private float acceleration = 50f;
[SerializeField] private float deceleration = 40f;
[SerializeField] private float gridCellSize = 1f;
```

### Player Visual Feedback

- Player color updates to reflect combined active mask colors
- Grey color when no masks active
- Emissive materials supported (URP/HDRP compatible)

### Mask Dropping Restrictions

- Cannot drop while inside a barrier (`_currentBarrier != null`)
- Dropping on existing mask triggers swap
- Prevents immediate re-pickup of just-dropped mask

---

## Inventory System

### File: `Assets/Scripts/Core/MaskInventory.cs`

Slot-based inventory with multi-mask toggle support.

### Configuration

- **MaxSlots**: 3 (constant)
- Each slot holds one `ColorType`
- Each slot has independent active/inactive state

### Key Methods

```csharp
// Slot access
GetSlot(int index)              // Get mask in slot
IsSlotActive(int index)         // Check if slot is toggled on
IsSlotEmpty(int index)          // Check if slot is empty
FindEmptySlot()                 // First empty slot index (-1 if full)

// Mask management
TryAddMask(ColorType mask)      // Add to first empty slot
SetSlot(int index, ColorType)   // Set specific slot
ToggleMask(int index)           // Toggle active state
DeactivateAll()                 // Turn off all masks
DropMask(int index)             // Remove and return mask color
RemoveFromSlot(int index)       // Clear slot

// Color queries
GetCombinedActiveColor()        // Combined color of all active masks
GetActiveSlotIndices()          // List of active slot indices
CanPassThrough(ColorType)       // Can pass barrier with current actives?

// Barrier passage (with residue)
ApplyBarrierSubtraction(ColorType barrierColor)
ApplyBarrierSubtractionFromSlots(ColorType, List<int> slotIndices)
CalculateOptimalSubtraction(ColorType required)

// Checkpointing
CreateSnapshot()                // Save state
RestoreFromSnapshot(InventorySnapshot)
```

### Events

```csharp
event Action OnInventoryChanged;           // Any slot change
event Action<int, bool> OnMaskToggled;     // Slot toggle (index, isActive)
event Action<ColorType> OnMaskConsumed;    // After barrier subtraction
event Action<int, ColorType> OnMaskDropped; // Mask dropped (index, color)
```

### Residue System

The `CalculateOptimalSubtraction` method intelligently determines which slots to subtract from:

1. For each required primary color in the barrier
2. Find the best slot that has it (preferring slots already being used)
3. Minimize "waste" by choosing masks with fewer extra primaries
4. Return a mapping of slot → colors to remove

---

## Game Objects

### Color Barriers

**File**: `Assets/Scripts/Gameplay/ColorBarrier.cs`

Colored walls that allow passage with matching masks.

#### Dual Collider System

- **Solid Collider**: Blocks entities without matching color
- **Trigger Collider**: Detects approaching entities (slightly larger)

#### Phasing Flow

1. Entity approaches with correct combined color
2. `OnTriggerEnter`: Solid collider disables, entity enters
3. Entity walks through (visual becomes semi-transparent)
4. `OnTriggerExit`: Solid collider re-enables, colors subtracted

#### Key Properties

```csharp
[SerializeField] private ColorType requiredColor;
[SerializeField] private float phasingAlpha = 0.3f;
[SerializeField] private float normalAlpha = 1f;
```

#### IColorInventory Interface

Barriers work with any entity implementing:

```csharp
public interface IColorInventory
{
    ColorType GetCombinedActiveColor();
    bool CanPassThrough(ColorType barrierColor);
    void ApplyBarrierSubtraction(ColorType barrierColor);
}
```

---

### Mask Pickups

**File**: `Assets/Scripts/Gameplay/MaskPickup.cs`

Collectible items that add masks to inventory.

#### Behavior

- Float and rotate animation
- Trigger-based collection on player contact
- Fails silently if inventory full
- Destroys self after collection (configurable)

#### Key Properties

```csharp
[SerializeField] private ColorType colorToGive;
[SerializeField] private bool destroyOnPickup = true;
[SerializeField] private string pickupId;  // For save/load
```

#### Events

```csharp
public static event Action<string> OnMaskPickupCollected;  // pickupId
```

---

### Dropped Masks

**File**: `Assets/Scripts/Gameplay/DroppedMask.cs`

Masks placed on the ground by players or bots.

#### Behavior

- Grid-aligned position
- Bob and rotate animation
- Pickup on trigger enter (player only)
- Prevents immediate re-pickup after dropping

#### Key Methods

```csharp
Initialize(ColorType color)
Initialize(Vector3 position, ColorType color)
Collect()
PreventImmediatePickup()

// Static factory
static DroppedMask Spawn(GameObject prefab, Vector3 position, ColorType color)
static DroppedMask Spawn(GameObject prefab, Vector3 pos, ColorType color, string savedMaskId)
```

#### Events

```csharp
public static event Action<Vector3, ColorType> SpawnDroppedMask;
public static event Action<DroppedMask> OnAnyMaskCollected;
public static event Action<DroppedMask> OnMaskSpawned;
```

---

### Exploding Barrels

**File**: `Assets/Scripts/Gameplay/ExplodingBarrel.cs`

Hazardous objects that explode on color-matched contact.

#### Dual Collider System (same as barriers)

- Solid collider blocks entities WITHOUT matching color
- Trigger detects entities WITH matching color

#### Behavior by Entity Type

**Player Contact**:
- If player has matching color active → Explosion → Player death → Checkpoint restore
- If player lacks color → Blocked (solid wall)

**Bot Contact**:
- If bot has matching color → Explosion → Bot destroyed → Drops all masks
- If bot lacks color → Blocked

#### Events

```csharp
public static event Action<ExplodingBarrel, bool> OnBarrelExploded;  // (barrel, isPlayer)
public static event Action OnPlayerExploded;  // For CheckpointManager
```

---

### Portals

**File**: `Assets/Scripts/Gameplay/Portal.cs`

Bidirectional teleportation points.

#### Types

| Type | Color | Behavior |
|------|-------|----------|
| Regular | Cyan | Transition only |
| Checkpoint | Gold | Save state, then transition |

#### Configuration

```csharp
[SerializeField] private string portalId;
[SerializeField] private EntranceExitLink link;  // Links two portals
[SerializeField] private bool isCheckpoint;
```

#### Events

```csharp
public static event Action<Portal> OnCheckpointReached;
public static event Action<Portal> OnPortalEntered;
```

#### EntranceExitLink Asset

**File**: `Assets/Scripts/Level/Data/EntranceExitLink.cs`

ScriptableObject that defines portal connections between levels.

---

### Hidden Blocks

**File**: `Assets/Scripts/Gameplay/HiddenBlock.cs`

Concealed areas that reveal when touched.

#### Behavior

- Dark gray appearance (distinguishable from Black barriers)
- NO solid collider (doesn't block movement)
- Trigger collider only
- Fades out on contact, reveals contents underneath
- State persists for checkpointing

#### Key Methods

```csharp
Reveal()                    // Trigger reveal
Restore()                   // Reset to hidden state
SetRevealed(bool revealed)  // Direct state set (loading)
```

---

### Exit Goal

**File**: `Assets/Scripts/Gameplay/ExitGoal.cs`

Level completion trigger.

#### Behavior

- Pulsing glow effect
- Fires event on player contact

```csharp
public static event Action OnPlayerReachedExit;
```

---

## Bot System

### Bot Controller

**File**: `Assets/Scripts/Gameplay/Bot/BotController.cs`

AI entities that follow predefined paths with same color mechanics as player.

#### Movement

- Waypoint-based pathfinding
- Same physics as player
- Configurable speed multiplier

#### Bot Rules

1. Follows `BotPathData` waypoints
2. Collects masks it doesn't already own
3. Drops residue for colors it already has
4. Passes through barriers (with residue subtraction)
5. **Stops permanently on player collision** (becomes kinematic)
6. Explodes in barrels (drops all masks)
7. Can regenerate initial color via matching barriers

### Bot Inventory

**File**: `Assets/Scripts/Gameplay/Bot/BotInventory.cs`

Implements `IColorInventory` interface.

#### Key Differences from Player Inventory

- Has an "initial color" that can regenerate
- `AlreadyHasAllColors(ColorType)` - Check before pickup
- `TryPickupMask(ColorType)` - Returns residue color
- `DropAllMasks()` - For explosion handling

### Bot Path Data

**File**: `Assets/Scripts/Gameplay/Bot/BotPathData.cs`

ScriptableObject defining bot movement paths.

#### Path Modes

```csharp
public enum PathMode
{
    OneWay,    // Stop at end
    Loop,      // Return to start
    PingPong   // Reverse direction
}
```

---

## Level System

### Level Data

**File**: `Assets/Scripts/Level/Data/LevelData.cs`

ScriptableObject containing all level data.

#### Grid System

- **GridSize**: 32x32 (constant)
- **CellSize**: Configurable (default 1.0)

#### Layer System

Each level consists of multiple layers:

| Layer | Class | Contents |
|-------|-------|----------|
| Ground | `GroundLayer` | Walkable floor tiles |
| Wall | `WallLayer` | Impassable walls |
| Barrier | `BarrierLayer` | Color barriers (BarrierData) |
| Pickup | `PickupLayer` | Mask pickups (PickupData) |
| Barrel | `BarrelLayer` | Exploding barrels (BarrelData) |
| Bot | `BotLayer` | Bot entities (BotData) |
| Portal | `PortalLayer` | Portals + player spawn |
| Hidden | `HiddenAreaLayer` | Hidden blocks |

#### Data Structures

```csharp
// Barrier placement
public class BarrierData {
    public Vector2Int position;
    public ColorType color;
}

// Pickup placement
public class PickupData {
    public Vector2Int position;
    public ColorType color;
    public string pickupId;
}

// Bot configuration
public class BotData {
    public string botId;
    public Vector2Int startPosition;
    public ColorType initialColor;
    public BotPathData pathData;
    public PathMode pathMode;
}

// Portal configuration
public class PortalData {
    public string portalId;
    public Vector2Int position;
    public EntranceExitLink link;
    public bool isCheckpoint;
}
```

### Level Manager

**File**: `Assets/Scripts/Level/LevelManager.cs`

Handles level loading, generation, and transitions.

#### Key Methods

```csharp
LoadLevel(LevelData levelData)
TransitionToLevel(EntranceExitLink link, string sourcePortalId)
SpawnPlayer(Vector3 position)
```

### Level Generator

**File**: `Assets/Scripts/Level/LevelGenerator.cs`

Spawns GameObjects from LevelData.

---

## Save/Checkpoint System

### Checkpoint Manager

**File**: `Assets/Scripts/Save/CheckpointManager.cs`

Handles checkpoint saving and restoration.

#### Saved State Includes

- Player position
- Inventory state (`InventorySnapshot`)
- Collected pickups (by ID)
- Revealed hidden blocks (by ID)
- Destroyed barrels (by ID)
- Bot states (`BotSnapshot`)
- Dropped mask positions

#### Key Methods

```csharp
SaveCheckpoint(Vector3 position, string levelId, string portalId)
RestoreCheckpoint()
HasCheckpoint()
```

### Snapshot Classes

```csharp
// Inventory state
public class InventorySnapshot {
    public ColorType[] Slots;
    public bool[] ActiveSlots;
}

// Bot state
public class BotSnapshot {
    public string BotId;
    public Vector3 Position;
    public BotPathState PathState;
    public BotInventorySnapshot InventorySnapshot;
    public bool IsDead;
}

// Dropped mask state
public class DroppedMaskSnapshot {
    public string MaskId;
    public Vector3 Position;
    public ColorType MaskColor;
}
```

---

## Code Architecture

### Dependency Injection (Zenject)

**File**: `Assets/Scripts/Installers/GameInstaller.cs`

The game uses Zenject for dependency injection.

#### Bindings

```csharp
Container.Bind<MaskInventory>().AsSingle();
Container.Bind<LevelManager>().FromComponentInHierarchy().AsSingle();
Container.Bind<CheckpointManager>().FromComponentInHierarchy().AsSingle();
// ... other bindings
```

#### Injection Pattern

Components receive dependencies via `[Inject]` attribute:

```csharp
[Inject]
public void Construct(MaskInventory inventory)
{
    _inventory = inventory;
}
```

#### Fallback Resolution

Many components include fallback for non-injected instantiation:

```csharp
private void Start()
{
    if (_inventory == null)
    {
        var sceneContext = FindObjectOfType<Zenject.SceneContext>();
        if (sceneContext != null && sceneContext.Container != null)
        {
            _inventory = sceneContext.Container.TryResolve<MaskInventory>();
        }
    }
}
```

### Event-Driven Communication

The game uses static events for loose coupling:

```csharp
// PlayerController
public static event Action<Vector3, ColorType> OnMaskDropRequested;

// MaskPickup
public static event Action<string> OnMaskPickupCollected;

// ExplodingBarrel
public static event Action OnPlayerExploded;

// Portal
public static event Action<Portal> OnCheckpointReached;

// ExitGoal
public static event Action OnPlayerReachedExit;

// HiddenBlock
public static event Action<HiddenBlock> OnBlockRevealed;

// DroppedMask
public static event Action<DroppedMask> OnAnyMaskCollected;
public static event Action<DroppedMask> OnMaskSpawned;
```

### Assembly Definitions

The project uses assembly definitions for modular compilation:

```
Assets/Scripts/Core/          → BreakingHue.Core.asmdef
Assets/Scripts/Gameplay/      → BreakingHue.Gameplay.asmdef
Assets/Scripts/Level/         → BreakingHue.Level.asmdef
Assets/Scripts/UI/            → BreakingHue.UI.asmdef
Assets/Scripts/Installers/    → BreakingHue.Installers.asmdef
Assets/Scripts/Save/          → BreakingHue.Save.asmdef
Assets/Scripts/Camera/        → BreakingHue.Camera.asmdef
```

---

## Dependencies

### Unity Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Unity Input System | Built-in | New input handling |
| Universal RP | Built-in | Rendering pipeline |
| UI Toolkit | Built-in | HUD system |

### External Packages (via manifest.json)

| Package | ID | Purpose |
|---------|----|---------|
| UniTask | com.cysharp.unitask | Async/await support |
| Extenject (Zenject) | com.svermeulen.extenject | Dependency injection |

### Required Tags

Configure in Edit > Project Settings > Tags and Layers:

- `Player` - Required for player detection

### Required Layers (Recommended)

- `Player`
- `Bot`
- `Barrier`
- `Pickup`

---

## File Structure

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── ColorType.cs          # Color enum + extensions
│   │   ├── MaskInventory.cs      # Player inventory system
│   │   ├── GameConfig.cs         # Game configuration
│   │   └── GameManager.cs        # Game state management
│   │
│   ├── Gameplay/
│   │   ├── PlayerController.cs   # Player movement + input
│   │   ├── ColorBarrier.cs       # Color barriers
│   │   ├── MaskPickup.cs         # Collectible masks
│   │   ├── DroppedMask.cs        # Dropped mask objects
│   │   ├── ExplodingBarrel.cs    # Hazard barrels
│   │   ├── Portal.cs             # Teleport portals
│   │   ├── HiddenBlock.cs        # Revealable secrets
│   │   ├── ExitGoal.cs           # Level completion
│   │   └── Bot/
│   │       ├── BotController.cs  # Bot AI + movement
│   │       ├── BotInventory.cs   # Bot color inventory
│   │       └── BotPathData.cs    # Bot path definitions
│   │
│   ├── Level/
│   │   ├── LevelManager.cs       # Level loading/transitions
│   │   ├── LevelGenerator.cs     # GameObject spawning
│   │   └── Data/
│   │       ├── LevelData.cs      # Level ScriptableObject
│   │       ├── LevelSaveData.cs  # Save data structures
│   │       └── EntranceExitLink.cs # Portal connections
│   │
│   ├── Save/
│   │   └── CheckpointManager.cs  # Checkpoint system
│   │
│   ├── UI/
│   │   ├── GameHUDController.cs  # HUD management
│   │   └── MainMenuController.cs # Main menu
│   │
│   ├── Camera/
│   │   └── GameCamera.cs         # Camera controls
│   │
│   └── Installers/
│       └── GameInstaller.cs      # Zenject bindings
│
├── UI/
│   ├── HUD.uxml                  # UI Toolkit layout
│   └── HUD.uss                   # UI Toolkit styles
│
├── Materials/
│   ├── M_Barrier_*.mat           # Barrier materials
│   ├── M_Pickup_*.mat            # Pickup materials
│   └── ...
│
├── Levels/
│   ├── Level01_Tutorial.asset   # Level data assets
│   ├── Level02_Advanced.asset
│   └── ...
│
└── Prefabs/
    ├── Player.prefab
    ├── Barrier.prefab
    ├── Pickup.prefab
    ├── DroppedMask.prefab
    ├── ExplodingBarrel.prefab
    ├── Portal.prefab
    ├── HiddenBlock.prefab
    ├── Bot.prefab
    └── ...
```

---

## Gameplay Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         GAME START                               │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  LevelManager.LoadLevel()                                        │
│  ├── Instantiate floor/walls                                     │
│  ├── Spawn barriers, pickups, barrels                           │
│  ├── Spawn bots with paths                                       │
│  ├── Spawn portals                                               │
│  └── Spawn player at spawn position                              │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      GAMEPLAY LOOP                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Player Movement (WASD)                                          │
│       │                                                          │
│       ├──▶ Pickup Contact ──▶ TryAddMask() ──▶ Inventory Update │
│       │                                                          │
│       ├──▶ Barrier Contact                                       │
│       │       │                                                  │
│       │       ├── Has Color? ──▶ Phase Through ──▶ Subtract     │
│       │       └── No Color? ──▶ Blocked                         │
│       │                                                          │
│       ├──▶ Barrel Contact                                        │
│       │       │                                                  │
│       │       ├── Has Color? ──▶ EXPLOSION ──▶ Checkpoint       │
│       │       └── No Color? ──▶ Blocked                         │
│       │                                                          │
│       ├──▶ Portal Contact ──▶ Transition/Checkpoint             │
│       │                                                          │
│       ├──▶ Hidden Block ──▶ Reveal Contents                     │
│       │                                                          │
│       └──▶ Exit Goal ──▶ Level Complete                         │
│                                                                  │
│  Mask Toggle (1/2/3)                                             │
│       └──▶ ToggleMask() ──▶ Update Combined Color               │
│                                                                  │
│  Mask Drop (Q)                                                   │
│       └──▶ DropMask() ──▶ Spawn DroppedMask                     │
│                                                                  │
│  Bot AI Loop                                                     │
│       ├──▶ Follow Path Waypoints                                │
│       ├──▶ Pickup Masks (if new colors)                         │
│       ├──▶ Phase Through Barriers                               │
│       └──▶ Stop on Player Collision                             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      LEVEL COMPLETE                              │
│  ├── Save progress                                               │
│  └── Load next level or show victory                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Quick Reference: Color Combinations

| Active Masks | Combined Color | Can Pass Through |
|--------------|----------------|------------------|
| Red | Red | Red barriers |
| Yellow | Yellow | Yellow barriers |
| Blue | Blue | Blue barriers |
| Red + Yellow | Orange | Red, Yellow, Orange barriers |
| Yellow + Blue | Green | Yellow, Blue, Green barriers |
| Red + Blue | Purple | Red, Blue, Purple barriers |
| Red + Yellow + Blue | Black | ALL barriers |

---

## Quick Reference: Residue Examples

| Mask(s) Used | Barrier | Residue Left |
|--------------|---------|--------------|
| Red | Red | None (empty) |
| Orange (R+Y) | Red | Yellow |
| Purple (R+B) | Blue | Red |
| Black (R+Y+B) | Orange (R+Y) | Blue |
| Green (Y+B) | Green | None (empty) |
| Purple + Yellow (separate) | Black | None (all consumed) |

---

## Troubleshooting

### Common Issues

1. **Player not picking up masks**: Check `Player` tag is set
2. **Barriers not working**: Ensure dual colliders (solid + trigger)
3. **Injection failing**: Check SceneContext has GameInstaller
4. **Colors not combining**: Verify masks are ACTIVE (toggled on)
5. **Bots not moving**: Check BotPathData has waypoints

### Debug Logging

Most components log to console with prefixes:
- `[PlayerController]`
- `[ColorBarrier]`
- `[MaskPickup]`
- `[BotController]`
- `[ExplodingBarrel]`
- `[Portal]`

---

*Last updated: January 2026*
