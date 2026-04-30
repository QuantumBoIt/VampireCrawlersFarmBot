using System;
using System.Text;
using UnityEngine;

namespace VampireCrawlersFarmBot
{
    internal sealed class MapObserver
    {
        private static readonly string[] Keywords =
        {
            "map", "player", "chest", "exit", "shovel", "dungeon", "nuke",
            "bomb", "marker", "icon", "pointer", "position", "grid", "cell",
            "minimap", "navigator", "pathfind", "room", "corridor", "door",
            "entrance", "treasure", "loot", "reward", "spawn"
        };

        internal void DumpMapObjects()
        {
            BotLogger.Info("=== F10 Map Dump Begin ===");
            try
            {
                int hits = 0;
                foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
                {
                    try
                    {
                        var lower = go.name.ToLowerInvariant();
                        if (!MatchesAny(lower, Keywords)) continue;

                        var active = go.activeInHierarchy ? "active" : "inactive";
                        var pos = go.transform.position;
                        var rot = go.transform.eulerAngles;
                        BotLogger.Info(
                            $"  Map: [{active}] \"{go.name}\"" +
                            $"  pos=({pos.x:F2},{pos.y:F2},{pos.z:F2})" +
                            $"  rot=({rot.x:F0},{rot.y:F0},{rot.z:F0})" +
                            $"  comps={ComponentSummary(go)}");
                        hits++;
                    }
                    catch (Exception ex)
                    {
                        BotLogger.Warn($"  Map: error reading object: {ex.Message}");
                    }
                }

                BotLogger.Info($"=== F10 Map Dump End ({hits} keyword hits) ===");
            }
            catch (Exception ex)
            {
                BotLogger.Error("DumpMapObjects failed", ex);
            }
        }

        private static bool MatchesAny(string lower, string[] kws)
        {
            foreach (var kw in kws)
                if (lower.Contains(kw)) return true;
            return false;
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
    }
}
