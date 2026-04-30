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

        private void TickStateMachine()
        {
            var state = _sm.CurrentState;

            if (state != _lastTickedState)
            {
                BotLogger.Debug($"Tick: entering state {state}");
                _lastTickedState = state;
            }

            var timeout = BotConfig.Instance.StateTimeoutSeconds.Value;

            switch (state)
            {
                case FarmState.Disabled:
                    break;

                case FarmState.ToWorldMap:
                    TickStub(state, timeout);
                    break;

                case FarmState.SelectDairyPlant:
                    TickStub(state, timeout);
                    break;

                case FarmState.SelectCurdlingFactory:
                    TickStub(state, timeout);
                    break;

                case FarmState.EnterStage:
                    TickStub(state, timeout);
                    break;

                case FarmState.WaitRunLoaded:
                    TickStub(state, timeout);
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

        private void TickStub(FarmState state, float timeoutSeconds)
        {
            if (_sm.TimedOut(timeoutSeconds))
                _recovery.EnterRecovery($"State {state} timed out after {timeoutSeconds}s (stub — not yet implemented)");
        }
    }
}
