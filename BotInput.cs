using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VampireCrawlersFarmBot
{
    internal sealed class BotInput
    {
        // ── UI clicking ──────────────────────────────────────────────────────

        internal bool ClickButton(Button button, string label = "")
        {
            if (button == null)
            {
                BotLogger.Warn($"ClickButton: null ({label})");
                return false;
            }
            if (!button.interactable)
            {
                BotLogger.Warn($"ClickButton: not interactable ({label})");
                return false;
            }
            try
            {
                BotLogger.Info($"Click: {label}");
                button.onClick.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                BotLogger.Error($"ClickButton failed ({label})", ex);
                return false;
            }
        }

        // Fires a Submit event on the EventSystem's currently selected GameObject —
        // identical to what the Enter key does, without going through onClick.Invoke().
        internal bool SubmitCurrentSelection()
        {
            var es = EventSystem.current;
            if (es == null) { BotLogger.Warn("SubmitCurrentSelection: no EventSystem"); return false; }
            var go = es.currentSelectedGameObject;
            if (go == null) { BotLogger.Warn("SubmitCurrentSelection: nothing selected"); return false; }
            BotLogger.Info($"SubmitCurrentSelection: {go.name}");
            try
            {
                ExecuteEvents.Execute(go, new BaseEventData(es), ExecuteEvents.submitHandler);
                return true;
            }
            catch (Exception ex)
            {
                BotLogger.Error($"SubmitCurrentSelection failed ({go.name})", ex);
                return false;
            }
        }

        // ── Movement stubs (Phase 3+) ─────────────────────────────────────

        internal void MoveForward()  => BotLogger.Debug("STUB: move forward");
        internal void TurnRight()    => BotLogger.Debug("STUB: turn right");
        internal void TurnLeft()     => BotLogger.Debug("STUB: turn left");
        internal void TurnAround()   => BotLogger.Debug("STUB: turn 180°");
        internal void PressEscape()  => BotLogger.Debug("STUB: Escape");
        internal void PressInteract() => BotLogger.Debug("STUB: Interact");
    }
}
