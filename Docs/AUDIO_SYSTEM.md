# Breaking Hue - Audio System Documentation

This document provides comprehensive information about the game's audio implementation for developers and AI agents working on audio-related features.

## Table of Contents
- [Overview](#overview)
- [Current Implementation](#current-implementation)
- [Audio Components](#audio-components)
- [Usage Patterns](#usage-patterns)
- [Extension Guide](#extension-guide)
- [Best Practices](#best-practices)

---

## Overview

Breaking Hue uses a **decentralized audio system** where sound effects are handled per-component using Unity's built-in `AudioSource` and `AudioClip` classes. There is currently no centralized AudioManager or AudioMixer.

### Current Capabilities
- Sound effect playback on gameplay events
- Looping audio support (e.g., charging sounds)
- One-shot audio playback
- AudioSource detachment for destruction-safe playback

### Not Yet Implemented
- Centralized volume control
- AudioMixer for mixing/effects
- Background music system
- Spatial audio configuration
- Audio pooling

---

## Current Implementation

### Project Audio Settings

Located in `ProjectSettings/AudioManager.asset`:

| Setting | Value |
|---------|-------|
| Global Volume | 1.0 |
| DSP Buffer Size | 1024 |
| Virtual Voice Count | 512 |
| Real Voice Count | 32 |

### Audio File Organization

Currently, audio clips are referenced via serialized fields on prefabs/components. There is no dedicated audio folder structure.

**Recommended folder structure (if implementing):**
```
Assets/
└── Audio/
    ├── SFX/
    │   ├── Player/
    │   ├── Gameplay/
    │   └── UI/
    └── Music/
```

---

## Audio Components

### 1. SelfDestructController

**File:** `Assets/Scripts/Gameplay/SelfDestructController.cs`

Handles the player's self-destruct/restart mechanic with audio feedback.

#### Audio Fields
```csharp
[Header("Audio")]
[SerializeField] private AudioClip chargingSound;  // Looping while holding
[SerializeField] private AudioClip triggerSound;   // On activation
[SerializeField] private AudioClip cancelSound;    // On release before trigger
```

#### Audio Behavior
| Event | Sound | Playback Type |
|-------|-------|---------------|
| Hold button start | `chargingSound` | Loop via `AudioSource.Play()` |
| Button released early | `cancelSound` | One-shot via `PlayOneShot()` |
| Hold complete | `triggerSound` | One-shot via `PlayOneShot()` |

#### Implementation Pattern
```csharp
private AudioSource _audioSource;

private void Awake()
{
    _audioSource = GetComponent<AudioSource>();
    if (_audioSource == null)
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
    }
}

// Start charging sound (looped)
private void StartChargingSound()
{
    if (chargingSound != null && _audioSource != null)
    {
        _audioSource.clip = chargingSound;
        _audioSource.loop = true;
        _audioSource.Play();
    }
}

// Stop charging and play cancel
private void CancelCharging()
{
    _audioSource.Stop();
    if (cancelSound != null)
    {
        _audioSource.PlayOneShot(cancelSound);
    }
}
```

---

### 2. ExplodingBarrel

**File:** `Assets/Scripts/Gameplay/ExplodingBarrel.cs`

Handles explosion sound effects for hazard barrels.

#### Audio Fields
```csharp
[Header("Audio")]
[SerializeField] private AudioSource explosionSFX;
```

#### Audio Behavior
- Plays explosion sound when barrel detonates
- **Detaches AudioSource from parent** before playing to survive object destruction

#### Implementation Pattern
```csharp
private void PlayExplosionSound()
{
    if (explosionSFX != null)
    {
        // Detach so sound continues after barrel destruction
        explosionSFX.transform.SetParent(null);
        explosionSFX.Play();
        
        // Clean up AudioSource after playback
        Destroy(explosionSFX.gameObject, explosionSFX.clip.length);
    }
}
```

**Key Pattern:** When playing sounds on objects that will be destroyed, detach the AudioSource first to allow the sound to complete.

---

### 3. HiddenBlock

**File:** `Assets/Scripts/Gameplay/HiddenBlock.cs`

Handles reveal sound when hidden areas are discovered.

#### Audio Fields
```csharp
[Header("Audio")]
[SerializeField] private AudioSource revealSFX;
```

#### Audio Behavior
- Plays reveal sound once when block is first revealed
- Only plays on initial reveal, not on checkpoint restore

#### Implementation Pattern
```csharp
public void Reveal()
{
    if (_isRevealed) return;
    
    _isRevealed = true;
    
    if (revealSFX != null)
    {
        revealSFX.Play();
    }
    
    // Visual fade out...
}
```

---

## Usage Patterns

### Pattern 1: Simple One-Shot Sound

For single sound effects triggered by events:

```csharp
[SerializeField] private AudioClip soundEffect;
private AudioSource _audioSource;

private void PlaySound()
{
    if (soundEffect != null && _audioSource != null)
    {
        _audioSource.PlayOneShot(soundEffect);
    }
}
```

### Pattern 2: Looping Sound with Start/Stop

For continuous sounds (charging, ambient):

```csharp
[SerializeField] private AudioClip loopingSound;
private AudioSource _audioSource;

private void StartLoop()
{
    _audioSource.clip = loopingSound;
    _audioSource.loop = true;
    _audioSource.Play();
}

private void StopLoop()
{
    _audioSource.Stop();
    _audioSource.loop = false;
}
```

### Pattern 3: Destruction-Safe Playback

For sounds on objects being destroyed:

```csharp
private void PlaySoundAndDestroy()
{
    if (_audioSource != null && _audioSource.clip != null)
    {
        // Detach AudioSource
        _audioSource.transform.SetParent(null);
        _audioSource.Play();
        
        // Schedule cleanup
        Destroy(_audioSource.gameObject, _audioSource.clip.length + 0.1f);
    }
    
    // Destroy this object
    Destroy(gameObject);
}
```

### Pattern 4: Auto-Create AudioSource

For components that may or may not have AudioSource attached:

```csharp
private AudioSource _audioSource;

private void Awake()
{
    _audioSource = GetComponent<AudioSource>();
    if (_audioSource == null)
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }
}
```

---

## Extension Guide

### Adding Sound to an Existing Component

1. Add serialized AudioClip field(s):
```csharp
[Header("Audio")]
[SerializeField] private AudioClip actionSound;
```

2. Get or create AudioSource in Awake:
```csharp
private AudioSource _audioSource;

private void Awake()
{
    _audioSource = GetComponent<AudioSource>();
    if (_audioSource == null)
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
    }
}
```

3. Play at appropriate event:
```csharp
private void OnActionTriggered()
{
    if (actionSound != null)
    {
        _audioSource.PlayOneShot(actionSound);
    }
}
```

4. Update prefab to include AudioSource component and assign AudioClip

### Adding Sound to New Entity Type

1. Follow the pattern above in your new component
2. Create/import AudioClip assets
3. Configure AudioSource on prefab:
   - `Play On Awake`: Usually false
   - `Spatial Blend`: 0 (2D) or 1 (3D) based on needs
   - `Volume`: 1.0 (adjust per sound)

### Suggested Sounds to Add

| Component | Event | Sound Type |
|-----------|-------|------------|
| `MaskPickup` | Collected | Positive chime |
| `DroppedMask` | Spawned | Soft thud |
| `ColorBarrier` | Phase through | Whoosh/shimmer |
| `Portal` | Enter | Teleport whoosh |
| `PlayerController` | Movement | Footsteps (optional) |
| `BotController` | Movement | Mechanical footsteps |
| `ExitGoal` | Reached | Victory fanfare |

---

## Best Practices

### Do's
- Always null-check AudioClip and AudioSource before playing
- Use `PlayOneShot()` for overlapping sounds
- Detach AudioSource before destroying objects with playing sounds
- Keep AudioClip references on prefabs, not hard-coded paths
- Use descriptive names for audio fields

### Don'ts
- Don't use `AudioSource.Play()` for rapid fire sounds (use pooling or PlayOneShot)
- Don't hard-code audio file paths
- Don't forget to clean up detached AudioSources
- Don't play sounds in `OnDestroy()` without detaching first

### Performance Considerations
- Limit simultaneous sound count (Real Voice Count = 32)
- Consider audio pooling for frequently played sounds
- Use compressed audio formats for longer clips
- Keep short SFX uncompressed for instant playback

---

## Future Improvements

### Centralized AudioManager (Recommended)

Consider implementing a singleton AudioManager for:
- Global volume control
- Sound categories (SFX, Music, UI)
- Audio pooling
- Fade in/out utilities

```csharp
// Example interface
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    public float MasterVolume { get; set; }
    public float SFXVolume { get; set; }
    public float MusicVolume { get; set; }
    
    public void PlaySFX(AudioClip clip, Vector3 position = default);
    public void PlayMusic(AudioClip clip, bool loop = true);
    public void StopMusic(float fadeTime = 0f);
}
```

### AudioMixer Integration

For advanced mixing:
1. Create AudioMixer asset
2. Add groups: Master, SFX, Music, UI
3. Expose volume parameters
4. Route AudioSources to appropriate groups

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Assets/Scripts/Gameplay/SelfDestructController.cs` | Charging/trigger/cancel sounds |
| `Assets/Scripts/Gameplay/ExplodingBarrel.cs` | Explosion sound |
| `Assets/Scripts/Gameplay/HiddenBlock.cs` | Reveal sound |
| `ProjectSettings/AudioManager.asset` | Global audio settings |

---

*Last updated: January 2026*
