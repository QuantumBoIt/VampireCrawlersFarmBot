using System;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

                DumpLevelSelectDetails();

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

        private static void DumpLevelSelectDetails()
        {
            var root = GameObject.Find("LevelSelect/LevelSelectUI");
            if (root == null || !root.activeInHierarchy) return;

            BotLogger.Info("=== F9 LevelSelect Detail Begin ===");

            var es = EventSystem.current;
            var selected = es == null ? null : es.currentSelectedGameObject;
            BotLogger.Info($"  EventSystem selected: {(selected == null ? "(null)" : BuildPath(selected.transform))}");

            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
            {
                if (go == null || !go.activeInHierarchy) continue;
                var path = BuildPath(go.transform);
                var lower = path.ToLowerInvariant();
                if (!lower.Contains("levelselect/levelselectui")) continue;
                if (!lower.Contains("ui_maplocations_sublevelinfo") &&
                    !lower.Contains("ui_maplocations_stageinfopanel") &&
                    !lower.Contains("arrowrightbutton") &&
                    !lower.Contains("arrowleftbutton") &&
                    !lower.Contains("startdungeonbutton"))
                    continue;

                var rect = go.GetComponent<RectTransform>();
                var pos = rect == null ? "" : $" rect=({rect.position.x:F1},{rect.position.y:F1}) size=({rect.rect.width:F1},{rect.rect.height:F1})";
                var btn = go.GetComponent<Button>();
                var interactable = btn == null ? "" : $" interactable={btn.interactable}";
                BotLogger.Info($"  LevelSelect: {path} active={go.activeInHierarchy}{interactable}{pos} text='{Trim(ReadTextBlob(go))}' comps={ComponentSummary(go)}");

                foreach (var c in go.GetComponents<Component>())
                    DumpInterestingComponent(c, "    ");
            }

            BotLogger.Info("=== F9 LevelSelect Detail End ===");
        }

        private static void DumpInterestingComponent(Component c, string indent)
        {
            if (c == null) return;
            string typeName;
            try { typeName = c.GetIl2CppType()?.Name ?? c.GetType().Name; }
            catch { typeName = c.GetType().Name; }

            var lower = typeName.ToLowerInvariant();
            if (!lower.Contains("sublevellocationinfo") &&
                !lower.Contains("stageinfopanel") &&
                !lower.Contains("pointerevents") &&
                !lower.Contains("button"))
                return;

            BotLogger.Info($"{indent}Component {typeName}: {DumpSimpleMembers(c)}");
        }

        private static string DumpSimpleMembers(object obj)
        {
            var sb = new StringBuilder();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = obj.GetType();

            foreach (var f in type.GetFields(flags))
            {
                if (sb.Length > 400) break;
                try
                {
                    var value = f.GetValue(obj);
                    if (IsSimple(value))
                        sb.Append(f.Name).Append('=').Append(value ?? "null").Append("; ");
                }
                catch { }
            }

            foreach (var p in type.GetProperties(flags))
            {
                if (sb.Length > 700) break;
                try
                {
                    if (p.GetIndexParameters().Length != 0) continue;
                    var value = p.GetValue(obj);
                    if (IsSimple(value))
                        sb.Append(p.Name).Append('=').Append(value ?? "null").Append("; ");
                }
                catch { }
            }

            if (sb.Length == 0) return "(no simple members)";
            return sb.ToString();
        }

        private static bool IsSimple(object value)
        {
            if (value == null) return true;
            var t = value.GetType();
            return t.IsPrimitive || value is string || value is Enum;
        }

        private static string ReadTextBlob(GameObject root)
        {
            var sb = new StringBuilder();
            try
            {
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                    if (text != null && !string.IsNullOrEmpty(text.text))
                        sb.Append(text.text).Append(' ');
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    if (text != null && !string.IsNullOrEmpty(text.text))
                        sb.Append(text.text).Append(' ');
            }
            catch { }
            return sb.ToString();
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Replace("\r", " ").Replace("\n", " ");
            return value.Length <= 120 ? value : value.Substring(0, 120);
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
