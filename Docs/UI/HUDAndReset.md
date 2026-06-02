# HUD And Runtime Reset

## Purpose
The UI code currently covers player health/stamina display and an automatically created in-game reset button.

## Main Files
- `Assets/scripts/UI/PlayerHUD.cs`
- `Assets/scripts/UI/InGameResetButton.cs`
- `Assets/scripts/Combat/Health.cs`
- `Assets/scripts/Combat/Stamina.cs`

## Runtime Flow
### Player HUD
1. `PlayerHUD` finds the player if not assigned.
2. It resolves `Health` and `Stamina`.
3. It subscribes to health and stamina events.
4. It updates assigned `Image.fillAmount` or `Slider` values.

### Runtime Reset
1. `InGameResetButton` listens after scene load.
2. It finds the best active canvas.
3. It creates a reset button under that canvas if one does not already exist.
4. Button click or configured keyboard shortcut reloads the current scene.

## Inspector Wiring
- `PlayerHUD` needs either fill images or sliders for health and stamina.
- A scene canvas must exist for `InGameResetButton` to create its button.
- If no event system exists, `InGameResetButton` creates one with `InputSystemUIInputModule`.

## Important Rules
- HUD should listen to events instead of polling every frame.
- Reset button reloads the active scene and resets `Time.timeScale` to 1.
- Runtime reset is globally initialized through `RuntimeInitializeOnLoadMethod`, not manually placed per scene.

## Known Issues
- Runtime reset UI is development-oriented and may not belong in final player-facing UI.
- Auto-created UI depends on there being at least one active canvas in the scene.

## Related Docs
- `../../COMBAT_SYSTEM_OVERVIEW.md`

