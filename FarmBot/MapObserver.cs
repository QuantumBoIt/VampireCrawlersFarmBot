using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VampireCrawlersFarmBot
{
    internal sealed class MapSnapshot
    {
        internal bool IsValid;
        internal int Width;
        internal int Height;
        internal GridPos Player;
        internal List<MapMarker> Chests = new();
        internal MapMarker Exit;
        internal HashSet<GridPos> BlockedCells = new();
    }

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
            var minimap = FindMinimapObject();
            if (minimap == null) { BotLogger.Warn("DumpMinimap: DungeonMinimap not found"); return; }
            BotLogger.Info("=== Minimap Hierarchy Begin ===");
            DumpTree(minimap.transform, 0);
            BotLogger.Info("=== Minimap Hierarchy End ===");
        }

        internal void DumpMinimapNavigationSnapshot()
        {
            BotLogger.Info("=== Minimap Navigation Snapshot Begin ===");
            try
            {
                var minimap = FindMinimapObject();
                if (minimap == null)
                {
                    BotLogger.Warn("MinimapNav: DungeonMinimap not found");
                    return;
                }

                float cell = InferMinimapCellSize(minimap.transform);
                BotLogger.Info($"MinimapNav: inferred cellSize={cell:F2}");

                var player = FindDescendant(minimap.transform, "PlayerIcon");
                if (player != null)
                    DumpMinimapPoint("player", player, cell, parseGridFromName: false);
                else
                    BotLogger.Warn("MinimapNav: PlayerIcon not found");

                DumpNamedChildren(minimap.transform, "EventIcons", "event", cell, includeInactive: true);
                DumpNamedChildren(minimap.transform, "TileOverlays", "tileOverlay", cell, includeInactive: true);
                DumpNamedChildren(minimap.transform, "DestructibleOverlay", "destructibleOverlay", cell, includeInactive: true);
                DumpMinimapReflection(minimap);
                DumpDungeonMapReflection();
            }
            catch (Exception ex)
            {
                BotLogger.Warn($"MinimapNav: snapshot failed: {ex.Message}");
            }
            finally
            {
                BotLogger.Info("=== Minimap Navigation Snapshot End ===");
            }
        }

        internal MapSnapshot ReadMinimapSnapshot()
        {
            var snapshot = new MapSnapshot();
            var minimap = FindMinimapObject();
            if (minimap == null)
            {
                BotLogger.Warn("ReadMinimapSnapshot: DungeonMinimap not found");
                return snapshot;
            }

            float cell = InferMinimapCellSize(minimap.transform);
            int maxX = -1;
            int maxZ = -1;
            CollectMapData(minimap.transform, cell, snapshot, ref maxX, ref maxZ);
            snapshot.Width = maxX + 1;
            snapshot.Height = maxZ + 1;
            snapshot.IsValid = snapshot.Width > 0 && snapshot.Height > 0;
            BotLogger.Info(
                $"ReadMinimapSnapshot: valid={snapshot.IsValid}, player={snapshot.Player}, " +
                $"chests={snapshot.Chests.Count}, exit={(snapshot.Exit == null ? "none" : snapshot.Exit.Pos.ToString())}, " +
                $"blockedCells={snapshot.BlockedCells.Count}, size={snapshot.Width}x{snapshot.Height}");
            return snapshot;
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

        private static void CollectMapData(Transform t, float cell, MapSnapshot snapshot, ref int maxX, ref int maxZ)
        {
            if (t == null) return;

            if (t.name.StartsWith("Tile (", StringComparison.Ordinal) &&
                TryParseGridName(t.name, out var tx, out var tz))
            {
                if (tx > maxX) maxX = tx;
                if (tz > maxZ) maxZ = tz;
            }
            else if (t.name == "PlayerIcon")
            {
                snapshot.Player = GridFromLocalPosition(t.localPosition, cell);
            }
            else if (t.name.StartsWith("Event (", StringComparison.Ordinal))
            {
                var image = t.GetComponent<Image>();
                var sprite = image != null && image.sprite != null ? image.sprite.name : "";
                if (t.gameObject.activeInHierarchy && sprite == "MiniMap_TreasureRegular" &&
                    TryParseGridName(t.name, out var ex, out var ez))
                {
                    snapshot.Chests.Add(new MapMarker
                    {
                        Pos = new GridPos(ex, ez),
                        Label = t.name
                    });
                }
            }
            else if (t.name.StartsWith("Tile Overlay", StringComparison.Ordinal))
            {
                var image = t.GetComponent<Image>();
                var sprite = image != null && image.sprite != null ? image.sprite.name : "";
                if (t.gameObject.activeInHierarchy && sprite.StartsWith("MiniMap_Unwalkable", StringComparison.Ordinal) &&
                    TryParseGridName(t.name, out var bx, out var bz))
                {
                    snapshot.BlockedCells.Add(new GridPos(bx, bz));
                }
            }
            else if (t.name == "MiniMapEvent(Clone)")
            {
                var image = t.GetComponent<Image>();
                var sprite = image != null && image.sprite != null ? image.sprite.name : "";
                if (t.gameObject.activeInHierarchy && sprite == "MiniMap_IconExit")
                {
                    snapshot.Exit = new MapMarker
                    {
                        Pos = GridFromLocalPosition(t.localPosition, cell),
                        Label = "Exit"
                    };
                }
            }

            for (int i = 0; i < t.childCount; i++)
                CollectMapData(t.GetChild(i), cell, snapshot, ref maxX, ref maxZ);
        }

        private static GridPos GridFromLocalPosition(Vector3 lp, float cell)
            => new GridPos(Mathf.RoundToInt(lp.x / cell), Mathf.RoundToInt(lp.y / cell));

        private static void DumpNamedChildren(Transform root, string containerName, string label, float cell, bool includeInactive)
        {
            var container = FindDescendant(root, containerName);
            if (container == null)
            {
                BotLogger.Info($"MinimapNav: {containerName} not found");
                return;
            }

            int count = 0;
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (!includeInactive && !child.gameObject.activeInHierarchy) continue;
                DumpMinimapPoint(label, child, cell, parseGridFromName: true);
                count++;
            }
            BotLogger.Info($"MinimapNav: {containerName} dumped {count} child marker(s)");
        }

        private static void DumpMinimapPoint(string label, Transform t, float cell, bool parseGridFromName)
        {
            var lp = t.localPosition;
            int gx = Mathf.RoundToInt(lp.x / cell);
            int gy = Mathf.RoundToInt(lp.y / cell);
            if (parseGridFromName && TryParseGridName(t.name, out var nx, out var ny))
            {
                gx = nx;
                gy = ny;
            }

            var image = t.GetComponent<Image>();
            var sprite = image != null && image.sprite != null ? image.sprite.name : "(none)";
            var color = image != null ? FormatColor(image.color) : "(none)";
            BotLogger.Info(
                $"MinimapNav: {label} \"{t.name}\"" +
                $" grid=({gx},{gy})" +
                $" lpos=({lp.x:F1},{lp.y:F1},{lp.z:F1})" +
                $" active={t.gameObject.activeInHierarchy}" +
                $" sprite=\"{sprite}\" color={color}" +
                $" comps={ComponentSummary(t.gameObject)}");
        }

        private static float InferMinimapCellSize(Transform root)
        {
            var xs = new List<float>();
            var ys = new List<float>();
            CollectTilePositions(root, xs, ys);
            var sx = SmallestPositiveDelta(xs);
            var sy = SmallestPositiveDelta(ys);
            if (sx > 0f && sy > 0f) return (sx + sy) * 0.5f;
            if (sx > 0f) return sx;
            if (sy > 0f) return sy;
            return 45.714f;
        }

        private static void CollectTilePositions(Transform t, List<float> xs, List<float> ys)
        {
            if (t == null) return;
            if (t.name.StartsWith("Tile (", StringComparison.Ordinal))
            {
                var lp = t.localPosition;
                xs.Add(lp.x);
                ys.Add(lp.y);
            }
            for (int i = 0; i < t.childCount; i++)
                CollectTilePositions(t.GetChild(i), xs, ys);
        }

        private static float SmallestPositiveDelta(List<float> values)
        {
            if (values.Count < 2) return 0f;
            values.Sort();
            float best = float.MaxValue;
            for (int i = 1; i < values.Count; i++)
            {
                float delta = Mathf.Abs(values[i] - values[i - 1]);
                if (delta > 1f && delta < best) best = delta;
            }
            return best == float.MaxValue ? 0f : best;
        }

        private static bool TryParseGridName(string name, out int x, out int y)
        {
            x = 0;
            y = 0;
            int start = name.IndexOf('(');
            int comma = name.IndexOf(',', start + 1);
            int end = name.IndexOf(')', comma + 1);
            if (start < 0 || comma < 0 || end < 0) return false;
            return int.TryParse(name.Substring(start + 1, comma - start - 1).Trim(), out x) &&
                   int.TryParse(name.Substring(comma + 1, end - comma - 1).Trim(), out y);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendant(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindMinimapObject()
        {
            var active = GameObject.Find("DungeonMinimap");
            if (active != null) return active;

            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
            {
                if (go != null && go.name == "DungeonMinimap")
                    return go;
            }
            return null;
        }

        private static string FormatColor(Color c)
            => $"({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";

        private static void DumpMinimapReflection(GameObject minimap)
        {
            BotLogger.Info("=== Minimap Component Reflection Begin ===");
            foreach (var c in minimap.GetComponents<Component>())
            {
                if (c == null) continue;
                var typeName = SafeTypeName(c);
                if (typeName.IndexOf("Minimap", StringComparison.OrdinalIgnoreCase) >= 0)
                    DumpComponentMembers("MinimapComponent", c, 32);
            }
            BotLogger.Info("=== Minimap Component Reflection End ===");
        }

        private static void DumpDungeonMapReflection()
        {
            BotLogger.Info("=== Dungeon Map Reflection Begin ===");
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
            {
                if (go == null) continue;
                if (go.name != "DungeonEnvironment" && go.name != "DungeonObjects" && go.name != "DungeonMinimap")
                    continue;

                BotLogger.Info($"MapReflect: GO \"{BuildPath(go.transform)}\" active={go.activeInHierarchy} comps={ComponentSummary(go)}");
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var typeName = SafeTypeName(c);
                    var lower = typeName.ToLowerInvariant();
                    if (lower.Contains("dungeon") || lower.Contains("map") || lower.Contains("minimap"))
                        DumpComponentMembers($"MapReflect:{go.name}", c, 40);
                }
            }
            BotLogger.Info("=== Dungeon Map Reflection End ===");
        }

        private static void DumpComponentMembers(string prefix, object obj, int maxMembers)
        {
            var type = obj.GetType();
            BotLogger.Info($"{prefix}: component={SafeTypeName(obj)} managedType={type.FullName}");

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            int count = 0;
            for (var t = type; t != null && count < maxMembers; t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (count >= maxMembers) break;
                    if (field.IsStatic) continue;
                    DumpMember(prefix, field.Name, field.FieldType, () => field.GetValue(obj));
                    count++;
                }

                foreach (var prop in t.GetProperties(flags))
                {
                    if (count >= maxMembers) break;
                    if (prop.GetIndexParameters().Length != 0) continue;
                    var getter = prop.GetGetMethod(true);
                    if (getter == null || getter.IsStatic) continue;
                    DumpMember(prefix, prop.Name, prop.PropertyType, () => prop.GetValue(obj));
                    count++;
                }
            }
        }

        private static void DumpMember(string prefix, string name, Type type, Func<object> getter)
        {
            try
            {
                var value = getter();
                BotLogger.Info($"{prefix}:   {name} ({type.Name}) = {FormatMemberValue(value)}");
            }
            catch (Exception ex)
            {
                BotLogger.Info($"{prefix}:   {name} ({type.Name}) = <error: {ex.GetType().Name}>");
            }
        }

        private static string FormatMemberValue(object value)
        {
            if (value == null) return "null";
            if (value is string s) return $"\"{s}\"";
            if (value is int || value is float || value is double || value is bool || value is Enum)
                return value.ToString();
            if (value is Vector2 v2) return $"Vector2({v2.x:F2},{v2.y:F2})";
            if (value is Vector3 v3) return $"Vector3({v3.x:F2},{v3.y:F2},{v3.z:F2})";
            if (value is Transform tr) return $"Transform({BuildPath(tr)})";
            if (value is GameObject go) return $"GameObject({BuildPath(go.transform)})";
            if (value is Component c) return $"Component({SafeTypeName(c)}:{BuildPath(c.transform)})";
            if (value is ICollection coll) return $"Collection({value.GetType().Name}, Count={coll.Count})";
            if (value is IEnumerable) return $"Enumerable({value.GetType().Name})";
            return $"{SafeTypeName(value)}";
        }

        private static string SafeTypeName(object obj)
        {
            try
            {
                if (obj is Component c)
                    return c.GetIl2CppType()?.Name ?? c.GetType().Name;
                return obj.GetType().Name;
            }
            catch { return "(unknown)"; }
        }

        private static string BuildPath(Transform t)
        {
            if (t == null) return "(null)";
            if (t.parent == null) return t.name;
            return BuildPath(t.parent) + "/" + t.name;
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
