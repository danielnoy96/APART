# APART Conversation Handoff

> Purpose: short current-status handoff for a future chat.
>
> Last updated: 2026-05-31

## Do Not Use This As Architecture Reference
This file is intentionally short. For architecture and system details, use:
- `AGENTS.md` - project instructions and code-context rules.
- `PROJECT_CONTEXT.md` - current high-level project snapshot.
- `Docs/SYSTEMS_INDEX.md` - focused system doc map.
- `COMBAT_SYSTEM_OVERVIEW.md` - combat/player/enemy damage overview.
- `GOAP_CRASHKONIJN_WORKING_NOTES.md` - deep GOAP playbook; search by relevant section only.

## Current Documentation Status
Recently updated:
- `AGENTS.md`
- `PROJECT_CONTEXT.md`
- `COMBAT_SYSTEM_OVERVIEW.md`
- `Docs/SYSTEMS_INDEX.md`
- `Docs/AI/GOAPOverview.md`
- `Docs/Enemy/EnemyAwareness.md`
- `Docs/Enemy/EnemyController.md`
- `Docs/World/CheckpointsRespawnHazards.md`
- `Docs/World/BreakableCrumblePlatforms.md`
- `Docs/Combat/PlayerCombo.md`
- `Docs/UI/HUDAndReset.md`

The old duplicated architecture content in this file was removed to avoid conflicts with the updated source docs.

## Current Project Snapshot
- Code source of truth is `Assets/scripts/**`.
- Main scene is `Assets/Scenes/SampleScene.unity`.
- GOAP is CrashKonijn GOAP v3.1.2 from `Packages/manifest.json`.
- GOAP assets are under `Assets/scripts/GOAP/`.
- Player uses lowercase `player` plus plain C# `PlayerState` classes.
- Combat uses reusable `Health`, `Stamina`, `Combat`, `ContactDamage`, `KnockbackReceiver`, `DrainableCorpse`, and `PlayerCombo`.
- Enemies use `EnemyController` for movement execution.
- `EnemyAwareness` is a behavior gate, not GOAP.
- GOAP currently includes patrol, chase, jump-to-player, and jump-obstacle behavior.
- Dead enemies remain as drainable corpses.
- Permanent checkpoints restore health/stamina; mini checkpoints/hazards do not fully restore.
- Breakable platforms use procedural pooled crumble/shard visuals.

## Known Watch Points
- If a doc conflicts with code, trust code and update the doc.
- Some older GOAP notes may still describe patrol/chase only; current code includes jump GOAP actions and sensors.
- GOAP config can drift from `[GoapId]` classes and generated/config assets.
- Missing GOAP runner, receiver wiring, agent type, targets, sensors, or capability config can make enemies appear frozen.
- `EnemyController.IsGrounded()` returns true when `groundCheck` is missing, which can hide prefab setup mistakes.
- Animation event fallbacks prevent some stuck states, but missing events can still desync visuals and gameplay timing.

## Suggested Next Documentation Pass
If continuing documentation cleanup, update these next:
- `GOAP_CRASHKONIJN_WORKING_NOTES.md` - add a short project-current behavior note if needed; do not rewrite broadly.
- `skills/unity-animator-wiring/SKILL.md` - update only if current animation wiring docs are stale.

## Suggested Next Code/Unity Checks
- Verify GOAP enemy prefab wiring against `PROJECT_CONTEXT.md` and `Docs/AI/GOAPOverview.md`.
- Verify `CapabilityConfigScriptable.asset` includes current GOAP actions/sensors/world keys.
- Verify player prefab has `PlayerRespawnController`, `PlayerCombo` if desired, and correct Input System action map.
- Verify enemy prefabs have contact damage on child trigger sensors and jump checks wired if jump behavior is expected.

