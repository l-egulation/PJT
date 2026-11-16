using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Pursuit tuning for melee walkers. Source: data/combat/enemy_pursuit.json
    // (tools/combat/export_enemy_pursuit.py writes Resources/EnemyPursuit.json). The same rules are previewed in
    // tools/combat/monster_pursuit_tuner/index.html.
    // Per-species jump ceiling and whether the species hops at random while it walks.
    [Serializable]
    public sealed class SpeciesMotion { public string id; public float jumpHeight; public bool randomHop; }

    [Serializable]
    public sealed class EnemyPursuitSettings
    {
        public string schema, status;
        public string[] appliesTo;
        public float chaseSpeedScale = 1, jumpHeight = 3, jumpAirSpeed = 6, jumpWindupSeconds, maxDropHeight = 16;
        public float replanSeconds = .25f, detectionScale = 1;
        public float targetFacingDeadzone;
        public bool TargetAligned(float deltaX) => Mathf.Abs(deltaX) <= targetFacingDeadzone;
        public int FacingToward(float deltaX, int current) => TargetAligned(deltaX) ? current : deltaX < 0 ? -1 : 1;
        public bool dropThrough = true, walkOffLedges = true;
        // Random hops while chasing on a floor, and a random pause at the take-off point before a route jump or drop.
        public bool randomHop;
        public float randomHopIntervalMin = 1.5f, randomHopIntervalMax = 4, randomHopHeightMin = .8f, randomHopHeightMax = 2;
        public float pathJumpDelayMin, pathJumpDelayMax;
        // Per-individual spread (+-fraction) of chase speed and jump wind-up, fixed by the stage seed at spawn.
        public float individualVariance;
        // Discovery reaction and the one-step "!" chain alert.
        public float reactionDelayMin, reactionDelayMax;
        public bool alertEnabled;
        public float alertRadius, alertDelayPerUnit, alertJitter, alertMarkSeconds = 1;
        public string[] alertReceivers;
        public SpeciesMotion[] species;
        // Off-screen give-up (`screenLeash`): a hunter whose body stays fully outside the view camera gives up and
        // walks home at its normal speed; it teleports home while both it and home are off screen once stuck.
        public bool screenLeash;
        public float leaveScreenSeconds = 1, unseenGiveUpSeconds = 6, returnTeleportSeconds = 10, noRouteTeleportSeconds = 1;
        public SpeciesMotion MotionFor(string id)
        {
            if (species != null) foreach (var m in species) if (m != null && m.id == id) return m;
            return new SpeciesMotion { id = id, jumpHeight = jumpHeight, randomHop = randomHop };
        }
        static EnemyPursuitSettings cached;
        public static EnemyPursuitSettings Load()
        {
            if (cached != null) return cached;
            var text = Resources.Load<TextAsset>("EnemyPursuit");
            if (!text) throw new InvalidOperationException("Run tools/combat/export_enemy_pursuit.py before using enemy pursuit.");
            var s = JsonUtility.FromJson<EnemyPursuitSettings>(text.text);
            if (s == null || s.schema != "enemy-pursuit-v1" || s.appliesTo == null || s.chaseSpeedScale <= 0 || s.jumpHeight <= 0 ||
                s.jumpAirSpeed <= 0 || s.jumpWindupSeconds < 0 || s.maxDropHeight <= 0 || s.replanSeconds <= 0 || s.detectionScale <= 0 ||
                !(s.targetFacingDeadzone >= 0 && s.targetFacingDeadzone <= 1) ||
                s.randomHopIntervalMin <= 0 || s.randomHopIntervalMax < s.randomHopIntervalMin ||
                s.randomHopHeightMin <= 0 || s.randomHopHeightMax < s.randomHopHeightMin ||
                s.pathJumpDelayMin < 0 || s.pathJumpDelayMax < s.pathJumpDelayMin ||
                s.individualVariance < 0 || s.individualVariance >= 1 ||
                s.reactionDelayMin < 0 || s.reactionDelayMax < s.reactionDelayMin ||
                s.alertRadius < 0 || s.alertDelayPerUnit < 0 || s.alertJitter < 0 || s.alertMarkSeconds <= 0 || s.alertReceivers == null ||
                s.leaveScreenSeconds < 0 || s.unseenGiveUpSeconds <= 0 || s.returnTeleportSeconds <= 0 || s.noRouteTeleportSeconds < 0)
                throw new InvalidOperationException("Invalid enemy pursuit settings.");
            return cached = s;
        }
        public bool Applies(string id) => !string.IsNullOrEmpty(id) && Array.IndexOf(appliesTo, id) >= 0;
        public bool AlertsFor(string id) => !string.IsNullOrEmpty(id) && Array.IndexOf(alertReceivers, id) >= 0;
    }

    // Axis-aligned terrain read from enabled ArenaSurface box colliders. Tilemaps and rotated boxes are not
    // part of the graph; a monster on them simply finds no route and holds.
    public sealed class PursuitTerrain
    {
        public struct Box { public float x0, x1, y0, y1; public bool oneWay, noDrop; }
        public readonly List<Box> boxes = new List<Box>();
        public int Version { get; private set; }
        readonly Dictionary<(float width, float height, float speed, float jump, float drop, bool dropThrough, bool walkOff, float rise, float fall), PursuitGraph> graphs =
            new Dictionary<(float, float, float, float, float, bool, bool, float, float), PursuitGraph>();
        static readonly Dictionary<CombatArena, PursuitTerrain> byArena = new Dictionary<CombatArena, PursuitTerrain>();
        float checkedAt = float.NegativeInfinity; int hash;

        public static PursuitTerrain For(CombatArena arena)
        {
            if (!arena) throw new ArgumentNullException(nameof(arena));
            if (!byArena.TryGetValue(arena, out var terrain))
            {
                // Forget destroyed arenas (scene reloads) before adding the new one.
                var stale = new List<CombatArena>(); foreach (var k in byArena.Keys) if (!k) stale.Add(k);
                foreach (var k in stale) byArena.Remove(k);
                byArena.Add(arena, terrain = new PursuitTerrain());
            }
            float now = arena.Now;
            // Terrain changes (doors, rebuilt rooms) are picked up within a second; a reset clock rechecks at once.
            if (now - terrain.checkedAt >= 1 || now < terrain.checkedAt) { terrain.checkedAt = now; terrain.Refresh(); }
            return terrain;
        }
        public static void InvalidateAll() { foreach (var t in byArena.Values) t.checkedAt = float.NegativeInfinity; }
        void Refresh()
        {
            var found = new List<Box>(); int h = 17;
            foreach (var surface in UnityEngine.Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None))
            {
                var box = surface.GetComponent<BoxCollider2D>();
                if (!box || !box.enabled || box.isTrigger || !box.gameObject.activeInHierarchy) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(surface.transform.eulerAngles.z, 0)) > .01f) continue;
                var b = box.bounds;
                found.Add(new Box { x0 = b.min.x, x1 = b.max.x, y0 = b.min.y, y1 = b.max.y, oneWay = surface.oneWay, noDrop = surface.blockDropThrough });
                unchecked { h = h * 31 + b.min.x.GetHashCode(); h = h * 31 + b.max.y.GetHashCode(); h = h * 31 + b.size.x.GetHashCode() + (surface.oneWay ? 7 : 0); }
            }
            if (h == hash && found.Count == boxes.Count) return;
            hash = h; boxes.Clear(); boxes.AddRange(found); graphs.Clear(); Version++;
            IndexSolids();
        }
        // Spatial cache of the solid (not one-way) boxes: 2D cells for overlap tests and per-column lists sorted by
        // bottom for ceiling lookups. Rebuilt with the boxes; answers equal the full scans they replace.
        public const float Cell = 8;
        readonly Dictionary<long, List<int>> solidCells = new Dictionary<long, List<int>>();
        readonly Dictionary<int, List<int>> solidColumns = new Dictionary<int, List<int>>();
        readonly List<int> oversized = new List<int>();
        int[] solidSeen = Array.Empty<int>(); int solidStamp;
        static long CellKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
        public static int CellOf(float v) => Mathf.FloorToInt(v / Cell);
        // Index of the boxes for tests and tools that read or replace the box list directly.
        public void IndexSolids()
        {
            solidCells.Clear(); solidColumns.Clear(); oversized.Clear(); solidSeen = new int[boxes.Count]; solidStamp = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i]; if (b.oneWay) continue;
                int cx0 = CellOf(b.x0), cx1 = CellOf(b.x1), cy0 = CellOf(b.y0), cy1 = CellOf(b.y1);
                bool large = (long)(cx1 - cx0 + 1) * (cy1 - cy0 + 1) > 1024;
                if (large) oversized.Add(i);
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    if (!solidColumns.TryGetValue(cx, out var column)) solidColumns.Add(cx, column = new List<int>());
                    column.Add(i);
                    if (large) continue;
                    for (int cy = cy0; cy <= cy1; cy++)
                    {
                        long key = CellKey(cx, cy);
                        if (!solidCells.TryGetValue(key, out var cell)) solidCells.Add(key, cell = new List<int>());
                        cell.Add(i);
                    }
                }
            }
            foreach (var column in solidColumns.Values) column.Sort((a, b) => boxes[a].y0.CompareTo(boxes[b].y0));
            indexed = boxes.Count;
        }
        int indexed = -1;
        void EnsureIndex() { if (indexed != boxes.Count) IndexSolids(); }
        public bool SolidIn(float x0, float x1, float y0, float y1)
        {
            EnsureIndex();
            if (++solidStamp == int.MaxValue) { Array.Clear(solidSeen, 0, solidSeen.Length); solidStamp = 1; }
            foreach (int i in oversized)
            {
                var b = boxes[i];
                if (b.x0 < x1 - 1e-4f && b.x1 > x0 + 1e-4f && b.y0 < y1 - 1e-4f && b.y1 > y0 + 1e-4f) return true;
            }
            int cx0 = CellOf(Mathf.Min(x0 + 1e-4f, x1 - 1e-4f)), cx1 = CellOf(Mathf.Max(x0 + 1e-4f, x1 - 1e-4f));
            int cy0 = CellOf(Mathf.Min(y0 + 1e-4f, y1 - 1e-4f)), cy1 = CellOf(Mathf.Max(y0 + 1e-4f, y1 - 1e-4f));
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    if (!solidCells.TryGetValue(CellKey(cx, cy), out var cell)) continue;
                    foreach (int i in cell)
                    {
                        if (solidSeen[i] == solidStamp) continue; solidSeen[i] = solidStamp;
                        var b = boxes[i];
                        if (b.x0 < x1 - 1e-4f && b.x1 > x0 + 1e-4f && b.y0 < y1 - 1e-4f && b.y1 > y0 + 1e-4f) return true;
                    }
                }
            return false;
        }
        public float CeilingAbove(float x0, float x1, float y)
        {
            EnsureIndex();
            float c = float.PositiveInfinity, floor = y - 1e-3f;
            for (int cx = CellOf(Mathf.Min(x0 + 1e-4f, x1 - 1e-4f)), last = CellOf(Mathf.Max(x0 + 1e-4f, x1 - 1e-4f)); cx <= last; cx++)
            {
                if (!solidColumns.TryGetValue(cx, out var column)) continue;
                // First box whose bottom is at or above the floor; the column is sorted by bottom.
                int lo = 0, hi = column.Count;
                while (lo < hi) { int mid = (lo + hi) / 2; if (boxes[column[mid]].y0 >= floor) hi = mid; else lo = mid + 1; }
                for (int k = lo; k < column.Count; k++)
                {
                    var b = boxes[column[k]];
                    if (b.y0 >= c) break;
                    if (b.x0 < x1 - 1e-4f && b.x1 > x0 + 1e-4f) { c = b.y0; break; }
                }
            }
            return c;
        }
        public PursuitGraph Graph(float halfWidth, float height, float airSpeed, float jumpHeight, EnemyPursuitSettings s)
        {
            var key = (halfWidth, height, airSpeed, jumpHeight, s.maxDropHeight, s.dropThrough, s.walkOffLedges, PursuitGraph.Rise, PursuitGraph.Fall);
            if (!graphs.TryGetValue(key, out var graph)) graphs.Add(key, graph = new PursuitGraph(this, halfWidth, height, airSpeed, jumpHeight, s));
            return graph;
        }
    }

    public enum PursuitEdgeKind { Fall, Drop, Jump }
    public sealed class PursuitSegment
    {
        public int id; public float x0, x1, y; public bool oneWay, noDrop, blockedL, blockedR;
        // Body-centre range the actor can stand in: feet on the top, never inside a wall at either end.
        public float c0, c1; public bool wallL, wallR;
        public bool Standable => c1 >= c0 - 1e-3f;
    }
    public sealed class PursuitEdge
    {
        // target is kept by reference: a pending jump must not index a graph rebuilt after it was planned.
        public PursuitSegment target;
        public int from, to; public PursuitEdgeKind kind; public float t0, t1, land = float.NaN, dy, need, room; public int dir;
        string key;
        // Built once per edge: the failed-link table is read for every edge of every search.
        public string Key => key ??= from + ">" + to + ":" + kind + ":" + t0.ToString("0.0");
    }

    // Walkable top spans for one body size and the fall/drop/jump links between them. Mirrors the HTML tuner.
    public sealed class PursuitGraph
    {
        public const float SolidMargin = .3f, OneWayMargin = .15f, FootHalf = .5f;
        public readonly List<PursuitSegment> segments = new List<PursuitSegment>();
        public readonly List<List<PursuitEdge>> edges = new List<List<PursuitEdge>>();
        // Routes shared by hunters of this graph (EnemyPursuit.Route): same start floor, same target floor, same
        // half-unit start/target cells. A shared list is never modified; it lives until the replan period ends.
        internal sealed class SharedRoute { public List<PursuitEdge> path; public float at; }
        internal readonly Dictionary<(int from, int goal, int x, int tx, int ty), SharedRoute> sharedRoutes =
            new Dictionary<(int, int, int, int, int), SharedRoute>();
        readonly PursuitTerrain terrain;
        readonly float hw, bh, fw, jumpHeight;
        readonly EnemyPursuitSettings settings;
        public PursuitGraph(PursuitTerrain terrain, float halfWidth, float height, float airSpeed, float jumpHeight, EnemyPursuitSettings s)
        {
            this.terrain = terrain; hw = halfWidth; bh = height; fw = Mathf.Min(halfWidth, FootHalf); settings = s; this.jumpHeight = jumpHeight;
            BuildSegments(); IndexSegments(); BuildStandRanges(); BuildEdges(airSpeed);
        }
        // Spatial cache of the walkable tops: cells by (x, height) for At, per-column lists sorted by height (high
        // first, then id) for Below. Lookups keep the full scans' id-order tie breaks.
        readonly Dictionary<long, List<PursuitSegment>> segmentCells = new Dictionary<long, List<PursuitSegment>>();
        readonly Dictionary<int, List<PursuitSegment>> segmentColumns = new Dictionary<int, List<PursuitSegment>>();
        static long SegmentKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
        void IndexSegments()
        {
            foreach (var s in segments)
            {
                int cy = PursuitTerrain.CellOf(s.y);
                for (int cx = PursuitTerrain.CellOf(s.x0), last = PursuitTerrain.CellOf(s.x1); cx <= last; cx++)
                {
                    long key = SegmentKey(cx, cy);
                    if (!segmentCells.TryGetValue(key, out var cell)) segmentCells.Add(key, cell = new List<PursuitSegment>());
                    cell.Add(s);
                    if (!segmentColumns.TryGetValue(cx, out var column)) segmentColumns.Add(cx, column = new List<PursuitSegment>());
                    column.Add(s);
                }
            }
            foreach (var column in segmentColumns.Values) column.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.id.CompareTo(b.id));
        }
        // jumpHeight is a ceiling: jump only as high as this link needs. A solid top must be cleared by a margin
        // and the sideways travel onto it happens while above its corner; other links use the whole flight.
        public static float LowestJump(float dy, float need, float room, bool solidUp, float airSpeed)
        {
            for (float h = Mathf.Max(dy + (solidUp ? .6f : .5f), 1f); h < room; h += .1f)
            {
                float time = solidUp ? Mathf.Sqrt(2 * (h - dy) / Rise) + Mathf.Sqrt(2 * (h - dy) / Fall) : AirTime(dy, h, Rise, Fall);
                if (need <= airSpeed * time) return h;
            }
            return room;
        }
        public static float AirTime(float dy, float h, float rise, float fall)
            => dy > h ? float.PositiveInfinity : Mathf.Sqrt(2 * h / rise) + Mathf.Sqrt(2 * Mathf.Max(0, h - dy) / fall);
        public static float Rise = 66.6666667f, Fall = 104.1666667f;
        void BuildSegments()
        {
            // Join same-height tops of the same kind, then cut where a solid box sits within body height above.
            var groups = new Dictionary<string, (float y, bool oneWay, bool noDrop, List<Vector2> spans)>();
            foreach (var b in terrain.boxes)
            {
                float top = Mathf.Round(b.y1 * 1000) / 1000;
                string k = top + "|" + b.oneWay + "|" + b.noDrop;
                if (!groups.TryGetValue(k, out var g)) groups.Add(k, g = (top, b.oneWay, b.noDrop, new List<Vector2>()));
                g.spans.Add(new Vector2(b.x0, b.x1));
            }
            foreach (var g in groups.Values)
            {
                g.spans.Sort((a, b) => a.x.CompareTo(b.x));
                var pieces = new List<(float l, float r, bool bl, bool br)>();
                foreach (var s in g.spans)
                {
                    if (pieces.Count > 0 && s.x <= pieces[pieces.Count - 1].r + .02f)
                    { var last = pieces[pieces.Count - 1]; pieces[pieces.Count - 1] = (last.l, Mathf.Max(last.r, s.y), false, false); }
                    else pieces.Add((s.x, s.y, false, false));
                }
                foreach (var o in terrain.boxes)
                {
                    if (o.oneWay || !(o.y0 < g.y + bh && o.y1 > g.y + .02f)) continue;
                    var next = new List<(float l, float r, bool bl, bool br)>();
                    foreach (var p in pieces)
                    {
                        if (o.x1 <= p.l || o.x0 >= p.r) { next.Add(p); continue; }
                        if (o.x0 > p.l) next.Add((p.l, o.x0, p.bl, true));
                        if (o.x1 < p.r) next.Add((o.x1, p.r, true, p.br));
                    }
                    pieces = next;
                }
                foreach (var p in pieces)
                    if (p.r - p.l > .3f)
                        segments.Add(new PursuitSegment { id = segments.Count, x0 = p.l, x1 = p.r, y = g.y, oneWay = g.oneWay, noDrop = g.noDrop, blockedL = p.bl, blockedR = p.br });
            }
            foreach (var unused in segments) edges.Add(new List<PursuitEdge>());
        }
        public PursuitSegment At(float x, float feetY, float tol = .08f)
        {
            PursuitSegment best = null; float bestD = float.PositiveInfinity;
            int cx0 = PursuitTerrain.CellOf(x - hw - .05f), cx1 = PursuitTerrain.CellOf(x + hw + .05f);
            int cy0 = PursuitTerrain.CellOf(feetY - tol), cy1 = PursuitTerrain.CellOf(feetY + tol);
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    if (!segmentCells.TryGetValue(SegmentKey(cx, cy), out var cell)) continue;
                    foreach (var s in cell)
                    {
                        if (Mathf.Abs(s.y - feetY) > tol || x + hw < s.x0 - .05f || x - hw > s.x1 + .05f) continue;
                        float d = Mathf.Max(0, Mathf.Max(s.c0 - x, x - s.c1));
                        if (d < bestD || d == bestD && best != null && s.id < best.id) { bestD = d; best = s; }
                    }
                }
            return best;
        }
        public PursuitSegment Below(float x, float y)
        {
            PursuitSegment best = null; float top = y + .05f;
            float l = x - hw + .05f, r = x + hw - .05f;
            for (int cx = PursuitTerrain.CellOf(Mathf.Min(l, r)), last = PursuitTerrain.CellOf(Mathf.Max(l, r)); cx <= last; cx++)
            {
                if (!segmentColumns.TryGetValue(cx, out var column)) continue;
                // First top at or below the probe height; the column runs from high to low, then by id.
                int lo = 0, hi = column.Count;
                while (lo < hi) { int mid = (lo + hi) / 2; if (column[mid].y <= top) hi = mid; else lo = mid + 1; }
                for (int k = lo; k < column.Count; k++)
                {
                    var s = column[k];
                    if (best != null && (s.y < best.y || s.y == best.y && s.id > best.id)) break;
                    if (x + hw > s.x0 + .05f && x - hw < s.x1 - .05f && s.Standable) { best = s; break; }
                }
            }
            return best;
        }
        // Feet (up to 1 unit wide) may hang over an open edge; a wall end keeps the whole body out of the wall.
        void BuildStandRanges()
        {
            foreach (var a in segments)
            {
                a.wallL = a.blockedL || terrain.SolidIn(a.x0 - hw, a.x0, a.y + .02f, a.y + bh);
                a.wallR = a.blockedR || terrain.SolidIn(a.x1, a.x1 + hw, a.y + .02f, a.y + bh);
                a.c0 = a.wallL ? a.x0 + hw : a.x0 + fw; a.c1 = a.wallR ? a.x1 - hw : a.x1 - fw;
                if (a.wallL && !a.blockedL)
                {
                    float w = a.x0;
                    foreach (var r in terrain.boxes)
                        if (!r.oneWay && r.x1 <= a.x0 + 1e-3f && r.x1 > a.x0 - hw && r.y0 < a.y + bh && r.y1 > a.y + .02f) w = Mathf.Min(w, r.x1);
                    a.c0 = Mathf.Max(a.x0 + fw, w + hw);
                }
                if (a.wallR && !a.blockedR)
                {
                    float w = a.x1;
                    foreach (var r in terrain.boxes)
                        if (!r.oneWay && r.x0 >= a.x1 - 1e-3f && r.x0 < a.x1 + hw && r.y0 < a.y + bh && r.y1 > a.y + .02f) w = Mathf.Max(w, r.x0);
                    a.c1 = Mathf.Min(a.x1 - fw, w - hw);
                }
            }
        }
        static float Clamp(float x, PursuitSegment s) => Mathf.Min(Mathf.Max(x, s.c0), s.c1);
        float JumpRoom(float x, float y) => Mathf.Min(jumpHeight, terrain.CeilingAbove(x - hw, x + hw, y + bh) - y - bh - .02f);
        void Add(PursuitSegment a, PursuitSegment b, PursuitEdgeKind kind, float t0, float t1, float land, float dy = 0, float need = 0, float room = 0, int dir = 0)
            => edges[a.id].Add(new PursuitEdge { target = b, from = a.id, to = b.id, kind = kind, t0 = t0, t1 = t1, land = land, dy = dy, need = need, room = room, dir = dir });
        void BuildEdges(float V)
        {
            const float Reach = 24;
            foreach (var a in segments)
            {
                if (!a.Standable) continue;
                if (settings.walkOffLedges)
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        // The whole body leaves the edge before it falls; a wall end cannot be walked off.
                        if (dir < 0 ? a.wallL : a.wallR) continue;
                        float tx = dir < 0 ? a.x0 - hw - .05f : a.x1 + hw + .05f;
                        if (terrain.SolidIn(tx - hw, tx + hw, a.y + .02f, a.y + bh)) continue;
                        var b = Below(tx, a.y - .1f);
                        if (b != null && a.y - b.y <= settings.maxDropHeight)
                            Add(a, b, PursuitEdgeKind.Fall, tx, tx, Clamp(tx + dir * .3f, b), dir: dir);
                    }
                foreach (var b in segments)
                {
                    if (b == a || !b.Standable) continue;
                    if (b.x0 > a.x1 + Reach || b.x1 < a.x0 - Reach) continue;
                    float dy = b.y - a.y, ol0 = Mathf.Max(a.c0, b.c0), ol1 = Mathf.Min(a.c1, b.c1);
                    bool overlap = ol1 >= ol0, xOverlap = a.x0 < b.x1 && b.x0 < a.x1;
                    if (settings.dropThrough && a.oneWay && !a.noDrop && dy < -.05f && -dy <= settings.maxDropHeight && overlap && Below((ol0 + ol1) / 2, a.y - .1f) == b)
                        Add(a, b, PursuitEdgeKind.Drop, ol0, ol1, float.NaN, dy);
                    if (dy < -settings.maxDropHeight || dy > jumpHeight) continue;
                    if (xOverlap && dy > 0)
                    {
                        if (b.oneWay && overlap)
                        {
                            // Pass up through a one-way top; only where no solid box lies between the two floors.
                            var spans = new List<Vector2> { new Vector2(ol0, ol1) };
                            foreach (var r in terrain.boxes)
                            {
                                if (r.oneWay || !(r.y0 < b.y + bh && r.y1 > a.y + .02f)) continue;
                                float l = r.x0 - hw, rr = r.x1 + hw; var next = new List<Vector2>();
                                foreach (var sp in spans)
                                {
                                    if (rr <= sp.x || l >= sp.y) { next.Add(sp); continue; }
                                    if (l > sp.x) next.Add(new Vector2(sp.x, l));
                                    if (rr < sp.y) next.Add(new Vector2(rr, sp.y));
                                }
                                spans = next;
                            }
                            foreach (var sp in spans)
                            { float room = JumpRoom((sp.x + sp.y) / 2, a.y); if (dy <= room - OneWayMargin) Add(a, b, PursuitEdgeKind.Jump, sp.x, sp.y, float.NaN, dy, 0, room); }
                        }
                        else if (!b.oneWay)
                            for (int dir = -1; dir <= 1; dir += 2)
                            {
                                // A solid top above is climbed from beside it, straight up past the corner.
                                float tx = dir < 0 ? b.x0 - hw - .1f : b.x1 + hw + .1f;
                                if (tx < a.c0 - .01f || tx > a.c1 + .01f) continue;
                                float room = JumpRoom(tx, a.y);
                                if (dy <= room - SolidMargin) Add(a, b, PursuitEdgeKind.Jump, tx, tx, Clamp(dir < 0 ? b.x0 + hw + .1f : b.x1 - hw - .1f, b), dy, .2f, room);
                            }
                        continue;
                    }
                    if (xOverlap) continue;
                    if (Mathf.Abs(dy) < .05f && Mathf.Max(b.x0 - a.x1, a.x0 - b.x1) < .05f) continue;
                    bool right = b.x0 >= a.x1 - .01f; float gap = right ? b.x0 - a.x1 : a.x0 - b.x1;
                    // Past a wall end only the obstacle's own top (touching) is climbed; farther floors go over it.
                    if ((right ? a.wallR : a.wallL) && (gap > .05f || dy <= 0)) continue;
                    // A higher solid top is jumped from beside it so the rising body clears its underside.
                    float jx = right ? a.c1 : a.c0;
                    if (!b.oneWay && dy > 0) jx = right ? Mathf.Min(jx, b.x0 - hw - .05f) : Mathf.Max(jx, b.x1 + hw + .05f);
                    if (jx < a.c0 - .01f || jx > a.c1 + .01f) continue;
                    // Landing needs any overlap with the top, so the air travel is to its near edge.
                    float jl = right ? b.c0 : b.c1, need = Mathf.Max(.2f, right ? b.x0 - hw + .1f - jx : jx - (b.x1 + hw - .1f));
                    float jroom = JumpRoom(jx, a.y), top = Mathf.Max(a.y, b.y);
                    if (dy > jroom - (b.oneWay ? OneWayMargin : SolidMargin)) continue;
                    if (terrain.SolidIn(Mathf.Min(a.x1, b.x1), Mathf.Max(a.x0, b.x0), top + .02f, top + bh)) continue;
                    if (need <= V * AirTime(dy, jroom, Rise, Fall)) Add(a, b, PursuitEdgeKind.Jump, jx, jx, jl, dy, need, jroom);
                }
            }
        }
        // Can a body that falls off this end of `from` route back up to `from`? Cached per end.
        readonly Dictionary<int, bool> returnable = new Dictionary<int, bool>();
        public bool CanReturnAfterFall(PursuitSegment from, int dir)
        {
            int key = from.id * 2 + (dir > 0 ? 1 : 0);
            if (returnable.TryGetValue(key, out bool known)) return known;
            bool result = false;
            if (!(dir > 0 ? from.wallR : from.wallL))
            {
                var landing = Below(dir > 0 ? from.x1 + hw + .05f : from.x0 - hw - .05f, from.y - .1f);
                if (landing != null)
                {
                    var seen = new HashSet<int> { landing.id }; var queue = new Queue<int>(); queue.Enqueue(landing.id);
                    while (queue.Count > 0 && !result)
                    {
                        int u = queue.Dequeue();
                        if (u == from.id) { result = true; break; }
                        foreach (var e in edges[u]) if (seen.Add(e.to)) queue.Enqueue(e.to);
                    }
                }
            }
            returnable[key] = result; return result;
        }
        // The standable segment that continues `s` past its `dir` end at (almost) the same height, if any.
        public PursuitSegment Continuation(PursuitSegment s, int dir)
        {
            if (dir > 0 ? s.wallR : s.wallL) return null;
            PursuitSegment best = null;
            foreach (var n in segments)
            {
                if (n == s || !n.Standable || Mathf.Abs(n.y - s.y) > .08f) continue;
                bool touches = dir > 0 ? n.x0 <= s.x1 + .05f && n.x1 > s.x1 + .05f : n.x1 >= s.x0 - .05f && n.x0 < s.x0 - .05f;
                if (touches && (best == null || (dir > 0 ? n.x1 > best.x1 : n.x0 < best.x0))) best = n;
            }
            return best;
        }
        public bool Overlaps(PursuitSegment s, float x, float feetY)
            => Mathf.Abs(s.y - feetY) < .08f && x + hw > s.x0 - .05f && x - hw < s.x1 + .05f;
    }

    // One melee walker's pursuit: route over the segment graph, then walk, jump, drop or fall along it.
    public sealed class EnemyPursuit
    {
        public readonly CombatActor actor;
        public readonly EnemyPursuitSettings settings;
        public bool Hunting { get; private set; }
        public string Status { get; private set; } = "";
        List<PursuitEdge> plan; int planVersion = -1;
        // The last search's floors. A search that found no route is retried on the replan period or when either floor
        // changes, not on every step.
        bool planTried; int triedFrom = -1, triedGoal = -1;
        float planAt = float.NegativeInfinity, windupUntil = float.NegativeInfinity;
        PursuitEdge pending, airEdge; float airX; bool airborne;
        readonly Dictionary<string, float> failedUntil = new Dictionary<string, float>();
        // Search work arrays indexed by segment id; an entry counts only when its stamp is the current search's.
        float[] routeCost = Array.Empty<float>(), routeAt = Array.Empty<float>();
        PursuitEdge[] routePrevious = Array.Empty<PursuitEdge>();
        int[] routeSeen = Array.Empty<int>(), routeDone = Array.Empty<int>(), routeLinked = Array.Empty<int>();
        int routeStamp;
        readonly List<int> routeOpen = new List<int>();
        readonly List<PursuitEdge> routePath = new List<PursuitEdge>();
        System.Random random;
        bool hopping; float hopVx, nextHopAt = float.NaN, pathWaitUntil; string pathWaitKey;
        // Seeded by the stage seed, species id and spawn so a replay of the same seed gives the same individual.
        readonly string id; readonly Vector2 origin;
        public float SpeedFactor { get; }
        public float WindupFactor { get; }
        // Species jump ceiling and random-hop switch (enemy_pursuit.json `species`); settable for tests.
        public float JumpHeight { get; set; }
        public bool RandomHop { get; set; }
        public EnemyPursuit(CombatActor actor, EnemyPursuitSettings settings, string id)
        {
            this.actor = actor; this.settings = settings; this.id = id; origin = actor.Position;
            var variance = EnemyRandom.For(actor.arena, id, origin, EnemyRandom.Variance);
            float v = settings.individualVariance;
            SpeedFactor = EnemyRandom.Range(variance, 1 - v, 1 + v); WindupFactor = EnemyRandom.Range(variance, 1 - v, 1 + v);
            random = EnemyRandom.For(actor.arena, id, origin, EnemyRandom.Hop);
            var motion = settings.MotionFor(id); JumpHeight = motion.jumpHeight; RandomHop = motion.randomHop;
        }
        public void Begin()
        {
            Hunting = true; Returning = seenOnScreen = false; offSince = float.NaN; huntSince = actor.arena.Now;
        }
        // Off-screen give-up state: walking (or teleporting) back to the spawn point.
        public bool Returning { get; private set; }
        // The spawn point the enemy walks back to (feet); set by the caller each step from its own spawn.
        public Vector2 Home { get; private set; }
        bool seenOnScreen, homeward; float huntSince, offSince = float.NaN, returnSince, stuckSince = float.NaN;

        // Whether a body box at `center` overlaps the arena's view camera. No orthographic view camera: always true.
        public static bool InView(CombatArena arena, Vector2 center, Vector2 size)
        {
            var cam = arena ? arena.ViewCamera : null;
            if (!cam || !cam.orthographic) return true;
            float hh = cam.orthographicSize, hw = hh * cam.aspect; Vector3 c = cam.transform.position;
            return Mathf.Abs(center.x - c.x) < hw + size.x / 2 && Mathf.Abs(center.y - c.y) < hh + size.y / 2;
        }
        public bool OnScreen => InView(actor.arena, actor.Center, actor.size);
        bool HomeOnScreen => InView(actor.arena, Home + new Vector2(actor.offset.x * actor.facing, actor.offset.y), actor.size);

        // Gives up once the body stays fully off screen for leaveScreenSeconds after it has been on screen, or when a
        // hunter that started off screen never reaches the screen within unseenGiveUpSeconds. Screen presence is
        // tracked every step, attacks included; the give-up itself waits until no attack or jump is in progress.
        bool Leash(float now, bool canStop)
        {
            if (!settings.screenLeash || !Hunting) return false;
            if (OnScreen) { seenOnScreen = true; offSince = float.NaN; return false; }
            if (seenOnScreen && float.IsNaN(offSince)) offSince = now;
            if (!canStop || Busy) return false;
            if (seenOnScreen ? now - offSince < settings.leaveScreenSeconds : now - huntSince < settings.unseenGiveUpSeconds) return false;
            Reset(); Returning = true; returnSince = now; Status = "복귀"; return true;
        }

        // Called by the enemy every step before its own logic. Returns true while the enemy is returning home: the
        // caller applies `velocity` and skips sensing, alerting and attacking for this step.
        // canStop: not in an attack. detected: the enemy detects the target this step (only counts on screen).
        // frozen: stagger/stun/root. Arrival or re-detection ends the return and clears the "!" state.
        public bool StepReturn(Vector2 home, bool canStop, bool detected, bool frozen, float baseSpeed, float dt, EnemyAlert alert, out float velocity, out bool arrived)
        {
            velocity = 0; arrived = false; float now = actor.arena.Now; Home = home;
            if (Leash(now, canStop)) alert?.Mute();
            if (!Returning) return false;
            if (detected && OnScreen) { Reset(); alert?.Reset(); return false; }
            velocity = frozen ? 0 : StepHome(baseSpeed, dt, now, out arrived);
            if (arrived) { Reset(); alert?.Reset(); }
            return true;
        }
        float StepHome(float baseSpeed, float dt, float now, out bool arrived)
        {
            arrived = false;
            var graph = CurrentGraph(baseSpeed, out _);
            var home = graph.At(Home.x, Home.y) ?? graph.Below(Home.x, Home.y + .1f);
            if (actor.grounded && !Busy && Mathf.Abs(BodyX - Home.x) < .15f && (home == null || graph.Overlaps(home, BodyX, actor.Position.y)))
            { arrived = true; return 0; }
            float v; homeward = true;
            try { v = Step(null, Home, true, baseSpeed, false, dt); } finally { homeward = false; }
            bool waiting = Status == "하향 대기" || Status == "도약 대기";
            if (actor.grounded && !Busy && Mathf.Abs(v) < .01f && !waiting) { if (float.IsNaN(stuckSince)) stuckSince = now; }
            else stuckSince = float.NaN;
            bool stuck = !float.IsNaN(stuckSince) && now - stuckSince >= settings.noRouteTeleportSeconds;
            if ((stuck || now - returnSince >= settings.returnTeleportSeconds) && !OnScreen && !HomeOnScreen)
            {
                pending = airEdge = null; airborne = hopping = false;
                actor.Position = Home; actor.VelocityX = 0; actor.VelocityY = 0; Physics2D.SyncTransforms();
                arrived = true; return 0;
            }
            Status = ReturnStatus(Status);
            return v;
        }
        static readonly Dictionary<string, string> returnStatus = new Dictionary<string, string>();
        static string ReturnStatus(string status)
        {
            if (!returnStatus.TryGetValue(status, out var text)) returnStatus.Add(status, text = "복귀 · " + status);
            return text;
        }
        public void Reset()
        {
            Returning = false; stuckSince = float.NaN;
            Hunting = airborne = hopping = false; ForgetPlan(); pending = airEdge = null; planAt = windupUntil = float.NegativeInfinity;
            failedUntil.Clear(); Status = ""; nextHopAt = float.NaN; pathWaitKey = null;
            random = EnemyRandom.For(actor.arena, id, origin, EnemyRandom.Hop);
            routePath.Clear(); routeOpen.Clear(); Array.Clear(routePrevious, 0, routePrevious.Length);
        }
        float Range(float min, float max) => max <= min ? min : min + (float)random.NextDouble() * (max - min);
        // A random hop while walking on a floor: skipped inside attack position (hold), when the landing would leave
        // the standing range, or when the ceiling leaves less than the minimum height.
        float Walk(PursuitTerrain terrain, PursuitSegment cur, float v, bool hold, float now)
        {
            if (v == 0 || hold || homeward || !RandomHop || cur == null) return v;
            if (float.IsNaN(nextHopAt)) nextHopAt = now + Range(settings.randomHopIntervalMin, settings.randomHopIntervalMax);
            if (now < nextHopAt) return v;
            float x = BodyX, y = actor.Position.y, hw = HalfWidth;
            float room = terrain.CeilingAbove(x - hw, x + hw, y + BodyHeight) - y - BodyHeight - .02f;
            float h = Mathf.Min(Mathf.Min(Range(settings.randomHopHeightMin, settings.randomHopHeightMax), JumpHeight), room);
            float land = x + v * PursuitGraph.AirTime(0, Mathf.Max(h, .01f), PursuitGraph.Rise, PursuitGraph.Fall);
            if (h < settings.randomHopHeightMin || land < cur.c0 || land > cur.c1) { nextHopAt = now + .3f; return v; }
            actor.VelocityY = Mathf.Sqrt(2 * PursuitGraph.Rise * h); actor.grounded = false;
            hopping = airborne = true; hopVx = v;
            nextHopAt = now + Range(settings.randomHopIntervalMin, settings.randomHopIntervalMax);
            Status = "무작위 점프"; return v;
        }
        // Random pause at the take-off point, rolled once per route link.
        bool PathWait(PursuitEdge e, float now)
        {
            if (pathWaitKey != e.Key) { pathWaitKey = e.Key; pathWaitUntil = now + Range(settings.pathJumpDelayMin, settings.pathJumpDelayMax); }
            return now < pathWaitUntil;
        }
        public bool Busy => airborne || pending != null;
        float BodyX => actor.Position.x + actor.offset.x * actor.facing;
        float HalfWidth => actor.size.x / 2;
        float BodyHeight => actor.size.y;

        // Horizontal speed (feet frame) for this step. Jumps and drops are applied to the actor directly; the
        // caller then runs PhysicsStep with the result. hold: already in attack position on the target's floor.
        PursuitGraph CurrentGraph(float baseSpeed, out PursuitTerrain terrain)
        {
            float airSpeed = Mathf.Max(baseSpeed * settings.chaseSpeedScale * SpeedFactor, settings.jumpAirSpeed);
            terrain = PursuitTerrain.For(actor.arena);
            PursuitGraph.Rise = actor.arena.Movement.RiseGravity; PursuitGraph.Fall = actor.arena.Movement.FallGravity;
            return terrain.Graph(HalfWidth, BodyHeight, airSpeed, JumpHeight, settings);
        }
        // Charge travel for this step (feet frame). Charges run off an open edge only when the floor below routes
        // back up; otherwise they stop with the feet on the edge (a shorter charge). Airborne charges are unchanged.
        public float LimitCharge(float delta, float baseSpeed, out bool stoppedAtEdge)
        {
            stoppedAtEdge = false;
            if (delta == 0 || !actor.grounded) return delta;
            var graph = CurrentGraph(baseSpeed, out _);
            float x = BodyX; var cur = graph.At(x, actor.Position.y);
            if (cur == null) return delta;
            int dir = delta > 0 ? 1 : -1; float limit = dir > 0 ? cur.c1 : cur.c0;
            if (dir > 0 ? x + delta <= limit : x + delta >= limit) return delta;
            // A floor of the other kind (solid ground ↔ drop-through platform) that touches this end at the same
            // height is one continuous floor for the charge, not an edge.
            for (int guard = 0; guard < 8; guard++)
            {
                var next = graph.Continuation(cur, dir);
                if (next == null) break;
                cur = next; limit = dir > 0 ? cur.c1 : cur.c0;
                if (dir > 0 ? x + delta <= limit : x + delta >= limit) return delta;
            }
            if (!(dir > 0 ? cur.wallR : cur.wallL) && graph.CanReturnAfterFall(cur, dir)) return delta;
            stoppedAtEdge = true;
            return dir > 0 ? Mathf.Max(0, limit - x) : Mathf.Min(0, limit - x);
        }

        public float Step(CombatActor target, Vector2 targetFeet, bool targetGrounded, float baseSpeed, bool hold, float dt)
        {
            // Homeward walks at the normal speed; air speed stays the chase value the route graph was built with.
            float now = actor.arena.Now, chase = baseSpeed * settings.chaseSpeedScale * SpeedFactor, airSpeed = Mathf.Max(chase, settings.jumpAirSpeed);
            float speed = homeward ? baseSpeed * SpeedFactor : chase;
            var graph = CurrentGraph(baseSpeed, out var terrain);
            float x = BodyX, hw = HalfWidth;
            if (pending != null)
            {
                Status = "점프 준비";
                if (now + dt >= windupUntil) Launch(graph, airSpeed);
                return 0;
            }
            if (!actor.grounded)
            {
                airborne = true;
                if (hopping) { Status = "무작위 점프"; return hopVx; }
                if (airEdge == null) { Status = "공중"; return 0; }
                Status = airEdge.kind == PursuitEdgeKind.Jump ? "점프" : "낙하";
                float d = airX - x; return Mathf.Sign(d) * Mathf.Min(airSpeed, Mathf.Abs(d) / dt);
            }
            if (airborne) Land(graph, now);
            var cur = graph.At(x, actor.Position.y);
            var goal = targetGrounded ? graph.At(targetFeet.x, targetFeet.y) ?? graph.Below(targetFeet.x, targetFeet.y + .1f) : graph.Below(targetFeet.x, targetFeet.y);
            // Target-facing uses the stable feet/root positions, not a body
            // centre whose X changes when an asymmetric monster flips.
            float targetDelta = targetFeet.x - (homeward ? x : actor.Position.x);
            float toward = targetDelta < 0 ? -1 : 1;
            if (cur != null && goal != null && cur.id == goal.id)
            {
                ForgetPlan();
                Status = "추격";
                if (hold || Mathf.Abs(targetDelta) <= (homeward ? .05f : Mathf.Max(.05f, settings.targetFacingDeadzone)))
                { actor.SyncFacing(homeward ? (int)toward : settings.FacingToward(targetDelta, actor.facing)); return 0; }
                return Walk(terrain, cur, Inside(cur, x, hw, toward * speed, dt) ? toward * speed : 0, hold, now);
            }
            int curId = cur != null ? cur.id : -1, goalId = goal != null ? goal.id : -1;
            bool retry = plan == null && !(planTried && triedFrom == curId && triedGoal == goalId);
            if (retry || now - planAt >= settings.replanSeconds || now < planAt || planVersion != terrain.Version)
            {
                plan = Route(graph, cur, goal, x, targetFeet, speed, baseSpeed * settings.chaseSpeedScale, now);
                planAt = now; planVersion = terrain.Version; planTried = true; triedFrom = curId; triedGoal = goalId;
            }
            var e = plan != null && plan.Count > 0 ? plan[0] : null;
            if (e == null || cur == null || e.from != cur.id)
            {
                if (e != null) planAt = float.NegativeInfinity;
                Status = e != null ? "경로 재계산" : "경로 없음 · 대기";
                if (hold || cur == null || !homeward && settings.TargetAligned(targetDelta)) return 0;
                return Walk(terrain, cur, Inside(cur, x, hw, toward * speed, dt) ? toward * speed : 0, hold, now);
            }
            float tx = Mathf.Clamp(x, e.t0, e.t1);
            if (e.kind == PursuitEdgeKind.Fall)
            { Status = "절벽으로"; airEdge = e; airX = e.land; return e.dir * speed; }
            if (Mathf.Abs(tx - x) > .08f && !(actor.wallHit && Mathf.Abs(tx - x) < hw + .6f))
            {
                Status = e.kind == PursuitEdgeKind.Drop ? "하향 지점으로" : "도약 지점으로";
                float v = Mathf.Sign(tx - x) * Mathf.Min(speed, Mathf.Abs(tx - x) / dt);
                // Hops only on the way, not over the last stretch to the take-off point.
                return Mathf.Abs(tx - x) > 1.5f ? Walk(terrain, cur, v, hold, now) : v;
            }
            if (PathWait(e, now)) { Status = e.kind == PursuitEdgeKind.Drop ? "하향 대기" : "도약 대기"; return 0; }
            pathWaitKey = null;
            if (e.kind == PursuitEdgeKind.Drop)
            {
                airEdge = e; airX = x; Status = "하향점프";
                if (!actor.DropThrough()) Fail(e, now);
                return 0;
            }
            float windup = settings.jumpWindupSeconds * WindupFactor;
            pending = e; windupUntil = now + windup; Status = "점프 준비";
            if (windup <= 0) Launch(graph, airSpeed);
            return 0;
        }
        // Blocks only movement that leaves the standing range; an actor already past it may walk back in.
        static bool Inside(PursuitSegment s, float x, float hw, float v, float dt)
        { float nx = x + v * dt; return !(v < 0 && nx < s.c0 || v > 0 && nx > s.c1); }
        void Launch(PursuitGraph graph, float airSpeed)
        {
            var e = pending; pending = null; if (e == null) return;
            float x = BodyX, lx = float.IsNaN(e.land) ? x : e.land;
            float h = PursuitGraph.LowestJump(e.dy, e.need, e.room, e.dy > 0 && !e.target.oneWay, airSpeed);
            actor.VelocityY = Mathf.Sqrt(2 * PursuitGraph.Rise * Mathf.Max(.1f, h)); actor.grounded = false;
            airEdge = e; airX = lx; airborne = true;
            if (Mathf.Abs(lx - x) > .01f) actor.SyncFacing(lx < x ? -1 : 1);
        }
        void Land(PursuitGraph graph, float now)
        {
            if (hopping) { hopping = airborne = false; return; }
            airborne = false; var e = airEdge; airEdge = null; ForgetPlan();
            if (e == null) return;
            // Partial footing counts: the jump worked if the body now rests on the intended top at all.
            if (!graph.Overlaps(e.target, BodyX, actor.Position.y)) Fail(e, now);
        }
        void Fail(PursuitEdge e, float now) { failedUntil[e.Key] = now + 3; ForgetPlan(); airEdge = null; }
        void ForgetPlan() { plan = null; planTried = false; }
        // Hunters on the same floor heading for the same target floor share one search per replan period. The shared
        // search starts from the half-unit start cell toward the half-unit target cell at the species' chase speed, so
        // its result does not depend on which hunter asked first. Return walks and hunters with a recently failed link
        // search alone with their own position and speed.
        List<PursuitEdge> Route(PursuitGraph g, PursuitSegment start, PursuitSegment goal, float x, Vector2 target, float speed, float sharedSpeed, float now)
        {
            if (start == null || goal == null) return null;
            if (homeward || failedUntil.Count > 0) return Plan(g, start, goal, x, target, speed, now);
            var key = (start.id, goal.id, Mathf.RoundToInt(x * 2), Mathf.RoundToInt(target.x * 2), Mathf.RoundToInt(target.y * 2));
            if (g.sharedRoutes.TryGetValue(key, out var shared) && now >= shared.at && now - shared.at < settings.replanSeconds)
                return shared.path;
            if (g.sharedRoutes.Count >= 256) g.sharedRoutes.Clear();
            var path = Plan(g, start, goal, key.Item3 / 2f, new Vector2(key.Item4 / 2f, key.Item5 / 2f), sharedSpeed, now);
            g.sharedRoutes[key] = new PursuitGraph.SharedRoute { path = path == null ? null : new List<PursuitEdge>(path), at = now };
            return g.sharedRoutes[key].path;
        }
        // Dijkstra over segments; cost = walk time + air time. Unreachable targets route to the nearest reachable floor.
        List<PursuitEdge> Plan(PursuitGraph g, PursuitSegment start, PursuitSegment goal, float x, Vector2 target, float speed, float now)
        {
            if (start == null || goal == null) return null;
            int n = g.segments.Count;
            if (routeCost.Length < n)
            {
                routeCost = new float[n]; routeAt = new float[n]; routePrevious = new PursuitEdge[n];
                routeSeen = new int[n]; routeDone = new int[n]; routeLinked = new int[n]; routeStamp = 0;
            }
            if (++routeStamp == int.MaxValue)
            { Array.Clear(routeSeen, 0, routeSeen.Length); Array.Clear(routeDone, 0, routeDone.Length); Array.Clear(routeLinked, 0, routeLinked.Length); routeStamp = 1; }
            int stamp = routeStamp;
            var cost = routeCost; var at = routeAt; var prev = routePrevious; var seen = routeSeen; var done = routeDone; var linked = routeLinked;
            cost[start.id] = 0; at[start.id] = x; seen[start.id] = stamp;
            var open = routeOpen; open.Clear(); open.Add(start.id);
            int best = start.id; float bestD = Near(start, target); int expanded = 0;
            while (open.Count > 0 && expanded++ < 600)
            {
                int bi = 0; float bc = cost[open[0]];
                for (int i = 1; i < open.Count; i++) { float c = cost[open[i]]; if (c < bc) { bi = i; bc = c; } }
                int u = open[bi]; open.RemoveAt(bi); if (done[u] == stamp) continue; done[u] = stamp;
                if (u == goal.id) { best = u; break; }
                float nd = Near(g.segments[u], target); if (nd < bestD - .5f) { bestD = nd; best = u; }
                foreach (var e in g.edges[u])
                {
                    if (failedUntil.Count > 0 && failedUntil.TryGetValue(e.Key, out float until) && until > now) continue;
                    float tx = Mathf.Clamp(at[u], e.t0, e.t1), lx = float.IsNaN(e.land) ? tx : e.land;
                    float air = e.kind == PursuitEdgeKind.Jump ? PursuitGraph.AirTime(e.dy, e.room, PursuitGraph.Rise, PursuitGraph.Fall) + settings.jumpWindupSeconds + .25f
                        : Mathf.Sqrt(2 * Mathf.Max(0, g.segments[e.from].y - g.segments[e.to].y) / PursuitGraph.Fall) + .1f;
                    float c = cost[u] + Mathf.Abs(tx - at[u]) / speed + air;
                    if (seen[e.to] != stamp || c < cost[e.to])
                    {
                        seen[e.to] = stamp; cost[e.to] = c; at[e.to] = lx;
                        // Graph edges are immutable after construction. Keeping the
                        // edge also keeps its old target alive across graph rebuilds.
                        prev[e.to] = e; linked[e.to] = stamp;
                        open.Add(e.to);
                    }
                }
            }
            if (best == start.id) return null;
            var path = routePath; path.Clear(); int k = best;
            while (linked[k] == stamp) { var edge = prev[k]; path.Insert(0, edge); k = edge.from; if (k == start.id) break; }
            return path;
        }
        static float Near(PursuitSegment segment, Vector2 target) =>
            Mathf.Max(0, Mathf.Max(segment.x0 - target.x, target.x - segment.x1)) + Mathf.Abs(segment.y - target.y) * 1.5f;
    }
}
