# Breaking Hue - Asset Management Documentation

This document covers the organization, conventions, and workflows for managing game assets in Breaking Hue.

## Table of Contents
- [Overview](#overview)
- [Folder Structure](#folder-structure)
- [GameConfig System](#gameconfig-system)
- [Materials](#materials)
- [Prefabs](#prefabs)
- [Level Data](#level-data)
- [Adding New Entity Types](#adding-new-entity-types)
- [Best Practices](#best-practices)

---

## Overview

Breaking Hue uses a centralized configuration approach where all game assets are referenced through `GameConfig`, a ScriptableObject that acts as the single source of truth for prefabs, levels, and portal connections.

### Key Principles

1. **Centralized References** - GameConfig holds all prefab/level references
2. **Convention Over Configuration** - Consistent naming enables auto-discovery
3. **ScriptableObject Data** - Levels and paths stored as assets
4. **Material Mapping** - Colors map to specific materials at runtime

---

## Folder Structure

```
Assets/
├── Config/
│   └── GameConfig.asset          # Central configuration
│
├── Editor/
│   ├── GameSetupWindow.cs        # Asset generation
│   ├── AutoConnectWindow.cs      # Asset connection
│   └── LevelEditorWindow.cs      # Level editing
│
├── Input/
│   └── GameInputActions.inputactions  # Input definitions
│
├── Levels/
│   ├── Level01_Tutorial.asset    # Level data assets
│   ├── Level02_Advanced.asset
│   └── Links/
│       └── Portal_*.asset        # Portal connections
│
├── Materials/
│   ├── M_Barrier_*.mat           # Barrier materials (7 colors)
│   ├── M_Pickup_*.mat            # Pickup materials (7 colors)
│   ├── M_Barrel_*.mat            # Barrel materials (3 colors)
│   ├── M_Player.mat
│   ├── M_Bot.mat
│   ├── M_Portal.mat
│   ├── M_Portal_Checkpoint.mat
│   ├── M_Wall.mat
│   ├── M_Floor.mat
│   └── M_Hidden.mat
│
├── Prefabs/
│   ├── Player.prefab
│   ├── Bot.prefab
│   ├── ColorBarrier.prefab
│   ├── MaskPickup.prefab
│   ├── DroppedMask.prefab
│   ├── ExplodingBarrel.prefab
│   ├── Portal.prefab
│   ├── HiddenBlock.prefab
│   ├── Floor.prefab
│   └── Wall.prefab
│
├── Scenes/
│   ├── MainMenu.unity
│   └── World.unity
│
├── Scripts/
│   └── [organized by assembly]
│
└── UI/
    ├── *.uxml                    # UI layouts
    ├── *.uss                     # UI styles
    └── PanelSettings.asset
```

---

## GameConfig System

**File:** `Assets/Scripts/Core/GameConfig.cs`

**Asset:** `Assets/Config/GameConfig.asset`

### Purpose

GameConfig is a ScriptableObject that centralizes all game asset references, eliminating the need to manually connect prefabs to individual levels or managers.

### Structure

```csharp
[CreateAssetMenu(fileName = "GameConfig", menuName = "Breaking Hue/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Prefabs")]
    public LevelPrefabs prefabs;
    
    [Header("Levels")]
    public LevelData startingLevel;
    public List<LevelData> allLevels;
    
    [Header("Portal Links")]
    public List<EntranceExitLink> portalLinks;
}
```

### LevelPrefabs Struct

```csharp
[System.Serializable]
public struct LevelPrefabs
{
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject barrierPrefab;
    public GameObject pickupPrefab;
    public GameObject barrelPrefab;
    public GameObject botPrefab;
    public GameObject portalPrefab;
    public GameObject hiddenBlockPrefab;
    public GameObject droppedMaskPrefab;
    public GameObject playerPrefab;
}
```

### Key Methods

```csharp
// Find portal link by portal ID
EntranceExitLink FindLinkForPortal(string portalId)

// Find level by ID
LevelData FindLevelById(string levelId)

// Validate all prefabs assigned
bool ValidatePrefabs(out List<string> missingPrefabs)

// Validate entire configuration
bool Validate(out List<string> issues)
```

### Usage in Code

```csharp
// LevelGenerator uses GameConfig for spawning
[SerializeField] private GameConfig gameConfig;

private void SpawnBarrier(Vector3 position, ColorType color)
{
    var barrier = Instantiate(gameConfig.prefabs.barrierPrefab, position, Quaternion.identity);
    barrier.GetComponent<ColorBarrier>().SetColor(color);
}
```

### Auto-Connection

Use **Window > Breaking Hue > Auto Connect** to automatically populate GameConfig with discovered assets.

---

## Materials

### Naming Convention

```
M_[Type]_[Color].mat
```

| Type | Purpose | Colors Available |
|------|---------|------------------|
| Barrier | Color barriers | Red, Yellow, Blue, Orange, Green, Purple, Black |
| Pickup | Mask pickups | Red, Yellow, Blue, Orange, Green, Purple, Black |
| Barrel | Exploding barrels | Red, Yellow, Blue |
| Player | Player character | N/A (single) |
| Bot | Bot entities | N/A (single) |
| Portal | Regular portals | N/A (single) |
| Portal_Checkpoint | Checkpoint portals | N/A (single) |
| Wall | Wall blocks | N/A (single) |
| Floor | Floor tiles | N/A (single) |
| Hidden | Hidden blocks | N/A (single) |

### Material Properties

All materials use **Universal Render Pipeline/Lit** shader.

#### Barrier Materials (Transparent)
```
Surface Type: Transparent
Blending Mode: Alpha
Base Color: [RYB color], Alpha 0.8
Emission: [color] × 0.5
Render Queue: 3000
```

#### Pickup Materials (Opaque, Emissive)
```
Surface Type: Opaque
Base Color: [RYB color]
Emission: [color] × 0.8
```

#### Barrel Materials (Opaque, Dimmed)
```
Surface Type: Opaque
Base Color: [RYB color] × 0.8
Emission: [color] × 0.8
```

### Color to Material Mapping

At runtime, components look up materials by color:

```csharp
// ColorBarrier.cs
public void SetColor(ColorType color)
{
    _requiredColor = color;
    var renderer = GetComponent<Renderer>();
    renderer.material = Resources.Load<Material>($"Materials/M_Barrier_{color}");
}
```

**Note:** The current implementation uses direct material assignment on prefabs. For dynamic color changes, implement a material lookup system.

### Creating New Materials

1. Right-click in Materials folder > Create > Material
2. Name following convention: `M_[Type]_[Color]`
3. Set shader to URP/Lit
4. Configure base color matching RYB model
5. Enable emission if needed
6. For transparency, set Surface Type to Transparent

---

## Prefabs

### Prefab Structure

Each prefab contains specific components for its gameplay role:

#### Player.prefab
```
Player (GameObject)
├── Components:
│   ├── PlayerController
│   ├── Rigidbody (UseGravity, !IsKinematic)
│   ├── CapsuleCollider
│   └── [Visual child with Renderer]
├── Tag: "Player"
└── Material: M_Player
```

#### Bot.prefab
```
Bot (GameObject)
├── Components:
│   ├── BotController
│   ├── BotInventory
│   ├── Rigidbody
│   └── CapsuleCollider
└── Material: M_Bot
```

#### ColorBarrier.prefab
```
ColorBarrier (GameObject)
├── Components:
│   ├── ColorBarrier (script)
│   ├── BoxCollider (Solid - IsTrigger: false)
│   └── BoxCollider (Trigger - IsTrigger: true, slightly larger)
└── Material: M_Barrier_Red (default, changed at runtime)
```

#### MaskPickup.prefab
```
MaskPickup (GameObject)
├── Components:
│   ├── MaskPickup (script)
│   └── BoxCollider (IsTrigger: true)
├── Scale: (0.5, 0.5, 0.5)
└── Material: M_Pickup_Red (default)
```

#### DroppedMask.prefab
```
DroppedMask (GameObject)
├── Components:
│   ├── DroppedMask (script)
│   └── SphereCollider (IsTrigger: true)
├── Scale: (0.4, 0.4, 0.4)
└── Material: Dynamic (set on spawn)
```

#### ExplodingBarrel.prefab
```
ExplodingBarrel (GameObject)
├── Components:
│   ├── ExplodingBarrel (script)
│   ├── BoxCollider (Solid)
│   ├── BoxCollider (Trigger)
│   └── AudioSource (for explosion SFX)
├── Scale: (0.8, 0.8, 0.8)
└── Material: M_Barrel_Red (default)
```

#### Portal.prefab
```
Portal (GameObject)
├── Components:
│   ├── Portal (script)
│   └── BoxCollider (IsTrigger: true)
├── Rotation: Facing direction
├── Scale: (1, 2, 0.1) - Quad shape
└── Material: M_Portal or M_Portal_Checkpoint
```

#### HiddenBlock.prefab
```
HiddenBlock (GameObject)
├── Components:
│   ├── HiddenBlock (script)
│   ├── BoxCollider (IsTrigger: true)
│   └── AudioSource (for reveal SFX)
└── Material: M_Hidden
```

### Prefab Variants

For color variations, use the same prefab with runtime color assignment rather than creating separate prefabs per color.

---

## Level Data

### LevelData ScriptableObject

**File:** `Assets/Scripts/Level/Data/LevelData.cs`

```csharp
[CreateAssetMenu(fileName = "NewLevel", menuName = "Breaking Hue/Level Data")]
public class LevelData : ScriptableObject
{
    public string levelId;
    public string levelName;
    
    public const int GridSize = 32;
    public float cellSize = 1f;
    
    public List<Vector2Int> groundPositions;
    public List<Vector2Int> wallPositions;
    public List<BarrierData> barriers;
    public List<PickupData> pickups;
    public List<BarrelData> barrels;
    public List<BotData> bots;
    public List<PortalData> portals;
    public List<Vector2Int> hiddenBlockPositions;
    public Vector2Int playerSpawnPosition;
}
```

### Creating Levels

#### Method 1: Level Editor (Recommended)
1. Open **Window > Breaking Hue > Level Editor**
2. Create new level or load existing
3. Use layer tools to paint elements
4. Save to create/update LevelData asset

#### Method 2: Manual Creation
1. Right-click in Levels folder > Create > Breaking Hue > Level Data
2. Set levelId and levelName
3. Populate position/data lists in Inspector
4. Tedious but possible for simple levels

#### Method 3: Programmatic Generation
See `ExampleLevelGenerator.cs` for generating levels via code.

### EntranceExitLink

Portal connections are stored as separate assets:

```csharp
[CreateAssetMenu(fileName = "PortalLink", menuName = "Breaking Hue/Portal Link")]
public class EntranceExitLink : ScriptableObject
{
    public string linkId;
    public PortalEndpoint portalA;
    public PortalEndpoint portalB;
    
    public bool ConnectsPortal(string portalId);
    public PortalEndpoint GetDestination(string sourcePortalId);
}
```

Create links via Level Editor or manually in `Assets/Levels/Links/`.

---

## Adding New Entity Types

### Step-by-Step Guide

1. **Create Script**
   ```csharp
   // Assets/Scripts/Gameplay/NewEntity.cs
   public class NewEntity : MonoBehaviour
   {
       [SerializeField] private ColorType color;
       // ... implementation
   }
   ```

2. **Create Prefab**
   - Create GameObject with required components
   - Add your script
   - Configure colliders (solid, trigger, or both)
   - Save as prefab in `Assets/Prefabs/`

3. **Create Material(s)**
   - If color-based, create `M_NewEntity_[Color].mat` for each color
   - If single appearance, create `M_NewEntity.mat`

4. **Add to LevelPrefabs** (if spawned by LevelGenerator)
   ```csharp
   // In GameConfig.cs LevelPrefabs struct
   public GameObject newEntityPrefab;
   ```

5. **Add to LevelData** (if placed in levels)
   ```csharp
   // In LevelData.cs
   public List<NewEntityData> newEntities;
   
   [System.Serializable]
   public class NewEntityData
   {
       public string entityId;
       public Vector2Int position;
       public ColorType color;
       // ... other properties
   }
   ```

6. **Add to Level Editor** (optional but recommended)
   - Add layer enum value
   - Add snapshot class
   - Implement rendering/editing
   - See `Docs/LevelEditorGuide.md` for details

7. **Add to LevelGenerator**
   ```csharp
   // In LevelGenerator.cs
   private void SpawnNewEntities()
   {
       foreach (var data in _levelData.newEntities)
       {
           var entity = Instantiate(_gameConfig.prefabs.newEntityPrefab);
           entity.transform.position = GridToWorld(data.position);
           entity.GetComponent<NewEntity>().Initialize(data);
       }
   }
   ```

8. **Update Auto Connect**
   - Add prefab detection in `AutoConnectWindow.cs`
   - Add validation for new prefab field

9. **Add to Checkpoint System** (if state needs saving)
   ```csharp
   // In CheckpointManager.cs
   private List<NewEntitySnapshot> _newEntityStates;
   ```

---

## Best Practices

### Naming Conventions

| Asset Type | Convention | Example |
|------------|------------|---------|
| Materials | `M_[Type]_[Color]` | `M_Barrier_Red` |
| Prefabs | `[EntityName]` | `ColorBarrier` |
| Levels | `Level[##]_[Name]` | `Level01_Tutorial` |
| Portal Links | `Portal_[A]_to_[B]` | `Portal_L1_to_L2` |
| Scripts | `[EntityName].cs` | `ColorBarrier.cs` |
| UI Assets | `[ScreenName].[ext]` | `PauseMenu.uxml` |

### Organization Rules

1. **One prefab per entity type** - Use runtime configuration, not prefab variants
2. **Color variations via materials** - Don't duplicate prefabs for colors
3. **Levels in dedicated folder** - Keep `Assets/Levels/` clean
4. **Links as sub-folder** - Portal links in `Assets/Levels/Links/`
5. **GameConfig is truth** - All runtime lookups go through GameConfig

### Version Control

- Commit `.meta` files alongside assets
- Use text serialization for prefabs/scenes (Edit > Project Settings > Editor > Asset Serialization: Force Text)
- Avoid large binary assets in main repo

### Performance

- Use prefab pooling for frequently spawned objects (DroppedMask)
- Share materials across instances (don't instantiate materials)
- Keep level data lightweight (positions, not full transforms)

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Assets/Scripts/Core/GameConfig.cs` | Configuration ScriptableObject |
| `Assets/Config/GameConfig.asset` | Configuration instance |
| `Assets/Scripts/Level/Data/LevelData.cs` | Level data structure |
| `Assets/Scripts/Level/LevelGenerator.cs` | Level spawning |
| `Assets/Editor/AutoConnectWindow.cs` | Asset connection tool |

---

*Last updated: January 2026*
