# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**BindTD** — A top-down roguelike tower defense game built in Unity 6 (6000.3.8f1) using URP 17.3.0. Players bind turrets with different bullet types, manage resources, unlock augments, and defend against waves on procedurally generated terrain.

The game design document lives at `Assets/Docs/BindTDGDD.md`.

## Build & Development

- **Unity Version:** 6000.3.8f1 (Unity 6)
- **Render Pipeline:** URP with dual presets — `Assets/Settings/PC_RPAsset.asset` (PC) and `Assets/Settings/Mobile_RPAsset.asset` (Mobile)
- **Input System:** New Input System (`com.unity.inputsystem` 1.18.0)
- **Platform target:** PC (keyboard + mouse)
- **Scenes:** `GameScene`, `MainMenuScene`, `LoadingScene` (in `Assets/Scenes/`)
- Open in Unity Hub and load `GameScene` to play, or `MainMenuScene` for the full flow

There is no CLI build pipeline or test runner configured — all building and testing is done through the Unity Editor.

## Architecture

### Game State Flow

`GameHandler` is the central state machine with states: `WaitingToStart` → `Preparation` → `Playing` → `GameOver`. State transitions broadcast through the static `GameEvents` event bus. Waves start when the player expands the map (no auto-timer).

### Event System

`GameEvents.cs` is the static event dispatcher that decouples systems. Key events: `WaveStarted`, `WaveCompleted`, `PlayerDied`, `GameStateChanged`, `AugmentSelectionStarted`, `MapExpansionStarted`. UI events live in `GameUIEvent.cs`, audio in `SoundEvent.cs`.

### Core Systems (all under `Assets/Scripts/`)

| System | Key Files | Purpose |
|--------|-----------|---------|
| **Turret** | `Turret.cs`, `TurretBaseModule.cs`, `TurretBarrelModule.cs` | Targeting (7 modes), rotation, firing, LOS checks, range visualization |
| **Bullet** | `BulletProjectile.cs`, `BulletEffectApplicator.cs` | Projectile physics (arc/direct), elemental effects (Ice/Fire/Electric/Explosive), bouncing |
| **Enemy** | `Enemy.cs`, `BossEnemy.cs`, `EnemyAnimator.cs` | 3-layer HP (Health/Shield/Barrier), path following, debuff tracking |
| **Building** | `BuildManager.cs`, `PlacementSystem.cs`, `Node.cs` | Turret/bullet selection, grid placement, money validation |
| **Waves** | `WaveManager.cs`, `WaveConfigSO` | Wave spawning, group configuration, difficulty progression |
| **Augments** | `UpgradesManager.cs`, `AugmentSO.cs`, `StatShardSO.cs` | Roguelike augment cards (5 rarities), stat shards, deterministic RNG |
| **Map Gen** | `WFCWorldManager` (third-party), `Pathfinder.cs`, `MapExpansionManager.cs` | Wave Function Collapse terrain, path graph construction, chunk expansion |
| **Economy** | `PlayerStats.cs` | Static wallet/lives with events (`MoneyChanged`, `LivesChanged`) |
| **UI** | `UIManager.cs`, `Shop.cs`, `TurretUpgradeUI.cs`, `UpgradeSelectionUI.cs` | State-driven panel visibility, shop, augment card selection |
| **Audio** | `SoundManager.cs`, `AudioPoolManager.cs` | Pooled audio playback by category |
| **Pooling** | `BulletPoolManager`, `EnemyPoolManager`, `TurretPoolManager`, `VFXPoolManager` | Object pooling for all frequently spawned types |

### Design Patterns

- **Singletons** — Most managers (`BuildManager`, `GameHandler`, `WaveManager`, `UpgradesManager`, `Pathfinder`, etc.)
- **ScriptableObject configuration** — Turrets (`TurretBlueprintSO` + `TurretPropertiesSO`), bullets (`BulletBlueprintSO` + `BulletPropertiesSO`), augments (`AugmentSO`), stat shards (`StatShardSO`), waves (`WaveConfigSO`)
- **Static event bus** — `GameEvents` for cross-system communication
- **Object pooling** — Dedicated pool manager per spawnable type
- **Blueprint pattern** — Turrets and bullets are defined by a Blueprint SO (identity/cost) paired with a Properties SO (stats/behavior)

### Turret-Bullet Binding

Turrets and bullets are separate SOs. A turret has a default bullet but the player can swap bullets via the shop. Fire modes (Single, Multi-shot, Burst, Pulse, Arc, Beam) are on `TurretPropertiesSO`; elemental types (Normal, Ice, Fire, Electric, Explosive) are on `BulletPropertiesSO`. The combination of fire mode × bullet type creates gameplay variety.

### Key Helpers

- `PredictionHelpers.cs` — Arc and linear trajectory prediction for turret targeting
- `ShootHelpers.cs` — Shooting utility methods
- `BulletEffectApplicator.cs` — Applies elemental debuffs (slow, DOT, stun, chain lightning) to enemies on hit

## Conventions

- C# scripts live in `Assets/Scripts/`, SOs in `Assets/Scripts/SO/`
- Prefabs in `Assets/Prefabs/`, organized by type (turrets, bullets, enemies, VFX, UI, build presets)
- Audio clips in `Assets/Audio/`, referenced via `AudioRefClip` SOs
- `PlayerStats` uses static fields with `ResetStaticData()` for run resets — call this when starting a new game
- Deterministic RNG via seeded `System.Random` in `UpgradesManager` and WFC generation
