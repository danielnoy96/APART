# Gameplay VFX

Purpose: define ownership for gameplay-triggered particle effects so animation clips, code, and ParticleSystem inspector tuning do not fight each other.

## Ownership Rule

Gameplay code decides when a gameplay VFX starts or stops.

ParticleSystem assets and inspector settings decide how the VFX looks.

Animation clips should animate sprite frames and animation intent. They should not be the primary trigger for hit, life-drain, death, or corpse gameplay particles unless the task is explicitly to add an animation-timed event.

## Main Files

- `Assets/scripts/PlayerVfxController.cs`
- `Assets/scripts/Enemy/EnemyVfxController.cs`
- `Assets/scripts/player.cs`
- `Assets/scripts/PlayerLifeDrainState.cs`
- `Assets/scripts/Enemy/Enemy.cs`
- `Assets/scripts/Combat/ContactDamage.cs`

## Player VFX

`PlayerVfxController` owns player gameplay particle playback.

Current player effects:

- Hit feedback is emitted from `player.TryTakeDamage`.
- Contact damage passes the damage source position into `player.TryTakeDamage`, so hit VFX can be oriented from where damage came from.
- Life drain feedback starts in `PlayerLifeDrainState.Enter` and stops in `PlayerLifeDrainState.Exit`.
- Respawn calls clear hit feedback and stop life-drain feedback.

Important tuning note:

- Hit particle visual direction may be dominated by the ParticleSystem's `Main > Start Speed` and `Shape` module. If code-set velocity appears ignored, inspect `Shape`, cone rotation, `Start Speed`, `Force over Lifetime`, `External Forces`, and parent transform scale before changing gameplay code.
- `Velocity over Lifetime` is not always the true source of visible motion if particles are born from a directional shape with nonzero start speed.

## Enemy VFX

`EnemyVfxController` owns enemy hit, death, and corpse particle playback.

Current enemy effects:

- Enemy hit feedback starts from `Enemy.HandleDamaged`.
- Enemy death feedback starts from `Enemy.HandleDeath`.
- Corpse feedback starts from `Enemy.FinalizeCorpse`, after `DrainableCorpse` is ensured/configured.

Corpse VFX is deliberately triggered after corpse finalization, not immediately on health death, so it means "this enemy is now a drainable corpse."

## Inspector Tuning

ParticleSystem modules and renderers should remain artist/designer-tuned in Unity.

For corpse particles, code should not overwrite renderer size, sorting layer, sorting order, material, shape, speed, color, or lifetime. `EnemyVfxController` should only resolve references, prevent unwanted play-on-awake, clear stale particles, and call `Play`.

For hit/death effects, code may still configure minimal runtime safety settings if the effect is intentionally code-authored. Before adding more runtime overrides, prefer exposing serialized fields or tuning the ParticleSystem in the inspector.

## Setup Expectations

Player:

- Root has `player` and `PlayerVfxController`.
- Hit ParticleSystem is usually under `Player/sprite/Hit vfx`.
- Life-drain ParticleSystem is assigned to `PlayerVfxController.lifeDrainParticles` or auto-resolved from a named child/fallback under `sprite`.

Enemy:

- Root has `Enemy`, `EnemyVfxController`, `Health`, and `EnemyController`.
- Hit effect child is usually named `enemy hit effect`.
- Death effect child is usually named `enemy dead effect`.
- Corpse effect child is usually named `enemy dead life drain effect`, `enemy corpse effect`, or `enemy corps effect`.

## Debug Checklist

If a particle effect does not show:

- Confirm the relevant VFX controller has a ParticleSystem reference.
- Confirm the effect GameObject is active.
- Confirm `Play On Awake` is not the only trigger.
- Confirm renderer sorting layer/order and `Max Particle Size`.
- Confirm material alpha and Color over Lifetime alpha.
- Confirm the owning gameplay method is actually called.

If particles move in the wrong direction:

- Check emitted particle velocity with `ParticleSystem.GetParticles`.
- Check `Main > Start Speed`.
- Check `Shape` type, rotation, random direction, and spherical direction.
- Check `Velocity over Lifetime`, `Force over Lifetime`, `Limit Velocity`, `External Forces`, and `Inherit Velocity`.
- Check parent/root scale and simulation space.
