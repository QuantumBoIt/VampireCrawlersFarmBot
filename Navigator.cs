using System.Collections.Generic;
using UnityEngine;

namespace VampireCrawlersFarmBot
{
    internal enum CardinalDir { North, East, South, West }

    internal struct GridPos
    {
        internal int X, Z;
        public override string ToString() => $"({X},{Z})";
    }

    internal sealed class MapMarker
    {
        internal GridPos Pos;
        internal Vector3 WorldPos;
        internal string Label;
        internal bool Done;
        internal bool Unreachable;
    }

    internal sealed class DungeonMap
    {
        internal GridPos PlayerPos;
        internal CardinalDir PlayerFacing;
        internal List<MapMarker> Chests = new();
        internal MapMarker Exit;
    }

    internal sealed class Navigator
    {
        internal DungeonMap CurrentMap { get; } = new DungeonMap();

        // ── Chest tracking ───────────────────────────────────────────────────

        internal void SetChests(List<Transform> transforms)
        {
            CurrentMap.Chests.Clear();
            foreach (var t in transforms)
            {
                CurrentMap.Chests.Add(new MapMarker
                {
                    WorldPos = t.position,
                    Label    = t.name
                });
            }
            BotLogger.Info($"Navigator: loaded {CurrentMap.Chests.Count} chests");
        }

        internal bool HasUnvisitedChests()
            => CurrentMap.Chests.Exists(c => !c.Done && !c.Unreachable);

        // Returns the nearest unvisited chest to playerPos, or null.
        internal MapMarker GetNextChest(Vector3 playerPos)
        {
            MapMarker best = null;
            float bestDist = float.MaxValue;
            foreach (var c in CurrentMap.Chests)
            {
                if (c.Done || c.Unreachable) continue;
                float d = Vector3.Distance(playerPos, c.WorldPos);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        // ── Direction helpers ────────────────────────────────────────────────

        // Cardinal direction the player is currently facing (yaw-based).
        internal static CardinalDir GetFacing(Transform player)
        {
            float yaw = player.eulerAngles.y % 360f;
            if (yaw < 0) yaw += 360f;
            if (yaw <  45f || yaw >= 315f) return CardinalDir.North;
            if (yaw < 135f)                return CardinalDir.East;
            if (yaw < 225f)                return CardinalDir.South;
            return CardinalDir.West;
        }

        // Cardinal direction from 'from' toward 'to' (dominant horizontal axis).
        internal static CardinalDir DirectionTo(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            if (Mathf.Abs(dx) >= Mathf.Abs(dz))
                return dx >= 0 ? CardinalDir.East : CardinalDir.West;
            return dz >= 0 ? CardinalDir.North : CardinalDir.South;
        }

        // Returns how many 90° right-turns are needed to face 'target'.
        // -1 = one left, 0 = already facing, 1 = one right, 2 = turn around.
        internal static int TurnsRight(CardinalDir facing, CardinalDir target)
        {
            int diff = ((int)target - (int)facing + 4) % 4;
            if (diff == 3) diff = -1; // prefer single left over three rights
            return diff;
        }

        // ── Legacy stubs (kept for compatibility) ────────────────────────────

        internal List<GridPos> PlanPath(GridPos from, GridPos to)
        {
            BotLogger.Debug($"PlanPath stub: {from} → {to}");
            return new List<GridPos>();
        }

        internal void UpdatePlayerPosition(Vector3 worldPos)
            => BotLogger.Debug($"UpdatePlayerPosition stub: {worldPos}");

        internal void MarkDirectionBlocked(GridPos cell, CardinalDir dir)
            => BotLogger.Debug($"MarkDirectionBlocked stub: {dir} of {cell}");

        internal void MarkChestUnreachable(MapMarker chest)
        {
            BotLogger.Warn($"Marking chest {chest.Label} unreachable");
            chest.Unreachable = true;
        }
    }
}
