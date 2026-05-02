# VampireCrawlersFarmBot

`VampireCrawlersFarmBot` is a BepInEx IL2CPP mod for Vampire Crawlers. It adds a hotkey-driven farming bot that can enter a configured stage, detonate the dungeon nuke, resolve safe level-up choices, collect chest cash-outs, trigger the floor exit, settle the run through the normal result screens, and loop from town.

Earlier development used separate helper mods as reference code. They are no longer included in this project, so the built DLL only exposes the FarmBot plugin.

## Current Capabilities

The FarmBot currently supports the configured Dairy Plant route. By default it selects the Dairy Plant world and the Curdling Factory stage. The world and stage names are configurable, and the Dairy Plant sub-stage picker refreshes the game's pointer-driven highlighted row before entering the stage.

1. Start from the town map.
2. Open the world map.
3. Select the configured world, currently Dairy Plant.
4. Select the configured stage, default Curdling Factory.
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
- `F9`: dump scene and UI objects. Requires `VerboseLogging = true` to print detailed output.
- `F10`: dump map-related objects. Requires `VerboseLogging = true` to print detailed output.
- `F11`: log current state and dungeon debug snapshots. Requires `VerboseLogging = true` for full snapshots.
- `F12`: emergency stop.

When enabled with `F8`, the bot starts from the current town state and proceeds through the full route. With `LoopRuns = true`, it starts another run after returning to town. A run watchdog aborts back to town if an in-dungeon run exceeds the configured time limit.

## Configuration

The generated config file is:

```text
<GameRoot>/BepInEx/config/com.your_user.vampirecrawlers.farmbot.cfg
```

Important options:

- `General.EnabledOnStart`: start automatically when the plugin loads.
- `General.PauseWhenUnfocused`: pause bot ticks when the game window is unfocused.
- `General.LoopRuns`: continue farming after returning to town. Defaults to `true`.
- `General.VerboseLogging`: enable detailed development logs and dump output. Defaults to `false`.
- `General.LogLevel`: detailed log level used only when `VerboseLogging = true`. Defaults to `Warn` for long-running farming.
- `Hotkeys.Toggle`: default `F8`.
- `Hotkeys.DumpScene`: default `F9`.
- `Hotkeys.DumpMap`: default `F10`.
- `Hotkeys.Step`: default `F11`.
- `Hotkeys.EmergencyStop`: default `F12`.
- `Stage.WorldName`: configured world name.
- `Stage.StageName`: configured stage name inside the selected world. Current known Dairy Plant stages include `乳品厂`, `牛奶厂`, and `凝乳厂`.
- `Navigation.MaxPathRetries`: retry limit for stage/menu navigation.
- `Navigation.MaxMoveFailCount`: movement failure threshold.
- `Timing.UiWaitMs`: delay after UI actions.
- `Timing.MoveWaitMs`: delay after movement actions.
- `Timing.TurnWaitMs`: delay after turns.
- `Timing.StateTimeoutSeconds`: recovery timeout.
- `Timing.RunWatchdogSeconds`: maximum in-dungeon run time before aborting to village. Defaults to `90`; set `0` to disable. This is intentionally earlier than the `NavigateToExit` state timeout (`StateTimeoutSeconds * 7`, about 140s by default).
- `Rewards.AvoidGem`: avoid gem upgrades.
- `Rewards.SkipIfUncertain`: keep uncertain reward handling conservative.
- `Chest.PreferCashOut`: choose chest cash-out.
- `Chest.ExpectedCashOutGold`: expected cash-out value.

## Build

The project targets `.NET 6` and references BepInEx and IL2CPP interop assemblies from the local game installation. The default project file expects:

```text
C:\Users\xxxx\GAME\VampireCrawlers
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

Normal farming keeps detailed run markers muted by default. With `VerboseLogging = false`, the log should stay mostly limited to startup, `F8` enable/disable, emergency stop, warnings, and errors. For long-running farming, keep `VerboseLogging = false` or use `VerboseLogging = true` with `LogLevel = Warn`.

When `VerboseLogging = true`, useful run markers include:

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

`GameObserver` locates known UI paths for the town, world map, Dairy Plant world, stage selection panel, and dungeon start button. `FarmBotRunner` drives those UI objects through a state machine.

World selection uses the observed world map panel and right-swipe button until the configured `Stage.WorldName` is visible. Inside the Dairy Plant sub-stage list, the bot identifies the target row from UI text, then sends staged keyboard `Down` presses followed by `Enter/Submit`. This mirrors the reliable manual flow where the bold highlighted row controls which sub-stage enters the dungeon.

### Current Limitations

- Generalize world/stage selection beyond the currently observed Dairy Plant UI paths. `Stage.StageName` can switch between known stages in the Dairy Plant world, but `Stage.WorldName` is still backed by the observed WorldMap -> DairyPlant path.

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

All bot source files live under `FarmBot/`.

- `FarmBot/Plugin.cs`: BepInEx entry point.
- `FarmBot/BotLogger.cs`: FarmBot logging wrapper with quiet and verbose modes.
- `FarmBot/BotConfig.cs`: BepInEx config entries.
- `FarmBot/FarmStateMachine.cs`: state enum and transition logging.
- `FarmBot/FarmBotRunner.cs`: main hotkey and bot state machine.
- `FarmBot/GameObserver.cs`: scene and UI lookup.
- `FarmBot/UiObserver.cs`: UI dump helpers.
- `FarmBot/MapObserver.cs`: minimap and map dumps.
- `FarmBot/Navigator.cs`: grid pathing, chest/exit tracking, blocked edge learning.
- `FarmBot/BotInput.cs`: UI clicks, pointer events, staged key taps, and movement input.
- `FarmBot/RewardPolicy.cs`: reward choice policy.
- `FarmBot/ChestPolicy.cs`: chest cash-out policy.
- `FarmBot/RecoveryManager.cs`: recovery state handling.

## Safety Notes

- `F12` disables the bot immediately.
- `PauseWhenUnfocused` is enabled by default.
- Reward selection avoids gem cards.
- Chest handling prefers cash-out.
- The bot uses observed UI clicks and minimap navigation; it does not alter save files or currency values directly.

## Debugging

Detailed logs are disabled by default to keep `BepInEx/LogOutput.log` readable during normal farming.

To re-enable development logging, edit the generated BepInEx config and set:

```ini
[General]
VerboseLogging = true
LogLevel = Debug
```

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


## TODO

### Done

- [x] Phase 1 infrastructure: BepInEx plugin metadata, logger/config wrappers, state machine, observers, hotkeys, and F9/F10/F11 dump tooling.
- [x] Full farming loop: town -> world map -> configured world -> configured stage -> dungeon -> nuke -> rewards -> chests -> exit -> settlement -> town.
- [x] Loop mode after returning to town with `General.LoopRuns`.
- [x] Configurable world/stage selection through `Stage.WorldName` and `Stage.StageName`.
- [x] World selection by cycling the right arrow; this is valid because all worlds loop.
- [x] Dairy Plant sub-stage selection fix using real pointer delta before click, avoiding the bug where `onClick` played a sound but did not update the selected stage.
- [x] Safe level-up selection that avoids gem cards, preventing gem-embed UI stalls.
- [x] Multi-chest handling: detect known chest markers, navigate to each, probe boundaries, open chest, and choose cash-out.
- [x] Exit handling: navigate to exit marker, probe boundary, enter next floor, then settle through the normal pause/exit/results flow.
- [x] Minimap-based navigation with blocked-edge learning for hidden walls.
- [x] Long-run watchdog: `Timing.RunWatchdogSeconds` aborts back to village if a dungeon run takes too long.
- [x] Quiet long-run logging: `VerboseLogging = false` suppresses large `Info`/`Debug` output; `Warn`/`Error` remain visible.
- [x] Local `memory.md` added for future agents and ignored by git.

### Still Open

- [ ] 引爆核弹逻辑优化（现在要三次）
- [ ] 寻路算法优化
- [ ] 界面切换优化， 现在有些地方的切换还不够高效
