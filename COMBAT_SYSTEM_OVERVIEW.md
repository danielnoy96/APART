# APART Combat Foundation (Systems Overview)

This document describes how the current 2D Metroidvania combat foundation is wired and how the main systems connect.

Notes:
- The player script class is named `player` (lowercase).
- Player gameplay logic is driven by a plain-C# Finite State Machine (FSM): states inherit `PlayerState` (not `MonoBehaviour`).
- Shared gameplay components (`Health`, `Stamina`, knockback, contact damage) are `MonoBehaviour`s intended to be reusable for both player and enemies.

---

## High-Level Architecture

**Player**
- Input + action decisions: `player` + FSM states (Idle/Move/Jump/Attack/Dash/LifeDrain).
- Execution helpers:
  - `Combat` for melee hit detection + cooldown.
  - `Stamina` for spending + regen.
  - `Health` for HP storage + events.
  - `KnockbackReceiver` for knockback velocity.
- Player-specific rules:
  - I-frames (invincibility) live in `player` via `TryTakeDamage`.
  - Knockback lock (prevents movement overwriting knockback) lives in `player` and is respected by movement helpers in `PlayerState`.

**Enemy**
- Lifecycle/damage reaction/corpse: `Enemy`.
- Movement/execution: `EnemyController`.
- Optional temporary decision-making (pre-GOAP): `EnemyBrain`.
- Contact damage: `ContactDamage` (usually on a child trigger sensor).
- Corpse interaction: `DrainableCorpse` is added/enabled on death and remains in-scene for Life Drain.

---

## Core Components

### `Assets/scripts/Combat/Health.cs`
Reusable HP component.
- Serialized: `maxHealth`, `currentHealth` (initialized to `maxHealth` in `Awake`).
- API:
  - `TakeDamage(int damage)`
  - `Heal(int amount)`
  - `Die()` (protected virtual)
- Events:
  - `OnDamaged(int damageApplied)`
  - `OnHealed(int healApplied)`
  - `OnDeath()`
- Death event fires once (guarded by `isDead`).

### `Assets/scripts/Combat/Stamina.cs`
Reusable stamina component.
- Serialized: `maxStamina`, `currentStamina`, `staminaRegenRate`, `regenDelayAfterSpend`
- API:
  - `HasStamina(float amount)`
  - `TrySpend(float amount)` (starts regen delay)
  - `Restore(float amount)`
  - `RestoreFull()`
- Events:
  - `OnStaminaChanged(current,max)`
  - `OnStaminaEmpty()`, `OnStaminaFull()` (edge-triggered; not spammed every frame)

### `Assets/scripts/Combat/Combat.cs`
Player melee hit detection + attack cooldown.
- Serialized: `attackPoint`, `attackRadius`, `damageableLayer`, `damage`, `attackCooldown`
- API:
  - `BeginAttack()` (sets cooldown timestamp)
  - `PerformHitCheck()` (Animation Event: hit frame)
  - `AttackAnimationFinished()` (Animation Event: final frame; calls `player.OnAttackAnimationFinished()`)
- Hit check uses `Physics2D.OverlapCircleAll` and ensures a `Health` is only damaged once per attack (HashSet).
- Optional debug (if enabled in inspector):
  - `debugInstantAttack`: runs hit+finish without animation events (for early testing).
  - `logHits`: logs hit results / no-hit diagnostics.

### `Assets/scripts/Combat/DrainableCorpse.cs`
Corpse interaction data for Life Drain.
- Serialized: `healAmount`, `drainDuration`, `destroyAfterDrain`
- Properties: `HealAmount`, `DrainDuration`, `IsDrained`, `DestroyAfterDrain`
- `Drain()`:
  - Returns `0` if already drained.
  - Marks drained and logs: `"Life drain complete"`.
  - Returns `healAmount`.
- `DestroyCorpse()`:
  - Only destroys if `destroyAfterDrain` is true.
  - Logs: `"Drainable corpse destroyed"`.

### `Assets/scripts/Combat/ContactDamage.cs`
Reusable contact damage applicator (commonly attached to an enemy child trigger sensor).
- Serialized: `damageAmount`, `damageCooldown`, `targetLayer`
- Uses cooldown timestamp to avoid per-frame damage.
- On overlap/collision:
  - If owner has `Health` and `IsDead == true`, it does nothing.
  - If target has `player`: calls `player.TryTakeDamage(damageAmount)` (respects i-frames).
    - If damage accepted: applies knockback if `KnockbackReceiver` exists and starts knockback lock on player.
  - Otherwise falls back to `Health.TakeDamage(damageAmount)` on target.

### `Assets/scripts/Combat/KnockbackReceiver.cs`
Reusable knockback velocity setter.
- Serialized: `knockbackForce`, `knockbackUpwardForce`, `knockbackDuration`, `minHorizontalFactor`
- `ApplyKnockback(sourcePosition)`:
  - Pushes away from source with guaranteed horizontal separation (avoids “pure knockup”).
  - Adds upward component.
- `IsKnockbackActive` is true for `knockbackDuration` after application (used by AI to avoid overriding velocity).

---

## Player FSM

### `Assets/scripts/PlayerState.cs`
Base for all player states.
- Provides helper methods:
  - `ApplyHorizontalMovement()` and `ApplyIdleHorizontalVelocity()` are **knockback-lock aware**: they early-return if `player.IsKnockbackLocked` is true.
- Jump buffering/coyote time uses existing `player` timers.

### `Assets/scripts/player.cs`
Main player component + FSM owner.
- Creates state instances in `Awake`:
  - `idleState`, `moveState`, `jumpState`, `attackState`, `dashState`, `lifeDrainState`
- Holds references (auto-found if null):
  - `Combat combat`, `Health health`, `Stamina stamina`
- Input flags (1-frame):
  - `attackPressed` via `OnAttack`
  - `dashPressed` via `OnSprint` (Dash reuses the existing Input Action named `Sprint`)
  - `lifeDrainPressed` via `OnInteract` (Life Drain reuses existing Input Action named `Interact`)
  - These flags reset in `LateUpdate`.
- Player i-frames:
  - `invincibilityDuration`, `TryTakeDamage(int damage)`
  - `TryTakeDamage` returns false while invincible.
- Knockback lock:
  - `StartKnockbackLock(duration)` and `IsKnockbackLocked`
- Life Drain detection:
  - Serialized: `drainCheckPoint`, `drainCheckRadius`, `drainableLayer`
  - `GetDrainableCorpse()` uses `Physics2D.OverlapCircle` at feet area and returns a non-drained corpse.

### State transitions (priority)
Current priority in Idle/Move:
`Dash > LifeDrain > Attack > Jump > Move > Idle`

Attack is **not cancelled by dash** (dash transitions are not allowed from `PlayerAttackState`).

### `Assets/scripts/PlayerAttackState.cs`
- On enter:
  - Triggers attack animation using player inspector string params (optional).
  - Stops horizontal velocity (keeps vertical).
  - Calls `combat.BeginAttack()` to start cooldown.
- Damage timing:
  - Damage is applied only by `Combat.PerformHitCheck()` (animation event or debug instant mode).

### `Assets/scripts/PlayerDashState.cs`
- On enter:
  - Spends stamina (`stamina.TrySpend(dashCost)`).
  - Applies dash velocity for `dashDuration`.
  - Optional end lag via `dashEndLag`.
  - Dash uses `dashPreserveVerticalVelocity` to choose horizontal-only or momentum dash.
- Cooldown:
  - `player.StartDashCooldown()` sets timestamp.
  - `player.CanDash` checks cooldown + stamina + not already dashing.

### `Assets/scripts/PlayerLifeDrainState.cs`
Corpse interaction (not an attack).
- Entry requirements:
  - Must be grounded.
  - `player.GetDrainableCorpse()` must return a valid corpse.
- During drain:
  - Logs `"Life drain in progress"` periodically (throttled).
  - Locks player horizontal movement.
- On completion:
  - Calls `corpse.Drain()`; only then heals player (`player.health.Heal(heal)`).
  - Destroys corpse **only after** completion (if configured).

---

## Enemy Systems

### `Assets/scripts/Enemy/Enemy.cs`
Enemy damage reaction + corpse setup.
- Subscribes to `Health.OnDamaged` and `Health.OnDeath`.
- On death:
  - Ensures `DrainableCorpse` exists/enabled (corpse remains in scene).
  - Disables all `ContactDamage` components on itself/children so corpses stop hurting.
  - Disables itself (the `Enemy` script) to stop further behavior.

### `Assets/scripts/Enemy/EnemyController.cs`
Movement executor (patrol/chase) + death stop.
- Uses Rigidbody2D velocity in `FixedUpdate`.
- Respects knockback:
  - If it has a `KnockbackReceiver` and `IsKnockbackActive` is true, it will not set velocity that tick.
- Flipping:
  - Uses `SpriteRenderer.flipX` to avoid physics snapping from scaling the rigidbody root.
  - Mirrors the contact sensor child offset (if `ContactDamage` is on an offset child).
- Supports two chase stop modes:
  - Stop at a distance (transform-distance on X), **or**
  - Stop only when the `ContactDamage` sensor collider overlaps the player collider.

### `Assets/scripts/Enemy/EnemyBrain.cs`
Temporary decision-maker (pre-GOAP).
- Disables itself automatically if a `GoapActionProvider` exists on the same enemy (prevents double-driving).

---

## UI

### `Assets/scripts/UI/PlayerHUD.cs`
Simple health + stamina bars.
- References:
  - `Health` and `Stamina` (auto-found if not assigned).
  - Each bar can be either an `Image` (filled) or a `Slider`.
- Subscribes/unsubscribes cleanly in `OnEnable`/`OnDisable`.

---

## Input Actions (New Input System, Send Messages)

Expected Input Action names:
- `Attack` → calls `player.OnAttack(InputValue)`
- `Sprint` → calls `player.OnSprint(InputValue)` (Dash)
- `Interact` → calls `player.OnInteract(InputValue)` (Life Drain)
- `Jump` → calls `player.OnJump(InputValue)`
- `Move` → calls `player.OnMove(InputValue)`

If your action names differ, either rename the actions or rename the methods to match the Send Messages convention.

---

## Unity Inspector Wiring Checklist

### Player GameObject
Must have:
- `player`, `Rigidbody2D`, `Animator`, `PlayerInput (Send Messages)`
- `Health`, `Stamina`, `KnockbackReceiver`
- `Combat` (on root or on a child if animation events will be on the child)

Assign:
- Grounding: `groundCheck`, `groundLayer`
- Drain: `drainCheckPoint` (near feet), `drainableLayer`
- Combat: `attackPoint`, `damageableLayer`
- Animator params: optional string fields on `player`/`Enemy` if you use triggers/bools.

### Enemy Prefab/GameObject
Must have:
- `Health`, `Enemy`, `EnemyController`, `KnockbackReceiver`
- `ContactDamage` on a child **DamageSensor** object with a trigger collider

Layer/collision:
- If you want no physical pushing:
  - Disable Enemy↔Player body collision in the matrix.
  - Put the DamageSensor on a layer that DOES overlap the Player layer (for triggers).

---

## GOAP Notes (if/when enabled)

The project includes the CrashKonijn GOAP package dependency in `Packages/manifest.json`:
- `com.crashkonijn.goap` (Git URL, version pinned)

Current GOAP scripts (if present in your project) live under:
- `Assets/Scripts/GOAP/...`

GOAP should replace decision-making only. `EnemyController` remains the executor that GOAP actions call.

---

## Suggested Next Steps
- Add enemy “attack” actions (not contact damage) as separate GOAP actions later.
- Replace `EnemyBrain` completely with GOAP agent goal selection once your GOAP configs/AgentTypes are set up.
- Add visual feedback: i-frame blink, hit flash, drain VFX, etc. (no gameplay changes).

