# Breaking Hue - Development Plan

## Project Architecture Complete ✅

### Files Created

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── ColorType.cs          # Flags enum + helpers
│   │   └── ColorInventory.cs     # Pure C# inventory service
│   ├── Level/
│   │   └── LevelGenerator.cs     # Pixel-to-prefab spawner
│   ├── Gameplay/
│   │   ├── ColorBarrier.cs       # Color-consuming barrier
│   │   ├── MaskPickup.cs         # Color pickup collectible
│   │   └── PlayerController.cs   # Rigidbody movement
│   ├── UI/
│   │   └── GameHUDController.cs  # UI Toolkit HUD
│   └── Installers/
│       └── GameInstaller.cs      # Zenject bindings
├── UI/
│   ├── HUD.uxml                  # UI Toolkit layout
│   └── HUD.uss                   # UI Toolkit styles
```

---

## Setup Instructions

### 1. Package Installation

The `manifest.json` has been updated to include:
- **UniTask** (`com.cysharp.unitask: 2.5.10`) - Async/await for Unity
- **Extenject/Zenject** (`com.svermeulen.extenject: 9.2.0`) - DI framework

Open Unity and let it resolve packages. If packages fail to load, use Window > Package Manager to manually add from OpenUPM.

### 2. Texture Import Settings (Level Map)

Create a 16x16 PNG texture for your level map with these **critical import settings**:

| Setting | Value |
|---------|-------|
| **Texture Type** | Default |
| **sRGB (Color Texture)** | ✅ Enabled |
| **Alpha Source** | Input Texture Alpha |
| **Alpha Is Transparency** | ✅ Enabled |
| **Read/Write** | ✅ Enabled (CRITICAL!) |
| **Generate Mip Maps** | ❌ Disabled |
| **Filter Mode** | Point (no filter) |
| **Compression** | None |
| **Format** | RGBA32 |

> ⚠️ **Read/Write MUST be enabled** or `GetPixel()` will throw an error!

### 3. Level Map Pixel Protocol

| Alpha Value | RGB Interpretation | Object Spawned |
|-------------|-------------------|----------------|
| `> 0.9` (Solid) | White (1,1,1) | Wall |
| `> 0.9` (Solid) | Grey (~0.5,0.5,0.5) | Player Spawn |
| `> 0.9` (Solid) | RGB Color | ColorBarrier |
| `0.4 - 0.6` (Semi) | RGB Color | MaskPickup |
| `< 0.4` | Any | Empty Space |

**Example Color Mappings:**
- Red barrier: `(255, 0, 0, 255)` - Requires Red color
- Green pickup: `(0, 255, 0, 128)` - Gives Green color
- Cyan barrier: `(0, 255, 255, 255)` - Requires Green + Blue

### 4. Scene Setup

1. **Create SceneContext:**
   - GameObject > Zenject > Scene Context
   - Add `GameInstaller` to the Installers list

2. **Create Prefabs:**
   - **Wall**: Cube with BoxCollider
   - **Player**: Cube with Rigidbody, BoxCollider, PlayerController, tag="Player"
   - **Barrier**: Cube with BoxCollider (IsTrigger), ColorBarrier
   - **Pickup**: Cube with BoxCollider (IsTrigger), MaskPickup

3. **Setup LevelGenerator:**
   - Add LevelGenerator to a GameObject
   - Assign prefabs and the level texture

4. **Setup HUD:**
   - Create GameObject with UIDocument component
   - Assign HUD.uxml as Source Asset
   - Add GameHUDController component

5. **Camera:**
   - Position camera above looking down (top-down view)
   - Example: Position (0, 10, 0), Rotation (90, 0, 0)

### 5. Tag Setup

Go to Edit > Project Settings > Tags and Layers and add:
- Tag: `Player`

---

## Assembly Definitions (Optional but Recommended)

Create `.asmdef` files for better compilation:

```
Assets/Scripts/Core/BreakingHue.Core.asmdef
Assets/Scripts/Level/BreakingHue.Level.asmdef
Assets/Scripts/Gameplay/BreakingHue.Gameplay.asmdef
Assets/Scripts/UI/BreakingHue.UI.asmdef
Assets/Scripts/Installers/BreakingHue.Installers.asmdef
```

---

## Quick Test

1. Create a simple 16x16 test texture:
   - White border = Walls
   - Grey pixel in corner = Player spawn
   - Red pixel (alpha=1) = Red barrier
   - Green pixel (alpha=0.5) = Green pickup

2. Run the scene, collect green pickup, pass through the green barrier.

---

## Next Steps

- [ ] Create prefabs with URP materials (emissive)
- [ ] Design first level texture
- [ ] Add win condition detection
- [ ] Add scene transitions
- [ ] Add audio feedback
