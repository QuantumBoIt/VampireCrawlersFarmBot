using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

        // Fires a pointer-click event on any GameObject, including those with custom
        // components (e.g. NukeButton) that implement IPointerClickHandler instead of
        // extending Button.
        internal bool ClickGameObject(GameObject go, string label = "")
        {
            if (go == null) { BotLogger.Warn($"ClickGameObject: null ({label})"); return false; }
            try
            {
                BotLogger.Info($"ClickGO: {label}");
                var es = EventSystem.current;
                var ptrData = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(go, ptrData, ExecuteEvents.pointerClickHandler);
                return true;
            }
            catch (Exception ex)
            {
                BotLogger.Error($"ClickGameObject failed ({label})", ex);
                return false;
            }
        }

        internal bool ClickGameObjectFull(GameObject go, string label = "")
        {
            if (go == null) { BotLogger.Warn($"ClickGameObjectFull: null ({label})"); return false; }
            var es = EventSystem.current;
            if (es == null) { BotLogger.Warn($"ClickGameObjectFull: no EventSystem ({label})"); return false; }

            try
            {
                var ptrData = BuildPointerData(es, go);
                BotLogger.Info($"ClickGOFull: {label} -> {BuildPath(go.transform)}");
                es.SetSelectedGameObject(go);

                ExecuteEvents.ExecuteHierarchy(go, ptrData, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.ExecuteHierarchy(go, ptrData, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(go, ptrData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(go, ptrData, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.ExecuteHierarchy(go, new BaseEventData(es), ExecuteEvents.submitHandler);
                return true;
            }
            catch (Exception ex)
            {
                BotLogger.Error($"ClickGameObjectFull failed ({label})", ex);
                return false;
            }
        }

        private static PointerEventData BuildPointerData(EventSystem es, GameObject go)
        {
            var data = new PointerEventData(es)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                eligibleForClick = true,
                pointerPress = go,
                rawPointerPress = go,
                pointerClick = go
            };

            var rect = go.GetComponent<RectTransform>();
            if (rect != null)
            {
                Camera cam = null;
                var canvas = go.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = canvas.worldCamera;
                data.position = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
            }
            return data;
        }

        private static string BuildPath(Transform t)
        {
            if (t == null) return "(null)";
            if (t.parent == null) return t.name;
            return BuildPath(t.parent) + "/" + t.name;
        }

        // Fires a Submit event on the EventSystem's currently selected object —
        // same code path as the Enter key, avoids direct onClick.Invoke on dialogs.
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

        // ── Keyboard simulation (Win32 P/Invoke) ────────────────────────────
        // Injects keystrokes at the OS level; Unity's InputSystem reads them
        // normally as long as the game window is focused.

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;

        internal void TapKey(Key key)
        {
            int vk = KeyToVk(key);
            if (vk <= 0) { BotLogger.Warn($"TapKey: no VK mapping for {key}"); return; }
            BotLogger.Debug($"TapKey: {key} (0x{vk:X2})");
            try
            {
                keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
                keybd_event((byte)vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                BotLogger.Error($"TapKey {key} failed", ex);
            }
        }

        private static int KeyToVk(Key key)
        {
            if (key >= Key.A && key <= Key.Z)          return 0x41 + (key - Key.A);
            if (key >= Key.Digit1 && key <= Key.Digit9) return 0x31 + (key - Key.Digit1);
            if (key == Key.Digit0) return 0x30;
            return key switch
            {
                Key.Escape     => 0x1B,
                Key.Enter      => 0x0D,
                Key.Space      => 0x20,
                Key.Tab        => 0x09,
                Key.UpArrow    => 0x26,
                Key.DownArrow  => 0x28,
                Key.LeftArrow  => 0x25,
                Key.RightArrow => 0x27,
                Key.F1  => 0x70, Key.F2  => 0x71, Key.F3  => 0x72, Key.F4  => 0x73,
                Key.F5  => 0x74, Key.F6  => 0x75, Key.F7  => 0x76, Key.F8  => 0x77,
                Key.F9  => 0x78, Key.F10 => 0x79, Key.F11 => 0x7A, Key.F12 => 0x7B,
                _ => -1
            };
        }

        // ── Dungeon movement via UI buttons ──────────────────────────────────
        //
        // This Doom-style dungeon uses ContextSensitiveButtonView — movement is
        // driven by UI button clicks, NOT keyboard input. We search for active
        // interactable Buttons under 3DDungeonMovement whose names match a hint list.
        // Falls back to keybd_event only if no matching button is found.

        private const string MovementRootPath =
            "CardGame/Player/Canvas/ShakeContainer/DungeonMovement/3DDungeonMovement";
        private const string MovementButtonsPath = MovementRootPath + "/Holder/MovementArrows(ugly)";

        internal bool MoveForward() => TryClickMovementButton(
            new[] { "Movement_Move_Forward" },
            new[] { "move_forward", "forward" },
            BotConfig.ParseKey(BotConfig.Instance.MoveForward.Value),
            "MoveForward");

        internal bool MoveBack() => TryClickMovementButton(
            new[] { "Movement_Move_Backward (yuk)" },
            new[] { "move_backward", "backward" },
            BotConfig.ParseKey(BotConfig.Instance.MoveBack.Value),
            "MoveBack");

        internal bool TurnLeft() => TryClickMovementButton(
            new[] { "Movement_Turn_Left" },
            new[] { "turn_left", "turnleft" },
            BotConfig.ParseKey(BotConfig.Instance.TurnLeft.Value),
            "TurnLeft");

        internal bool TurnRight() => TryClickMovementButton(
            new[] { "Movement_Turn_Right" },
            new[] { "turn_right", "turnright" },
            BotConfig.ParseKey(BotConfig.Instance.TurnRight.Value),
            "TurnRight");

        internal void PressInteract() => TapKey(BotConfig.ParseKey(BotConfig.Instance.Interact.Value));
        internal void PressEscape()   => TapKey(BotConfig.ParseKey(BotConfig.Instance.OpenMenu.Value));

        // Prefer confirmed button names from F11 dumps. Keyword fallback is strict
        // because every movement button starts with "Movement_".
        // Returns true if a button was found and clicked; false otherwise (uses key fallback).
        private bool TryClickMovementButton(string[] exactNames, string[] hints, Key fallbackKey, string label)
        {
            var root = GameObject.Find(MovementButtonsPath) ?? GameObject.Find(MovementRootPath);
            if (root != null)
            {
                var btn = FindButtonByExactName(root.transform, exactNames) ?? FindButtonByHint(root.transform, hints);
                if (btn != null)
                {
                    BotLogger.Info($"MovBtn: {label} → \"{btn.gameObject.name}\"");
                    btn.onClick.Invoke();
                    return true;
                }
                BotLogger.Info($"MovBtn: {label} — no UI button matched hints [{string.Join(",", hints)}], trying key");
            }
            else
            {
                BotLogger.Info($"MovBtn: {label} — movement root not found, trying key");
            }
            TapKey(fallbackKey);
            return false;
        }

        private static Button FindButtonByExactName(Transform parent, string[] names)
        {
            var btn = parent.GetComponent<Button>();
            if (btn != null && btn.interactable && btn.gameObject.activeInHierarchy)
            {
                foreach (var n in names)
                    if (parent.name == n) return btn;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindButtonByExactName(parent.GetChild(i), names);
                if (found != null) return found;
            }
            return null;
        }

        private static Button FindButtonByHint(Transform parent, string[] hints)
        {
            var btn = parent.GetComponent<Button>();
            if (btn != null && btn.interactable && btn.gameObject.activeInHierarchy)
            {
                var lower = parent.name.ToLowerInvariant();
                foreach (var h in hints)
                    if (lower.Contains(h)) return btn;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindButtonByHint(parent.GetChild(i), hints);
                if (found != null) return found;
            }
            return null;
        }
    }
}
