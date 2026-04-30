using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VampireCrawlersFarmBot
{
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

        // Per-state timing
        private float _waitUntil;
        private int _swipeCount;
        private float _lastLogTime;
        private bool _triedSubmit;

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
                // Brief wait at the start of every new state to let the game UI settle
                SetWait(BotConfig.Instance.UiWaitMs.Value);
            }

            switch (state)
            {
                case FarmState.Disabled:
                    break;

                case FarmState.ToWorldMap:
                    TickToWorldMap(timeout);
                    break;

                case FarmState.SelectDairyPlant:
                    TickSelectDairyPlant(timeout);
                    break;

                case FarmState.SelectCurdlingFactory:
                    TickSelectCurdlingFactory(timeout);
                    break;

                case FarmState.EnterStage:
                    TickEnterStage(timeout);
                    break;

                case FarmState.WaitRunLoaded:
                    TickWaitRunLoaded(timeout * 3);
                    break;

                case FarmState.UseNuke:
                    TickStub(state, timeout);
                    break;

                case FarmState.ResolveLevelUps:
                    TickStub(state, timeout);
                    break;

                case FarmState.ReadMap:
                    TickStub(state, timeout);
                    break;

                case FarmState.SelectNextChest:
                    TickStub(state, timeout);
                    break;

                case FarmState.NavigateToChest:
                    TickStub(state, timeout);
                    break;

                case FarmState.LocalScanChest:
                    TickStub(state, timeout);
                    break;

                case FarmState.OpenChest:
                    TickStub(state, timeout);
                    break;

                case FarmState.CashOutChest:
                    TickStub(state, timeout);
                    break;

                case FarmState.MarkChestDone:
                    TickStub(state, timeout);
                    break;

                case FarmState.NavigateToExit:
                    TickStub(state, timeout);
                    break;

                case FarmState.LocalScanExit:
                    TickStub(state, timeout);
                    break;

                case FarmState.EnterExit:
                    TickStub(state, timeout);
                    break;

                case FarmState.OpenExitMenu:
                    TickStub(state, timeout);
                    break;

                case FarmState.ConfirmExitToVillage:
                    TickStub(state, timeout);
                    break;

                case FarmState.CloseGameOver:
                    TickStub(state, timeout);
                    break;

                case FarmState.CloseBattleStats:
                    TickStub(state, timeout);
                    break;

                case FarmState.WaitVillageReturned:
                    TickStub(state, timeout);
                    break;

                case FarmState.Recovery:
                    break;

                default:
                    BotLogger.Warn($"Unknown state: {state}");
                    _recovery.EnterRecovery($"Unknown state {state}");
                    break;
            }
        }

        // ── Phase 2: Menu navigation ─────────────────────────────────────────

        // Wait for the WorldMap button to appear in the town carousel and click it.
        private void TickToWorldMap(float timeout)
        {
            if (_sm.TimedOut(timeout))
            {
                _recovery.EnterRecovery($"ToWorldMap timed out after {timeout}s");
                return;
            }
            if (!WaitDone()) return;
            if (!_game.IsInVillage())
            {
                BotLogger.Debug("ToWorldMap: not in village scene yet...");
                return;
            }
            var btn = _game.GetWorldMapButton();
            if (btn == null)
            {
                BotLogger.Debug("ToWorldMap: WorldMap button not found yet...");
                return;
            }
            _input.ClickButton(btn, "WorldMap");
            _sm.TransitionTo(FarmState.SelectDairyPlant, "WorldMap clicked");
        }

        // Wait for LevelSelect to open and click the DairyPlant world button.
        // Uses timeout*5 because LevelSelect animation can take several seconds on first open.
        private void TickSelectDairyPlant(float timeout)
        {
            if (_sm.TimedOut(timeout * 5))
            {
                _recovery.EnterRecovery($"SelectDairyPlant timed out after {timeout * 5}s");
                return;
            }
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
                }
                return;
            }
            // "not interactable" means it is already selected — still OK to advance
            _input.ClickButton(btn, "DairyPlant");
            _swipeCount = 0;
            _sm.TransitionTo(FarmState.SelectCurdlingFactory, "DairyPlant clicked");
        }

        // Wait for the stage info panel and swipe right until StartDungeonButton is enabled.
        private void TickSelectCurdlingFactory(float timeout)
        {
            if (_sm.TimedOut(timeout))
            {
                _recovery.EnterRecovery($"SelectCurdlingFactory timed out after {timeout}s");
                return;
            }
            if (!WaitDone()) return;
            if (!_game.IsDairyPlantPanelVisible())
            {
                BotLogger.Debug("SelectCurdlingFactory: stage panel not visible yet...");
                return;
            }
            var startBtn = _game.GetStartDungeonButton();
            if (startBtn != null && startBtn.interactable)
            {
                _sm.TransitionTo(FarmState.EnterStage, "StartDungeonButton ready");
                return;
            }
            if (_swipeCount >= BotConfig.Instance.MaxPathRetries.Value)
            {
                _recovery.EnterRecovery("StartDungeonButton never became interactable after max swipes");
                return;
            }
            var swipeBtn = _game.GetRightSwipeButton();
            if (swipeBtn == null)
            {
                BotLogger.Debug("SelectCurdlingFactory: RightSwipe button not found...");
                return;
            }
            _input.ClickButton(swipeBtn, $"RightSwipe #{_swipeCount + 1}");
            _swipeCount++;
            SetWait(BotConfig.Instance.UiWaitMs.Value);
        }

        // Click StartDungeonButton to enter the stage.
        private void TickEnterStage(float timeout)
        {
            if (_sm.TimedOut(timeout))
            {
                _recovery.EnterRecovery($"EnterStage timed out after {timeout}s");
                return;
            }
            if (!WaitDone()) return;
            var btn = _game.GetStartDungeonButton();
            if (btn == null || !btn.interactable)
            {
                LogPeriodic("EnterStage: StartDungeonButton not ready...");
                return;
            }
            if (!_input.ClickButton(btn, "StartDungeon"))
            {
                // Button GO may be mid-animation (interactable=true but inactive hierarchy)
                LogPeriodic("EnterStage: click failed, retrying...");
                return;
            }
            _sm.TransitionTo(FarmState.WaitRunLoaded, "StartDungeon clicked");
        }

        // Wait until the scene switches to the dungeon (SC_Game).
        private void TickWaitRunLoaded(float timeout)
        {
            if (_sm.TimedOut(timeout))
            {
                _recovery.EnterRecovery($"WaitRunLoaded timed out after {timeout}s");
                return;
            }
            if (_game.IsInDungeon())
            {
                _sm.TransitionTo(FarmState.UseNuke, "dungeon scene detected");
                return;
            }
            BotLogger.Debug("WaitRunLoaded: waiting for SC_Game...");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private bool WaitDone() => Time.realtimeSinceStartup >= _waitUntil;
        private void SetWait(int ms) => _waitUntil = Time.realtimeSinceStartup + ms / 1000f;

        // Log at Info level but throttle to once every 2 seconds to avoid log spam.
        private void LogPeriodic(string msg)
        {
            if (Time.realtimeSinceStartup - _lastLogTime < 2f) return;
            _lastLogTime = Time.realtimeSinceStartup;
            BotLogger.Info(msg);
        }

        private void TickStub(FarmState state, float timeoutSeconds)
        {
            if (_sm.TimedOut(timeoutSeconds))
                _recovery.EnterRecovery($"State {state} timed out after {timeoutSeconds}s (stub — not yet implemented)");
        }
    }
}
