using System.Collections.Generic;
using UnityEngine;

namespace VampireCrawlersFarmBot
{
    internal enum CardinalDir { North, East, South, West }

    internal struct GridPos
    {
        internal int X, Z;

        internal GridPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public override string ToString() => $"({X},{Z})";
        public override bool Equals(object obj) => obj is GridPos other && X == other.X && Z == other.Z;
        public override int GetHashCode() => (X * 397) ^ Z;
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
        internal int Width;
        internal int Height;
        internal List<MapMarker> Chests = new();
        internal MapMarker Exit;
        internal HashSet<GridPos> BlockedCells = new();
    }

    internal sealed class Navigator
    {
        internal DungeonMap CurrentMap { get; } = new DungeonMap();
        private readonly HashSet<string> _blockedEdges = new();
        private readonly HashSet<GridPos> _doneChestPositions = new();
        private readonly HashSet<GridPos> _unreachableChestPositions = new();

        internal void LoadMinimap(MapSnapshot snapshot)
        {
            CurrentMap.PlayerPos = snapshot.Player;
            CurrentMap.Width = snapshot.Width;
            CurrentMap.Height = snapshot.Height;
            CurrentMap.BlockedCells = new HashSet<GridPos>(snapshot.BlockedCells);

            var mergedChests = new List<MapMarker>();
            foreach (var old in CurrentMap.Chests)
            {
                old.Done = old.Done || _doneChestPositions.Contains(old.Pos);
                old.Unreachable = old.Unreachable || _unreachableChestPositions.Contains(old.Pos);
                mergedChests.Add(old);
            }

            // Minimap event markers can disappear after interacting with one chest.
            // Preserve previously seen, unfinished chest markers so a two-chest map
            // does not look complete after the first cash-out.
            foreach (var chest in snapshot.Chests)
            {
                var old = mergedChests.Find(c => c.Pos.Equals(chest.Pos));
                if (old != null)
                {
                    old.Label = chest.Label;
                    old.WorldPos = chest.WorldPos;
                    old.Done = old.Done || _doneChestPositions.Contains(chest.Pos);
                    old.Unreachable = old.Unreachable || _unreachableChestPositions.Contains(chest.Pos);
                    continue;
                }

                if (_doneChestPositions.Contains(chest.Pos)) chest.Done = true;
                if (_unreachableChestPositions.Contains(chest.Pos)) chest.Unreachable = true;
                mergedChests.Add(chest);
            }

            CurrentMap.Chests.Clear();
            CurrentMap.Chests.AddRange(mergedChests);
            CurrentMap.Exit = snapshot.Exit;

            BotLogger.Info(
                $"Navigator: minimap player={CurrentMap.PlayerPos}, chests={CurrentMap.Chests.Count}, " +
                $"unvisited={CurrentMap.Chests.FindAll(c => !c.Done && !c.Unreachable).Count}, " +
                $"exit={(CurrentMap.Exit == null ? "none" : CurrentMap.Exit.Pos.ToString())}, " +
                $"blockedCells={CurrentMap.BlockedCells.Count}");
        }

        internal void SetChests(List<Transform> transforms)
        {
            CurrentMap.Chests.Clear();
            foreach (var t in transforms)
            {
                CurrentMap.Chests.Add(new MapMarker
                {
                    WorldPos = t.position,
                    Label = t.name
                });
            }
            BotLogger.Info($"Navigator: loaded {CurrentMap.Chests.Count} chests");
        }

        internal bool HasUnvisitedChests()
            => CurrentMap.Chests.Exists(c => !c.Done && !c.Unreachable);

        internal MapMarker GetNextChest(GridPos playerPos)
        {
            MapMarker best = null;
            int bestLen = int.MaxValue;
            foreach (var c in CurrentMap.Chests)
            {
                if (c.Done || c.Unreachable) continue;
                var path = PlanPath(playerPos, c.Pos);
                if (path.Count == 0 && !playerPos.Equals(c.Pos)) continue;
                if (path.Count < bestLen)
                {
                    bestLen = path.Count;
                    best = c;
                }
            }
            return best;
        }

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

        internal static CardinalDir GetFacing(Transform player)
        {
            float yaw = player.eulerAngles.y % 360f;
            if (yaw < 0) yaw += 360f;
            if (yaw < 45f || yaw >= 315f) return CardinalDir.North;
            if (yaw < 135f) return CardinalDir.East;
            if (yaw < 225f) return CardinalDir.South;
            return CardinalDir.West;
        }

        internal static CardinalDir DirectionTo(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            if (Mathf.Abs(dx) >= Mathf.Abs(dz))
                return dx >= 0 ? CardinalDir.East : CardinalDir.West;
            return dz >= 0 ? CardinalDir.North : CardinalDir.South;
        }

        internal static CardinalDir DirectionTo(GridPos from, GridPos to)
        {
            if (to.X > from.X) return CardinalDir.East;
            if (to.X < from.X) return CardinalDir.West;
            if (to.Z > from.Z) return CardinalDir.North;
            return CardinalDir.South;
        }

        internal static int TurnsRight(CardinalDir facing, CardinalDir target)
        {
            int diff = ((int)target - (int)facing + 4) % 4;
            if (diff == 3) diff = -1;
            return diff;
        }

        internal List<GridPos> PlanPath(GridPos from, GridPos to)
        {
            var result = new List<GridPos>();
            if (from.Equals(to)) return result;

            var q = new Queue<GridPos>();
            var seen = new HashSet<GridPos>();
            var parent = new Dictionary<GridPos, GridPos>();
            q.Enqueue(from);
            seen.Add(from);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var next in Neighbors(cur))
                {
                    if (seen.Contains(next)) continue;
                    if (!IsPassable(next)) continue;
                    if (IsEdgeBlocked(cur, next)) continue;

                    seen.Add(next);
                    parent[next] = cur;
                    if (next.Equals(to))
                    {
                        var p = next;
                        while (!p.Equals(from))
                        {
                            result.Add(p);
                            p = parent[p];
                        }
                        result.Reverse();
                        BotLogger.Info($"Navigator: path {from} -> {to}, steps={result.Count}");
                        return result;
                    }
                    q.Enqueue(next);
                }
            }

            BotLogger.Warn($"Navigator: no path {from} -> {to}");
            return result;
        }

        internal void UpdatePlayerPosition(Vector3 worldPos)
            => BotLogger.Debug($"UpdatePlayerPosition stub: {worldPos}");

        internal void MarkDirectionBlocked(GridPos cell, CardinalDir dir)
            => MarkEdgeBlocked(cell, Step(cell, dir));

        internal void MarkEdgeBlocked(GridPos a, GridPos b)
        {
            _blockedEdges.Add(EdgeKey(a, b));
            _blockedEdges.Add(EdgeKey(b, a));
            BotLogger.Warn($"Navigator: blocked edge {a} <-> {b}");
        }

        internal void MarkChestUnreachable(MapMarker chest)
        {
            BotLogger.Warn($"Marking chest {chest.Label} unreachable");
            chest.Unreachable = true;
            _unreachableChestPositions.Add(chest.Pos);
        }

        internal void MarkChestDone(MapMarker chest)
        {
            chest.Done = true;
            _doneChestPositions.Add(chest.Pos);
            _unreachableChestPositions.Remove(chest.Pos);
        }

        internal static GridPos Step(GridPos p, CardinalDir dir)
        {
            return dir switch
            {
                CardinalDir.North => new GridPos(p.X, p.Z + 1),
                CardinalDir.East => new GridPos(p.X + 1, p.Z),
                CardinalDir.South => new GridPos(p.X, p.Z - 1),
                CardinalDir.West => new GridPos(p.X - 1, p.Z),
                _ => p
            };
        }

        private IEnumerable<GridPos> Neighbors(GridPos p)
        {
            yield return new GridPos(p.X + 1, p.Z);
            yield return new GridPos(p.X - 1, p.Z);
            yield return new GridPos(p.X, p.Z + 1);
            yield return new GridPos(p.X, p.Z - 1);
        }

        private bool IsPassable(GridPos p)
        {
            if (CurrentMap.Width > 0 && (p.X < 0 || p.X >= CurrentMap.Width)) return false;
            if (CurrentMap.Height > 0 && (p.Z < 0 || p.Z >= CurrentMap.Height)) return false;
            return !CurrentMap.BlockedCells.Contains(p);
        }

        private bool IsEdgeBlocked(GridPos a, GridPos b) => _blockedEdges.Contains(EdgeKey(a, b));
        private static string EdgeKey(GridPos a, GridPos b) => $"{a.X},{a.Z}->{b.X},{b.Z}";
    }
}
