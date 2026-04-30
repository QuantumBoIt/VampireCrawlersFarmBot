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

    // Phase 1 stub: navigation logic is not implemented yet.
    internal sealed class Navigator
    {
        internal DungeonMap CurrentMap { get; } = new DungeonMap();

        internal List<GridPos> PlanPath(GridPos from, GridPos to)
        {
            BotLogger.Debug($"STUB: path planning {from} → {to} not implemented");
            return new List<GridPos>();
        }

        internal void UpdatePlayerPosition(Vector3 worldPos)
        {
            BotLogger.Debug($"STUB: player world pos {worldPos} (grid mapping not implemented)");
        }

        internal void MarkDirectionBlocked(GridPos cell, CardinalDir dir)
        {
            BotLogger.Debug($"STUB: would mark {dir} of {cell} as blocked");
        }

        internal void MarkChestUnreachable(MapMarker chest)
        {
            BotLogger.Warn($"STUB: would mark chest {chest.Pos} unreachable");
            chest.Unreachable = true;
        }
    }
}
