# Player Combo

## Purpose
`PlayerCombo` rewards a chain of successful player hits by restoring stamina when the combo completes.

## Main Files
- `Assets/scripts/Combat/PlayerCombo.cs`
- `Assets/scripts/Combat/Combat.cs`
- `Assets/scripts/Combat/Stamina.cs`
- `Assets/scripts/player.cs`

## Runtime Flow
1. `PlayerCombo` requires the `player` component.
2. On enable, it subscribes to `Combat.OnHitCheckCompleted`.
3. A successful hit increments the combo counter.
4. If too much time passes between hits, the combo resets.
5. When `comboHitCount` is reached, stamina is restored and the combo resets.

## Inspector Wiring
- Add `PlayerCombo` to the player root.
- Player must have a valid `Combat` reference.
- Player must have a valid `Stamina` reference for the refund to matter.
- Tune `comboHitCount`, `comboResetSeconds`, and `comboStaminaRefund`.

## Important Rules
- Combo progress only advances on successful hit checks, not on attack button presses.
- Missing attacks do not currently reset the combo; timeout handles reset.
- Stamina restore uses `Stamina.Restore`, so it is clamped by the stamina component.

## Known Issues
- This system is not covered by the older combat overview.
- UI for combo progress does not appear to exist yet.

## Related Docs
- `../../COMBAT_SYSTEM_OVERVIEW.md`

