# VampireCrawlersFarmBot

`VampireCrawlersFarmBot` is a BepInEx IL2CPP mod for Vampire Crawlers. It adds a hotkey-driven farming bot that can enter a configured stage, detonate the dungeon nuke, resolve safe level-up choices, collect chest cash-outs, trigger the floor exit, settle the run through the normal result screens, and loop from town.

Earlier development used separate helper mods as reference code. They are no longer included in this project, so the built DLL only exposes the FarmBot plugin.

## Current Capabilities

The FarmBot currently supports the Dairy Plant / Curdling Factory farming route:

1. Start from the town map.
2. Open the world map.
3. Select Dairy Plant.
4. Select Curdling Factory.
5. Enter the dungeon.
6. Click the nuke button.
7. Resolve level-up card prompts.
8. Read the dungeon minimap.
9. Navigate to all known treasure markers.
10. Probe treasure cell boundaries and open chests.
11. Choose the chest cash-out option.
12. Navigate to the exit marker after all known chests are done.
13. Probe the exit cell boundary.
14. Enter the next floor.
15. Open the pause menu.
16. Abort/end the run through the in-game flow.
17. Confirm the exit dialog.
18. Close the Game Over / end-game button.
19. Close the results summary.
20. Close the achievement/battle summary if present.
21. Return to town.
22. Start the next run automatically when loop mode is enabled.

The bot is designed around observed game UI objects and minimap data. It does not use memory patching for rewards or direct currency changes.

## Hotkeys

Default hotkeys are configured through BepInEx config entries:

- `F8`: toggle the FarmBot on/off.
- `F9`: dump scene and UI objects.
- `F10`: dump map-related objects.
- `F11`: log current state and dungeon debug snapshots.
- `F12`: emergency stop.

When enabled with `F8`, the bot starts from the current town state and proceeds through the full route. With `LoopRuns = true`, it starts another run after returning to town.

## Configuration

The generated config file is:

```text
<GameRoot>/BepInEx/config/com.gong.vampirecrawlers.farmbot.cfg
```

Important options:

- `General.EnabledOnStart`: start automatically when the plugin loads.
- `General.PauseWhenUnfocused`: pause bot ticks when the game window is unfocused.
- `General.LoopRuns`: continue farming after returning to town. Defaults to `true`.
- `Hotkeys.Toggle`: default `F8`.
- `Hotkeys.DumpScene`: default `F9`.
- `Hotkeys.DumpMap`: default `F10`.
- `Hotkeys.Step`: default `F11`.
- `Hotkeys.EmergencyStop`: default `F12`.
- `Stage.WorldName`: configured world name.
- `Stage.StageName`: configured stage name.
- `Navigation.MaxPathRetries`: retry limit for stage/menu navigation.
- `Navigation.MaxMoveFailCount`: movement failure threshold.
- `Timing.UiWaitMs`: delay after UI actions.
- `Timing.MoveWaitMs`: delay after movement actions.
- `Timing.TurnWaitMs`: delay after turns.
- `Timing.StateTimeoutSeconds`: recovery timeout.
- `Rewards.AvoidGem`: avoid gem upgrades.
- `Rewards.SkipIfUncertain`: keep uncertain reward handling conservative.
- `Chest.PreferCashOut`: choose chest cash-out.
- `Chest.ExpectedCashOutGold`: expected cash-out value.

## Build

The project targets `.NET 6` and references BepInEx and IL2CPP interop assemblies from the local game installation. The default project file expects:

```text
C:\Users\gong\GAME\VampireCrawlers
```

Build from the repository root:

```powershell
dotnet build
```

The output DLL is:

```text
bin/Debug/net6.0/VampireCrawlersFarmBot.dll
```

Deploy it to:

```text
<GameRoot>/BepInEx/plugins/VampireCrawlersFarmBot.dll
```

Then restart the game. BepInEx does not hot-reload this DLL during a running game session.

## Runtime Log

The main log is:

```text
<GameRoot>/BepInEx/LogOutput.log
```

Expected startup line:

```text
[FarmBot] FarmBot loaded. Version: 0.1.0
```

Useful run markers:

```text
FarmBot enabled = True
State WaitRunLoaded -> UseNuke
UseNuke: firing attempt #...
State UseNuke -> ResolveLevelUps
Click: LevelUpCard(safe-non-gem)
ReadMinimapSnapshot: valid=True, player=..., chests=..., exit=...
State ReadMap -> SelectNextChest
NavigateToChest: player=..., target=..., next=...
Click: ChestDone(cash-out)
State ... -> NavigateToExit
LocalScanExit: ... treating exit as triggered
State WaitNextFloorLoaded -> OpenPauseMenu
ClickGOFull: HudPause attempt #...
Click: PauseAbortRun
Click: ConfirmExitYes
Click: CloseEndGameScreen
Click: CloseGameOver
Click: CloseBattleStats
FarmBot loop complete; starting next run.
```

## How The Bot Works

### Menu Flow

`GameObserver` locates known UI paths for the town, world map, Dairy Plant, Curdling Factory, and dungeon start button. `FarmBotRunner` drives those UI objects through a state machine.

### Nuke Flow

The nuke button is a custom object, not a normal Unity `Button`. `BotInput.ClickGameObjectFull` sends pointer enter, pointer down, pointer up, pointer click, and submit events to the observed nuke target:

```text
CardGame/Player/Canvas/ShakeContainer/DungeonMovement/3DDungeonMovement/Holder/BombaInfernale/button
```

The bot treats the nuke as successful when level-up UI appears or the nuke UI disappears.

### Level-Up Rewards

The reward logic is conservative:

- It logs all observed reward options.
- It auto-clicks only confirmed non-gem card options.
- It avoids gem cards.
- It waits instead of forcing a risky pick when all options are gem or uncertain.
- It does not auto-skip rewards.

This prevents gem-card modals from trapping the bot in the gem socketing interface.

### Minimap Navigation

`MapObserver` reads `DungeonMinimap` objects:

- `PlayerIcon` gives the player grid cell.
- `MiniMap_TreasureRegular` markers identify chest targets.
- `MiniMap_IconExit` identifies the exit target.
- `MiniMap_Unwalkable*` overlays identify blocked cells.

`Navigator` builds a grid map and plans BFS paths to chests first, then the exit. If a planned forward move does not change the player cell, that edge is recorded as blocked and the path is replanned.

### Chest Handling

Chest markers indicate a target grid cell, not an exact interact position. After reaching a chest cell, the bot probes up to four boundaries:

1. Move forward.
2. If the chest UI opens, cash out.
3. If the probe entered a neighboring cell, move back and mark that boundary as not the chest.
4. Turn right and try the next boundary.

Known chest markers are preserved across minimap reloads so multi-chest maps do not look complete after the first chest marker disappears.

### Exit Handling

The exit is also boundary-triggered. The bot navigates to the exit marker, probes boundaries, and treats either the exit UI/minimap transition or exit marker disappearance as a successful trigger.

After reaching the next floor, the bot waits briefly for the UI to settle, opens the pause menu through the HUD pause button, clicks `_AbortRunButton`, confirms the dialog, and proceeds through the result screens.

### Result Screens

Observed result flow:

1. `MessageBoxManager/.../YesButton/_YesButton`
2. `CardGame/Player/Canvas/EndGameButtons/EndGameButton/_EndGameButton`
3. `CardGame/ResultsSummaryModal/Canvas/QuitButton/_QuitButton`
4. `CardGame/AchievementSummaryModal/Canvas/QuitButton/_QuitButton` when present

The bot handles these in order and then waits for town.

## Main Files

- `Plugin.cs`: BepInEx entry point.
- `BotLogger.cs`: FarmBot logging wrapper.
- `BotConfig.cs`: BepInEx config entries.
- `FarmStateMachine.cs`: state enum and transition logging.
- `FarmBotRunner.cs`: main hotkey and bot state machine.
- `GameObserver.cs`: scene and UI lookup.
- `UiObserver.cs`: UI dump helpers.
- `MapObserver.cs`: minimap and map dumps.
- `Navigator.cs`: grid pathing, chest/exit tracking, blocked edge learning.
- `BotInput.cs`: UI clicks, pointer events, and fallback key taps.
- `RewardPolicy.cs`: reward choice policy.
- `ChestPolicy.cs`: chest cash-out policy.
- `RecoveryManager.cs`: recovery state handling.

## Safety Notes

- `F12` disables the bot immediately.
- `PauseWhenUnfocused` is enabled by default.
- Reward selection avoids gem cards.
- Chest handling prefers cash-out.
- The bot uses observed UI clicks and minimap navigation; it does not alter save files or currency values directly.

## Debugging

Use `F9` for UI and scene dumps when a menu or result screen is not recognized.

Use `F10` for map/object dumps when map markers are unclear.

Use `F11` in the dungeon to dump:

- current state,
- minimap navigation snapshot,
- minimap hierarchy,
- movement buttons,
- nuke hierarchy.

Common clues:

- `UseNuke: nuke click targets not visible`: the nuke path changed or is inactive.
- `ResolveLevelUps: no confirmed safe non-gem card`: manual reward choice is required.
- `NavigateToChest: blocked moving ...`: a hidden wall was learned and pathing should replan.
- `LocalScanChest: ... did not open chest`: the boundary probe is still searching.
- `OpenPauseMenu timed out`: the pause button path or UI timing needs another dump.
- `ConfirmExitToVillage timed out`: confirmation or result-screen paths changed.
