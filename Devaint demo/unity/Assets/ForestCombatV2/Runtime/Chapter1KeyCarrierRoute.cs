using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // Where the key carrier can stand and how it gets from one landing to the next, and where it runs.
    // Landings are tile tops and decks cut into short pieces so path costs follow where a body actually stands.
    // The carrier may use every room but the boss room (user decision 2026-11-08). It flees the player along
    // a committed route through other rooms and re-decides only on arrival, when the player gets ahead on the
    // route, or when cornered (HTML experiment outputs/key-carrier-sim/rooms.html, 2026-11-08).
    public sealed class Chapter1KeyCarrierRoute
    {
        public const float ChunkWidth = 4, FleeStartDistance = 12, FleeStopDistance = 18, AvoidRadius = 4,
            MinEscapeRoute = 15, CornerSeconds = 2.5f, FailedEdgeSeconds = 5;

        public enum Move { Walk, Jump, Drop, Fall }
        public sealed class Landing
        {
            public int Index, Deck = -1, Room = -1;
            public float X0, X1, Y;
            public bool Droppable, Tile, Allowed;
            public float Mid => (X0 + X1) / 2;
            public float Clamp(float x, float inset) { inset = Mathf.Min((X1 - X0) / 2, inset); return Mathf.Clamp(x, X0 + inset, X1 - inset); }
        }
        public sealed class Edge { public int To; public Move Kind; public float Cost, TakeOff; public int Side; }

        readonly ForestData data;
        readonly Vector2 body;
        readonly float speed, fallGravity, maxClimb;
        // World y of tile row r's top is offsetY - r. Chapter 1 maps sit below y = 0 (offset 0); chapter 2 builds the same
        // tile grid from its block map, whose world y counts up from the bottom (offset = rows).
        readonly float offsetY;
        readonly List<Rect> roomAreas = new List<Rect>();
        readonly List<bool> forbidden = new List<bool>();
        public readonly List<Landing> Landings = new List<Landing>();
        readonly List<List<Edge>> edges = new List<List<Edge>>();
        Rect? restriction;

        public Chapter1KeyCarrierRoute(ForestData data, Vector2 body, float speed, float fallGravity, float maxClimb, float offsetY = 0)
        {
            this.data = data; this.body = body; this.speed = speed; this.fallGravity = fallGravity; this.maxClimb = maxClimb; this.offsetY = offsetY;
            foreach (var room in data.rooms ?? Array.Empty<ForestRoom>())
            { roomAreas.Add(new Rect(room.x, offsetY - (room.y + room.h), room.w, room.h)); forbidden.Add(room.kind == "boss"); }
            foreach (var passage in data.passages ?? Array.Empty<ForestPassage>())
            { roomAreas.Add(new Rect(passage.x, offsetY - (passage.y + passage.h), passage.w, passage.h)); forbidden.Add(false); }
            CollectLandings();
            Classify();
            BuildEdges();
        }

        public int RoomCount => data.rooms?.Length ?? 0;
        public bool Solid(int col, int row) => col < 0 || row < 0 || col >= data.cols || row >= data.rows || data.ground[row * data.cols + col] != 0;
        // A world box (x right, y up) against the tile grid, whose row 0 is the top and whose tile row r spans y -r-1..-r.
        public bool Clear(float x0, float x1, float y0, float y1)
        {
            if (x1 < x0) (x0, x1) = (x1, x0);
            if (y1 <= y0) return true;
            y0 -= offsetY; y1 -= offsetY;
            int c0 = Mathf.FloorToInt(x0), c1 = Mathf.CeilToInt(x1) - 1, r0 = Mathf.FloorToInt(-y1), r1 = Mathf.CeilToInt(-y0) - 1;
            for (int r = r0; r <= r1; r++) for (int c = c0; c <= c1; c++) if (Solid(c, r)) return false;
            return true;
        }

        void CollectLandings()
        {
            var whole = new List<Landing>();
            int headroom = Mathf.Max(1, Mathf.CeilToInt(body.y));
            for (int row = 1; row < data.rows; row++)
            {
                bool Surface(int col)
                {
                    if (data.ground[row * data.cols + col] == 0) return false;
                    for (int k = 1; k <= headroom; k++) if (row - k >= 0 && data.ground[(row - k) * data.cols + col] != 0) return false;
                    return true;
                }
                for (int x = 0; x < data.cols;)
                {
                    if (!Surface(x)) { x++; continue; }
                    int start = x++;
                    while (x < data.cols && Surface(x)) x++;
                    whole.Add(new Landing { X0 = start, X1 = x, Y = offsetY - row, Tile = true });
                }
            }
            var decks = data.platforms ?? Array.Empty<FoliagePlatform>();
            for (int i = 0; i < decks.Length; i++)
                whole.Add(new Landing { X0 = decks[i].x / 32f, X1 = (decks[i].x + decks[i].w) / 32f, Y = offsetY - decks[i].y / 32f, Deck = i, Droppable = !decks[i].blockDropThrough });
            foreach (var l in whole)
            {
                if (l.X1 - l.X0 < body.x) continue;
                int pieces = Mathf.Max(1, Mathf.RoundToInt((l.X1 - l.X0) / ChunkWidth));
                for (int k = 0; k < pieces; k++)
                    Landings.Add(new Landing { Index = Landings.Count, X0 = Mathf.Lerp(l.X0, l.X1, k / (float)pieces), X1 = Mathf.Lerp(l.X0, l.X1, (k + 1) / (float)pieces), Y = l.Y, Deck = l.Deck, Droppable = l.Droppable, Tile = l.Tile });
            }
        }

        void Classify()
        {
            foreach (var l in Landings)
            {
                var at = new Vector2(l.Mid, l.Y + .1f);
                l.Room = -1; bool inside = false, banned = false;
                for (int i = 0; i < roomAreas.Count; i++)
                {
                    if (!roomAreas[i].Contains(at)) continue;
                    inside = true; banned |= forbidden[i];
                    if (i < RoomCount && l.Room < 0) l.Room = i;
                }
                l.Allowed = inside && !banned;
                if (restriction.HasValue)
                {
                    var r = restriction.Value;
                    l.Allowed = l.X1 > r.xMin && l.X0 < r.xMax && l.Y + .1f >= r.yMin && l.Y + .1f <= r.yMax;
                }
            }
        }

        // Verification only: confines the carrier to landings touching this area.
        public void Restrict(Rect? area) { restriction = area; Classify(); }

        void BuildEdges()
        {
            for (int a = 0; a < Landings.Count; a++)
            {
                var list = new List<Edge>();
                for (int b = 0; b < Landings.Count; b++)
                {
                    if (a == b) continue;
                    var edge = Link(Landings[a], Landings[b]);
                    if (edge != null) list.Add(edge);
                }
                edges.Add(list);
            }
        }

        Edge Link(Landing a, Landing b)
        {
            float dy = b.Y - a.Y, w = body.x, half = w / 2;
            float ov0 = Mathf.Max(a.X0, b.X0), ov1 = Mathf.Min(a.X1, b.X1), ov = ov1 - ov0;
            bool right = b.Mid > a.Mid;
            float gap = Mathf.Max(0, Mathf.Max(b.X0 - a.X1, a.X0 - b.X1));
            if (gap > 4 && dy > -1) return null;
            Edge best = null;
            void Offer(Move kind, float extra, float takeOff = 0, int side = 0)
            {
                float cost = Vector2.Distance(new Vector2(a.Mid, a.Y), new Vector2(b.Mid, b.Y)) + extra;
                if (best == null || cost < best.Cost) best = new Edge { To = b.Index, Kind = kind, Cost = cost, TakeOff = takeOff, Side = side };
            }
            if (Mathf.Abs(dy) < .05f && gap < .05f && ov <= .05f) Offer(Move.Walk, 0);
            float edgeX = right ? a.X1 : a.X0, farX = right ? b.X0 : b.X1;
            if (dy > .05f)
            {
                if (dy > maxClimb) return best;
                if (ov >= w && !b.Tile)
                {
                    float cx = (ov0 + ov1) / 2;
                    if (Clear(cx - half, cx + half, a.Y + .05f, b.Y + body.y)) Offer(Move.Jump, 1.5f, cx);
                }
                else if (gap <= 3 && ov < w &&
                    Clear(right ? edgeX - .5f : farX, right ? farX : edgeX + .5f, a.Y + .05f, b.Y + body.y + .5f) &&
                    Clear(right ? edgeX - .5f : farX - .5f, right ? farX + .5f : edgeX + .5f, b.Y + .05f, b.Y + body.y + .5f))
                    Offer(Move.Jump, 1.5f, right ? edgeX - half : edgeX + half);
                return best;
            }
            if (a.Droppable && ov >= w && dy < -.5f)
            {
                float cx = (ov0 + ov1) / 2;
                if (Clear(cx - half, cx + half, b.Y + .05f, a.Y - .05f)) Offer(Move.Drop, .5f, cx);
            }
            if (gap > .05f && gap <= 3.5f && dy > -1.5f &&
                Clear(right ? edgeX - .5f : farX, right ? farX : edgeX + .5f, Mathf.Min(a.Y, b.Y) + .05f, a.Y + body.y + 1.5f))
                Offer(Move.Jump, 1.5f, right ? edgeX - half : edgeX + half);
            if (dy < -.3f)
            {
                float reach = .6f + speed * Mathf.Sqrt(2 * -dy / fallGravity);
                bool beyond = right ? b.X1 > a.X1 + .4f : b.X0 < a.X0 - .4f;
                float far = right ? b.X0 - a.X1 : a.X0 - b.X1;
                if (beyond && far <= reach)
                {
                    float f = right ? Mathf.Max(b.X0, edgeX + 1) : Mathf.Min(b.X1, edgeX - 1);
                    if (Clear(right ? edgeX : f, right ? f : edgeX, b.Y + .05f, a.Y + body.y + .2f)) Offer(Move.Fall, .5f, 0, right ? 1 : -1);
                }
            }
            return best;
        }

        public Edge EdgeBetween(int a, int b) { foreach (var e in edges[a]) if (e.To == b) return e; return null; }
        public IReadOnlyList<Edge> EdgesFrom(int a) => edges[a];

        public float[] Costs(int start, Func<int, int, float> extra, int[] previous)
        {
            int n = Landings.Count;
            var cost = new float[n];
            for (int i = 0; i < n; i++) { cost[i] = float.PositiveInfinity; if (previous != null) previous[i] = -1; }
            if (start < 0) return cost;
            cost[start] = 0;
            var open = new SortedSet<(float, int)>();
            open.Add((0, start));
            while (open.Count > 0)
            {
                var (d, u) = open.Min; open.Remove(open.Min);
                if (d > cost[u]) continue;
                foreach (var e in edges[u])
                {
                    float add = extra != null ? extra(e.To, u) : 0;
                    if (float.IsInfinity(add)) continue;
                    float next = d + e.Cost + add;
                    if (next >= cost[e.To]) continue;
                    if (!float.IsInfinity(cost[e.To])) open.Remove((cost[e.To], e.To));
                    cost[e.To] = next; if (previous != null) previous[e.To] = u;
                    open.Add((next, e.To));
                }
            }
            return cost;
        }

        public static List<int> PathTo(int[] previous, int goal)
        {
            var path = new List<int>();
            for (int v = goal; v >= 0; v = previous[v]) path.Insert(0, v);
            return path;
        }

        // The landing a body at this foot position stands on (grounded) or would fall onto.
        public Landing StandingOn(Vector2 feet)
        {
            Landing best = null; float bestGap = float.PositiveInfinity;
            foreach (var l in Landings)
            {
                if (Mathf.Abs(l.Y - feet.y) > .08f) continue;
                float gap = feet.x < l.X0 ? l.X0 - feet.x : feet.x > l.X1 ? feet.x - l.X1 : 0;
                if (gap <= body.x / 2 && gap < bestGap) { best = l; bestGap = gap; }
            }
            return best;
        }
        public Landing Below(Vector2 feet)
        {
            Landing best = null;
            foreach (var l in Landings)
            {
                if (l.Y > feet.y + .08f || feet.x < l.X0 - body.x / 2 || feet.x > l.X1 + body.x / 2) continue;
                if (best == null || l.Y > best.Y || l.Y == best.Y && Mathf.Abs(l.Mid - feet.x) < Mathf.Abs(best.Mid - feet.x)) best = l;
            }
            return best;
        }
        public static float Distance(Landing l, Vector2 at)
        {
            float dx = at.x < l.X0 ? l.X0 - at.x : at.x > l.X1 ? at.x - l.X1 : 0;
            return Mathf.Sqrt(dx * dx + (l.Y - at.y) * (l.Y - at.y));
        }

        // ---- Planning ----
        public bool Fleeing { get; private set; }
        public int Goal { get; private set; } = -1;
        public bool Cornered { get; private set; }
        public List<int> Path { get; private set; } = new List<int>();
        public string LastPlan { get; private set; } = "none";
        float cornerUntil, pauseUntil;
        int heading = 1;
        readonly List<int> recentRooms = new List<int>();
        readonly Dictionary<int, float> steppedUntil = new Dictionary<int, float>();
        readonly Dictionary<long, float> failedUntil = new Dictionary<long, float>();

        public void Reset()
        {
            Fleeing = false; Goal = -1; Cornered = false; Path.Clear(); cornerUntil = pauseUntil = 0; heading = 1;
            recentRooms.Clear(); steppedUntil.Clear(); failedUntil.Clear(); LastPlan = "none";
        }
        public void EnterRoom(int room)
        {
            if (room < 0) return;
            recentRooms.Remove(room); recentRooms.Insert(0, room);
            if (recentRooms.Count > 3) recentRooms.RemoveAt(3);
        }
        // A drop-through deck the carrier stepped on stays off its route for this long (user 2026-11-08).
        public void Stepped(Landing l, float until)
        {
            if (l == null || l.Deck < 0 || !l.Droppable) return;
            foreach (var k in Landings) if (k.Deck == l.Deck) steppedUntil[k.Index] = until;
        }
        public float SteppedUntil(int landing) => steppedUntil.TryGetValue(landing, out float until) ? until : 0;
        public void Fail(int from, int to, float now) => failedUntil[(long)from * 65536 + to] = now + FailedEdgeSeconds;
        public void ClearGoal() { Goal = -1; Path.Clear(); Cornered = false; }
        public void Pause(float until) => pauseUntil = until;

        public void Plan(float now, Landing current, Vector2 carrier, Vector2 player, System.Random random)
        {
            if (current == null) { Path.Clear(); return; }
            float playerDistance = Vector2.Distance(carrier, player);
            if (!Fleeing && playerDistance < FleeStartDistance) Fleeing = true;
            else if (Fleeing && playerDistance > FleeStopDistance) Fleeing = false;
            var previous = new int[Landings.Count];
            float Extra(int v, int u)
            {
                var l = Landings[v];
                if (!l.Allowed) return float.PositiveInfinity;
                float add = 0;
                if (SteppedUntil(v) > now) add += 1000;
                if (failedUntil.TryGetValue((long)u * 65536 + v, out float failed) && failed > now) add += 1000;
                float near = Distance(l, player);
                if (near < 2) add += 200; else if (near < AvoidRadius) add += 30;
                return add;
            }
            var mine = Costs(current.Index, Extra, previous);
            int goal = -1;
            if (Fleeing)
            {
                var them = Costs(Below(player)?.Index ?? -1, null, null);
                float PlayerCost(Landing l) => float.IsInfinity(them[l.Index]) ? Vector2.Distance(new Vector2(l.Mid, l.Y), player) : them[l.Index];
                bool keep = false;
                if (Goal >= 0 && Goal != current.Index && mine[Goal] < 1000)
                {
                    if (Cornered && now < cornerUntil) keep = true;
                    else if (mine[Goal] < 200)
                    {
                        keep = true;
                        var ahead = PathTo(previous, Goal);
                        for (int k = 1; k < ahead.Count && k < 8; k++)
                        {
                            var l = Landings[ahead[k]];
                            float near = Distance(l, player);
                            if (near < 3 && near < Distance(l, carrier)) { keep = false; break; }
                        }
                    }
                }
                if (keep) goal = Goal;
                else
                {
                    Cornered = false;
                    float best = float.NegativeInfinity;
                    foreach (var l in Landings)
                    {
                        if (!l.Allowed || l == current || mine[l.Index] >= 200 || mine[l.Index] < MinEscapeRoute) continue;
                        int dir = l.Mid > carrier.x ? 1 : -1;
                        float score = PlayerCost(l) - .3f * mine[l.Index] + (dir == heading ? 8 : 0) +
                            (l.Room >= 0 && !recentRooms.Contains(l.Room) ? 10 : 0) + (float)random.NextDouble() * 4;
                        if (score > best) { best = score; goal = l.Index; }
                    }
                    if (goal < 0)
                    {
                        // Cornered: slip past the player toward whatever leaves the most room.
                        foreach (var l in Landings)
                        {
                            if (!l.Allowed || l == current || mine[l.Index] >= 1000) continue;
                            float score = PlayerCost(l) - mine[l.Index] % 200;
                            if (score > best) { best = score; goal = l.Index; }
                        }
                        if (goal >= 0) { Cornered = true; cornerUntil = now + CornerSeconds; }
                    }
                    if (goal >= 0) heading = Landings[goal].Mid > carrier.x ? 1 : -1;
                }
                LastPlan = Cornered ? "cornered" : "flee";
            }
            else
            {
                if (Goal >= 0 && Goal != current.Index && mine[Goal] < 1000) goal = Goal;
                else if (now >= pauseUntil)
                {
                    var pool = new List<Landing>(); var other = new List<Landing>();
                    foreach (var l in Landings)
                    {
                        if (!l.Allowed || l == current || mine[l.Index] >= 1000) continue;
                        pool.Add(l); if (l.Room >= 0 && l.Room != current.Room) other.Add(l);
                    }
                    var from = other.Count > 0 && random.NextDouble() < .65 ? other : pool;
                    if (from.Count > 0) goal = from[random.Next(from.Count)].Index;
                }
                LastPlan = "wander";
            }
            Goal = goal;
            if (goal < 0) { Path.Clear(); return; }
            Path = PathTo(previous, goal);
        }
    }
}
