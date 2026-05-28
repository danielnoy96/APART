# Skill: APART Unity Animation System

Use this skill when working on APART's Unity animation wiring, especially player or enemy 2D sprite animations, Animator parameters, Animation Events, or any future animation-system refactor.

## Purpose

APART currently uses Unity-native animation assets:

- Frames are authored and tuned in Unity `.anim` clips.
- Timing-sensitive calls are placed as Unity Animation Events.
- Animator Controllers decide clip transitions from parameters.
- Gameplay is driven by C# state machines and components.

The long-term goal is not to hide animation frames in code. The goal is to keep the Unity Animation window as the place where frames, timing, and events are tuned, while cleaning the code so animation requests are centralized and easy to reason about.

## Core Design Principle

Gameplay code should decide the animation intent:

```text
Idle, Run, JumpRise, Fall, Dash, Attack, LifeDrain, Hit
```

Animation code should decide how that intent is applied to Unity's `Animator`.

The player follows that split through `PlayerAnimationDriver`. Enemy animation follows the same split through `EnemyAnimationDriver`.

## Current System Map

### Player Owner

Main file:

```text
C:\Users\USER\APART\Assets\scripts\player.cs
```

The `player` component currently owns:

- `public Animator anim`
- `PlayerAnimationDriver animationDriver`
- FSM state instances:
  - `PlayerIdleState`
  - `PlayerMoveState`
  - `PlayerJumpState`
  - `PlayerAttackState`
  - `PlayerDashState`
  - `PlayerLifeDrainState`
- Input latch flags:
  - `attackPressed`
  - `dashPressed`
  - `lifeDrainPressed`
  - `lifeDrainHeld`
- Animation parameter string fields:
  - `dashBoolParam = "isDashing"`
  - `hitBoolParam = "isHit"`
  - `attackBoolParam = "isAttacking"`
  - `lifeDrainBoolParam = "isLifeDraining"`

`player.FixedUpdate()` calls:

```text
CheckGrounded()
RefreshLifeDrainHeldInput()
currentState.FixedUpdate()
SyncAnimationState()
```

`player.Update()` calls:

```text
RefreshLifeDrainHeldInput()
currentState.Update()
hit/invincibility timer updates
SyncAnimationState()
```

`SyncAnimationState()` delegates locomotion and hit animation state to `PlayerAnimationDriver`; `player.cs` is the only player code that owns continuous locomotion/hit sync.

### Player State Flow

Current priority in idle and move states:

```text
Dash > LifeDrain > Attack > Jump > Move > Idle
```

Attack does not currently allow dash cancellation. Dash has its own duration, ease-out, and optional end lag. Life Drain locks the player in place while draining.

## Current Animator Parameters

Current player Animator Controller:

```text
C:\Users\USER\APART\Assets\sprites\animations\character animations\sprite.controller
```

Controller cleanup status:

- The controller keeps one live Base Layer `attack` state and one live Base Layer `dash` state.
- Any State transitions for `isAttacking` and `isDashing` target those live Base Layer states.
- Old orphan duplicate `attack`/`dash` states and their unused transitions were removed during Pass 3.

Known parameters in this controller:

| Parameter | Type | Current writer |
| --- | --- | --- |
| `isIdle` | Bool | `PlayerAnimationDriver.SetLocomotion(...)` |
| `isRunning` | Bool | `PlayerAnimationDriver.SetLocomotion(...)` |
| `isGrounded` | Bool | `PlayerAnimationDriver.SetLocomotion(...)` |
| `isJumping` | Bool | `PlayerAnimationDriver.SetLocomotion(...)` |
| `yVelocity` | Float | `PlayerAnimationDriver.SetLocomotion(...)` |
| `isDashing` | Bool | `PlayerAnimationDriver.PlayTimedAction(PlayerAnim.Dash, ...)` |
| `isAttacking` | Bool | `PlayerAnimationDriver.PlayTimedAction(PlayerAnim.Attack, ...)` |
| `isLifeDraining` | Bool | `PlayerAnimationDriver.SetAction(PlayerAnim.LifeDrain, ...)` |
| `isHit` | Bool | `PlayerAnimationDriver.SetHit(...)` |

## Current Player Animation Writers

### `PlayerAnimationDriver.cs`

File:

```text
C:\Users\USER\APART\Assets\scripts\PlayerAnimationDriver.cs
```

This is the only player-side script that should call `Animator.SetBool(...)` or `Animator.SetFloat(...)`.

Current driver API:

- `Initialize(Animator animator)`
- `ConfigureParams(string dash, string attack, string lifeDrain, string hit)`
- `SetLocomotion(bool isIdle, bool isRunning, bool isGrounded, float yVelocity)`
- `SetHit(bool active)`
- `SetHit(bool active, float normalizedTime)`
- `SetAction(PlayerAnim anim, bool active)`
- `PlayTimedAction(PlayerAnim anim, float seconds)`
- `ResetAll()`

Timed action holds are stored per `PlayerAnim`, so attack and dash no longer cancel each other's pending clear.

### Player State Usage

Player states now request animation intent through `player.AnimationDriver`:

- `PlayerIdleState`, `PlayerMoveState`, and `PlayerJumpState` do not call the animation driver.
- `PlayerAttackState` calls `PlayTimedAction(PlayerAnim.Attack, player.attackAnimHoldSeconds)`.
- `PlayerDashState` calls `PlayTimedAction(PlayerAnim.Dash, player.dashAnimHoldSeconds)`.
- `PlayerLifeDrainState` calls `SetAction(PlayerAnim.LifeDrain, true/false)`.

States should not call `Animator` directly. Locomotion and hit animation sync are centralized in `player.SyncAnimationState()`.

### `PlayerAttackState.cs`

Current animation behavior:

- On enter, spends stamina.
- Calls `player.AnimationDriver.PlayTimedAction(PlayerAnim.Attack, player.attackAnimHoldSeconds)`.
- Stops horizontal velocity.
- Calls `player.combat.BeginAttack()`.
- Does not clear `isAttacking` in `Exit()`.
- Ends through `PlayerAnimationEventRelay.AttackFinished()` if the Animation Event is wired.
- Has a defensive fallback: if animation events are missing, it exits when `Combat.CanAttack` becomes true after cooldown.

### `PlayerDashState.cs`

Current animation behavior:

- On enter, spends stamina.
- Calls `player.AnimationDriver.PlayTimedAction(PlayerAnim.Dash, player.dashAnimHoldSeconds)`.
- Starts dash cooldown.
- Applies dash velocity by timer.
- Does not clear `isDashing` in `Exit()`.
- Dash physics are code-owned, not animation-event-owned.

### `PlayerLifeDrainState.cs`

Current animation behavior:

- On enter, calls `player.AnimationDriver.SetAction(PlayerAnim.LifeDrain, true)`.
- On exit, calls `player.AnimationDriver.SetAction(PlayerAnim.LifeDrain, false)`.
- Drain duration comes from `DrainableCorpse.DrainDuration`, not from the animation clip.
- Life Drain currently has no required animation event.

## Current Animation Events

Animation Events are currently used for precise timing and completion callbacks.

### Player Attack Events

Primary relay file:

```text
C:\Users\USER\APART\Assets\scripts\PlayerAnimationEventRelay.cs
```

Backing combat file:

```text
C:\Users\USER\APART\Assets\scripts\Combat\Combat.cs
```

Expected attack clip events:

| Event function | Purpose |
| --- | --- |
| `PlayerAnimationEventRelay.AttackHit()` | Called on the exact hit frame. Routes to `Combat.PerformHitCheck()`. |
| `PlayerAnimationEventRelay.AttackFinished()` | Called near the end of the attack clip. Routes to `player.OnAttackAnimationFinished()`. |

Unity calls Animation Events on components attached to the same GameObject as the `Animator` playing the clip. The player auto-wiring therefore ensures `PlayerAnimationEventRelay` exists on the Animator GameObject, even when the Animator is on a child object such as `sprite`.

Legacy clip events still supported:

- `Combat.PerformHitCheck()`
- `Combat.AttackAnimationFinished()`

Current `attack.anim` status:

- The clip has serialized Animation Events for `AttackHit` and `AttackFinished`.
- `AttackHit` is placed on the middle strike frame.
- `AttackFinished` is placed on the last visible frame.
- When retuning in Unity's Animation window, keep these events on `PlayerAnimationEventRelay`.

Fallback:

- `Combat.debugInstantAttack` can perform hit and finish from code for early testing.
- `PlayerAttackState.Update()` exits when cooldown has elapsed if the finished event is not wired.

### Enemy Awareness Events

Files:

```text
C:\Users\USER\APART\Assets\scripts\Enemy\EnemyAwareness.cs
C:\Users\USER\APART\Assets\scripts\Enemy\EnemyAnimationEventRelay.cs
```

Expected enemy pop/unpop clip events:

| Event function | Purpose |
| --- | --- |
| `EnemyAnimationEventRelay.PopAnimationFinished()` | Routes to `EnemyAwareness.OnPopAnimationFinished()` |
| `EnemyAnimationEventRelay.UnpopAnimationFinished()` | Routes to `EnemyAwareness.OnUnpopAnimationFinished()` |

Known clips with these events:

```text
C:\Users\USER\APART\Assets\sprites\animations\character animations\pop.anim
C:\Users\USER\APART\Assets\sprites\animations\character animations\unpop.anim
```

## Current Enemy Animation Wiring

### `EnemyAnimationDriver.cs`

File:

```text
C:\Users\USER\APART\Assets\scripts\Enemy\EnemyAnimationDriver.cs
```

This is the only enemy-side script that should call `Animator.SetBool(...)`, `Animator.SetFloat(...)`, `Animator.SetTrigger(...)`, `Animator.ResetTrigger(...)`, or `Animator.Play(...)`.

Current driver API:

- `Initialize(Animator targetAnimator)`
- `ConfigureMovement(string moveBool, string speedFloat)`
- `ConfigureDamage(string hurtTrigger, string deathTrigger, string deathState)`
- `ConfigureAwareness(string popBool, string unpopBool)`
- `SetMovement(float speedAbs)`
- `PlayHurt()`
- `PlayDeath()`
- `SetPopping(bool active)`
- `SetUnpopping(bool active)`
- `ResetAll()`

The existing enemy scripts still own gameplay state, damage, death, and awareness decisions. They configure/request animation through the driver instead of writing Animator parameters directly.

### `EnemyController.cs`

File:

```text
C:\Users\USER\APART\Assets\scripts\Enemy\EnemyController.cs
```

Optional movement Animator params:

- `moveBoolParam`
- `speedFloatParam`

Current animation behavior:

- Computes horizontal speed from `Rigidbody2D.linearVelocity.x`.
- Calls `EnemyAnimationDriver.SetMovement(speedAbs)`.
- The driver writes optional movement bool/float params. Empty or whitespace strings mean no write.

### `Enemy.cs`

File:

```text
C:\Users\USER\APART\Assets\scripts\Enemy\Enemy.cs
```

Optional damage/death Animator fields:

- `hurtTriggerParam`
- `deathTriggerParam`
- `deathStateName = "small enemy dead"`
- `corpseTransitionDelay`

Current animation behavior:

- Calls `EnemyAnimationDriver.PlayHurt()` when damaged and still alive.
- Calls `EnemyAnimationDriver.PlayDeath()` on death.
- Death animation is used as a visual transition before the object becomes a drainable corpse.
- Corpse finalization is delayed by `corpseTransitionDelay` when the driver reports that a death animation was played.

### `EnemyAwareness.cs`

File:

```text
C:\Users\USER\APART\Assets\scripts\Enemy\EnemyAwareness.cs
```

Optional pop/unpop Animator bool params:

- `popBoolParam = "isPopping"`
- `unpopBoolParam = "isUnpopping"`

Current animation behavior:

- Calls `EnemyAnimationDriver.SetPopping(value)`.
- Calls `EnemyAnimationDriver.SetUnpopping(value)`.
- Pop has both an Animation Event path and a fallback timer through `wakeFallbackSeconds`.

## Known Problems And Refactor Hazards

### Animator Writes Are Scattered

Player Animator parameter writes are now centralized in:

- `PlayerAnimationDriver.cs`

Player gameplay states still decide when to request animation intent. `PlayerAnimationEventRelay` receives player clip events and routes combat timing internally.

Enemy Animator parameter writes are now centralized in:

- `EnemyAnimationDriver.cs`

Enemy gameplay, death, awareness, and event relay behavior is still spread across:

- `EnemyController.cs`
- `Enemy.cs`
- `EnemyAwareness.cs`
- `EnemyAnimationEventRelay.cs`

When refactoring enemy animation, search all of these before removing parameters, changing controller transitions, or renaming Animation Events.

### There Are Two State Machines

The player has:

- A C# gameplay FSM.
- A Unity Animator Controller graph.

Both currently encode pieces of movement/action state. This can make bugs hard to reason about because gameplay state and visual state can disagree.

### Timed Action Holds Are Visual Holds

Attack and dash bools are still timed visual holds:

- `PlayerAttackState.Exit()` does not clear `isAttacking`.
- `PlayerDashState.Exit()` does not clear `isDashing`.
- `PlayerAnimationDriver` clears them after their configured hold seconds.

This preserves the current visual behavior while avoiding the old shared-coroutine bug.

### Timed Holds Can Drift From Gameplay

Current values:

- `attackAnimHoldSeconds`
- `dashAnimHoldSeconds`
- `hitAnimHoldSeconds`

These can make a clip keep playing after gameplay moved on, or stop before the visual action should finish. They also require constant retuning when clips change.

### Animation Events Should Not Be The Only Safety Net

Animation Events are good for exact frames, but missing events should not permanently lock gameplay. Current attack already has a fallback. Future actions that depend on finish events should also have a code fallback.

## Current Target Architecture

The target architecture should keep Unity-native authoring:

- Keep `.anim` clips.
- Keep the Animation window as the place to edit sprite frames.
- Keep Animation Events visible on the clip timeline.
- Keep Animator Controller assets editable in Unity.

The current player code shape is:

```text
Player FSM -> PlayerAnimationDriver -> Animator Controller / Animation Clips
```

The current enemy code shape is:

```text
Enemy gameplay components -> EnemyAnimationDriver -> Animator Controller / Animation Clips
```

### `PlayerAnimationDriver`

`PlayerAnimationDriver` is the only player script that should talk directly to `Animator`.

Suggested public intent API:

```csharp
public enum PlayerAnim
{
    Idle,
    Run,
    JumpRise,
    Fall,
    Dash,
    Attack,
    LifeDrain,
    Hit,
    Death
}
```

Current driver responsibilities:

- Own the `Animator` reference.
- Own all Animator parameter hashes.
- Expose semantic methods such as:
  - `SetLocomotion(bool isIdle, bool isRunning, bool isGrounded, float yVelocity)`
  - `PlayTimedAction(PlayerAnim anim, float seconds)`
  - `SetAction(PlayerAnim anim, bool active)`
  - `SetHit(bool active)`
  - `SetHit(bool active, float normalizedTime)`
- Keep independent timed action holds per `PlayerAnim`.
- Reset all visual bools on respawn.
- Expose optional inspector debug state for current locomotion, last action request, timed-action activity, hit activity, and Y velocity.
- Scrub the `hit` animation state from normalized hit/knockback progress so the first hit sprite holds for the first half and the second hit sprite holds for the second half.

### What Gameplay States Should Do

Player states should stop calling `Animator` directly.

Instead of:

```csharp
Anim.SetBool("isRunning", true);
player.anim.SetBool(player.attackBoolParam, true);
player.anim.SetBool(player.lifeDrainBoolParam, true);
```

Action states should request action intent:

```csharp
player.AnimationDriver.PlayTimedAction(PlayerAnim.Attack, player.attackAnimHoldSeconds);
player.AnimationDriver.SetAction(PlayerAnim.LifeDrain, true);
```

The rule is: `player.cs` owns continuous locomotion/hit sync, action states request action animation intent, and only the driver writes Animator parameters.

### `EnemyAnimationDriver`

`EnemyAnimationDriver` is the only enemy script that should talk directly to `Animator`.

Current driver responsibilities:

- Own the enemy `Animator` reference.
- Own movement, hurt, death, pop, and unpop parameter hashes.
- Preserve existing optional inspector string params.
- Apply movement visuals from speed.
- Apply hurt/death triggers and immediate death-state override.
- Apply awareness pop/unpop bools.
- Expose optional inspector debug state for last request, movement speed, moving state, and awareness pop/unpop bools.

Enemy scripts should keep owning gameplay decisions:

- `EnemyController` owns patrol/chase/dead movement state.
- `Enemy` owns health, VFX, death, and corpse finalization.
- `EnemyAwareness` owns hiding/waking/active state and fallback timing.
- `EnemyAnimationEventRelay` owns clip event routing.

### Metroidvania Animation Rules

Use Animation Events only where a specific frame matters:

- `AttackHit`
- `AttackFinished`
- `LifeDrainApply` if the heal should happen on a visible frame
- `HitReactFinished` if hit stun should follow the clip
- `PopAnimationFinished`
- `UnpopAnimationFinished`

Do not add start events just because an action starts:

- Attack starts in `PlayerAttackState.Enter()`.
- Dash starts in `PlayerDashState.Enter()`.
- Life Drain starts in `PlayerLifeDrainState.Enter()`.

Dash movement should remain code-owned unless there is a deliberate design decision to make dash anticipation or recovery animation-owned.

## Migration Checklist For Future Animation Work

When changing the player animation system, keep this order.

1. Preserve Unity `.anim` clips and Animation Events unless the requested change explicitly says otherwise.
2. Add or change player animation parameters inside `PlayerAnimationDriver`, not inside gameplay states.
3. Route new player state animation requests through semantic `PlayerAnim` values or driver methods.
4. Keep attack hit timing in `PlayerAnimationEventRelay.AttackHit()` unless replacing the event contract deliberately.
5. Recheck the Animator Controller for transitions that depend on renamed or deleted parameters.
6. Recheck clips for Animation Events that call removed or renamed methods.
7. Keep fallbacks for finished events that can affect player control.

When changing the enemy animation system:

1. Preserve existing enemy clips and event function names unless the requested change explicitly says otherwise.
2. Add or change enemy animation parameters inside `EnemyAnimationDriver`, not inside `EnemyController`, `Enemy`, or `EnemyAwareness`.
3. Keep `PopAnimationFinished()` and `UnpopAnimationFinished()` clip events compatible unless the clips are migrated in the same change.
4. Keep corpse finalization controlled by `Enemy.cs`; death animation is visual transition timing, not ownership of corpse behavior.

## Refactor Search Commands

Before and after any animation refactor, search:

```powershell
rg -n "SetBool|SetTrigger|SetFloat|SetInteger|ResetTrigger|Animator.Play|CrossFade|HoldAnimatorBool" Assets/scripts -g "*.cs"
```

Expected direct Animator writers:

```text
Assets/scripts/PlayerAnimationDriver.cs
Assets/scripts/Enemy/EnemyAnimationDriver.cs
```

Search event methods:

```powershell
rg -n "AttackHit|AttackFinished|AnimationFinished|PerformHitCheck|PopAnimationFinished|UnpopAnimationFinished" Assets/scripts -g "*.cs"
```

Search Animator assets:

```powershell
rg -n "isIdle|isRunning|isGrounded|isJumping|yVelocity|isDashing|isAttacking|isLifeDraining|isHit|isMoving|isPopping|isUnpopping|Hurt|Death|functionName:" "Assets\sprites\animations\character animations" -g "*.controller" -g "*.anim"
```

Expected current clip events:

```text
attack.anim -> AttackHit, AttackFinished
pop.anim -> PopAnimationFinished
unpop.anim -> UnpopAnimationFinished
```

The refactor is not done until old direct Animator writes are either removed or intentionally documented as still owned by a specific driver/component.

## Acceptance Checklist For Future Animation Work

A future animation change is clean only if:

- Unity `.anim` clips remain editable in the Animation window.
- Frame timing is not hidden in code unless it is gameplay timing, not art timing.
- Player states do not directly call `SetBool`, `SetFloat`, `SetTrigger`, `Animator.Play`, or `CrossFade`.
- There is a single owner for player Animator writes.
- Enemy behavior scripts do not directly call `SetBool`, `SetFloat`, `SetTrigger`, `ResetTrigger`, `Animator.Play`, or `CrossFade`.
- There is a single owner for enemy Animator writes.
- Attack hit timing is visible as an Animation Event or has a clearly documented replacement.
- Missing finished events cannot permanently lock player control.
- Removed parameters are removed from both code and Animator Controller transitions.
- Removed event methods are removed from clips or replaced with compatible relay methods.
- Enemy animation wiring is checked separately from player animation wiring.
