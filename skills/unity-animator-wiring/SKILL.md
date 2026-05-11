# Skill: Unity Animator ↔ Gameplay Action Wiring (APART)

This skill documents the **current APART project pattern** for connecting gameplay actions (dash/attack/life-drain/etc.) to Unity Animator parameters + Animator Controller transitions, so future prompts can be implemented consistently.

## Scope (this repo)

**Player**
- Gameplay actions are modeled as **FSM states** in `C:\Users\USER\APART\Assets\scripts\Player*State.cs`.
- Input is latched as **one-frame booleans** on `player` in `C:\Users\USER\APART\Assets\scripts\player.cs` (e.g. `attackPressed`, `dashPressed`, `lifeDrainPressed`), then consumed by states.
- Animator parameters currently used by the base controller live in `C:\Users\USER\APART\Assets\sprites\animations\character animations\sprite.controller`:
  - `isIdle` (bool), `isRunning` (bool)
  - `isGrounded` (bool), `isJumping` (bool), `yVelocity` (float)

**Enemies**
- Enemy movement params are optional strings in `C:\Users\USER\APART\Assets\scripts\Enemy\EnemyController.cs` (e.g. `moveBoolParam`, `speedFloatParam`).
- Hurt/death one-shots are optional triggers in `C:\Users\USER\APART\Assets\scripts\Enemy\Enemy.cs`.

## Design rules (project conventions)

1. **Movement/locomotion params** are always safe to set every physics tick.
   - In APART this is done in `player.SyncAnimatorMovementParams()` (FixedUpdate).
2. **Action animations** (dash/attack/life drain) are driven by **state Enter/Exit**.
   - Set animator bool `true` in `Enter()`, and **always** set it back to `false` in `Exit()`.
3. **Parameter names for optional action animations** are configured in the Inspector as strings on the actor component.
   - Player: `attackBoolParam`, `dashBoolParam`, `lifeDrainBoolParam` in `player.cs`.
   - Enemy: `hurtTriggerParam`, `deathTriggerParam` in `Enemy.cs`, plus movement params in `EnemyController.cs`.
4. **If a parameter name is empty/whitespace**, code must do nothing (feature is optional).
5. **APART convention (for now): use bool parameters only** for gameplay/action animations (enter/exit sets/unsets). Do not introduce triggers unless explicitly requested.
6. **Animation Events** are the preferred way to sync “hit frame” and “animation finished”.
   - In APART, `Combat.PerformHitCheck()` and `Combat.AttackAnimationFinished()` are designed to be called from clip events.
   - There is a defensive fallback: `PlayerAttackState` ends automatically when cooldown elapses if events aren’t wired.

## Implementation workflow (do this every time)

### A) Decide the animation contract

For a new action `X` (e.g. “Parry”, “Heal”, “Cast”):
- Use a **bool** parameter `isX` that stays true for the lifetime of `PlayerXState`.
- Decide whether you need:
  - A “finished” callback (animation event) to end the state cleanly.
  - A gameplay timing event (hit frame, spawn projectile, i-frames start/end, etc.).

### B) Update the Animator Controller (Animator window)

Target controller(s):
- Player: `C:\Users\USER\APART\Assets\sprites\animations\character animations\sprite.controller`

Steps:
1. Add parameter(s) (`isX` bool).
2. Add/choose state(s) (e.g. `X`) and assign the clip.
3. Create transitions that match the **current APART controller style**:
   - **Any State → X** when `isX == true`
     - Turn **OFF** “Can Transition To Self” on this transition (otherwise the state can restart every frame and you’ll see only the first frame).
     - Keep transition duration very small (snappy) unless you intentionally want blending.
   - **X → idle** when `isX == false` AND `isIdle == true`
   - **X → running** when `isX == false` AND `isRunning == true`
   - (Optional) If you later want airborne exits, add routes that use `isJumping/isGrounded` and target the Jump sub-state machine; right now the controller is primarily locomotion (idle/running) + a Jump sub-state machine.
4. Verify you do **not** accidentally create duplicate Any State transitions to the same action state.
   - If you see multiple Any State → X transitions, delete extras or make sure all of them have “Can Transition To Self” OFF.

What “locomotion” means in this project:
- `isIdle` and `isRunning` are driven by the active `PlayerIdleState` / `PlayerMoveState` (plus their per-frame updates).
- `isGrounded`, `isJumping`, and `yVelocity` are driven centrally in `player.SyncAnimatorMovementParams()`.

### C) Update code (FSM + animator parameter wiring)

#### Player action state pattern (bool-driven)

1. Ensure there is a state type `PlayerXState : PlayerState` (mirrors `PlayerDashState`, `PlayerAttackState`, `PlayerLifeDrainState`).
2. In `Enter()`:
   - Consume the latched input (`player.xPressed = false`).
   - Set animator bool if configured:
     - `if (player.anim != null && !string.IsNullOrWhiteSpace(player.xBoolParam)) player.anim.SetBool(player.xBoolParam, true);`
3. In `Exit()`:
   - Reset the animator bool:
     - `player.anim.SetBool(player.xBoolParam, false);` guarded the same way.
4. Transition out deterministically:
   - Prefer a single `TransitionOut()` helper that routes to `jumpState` if airborne, else move/idle based on input.

Where to follow existing examples:
- Attack bool in `C:\Users\USER\APART\Assets\scripts\PlayerAttackState.cs`
- Dash bool in `C:\Users\USER\APART\Assets\scripts\PlayerDashState.cs`
- Life drain bool in `C:\Users\USER\APART\Assets\scripts\PlayerLifeDrainState.cs`

## Animator Controller “Recipe” (copy/paste checklist)

Use this when you open the Animator window and want a deterministic, repeatable setup.

### Recipe: Add an action state (Dash/Attack/etc.)

Given:
- Action state name: `X` (e.g. `dash`, `attack`)
- Bool parameter: `isX` (e.g. `isDashing`, `isAttacking`)

Do:
1. Parameters tab: add `isX` (Bool).
2. Create state `X` and assign the `X.anim` clip.
3. Add transition **Any State → X**
   - Condition: `isX` == true
   - Has Exit Time: OFF
   - Transition Duration: near 0
   - **Can Transition To Self: OFF**  ← critical (prevents “only first frame”)
4. Add transition **X → idle**
   - Conditions: `isX` == false AND `isIdle` == true
   - Has Exit Time: OFF (state ends when code clears the bool)
5. Add transition **X → running**
   - Conditions: `isX` == false AND `isRunning` == true
   - Has Exit Time: OFF
6. Sanity check:
   - While playing, when you set `isX` true it should enter `X` once and keep playing.
   - When code sets `isX` false, it should exit to idle/running depending on your locomotion bools.

### Known pitfall (and the fix)

Symptom:
- Action animation only shows the first frame / keeps snapping back to frame 1.

Cause (common in APART if the Animator graph is edited quickly):
- More than one Any State → X transition exists, or “Can Transition To Self” is ON on Any State → X, so the Animator restarts X repeatedly while `isX` is true.

Fix:
- Ensure there is only one Any State → X transition, and it has “Can Transition To Self” OFF.

#### Player animator “always-on” params

If you add locomotion parameters that should always be updated (like `yVelocity`):
- Prefer adding **hashed IDs** in `player.cs` (see `IsGroundedParam`, `IsJumpingParam`, `YVelocityParam`) and update them in `SyncAnimatorMovementParams()`.
- Only do this for parameters that are truly “core” for the controller; optional action params should stay string-configured.

### D) Wire Animation Events (when the action has timing)

For melee attacks (current pattern):
- Add clip events on the attack animation clip:
  - Call `Combat.PerformHitCheck()` at the hit frame.
  - Call `Combat.AttackAnimationFinished()` at the last frame.

Code references:
- `C:\Users\USER\APART\Assets\scripts\Combat\Combat.cs` (`PerformHitCheck`, `AttackAnimationFinished`)
- `C:\Users\USER\APART\Assets\scripts\player.cs` (`OnAttackAnimationFinished`)
- `C:\Users\USER\APART\Assets\scripts\PlayerAttackState.cs` (fallback end condition)

Rule of thumb:
- If the state must end exactly when the clip ends, wire a finished event.
- If you can tolerate approximation, add a code fallback (timer/cooldown) like `PlayerAttackState` does.

### E) Inspector wiring checklist (the part people forget)

Player GameObject:
- `Animator` component uses the expected controller (`sprite.controller` or the correct override).
- `player` component:
  - Optional action parameter name fields match the controller parameter strings exactly:
    - `attackBoolParam`, `dashBoolParam`, `lifeDrainBoolParam` (and any new `xBoolParam` you add).
- `PlayerInput` is set up so actions call the correct message methods:
  - `OnAttack`, `OnSprint` (dash), `OnInteract` (life drain) are currently used.

Enemy GameObject:
- `EnemyController` optional param strings match its Animator Controller.
- `Enemy` trigger strings match its Animator Controller.

## Prompt template (use this to keep future requests coherent)

When asking to “connect animation to action”, provide:
- Actor: `Player` or `Enemy` (which prefab / scene object).
- Controller asset path (e.g. `Assets/sprites/animations/character animations/sprite.controller`).
- New animation state name(s): e.g. `parry`.
- Parameter(s) to add: e.g. `isParrying` (bool).
- Desired transitions:
  - from which state(s) to which state(s)
  - exit mechanism (Exit Time vs `isX == false`)
- Code hook:
  - which state should set/reset the parameter (new `PlayerParryState` etc.)
  - whether you need animation events (hit frame / finished)
- Acceptance checks:
  - which input triggers it
  - what should happen if animation events are missing (fallback or not)

Example (copy/paste and fill in):
```
Connect a new Player action animation:
- Action: Parry
- Controller: Assets/sprites/animations/character animations/sprite.controller
- Animator param: isParrying (bool)
- Animator states/transitions: Idle/Run/Jump -> Parry when isParrying=true, Parry -> locomotion when isParrying=false
- Code: add PlayerParryState that sets isParrying true in Enter and false in Exit; consume parryPressed input
- Events: none (state ends via timer 0.25s)
```

## Notes / current limitations

- The current player controller uses `isIdle/isRunning` set from states plus `isGrounded/isJumping/yVelocity` set centrally; keep this split unless you explicitly refactor the controller.
- Several “Trigger param” fields exist on `player.cs` (`dashTriggerParam`, `attackTriggerParam`, `lifeDrainTriggerParam`) but **the current implementation does not use triggers**; ignore these fields unless you explicitly decide to migrate later.
