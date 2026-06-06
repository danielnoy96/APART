# APART Systems Index

Purpose: quick map of the game systems so future work can start from the right context file instead of rereading every note.

## Read First
- `../AGENTS.md` - project instructions for AI agents.
- `../PROJECT_CONTEXT.md` - current high-level project snapshot.
- `../COMBAT_SYSTEM_OVERVIEW.md` - broad combat/player/enemy foundation.
- `../GOAP_CRASHKONIJN_WORKING_NOTES.md` - deep GOAP playbook; search by section instead of reading fully by default.

## System Docs

### AI
- `AI/GOAPOverview.md` - what GOAP owns, what it does not own, and current actions/sensors.

### Enemy
- `Enemy/EnemyAwareness.md` - wake/hide/active state gate for enemy behavior.
- `Enemy/EnemyController.md` - enemy movement executor: patrol, chase, jumping, stopping, death movement shutdown.

### World
- `World/CheckpointsRespawnHazards.md` - permanent checkpoints, mini checkpoints, respawn grace, instant-kill hazards.
- `World/BreakableCrumblePlatforms.md` - timed breakable platforms, shard generation, crumble VFX, respawn.

### Combat
- `Combat/PlayerCombo.md` - successful-hit combo counter and stamina refund.

### VFX
- `VFX/GameplayVFX.md` - ownership and debugging guide for gameplay-triggered player/enemy particle effects.

### UI
- `UI/HUDAndReset.md` - player health/stamina HUD and runtime reset button.

## Important Context Rules
- GOAP is planning/orchestration only. Real movement is still owned by `EnemyController`.
- `EnemyAwareness` is an enemy gameplay gate, not a GOAP system, but GOAP actions must respect it.
- Checkpoint mini-respawn and permanent-respawn have different restore behavior.
- Breakable platform visuals are not cosmetic-only; they are coupled to platform enable/disable timing.
- Gameplay-triggered particles are owned by VFX controller scripts; ParticleSystem inspector settings own the look.
