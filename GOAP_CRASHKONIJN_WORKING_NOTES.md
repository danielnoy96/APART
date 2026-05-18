# CrashKonijn GOAP v3 Working Playbook

Purpose: this file is the local working reference for implementing, debugging, and extending the CrashKonijn GOAP system in this Unity project.

Docs source: https://goap.crashkonijn.com  
Docs reviewed: 2026-05-18

This is not a copy of the docs. It is a practical implementation guide for how I should work with the system in this repository.

## Non-negotiable rules

- GOAP is a planner/executor framework, not the authoritative game state.
- Never treat GOAP `WorldState` as source of truth.
- Store real gameplay state in normal Unity components (`MonoBehaviour`, inventory, health, hunger, blackboard, registries).
- Sensors read the real state and expose simplified values to GOAP.
- Action `Effects` are planning metadata only. They do not mutate the game world.
- Actions must mutate gameplay state themselves in `Complete`, `Perform`, or another appropriate action hook.
- Keep GOAP `Goal`, `Action`, and `Sensor` classes stateless unless the docs explicitly provide a per-agent storage mechanism.
- Per-agent and per-run action data belongs in an `IActionData` class, not on the action instance.
- Every action should have a target unless it is intentionally targetless and configured with `RequiresTarget = false`.
- Do not model movement as separate "move to X" GOAP actions unless there is a deliberate design reason.
- A `GoapBehaviour` object needs a controller, or the system will not run.
- Goal selection is game-specific. The library resolves actions for requested goals; it does not decide the best high-level goal for the game.

## Package and version assumptions

- The official install URL in the docs points at the GitHub package path with version `3.1.2`.
- The docs say the package was built using Unity 2022.2 and newer Unity versions should work.
- v3 has been tested with Unity 2021.3 and newer, but the v3 upgrade guide says v3 dropped support for Unity 2021.x and was built on 2022.2. When changing package versions, verify the package manifest and Unity version locally instead of relying on memory.

## Core model

### GraphBuilder

The `GraphBuilder` runs when an `AgentType` is created. It uses:

- Goals
- Actions
- Goal conditions
- Action conditions
- Action effects
- World keys
- Target keys

It builds the static planning graph that connects actions to other actions and goals.

### Resolver

The `Resolver` runs at runtime. It uses:

- The graph from the `GraphBuilder`
- The current sensor-fed `WorldState`
- The currently requested goal or goals
- Action costs
- Target distances
- Disabled actions

It works backward from requested goals and chooses an executable low-cost action path.

### Important consequence

GOAP can infer "this action increases `PearCount`", but it will not actually add a pear to inventory. The action must modify `DataBehaviour`, inventory, world objects, combat state, etc.

## Runtime architecture

### GoapBehaviour object

Create or locate a scene object that owns the GOAP runner:

- `GoapBehaviour`
- One GOAP controller:
  - `ReactiveControllerBehaviour`
  - `ProactiveControllerBehaviour`
  - `ManualControllerBehaviour`
- Agent type factories or `AgentTypeBehaviour` children, depending on configuration style.

### Agent object

Each GOAP-controlled actor generally needs:

- `AgentBehaviour`
- `GoapActionProvider`
- A component that requests goals, often a "brain" or state machine script.
- Movement script that listens to `AgentBehaviour.Events`.
- Gameplay data components used by sensors and actions.

The `AgentBehaviour` executes actions. The `GoapActionProvider` decides which action should be executed.

### AgentBehaviour

Use `AgentBehaviour` for:

- Running the current action.
- Movement target events.
- Action lifecycle events.
- Manual `Run` calls if `RunInUnityUpdate` is disabled.
- Distance heuristics via `SetDistanceMultiplierSpeed` or a custom distance observer.

Do not put GOAP goal selection calls directly on `AgentBehaviour`; in v3 those belong to `GoapActionProvider`.

### GoapActionProvider

Use `GoapActionProvider` for:

- Assigning `AgentType`.
- Requesting one or more goals.
- Reading current plan/debug state.
- Enabling and disabling actions.

Typical usage:

```csharp
var agent = GetComponent<AgentBehaviour>();
var provider = GetComponent<GoapActionProvider>();

agent.ActionProvider = provider;
provider.RequestGoal<IdleGoal, PickupPearGoal>();
```

## Configuration choice

There are two supported configuration styles. Choose based on what the local project already uses.

### ScriptableObject configuration

Use this when the project favors editor-driven setup and simple non-generic GOAP classes.

Create:

- `Create > GOAP > Generator`
- `Create > GOAP > Agent Type`
- `Create > GOAP > Capability`
- `Create > GOAP > WorldKey`
- `Create > GOAP > TargetKey`

Rules:

- ScriptableObject setup requires a `GeneratorScriptable`.
- All GOAP classes and SO configs must live under subfolders of a generator.
- Set the generator namespace correctly.
- GOAP classes must be in that namespace to be found.
- ScriptableObject setup cannot use generic classes.
- Use `[GoapId(...)]` on generated/configured classes to keep inspector references stable across renames/moves.
- Use inspector `Check Issues` / `Fix Issues` when references or config validation look wrong.

### Code configuration

Use this when the project needs generics, custom setup code, dynamic conditions, or strongly code-driven configuration.

Agent type pattern:

```csharp
public class DemoAgentTypeFactory : AgentTypeFactoryBase
{
    public override IAgentTypeConfig Create()
    {
        var builder = this.CreateBuilder("DemoAgent");

        builder.AddCapability<IdleCapabilityFactory>();
        builder.AddCapability<PearCapabilityFactory>();

        return builder.Build();
    }
}
```

Capability pattern:

```csharp
public class IdleCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("IdleCapability");

        builder.AddGoal<IdleGoal>()
            .AddCondition<IsIdle>(Comparison.GreaterThanOrEqual, 1)
            .SetBaseCost(2);

        builder.AddAction<IdleAction>()
            .AddEffect<IsIdle>(EffectType.Increase)
            .SetTarget<IdleTarget>();

        builder.AddTargetSensor<IdleTargetSensor>()
            .SetTarget<IdleTarget>();

        return builder.Build();
    }
}
```

v3.1 note:

- Prefer `this.CreateBuilder(...)` inside `AgentTypeFactoryBase` instead of manually creating `new AgentTypeBuilder(...)`.
- v3.1 supports dynamic conditions in code.
- v3.1 allows injection into `AgentTypeFactoryBase` and `CapabilityFactoryBase`.

Scene hookup for code config:

- Add the `AgentTypeFactoryBase` component to a scene GameObject.
- Add that object/factory to the `GoapBehaviour` agent type config factory list.
- At runtime, assign the resolved agent type:

```csharp
var goap = FindObjectOfType<GoapBehaviour>();
provider.AgentType = goap.GetAgentType("DemoAgent");
```

## Goals

Goals represent desired outcomes. They are not behavior scripts.

Goal class rule:

```csharp
public class EatGoal : GoalBase
{
}
```

Goal config contains:

- Goal class type.
- Conditions that define when the goal is satisfied.
- Optional base cost.

Guidelines:

- Keep goal classes empty/stateless by default.
- Put goal achievement criteria in config, not mutable fields.
- Request goals through `GoapActionProvider`.
- Request multiple goals when the resolver may choose between acceptable outcomes.
- Do not assume GOAP chooses the best game-level goal. Add a brain/utility/FSM layer for that.

Example:

```csharp
provider.RequestGoal<IdleGoal, PickupPearGoal>();
```

## Actions

An action has four parts:

- Config: conditions, effects, cost, target, movement settings.
- Action class: stateless behavior logic.
- Action data: per-agent/per-run state.
- Action props: shared configuration values set in inspector or builder.

### Action config fields

Use these deliberately:

- `Conditions`: must be met for the action to be executable.
- `Effects`: planner-facing state direction after action succeeds.
- `BaseCost`: inherent cost before distance cost.
- `TargetKey`: where the action is performed.
- `StoppingDistance`: how close the agent must be to perform.
- `RequiresTarget`: false only for genuinely targetless actions.
- `ValidateTarget`: whether target remains valid during execution.
- `ValidateConditions`: whether action conditions remain valid during execution.
- `MoveMode`: movement/action relationship.

### MoveMode

Use `MoveBeforePerforming` when the action must occur after reaching the target.

Use `PerformWhileMoving` when the action can run while the agent is traveling.

### Action lifecycle

Common overrides:

- `Created`: action object was created.
- `IsValid`: validate before action continues.
- `Start`: action starts.
- `BeforePerform`: first frame before performing.
- `Perform`: required action loop.
- `Complete`: action successfully completed.
- `Stop`: action stopped.
- `End`: action completed or stopped.

Minimal action skeleton:

```csharp
public class ExampleAction : GoapActionBase<ExampleAction.Data>
{
    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        return ActionRunState.Completed;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }
    }
}
```

### Action data

Use action data for:

- Current target.
- Timers.
- Cached component references.
- Any state specific to one agent running this action.

Pattern:

```csharp
public class Data : IActionData
{
    public ITarget Target { get; set; }

    [GetComponent]
    public DataBehaviour Data { get; set; }
}
```

### Action props

Use props for shared configurable values:

```csharp
[Serializable]
public class Props : IActionProperties
{
    public float MinDuration;
    public float MaxDuration;
}
```

Then use:

```csharp
public class WaitAction : GoapActionBase<WaitAction.Data, WaitAction.Props>
{
}
```

### IActionRunState

Built-in run states include:

- `ActionRunState.Continue`
- `ActionRunState.ContinueOrResolve`
- `ActionRunState.Stop`
- `ActionRunState.Completed`
- `ActionRunState.Wait(time, mayResolve)`
- `ActionRunState.WaitThenComplete(time, mayResolve)`
- `ActionRunState.WaitThenStop(time, mayResolve)`
- `ActionRunState.StopAndLog(message)`

Use `mayResolve` carefully with `ProactiveController`. If an action should not be interrupted during a timed phase, keep `mayResolve` false.

Example timed action:

```csharp
public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
{
    return ActionRunState.WaitThenComplete(0.5f);
}
```

### Enabling and disabling actions

Actions can be disabled so the resolver ignores them.

Use cases:

- Cooldowns.
- Temporary invalid abilities.
- Resource lockouts.
- One-time actions.

Patterns:

```csharp
provider.Disable<PickupPearAction>();
provider.Enable<PickupPearAction>();
```

Inside an action:

```csharp
this.Disable(agent, ActionDisabler.ForTime(5f));
```

## WorldKeys

`WorldKey` classes identify integer values in planning.

Rules:

- All world state values are `int`.
- Use comparisons for thresholds, not equality.
- A `WorldKey` should have a sensor or be intentionally synthetic.

Code pattern:

```csharp
public class Hunger : WorldKeyBase
{
}
```

Good examples:

- `Hunger`
- `Health`
- `PearCount`
- `AmmoCount`
- `ThreatLevel`
- `HasWeapon` represented as `0` or `1`

Avoid excessive boolean key explosion when one integer key can express a useful range.

## TargetKeys

`TargetKey` classes identify world positions for actions.

Rules:

- Every target key needs a target sensor.
- Target sensors return `ITarget`.
- Use `TransformTarget` for moving targets.
- Use `PositionTarget` for fixed or generated positions.

Code pattern:

```csharp
public class ClosestPear : TargetKeyBase
{
}
```

Examples:

- `ClosestEnemy`
- `ClosestPear`
- `CoverPosition`
- `IdleTarget`
- `WorkbenchTarget`

## Conditions and effects

Conditions are planner requirements:

- `WorldKey`
- `Comparison`
- `Value`

Effects are planner direction hints:

- `WorldKey`
- `EffectType.Increase` or `EffectType.Decrease`

Matching rule:

- `GreaterThan` and `GreaterThanOrEqual` conditions are satisfied by actions that increase that key.
- `SmallerThan` and `SmallerThanOrEqual` conditions are satisfied by actions that decrease that key.

There is no equality comparison because equality does not give the planner a direction.

Example:

```csharp
builder.AddGoal<EatGoal>()
    .AddCondition<Hunger>(Comparison.SmallerThanOrEqual, 0);

builder.AddAction<EatAction>()
    .AddCondition<PearCount>(Comparison.GreaterThanOrEqual, 1)
    .AddEffect<Hunger>(EffectType.Decrease)
    .SetRequiresTarget(false);
```

Important: if `EatAction` decreases `Hunger` in GOAP config, the code must still set the real hunger value.

## Sensors

Sensors read game state and feed GOAP when the resolver needs it.

Sensor dimensions:

- Key type:
  - `WorldKey`: integer value.
  - `TargetKey`: position/target.
- Scope:
  - Local: per agent.
  - Global: shared by all agents of an `AgentType`.

Base classes:

- `LocalWorldSensorBase`
- `GlobalWorldSensorBase`
- `LocalTargetSensorBase`
- `GlobalTargetSensorBase`
- `MultiSensorBase`

### Local world sensor

Use when the value is specific to one agent.

```csharp
public class HungerSensor : LocalWorldSensorBase
{
    public override SenseValue Sense(IActionReceiver agent, IComponentReference references)
    {
        var data = references.GetCachedComponent<DataBehaviour>();
        return (int)data.Hunger;
    }
}
```

### Local target sensor

Use when the target depends on the agent.

```csharp
public class ClosestPearSensor : LocalTargetSensorBase
{
    private PearBehaviour[] pears;

    public override void Update()
    {
        this.pears = Object.FindObjectsOfType<PearBehaviour>();
    }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget target)
    {
        var closest = this.FindClosest(agent.Transform.position);

        if (closest == null)
            return null;

        if (target is TransformTarget transformTarget)
            return transformTarget.SetTransform(closest.transform);

        return new TransformTarget(closest.transform);
    }
}
```

### Target reuse

The target sensor receives the previous `ITarget`. Reuse it where possible to avoid unnecessary allocations and preserve target identity.

### MultiSensor

Use `MultiSensorBase` when multiple GOAP values come from one source.

Good use case:

- `DataBehaviour` provides `PearCount` and `Hunger`.
- Scene pear lookup provides `ClosestPear`.
- One `PearSensor` registers all three.

Pattern:

```csharp
public class PearSensor : MultiSensorBase
{
    public PearSensor()
    {
        this.AddLocalWorldSensor<PearCount>((agent, references) =>
        {
            var data = references.GetCachedComponent<DataBehaviour>();
            return data.PearCount;
        });

        this.AddLocalWorldSensor<Hunger>((agent, references) =>
        {
            var data = references.GetCachedComponent<DataBehaviour>();
            return (int)data.Hunger;
        });

        this.AddLocalTargetSensor<ClosestPear>((agent, references, target) =>
        {
            return this.GetClosestPearTarget(agent, target);
        });
    }
}
```

### Sensor timers

Use sensor timers to reduce work:

- `SensorTimer.Always`
- `SensorTimer.Once`
- `SensorTimer.Interval(seconds)`

Use `Once` for stable self targets.
Use `Interval` for expensive searches that do not need per-frame updates.
Use `Always` for rapidly changing combat/position data.

Example:

```csharp
public override ISensorTimer Timer { get; } = SensorTimer.Interval(0.25f);
```

## Movement

CrashKonijn GOAP does not prescribe a movement implementation.

The usual flow:

- Action has a `TargetKey`.
- Target sensor supplies the target.
- Resolver includes distance cost.
- `AgentBehaviour` raises target events.
- Movement component reacts to those events.

Relevant events:

- `OnTargetChanged`
- `OnTargetInRange`
- `OnTargetOutOfRange`

Movement component responsibilities:

- Store current target.
- Decide whether movement is active.
- Move with the project movement system.
- Preserve y-axis / navmesh / physics constraints as required by the project.

If using NavMesh distance, implement `IAgentDistanceObserver` and assign it to `AgentBehaviour.DistanceObserver`.

## Controllers

A controller must exist on the `GoapBehaviour` GameObject.

### ReactiveController

Runs sensors and resolver when an agent needs a new action.

Use for:

- Simple agents.
- Lower overhead.
- v2-like behavior.

### ProactiveController

Runs sensors and resolver periodically even while an action is running.

Use for:

- Fast-changing worlds.
- Opportunistic action switching.
- Agents that should adapt before current action ends.

Guard sensitive action phases with `IActionRunState.MayResolve` / `mayResolve: false`.

### ManualController

Runs when explicitly triggered by agent/system code.

Use for:

- Deterministic update scheduling.
- Turn-based systems.
- Custom batching.
- Performance-controlled AI ticks.

## Goal selection layer

The docs are explicit: deciding the best goal is game-specific.

Acceptable approaches:

- Simple brain/FSM.
- Utility AI.
- Behavior tree.
- Designer-authored priority rules.
- Combat state machine that requests GOAP goals.

Pattern:

```csharp
private void OnActionEnd(IAction action)
{
    if (this.data.Hunger > 50)
    {
        this.provider.RequestGoal<EatGoal>();
        return;
    }

    this.provider.RequestGoal<IdleGoal, PickupPearGoal>();
}
```

## Data injection

Use data injection when GOAP classes need scene services or registries without hard-coding scene lookups.

Pattern:

- Create a `MonoBehaviour` implementing `IGoapInjector`.
- Inject dependencies into actions/goals/sensors/factories.
- Bind the custom initializer to `GoapBehaviour` / runner config initialization.

v3.1 injector surface includes:

- `Inject(IAction action)`
- `Inject(IGoal goal)`
- `Inject(ISensor sensor)`
- `Inject(IAgentTypeFactory factory)`
- `Inject(ICapabilityFactory factory)`

Use injection for:

- Item factories.
- Spawn systems.
- World registries.
- Service locators.
- Third-party DI containers such as Zenject.

Do not use injection for per-agent state. Use action data and cached components for that.

## Graph Viewer

Use `Tools/GOAP/Graph Viewer` or `Ctrl+G`.

It can visualize:

- `AgentTypeScriptable`
- `CapabilityConfigScriptable`
- `ScriptableCapabilityFactoryBase`
- `AgentTypeFactoryBase`
- `AgentTypeBehaviour`
- `GoapActionProvider`

During play mode, selecting an agent can show condition state based on current world data.

Use it when:

- A graph is missing nodes.
- Actions are not connected to goals.
- Sensors are not producing expected condition values.
- Target names/effects are confusing.
- ScriptableObject references broke after moving/renaming code.

## Debugging checklist

When an agent does not act:

1. Confirm `GoapBehaviour` exists in scene.
2. Confirm a controller exists on the `GoapBehaviour` object.
3. Confirm agent has `AgentBehaviour`.
4. Confirm agent has `GoapActionProvider`.
5. Confirm `AgentBehaviour.ActionProvider` points to `GoapActionProvider`.
6. Confirm `GoapActionProvider.AgentType` or `AgentTypeBehaviour` is assigned.
7. Confirm a brain script calls `RequestGoal`.
8. Confirm requested goal exists in the agent type.
9. Confirm goal conditions can be reached by action effects.
10. Confirm required world keys have sensors.
11. Confirm required target keys have target sensors.
12. Confirm target sensors return non-null targets for actions requiring targets.
13. Confirm actions are not disabled.
14. Confirm `ValidateConditions` or `ValidateTarget` is not stopping the action.
15. Open Graph Viewer and inspect connections/condition state.

When an action starts but has no gameplay effect:

1. Check whether the action mutates real gameplay state.
2. Check `Complete` is actually called.
3. Check `Perform` returns a completing run state.
4. Check action data has injected components.
5. Check target type assumptions, especially `TransformTarget` vs `PositionTarget`.

When the planner never chooses an action:

1. Check condition/effect direction.
2. Replace equality thinking with threshold comparisons.
3. Check base cost is not too high.
4. Check distance multiplier and target distance.
5. Check disabled actions.
6. Check target requirement.
7. Check if another requested goal is cheaper.

## Implementation workflow for new GOAP behavior

Use this sequence for every new behavior:

1. Define the real game state in normal Unity components.
2. Decide what GOAP needs to know as integer `WorldKey` values.
3. Decide what GOAP needs to know as positional `TargetKey` values.
4. Add or generate key classes.
5. Add sensors that expose current state/targets.
6. Add a goal class.
7. Configure the goal conditions.
8. Add one or more action classes.
9. Configure action conditions, effects, costs, targets, and move mode.
10. Implement real state mutation in action code.
11. Add action data for per-agent state and cached components.
12. Add props for designer/configurable shared values.
13. Add everything to a capability.
14. Add the capability to an agent type.
15. Assign the agent type to the provider.
16. Update the brain to request the goal.
17. Validate in Graph Viewer.
18. Play test and inspect action/goal events.

## Example: pickup resource behavior

State:

- `DataBehaviour.ResourceCount`
- Scene objects with `ResourceBehaviour`

Keys:

- `ResourceCount : WorldKeyBase`
- `ClosestResource : TargetKeyBase`

Goal:

- `PickupResourceGoal`
- Condition: `ResourceCount >= desiredAmount`

Action:

- `PickupResourceAction`
- Effect: `ResourceCount Increase`
- Target: `ClosestResource`
- Requires target: true
- Complete:
  - Increment `ResourceCount`
  - Destroy/deactivate target resource object

Sensor:

- MultiSensor or target sensor that finds closest resource.
- Return `TransformTarget` for resource transform.
- Return null if no resource exists.

Brain:

- Request `PickupResourceGoal` when collecting is acceptable.
- Request fallback goal like `IdleGoal` at the same time if useful.

## Example: consume inventory behavior

State:

- `DataBehaviour.ResourceCount`
- `DataBehaviour.Hunger`

Keys:

- `ResourceCount : WorldKeyBase`
- `Hunger : WorldKeyBase`

Goal:

- `EatGoal`
- Condition: `Hunger <= 0`

Action:

- `EatAction`
- Condition: `ResourceCount >= 1`
- Effect: `Hunger Decrease`
- `RequiresTarget = false`
- Complete:
  - Decrement `ResourceCount`
  - Reset/decrease real hunger value

Important:

- Targetless action must be explicitly configured as targetless.
- If hunger is a float in real state, cast or map it to int in sensors.

## Upgrade notes that affect implementation

### v3.1

- `AgentTypeFactoryBase` should use `CreateBuilder`.
- `IGoapInjector` includes factory injection methods.
- Code configuration supports dynamic conditions.
- `AgentBehaviour.Run()` can be called manually with custom delta time if `RunInUnityUpdate` is false.

### v3.0

- `AgentBehaviour` and `GoapActionProvider` are separate.
- GOAP methods moved to `GoapActionProvider`.
- `GoapSet` became `AgentType` plus `Capability`.
- Goals are requested, not simply set.
- Multiple goals can be requested at once.
- Goals have base cost.
- Actions can be disabled.
- Action props exist for shared configurable values.
- `ActionRunState` became `IActionRunState`.
- Sensors can use timers.
- Target sensors receive previous target.
- Multi-sensors exist.
- Controller is required on `GoapBehaviour`.
- `InRange` was renamed to `StoppingDistance`.
- `RequiresTarget`, `ValidateTarget`, and `ValidateConditions` matter for runtime behavior.

### v2.1 to v3 migration reminders

- Replace `GoapRunnerBehaviour` with `GoapBehaviour`.
- Add a controller to the GOAP object.
- Convert old `GoapSetFactoryBase` usage to v3 factory/capability patterns.
- Use `GoapActionProvider.RequestGoal`.
- Update namespace imports to v3 namespaces.
- Update sensors to use `IActionReceiver`.
- Use `.Transform` where v3 interfaces expose transform wrappers.

## Project conventions to follow when editing this repo

- Before creating new GOAP code, inspect existing GOAP folders and mirror their structure.
- Prefer the configuration style already used by the project.
- Do not mix ScriptableObject and code config casually; only mix when the docs-supported setup is already used locally.
- Keep key, goal, action, and sensor names domain-specific and readable.
- Keep action classes small; push game state into existing components.
- Avoid `FindObjectOfType` in hot paths; use sensors `Update`, registries, injection, or cached component references.
- Use `GetCachedComponent` / `[GetComponent]` for per-agent component references.
- Use sensor timers for expensive target searches.
- Use Graph Viewer after adding or changing goals/actions/sensors.

## Source pages reviewed

- https://goap.crashkonijn.com/
- https://goap.crashkonijn.com/readme/theory
- https://goap.crashkonijn.com/readme/tutorial/gettingstarted
- https://goap.crashkonijn.com/readme/tutorial/setup
- https://goap.crashkonijn.com/readme/tutorial/pears
- https://goap.crashkonijn.com/readme/faq
- https://goap.crashkonijn.com/config/scriptableobjects
- https://goap.crashkonijn.com/config/code
- https://goap.crashkonijn.com/classes/goals
- https://goap.crashkonijn.com/classes/actions
- https://goap.crashkonijn.com/classes/agentbehaviourandactionprovider
- https://goap.crashkonijn.com/classes/sensors
- https://goap.crashkonijn.com/classes/targetkeys
- https://goap.crashkonijn.com/classes/worldkeys
- https://goap.crashkonijn.com/general/agenttypeandcapabilities
- https://goap.crashkonijn.com/general/controllers
- https://goap.crashkonijn.com/general/generator
- https://goap.crashkonijn.com/general/worldstate
- https://goap.crashkonijn.com/general/conditionsandeffects
- https://goap.crashkonijn.com/general/injection
- https://goap.crashkonijn.com/general/lifecycle
- https://goap.crashkonijn.com/general/graphviewer
- https://goap.crashkonijn.com/examples/simple
- https://goap.crashkonijn.com/examples/complex
- https://goap.crashkonijn.com/upgrading/upgrade-guide-3.0-3.1
- https://goap.crashkonijn.com/upgrading/core-concepts
- https://goap.crashkonijn.com/upgrading/upgrade-guide-2.1-3.0
- https://goap.crashkonijn.com/upgrading/upgrade-guide-2.0-2.1
