# Enemy Awareness

## Purpose
`EnemyAwareness` controls whether an enemy is allowed to run regular behavior. It handles hiding, waking, returning to sleep, and asleep states.

This is not GOAP. It is an enemy gameplay gate that GOAP actions and movement code must respect.

## Main Files
- `Assets/scripts/Enemy/EnemyAwareness.cs`
- `Assets/scripts/Enemy/EnemyAnimationDriver.cs`
- `Assets/scripts/Enemy/EnemyAnimationEventRelay.cs`
- `Assets/scripts/Enemy/EnemyController.cs`

## Runtime Flow
1. Enemy starts in an awareness state, currently serialized as `Hiding` by default.
2. While not active, `EnemyAwareness` stops movement through `EnemyController`.
3. `WakeAndReady()` moves a hiding enemy into `Waking`, starts the popping animation, and returns false until waking is complete.
4. Animation events call `OnPopAnimationFinished()` or `OnUnpopAnimationFinished()`.
5. Once active, `CanRunRegularBehavior` is true and movement/GOAP behavior can proceed.

## States
- `Asleep` - cannot wake through `WakeAndReady()`.
- `Hiding` - hidden but can begin waking.
- `Waking` - waiting for pop animation or fallback timer.
- `ReturningToSleep` - playing unpop animation, then returns to hiding.
- `Active` - regular behavior can run.

## Inspector Wiring
- Optional `Animator`; auto-found in children if missing.
- Optional `EnemyAnimationDriver`; auto-added if missing.
- Optional `EnemyController`; auto-found on the same object.
- Animation bool params default to `isPopping` and `isUnpopping`.

## Important Rules
- GOAP actions should stop if `EnemyAwareness.CanRunRegularBehavior` is false.
- Awareness should stop movement while waking, hiding, sleeping, or returning to sleep.
- Animation events are preferred, but `wakeFallbackSeconds` prevents waking from getting stuck if an event is missing.

## Known Issues
- This system is currently not documented in the older broad context files.
- If pop/unpop animation events are missing, behavior depends on fallback timing and may not visually sync perfectly.

## Related Docs
- `EnemyController.md`
- `../AI/GOAPOverview.md`

