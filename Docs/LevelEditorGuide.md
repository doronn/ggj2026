# Level Editor Technical Documentation

This document provides comprehensive context about the Level Editor tool for the Breaking Hue game project. It is intended for developers and AI agents working on level creation features.

## Overview

The Level Editor is a Unity Editor window (`Window > Breaking Hue > Level Editor`) that provides a visual grid-based interface for creating and editing game levels. It uses a layer-based system where different game elements are organized into separate editable layers.

## File Structure

```
Assets/Editor/
├── LevelEditorWindow.cs      # Main editor window (2000+ lines)
├── LevelEditorAction.cs      # Undo/redo action data structures
├── LevelDataSnapshot.cs      # Working copy for non-destructive editing
└── LevelEditorHelpWindow.cs  # Help popup window

Assets/Scripts/Level/Data/
├── LevelData.cs              # ScriptableObject containing all level data
├── EntranceExitLink.cs       # Portal linking system
└── [Layer classes]           # GroundLayer, WallLayer, etc.

Assets/Scripts/Gameplay/Bot/
└── BotPathData.cs            # Bot path waypoints and modes
```

## Architecture

### Data Flow

```
LevelData (Asset)
      ↓ Load
LevelDataSnapshot (Working Copy)
      ↓ Edit operations
      ↓ Undo/Redo stack
      ↓ Save
LevelData (Asset) → AssetDatabase
```

### Key Classes

#### `LevelEditorWindow` (EditorWindow)
The main editor window with three-panel layout:
- **Left Panel (220px)**: Layer selection, visibility toggles, tools, save button
- **Center Panel**: Visual 32x32 grid for editing
- **Right Panel (280px)**: Properties, statistics, legend, context help

#### `LevelDataSnapshot`
Deep copy of `LevelData` for non-destructive editing:
- All edits modify the snapshot, not the original asset
- Changes only persist when user clicks "Save"
- Enables proper undo/redo without affecting disk

#### `LevelEditorAction`
Represents an undoable operation:
```csharp
class LevelEditorAction {
    EditorLayer Layer;
    ActionType Type;      // Add, Remove, Modify, Move, BatchAdd, BatchRemove
    List<ActionData> Elements;
    string Description;
}
```

## Grid System

- **Grid Size**: 32x32 cells (constant, not configurable per level)
- **Visual Cell Size**: 16 pixels × zoom factor
- **Zoom Range**: 0.25x to 3.0x (scroll wheel)
- **Coordinate System**: Y-axis flipped (grid Y=0 at bottom, editor Y=0 at top)
- **Pan**: Middle mouse button drag

### Coordinate Conversion

```csharp
// Screen position to grid coordinates
Vector2Int? ScreenToGrid(Vector2 screenPos)

// Grid position to editor visual position
Vector2 GridToEditor(Vector2Int gridPos, Vector2 gridStart, float cellSize)

// Grid to world (runtime) - in LevelData
Vector3 GridToWorld(int x, int y)
```

## Layer System

The editor has 9 layers, each with a visibility toggle:

| Index | Layer | Data Structure | Visual Color |
|-------|-------|----------------|--------------|
| 0 | Ground | `List<Vector2Int>` | Dark grey (0.3, 0.3, 0.35) |
| 1 | Walls | `List<Vector2Int>` | White |
| 2 | Barriers | `List<BarrierDataSnapshot>` | Color-tinted |
| 3 | Pickups | `List<PickupDataSnapshot>` | Bright color-tinted |
| 4 | Barrels | `List<BarrelDataSnapshot>` | Dark color-tinted |
| 5 | Bots | `List<BotDataSnapshot>` | Cyan (red if no path) |
| 6 | Bot Paths | (editing mode) | Yellow (current), Cyan (existing) |
| 7 | Portals | `List<PortalDataSnapshot>` | Teal/Orange (checkpoint) |
| 8 | Hidden Areas | `List<HiddenBlockDataSnapshot>` | Very dark grey |

### Layer Visibility

```csharp
private bool[] _layerVisibility = new bool[9] { true, true, true, true, true, true, true, true, true };
```

Visibility is checked in `GetCellColor()` before rendering each layer.

## Tools

### Paint Tool (Hotkey: 1)
- Left-click/drag to add elements
- For colored elements, uses `_selectedColor`

### Erase Tool (Hotkey: 2)
- Left-click/drag to remove elements
- Only affects current layer

### Select Tool (Hotkey: 3)
- Click to select single item
- Shift+click for multi-select
- Drag selected items to move
- Delete key removes selection
- Properties panel shows editable properties

### Fill Tool (Hotkey: 4)
- Click and drag to define rectangle
- Release to fill entire area
- Recorded as single undo action (`BatchAdd`)

## Undo/Redo System

### Implementation
```csharp
private Stack<LevelEditorAction> _undoStack;
private Stack<LevelEditorAction> _redoStack;
private const int MaxUndoSteps = 100;
```

### Keyboard Shortcuts
- **Undo**: Ctrl+Z (Cmd+Z on Mac)
- **Redo**: Ctrl+Shift+Z or Ctrl+Y

### Action Recording
Every modification calls `RecordAction()` before making changes:
```csharp
RecordAction(new LevelEditorAction(EditorLayer.Ground, ActionType.Add) {
    Elements = { new ActionData(pos) }
});
```

## Non-Destructive Editing

### Working Copy Flow
1. On level load: `_workingCopy = LevelDataSnapshot.CreateFrom(_currentLevel)`
2. All edits modify `_workingCopy`
3. On save: `_workingCopy.ApplyTo(_currentLevel)` + `AssetDatabase.SaveAssets()`

### Unsaved Changes Handling
- `_hasUnsavedChanges` flag tracks dirty state
- Window title shows asterisk when dirty: "Level Editor*"
- Warning dialog on:
  - Switching levels
  - Closing window
  - Creating new level

## Bot Path System

### Data Structures
```csharp
class BotDataSnapshot {
    string BotId;
    Vector2Int StartPosition;
    ColorType InitialColor;
    PathMode PathMode;          // Loop, PingPong, OneWay
    BotPathData PathData;       // Optional: external asset
    List<Vector2Int> InlineWaypoints;
}
```

### Path Editing Workflow
1. Switch to Bot Paths layer
2. Select bot from dropdown OR click on bot in grid
3. Click grid cells to add waypoints
4. Click "Apply Path to Selected Bot"

### Visual Indicators
- Numbered waypoints (1, 2, 3...)
- Directional arrows on path lines
- Dashed line for loop return
- Red highlight for bots without paths

## Portal System

### Data Structure
```csharp
class PortalDataSnapshot {
    string PortalId;
    Vector2Int Position;
    bool IsCheckpoint;
    bool IsEntrance;
    EntranceExitLink Link;
}
```

### Linking Workflow
1. Click first portal (selects as Portal A)
2. Click second portal (selects as Portal B)
3. Click "Create Link" button
4. Save location for `EntranceExitLink` asset
5. Link automatically assigned to both portals

## Color System

```csharp
enum ColorType {
    Red, Yellow, Blue,      // Primary
    Orange, Green, Purple,  // Secondary
    Black
}
```

Used by: Barriers, Pickups, Barrels, Bots

Color picker UI in properties panel for relevant layers.

## Input Handling

### Mouse Input
| Input | Action |
|-------|--------|
| Left Click/Drag | Tool operation |
| Middle Mouse Drag | Pan grid |
| Right Click | Context menu |
| Scroll Wheel | Zoom |

### Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| 1 | Paint tool |
| 2 | Erase tool |
| 3 | Select tool |
| 4 | Fill tool |
| Ctrl+Z | Undo |
| Ctrl+Shift+Z / Ctrl+Y | Redo |
| Ctrl+S | Save |
| Escape | Clear selection / Cancel |
| Delete | Delete selection |

## UI Components

### Tooltips
All interactive elements have tooltip content:
```csharp
private static class Tooltips {
    public static readonly GUIContent Paint = new GUIContent("Paint", "Click or drag to add elements...");
    // ...
}
```

### Legend Panel
Collapsible section (`_showLegend`) showing:
- Element types with color swatches
- All 7 color variants

### Context Help
Collapsible section (`_showHelp`) with layer-specific instructions.

### Help Window
`LevelEditorHelpWindow` popup with sections:
- Overview
- Layers
- Tools
- Bot Paths
- Portals
- Keyboard Shortcuts
- Tips & Best Practices

## Extension Points

### Adding a New Layer
1. Add enum value to `EditorLayer`
2. Add data structure to `LevelDataSnapshot`
3. Add snapshot class (e.g., `NewLayerDataSnapshot`)
4. Update `LevelDataSnapshot.CreateFrom()` and `ApplyTo()`
5. Add visibility toggle (extend `_layerVisibility` array)
6. Add to `GetCellColor()` rendering
7. Add to `DrawLayerButtonWithVisibility()` calls
8. Add Add/Remove methods
9. Add to `HandleGridClick()` switch
10. Add to `GetItemAtPosition()` for selection
11. Add to `DeleteItemAtPositionNoUndo()` for batch operations

### Adding a New Tool
1. Add enum value to `EditorTool`
2. Add tool button in `DrawLayerPanel()`
3. Add keyboard shortcut in `HandleKeyboardShortcuts()`
4. Add input handler method
5. Add to input handling switch in `HandleInput()`

## Common Debugging

### Level Not Saving
- Check `_hasUnsavedChanges` flag
- Verify `ApplyTo()` is copying all data
- Ensure `EditorUtility.SetDirty()` is called

### Undo Not Working
- Verify `RecordAction()` is called BEFORE modification
- Check action has correct `Elements` data
- Verify `RestoreElement()` handles the layer type

### Visual Not Updating
- Call `Repaint()` after modifications
- Check layer visibility toggle
- Verify `GetCellColor()` checks the correct data source

### Selection Not Working
- Verify `GetItemAtPosition()` checks the layer
- Check layer visibility is enabled
- Ensure selection list is being updated

## Testing Checklist

When modifying the level editor:
- [ ] All layers can be painted and erased
- [ ] Undo/redo works for all operations
- [ ] Save/load preserves all data
- [ ] Selection and move works
- [ ] Fill tool creates correct rectangles
- [ ] Bot paths can be created and applied
- [ ] Portal linking creates valid assets
- [ ] Unsaved changes warning appears
- [ ] Keyboard shortcuts work
- [ ] Layer visibility toggles work

## Related Runtime Components

- `LevelManager` - Loads and instantiates levels at runtime
- `LevelGenerator` - Legacy texture-based generation (deprecated)
- `BotController` - Uses path data for bot movement
- `Portal` - Uses `EntranceExitLink` for teleportation
- `CheckpointManager` - Uses checkpoint portals for save system
