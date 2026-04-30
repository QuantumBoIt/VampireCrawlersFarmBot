# VampireCrawlersFarmBot Plan

## Current Goal

Make one manual FarmBot run enter the selected stage, detonate the nuke, resolve level-up
cards, explore the dungeon, cash out discovered chests, find/trigger exit, return to village,
and stop. Looped farming remains disabled until this full single run is stable.

## Current Status

- Plugin loads through BepInEx and logs `FarmBot loaded`.
- F8 toggles the FarmBot state machine.
- F9 dumps scene/UI objects.
- F10 dumps map-related objects.
- F11 logs current state and, in dungeon, dumps:
  - minimap navigation snapshot,
  - full minimap hierarchy,
  - movement buttons,
  - nuke hierarchy.
- F12 emergency-stops the bot.
- Stage entry flow is implemented:
  - Town map
  - World map
  - Dairy Plant
  - Curdling Factory
  - Start dungeon
- Current nuke flow:
  - Wait for `SC_Game`.
  - Find `BombaInfernale` nuke click targets.
  - Send full UI event sequence to each candidate target.
  - Wait for confirmation signal:
    - level-up card modal appears, or
    - nuke UI disappears.
  - Continue to `ResolveLevelUps` after confirmation.
- Post-nuke flow is now enabled:
  - Level-up selection is conservative automatic:
    - log every option as `LevelUpOption`,
    - auto-click only confirmed non-gem cards,
    - wait for manual input if all options are gem/uncertain,
    - never skip automatically.
  - Read/reset exploration state.
  - Read minimap `PlayerIcon`, `MiniMap_TreasureRegular`, `MiniMap_IconExit`, and `MiniMap_Unwalkable*`.
  - Use minimap BFS navigation to target treasure first, then exit.
  - Learn hidden blocked edges when a planned forward move does not change the minimap cell.
  - On arrival at a treasure grid, probe the four cell boundaries:
    face boundary, move forward once, cash out if chest menu opens, otherwise
    move back if the probe entered a neighboring cell, then turn right and try
    the next boundary. Each treasure gets at most four boundary probes.
  - Preserve all previously seen minimap treasure markers across minimap reloads,
    so a map with two chests does not become "complete" after the first marker
    disappears during cash-out.
  - Fall back to room exploration only if the minimap snapshot is unavailable.
  - Detect chest menus.
  - Prefer `DoneButton` cash-out for chests.
  - Navigate to the minimap exit marker after all known chests are done.
  - The bot no longer presses Escape as an exit fallback during dungeon cleanup,
    because that abandons the run without the stage-completion reward.
  - Exit grids now use the same four-direction forward boundary probe as chests.
    Only after the real exit/completion modal appears does the bot click
    `ExitToVillage`.
- Single-run mode is active. Returning to village disables the bot instead of starting another run.

## Confirmed From Latest Log

- F11 confirms the nuke object path:
  `CardGame/Player/Canvas/ShakeContainer/DungeonMovement/3DDungeonMovement/Holder/BombaInfernale/button`
- The clickable child is active and interactable.
- Its component list includes `NukeButton`.
- The parent `BombaInfernale` contains decorative/sound children, but the real target is the `button` child.
- Earlier log confirmed the bot reached `UseNuke`, retried full UI clicks, then logged:
  `State UseNuke -> Disabled (nuke detonation confirmed)`
- That proved detonation detection worked. The current build now transitions from `UseNuke`
  to `ResolveLevelUps` instead of stopping immediately.
- Latest movement failure root cause:
  `MoveForward` was clicking `Movement_Turn_Left` because the loose hint `move` matched
  every `Movement_*` button. Movement lookup now prefers exact F11-confirmed button names.
- Latest gem-card log showed global/hidden gem objects (`AnimatedGemView`, `GemTriggeredVFX`),
  but the actual selectable gem options are reliably exposed as a `GemCard` child under a
  `... gem view` option. Normal cards are exposed as `Card_A_...` / `Card_S_...` with `CardView`.
  Current code auto-clicks normal cards only and waits on gem/uncertain options.
- Latest minimap log confirms an 11x11 `DungeonMinimap` grid:
  - tiles are named `Tile (x, y)`,
  - cell spacing is about `45.7`,
  - `PlayerIcon` local position maps to the current grid cell,
  - `EventIcons/Event (x, y)` gives candidate event targets,
  - treasure is `Event (x,y)` with sprite `MiniMap_TreasureRegular`,
  - exit is `MiniMapEvent(Clone)` with sprite `MiniMap_IconExit`,
  - unwalkable cells are `Tile Overlay (x,y)` with sprite `MiniMap_Unwalkable*`.
- Latest chest log confirms the bot can route to the treasure grid, but treasure
  interaction is boundary/forward-trigger based. A chest marker at `Event (x, y)`
  means "stand in that grid cell and probe the four boundaries with forward
  movement", not "press interact once in the current facing". If a probe moves to
  another cell, the bot backs out and tries the next boundary.

## Near-Term Cleanup

- Done: narrow nuke clicking to the confirmed `BombaInfernale/button` target first.
- Done: keep parent/fallback targets only as fallback when the direct target is unavailable.
- Done: reduce noisy `DairyPlant not interactable` spam during world-map selection.
- Done: re-enable safe automatic non-gem level-up picks after gem-card evidence confirmed `GemCard`.
- Done: add F11 minimap navigation snapshot with player grid, event markers, overlays,
  sprite/color, and shallow dungeon map component reflection.
- Done: first minimap navigation pass:
  - BFS to nearest visible treasure,
  - cash out chest,
  - repeat remaining treasures,
  - BFS to exit,
  - mark hidden blocked edges and replan.
- Done: boundary scan pass:
  - treasure grids use four-direction forward probes before marking unreachable,
  - if a forward probe enters another cell, move back and continue scanning,
  - completed/unreachable chest positions persist across minimap reloads,
  - known chest markers are merged across minimap reloads for multi-chest maps,
  - exit grids use four-direction forward probes,
  - Escape fallback was removed from normal exit navigation to avoid forced
    no-reward village returns,
  - completed chests return to minimap target selection instead of old exploration.
- Keep looped farming disabled until movement, rewards, chests, and exit are reliable.

## Important Files

- `Plugin.cs`: BepInEx entry point.
- `FarmBotRunner.cs`: hotkeys and state machine.
- `GameObserver.cs`: scene/UI object lookup, including nuke target discovery.
- `BotInput.cs`: UI clicks, full pointer event clicks, keyboard taps.
- `UiObserver.cs`: UI, movement button, and nuke hierarchy dumps.
- `MapObserver.cs`: map/minimap dumps.
- `Navigator.cs`: later navigation work.

## Next Test

1. Build with `dotnet build`.
2. Copy `bin/Debug/net6.0/VampireCrawlersFarmBot.dll` to:
   `C:\Users\gong\GAME\VampireCrawlers\BepInEx\plugins\VampireCrawlersFarmBot.dll`
3. Restart the game.
4. Open `C:\Users\gong\GAME\VampireCrawlers\BepInEx\LogOutput.log`.
5. Press F8 once in town.
6. Watch for these log lines:
   - `FarmBot enabled = True`
   - `State WaitRunLoaded -> UseNuke`
   - `UseNuke: firing attempt #...`
   - `ClickGOFull: NukeButton attempt #...`
   - `FarmBot nuke detonated; continuing to level-up resolution and dungeon cleanup.`
   - `State UseNuke -> ResolveLevelUps`
   - `LevelUpOption: ... safe=... gem=... reason=...`
   - `Click: LevelUpCard(safe-non-gem)` when at least one safe normal card exists.
   - `ResolveLevelUps: no confirmed safe non-gem card; waiting for manual card choice...`
     if all visible options are gem/uncertain.
   - `State ResolveLevelUps -> ReadMap`
   - `ReadMinimapSnapshot: valid=True, player=..., chests=..., exit=...`
   - `State ReadMap -> SelectNextChest` when treasure exists
   - `Navigator: path ...`
   - `NavigateToChest: player=..., target=..., next=...`
   - `NavigateToChest: blocked moving ...` if a hidden wall is found
   - `LocalScanChest: boundary forward probe #.../4`
   - `LocalScanChest: ... leads to cell ...; moving back ...` when that side is
     a connected path rather than a chest trigger
   - `Click: ChestDone(cash-out)` after a chest boundary opens the menu
   - `State MarkChestDone -> SelectNextChest`
   - `State ... -> NavigateToExit`
   - `LocalScanExit: boundary forward probe #.../4`
   - `State LocalScanExit -> EnterExit` only after probing the real exit boundary
   - `State ... -> WaitVillageReturned`
   - `FarmBot single-run mode complete; bot disabled.`
7. If the nuke still does not fire, press F11 while in the dungeon and inspect the nuke hierarchy dump.
8. For navigation work, press F11 when the minimap shows chest/exit markers and keep:
   - `MinimapNav: player ... grid=(x,y)`
   - `MinimapNav: event ... grid=(x,y) ... sprite=... color=...`
   - `MinimapNav: tileOverlay ...`
   - `MinimapNav: destructibleOverlay ...`
   - `MapReflect: ...`

## Failure Clues

- If there is no `UseNuke: firing attempt`, the bot did not reach the dungeon or did not detect `SC_Game`.
- If there is `UseNuke: nuke click targets not visible`, the `BombaInfernale` path is wrong or inactive.
- If there are `ClickGOFull` lines but no detonation, the target object exists but needs a different event/method.
- The bot must not auto-skip level-up cards.
- The bot must not auto-pick `gem=True` options. If it does, keep nearby `LevelUpOption`
  and `Click: LevelUpCard(safe-non-gem)` lines.
- If the bot does not resume after manual card choice, the `ChooseCardModal` path likely changed or another modal remains open.
- If movement does not occur, inspect F11 movement button dump and `MovBtn:` logs.
  Expected forward movement log: `MovBtn: MoveForward -> "Movement_Move_Forward"`.
- If navigation goes the wrong direction, inspect:
  - `NavigateToChest: ... facing=... needed=...`
  - `move confirmed`
  - `moved unexpectedly`
  This means the yaw-to-minimap direction mapping needs adjustment.
- If chests are visible but not cashed out, inspect F9/F11 for `MenuCanvas`, `DoneButton`, and `OpenButton`.
- If exit does not work, capture F9 after exploration reaches the exit or after pressing Escape.
- If minimap events cannot be classified, compare F11 logs at three moments:
  when a chest marker is visible, when standing in a chest room, and when the exit marker/menu is visible.

## Deferred Work

- Improve level-up reward policy from "first confirmed safe non-gem" to ranked card choices.
- Add optional Skip fallback only after enough gem-card logs confirm safe conditions.
- Improve dungeon movement/pathing beyond the current simple room sweep.
- Refine minimap navigation after test logs:
  - confirm yaw-to-grid direction mapping,
  - classify any additional treasure tiers,
  - improve behavior when standing on treasure but chest menu does not open.
- Confirm exit UI paths and post-run Game Over / battle data close buttons from logs.
- Add config option for looped runs only after the full run is stable.
