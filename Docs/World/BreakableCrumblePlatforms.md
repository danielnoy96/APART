# Breakable Crumble Platforms

## Purpose
Breakable timed platforms trigger when touched, play crumble/shard visuals, disable their colliders/renderers after a delay, then respawn after another delay.

## Main Files
- `Assets/scripts/BreakableTimedPlatform.cs`
- `Assets/scripts/BushCrumbleVFX.cs`
- `Assets/scripts/SpriteShardMeshBuilder.cs`
- `Assets/scripts/WoodCrumbleMaskGenerator.cs`
- `Assets/scripts/WoodCrumbleRegionBuilder.cs`
- `Assets/scripts/WoodCrumbleMaskPreview.cs`

## Runtime Flow
1. Platform detects trigger/collision from `triggerLayer`.
2. It starts a break routine and marks itself busy.
3. Linked `BushCrumbleVFX` plays for the crumble duration.
4. Shards are generated or reused from a prewarmed cache.
5. Main visual is hidden and colliders/renderers are disabled after `breakDelay`.
6. After `respawnDelay`, platform colliders/renderers and linked VFX are restored.

## Inspector Wiring
- `triggerLayer` should usually include Player.
- `visualTarget` should point to the sprite renderer used for shard generation.
- `shardMaterial` is optional; generated material can use the sprite texture.
- Optional linked `BushCrumbleVFX` can be assigned directly or auto-found on children.
- Mask generation settings control shard shape and count.

## Important Rules
- The crumble visuals and platform gameplay timing are coupled.
- Prewarming shards on start reduces runtime spikes.
- Destroying/rebuilding shard cache should be avoided during active gameplay unless necessary.
- If `visualTarget` or sprite is missing, gameplay still disables the platform but shard visuals will not spawn.

## Known Issues
- This system has many tuning fields and no separate high-level doc existed before this file.
- Mask preview runs in gizmo drawing and can be expensive if settings are large.

## Related Docs
- `../Enemy/EnemyController.md`

