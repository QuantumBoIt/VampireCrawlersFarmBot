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
