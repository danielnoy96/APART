# APART — Conversation Handoff (GOAP + Combat + Player/Enemy Wiring)

This file consolidates what was decided and implemented in the chat so a future chat can pick up immediately.

Sources merged into this doc:
- `PROJECT_CONTEXT.md` (GOAP snapshot / wiring expectations)
- `COMBAT_SYSTEM_OVERVIEW.md` (combat + FSM + reusable components)
- Chat progress notes (GOAP debugging and fixes)

Date: 2026-05-10 (Asia/Jerusalem)

---

## 1) What we’re building (non-negotiables)
- **CrashKonijn GOAP v3.1.2** is the GOAP system used (UPM Git dependency).
- **GOAP decides “what”** (goal/action selection: Patrol vs Chase).
- **EnemyController executes “how”** (Rigidbody2D movement; we do not rewrite movement systems).
- Enemy AI is **modular**:
  - A swappable decision module on the prefab chooses which GOAP goal to request.
- Player combat rules stay intact:
  - Player i-frames remain in `player.TryTakeDamage`.
  - Contact damage uses a child trigger + `ContactDamage`.
  - Life drain is corpse interaction via `DrainableCorpse` (not attack hit detection).
- Dead enemies are not destroyed immediately; corpses remain drainable.

---

## 2) Combat & Player systems (current architecture)
The combat foundation is documented in `COMBAT_SYSTEM_OVERVIEW.md`. Key points that matter for GOAP/enemy work:

### Player (`player` lowercase) + FSM
- Player is controlled by a plain C# FSM (`PlayerState` subclasses), not MonoBehaviours.
- Inputs are New Input System + `PlayerInput` set to **Send Messages**:
  - `Attack` → `OnAttack`
  - `Sprint` → `OnSprint` (dash)
  - `Interact` → `OnInteract` (life drain)
  - `Move` → `OnMove`
  - `Jump` → `OnJump`
- Player i-frames:
  - `ContactDamage` calls `player.TryTakeDamage(damageAmount)`.
  - `TryTakeDamage` enforces invincibility window.
- Knockback:
  - `KnockbackReceiver` applies velocity.
  - Player has a knockback lock timer so movement doesn’t override knockback.

### Enemy combat
- `Enemy` subscribes to `Health` events and ensures `DrainableCorpse` exists/enabled on death.
- `EnemyController` disables all `ContactDamage` sources on death and keeps corpse object alive.
- Contact damage is expected on a child sensor object with trigger collider.

---

## 3) GOAP integration (what exists now)

### 3.1 Folder layout (project-side)
GOAP scripts live under `Assets/scripts/GOAP/**` (note: repo currently uses lowercase `scripts` in the path).

### 3.2 GOAP domain scripts (minimal set)
We intentionally keep GOAP domain minimal:
- Goals:
  - `PatrolGoal`
  - `ChasePlayerGoal`
- Actions:
  - `PatrolAction` → calls `EnemyController.Patrol()`
  - `ChasePlayerAction` → calls `EnemyController.ChasePlayer()`
- WorldKeys:
  - `IsPlayerInRange` (written by sensor)
  - `HasTarget` (optional depending on config)
- Sensors:
  - `PlayerDistanceSensor` writes `IsPlayerInRange` using `EnemyGoapAgentBridge.DetectionRange`
- Targets:
  - `PlayerTarget` + `PlayerTargetResolver` exist, but the chase action was made target-optional to avoid resolver deadlocks during iteration.

### 3.3 Modular decision layer (project-side)
We added a prefab-level decision abstraction:
- `EnemyDecisionModuleBase` — decision-only API.
- `DistanceGoalSelector` — requests Patrol/Chase based on player distance.

### 3.4 Bridge / orchestration
`EnemyGoapAgentBridge` is the adapter between GOAP and the existing enemy controller:
- Ensures required GOAP components exist:
  - `GoapActionProvider`
  - `AgentTypeBehaviour`
  - `CrashKonijn.Agent.Runtime.AgentBehaviour` (ActionReceiver)
- Ensures receiver wiring (important):
  - `AgentBehaviour.ActionProviderBase = GoapActionProvider`
  - This makes `GoapActionProvider.Receiver` valid.
- Disables legacy `EnemyBrain` when GOAP is present (Always-GOAP policy).
- Assigns player transform to `EnemyController.SetPlayer`.
- Stops GOAP on death by disabling `GoapActionProvider`.
- Debug logging option (`debugLog`) to print:
  - requested goal, current plan goal/action, receiver states
  - GOAP events (`NoActionFound`, `GoalStart`, etc.)
- Safety fallback:
  - If GOAP emits `NoActionFound`, the bridge calls `EnemyController.Patrol()`/`ChasePlayer()` directly based on the last requested goal.
  - This is a practical non-invasive safety net while iterating capability config.

### 3.5 Prefab-safe runner binding
`AgentTypeBehaviour.runner` is a scene reference and cannot be stored in prefabs.
To solve that, we added:
- `GoapRunnerResolver` (`DefaultExecutionOrder(-200)`)
  - Finds scene `GoapBehaviour`.
  - Reflection-injects it into `AgentTypeBehaviour` before `AgentTypeBehaviour.Awake()` runs.

---

## 4) GOAP assets (Generator / Capability / AgentType)
These assets are required for GOAP to resolve actions.

### Generator
- `Assets/scripts/GOAP/Game.GOAP.asset`
- Contains discovered GOAP scripts and their IDs.
- We filled blank `<Id>` fields to match `[GoapId("...")]` attributes.

### Capability
- `Assets/scripts/GOAP/Config/CapabilityConfigScriptable.asset`
- This is where goals/actions/keys/sensors/targets are configured.

Important design choice:
- We model goal satisfaction using the sensor-driven world key `IsPlayerInRange`.
  - Chase goal: satisfied when `IsPlayerInRange >= 1`
  - Patrol goal: satisfied when `IsPlayerInRange <= 1`
- Actions have effects on `IsPlayerInRange` (increase/decrease) so the planner has a graph to satisfy goal conditions.
- Actions are configured with `moveMode = PerformWhileMoving` so `Perform()` can run continuously even when not “in range”.
  - (Since we do not implement GOAP movement, `MoveBeforePerforming` can prevent `Perform()` from ever running.)

### AgentType
- `Assets/scripts/GOAP/Config/AgentTypeScriptable.asset`
- References the capability asset above.

Known Unity-editor caveat:
- Clicking “Fix issues!” on capability assets can fail during import with:
  - `AssetDatabase.SaveAssetIfDirty restricted during asset importing`
  - Solution: wait until importing finishes, then click again.

---

## 5) Unity wiring checklists (how to set it up)

### Scene GOAP runner
Create a `GOAP` GameObject and add:
- `GoapBehaviour`
- `ReactiveControllerBehaviour`

### Enemy prefab (GOAP-driven enemy)
Must have gameplay components:
- `Health`, `Enemy`, `EnemyController`, `KnockbackReceiver`

Must have GOAP components:
- `GoapRunnerResolver`
- `AgentTypeBehaviour` (assign `config = AgentTypeScriptable.asset`, leave runner empty)
- `GoapActionProvider` (assign its `AgentTypeBehaviour`)
- `CrashKonijn.Agent.Runtime.AgentBehaviour` (set `ActionProviderBase = GoapActionProvider`)
- `EnemyGoapAgentBridge` (assign `decisionModule`)
- `DistanceGoalSelector` (or another module)

Contact damage child:
- child object with `Collider2D` set to `IsTrigger=true`
- `ContactDamage` with `targetLayer` including Player layer

### Player object
Must have:
- `player`, `Rigidbody2D`, `Animator`, `PlayerInput (Send Messages)`, `Health`, `Stamina`, `KnockbackReceiver`, `Combat`
Assign:
- `groundCheck`, `groundLayer`
- LifeDrain: `drainCheckPoint`, `drainCheckRadius`, `drainableLayer`

---

## 6) What went wrong during the chat (and what fixes were applied)

### 6.1 “Enemy does not chase / no log lines”
We added targeted debug logging and state reporting in `EnemyGoapAgentBridge`.

### 6.2 “CurrentPlan.Action = NULL” (GOAP not producing a plan)
We confirmed through status logs that:
- goal requests were happening
- but GOAP emitted `NoActionFound` for `ChasePlayerGoal`

Fix direction:
- capability config must form a satisfiable graph (conditions/effects must align).
- we re-centered goal satisfaction on `IsPlayerInRange` (sensor-driven) instead of “manual keys without sensors”.

### 6.3 “AgentTypeConfig has errors”
Earlier logs showed:
- goals/actions missing conditions/effects/targets due to missing IDs / invalid refs.
We addressed this by:
- filling IDs in `Game.GOAP.asset`
- ensuring capability points at valid IDs

### 6.4 GOAP receiver wiring exceptions
We observed:
- `GoapException: There is no ActionReceiver assigned...`
Fix direction:
- ensure every GOAP enemy has `AgentBehaviour` and it’s wired to the `GoapActionProvider`.
- added `RequireComponent(AgentBehaviour)` and fail-fast errors.

---

## 7) How to debug GOAP quickly (practical)
Enable `EnemyGoapAgentBridge.debugLog`.

Look for these:
- `CurrentPlan.Action = ...`
  - If `NULL`, GOAP didn’t pick an action.
- `Events.NoActionFound (goals=...)`
  - Means capability config is not resolvable for the requested goal.

If `NoActionFound` happens:
- Verify `CapabilityConfigScriptable.asset` goal conditions and action effects use the same key and are not missing IDs.
- Ensure `PlayerDistanceSensor` is working (player found, detection range matches).
- Ensure `AgentTypeBehaviour.config` references the right agent type asset and runner is injected (scene has GoapBehaviour).

---

## 8) Current status (as of this doc)
- Combat foundation + player FSM are documented and stable in `COMBAT_SYSTEM_OVERVIEW.md`.
- GOAP package is installed (manifest dependency).
- GOAP-first modular enemy structure exists:
  - decision module → bridge → GOAP request → GOAP provider/receiver → action → EnemyController methods.
- Debug instrumentation exists in bridge and actions to diagnose plan resolution.
- Capability config is actively being iterated; when `NoActionFound` appears, the bridge provides a direct controller fallback so gameplay remains testable while GOAP config is corrected.

---

## 9) Where to start in a new chat
Ask the next assistant to:
1. Read `CONVERSATION_HANDOFF.md`, `PROJECT_CONTEXT.md`, and `COMBAT_SYSTEM_OVERVIEW.md`.
2. Check `Editor.log` for GOAP validation errors and `NoActionFound`.
3. Verify these assets:
   - `Assets/scripts/GOAP/Game.GOAP.asset`
   - `Assets/scripts/GOAP/Config/CapabilityConfigScriptable.asset`
   - `Assets/scripts/GOAP/Config/AgentTypeScriptable.asset`
4. Verify enemy prefab wiring:
   - `GoapRunnerResolver`, `AgentTypeBehaviour`, `GoapActionProvider`, `AgentBehaviour`, `EnemyGoapAgentBridge`, and a decision module.

