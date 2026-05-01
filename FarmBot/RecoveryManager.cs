using System;
using System.Collections.Generic;
using UnityEngine;

namespace VampireCrawlersFarmBot
{
    internal sealed class ActionRecord
    {
        internal float Timestamp;
        internal FarmState State;
        internal string Action;
        internal string Result;
    }

    // Phase 1 stub: logs recovery reason and sets state to Recovery.
    internal sealed class RecoveryManager
    {
        private const int HistorySize = 20;
        private readonly Queue<ActionRecord> _history = new();
        private readonly FarmStateMachine _sm;

        internal RecoveryManager(FarmStateMachine sm) => _sm = sm;

        internal void Record(FarmState state, string action, string result)
        {
            _history.Enqueue(new ActionRecord
            {
                Timestamp = Time.realtimeSinceStartup,
                State = state,
                Action = action,
                Result = result
            });
            while (_history.Count > HistorySize)
                _history.Dequeue();
        }

        internal void EnterRecovery(string reason, Exception ex = null)
        {
            BotLogger.Warn($"Recovery triggered: {reason}");
            if (ex != null) BotLogger.Error("Exception during recovery trigger", ex);
            DumpHistory();
            _sm.TransitionTo(FarmState.Recovery, reason);
        }

        private void DumpHistory()
        {
            BotLogger.Info("--- Recent action history ---");
            foreach (var r in _history)
                BotLogger.Info($"  [{r.Timestamp:F1}s] {r.State} | {r.Action} → {r.Result}");
            BotLogger.Info("--- End action history ---");
        }
    }
}
