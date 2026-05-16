# APART Unity Project — Working Context (GOAP + Player/Enemy)

> Purpose: keep a persistent, repo-local snapshot of the current setup, decisions, and known friction points so we can refer back without re-explaining everything each time.
>
> Last updated: 2026-05-16

## High-level goals (what we’re building)
- **CrashKonijn GOAP v3.1.2** is the GOAP system (not a custom/LLamaAcademy re-implementation).
- **GOAP decides _what_** the enemy is trying to do (goal/action selection): **Patrol** vs **Chase** only for now.
- **EnemyController executes _how_** the enemy moves (Rigidbody2D implementation stays the single executor; we do not rewrite movement).
- Enemy AI must be **modular**: decision logic is a swappable component on the prefab.
- **Always-GOAP policy** for enemies that have GOAP components: legacy `EnemyBrain` should auto-disable so there’s no double-driving.

## Package status
- GOAP package is installed through UPM Git dependency in `Packages/manifest.json`:
  - `com.crashkonijn.goap` (Git URL pinned to `#3.1.2`)

## Folder / script layout (project-side, expected)
Scripts should live under:
- `Assets/Scripts/GOAP/Actions`
- `Assets/Scripts/GOAP/Goals`
- `Assets/Scripts/GOAP/Sensors`
- `Assets/Scripts/GOAP/Targets`
- `Assets/Scripts/GOAP/WorldKeys`

## Implemented project-side GOAP code (what exists now)
### GOAP domain scripts (under `Assets/Scripts/GOAP/**`)
- Actions:
  - `PatrolAction`
  - `ChasePlayerAction`
- Goals:
  - `PatrolGoal`
  - `ChasePlayerGoal`
- WorldKeys:
  - `HasTarget`
  - `IsPlayerInRange`
- Sensors:
  - `HasPlayerTargetSensor` (writes `HasTarget`)
  - `PlayerDistanceSensor` (writes `IsPlayerInRange`)
- Targets:
  - `PlayerTarget` (TargetKey)
  - `PlayerTargetResolver` (target resolver)
- Bridge / orchestration:
  - `EnemyGoapAgentBridge`
- Modular decision module abstraction:
  - `EnemyDecisionModuleBase`
  - `DistanceGoalSelector` (Patrol vs Chase based on distance; optional hysteresis)
- Prefab-safe runner binding:
  - `GoapRunnerResolver` (DefaultExecutionOrder -200; injects `AgentTypeBehaviour.runner` from a scene `GoapBehaviour`)

### Important implementation behavior (current)
- `EnemyGoapAgentBridge`:
  - Requires `GoapActionProvider` + `AgentTypeBehaviour`.
  - Requires a decision module (`EnemyDecisionModuleBase`).
  - Disables legacy `EnemyBrain` in `Awake` when present.
  - On death (`Health.IsDead`): calls `EnemyController.SetDead()` and disables `GoapActionProvider` to stop planning/execution.
  - Ensures GOAP receiver wiring at runtime:
    - CrashKonijn v3 needs `CrashKonijn.Agent.Runtime.AgentBehaviour` (receiver).
    - Bridge attempts to find `AgentBehaviour` and assign its `ActionProviderBase` / `ActionProvider` to the `GoapActionProvider` when missing.

- `EnemyController`:
  - Still contains its internal movement loop.
  - To prevent GOAP action movement from being overwritten every `FixedUpdate`, it now **skips its internal movement only when GOAP is actively executing**:
    - If `AgentBehaviour.ActionState.Action != null` → return early from `FixedUpdate`.
    - Otherwise, controller falls back to its internal logic (helps during partial setup).

## Unity scene + asset wiring (what should exist)
### Scene runner (required)
In the scene:
- Create `GOAP` GameObject
  - Add `GoapBehaviour`
  - Add `ReactiveControllerBehaviour`

### Generator + config assets (project-side)
Expected assets:
- Generator:
  - `Assets/Scripts/GOAP/Game.GOAP.asset` (namespace: `Game.GOAP`)
- Capability + AgentType:
  - `Assets/Scripts/GOAP/Config/CapabilityConfigScriptable.asset`
  - `Assets/Scripts/GOAP/Config/AgentTypeScriptable.asset`

Notes:
- Capability inspector may show issues if IDs are blank; it must be fixed via the inspector (“Fix issues!”) once Unity is idle (not importing).
- We added stable `[GoapId]` attributes to GOAP classes to prevent “matched by name, not id” issues.

## Prefab wiring (enemy)
Enemy root should have:
- `Health`, `Enemy`, `EnemyController`, `KnockbackReceiver`
- GOAP components:
  - `GoapRunnerResolver`
  - `AgentTypeBehaviour` (`config` = `AgentTypeScriptable.asset`, runner left empty)
  - `GoapActionProvider` (assigned to the `AgentTypeBehaviour`)
  - `CrashKonijn.Agent.Runtime.AgentBehaviour` (receiver; ActionProviderBase wired to `GoapActionProvider`)
  - `EnemyGoapAgentBridge` (`decisionModule` assigned)
  - `DistanceGoalSelector` (or another module later)

Contact damage should be on a child sensor object:
- Child `DamageSensor`
  - `Collider2D` with `IsTrigger=true`
  - `ContactDamage` with `targetLayer` including Player layer
  - On death: contact damage must stop (disable component or have it gate on `Health.IsDead`)

## Player setup (expected)
Player root must have:
- `player` (lowercase class), `Rigidbody2D`, `Animator`, `PlayerInput` (Send Messages), `Health`, `Stamina`, `KnockbackReceiver`, `Combat`
Inspector fields to assign:
- `groundCheck`, `groundLayer`
- LifeDrain: `drainCheckPoint`, `drainCheckRadius`, `drainableLayer`
- LifeDrain animation param: `lifeDrainBoolParam` defaults to `"isLifeDraining"` (and is forced to that value at runtime if serialized as empty).
Input actions expected (Send Messages):
- `Attack` → `OnAttack`
- `Sprint` → `OnSprint` (dash)
- `Interact` → `OnInteract` (corpse drain)

## Player-side fixes previously applied (behavioral)
- `SampleScene` PlayerInput `Default Action Map` was empty → set to `"Player"`.
- Jump animation: Animator params were synced in `player.FixedUpdate()`:
  - `isGrounded`, `isJumping = !isGrounded`, `yVelocity`.
- “Left mouse click stops control”: `PlayerAttackState.Update()` was given a fallback that exits the attack state when `Combat.CanAttack` becomes true (covers cases where animation events weren’t wired).
- Life drain animation: Animator controller uses `isLifeDraining` bool transitions (param name must match `lifeDrainBoolParam`).

## Known current issue(s) to investigate when GOAP “doesn’t chase”
Symptoms reported:
- Enemy patrols but never chases; later enemy stops moving; no useful log lines.

Most likely friction points:
1. GOAP isn’t executing an action (`AgentBehaviour.ActionState.Action` stays null).
2. Capability config asset still has invalid/blank IDs and wasn’t “fixed” successfully via inspector.
3. Sensors/targets aren’t producing valid world state (e.g., no `player` found, resolver not returning target, distance sensor not marking in-range).
4. Decision module threshold too small for scene scale.

## Where to look (files / assets / scene)
- Scene: `Assets/Scenes/SampleScene.unity`
- GOAP assets:
  - `Assets/Scripts/GOAP/Game.GOAP.asset`
  - `Assets/Scripts/GOAP/Config/CapabilityConfigScriptable.asset`
  - `Assets/Scripts/GOAP/Config/AgentTypeScriptable.asset`
- Key scripts:
  - `Assets/Scripts/GOAP/EnemyGoapAgentBridge.cs`
  - `Assets/Scripts/GOAP/GoapRunnerResolver.cs`
  - `Assets/Scripts/GOAP/DistanceGoalSelector.cs`
  - `Assets/Scripts/Enemy/EnemyController.cs`
- Unity Editor log:
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log`
