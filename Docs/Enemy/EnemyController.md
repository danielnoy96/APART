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
- `Assets/scripts/GOAP/Actions/JumpToPlayerAction.cs`
- `Assets/scripts/GOAP/Actions/JumpObstacleAction.cs`

## Runtime Flow
1. `Awake()` resolves Rigidbody2D, Animator, health, contact damage, knockback, sprite renderer, and player references.
2. Default state is patrol.
3. `FixedUpdate()` runs patrol/chase/idle movement unless dead or knockback is active.
4. `Patrol()` moves between patrol points when assigned, otherwise moves around spawn position using `patrolDistance`.
5. `ChasePlayer()` moves toward the player and may jump if the player is above.
6. `TryJump()` applies upward velocity when grounded and jump cooldown allows it.
7. On death, movement stops, contact damage sources are disabled, and the controller disables itself.

## Inspector Wiring
- Required for movement: `Rigidbody2D`.
- Recommended: `Health`, `KnockbackReceiver`, `SpriteRenderer`, `ContactDamage` child sensor.
- Ground jump checks need `groundCheck`, `groundCheckRadius`, and `groundLayer`.
- Obstacle checks need `wallCheck`, `wallCheckDistance`, and `obstacleLayer`.
- Patrol can use `patrolPoints` or fallback to `patrolDistance`.

## Important Rules
- Do not bypass `EnemyController` for movement unless deliberately replacing the movement system.
- Do not overwrite velocity during knockback.
- Prefer flipping `SpriteRenderer.flipX`; root scale flips can affect collider offsets.
- Contact damage sensor local X offset is mirrored when the enemy turns.
- Death keeps the corpse active for life drain; it does not destroy the enemy immediately.
- GOAP obstacle jumping should be driven by `IsObstacleAhead()`; player-above jumping should be driven by `IsPlayerAboveReadyToJump()`.

## Known Issues
- `IsGrounded()` returns true if `groundCheck` is missing, which avoids breaking basic behavior but can hide setup mistakes.
- Jump behavior depends on correct ground, obstacle, and player collider setup.

## Related Docs
- `EnemyAwareness.md`
- `../AI/GOAPOverview.md`
- `../../COMBAT_SYSTEM_OVERVIEW.md`
