# APART Combat Foundation

> Purpose: overview of the current combat, damage, stamina, knockback, corpse, and player action systems.
>
> Last updated: 2026-05-31

## Related Docs
- `PROJECT_CONTEXT.md` - high-level project snapshot.
- `Docs/SYSTEMS_INDEX.md` - focused system doc map.
- `Docs/Combat/PlayerCombo.md` - combo-specific details.
- `Docs/World/CheckpointsRespawnHazards.md` - respawn, permanent checkpoints, mini checkpoints, hazards.
- `Docs/UI/HUDAndReset.md` - health/stamina HUD and runtime reset.
- `Docs/Enemy/EnemyController.md` - enemy movement, contact damage sensor, knockback behavior.

## High-Level Architecture

### Player
- Main owner: `Assets/scripts/player.cs`.
- Action selection: plain C# FSM states inheriting from `PlayerState`, not MonoBehaviours.
- Combat execution:
  - `Combat` for melee hit detection and cooldown.
  - `Stamina` for jump/attack/dash/life-drain costs and regeneration.
  - `Health` for HP, damage, heal, death, and revive.
  - `KnockbackReceiver` for Rigidbody2D knockback velocity.
  - `PlayerCombo` for successful-hit chain stamina refunds.
- Player-specific rules:
  - Invincibility lives in `player.TryTakeDamage`.
  - Player damage should go through `player.TryTakeDamage`, not directly through `Health.TakeDamage`, unless intentionally bypassing i-frames.
  - Knockback lock lives in `player` and is respected by `PlayerState` movement helpers.
  - Respawn uses `player.ResetForRespawn`.

### Enemy
- Lifecycle, hurt/death presentation, and corpse setup: `Enemy`.
- Movement execution: `EnemyController`.
- Contact damage: `ContactDamage`, usually on a child trigger sensor.
- Damage storage: `Health`.
- Corpse interaction: `DrainableCorpse` is ensured on death and remains in scene for life drain.
- Enemy death disables contact damage and stops movement; dead enemies are not destroyed by default.

## Core Components

### `Assets/scripts/Combat/Health.cs`
Reusable HP component.

API:
- `TakeDamage(int damage)`
- `Heal(int amount)`
- `ReviveFull()`
- `Die()` protected virtual

Events:
- `OnDamaged(int damageApplied)`
- `OnHealed(int healApplied)`
- `OnDeath()`

Rules:
- `Awake()` clamps max health and initializes current health to max.
- Death fires once because it is guarded by `isDead`.
- `ReviveFull()` clears `isDead`, restores full health, and invokes `OnHealed` if health increased.
- Permanent checkpoint respawn uses `ReviveFull()`.

### `Assets/scripts/Combat/Stamina.cs`
Reusable stamina component.

API:
- `HasStamina(float amount)`
- `TrySpend(float amount)`
- `Restore(float amount)`
- `RestoreFull()`

Events:
- `OnStaminaChanged(current, max)`
- `OnStaminaEmpty()`
- `OnStaminaFull()`

Rules:
- `TrySpend` blocks regeneration until `regenDelayAfterSpend` passes.
- Regeneration happens in `Update`.
- `Restore` and `RestoreFull` are clamped to max stamina.
- Permanent checkpoint respawn uses `RestoreFull()`.

### `Assets/scripts/Combat/Combat.cs`
Player melee hit detection and attack cooldown.

Key behavior:
- `BeginAttack()` starts cooldown and optionally runs debug instant attack.
- `PerformHitCheck()` uses `Physics2D.OverlapCircleAll`.
- Each `Health` is damaged at most once per attack using a `HashSet`.
- Hit checks emit:
  - `OnHitCheckCompleted(bool hitSomething)`
  - `OnSuccessfulHit(int damagedCount)`
- Knockback prefers the target's `EnemyController.KnockbackReceiver`, then falls back to receivers on parent/children.
- Animation clips should prefer `PlayerAnimationEventRelay.AttackHit`; legacy direct animation events to `Combat.PerformHitCheck` still work.

### `Assets/scripts/Combat/ContactDamage.cs`
Reusable contact damage applicator, commonly used on enemy child trigger sensors.

Rules:
- Respects owner `Health.IsDead`; dead damage sources do nothing.
- Uses `damageCooldown` to avoid per-frame damage spam.
- Checks `targetLayer`.
- For player targets, calls `player.TryTakeDamage(damageAmount)`.
- If player damage is accepted, applies knockback and starts the player's knockback lock.
- For non-player targets, falls back to `Health.TakeDamage`.

### `Assets/scripts/Combat/KnockbackReceiver.cs`
Reusable Rigidbody2D knockback velocity setter.

Rules:
- Receiver can live on the Rigidbody2D object or a child; it searches parents for Rigidbody2D.
- `ApplyKnockback(sourcePosition)` pushes away from the source with guaranteed horizontal separation.
- `IsKnockbackActive` stays true for `knockbackDuration`.
- Player and enemy movement code should not overwrite velocity during active knockback.

### `Assets/scripts/Combat/DrainableCorpse.cs`
Corpse interaction data for life drain.

API:
- `ConfigureHealAmount(int newHealAmount)`
- `RenderBehind(Component drainer)`
- `Drain()`
- `DestroyCorpse()`

Rules:
- `ConfigureHealAmount` sets heal amount, recalculates drain duration from `secondsPerHealPoint`, and clears drained state.
- `RenderBehind` adjusts corpse and drainer sorting so the corpse renders behind the player during drain.
- `Drain()` returns `0` after the corpse has already been drained.
- `DestroyCorpse()` only destroys if `destroyAfterDrain` is true.

### `Assets/scripts/Combat/PlayerCombo.cs`
Successful-hit combo reward.

Rules:
- Requires `player`.
- Subscribes to `Combat.OnHitCheckCompleted`.
- Only successful hit checks increment combo progress.
- Combo resets on timeout or after reward.
- Completing the combo restores stamina through `Stamina.Restore`.
- Respawn resets combo through `player.ResetForRespawn`.

## Player FSM Combat Flow

Main files:
- `Assets/scripts/player.cs`
- `Assets/scripts/PlayerState.cs`
- `Assets/scripts/PlayerIdleState.cs`
- `Assets/scripts/PlayerMoveState.cs`
- `Assets/scripts/PlayerJumpState.cs`
- `Assets/scripts/PlayerAttackState.cs`
- `Assets/scripts/PlayerDashState.cs`
- `Assets/scripts/PlayerLifeDrainState.cs`

Input priorities in idle/move:
- `Dash > LifeDrain > Attack > Jump > Move > Idle`

Important behavior:
- Attack spends stamina on entry, triggers timed attack animation, stops horizontal velocity, and starts `Combat` cooldown.
- Attack damage happens only through hit check timing: animation event or debug instant mode.
- Attack state has a fallback exit when cooldown is ready, so missing attack-finished events do not permanently lock controls.
- Dash spends stamina, starts cooldown, applies dash velocity, then eases out or applies end lag depending on settings.
- Jump spends stamina only when the jump impulse is actually applied.
- Life drain is corpse interaction, not attack hit detection.
- Life drain requires grounded state, held interact input, and a valid non-drained corpse.
- Life drain spends stamina over time, heals on successful drain completion, and can refund half of stamina spent during a successful drain.

## Enemy Combat Flow

Main files:
- `Assets/scripts/Enemy/Enemy.cs`
- `Assets/scripts/Enemy/EnemyController.cs`
- `Assets/scripts/Combat/ContactDamage.cs`
- `Assets/scripts/Combat/DrainableCorpse.cs`

Important behavior:
- `Enemy` subscribes to `Health.OnDamaged` and `Health.OnDeath`.
- On damage, `Enemy` can play hurt animation and hit particles.
- On death, `Enemy` plays death presentation, disables contact damage, stops Rigidbody2D movement, and finalizes a drainable corpse.
- Corpse heal amount is computed from enemy max health using `corpseHealRatio`, `minCorpseHeal`, and `maxCorpseHeal`.
- `EnemyController.SetDead()` also stops movement, disables child `ContactDamage`, and disables the controller.
- Contact damage should usually live on a child `DamageSensor` with a trigger collider.

## Respawn And Hazards

Main files:
- `Assets/scripts/PlayerRespawnController.cs`
- `Assets/scripts/CheckpointManager.cs`
- `Assets/scripts/Checkpoint.cs`
- `Assets/scripts/InstantKillHazard.cs`

Important behavior:
- `PlayerRespawnController` listens to player `Health.OnDeath`.
- Normal death respawns to the permanent checkpoint and restores health/stamina fully.
- Hazard contact can damage the player and respawn to the mini checkpoint.
- Mini checkpoint respawn does not fully restore health/stamina.
- Respawn grace prevents immediate retrigger loops from hazards or death events.

## UI

Main file:
- `Assets/scripts/UI/PlayerHUD.cs`

Behavior:
- Finds player, health, and stamina if not assigned.
- Subscribes to health/stamina events in `OnEnable`.
- Updates either filled `Image` bars or `Slider` bars.
- Unsubscribes in `OnDisable`.

## Input Actions

Expected New Input System action names for Send Messages:
- `Move` -> `player.OnMove(InputValue)`
- `Jump` -> `player.OnJump(InputValue)`
- `Attack` -> `player.OnAttack(InputValue)`
- `Sprint` -> `player.OnSprint(InputValue)` for dash
- `Interact` -> `player.OnInteract(InputValue)` for life drain

If action names differ, rename the actions or rename the callback methods to match Send Messages conventions.

## Inspector Wiring Checklist

### Player
Required or expected components:
- `player`
- `Rigidbody2D`
- `PlayerInput` using Send Messages
- `Health`
- `Stamina`
- `Combat`
- `KnockbackReceiver`
- `PlayerRespawnController`
- Optional `PlayerCombo`

Important assignments:
- Grounding: `groundCheck`, `groundLayer`
- Combat: `attackPoint`, `damageableLayer`
- Life drain: `drainCheckPoint`, `drainCheckRadius`, `drainableLayer`
- HUD references if not auto-found
- Animation bool params if defaults differ

### Enemy
Required or expected components:
- `Health`
- `Enemy`
- `EnemyController`
- `KnockbackReceiver`
- Child `ContactDamage` trigger sensor
- Optional `EnemyAwareness`
- Optional GOAP components described in `PROJECT_CONTEXT.md` and `Docs/AI/GOAPOverview.md`

Important assignments:
- Contact damage `targetLayer` includes Player layer.
- Enemy body collision and contact trigger collision should be configured deliberately in the physics matrix.
- Jumping enemies need `groundCheck`, `groundLayer`, `wallCheck`, and `obstacleLayer`.

## Rules To Preserve
- Player i-frames live in `player.TryTakeDamage`.
- Damageable objects should use `Health`.
- Contact damage from dead enemies must stop.
- Knockback should not be overwritten by player/enemy movement while active.
- Dead enemies remain as drainable corpses unless a specific mechanic says otherwise.
- Life drain heals only after `DrainableCorpse.Drain()` succeeds.
- Combo progress is based on successful hit checks, not button presses.
- Respawn must reset player state before restoring health/stamina.

