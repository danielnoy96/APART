# Enemy Controller

## Purpose
`EnemyController` is the enemy movement executor. GOAP or legacy brain code can ask for patrol/chase/jump behavior, but the controller owns Rigidbody2D velocity, facing, stopping distance, contact sensor mirroring, jumping, and death movement shutdown.

## Main Files
- `Assets/scripts/Enemy/EnemyController.cs`
- `Assets/scripts/Enemy/Enemy.cs`
- `Assets/scripts/Combat/ContactDamage.cs`
- `Assets/scripts/Combat/KnockbackReceiver.cs`
- `Assets/scripts/GOAP/Actions/PatrolAction.cs`
- `Assets/scripts/GOAP/Actions/ChasePlayerAction.cs`
- `Assets/scripts/GOAP/Actions/ChargePlayerAction.cs`
- `Assets/scripts/GOAP/Actions/JumpToPlayerAction.cs`
- `Assets/scripts/GOAP/Actions/JumpObstacleAction.cs`

## Runtime Flow
1. `Awake()` resolves Rigidbody2D, Animator, health, contact damage, knockback, sprite renderer, and player references.
2. Default state is patrol.
3. `FixedUpdate()` runs patrol/chase/idle movement unless dead or knockback is active.
4. `Patrol()` moves between valid assigned patrol points when at least two are assigned, otherwise moves around spawn position using `patrolDistance`.
5. Optional patrol pauses can stop patrol physics movement while keeping the patrol-walk animation active. Pauses can be timer-driven or controlled by walk-clip Animation Events.
6. If assigned patrol points exist and the enemy has chased outside their horizontal span, patrol first returns it to the nearest patrol point, then resumes moving between the points.
7. `ChasePlayer()` moves toward the player and may jump if the player is above.
8. `ChargePlayer()` runs a fixed charger cycle: stand still for charge start, lock the player's horizontal direction, charge for the configured duration/distance, stand still for recovery, then wait for charge cooldown before another windup can start.
9. `TryJump()` applies upward velocity when grounded and jump cooldown allows it.
10. `TryJumpToPlayerAbove()` consumes one player-above jump opportunity so the enemy does not keep jumping every cooldown while the same above condition remains true.
11. On death, movement stops, the runtime movement material is cleared from the root collider, contact damage sources are disabled, and the controller disables itself.

## Inspector Wiring
- Required for movement: `Rigidbody2D`.
- Recommended: `Health`, `KnockbackReceiver`, `SpriteRenderer`, `ContactDamage` child sensor.
- Ground jump checks need `groundCheck`, `groundCheckRadius`, and `groundLayer`.
- Obstacle checks need `wallCheck`, `wallCheckDistance`, and `obstacleLayer`.
- Patrol can use `patrolPoints` or fallback to `patrolDistance`. Point arrays must have at least two non-null entries to override the fallback patrol range.
- Assigned patrol point transforms are captured as world-position anchors during `Awake()`. This means marker objects can be children of the enemy for scene organization without the patrol target moving along with the enemy at runtime.
- Enable `usePatrolMovePause` to make normal patrol walking stop and continue on a timer. `patrolMoveDuration` controls how long it walks before stopping; `patrolPauseDuration` controls how long it waits. During this pause, the controller keeps movement animation active so the walk state does not restart. Chase and charge ignore this pause.
- Enable `usePatrolAnimationEvents` to let walk-clip Animation Events control the pause timing instead. Add `PatrolPauseStart` where movement should stop and `PatrolPauseEnd` where movement should resume. This mode overrides the timed patrol pause. `maxPatrolAnimationPauseSeconds` is a safety fallback if the end event is missing.
- Enemy patrol pause Animation Events should call methods on `EnemyAnimationEventRelay`, not directly on `EnemyController`.
- Velocity-driven enemies apply a no-friction runtime material to their root movement collider when no explicit physics material is assigned. This keeps patrol/chase speed from being reduced by default floor friction. The runtime material is removed again on death so corpses use normal contact friction.
- Charger enemies should set `chargeSpeed` above `moveSpeed` and tune `chargeStartDuration`, `chargeDuration`, `chargeDistance`, `chargeRecoveryDuration`, and `chargeCooldownDuration`. Set `chargeDistance <= 0` to use duration only.
- Charger enemies should use a GOAP capability without jump actions.

## Important Rules
- Do not bypass `EnemyController` for movement unless deliberately replacing the movement system.
- Do not overwrite velocity during knockback.
- Prefer flipping `SpriteRenderer.flipX`; root scale flips can affect collider offsets.
- Contact damage sensor local X offset is mirrored when the enemy turns.
- Death keeps the corpse active for life drain; it does not destroy the enemy immediately.
- Player-above jumping should be driven by `IsPlayerAboveReadyToJump()` and executed through `TryJumpToPlayerAbove()`. Obstacle-ahead GOAP checks must not cause raw cooldown-based jumps by themselves.

## Known Issues
- `IsGrounded()` returns true if `groundCheck` is missing, which avoids breaking basic behavior but can hide setup mistakes.
- Jump behavior depends on correct ground, obstacle, and player collider setup.

## Related Docs
- `EnemyAwareness.md`
- `../AI/GOAPOverview.md`
- `../../COMBAT_SYSTEM_OVERVIEW.md`
