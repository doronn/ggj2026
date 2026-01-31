# Breaking Hue - Controls, Camera & UI Documentation

This document provides comprehensive information about the game's control system, camera behavior, and UI architecture for agents working on this codebase.

## Table of Contents
- [Input System Overview](#input-system-overview)
- [Control Bindings](#control-bindings)
- [Camera System](#camera-system)
- [UI Architecture](#ui-architecture)
- [Key Files Reference](#key-files-reference)

---

## Input System Overview

The game uses **Unity's New Input System** with automatic device switching between keyboard/mouse and gamepad controllers.

### InputManager (`Assets/Scripts/Input/InputManager.cs`)

A singleton that manages input state and detects the active input device.

**Device Types:**
- `KeyboardMouse` - Keyboard and mouse input
- `XboxController` - Xbox controllers (detected via device name)
- `PlayStationController` - DualShock/DualSense controllers
- `GenericGamepad` - Any other gamepad

**Key Properties:**
- `InputManager.Instance` - Singleton access
- `CurrentDeviceType` - The currently active device
- `IsKeyboardMouse` / `IsGamepad` - Quick device type checks

**Key Methods:**
- `GetPlayerAction(string actionName)` - Get a specific input action
- `GetPlayerActionMap()` / `GetUIActionMap()` - Get action maps
- `EnablePlayerInput()` / `EnableUIInput()` / `EnableAllInput()` - Toggle action maps

**Events:**
- `OnInputDeviceChanged(InputDeviceType)` - Fires when input device changes

### Input Actions Asset (`Assets/Input/GameInputActions.inputactions`)

Contains two action maps:
1. **Player** - In-game controls
2. **UI** - Menu navigation

---

## Control Bindings

### Player Actions

| Action | Keyboard/Mouse | Xbox Controller | PlayStation |
|--------|---------------|-----------------|-------------|
| **Move** | WASD / Arrow Keys | Left Stick | Left Stick |
| **Look** (Camera) | Mouse Delta | Right Stick | Right Stick |
| **Toggle Mask 1** | 1 / Numpad 1 | D-Pad Left | D-Pad Left |
| **Toggle Mask 2** | 2 / Numpad 2 | D-Pad Down | D-Pad Down |
| **Toggle Mask 3** | 3 / Numpad 3 | D-Pad Right | D-Pad Right |
| **Deactivate All Masks** | 0 / ` (backtick) | D-Pad Up | D-Pad Up |
| **Drop Mask** | Q | X Button | □ (Square) |
| **Self-Destruct** (Restart) | Hold R (2 sec) | Hold B (2 sec) | Hold ○ (Circle) |
| **Pause** | Escape | Start/Menu | Options |

### UI Actions

| Action | Keyboard | Controller |
|--------|----------|------------|
| **Navigate** | Arrow Keys | Left Stick / D-Pad |
| **Submit** | Enter / Space | A (South) |
| **Cancel** | Escape | B (East) |
| **Point** | Mouse Position | N/A |
| **Click** | Left Mouse | N/A |

---

## Camera System

### GameCamera (`Assets/Scripts/Camera/GameCamera.cs`)

The camera supports two modes:

#### Third-Person Mode (Default)
- **Perspective camera** following the player
- Rotates around Y-axis based on mouse/right stick input
- Configurable distance and height angle

**Settings:**
- `distance` - Distance from player (default: 12)
- `heightAngle` - Angle from horizontal in degrees (default: 60°)
- `rotationSpeed` - Gamepad rotation speed (degrees/sec, default: 120)
- `mouseSensitivity` - Mouse look sensitivity (default: 0.3)
- `followSmoothTime` - Camera follow smoothing (default: 0.1s)

**Important Behaviors:**
- Camera input is **disabled when `Time.timeScale == 0`** (during pause)
- If no target is assigned, camera searches for `"Player"` tagged GameObject
- Movement is **camera-relative** (forward = camera's forward direction)

**Public Properties:**
- `Forward` - Camera's forward direction on XZ plane
- `Right` - Camera's right direction on XZ plane
- `CurrentYRotation` - Current Y rotation in degrees

#### Fixed Camera Mode (Legacy)
- Orthographic top-down view
- Used for grid-based puzzle levels
- Supports letterboxing

---

## UI Architecture

The game uses **Unity UI Toolkit** (UXML/USS) for all UI.

### Main Menu (`Assets/Scripts/UI/MainMenuController.cs`)

**Scene:** `MainMenu`

**Features:**
- Play button - Loads game scene
- Reset Progress button - Clears all save data (with confirmation dialog)
- Quit button - Exits application
- Controls display - Shows current input bindings

**Styling:**
- Buttons have focus/hover effects (white background + cyan border on focus)
- Initial focus set on Play button for controller navigation

### Pause Menu (`Assets/Scripts/UI/PauseMenuController.cs`)

**Trigger:** Escape / Start button

**Features:**
- Pauses game (`Time.timeScale = 0`)
- Resume button
- Exit to Menu button (resets time scale to 1)

**Technical Details:**
- `sortingOrder = 100` to render above other UI
- Root element has `pickingMode = PickingMode.Position` for mouse interaction
- 0.2s cooldown on pause toggle to prevent double-triggering

**Events:**
- `OnGamePaused` / `OnGameResumed` - Static events for other systems to subscribe

### Controls Bar (`Assets/Scripts/UI/ControlsBarController.cs`)

**Position:** Bottom of screen during gameplay

**Features:**
- Shows contextual control hints
- Auto-updates when input device changes
- Displays self-destruct progress bar when holding restart button

**Technical Details:**
- Root has `pickingMode = PickingMode.Ignore` so it doesn't block pause menu clicks

### InputIconProvider (`Assets/Scripts/UI/InputIconProvider.cs`)

Utility class for getting control display text.

**Key Methods:**
- `GetBindingText(actionName)` - Returns key/button text for current device
- `GetActionDisplayName(actionName)` - Returns human-readable action name
- `GetControlHint(actionName)` - Returns formatted hint like "[Q] Drop Mask"
- `GetControlsSummary()` - Returns full controls summary string

### Button Focus Styling

Both menus use consistent button styling:

**Normal State:**
- Colored background (green for primary, red for danger, gray for neutral)
- White text, rounded corners

**Focus State (Keyboard/Controller Navigation):**
- White background
- Black text
- 6px cyan border
- 1.15x scale

**Hover State (Mouse):**
- Slightly brighter background
- 1.05x scale

---

## Key Files Reference

### Input System
| File | Description |
|------|-------------|
| `Assets/Input/GameInputActions.inputactions` | Input action definitions |
| `Assets/Scripts/Input/InputManager.cs` | Device detection and input state |

### Camera
| File | Description |
|------|-------------|
| `Assets/Scripts/Camera/GameCamera.cs` | Third-person camera controller |

### UI Controllers
| File | Description |
|------|-------------|
| `Assets/Scripts/UI/MainMenuController.cs` | Main menu logic |
| `Assets/Scripts/UI/PauseMenuController.cs` | Pause menu logic |
| `Assets/Scripts/UI/ControlsBarController.cs` | In-game controls display |
| `Assets/Scripts/UI/InputIconProvider.cs` | Control text/icon provider |
| `Assets/Scripts/UI/GameHUDController.cs` | In-game HUD (mask inventory) |

### UI Assets
| File | Description |
|------|-------------|
| `Assets/UI/PauseMenu.uss` | Pause menu styles |
| `Assets/UI/ControlsBar.uxml` | Controls bar layout |
| `Assets/UI/ControlsBar.uss` | Controls bar styles |
| `Assets/UI/HUD.uxml` | Game HUD layout |
| `Assets/UI/HUD.uss` | Game HUD styles |
| `Assets/UI/PanelSettings.asset` | UI Toolkit panel settings |

### Player
| File | Description |
|------|-------------|
| `Assets/Scripts/Gameplay/PlayerController.cs` | Player movement and mask input |
| `Assets/Scripts/Gameplay/SelfDestructController.cs` | Restart/checkpoint mechanic |

---

## Common Tasks

### Adding a New Control Binding
1. Add action to `GameInputActions.inputactions` in both control schemes
2. Add binding entry to `InputIconProvider.DefaultBindings` dictionary
3. Subscribe to action in relevant controller (e.g., `PlayerController`)

### Changing Camera Settings
Modify serialized fields on the `GameCamera` component in the scene, or adjust defaults in `GameCamera.cs`.

### Adding a New Menu Button
1. Add button to UXML or create in code
2. Call `ApplyButtonInteractiveStyles(button, backgroundColor)` for consistent styling
3. Register click callback

### Responding to Device Changes
```csharp
private void Start()
{
    InputManager.OnInputDeviceChanged += OnDeviceChanged;
}

private void OnDeviceChanged(InputManager.InputDeviceType deviceType)
{
    // Update UI based on new device
}
```
