# Project Agent Instructions

## Start Here
- Read `Docs/SYSTEMS_INDEX.md` first for the current system map and doc entry points.
- Read `PROJECT_CONTEXT.md` for the high-level snapshot, but verify against code before making changes.
- For focused work, read the relevant file under `Docs/` before editing scripts.
- Treat `Assets/scripts/**` as the source of truth. If a doc conflicts with code, trust the code and update the doc.
- Ignore `Library/PackageCache/**` Markdown unless package/API documentation is specifically needed.

## Skills And Deep Notes
- For CrashKonijn GOAP work in this Unity project, use the `crashkonijn-goap` skill when available.
- Do not load the full `GOAP_CRASHKONIJN_WORKING_NOTES.md` by default. Search/read only the sections relevant to the current task.
- Treat `GOAP_CRASHKONIJN_WORKING_NOTES.md` as the detailed project playbook for goals, actions, sensors, keys, agent types, capabilities, controllers, Graph Viewer debugging, and v3/v3.1 migration notes.
- For animation/Animator/event wiring work, read `skills/unity-animator-wiring/SKILL.md` and the relevant animation code first.

## Current Code Context
- Player code is centered on the lowercase `player` MonoBehaviour plus plain C# `PlayerState` classes.
- Player input uses Unity's New Input System with Send Messages callbacks: `OnMove`, `OnJump`, `OnAttack`, `OnSprint` for dash, and `OnInteract` for life drain.
- Player movement includes coyote time, jump buffering, variable gravity, jump cut, optional fall/rise clamps, dash stamina cost/cooldown/ease-out/end-lag, and knockback lock.
- Player animation is centralized through `PlayerAnimationDriver`; animation event clips should use `PlayerAnimationEventRelay` for attack hit/finish.
- Combat is component-based: `Health`, `Stamina`, `Combat`, `ContactDamage`, `KnockbackReceiver`, `DrainableCorpse`, and `PlayerCombo`.
- Player damage should go through `player.TryTakeDamage` so invincibility and hit animation rules are preserved.
- Enemy damage/death should go through `Health`; dead enemies remain as drainable corpses instead of being destroyed immediately.
- `EnemyController` owns enemy movement execution: patrol, chase, stop, facing, contact sensor mirroring, jump checks, and death movement shutdown.
- `EnemyAwareness` is not GOAP. It is a wake/hide/asleep behavior gate that GOAP and legacy enemy logic must respect.
- GOAP is planning/orchestration only. GOAP actions call real gameplay methods on `EnemyController`; they do not replace the movement system.
- Current GOAP behavior includes patrol, chase, jump-to-player, and jump-obstacle actions, with player-distance, player-above, obstacle-ahead, and target sensors.
- GOAP enemies follow the Always-GOAP policy: legacy `EnemyBrain` disables itself when GOAP components are present.
- `GoapRunnerResolver` injects the scene `GoapBehaviour` runner into prefab `AgentTypeBehaviour` at early execution order.
- Checkpoints have permanent and mini variants. Permanent respawn restores health/stamina; mini respawn is used by hazards and does not fully restore.
- Breakable crumble platforms combine gameplay timing with shard/particle/crumble visual systems; treat visuals and collider enable/disable timing as coupled.
- Runtime UI currently includes `PlayerHUD` health/stamina bars and an auto-created `InGameResetButton`.

## Editing Rules
- Keep behavior changes scoped to the owning system; avoid moving ownership across systems without an explicit reason.
- Do not bypass `EnemyController` for enemy movement unless the task is to replace enemy movement.
- Do not make GOAP goals/actions own persistent gameplay state. Use sensors and real Unity components as the source of truth.
- Do not remove animation-event fallbacks unless the target clips and events have been verified.
- Do not destroy dead enemies by default; life drain depends on corpse persistence.
- When changing ScriptableObject-backed GOAP classes, verify `[GoapId]` consistency and consider capability/agent asset wiring.
- When changing respawn, hazards, or death logic, verify interaction with respawn grace and player state reset.

