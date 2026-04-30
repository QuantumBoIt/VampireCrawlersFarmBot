using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VampireCrawlersFarmBot
{
    internal enum LocalScanPhase
    {
        ReadyToProbe,
        WaitingForProbeResult,
        BackingOut
    }

    internal sealed class FarmBotRunner : MonoBehaviour
    {
        private bool _enabled;
        private FarmState _lastTickedState = (FarmState)(-1);

        private FarmStateMachine _sm;
        private GameObserver _game;
        private UiObserver _ui;
        private MapObserver _map;
        private BotInput _input;
        private Navigator _nav;
        private RecoveryManager _recovery;
        private RewardPolicy _rewards;
        private ChestPolicy _chest;

        // Per-state timing / misc
        private float _waitUntil;
        private int _swipeCount;
        private float _lastLogTime;
        private bool _triedSubmit;
        private float _nukeClickTime;
        private int _nukeAttempts;

        // Current navigation target (chest or exit marker)
        private MapMarker _navTarget;

        // ExploreGrid state
        private int _exploreMoveCount;
        private bool _exploreInteracted;   // true after interact fired in current room
        private bool _exploreMoved;        // true after MoveForward pressed, awaiting result
        private Vector2 _explorePreMovePos; // minimap player pos before last MoveForward
        private int _wallTurnCount;        // consecutive turns taken after wall hits; 4 = fully stuck
        private bool _navMovePending;
        private GridPos _navMoveFrom;
        private GridPos _navMoveTo;
        private int _localScanAttempts;
        private LocalScanPhase _localScanPhase;
        private GridPos _localScanOrigin;
        private CardinalDir _localScanProbeDir;

        // How close (world units) the player must be to interact with a target.
        // Approximate; depends on the game's tile size. Calibrate after a dump.
        private const float ArrivalDistance = 3f;

        private void Awake()
        {
            _sm = new FarmStateMachine();
            _game = new GameObserver();
            _ui = new UiObserver();
            _map = new MapObserver();
            _input = new BotInput();
            _nav = new Navigator();
            _recovery = new RecoveryManager(_sm);
            _rewards = new RewardPolicy();
            _chest = new ChestPolicy();
            BotLogger.Info("FarmBotRunner initialised.");
        }

        private void Update()
        {
            try
            {
                HandleHotkeys();

                if (!_enabled) return;

                if (BotConfig.Instance.PauseWhenUnfocused.Value && !Application.isFocused)
                    return;

                TickStateMachine();
            }
            catch (Exception ex)
            {
                BotLogger.Error("Unhandled exception in FarmBotRunner.Update", ex);
                try { _recovery?.EnterRecovery("Unhandled exception in Update"); }
                catch { /* swallow to avoid infinite loop */ }
                _enabled = false;
            }
        }

        // ── Hotkeys ──────────────────────────────────────────────────────────

        private static bool KeyPressed(string keyName)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            var key = BotConfig.ParseKey(keyName);
            if (key == Key.None) return false;
            return kb[key].wasPressedThisFrame;
        }

        private void HandleHotkeys()
        {
            var cfg = BotConfig.Instance;

            if (KeyPressed(cfg.Toggle.Value))
            {
                _enabled = !_enabled;
                _sm.TransitionTo(_enabled ? FarmState.ToWorldMap : FarmState.Disabled,
                                 _enabled ? "toggled on" : "toggled off");
                BotLogger.Info($"FarmBot enabled = {_enabled}");
            }

            if (KeyPressed(cfg.DumpScene.Value))
            {
                BotLogger.Info("F9: Dumping scene and UI objects...");
                _game.DumpSceneInfo();
                _ui.DumpUiObjects();
            }

            if (KeyPressed(cfg.DumpMap.Value))
            {
                BotLogger.Info("F10: Dumping map objects...");
                _map.DumpMapObjects();
            }

            if (KeyPressed(cfg.Step.Value))
            {
                BotLogger.Info($"F11 Step: current state = {_sm.CurrentState}");
                if (_game.IsInDungeon())
                {
                    _map.DumpMinimapNavigationSnapshot();
                    _map.DumpMinimapHierarchy();
                    _ui.DumpDungeonMovementButtons();
                    _ui.DumpNukeHierarchy();
                }
            }

            if (KeyPressed(cfg.EmergencyStop.Value))
            {
                _enabled = false;
                _sm.TransitionTo(FarmState.Disabled, "emergency stop");
                BotLogger.Info("FarmBot emergency stopped.");
            }
        }

        // ── State machine ────────────────────────────────────────────────────

        private void TickStateMachine()
        {
            var state = _sm.CurrentState;
            var timeout = BotConfig.Instance.StateTimeoutSeconds.Value;

            if (state != _lastTickedState)
            {
                BotLogger.Debug($"Tick: entering state {state}");
                _lastTickedState = state;
                _triedSubmit = false;
                _navMovePending = false;
                _localScanAttempts = 0;
                _localScanPhase = LocalScanPhase.ReadyToProbe;
                if (state == FarmState.UseNuke)
                {
                    _nukeClickTime = 0f;
                    _nukeAttempts = 0;
                }
                SetWait(BotConfig.Instance.UiWaitMs.Value);
            }

            switch (state)
            {
                case FarmState.Disabled:   break;
                case FarmState.Recovery:   break;

                case FarmState.ToWorldMap:            TickToWorldMap(timeout);                   break;
                case FarmState.SelectDairyPlant:      TickSelectDairyPlant(timeout);             break;
                case FarmState.SelectCurdlingFactory: TickSelectCurdlingFactory(timeout);        break;
                case FarmState.EnterStage:            TickEnterStage(timeout);                   break;
                case FarmState.WaitRunLoaded:         TickWaitRunLoaded(timeout * 3);            break;

                case FarmState.UseNuke:               TickUseNuke(timeout);                      break;
                case FarmState.ResolveLevelUps:       TickResolveLevelUps(timeout);              break;
                case FarmState.ReadMap:               TickReadMap(timeout);                      break;
                case FarmState.ExploreGrid:           TickExploreGrid(timeout);                  break;

                case FarmState.SelectNextChest:       TickSelectNextChest(timeout);              break;
                case FarmState.NavigateToChest:       TickNavigateToChest(timeout * 5);          break;
                case FarmState.LocalScanChest:        TickLocalScanChest(timeout);               break;
                case FarmState.OpenChest:             TickOpenChest(timeout);                    break;
                case FarmState.CashOutChest:          TickCashOutChest(timeout);                 break;
                case FarmState.MarkChestDone:         TickMarkChestDone(timeout);                break;

                case FarmState.NavigateToExit:        TickNavigateToExit(timeout * 5);           break;
                case FarmState.LocalScanExit:         TickLocalScanExit(timeout);                break;
                case FarmState.EnterExit:             TickEnterExit(timeout);                    break;
                case FarmState.OpenExitMenu:          TickOpenExitMenu(timeout);                 break;
                case FarmState.ConfirmExitToVillage:  TickConfirmExitToVillage(timeout);         break;
                case FarmState.CloseGameOver:         TickCloseGameOver(timeout);                break;
                case FarmState.CloseBattleStats:      TickCloseBattleStats(timeout);             break;
                case FarmState.WaitVillageReturned:   TickWaitVillageReturned(timeout * 3);      break;

                default:
                    BotLogger.Warn($"Unknown state: {state}");
                    _recovery.EnterRecovery($"Unknown state {state}");
                    break;
            }
        }

        // ── Phase 2: Menu navigation ─────────────────────────────────────────

        private void TickToWorldMap(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery($"ToWorldMap timed out"); return; }
            if (!WaitDone()) return;
            if (!_game.IsInVillage()) { BotLogger.Debug("ToWorldMap: not in village yet..."); return; }
            var btn = _game.GetWorldMapButton();
            if (btn == null) { BotLogger.Debug("ToWorldMap: WorldMap button not found yet..."); return; }
            _input.ClickButton(btn, "WorldMap");
            _sm.TransitionTo(FarmState.SelectDairyPlant, "WorldMap clicked");
        }

        private void TickSelectDairyPlant(float timeout)
        {
            if (_sm.TimedOut(timeout * 5)) { _recovery.EnterRecovery($"SelectDairyPlant timed out"); return; }
            if (!WaitDone()) return;
            var btn = _game.GetDairyPlantButton();
            if (btn == null)
            {
                if (!_triedSubmit)
                {
                    _triedSubmit = true;
                    BotLogger.Info("SelectDairyPlant: submitting current selection to advance world map");
                    _input.SubmitCurrentSelection();
                    SetWait(BotConfig.Instance.UiWaitMs.Value * 2);
                }
                else
                {
                    LogPeriodic("SelectDairyPlant: waiting for LevelSelect...");
                    SetWait(BotConfig.Instance.UiWaitMs.Value);
                }
                return;
            }
            if (!btn.interactable)
            {
                LogPeriodic("SelectDairyPlant: DairyPlant button visible but not interactable yet...");
                SetWait(BotConfig.Instance.UiWaitMs.Value);
                return;
            }
            if (!_input.ClickButton(btn, "DairyPlant"))
            {
                LogPeriodic("SelectDairyPlant: DairyPlant click failed, retrying...");
                SetWait(BotConfig.Instance.UiWaitMs.Value);
                return;
            }
            _swipeCount = 0;
            _sm.TransitionTo(FarmState.SelectCurdlingFactory, "DairyPlant clicked");
        }

        private void TickSelectCurdlingFactory(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery($"SelectCurdlingFactory timed out"); return; }
            if (!WaitDone()) return;
            if (!_game.IsDairyPlantPanelVisible()) { BotLogger.Debug("SelectCurdlingFactory: stage panel not visible yet..."); return; }
            var startBtn = _game.GetStartDungeonButton();
            if (startBtn != null && startBtn.interactable)
            {
                _sm.TransitionTo(FarmState.EnterStage, "StartDungeonButton ready");
                return;
            }
            if (_swipeCount >= BotConfig.Instance.MaxPathRetries.Value)
            {
                _recovery.EnterRecovery("StartDungeonButton never interactable after max swipes");
                return;
            }
            var swipeBtn = _game.GetRightSwipeButton();
            if (swipeBtn == null) { BotLogger.Debug("SelectCurdlingFactory: RightSwipe not found..."); return; }
            _input.ClickButton(swipeBtn, $"RightSwipe #{_swipeCount + 1}");
            _swipeCount++;
            SetWait(BotConfig.Instance.UiWaitMs.Value);
        }

        private void TickEnterStage(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery($"EnterStage timed out"); return; }
            if (!WaitDone()) return;
            var btn = _game.GetStartDungeonButton();
            if (btn == null || !btn.interactable) { LogPeriodic("EnterStage: StartDungeonButton not ready..."); return; }
            if (!_input.ClickButton(btn, "StartDungeon")) { LogPeriodic("EnterStage: click failed, retrying..."); return; }
            _sm.TransitionTo(FarmState.WaitRunLoaded, "StartDungeon clicked");
        }

        private void TickWaitRunLoaded(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery($"WaitRunLoaded timed out"); return; }
            if (_game.IsInDungeon()) { _sm.TransitionTo(FarmState.UseNuke, "dungeon scene detected"); return; }
            BotLogger.Debug("WaitRunLoaded: waiting for SC_Game...");
        }

        // ── Phase 3: Dungeon — entry ─────────────────────────────────────────

        // Clicks BombaInfernale/button (NukeButton component, not standard Button).
        // Uses ExecuteEvents.Execute with pointerClickHandler so custom components
        // that implement IPointerClickHandler respond correctly.
        private void TickUseNuke(float timeout)
        {
            if (_sm.TimedOut(timeout))
            {
                _recovery.EnterRecovery("UseNuke timed out before detonation could be confirmed");
                return;
            }
            if (!WaitDone()) return;

            if (_triedSubmit)
            {
                if (_game.IsChooseCardModalVisible() || !_game.IsNukeVisible())
                {
                    CompleteSingleRunAfterNuke("nuke detonation confirmed");
                    return;
                }

                if (Time.realtimeSinceStartup - _nukeClickTime < 3f)
                {
                    LogPeriodic("UseNuke: waiting for detonation result...");
                    return;
                }

                _triedSubmit = false;
                SetWait(BotConfig.Instance.UiWaitMs.Value);
                LogPeriodic("UseNuke: no detonation result yet, retrying click...");
                return;
            }

            var targets = _game.GetNukeClickTargets();
            if (targets.Count == 0)
            {
                LogPeriodic("UseNuke: nuke click targets not visible, waiting...");
                return;
            }

            _nukeAttempts++;
            BotLogger.Info($"UseNuke: firing attempt #{_nukeAttempts}, targets={targets.Count}");
            foreach (var target in targets)
                _input.ClickGameObjectFull(target, $"NukeButton attempt #{_nukeAttempts}");

            _triedSubmit = true;
            _nukeClickTime = Time.realtimeSinceStartup;
            SetWait(BotConfig.Instance.UiWaitMs.Value * 2);
        }

        private void CompleteSingleRunAfterNuke(string reason)
        {
            _sm.TransitionTo(FarmState.ResolveLevelUps, reason);
            BotLogger.Info("FarmBot nuke detonated; continuing to level-up resolution and dungeon cleanup.");
        }

        // Handle level-up card choice modal. Click only confirmed non-gem cards.
        // Gem/uncertain choices wait for manual input; Skip is never used here.
        // After all level-ups are resolved, re-scans the map before resuming.
        private void TickResolveLevelUps(float timeout)
        {
            if (!WaitDone()) return;

            if (!_game.IsChooseCardModalVisible())
            {
                _sm.TransitionTo(FarmState.ReadMap, "no more level-up modals");
                return;
            }

            var safeBtn = _game.GetSafeLevelUpCardButton();
            if (safeBtn != null)
            {
                _input.ClickButton(safeBtn, "LevelUpCard(safe-non-gem)");
                SetWait(BotConfig.Instance.UiWaitMs.Value * 2);
                return;
            }

            LogPeriodic("ResolveLevelUps: no confirmed safe non-gem card; waiting for manual card choice...");
            SetWait(BotConfig.Instance.UiWaitMs.Value);
        }

        // Start grid exploration. Chests are not pre-scannable in a Doom-style dungeon
        // (they only activate when the player enters their room), so we always go to
        // ExploreGrid and discover chests in real time as rooms are visited.
        private void TickReadMap(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery("ReadMap timed out"); return; }
            if (!WaitDone()) return;

            _exploreMoveCount = 0;
            _exploreInteracted = false;
            _exploreMoved = false;
            _explorePreMovePos = Vector2.zero;
            _wallTurnCount = 0;

            var snapshot = _map.ReadMinimapSnapshot();
            if (snapshot.IsValid)
            {
                _nav.LoadMinimap(snapshot);
                if (_nav.HasUnvisitedChests())
                    _sm.TransitionTo(FarmState.SelectNextChest, "minimap chests found");
                else
                    _sm.TransitionTo(FarmState.NavigateToExit, "no minimap chests found");
                return;
            }

            _sm.TransitionTo(FarmState.ExploreGrid, "minimap unavailable, falling back to exploration");
        }

        // ── Phase 3b: Grid exploration ───────────────────────────────────────────
        //
        // Move room-by-room pressing Interact in each room. Chest menus that appear
        // after Interact are handled by transitioning to OpenChest (the existing
        // chest-handling states). After MarkChestDone the bot returns here to continue.
        //
        // Navigation pattern: go straight up to 12 steps, then turn right, repeat 4 times.
        // Minimap PlayerIcon position change is used to detect walls (no movement = wall).
        // If minimap is unavailable, all moves are assumed successful.
        private void TickExploreGrid(float timeout)
        {
            const int MaxMoves = 52;   // ~4 × 12 moves + 4 turns; enough for a small dungeon

            if (_sm.TimedOut(timeout * 12))
            {
                _sm.TransitionTo(FarmState.NavigateToExit, "ExploreGrid timed out");
                return;
            }
            if (!WaitDone()) return;

            // Always handle distractions first.
            if (_game.IsChooseCardModalVisible())
            {
                _sm.TransitionTo(FarmState.ResolveLevelUps, "level-up during exploration");
                return;
            }
            if (_game.GetChestDoneButton() != null || _game.GetChestOpenButton() != null)
            {
                BotLogger.Info("ExploreGrid: chest menu visible");
                _sm.TransitionTo(FarmState.OpenChest, "chest in current room");
                return;
            }

            // Phase A — interact in the current room (once per room entry).
            if (!_exploreInteracted)
            {
                _exploreInteracted = true;
                _exploreMoved = false;
                _input.PressInteract();
                BotLogger.Info($"ExploreGrid: interact (rooms visited={_exploreMoveCount})");
                SetWait(BotConfig.Instance.UiWaitMs.Value);
                return;
            }

            // Phase B — check for chest after the interact wait.
            if (!_exploreMoved)
            {
                if (_game.GetChestDoneButton() != null || _game.GetChestOpenButton() != null)
                {
                    _sm.TransitionTo(FarmState.OpenChest, "chest after interact");
                    return;
                }

                if (_exploreMoveCount >= MaxMoves)
                {
                    BotLogger.Info($"ExploreGrid: reached {MaxMoves} moves, exiting");
                    _sm.TransitionTo(FarmState.NavigateToExit, "exploration complete");
                    return;
                }

                // Scheduled turn every 12 straight moves.
                if (_exploreMoveCount > 0 && _exploreMoveCount % 12 == 0)
                {
                    _input.TurnRight();
                    _exploreMoveCount++;
                    _exploreInteracted = false;
                    BotLogger.Info($"ExploreGrid: scheduled turn at step {_exploreMoveCount - 1}");
                    SetWait(BotConfig.Instance.TurnWaitMs.Value);
                    return;
                }

                // Record position and step forward.
                _explorePreMovePos = _game.GetMinimapPlayerPos();
                _input.MoveForward();
                _exploreMoved = true;
                SetWait(BotConfig.Instance.MoveWaitMs.Value);
                return;
            }

            // Phase C — check whether we actually moved (wall detection via minimap).
            var postPos = _game.GetMinimapPlayerPos();
            bool miniMapValid = _explorePreMovePos.magnitude > 0.1f || postPos.magnitude > 0.1f;
            bool didMove = !miniMapValid || Vector2.Distance(postPos, _explorePreMovePos) > 2f;

            if (!didMove)
            {
                _wallTurnCount++;
                BotLogger.Info($"ExploreGrid: wall detected, turning right (step {_exploreMoveCount}, wallTurns={_wallTurnCount})");

                if (_wallTurnCount >= 4)
                {
                    // Tried all 4 directions without moving — truly stuck (input not working or dead end).
                    BotLogger.Info($"ExploreGrid: stuck after 4 turns, advancing to exit");
                    _sm.TransitionTo(FarmState.NavigateToExit, "ExploreGrid stuck");
                    return;
                }

                // Turn right but do NOT reset _exploreInteracted — we already interacted
                // in this room; just turn and try to move again.
                _input.TurnRight();
                _exploreMoved = false;
                SetWait(BotConfig.Instance.TurnWaitMs.Value);
                return;
            }

            // Successfully entered a new room.
            _wallTurnCount = 0;
            _exploreMoveCount++;
            BotLogger.Info($"ExploreGrid: new room (step {_exploreMoveCount}, minimap delta={(postPos - _explorePreMovePos).magnitude:F1})");
            _exploreInteracted = false;
            _exploreMoved = false;
        }

        // ── Phase 4: Chest loop ──────────────────────────────────────────────

        private void TickSelectNextChest(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery("SelectNextChest timed out"); return; }
            if (!WaitDone()) return;

            var snapshot = _map.ReadMinimapSnapshot();
            if (snapshot.IsValid)
                _nav.LoadMinimap(snapshot);

            if (!_nav.HasUnvisitedChests())
            {
                _sm.TransitionTo(FarmState.NavigateToExit, "all chests done");
                return;
            }
            _navTarget = snapshot.IsValid
                ? _nav.GetNextChest(snapshot.Player)
                : _nav.GetNextChest(Vector3.zero);
            if (_navTarget == null)
            {
                _sm.TransitionTo(FarmState.NavigateToExit, "no reachable chests left");
                return;
            }
            BotLogger.Info($"SelectNextChest: targeting {_navTarget.Label} at grid {_navTarget.Pos}");
            _sm.TransitionTo(FarmState.NavigateToChest, $"chest selected: {_navTarget.Label}");
        }

        // Move toward the targeted chest. Checks for level-up modal each tick.
        private void TickNavigateToChest(float timeout)
        {
            if (_sm.TimedOut(timeout))
            {
                if (_navTarget != null) _nav.MarkChestUnreachable(_navTarget);
                _sm.TransitionTo(FarmState.SelectNextChest, "NavigateToChest timed out");
                return;
            }
            if (!WaitDone()) return;

            if (_game.IsChooseCardModalVisible())
            {
                _sm.TransitionTo(FarmState.ResolveLevelUps, "level-up modal during navigation");
                return;
            }

            // Already at chest?
            if (_game.GetChestDoneButton() != null || _game.GetChestOpenButton() != null)
            {
                _sm.TransitionTo(FarmState.LocalScanChest, "chest menu already open");
                return;
            }

            if (_navTarget == null) { _sm.TransitionTo(FarmState.SelectNextChest, "no target"); return; }

            var snapshot = _map.ReadMinimapSnapshot();
            if (!snapshot.IsValid)
            {
                LogPeriodic("NavigateToChest: minimap snapshot unavailable");
                return;
            }
            _nav.LoadMinimap(snapshot);

            if (snapshot.Player.Equals(_navTarget.Pos))
            {
                _sm.TransitionTo(FarmState.LocalScanChest, $"arrived at chest grid {_navTarget.Pos}");
                return;
            }

            NavigateOneGridStep(snapshot, _navTarget.Pos, "NavigateToChest");
        }

        // Chests sit on one of the four boundaries of the current minimap cell.
        // Probe each boundary with MoveForward. If it leads to another cell, move
        // back immediately and try the next direction.
        private void TickLocalScanChest(float timeout)
        {
            if (_sm.TimedOut(timeout))
            {
                if (_navTarget != null) _nav.MarkChestUnreachable(_navTarget);
                _sm.TransitionTo(FarmState.SelectNextChest, "LocalScanChest timed out");
                return;
            }
            if (!WaitDone()) return;

            if (_game.GetChestDoneButton() != null || _game.GetChestOpenButton() != null)
            {
                _sm.TransitionTo(FarmState.OpenChest, "chest menu open");
                return;
            }

            var snapshot = _map.ReadMinimapSnapshot();
            if (!snapshot.IsValid)
            {
                LogPeriodic("LocalScanChest: minimap snapshot unavailable");
                return;
            }

            if (_localScanAttempts >= 4)
            {
                if (_navTarget != null) _nav.MarkChestUnreachable(_navTarget);
                _sm.TransitionTo(FarmState.SelectNextChest, "chest boundary scan failed");
                return;
            }

            if (_localScanPhase == LocalScanPhase.WaitingForProbeResult)
            {
                if (_game.GetChestDoneButton() != null || _game.GetChestOpenButton() != null)
                {
                    _sm.TransitionTo(FarmState.OpenChest, $"chest triggered by probing {_localScanProbeDir}");
                    return;
                }

                if (_navTarget != null && !SnapshotHasChestAt(snapshot, _navTarget.Pos))
                {
                    BotLogger.Info($"LocalScanChest: target marker {_navTarget.Pos} disappeared after probing {_localScanProbeDir}; waiting for chest/cashout UI");
                    _sm.TransitionTo(FarmState.OpenChest, "chest marker disappeared after probe");
                    return;
                }

                if (!snapshot.Player.Equals(_localScanOrigin))
                {
                    BotLogger.Info($"LocalScanChest: {_localScanProbeDir} leads to cell {snapshot.Player}; moving back to {_localScanOrigin}");
                    _input.MoveBack();
                    _localScanPhase = LocalScanPhase.BackingOut;
                    SetWait(BotConfig.Instance.MoveWaitMs.Value);
                    return;
                }

                BotLogger.Info($"LocalScanChest: {_localScanProbeDir} did not open chest and did not leave cell");
                AdvanceLocalScanDirection("LocalScanChest");
                return;
            }

            if (_localScanPhase == LocalScanPhase.BackingOut)
            {
                if (!snapshot.Player.Equals(_localScanOrigin))
                    BotLogger.Warn($"LocalScanChest: backout expected {_localScanOrigin}, still at {snapshot.Player}");
                AdvanceLocalScanDirection("LocalScanChest");
                return;
            }

            _localScanOrigin = snapshot.Player;
            var player = _game.GetPlayerTransform();
            if (player == null)
            {
                LogPeriodic("LocalScanChest: DungeonPlayer not found");
                return;
            }

            _localScanProbeDir = Navigator.GetFacing(player);
            BotLogger.Info($"LocalScanChest: boundary forward probe #{_localScanAttempts + 1}/4, origin={_localScanOrigin}, facing={_localScanProbeDir}");
            _input.MoveForward();
            _localScanPhase = LocalScanPhase.WaitingForProbeResult;
            SetWait(Math.Max(BotConfig.Instance.MoveWaitMs.Value, BotConfig.Instance.UiWaitMs.Value));
        }

        // Choose cash-out or open based on policy; transition to CashOutChest.
        private void TickOpenChest(float timeout)
        {
            if (_sm.TimedOut(timeout))
            {
                _sm.TransitionTo(FarmState.MarkChestDone, "OpenChest timed out");
                return;
            }
            if (!WaitDone()) return;

            var doneBtn = _game.GetChestDoneButton();
            var openBtn = _game.GetChestOpenButton();

            if (doneBtn == null && openBtn == null)
            {
                LogPeriodic("OpenChest: waiting for chest cash-out/open buttons...");
                return;
            }

            if (_chest.ShouldCashOut() && doneBtn != null)
            {
                _input.ClickButton(doneBtn, "ChestDone(cash-out)");
            }
            else if (openBtn != null)
            {
                _input.ClickButton(openBtn, "ChestOpen");
            }
            else if (doneBtn != null)
            {
                _input.ClickButton(doneBtn, "ChestDone");
            }

            SetWait(BotConfig.Instance.UiWaitMs.Value);
            _sm.TransitionTo(FarmState.CashOutChest, "chest button clicked");
        }

        // If the chest was opened (items shown), click DoneButton to close.
        // If already cashed out, this is a quick pass-through.
        private void TickCashOutChest(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _sm.TransitionTo(FarmState.MarkChestDone, "CashOutChest timed out"); return; }
            if (!WaitDone()) return;

            var doneBtn = _game.GetChestDoneButton();
            if (doneBtn != null)
            {
                _input.ClickButton(doneBtn, "ChestDone(close)");
                SetWait(BotConfig.Instance.UiWaitMs.Value);
            }
            _sm.TransitionTo(FarmState.MarkChestDone, "chest closed");
        }

        private void TickMarkChestDone(float timeout)
        {
            if (_navTarget != null)
            {
                _nav.MarkChestDone(_navTarget);
                BotLogger.Info($"MarkChestDone: {_navTarget.Label} done");
                _navTarget = null;
            }

            _exploreInteracted = true;
            _exploreMoved = false;
            _sm.TransitionTo(FarmState.SelectNextChest, "chest done, selecting next minimap target");
        }

        // ── Phase 5: Exit dungeon ─────────────────────────────────────────────

        // Navigate toward the minimap exit marker. Do not use Escape here: that
        // opens the abandon-run menu and skips the level-completion reward.
        private void TickNavigateToExit(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery("NavigateToExit timed out"); return; }
            if (!WaitDone()) return;

            if (_game.IsChooseCardModalVisible())
            {
                _sm.TransitionTo(FarmState.ResolveLevelUps, "level-up modal at exit");
                return;
            }

            var snapshot = _map.ReadMinimapSnapshot();
            if (snapshot.IsValid)
            {
                _nav.LoadMinimap(snapshot);
                if (_nav.CurrentMap.Exit != null)
                {
                    if (snapshot.Player.Equals(_nav.CurrentMap.Exit.Pos))
                    {
                        _sm.TransitionTo(FarmState.LocalScanExit, $"arrived at exit grid {_nav.CurrentMap.Exit.Pos}");
                        return;
                    }
                    NavigateOneGridStep(snapshot, _nav.CurrentMap.Exit.Pos, "NavigateToExit");
                    return;
                }
            }

            LogPeriodic("NavigateToExit: waiting for minimap exit marker; not pressing Escape because that abandons the run.");
            SetWait(BotConfig.Instance.UiWaitMs.Value);
        }

        // Like chests, the exit trigger may be on a cell boundary. Probe each
        // boundary with MoveForward; if it enters a neighboring cell, back out
        // and try the next direction.
        private void TickLocalScanExit(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery("LocalScanExit timed out"); return; }
            if (!WaitDone()) return;

            if (_game.IsExitMenuVisible())
            {
                _sm.TransitionTo(FarmState.EnterExit, "ExitToVillageButton visible");
                return;
            }

            if (_localScanAttempts >= 4)
            {
                _recovery.EnterRecovery("LocalScanExit boundary scan failed; refusing Escape abandon fallback");
                return;
            }

            var snapshot = _map.ReadMinimapSnapshot();
            if (!snapshot.IsValid)
            {
                LogPeriodic("LocalScanExit: minimap snapshot unavailable");
                SetWait(BotConfig.Instance.UiWaitMs.Value);
                return;
            }

            if (_localScanPhase == LocalScanPhase.WaitingForProbeResult)
            {
                if (_game.IsExitMenuVisible())
                {
                    _sm.TransitionTo(FarmState.EnterExit, $"exit triggered by probing {_localScanProbeDir}");
                    return;
                }

                if (!snapshot.Player.Equals(_localScanOrigin))
                {
                    BotLogger.Info($"LocalScanExit: {_localScanProbeDir} leads to cell {snapshot.Player}; moving back to {_localScanOrigin}");
                    _input.MoveBack();
                    _localScanPhase = LocalScanPhase.BackingOut;
                    SetWait(BotConfig.Instance.MoveWaitMs.Value);
                    return;
                }

                BotLogger.Info($"LocalScanExit: {_localScanProbeDir} did not open exit and did not leave cell");
                AdvanceLocalScanDirection("LocalScanExit");
                return;
            }

            if (_localScanPhase == LocalScanPhase.BackingOut)
            {
                if (!snapshot.Player.Equals(_localScanOrigin))
                    BotLogger.Warn($"LocalScanExit: backout expected {_localScanOrigin}, still at {snapshot.Player}");
                AdvanceLocalScanDirection("LocalScanExit");
                return;
            }

            _localScanOrigin = snapshot.Player;
            var player = _game.GetPlayerTransform();
            if (player == null)
            {
                LogPeriodic("LocalScanExit: DungeonPlayer not found");
                return;
            }

            _localScanProbeDir = Navigator.GetFacing(player);
            BotLogger.Info($"LocalScanExit: boundary forward probe #{_localScanAttempts + 1}/4, origin={_localScanOrigin}, facing={_localScanProbeDir}");
            _input.MoveForward();
            _localScanPhase = LocalScanPhase.WaitingForProbeResult;
            SetWait(Math.Max(BotConfig.Instance.MoveWaitMs.Value, BotConfig.Instance.UiWaitMs.Value));
        }

        // Click ExitToVillageButton.
        private void TickEnterExit(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery("EnterExit timed out"); return; }
            if (!WaitDone()) return;

            var btn = _game.GetExitToVillageButton();
            if (btn == null) { LogPeriodic("EnterExit: waiting for ExitToVillageButton..."); return; }
            _input.ClickButton(btn, "ExitToVillage");
            SetWait(BotConfig.Instance.UiWaitMs.Value);
            _sm.TransitionTo(FarmState.OpenExitMenu, "ExitToVillage clicked");
        }

        // Wait for any confirmation dialog after clicking ExitToVillage.
        private void TickOpenExitMenu(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _sm.TransitionTo(FarmState.ConfirmExitToVillage, "OpenExitMenu timed out"); return; }
            if (!WaitDone()) return;
            // If exit button disappeared, the exit was confirmed — advance.
            if (!_game.IsExitMenuVisible())
            {
                _sm.TransitionTo(FarmState.ConfirmExitToVillage, "exit menu closed");
                return;
            }
            // Still showing — click the button again or submit current selection.
            if (!_triedSubmit)
            {
                _triedSubmit = true;
                _input.SubmitCurrentSelection();
                SetWait(BotConfig.Instance.UiWaitMs.Value);
            }
        }

        // Wait for the scene to switch away from SC_Game, then advance.
        private void TickConfirmExitToVillage(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery("ConfirmExitToVillage timed out"); return; }
            if (!WaitDone()) return;
            if (!_game.IsInDungeon())
            {
                _sm.TransitionTo(FarmState.CloseGameOver, "left dungeon scene");
                return;
            }
            LogPeriodic("ConfirmExitToVillage: waiting to leave SC_Game...");
        }

        // ── Phase 6: Post-run cleanup ─────────────────────────────────────────
        // Game-over and battle-stats screen paths are TBD (need in-dungeon F9 dump).

        private void TickCloseGameOver(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _sm.TransitionTo(FarmState.CloseBattleStats, "CloseGameOver timed out"); return; }
            if (!WaitDone()) return;
            if (_game.IsInVillage()) { _sm.TransitionTo(FarmState.WaitVillageReturned, "already in village"); return; }

            var btn = _game.GetCloseGameOverButton();
            if (btn != null)
            {
                _input.ClickButton(btn, "CloseGameOver");
                SetWait(BotConfig.Instance.UiWaitMs.Value);
            }
            // TODO: add proper game-over screen detection from F9 dump
            if (!_triedSubmit)
            {
                _triedSubmit = true;
                _input.SubmitCurrentSelection();
                SetWait(BotConfig.Instance.UiWaitMs.Value);
            }
            if (_sm.TimedOut(3f)) _sm.TransitionTo(FarmState.CloseBattleStats, "CloseGameOver: advancing");
        }

        private void TickCloseBattleStats(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _sm.TransitionTo(FarmState.WaitVillageReturned, "CloseBattleStats timed out"); return; }
            if (!WaitDone()) return;
            if (_game.IsInVillage()) { _sm.TransitionTo(FarmState.WaitVillageReturned, "already in village"); return; }

            var btn = _game.GetCloseBattleStatsButton();
            if (btn != null)
            {
                _input.ClickButton(btn, "CloseBattleStats");
                SetWait(BotConfig.Instance.UiWaitMs.Value);
            }
            // TODO: add battle-stats detection from F9 dump
            if (!_triedSubmit)
            {
                _triedSubmit = true;
                _input.SubmitCurrentSelection();
                SetWait(BotConfig.Instance.UiWaitMs.Value);
            }
        }

        private void TickWaitVillageReturned(float timeout)
        {
            if (_sm.TimedOut(timeout)) { _recovery.EnterRecovery("WaitVillageReturned timed out"); return; }
            if (_game.IsInVillage())
            {
                _enabled = false;
                _sm.TransitionTo(FarmState.Disabled, "back in village after one run");
                BotLogger.Info("FarmBot single-run mode complete; bot disabled.");
                return;
            }
            LogPeriodic("WaitVillageReturned: waiting for SC_TownMap...");
        }

        // ── Navigation helper ────────────────────────────────────────────────

        private bool NavigateOneGridStep(MapSnapshot snapshot, GridPos target, string label)
        {
            var cfg = BotConfig.Instance;

            if (_navMovePending)
            {
                if (snapshot.Player.Equals(_navMoveTo))
                {
                    BotLogger.Info($"{label}: move confirmed {_navMoveFrom} -> {_navMoveTo}");
                }
                else if (snapshot.Player.Equals(_navMoveFrom))
                {
                    BotLogger.Info($"{label}: blocked moving {_navMoveFrom} -> {_navMoveTo}");
                    _nav.MarkEdgeBlocked(_navMoveFrom, _navMoveTo);
                }
                else
                {
                    BotLogger.Info($"{label}: moved unexpectedly {_navMoveFrom} -> {snapshot.Player}, expected {_navMoveTo}");
                }
                _navMovePending = false;
                return false;
            }

            var path = _nav.PlanPath(snapshot.Player, target);
            if (path.Count == 0)
            {
                BotLogger.Warn($"{label}: no grid path from {snapshot.Player} to {target}");
                return false;
            }

            var next = path[0];
            var needed = Navigator.DirectionTo(snapshot.Player, next);
            var playerT = _game.GetPlayerTransform();
            if (playerT == null)
            {
                LogPeriodic($"{label}: DungeonPlayer not found");
                return false;
            }

            var facing = Navigator.GetFacing(playerT);
            int turns = Navigator.TurnsRight(facing, needed);
            BotLogger.Info($"{label}: player={snapshot.Player}, target={target}, next={next}, facing={facing}, needed={needed}, turns={turns}");

            if (turns == 0)
            {
                _input.MoveForward();
                _navMovePending = true;
                _navMoveFrom = snapshot.Player;
                _navMoveTo = next;
                SetWait(cfg.MoveWaitMs.Value);
            }
            else if (turns > 0)
            {
                _input.TurnRight();
                SetWait(cfg.TurnWaitMs.Value);
            }
            else
            {
                _input.TurnLeft();
                SetWait(cfg.TurnWaitMs.Value);
            }
            return true;
        }

        // Execute one movement step (turn or move) toward 'targetWorldPos'.
        private void StepToward(Transform player, Vector3 targetWorldPos)
        {
            var cfg = BotConfig.Instance;
            var facing = Navigator.GetFacing(player);
            var needed = Navigator.DirectionTo(player.position, targetWorldPos);
            int turns = Navigator.TurnsRight(facing, needed);

            if (turns == 0)
            {
                _input.MoveForward();
                SetWait(cfg.MoveWaitMs.Value);
            }
            else if (turns > 0)
            {
                _input.TurnRight();
                SetWait(cfg.TurnWaitMs.Value);
            }
            else
            {
                _input.TurnLeft();
                SetWait(cfg.TurnWaitMs.Value);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private bool WaitDone() => Time.realtimeSinceStartup >= _waitUntil;
        private void SetWait(int ms) => _waitUntil = Time.realtimeSinceStartup + ms / 1000f;

        private string GetCurrentFacingLabel()
        {
            var player = _game.GetPlayerTransform();
            return player == null ? "unknown" : Navigator.GetFacing(player).ToString();
        }

        private static bool SnapshotHasChestAt(MapSnapshot snapshot, GridPos pos)
        {
            foreach (var chest in snapshot.Chests)
                if (chest.Pos.Equals(pos)) return true;
            return false;
        }

        private void AdvanceLocalScanDirection(string label)
        {
            _localScanAttempts++;
            if (_localScanAttempts >= 4)
            {
                _localScanPhase = LocalScanPhase.ReadyToProbe;
                return;
            }

            _input.TurnRight();
            _localScanPhase = LocalScanPhase.ReadyToProbe;
            BotLogger.Info($"{label}: turning right to probe next boundary ({_localScanAttempts}/4)");
            SetWait(BotConfig.Instance.TurnWaitMs.Value);
        }

        private void LogPeriodic(string msg)
        {
            if (Time.realtimeSinceStartup - _lastLogTime < 2f) return;
            _lastLogTime = Time.realtimeSinceStartup;
            BotLogger.Info(msg);
        }

    }
}
