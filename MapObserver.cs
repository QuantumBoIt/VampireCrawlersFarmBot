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

        // Dumps the full DungeonMinimap child hierarchy — call from F11 inside dungeon
        // to learn room-marker names and positions for future chest navigation.
        internal void DumpMinimapHierarchy()
        {
            var minimap = UnityEngine.GameObject.Find("DungeonMinimap");
            if (minimap == null) { BotLogger.Warn("DumpMinimap: DungeonMinimap not found"); return; }
            BotLogger.Info("=== Minimap Hierarchy Begin ===");
            DumpTree(minimap.transform, 0);
            BotLogger.Info("=== Minimap Hierarchy End ===");
        }

        private static void DumpTree(UnityEngine.Transform t, int depth)
        {
            try
            {
                string indent = new string(' ', depth * 2);
                var lp = t.localPosition;
                BotLogger.Info(
                    $"  {indent}\"{t.name}\"" +
                    $"  lpos=({lp.x:F1},{lp.y:F1},{lp.z:F1})" +
                    $"  active={t.gameObject.activeInHierarchy}" +
                    $"  comps={ComponentSummary(t.gameObject)}");
                for (int i = 0; i < t.childCount; i++)
                    DumpTree(t.GetChild(i), depth + 1);
            }
            catch (System.Exception ex) { BotLogger.Warn($"DumpTree error: {ex.Message}"); }
        }

        // Returns active TreasureChestCard transforms that are not pooled (y not ≈1000).
        // In a Doom-style grid dungeon chests only activate when the player enters their
        // room, so this returns non-empty only when the player is currently in a chest room.
        internal System.Collections.Generic.List<Transform> FindChestTransforms()
        {
            var result = new System.Collections.Generic.List<Transform>();
            try
            {
                foreach (var go in UnityEngine.Object.FindObjectsOfType<UnityEngine.GameObject>(true))
                {
                    try
                    {
                        if (!go.name.StartsWith("TreasureChestCard")) continue;
                        if (!go.activeInHierarchy) continue;
                        var pos = go.transform.position;
                        if (UnityEngine.Mathf.Abs(pos.y - 1000f) < 50f) continue; // pooled
                        result.Add(go.transform);
                        BotLogger.Info($"FindChestTransforms: active chest {go.name} at {pos}");
                    }
                    catch { }
                }
            }
            catch (System.Exception ex) { BotLogger.Warn($"FindChestTransforms error: {ex.Message}"); }
            BotLogger.Info($"FindChestTransforms: found {result.Count} active chest(s)");
            return result;
        }

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
