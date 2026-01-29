# Breaking Hue - Game System Guide

## Quick Start

### One-Click Setup
1. Open Unity
2. Go to **Window > Breaking Hue > Game Setup**
3. Click **Run Full Setup**
4. Open the **MainMenu** scene
5. Press **Play** and click the **PLAY** button

### Controls
- **WASD / Arrow Keys**: Move player
- **1, 2, 3**: Equip mask from inventory slot
- **0 / Backquote**: Unequip current mask

---

## Creating New Levels

### Pixel Color Reference

| Color | RGB Value | Alpha | Object Spawned |
|-------|-----------|-------|----------------|
| **Black** | (0, 0, 0) | 1.0 | Exit Goal |
| **White** | (1, 1, 1) | 1.0 | Wall |
| **Grey** | (0.5, 0.5, 0.5) | 1.0 | Player Spawn |
| **Red** | (1, 0, 0) | 1.0 | Red Barrier |
| **Green** | (0, 1, 0) | 1.0 | Green Barrier |
| **Blue** | (0, 0, 1) | 1.0 | Blue Barrier |
| **Red** | (1, 0, 0) | 0.5 | Red Mask Pickup |
| **Green** | (0, 1, 0) | 0.5 | Green Mask Pickup |
| **Blue** | (0, 0, 1) | 0.5 | Blue Mask Pickup |
| **Transparent** | Any | < 0.4 | Empty Space |

### Step-by-Step Level Creation

1. **Create Image**: Use any image editor (Photoshop, GIMP, Aseprite)
2. **Set Size**: 16x16 pixels recommended (can be larger)
3. **Draw Level**: Use colors from the reference table above
4. **Save as PNG**: Save with transparency support
5. **Import to Unity**: Drag into `Assets/Levels/` folder
6. **Configure Import Settings**:
   - Read/Write Enabled: ✓
   - Filter Mode: Point (no filter)
   - Compression: None
   - Max Size: At least your texture size
7. **Assign to LevelGenerator**: 
   - Open Game scene
   - Select LevelGenerator object
   - Drag your texture to the `Level Map` field

### Important: Texture Import Settings

**If your level doesn't generate correctly**, check these settings in the Inspector when selecting your PNG:

```
Texture Type: Default
Read/Write: ✓ Enabled
Filter Mode: Point (no filter)
Compression: None
```

---

## System Architecture

### Component Overview

```
[MainMenu Scene]
├── Camera
├── UI (UIDocument + MainMenuController)
└── Light

[Game Scene]
├── Camera (top-down)
├── Floor
├── SceneContext (Zenject)
│   └── GameInstaller
├── LevelGenerator
│   └── Spawns: Walls, Player, Barriers, Pickups, Exit
├── GameManager
├── HUD (UIDocument + GameHUDController)
└── Light
```

### Event Flow

```
MaskPickup.OnTriggerEnter
    → MaskInventory.TryAddMask()
    → MaskInventory.OnInventoryChanged event
    → GameHUDController.RefreshAllSlots()

Player equips mask (1/2/3 key)
    → MaskInventory.EquipSlot()
    → MaskInventory.OnMaskEquipped event
    → PlayerController.UpdatePlayerColor()
    → GameHUDController.RefreshAllSlots()

ColorBarrier.OnTriggerEnter
    → MaskInventory.CanPassThrough()
    → If true: Disable solid collider, allow passage

ColorBarrier.OnTriggerExit
    → Re-enable solid collider
    → MaskInventory.ConsumeEquipped()

ExitGoal.OnTriggerEnter
    → ExitGoal.OnPlayerReachedExit event
    → GameManager.OnLevelComplete()
    → Scene transition to MainMenu
```

### Zenject Dependency Injection

The `GameInstaller` binds these services:
- `MaskInventory`: Singleton, manages 3 inventory slots
- `LevelGenerator`: From hierarchy, generates level from texture
- `GameHUDController`: From hierarchy, displays inventory UI
- `GameManager`: From hierarchy, handles win/lose conditions

---

## Prefab Reference

### Wall
- Simple 1x1x1 cube
- BoxCollider (solid)
- White emissive material

### Player
- Root: PlayerController, Rigidbody, CapsuleCollider
- Child "Visual": Capsule mesh with grey material
- Uses Rigidbody physics with gravity
- Tag: "Player" (set automatically in script)

### ColorBarrier
- 1x1x1 cube with ColorBarrier script
- Auto-creates dual colliders (trigger + solid)
- Transparent material for phasing visual effect
- Initialize() sets required color

### MaskPickup
- 0.5x0.5x0.5 cube with MaskPickup script
- Trigger collider (auto-set)
- Rotates and bobs in Update()
- Initialize() sets color type to give

### ExitGoal
- Flat cylinder platform
- Tall trigger collider to catch player
- Green emissive material with pulsing effect
- Fires static event when player enters

---

## Troubleshooting

### Player Falls Through Floor
- **Cause**: No floor in scene or floor position wrong
- **Fix**: Ensure Floor object exists at Y=-0.5

### Barriers Don't Block/Allow Passage
- **Cause**: Player tag not set
- **Fix**: Verify player has "Player" tag (set automatically by PlayerController)

### Level Doesn't Generate
- **Cause**: Texture import settings incorrect
- **Fix**: Enable Read/Write, set Filter to Point, disable Compression

### Pickups Can't Be Collected
- **Cause**: MaskInventory not injected
- **Fix**: Ensure SceneContext has GameInstaller in Installers list

### HUD Doesn't Show
- **Cause**: Missing PanelSettings or UXML reference
- **Fix**: Assign PanelSettings.asset and HUD.uxml to UIDocument

### Scene Transition Fails
- **Cause**: Scenes not in Build Settings
- **Fix**: Run "Update Build Settings" from Game Setup window

---

## Tips for Level Design

1. **Always surround with walls** - Prevents player from walking off the map
2. **Place pickups before barriers** - Player needs mask before reaching barrier
3. **One player spawn** - Only place one grey pixel
4. **Test mask combinations** - Yellow = Red + Green, Cyan = Green + Blue, etc.
5. **Place exit last** - Put it where the player should end up after all barriers

---

*Generated by Breaking Hue Game Setup Tool*
