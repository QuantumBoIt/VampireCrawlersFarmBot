# VampireCrawlersFarmBot Plan

## Current Goal

Make one manual FarmBot run enter the selected stage and successfully detonate the nuke.
After the nuke is confirmed, stop the bot. Do not continue movement, chest handling,
exit handling, or looped farming yet.

## Current Status

- Plugin loads through BepInEx and logs `FarmBot loaded`.
- F8 toggles the FarmBot state machine.
- F9 dumps scene/UI objects.
- F10 dumps map-related objects.
- F11 logs current state and, in dungeon, dumps minimap, movement buttons, and nuke hierarchy.
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
  - Disable bot after confirmation.
- Single-run mode is active. Returning to village also disables the bot instead of starting another run.

## Confirmed From Latest Log

- F11 confirms the nuke object path:
  `CardGame/Player/Canvas/ShakeContainer/DungeonMovement/3DDungeonMovement/Holder/BombaInfernale/button`
- The clickable child is active and interactable.
- Its component list includes `NukeButton`.
- The parent `BombaInfernale` contains decorative/sound children, but the real target is the `button` child.
- The bot reached `UseNuke`, retried full UI clicks, then logged:
  `State UseNuke -> Disabled (nuke detonation confirmed)`
- This means the single-run stop condition worked after the nuke was considered detonated.

## Near-Term Cleanup

- Done: narrow nuke clicking to the confirmed `BombaInfernale/button` target first.
- Done: keep parent/fallback targets only as fallback when the direct target is unavailable.
- Done: reduce noisy `DairyPlant not interactable` spam during world-map selection.
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
   - `FarmBot single-run target complete: nuke detonated; bot disabled.`
7. If the nuke still does not fire, press F11 while in the dungeon and inspect the nuke hierarchy dump.

## Failure Clues

- If there is no `UseNuke: firing attempt`, the bot did not reach the dungeon or did not detect `SC_Game`.
- If there is `UseNuke: nuke click targets not visible`, the `BombaInfernale` path is wrong or inactive.
- If there are `ClickGOFull` lines but no detonation, the target object exists but needs a different event/method.
- If detonation works but the bot continues moving, `CompleteSingleRunAfterNuke` or single-run disabling regressed.

## Deferred Work

- Resolve level-up choices after nuke.
- Read dungeon map/minimap reliably.
- Implement movement and wall handling.
- Find and open chests.
- Cash out chests.
- Find/trigger dungeon exit.
- Return to village cleanly.
- Add config option for looped runs only after the full run is stable.
