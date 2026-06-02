# Checkpoints, Respawn, And Hazards

## Purpose
This system controls where the player returns after death or hazard contact.

There are two checkpoint types:
- Permanent checkpoints: used for normal death respawn and restore health/stamina fully.
- Mini checkpoints: used for hazard respawn and do not restore health/stamina fully.

## Main Files
- `Assets/scripts/Checkpoint.cs`
- `Assets/scripts/CheckpointManager.cs`
- `Assets/scripts/PlayerRespawnController.cs`
- `Assets/scripts/InstantKillHazard.cs`
- `Assets/scripts/player.cs`
- `Assets/scripts/Combat/Health.cs`
- `Assets/scripts/Combat/Stamina.cs`

## Runtime Flow
1. `PlayerRespawnController.Awake()` registers the player starting position as fallback spawn.
2. `Checkpoint` triggers set either the permanent or mini checkpoint on `CheckpointManager`.
3. Player death triggers `PlayerRespawnController.HandleDeath()`.
4. Normal death respawns to the permanent checkpoint and restores health/stamina fully.
5. `InstantKillHazard` optionally damages the player and then respawns to the mini checkpoint.
6. `CheckpointManager` applies respawn grace to avoid immediate retrigger loops.

## Inspector Wiring
- Checkpoint object needs a trigger or collision collider and `Checkpoint`.
- `Checkpoint.respawnPoint` can point to a child marker; if missing, the checkpoint transform is used.
- Hazard object needs trigger collider and `InstantKillHazard`.
- Player should have `PlayerRespawnController`, `player`, `Health`, and `Stamina`.

## Important Rules
- Mini respawn is intentionally different from permanent respawn: it does not fully restore health/stamina.
- Respawn grace exists to prevent hazards or death events from immediately firing again.
- Hazards use `player.TryTakeDamage`, so player invincibility can affect damage acceptance.

## Known Issues
- The term `InstantKillHazard` is misleading when `damageAmount` is not lethal; it can function as a damage-plus-respawn hazard.
- `CheckpointManager` can auto-create itself if missing, so scene setup mistakes may be less obvious.

## Related Docs
- `../../COMBAT_SYSTEM_OVERVIEW.md`

