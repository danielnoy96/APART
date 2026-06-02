# APART Unity Project - Working Context

> Purpose: repo-local current snapshot for future work. This file is the high-level map; detailed system notes live under `Docs/`.
>
> Last updated: 2026-05-31

## Read Order
- `AGENTS.md` - project rules for AI agents.
- `Docs/SYSTEMS_INDEX.md` - system map and focused doc links.
- This file - current high-level architecture and source-of-truth summary.
- Focused docs in `Docs/` before editing a specific system.
- `GOAP_CRASHKONIJN_WORKING_NOTES.md` only by targeted section search for GOAP details.

## Source Of Truth
- Code lives under `Assets/scripts/**` and is the source of truth when older docs disagree.
- Main scene: `Assets/Scenes/SampleScene.unity`.
- GOAP assets:
  - `Assets/scripts/GOAP/Game.GOAP.asset`
  - `Assets/scripts/GOAP/Config/CapabilityConfigScriptable.asset`
  - `Assets/scripts/GOAP/Config/AgentTypeScriptable.asset`
- Package status in `Packages/manifest.json`:
  - `com.crashkonijn.goap` is pinned to `https://github.com/crashkonijn/GOAP.git?path=/Package#3.1.2`
  - Unity Input System is present (`com.unity.inputsystem`).
  - 2D animation/sprite/tilemap packages are present.

## Core Architecture
- This is a 2D Unity project using Rigidbody2D movement and component-based combat.
- Player logic is owned by lowercase `player` plus plain C# `PlayerState` subclasses.
- Enemy movement execution is owned by `EnemyController`.
- GOAP is CrashKonijn GOAP v3.1.2 and is used for enemy planning/orchestration only.
- GOAP actions call real gameplay methods on `EnemyController`; GOAP does not own physics movement.
- `EnemyAwareness` is not GOAP. It is a wake/hide/asleep gameplay gate that both GOAP and legacy enemy logic must respect.
- Dead enemies remain in the scene as drainable corpses; do not destroy them by default.

## Player System
Main files:
- `Assets/scripts/player.cs`
- `Assets/scripts/PlayerState.cs`
- `Assets/scripts/PlayerIdleState.cs`
- `Assets/scripts/PlayerMoveState.cs`
- `Assets/scripts/PlayerJumpState.cs`
- `Assets/scripts/PlayerAttackState.cs`
- `Assets/scripts/PlayerDashState.cs`
- `Assets/scripts/PlayerLifeDrainState.cs`
- `Assets/scripts/PlayerAnimationDriver.cs`
- `Assets/scripts/PlayerAnimationEventRelay.cs`

Current behavior:
- Player uses a plain C# FSM, not MonoBehaviour states.
- Input uses New Input System `PlayerInput` with Send Messages callbacks:
  - `Move` -> `OnMove`
  - `Jump` -> `OnJump`
  - `Attack` -> `OnAttack`
  - `Sprint` -> `OnSprint` and is currently dash
  - `Interact` -> `OnInteract` and is currently life drain
- Inputs such as attack, dash, and life-drain press are one-frame flags cleared in `LateUpdate`.
- Jump uses coyote time, jump buffering, jump cut, variable gravity, apex hang, and optional vertical speed clamps.
- Jump, attack, dash, and life drain all interact with stamina.
- Player damage should go through `player.TryTakeDamage` to preserve invincibility and hit animation.
- Respawn calls `player.ResetForRespawn`, clears velocity/input/FSM damage state, resets animation, resets combo, and returns to idle.
- Player animation is centralized through `PlayerAnimationDriver`; attack animation events should use `PlayerAnimationEventRelay.AttackHit` and `AttackFinished`.
- Attack state has a fallback exit when `Combat.CanAttack` becomes true, so missing animation events do not permanently lock controls.

Player setup expectations:
- Root should have `player`, `Rigidbody2D`, `PlayerInput`, `Health`, `Stamina`, `Combat`, and usually `KnockbackReceiver`.
- Assign `groundCheck`, `groundLayer`, `drainCheckPoint`, `drainCheckRadius`, and `drainableLayer`.
- Default animation bool params include `isDashing`, `isAttacking`, `isLifeDraining`, and `isHit`.

## Combat And Shared Gameplay Components
Main files:
- `Assets/scripts/Combat/Health.cs`
- `Assets/scripts/Combat/Stamina.cs`
- `Assets/scripts/Combat/Combat.cs`
- `Assets/scripts/Combat/ContactDamage.cs`
- `Assets/scripts/Combat/KnockbackReceiver.cs`
- `Assets/scripts/Combat/DrainableCorpse.cs`
- `Assets/scripts/Combat/PlayerCombo.cs`
- `Assets/scripts/Combat/StaminaDebug.cs`

Current behavior:
- `Health` owns HP, death state, and damage/heal/death events.
- `Stamina` owns spend, restore, regeneration delay, and changed/empty/full events.
- `Combat` owns player melee overlap hit checks and attack cooldown.
- `Combat.PerformHitCheck` damages each `Health` only once per attack and applies knockback through the target's preferred `KnockbackReceiver`.
- `ContactDamage` is reusable contact damage. For player targets it calls `player.TryTakeDamage`; for non-player targets it falls back to `Health.TakeDamage`.
- `KnockbackReceiver` writes Rigidbody2D velocity and exposes `IsKnockbackActive`; player movement and enemy movement must not override knockback while active.
- `DrainableCorpse` stores corpse heal value, drain duration, render-behind behavior, and optional destroy-after-drain behavior.
- `PlayerCombo` listens to successful hit checks and restores stamina after a configurable successful-hit chain.

## Enemy System
Main files:
- `Assets/scripts/Enemy/Enemy.cs`
- `Assets/scripts/Enemy/EnemyController.cs`
- `Assets/scripts/Enemy/EnemyBrain.cs`
- `Assets/scripts/Enemy/EnemyAwareness.cs`
- `Assets/scripts/Enemy/EnemyAnimationDriver.cs`
- `Assets/scripts/Enemy/EnemyAnimationEventRelay.cs`

Current behavior:
- `EnemyController` is the movement executor for patrol, chase, stop, facing, contact sensor mirroring, jumping, obstacle checks, player-above checks, and death movement shutdown.
- `EnemyController` respects `KnockbackReceiver.IsKnockbackActive` and avoids overwriting velocity during knockback.
- `EnemyController` flips `SpriteRenderer.flipX` when available instead of root scale, and mirrors the contact damage sensor local X offset.
- Patrol supports assigned patrol points or fallback patrol around spawn position.
- Chase supports stopping by distance or by actual contact sensor overlap.
- Enemy jump behavior includes grounded checks, jump cooldown, player-above detection, obstacle-ahead raycast, and player-above reaction delay.
- `Enemy` owns hurt/death presentation, hit/death particles, corpse finalization, and ensuring a `DrainableCorpse` exists on death.
- `EnemyAwareness` states are `Asleep`, `Hiding`, `Waking`, `ReturningToSleep`, and `Active`.
- `EnemyAwareness.CanRunRegularBehavior` gates regular behavior. GOAP actions stop movement and stop running when awareness is not active.
- `EnemyBrain` is legacy fallback logic. It disables itself when GOAP components are present.

Enemy prefab expectations:
- Root should have `Health`, `Enemy`, `EnemyController`, and relevant animation/controller components.
- Contact damage should usually be on a child trigger sensor with `ContactDamage` targeting the Player layer.
- Jumping enemies need `groundCheck`, `groundLayer`, `wallCheck`, and `obstacleLayer` configured.
- If using awareness, animation events should call `EnemyAnimationEventRelay.PopAnimationFinished` and `UnpopAnimationFinished`.

## GOAP System
Main files:
- `Assets/scripts/GOAP/EnemyGoapAgentBridge.cs`
- `Assets/scripts/GOAP/EnemyDecisionModuleBase.cs`
- `Assets/scripts/GOAP/DistanceGoalSelector.cs`
- `Assets/scripts/GOAP/GoapRunnerResolver.cs`
- `Assets/scripts/GOAP/Goals/PatrolGoal.cs`
- `Assets/scripts/GOAP/Goals/ChasePlayerGoal.cs`
- `Assets/scripts/GOAP/Actions/PatrolAction.cs`
- `Assets/scripts/GOAP/Actions/ChasePlayerAction.cs`
- `Assets/scripts/GOAP/Actions/JumpToPlayerAction.cs`
- `Assets/scripts/GOAP/Actions/JumpObstacleAction.cs`
- `Assets/scripts/GOAP/Sensors/PlayerDistanceSensor.cs`
- `Assets/scripts/GOAP/Sensors/HasPlayerTargetSensor.cs`
- `Assets/scripts/GOAP/Sensors/PlayerAboveSensor.cs`
- `Assets/scripts/GOAP/Sensors/ObstacleAheadSensor.cs`
- `Assets/scripts/GOAP/Targets/PlayerTarget.cs`
- `Assets/scripts/GOAP/Targets/PlayerTargetResolver.cs`
- `Assets/scripts/GOAP/WorldKeys/*.cs`

Current GOAP behavior:
- Goals:
  - `PatrolGoal`
  - `ChasePlayerGoal`
- Actions:
  - `PatrolAction` -> calls `EnemyController.Patrol()`
  - `ChasePlayerAction` -> calls `EnemyController.ChasePlayer()`
  - `JumpToPlayerAction` -> jumps when the player is above, then continues chasing until landing or timeout
  - `JumpObstacleAction` -> jumps during chase when `ObstacleAheadSensor` reports a blocker, then continues chasing until landing or timeout
- Sensors:
  - `PlayerDistanceSensor` writes whether the player is in bridge detection range.
  - `HasPlayerTargetSensor` writes whether a player target exists.
  - `PlayerAboveSensor` reads `EnemyController.IsPlayerAboveReadyToJump()`.
  - `ObstacleAheadSensor` reads `EnemyController.IsObstacleAhead()`.
- Targets:
  - `PlayerTargetResolver` returns a `TransformTarget` for the current player.
- World keys include target, distance, player-above, obstacle-ahead, patrolling, and chasing keys.

GOAP bridge behavior:
- `EnemyGoapAgentBridge` requires `GoapActionProvider`, `CrashKonijn.Agent.Runtime.AgentBehaviour`, and `EnemyAwareness`.
- The bridge resolves `AgentTypeBehaviour`, receiver wiring, decision module, controller, awareness, health, and player.
- The bridge disables legacy `EnemyBrain` when GOAP is present.
- On death it calls `EnemyController.SetDead()` and disables `GoapActionProvider`.
- It hooks GOAP resolve/no-action/goal events for debug logging.
- If `OnNoActionFound` fires, it clears the last requested goal and falls back to direct patrol/chase execution so enemies do not freeze while config is being iterated.
- `DistanceGoalSelector` currently chooses chase/hide based on horizontal or full distance, optional hysteresis, and awareness wake readiness.
- `GoapRunnerResolver` uses early execution order and reflection to inject the scene `GoapBehaviour` runner into prefab `AgentTypeBehaviour.runner`.

Scene/prefab wiring:
- Scene needs a GOAP GameObject with `GoapBehaviour` and a controller such as `ReactiveControllerBehaviour`.
- GOAP enemy needs `GoapRunnerResolver`, `AgentTypeBehaviour`, `GoapActionProvider`, `CrashKonijn.Agent.Runtime.AgentBehaviour`, `EnemyGoapAgentBridge`, and a decision module such as `DistanceGoalSelector`.
- Prefabs should leave runner references empty and rely on `GoapRunnerResolver`.
- When changing GOAP classes, keep `[GoapId]` attributes stable and verify generated/config assets.

## Checkpoints, Respawn, And Hazards
Main files:
- `Assets/scripts/Checkpoint.cs`
- `Assets/scripts/CheckpointManager.cs`
- `Assets/scripts/PlayerRespawnController.cs`
- `Assets/scripts/InstantKillHazard.cs`

Current behavior:
- `Checkpoint` can be `Permanent` or `Mini`.
- `CheckpointManager` is a singleton and can auto-create itself if missing.
- `PlayerRespawnController` registers the player starting position as fallback spawn and respawns to permanent checkpoint on death.
- Permanent respawn restores health and stamina fully.
- Mini respawn is used by hazards and does not fully restore health/stamina.
- `InstantKillHazard` can apply damage through `player.TryTakeDamage` and optionally respawn to the mini checkpoint.
- Respawn grace prevents immediate loops from hazards/death events.

## Breakable Crumble Platforms
Main files:
- `Assets/scripts/BreakableTimedPlatform.cs`
- `Assets/scripts/BushCrumbleVFX.cs`
- `Assets/scripts/SpriteShardMeshBuilder.cs`
- `Assets/scripts/WoodCrumbleMaskGenerator.cs`
- `Assets/scripts/WoodCrumbleRegionBuilder.cs`
- `Assets/scripts/WoodCrumbleMaskPreview.cs`

Current behavior:
- Player touch/collision on the configured trigger layer starts a break routine.
- Shards are generated from the original sprite using a procedural mask and mesh chunks.
- Shards are visual-only Rigidbody2D objects without colliders.
- Platform remains collidable during `breakDelay`, then disables colliders/renderers and respawns after `respawnDelay`.
- Linked `BushCrumbleVFX` can run particle and vertical-crumble shader effects.
- Shards are prewarmed by default to reduce runtime spikes.
- `showMaskPreview` can be expensive at high mask resolutions.

## UI And Utilities
Main files:
- `Assets/scripts/UI/PlayerHUD.cs`
- `Assets/scripts/UI/InGameResetButton.cs`
- `Assets/scripts/SpriteMaterialOverride.cs`

Current behavior:
- `PlayerHUD` listens to `Health` and `Stamina` events and updates fill images or sliders.
- `InGameResetButton` auto-creates a reset button after scene load if an active canvas exists and can create an EventSystem if needed.
- Reset reloads the active scene and sets `Time.timeScale = 1`.
- `SpriteMaterialOverride` is an `ExecuteAlways` helper for applying a material to child `SpriteRenderer`s.

## Known Friction Points
- Older docs may still describe GOAP as patrol/chase only; current code also includes jump-to-player and jump-obstacle behavior.
- GOAP ScriptableObject config can drift from code if generated IDs or capability assets are not refreshed.
- GOAP enemies can appear frozen if scene runner, receiver wiring, agent type, sensors, targets, or capability config are missing.
- `EnemyController.IsGrounded()` returns true when `groundCheck` is missing. This preserves basic behavior but can hide prefab setup mistakes.
- Player attack and awareness animation systems have fallback timers, but missing animation events can still cause visual/gameplay timing mismatch.
- `InstantKillHazard` is a historical name; with non-lethal `damageAmount`, it behaves as damage-plus-mini-respawn.
- `InGameResetButton` is development-oriented runtime UI and may not belong in final player-facing UI.
