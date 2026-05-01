using UnityEngine;

namespace VampireCrawlersFarmBot
{
    internal enum FarmState
    {
        Disabled,

        // Enter stage
        ToWorldMap,
        SelectDairyPlant,
        SelectCurdlingFactory,
        EnterStage,
        WaitRunLoaded,

        // In-stage
        UseNuke,
        ResolveLevelUps,
        ReadMap,
        ExploreGrid,       // systematic room-by-room dungeon exploration

        // Chest loop (reached from ExploreGrid when chest menu opens)
        SelectNextChest,
        NavigateToChest,
        LocalScanChest,
        OpenChest,
        CashOutChest,
        MarkChestDone,

        // Exit
        NavigateToExit,
        LocalScanExit,
        EnterExit,

        // Post-stage
        WaitNextFloorLoaded,
        OpenPauseMenu,
        ClickPauseExitGame,
        ConfirmExitToVillage,
        CloseGameOver,
        CloseBattleStats,
        WaitVillageReturned,

        // Error handling
        Recovery
    }

    internal sealed class FarmStateMachine
    {
        internal FarmState CurrentState { get; private set; } = FarmState.Disabled;
        internal float LastTransitionTime { get; private set; }

        internal void TransitionTo(FarmState next, string reason = "")
        {
            if (CurrentState == next) return;
            var prev = CurrentState;
            CurrentState = next;
            LastTransitionTime = Time.realtimeSinceStartup;
            BotLogger.Info($"State {prev} → {next}{(string.IsNullOrEmpty(reason) ? "" : $" ({reason})")}");
        }

        internal bool TimedOut(float timeoutSeconds) =>
            Time.realtimeSinceStartup - LastTransitionTime > timeoutSeconds;
    }
}
