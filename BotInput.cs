using UnityEngine;

namespace VampireCrawlersFarmBot
{
    // Phase 1 stub: all methods log intent only — no real input is sent.
    internal sealed class BotInput
    {
        internal void PressKey(KeyCode key) =>
            BotLogger.Debug($"STUB: would press key {key}");

        internal void MoveForward() =>
            BotLogger.Debug("STUB: would move forward");

        internal void TurnRight() =>
            BotLogger.Debug("STUB: would turn right");

        internal void TurnLeft() =>
            BotLogger.Debug("STUB: would turn left");

        internal void TurnAround() =>
            BotLogger.Debug("STUB: would turn 180°");

        internal void ClickButton(string buttonName) =>
            BotLogger.Debug($"STUB: would click button \"{buttonName}\"");

        internal void ClickScreenPosition(float x, float y) =>
            BotLogger.Debug($"STUB: would click screen pos ({x:F0},{y:F0})");

        internal void PressEscape() =>
            BotLogger.Debug("STUB: would press Escape");

        internal void PressInteract() =>
            BotLogger.Debug("STUB: would press Interact (E)");
    }
}
