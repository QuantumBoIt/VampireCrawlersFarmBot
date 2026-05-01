using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VampireCrawlersFarmBot
{
    internal sealed class UiObserver
    {
        private static readonly string[] Keywords =
        {
            "ui", "canvas", "panel", "button", "text", "label", "menu", "dialog",
            "reward", "chest", "exit", "village", "world", "map", "stage",
            "confirm", "complete", "gameover", "game_over", "battle", "popup",
            "hud", "overlay", "screen", "window", "modal"
        };

        internal void DumpUiObjects()
        {
            BotLogger.Info("=== F9 UI Dump Begin ===");
            try
            {
                int hits = 0;
                foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
                {
                    try
                    {
                        var lower = go.name.ToLowerInvariant();
                        if (!MatchesAny(lower, Keywords)) continue;
                        var path = BuildPath(go.transform);
                        var active = go.activeInHierarchy ? "active" : "inactive";
                        BotLogger.Info($"  UI: [{active}] {path}  comps={ComponentSummary(go)}");
                        hits++;
                    }
                    catch (Exception ex)
                    {
                        BotLogger.Warn($"  UI: error reading object '{SafeName(go)}': {ex.Message}");
                    }
                }

                BotLogger.Info("  Canvas roots:");
                foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
                {
                    try
                    {
                        BotLogger.Info($"    Canvas \"{canvas.name}\" renderMode={canvas.renderMode} active={canvas.gameObject.activeInHierarchy}");
                        var t = canvas.transform;
                        for (int i = 0; i < t.childCount; i++)
                        {
                            try
                            {
                                var child = t.GetChild(i);
                                BotLogger.Info($"      child \"{child.name}\" active={child.gameObject.activeInHierarchy}");
                            }
                            catch { /* skip unreadable child */ }
                        }
                    }
                    catch (Exception ex)
                    {
                        BotLogger.Warn($"    Canvas error: {ex.Message}");
                    }
                }

                BotLogger.Info($"=== F9 UI Dump End ({hits} keyword hits) ===");
            }
            catch (Exception ex)
            {
                BotLogger.Error("DumpUiObjects failed", ex);
            }
        }

        // Dumps the full child hierarchy of BombaInfernale so we can find the
        // cover/lid GameObject that must be clicked before the red nuke button.
        internal void DumpNukeHierarchy()
        {
            const string nukePath =
                "CardGame/Player/Canvas/ShakeContainer/DungeonMovement/3DDungeonMovement/Holder/BombaInfernale";
            BotLogger.Info("=== F11 Nuke Hierarchy Dump Begin ===");
            var go = GameObject.Find(nukePath);
            if (go == null)
            {
                BotLogger.Warn($"  DumpNukeHierarchy: not found: {nukePath}");
                BotLogger.Info("=== F11 Nuke Hierarchy Dump End (missing) ===");
                return;
            }
            DumpFullTree(go.transform, 0);
            BotLogger.Info("=== F11 Nuke Hierarchy Dump End ===");
        }

        private static void DumpFullTree(Transform t, int depth)
        {
            try
            {
                string indent = new string(' ', depth * 2);
                var lp = t.localPosition;
                BotLogger.Info(
                    $"  {indent}\"{t.name}\"" +
                    $"  active={t.gameObject.activeInHierarchy}" +
                    $"  lpos=({lp.x:F1},{lp.y:F1},{lp.z:F1})" +
                    $"  comps={ComponentSummary(t.gameObject)}");
                for (int i = 0; i < t.childCount; i++)
                    DumpFullTree(t.GetChild(i), depth + 1);
            }
            catch (Exception ex) { BotLogger.Warn($"  DumpFullTree error: {ex.Message}"); }
        }

        // Dumps every Button (or component whose name contains "button") under the
        // 3DDungeonMovement hierarchy so we can discover the forward/turn button paths.
        // Call from F11 while standing in a dungeon room.
        internal void DumpDungeonMovementButtons()
        {
            const string parentPath =
                "CardGame/Player/Canvas/ShakeContainer/DungeonMovement/3DDungeonMovement";
            BotLogger.Info("=== F11 Movement Button Dump Begin ===");
            var parent = GameObject.Find(parentPath);
            if (parent == null)
            {
                BotLogger.Warn($"  DumpMovementButtons: parent not found: {parentPath}");
                BotLogger.Info("=== F11 Movement Button Dump End (parent missing) ===");
                return;
            }
            DumpButtonsUnder(parent.transform, 0);
            BotLogger.Info("=== F11 Movement Button Dump End ===");
        }

        private static void DumpButtonsUnder(Transform t, int depth)
        {
            try
            {
                var comps = t.GetComponents<Component>();
                bool hasButton = false;
                foreach (var c in comps)
                {
                    try
                    {
                        var n = c?.GetIl2CppType()?.Name ?? c?.GetType().Name ?? "";
                        if (n.ToLowerInvariant().Contains("button")) { hasButton = true; break; }
                    }
                    catch { }
                }
                if (hasButton || t.GetComponent<Button>() != null)
                {
                    string indent = new string(' ', depth * 2);
                    var btn = t.GetComponent<Button>();
                    string interactable = btn != null ? btn.interactable.ToString() : "n/a";
                    BotLogger.Info(
                        $"  {indent}\"{BuildPath(t)}\"" +
                        $"  active={t.gameObject.activeInHierarchy}" +
                        $"  interactable={interactable}" +
                        $"  comps={ComponentSummary(t.gameObject)}");
                }
                for (int i = 0; i < t.childCount; i++)
                    DumpButtonsUnder(t.GetChild(i), depth + 1);
            }
            catch (Exception ex) { BotLogger.Warn($"  DumpButtonsUnder error: {ex.Message}"); }
        }

        private static bool MatchesAny(string lower, string[] kws)
        {
            foreach (var kw in kws)
                if (lower.Contains(kw)) return true;
            return false;
        }

        private static string BuildPath(Transform t)
        {
            try
            {
                if (t.parent == null) return t.name;
                return BuildPath(t.parent) + "/" + t.name;
            }
            catch
            {
                return t.name;
            }
        }

        private static string ComponentSummary(GameObject go)
        {
            try
            {
                var comps = go.GetComponents<Component>();
                var sb = new StringBuilder();
                foreach (var c in comps)
                {
                    try
                    {
                        if (c == null) continue;
                        // In IL2CPP, use Il2CppType for actual type name
                        var typeName = c.GetIl2CppType()?.Name ?? c.GetType().Name;
                        sb.Append(typeName).Append(' ');
                    }
                    catch { sb.Append("? "); }
                }
                return sb.ToString().TrimEnd();
            }
            catch
            {
                return "(error)";
            }
        }

        private static string SafeName(GameObject go)
        {
            try { return go.name; }
            catch { return "(unknown)"; }
        }
    }
}
