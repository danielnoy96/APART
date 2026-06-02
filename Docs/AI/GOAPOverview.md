# GOAP Overview

## Purpose
CrashKonijn GOAP chooses enemy intent. It should decide which behavior an enemy is trying to run, while normal Unity gameplay components remain the source of truth for movement, damage, animation, and state.

## Main Files
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
- `Assets/scripts/GOAP/Sensors/PlayerAboveSensor.cs`
- `Assets/scripts/GOAP/Sensors/ObstacleAheadSensor.cs`
- `Assets/scripts/GOAP/Sensors/HasPlayerTargetSensor.cs`
- `Assets/scripts/GOAP/Targets/PlayerTarget.cs`
- `Assets/scripts/GOAP/Targets/PlayerTargetResolver.cs`

## Runtime Flow
1. `EnemyGoapAgentBridge` prepares GOAP components, assigns the player to `EnemyController`, and disables legacy `EnemyBrain` when GOAP is present.
2. A decision module such as `DistanceGoalSelector` requests a goal from `GoapActionProvider`.
3. GOAP uses world sensors and configured actions to pick an action.
4. Actions call real gameplay methods on `EnemyController`, such as `Patrol()`, `ChasePlayer()`, or `TryJump()`.
5. If the enemy dies, the bridge/controller stop GOAP-driven behavior.

## Current Behaviors
- Patrol.
- Chase player.
- Jump toward a player on a higher platform.
- Jump when an obstacle is ahead; this is separate from player-above jumping.

## Inspector Wiring
- Scene must contain a `GoapBehaviour` and controller component such as `ReactiveControllerBehaviour`.
- Enemy prefab needs `AgentTypeBehaviour`, `GoapActionProvider`, `CrashKonijn.Agent.Runtime.AgentBehaviour`, `GoapRunnerResolver`, and `EnemyGoapAgentBridge`.
- `AgentTypeBehaviour.runner` is a scene reference; prefabs should rely on `GoapRunnerResolver` to inject it.
- Capability/agent type assets must include the relevant goals, actions, world keys, target keys, and sensors.

## Important Rules
- GOAP does not own movement physics. `EnemyController` remains the movement executor.
- GOAP actions must mutate or call real gameplay state; effects only guide planning.
- GOAP actions must respect `EnemyAwareness.CanRunRegularBehavior` when `EnemyAwareness` is present.
- Goal selection belongs in bridge/decision module code, not inside goal/action classes.
- Do not treat `EnemyAwareness` as GOAP. It is an enemy-side behavior gate that GOAP actions check.

## Known Issues
- GOAP ScriptableObject configuration can drift from code if generated IDs or capability assets are not updated.

## Related Docs
- `../Enemy/EnemyController.md`
- `../Enemy/EnemyAwareness.md`
- `../../GOAP_CRASHKONIJN_WORKING_NOTES.md`
