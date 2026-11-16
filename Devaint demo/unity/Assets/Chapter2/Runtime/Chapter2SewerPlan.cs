using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deviant.Chapter2
{
    // v26 지하수로 화풍(2026-11-12 사용자 채택)의 배치 규칙. 판마다 시드가 다른 블록 맵에 맞춰 문·등·채광·물웅덩이 자리를 정한다.
    // 원본 규칙은 resources environments/stage2-sewer-modular-20261111/stage2-sewer-modern-v26 렌더러다(32px = 1u).
    // 물웅덩이는 바닥을 1u 파서(충돌도 뺀다) 물을 채운다. 그림만이 아니라 지형 충돌을 바꾸므로 지형을 만들기 전에 정한다.
    public sealed class Chapter2SewerPlan
    {
        public readonly struct Door { public readonly float X, Floor; public readonly int Size; public readonly float Radius, Height; public Door(float x, float floor, int size, float r, float h) { X = x; Floor = floor; Size = size; Radius = r; Height = h; } public Rect Frame => new Rect(X - Radius - 10 / 32f, Floor, 2 * (Radius + 10 / 32f), Height + 10 / 32f); }
        public readonly struct Lamp { public readonly Vector2 At; public readonly float HangFrom; public bool Hanging => !float.IsNaN(HangFrom); public Lamp(Vector2 at, float hang) { At = at; HangFrom = hang; } }
        public readonly struct Basin { public readonly int X0, X1, Floor; public readonly float FallX; public bool Fall => !float.IsNaN(FallX); public Basin(int a, int b, int floor, float fall = float.NaN) { X0 = a; X1 = b; Floor = floor; FallX = fall; } public int Width => X1 - X0; }
        // 가로 배관: 윗면 높이 Top, 굵기 18px + 그늘 4px. 방(높이 9u 이상)마다 하나, 땅·문 앞에서 끊긴다.
        public readonly struct Pipe { public readonly float Top, X0, X1; public Pipe(float top, float x0, float x1) { Top = top; X0 = x0; X1 = x1; } }
        // 물줄기: 벽 배수구(바닥 위 118px)에서 바닥(Floor)까지. 방마다 하나, 떨어지는 자리엔 웅덩이가 있다.
        public readonly struct Water { public readonly float X; public readonly int Floor; public Water(float x, int floor) { X = x; Floor = floor; } public float Outlet => Floor + 118 * Px; }

        public const float Px = 1 / 32f;
        static readonly (int r, int h)[] DoorSizes = { (76, 200), (60, 160), (46, 124) };
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<Lamp> Lamps = new List<Lamp>();
        public readonly List<Vector2> Shafts = new List<Vector2>();
        public readonly List<Basin> Basins = new List<Basin>();
        public readonly List<Pipe> Pipes = new List<Pipe>();
        public readonly List<Water> Waters = new List<Water>();
        // 그림에 이미 그려진 등(보스 앞 통로 시안의 벽등 셋). 그림은 두지 않고 캐릭터 색 맞춤용 빛 지도에만 더한다.
        public readonly List<Vector2> PaintedLamps = new List<Vector2>();
        static readonly Vector2[] HallLamps = { new Vector2(4, 3.3f), new Vector2(12.5f, 3.3f), new Vector2(24, 3.3f) };   // 시안 좌표(통로 안쪽 3..51, 바닥 0)
        readonly C2Room[] rooms;
        public readonly List<Rect> Windows;

        readonly HashSet<Vector2Int> solid = new HashSet<Vector2Int>();
        readonly List<Rect> decks = new List<Rect>();
        readonly List<Rect> blocked = new List<Rect>();
        // 물웅덩이·물줄기를 두지 않는 방(정비소: 정비공·작업대 앞, 사용자 2026-11-13 「정비소에는 물웅덩이 없애줘」).
        readonly List<Rect> dry = new List<Rect>();
        bool Dry(Rect r) => dry.Any(d => d.Overlaps(r));

        public static int Hash(params int[] a) { unchecked { uint v = 2166136261; foreach (int x in a) v = (v ^ (uint)x) * 16777619; v ^= v >> 13; v *= 2246822519; v ^= v >> 16; return (int)(v & int.MaxValue); } }
        public bool Solid(int x, int y) => solid.Contains(new Vector2Int(x, y));

        Chapter2SewerPlan(C2Layout layout)
        {
            rooms = layout.rooms; Windows = new List<Rect>();   // v26 화풍엔 창이 없다(사용자 결정 2026-11-12). 예전 창 규칙과 겹침 검사 자리만 남긴다.
            foreach (var s in layout.surfaces)
            {
                if (s.oneWay) { decks.Add(s.Bounds); continue; }
                for (int x = Mathf.FloorToInt(s.x); x < Mathf.CeilToInt(s.x + s.w); x++)
                    for (int y = Mathf.FloorToInt(s.y); y < Mathf.CeilToInt(s.y + s.h); y++) solid.Add(new Vector2Int(x, y));
            }
            if (layout.maintenance != null) foreach (var room in rooms) if (room.Bounds.Contains(layout.maintenance.Vector + Vector2.up * .1f)) dry.Add(room.Bounds);
            // 보스 전투장·보스 앞 통로는 따로 그린 연출이 있어 손대지 않는다.
            blocked.Add(new Rect(layout.boss.x - 1, layout.boss.y - 4, 32, 20));
            if (layout.bossHall != null && layout.bossHall.w > 0)
            {
                var h = layout.bossHall; blocked.Add(new Rect(h.x - 3, h.y - 1, h.w + 6, h.h + 2));
                foreach (var l in HallLamps) PaintedLamps.Add(new Vector2(layout.bossHallToLeft ? h.x + h.w - (l.x - 3) : h.x + (l.x - 3), h.y + l.y));
            }
        }

        // 바닥 줄: (y = 땅 윗면 높이, x0..x1)
        IEnumerable<(int y, int x0, int x1)> Runs(bool floors)
        {
            var cells = solid.Where(c => floors ? !Solid(c.x, c.y + 1) : !Solid(c.x, c.y - 1)).OrderBy(c => c.y).ThenBy(c => c.x).ToList();
            for (int i = 0; i < cells.Count;)
            {
                int j = i; while (j + 1 < cells.Count && cells[j + 1].y == cells[i].y && cells[j + 1].x == cells[j].x + 1) j++;
                yield return (floors ? cells[i].y + 1 : cells[i].y, cells[i].x, cells[j].x + 1); i = j + 1;
            }
        }
        bool AnySolid(Rect r)
        {
            for (int x = Mathf.FloorToInt(r.xMin); x < Mathf.CeilToInt(r.xMax); x++)
                for (int y = Mathf.FloorToInt(r.yMin); y < Mathf.CeilToInt(r.yMax); y++) if (Solid(x, y)) return true;
            return false;
        }
        public bool InBossArea(Vector2 p) => blocked.Count > 0 && blocked[0].Contains(p);
        bool Blocked(Rect r) => blocked.Any(b => b.Overlaps(r)) || Windows.Any(w => new Rect(w.x - .25f, w.y - .25f, w.width + .5f, w.height + .5f).Overlaps(r));
        bool HitsDeck(Rect r, float margin) => decks.Any(d => new Rect(d.x - margin, d.y + d.height - 12 * Px - margin, d.width + 2 * margin, 12 * Px + 2 * margin).Overlaps(r));
        bool HitsPipe(Rect r) => Pipes.Any(p => r.yMax >= p.Top - 28 * Px && r.yMin <= p.Top + 6 * Px && r.xMax >= p.X0 && r.xMin <= p.X1);
        bool HitsDoor(Rect r, float margin = 24 * Px) => Doors.Any(d => new Rect(d.Frame.x - margin, d.Frame.y, d.Frame.width + 2 * margin, d.Frame.height + margin).Overlaps(r));

        public static Chapter2SewerPlan Create(C2Layout layout, Vector2[] keepClear)
        {
            var p = new Chapter2SewerPlan(layout);
            p.PlanPipes(); p.PlanDoors(layout); p.PlanLights(); p.PlanWaters(); p.PlanBasins(keepClear); p.Cut(layout);
            return p;
        }

        // 문(터널 아치): 기둥 사이 칸(9u)마다, 바로 옆 칸 연속 금지. 테 폭 전체가 같은 높이 바닥 위, 위·옆이 땅에 안 잘리고 통과 발판·창과 안 겹칠 때만.
        // 큰 문부터 맞춰 보고 안 되면 작은 문, 기둥 받침에 닿지 않는 범위에서 8px 씩 좌우로 옮겨 본다.
        void PlanDoors(C2Layout layout)
        {
            var floors = Runs(true).ToList(); var used = new HashSet<(int, int)>();
            int minBay = Mathf.FloorToInt(floors.Min(f => f.x0) / 9f) - 1, maxBay = Mathf.CeilToInt(floors.Max(f => f.x1) / 9f) + 1;
            foreach (var y in floors.Select(f => f.y).Distinct().OrderBy(v => v))
                for (int bay = minBay; bay <= maxBay; bay++)
                {
                    if (used.Contains((bay - 1, y))) continue;
                    float c0 = bay * 9 + (144 + 14) * Px;
                    for (int size = 0; size < DoorSizes.Length; size++)
                    {
                        var (r, h) = DoorSizes[size]; int lim = 284 - (r + 14) - 158; bool placed = false;
                        foreach (int shift in Shifts(lim))
                        {
                            float cx = c0 + shift * Px, half = (r + 14) * Px;
                            if (!floors.Any(f => f.y == y && f.x0 <= cx - half && f.x1 >= cx + half)) continue;
                            if (AnySolid(new Rect(cx - (r + 20) * Px, y, (2 * r + 41) * Px, (h + 14) * Px))) continue;
                            if (HitsDeck(new Rect(cx - (r + 18) * Px, y, (2 * r + 36) * Px, (h + 18) * Px), 0)) continue;
                            var frame = new Rect(cx - (r + 10) * Px, y, (2 * r + 20) * Px, (h + 10) * Px);
                            if (Blocked(frame)) continue;
                            Doors.Add(new Door(cx, y, size, r * Px, h * Px)); used.Add((bay, y)); placed = true; break;
                        }
                        if (placed) break;
                    }
                }
        }
        static IEnumerable<int> Shifts(int lim) { yield return 0; for (int k = 8; k <= lim; k += 8) { yield return -k; yield return k; } }

        // 등: 바닥 위 벽등(8u 간격, 바닥에서 88px 위), 천장엔 채광 격자와 매단 등을 번갈아(5.5u 간격). 문 옆에는 벽등 하나.
        // 문·통과 발판·창과 겹치는 것, 서로 가까운 것(220×140px)은 뺀다.
        void PlanLights()
        {
            bool Near(Vector2 a, float dx, float dy) => Lamps.Any(l => Mathf.Abs(l.At.x - a.x) < dx && Mathf.Abs(l.At.y - a.y) < dy);
            bool Ok(Vector2 at, float top)
            {
                var box = new Rect(at.x - 8 * Px, at.y - 8 * Px, 16 * Px, top - at.y + 8 * Px);
                return !HitsDoor(box) && !HitsDeck(box, 8 * Px) && !Blocked(box) && !HitsPipe(box);
            }
            foreach (var (y, x0, x1) in Runs(true))
            {
                if (x1 - x0 < 3) continue;
                for (int k = Mathf.FloorToInt(x0 / 8f) + 1; k * 8 < x1 - .75f; k++)
                {
                    var at = new Vector2(k * 8, y + 88 * Px);
                    if (AnySolid(new Rect(at.x - 10 * Px, at.y - 30 * Px, 20 * Px, 60 * Px)) || Near(at, 220 * Px, 140 * Px) || !Ok(at, at.y + 26 * Px)) continue;
                    Lamps.Add(new Lamp(at, float.NaN));
                }
            }
            foreach (var (y, x0, x1) in Runs(false))
            {
                if (x1 - x0 < 3) continue;
                for (int k = Mathf.FloorToInt(x0 / 5.5f) + 1; k * 5.5f < x1 - .75f; k++)
                {
                    float x = k * 5.5f;
                    if (AnySolid(new Rect(x - 20 * Px, y - 130 * Px, 40 * Px, 128 * Px))) continue;
                    int slot = Hash(k, y, 7) % 3;
                    if (slot == 0) { var box = new Rect(x - 18 * Px, y - 4 * Px, 36 * Px, 4 * Px); if (!Blocked(box) && !HitsDoor(box)) Shafts.Add(new Vector2(x, y)); }
                    else if (slot == 1)
                    {
                        var at = new Vector2(x, y - (44 + k % 3 * 10) * Px);
                        if (Near(at, 220 * Px, 140 * Px) || !Ok(at, y)) continue;
                        Lamps.Add(new Lamp(at, y));
                    }
                }
            }
            foreach (var d in Doors)
                foreach (int side in Hash(Mathf.RoundToInt(d.X * 32), 5) % 2 == 1 ? new[] { 1, -1 } : new[] { -1, 1 })
                {
                    var at = new Vector2(d.X + side * (d.Radius + 48 * Px), d.Floor + Mathf.Round(d.Height * 32 * .56f) * Px);
                    if (AnySolid(new Rect(at.x - 10 * Px, at.y - 30 * Px, 20 * Px, 60 * Px)) || Near(at, 150 * Px, 140 * Px) || !Ok(at, at.y + 26 * Px)) continue;
                    Lamps.Add(new Lamp(at, float.NaN)); break;
                }
        }

        void PlanPipes()
        {
            foreach (var room in rooms)
            {
                if (room.h < 9 || room.id == "B") continue;
                float top = room.y + 4.5f + Hash(Mathf.RoundToInt(room.x * 32), Mathf.RoundToInt(room.y * 32), 91) % 4 * .5f;
                var band = new Rect(room.x, top - 22 * Px, room.w, 22 * Px);
                if (blocked.Any(b => b.Overlaps(band))) continue;
                Pipes.Add(new Pipe(top, room.x, room.x + room.w));
            }
        }

        // 물줄기 후보: 바닥 줄 양끝 24px 안쪽, 16px 간격. 배수구 위 20px 부터 바닥까지 폭 60px 이 비어 있고 등과 90px 이상 떨어진 곳.
        // 등에서 먼 곳부터 고르고 문·통과 발판·배관을 피하며 방마다 하나, 다른 물줄기와 300×200px 안에 두지 않는다.
        void PlanWaters()
        {
            var cands = new List<(float score, float x, int y)>();
            foreach (var (y, x0, x1) in Runs(true))
            {
                float a = x0 * 32 + 24, b = x1 * 32 - 24; if (b - a < 96) continue;
                for (float sx = a + 24; sx < b - 24; sx += 16)
                {
                    float x = sx * Px;
                    if (AnySolid(new Rect(x - 30 * Px, y + Px, 60 * Px, 137 * Px))) continue;
                    float dl = Lamps.Count == 0 ? 999 : Lamps.Min(l => Mathf.Abs(x - l.At.x) * 32 + Mathf.Abs(y + 118 * Px - l.At.y) * 32 * .3f);
                    if (dl < 90) continue;
                    cands.Add((Mathf.Min(dl, 260) + Hash(Mathf.RoundToInt(sx), 13) % 40, x, y));
                }
            }
            var used = new HashSet<string>();
            foreach (var c in cands.OrderByDescending(c => c.score))
            {
                var box = new Rect(c.x - 20 * Px, c.y, 40 * Px, 142 * Px);
                if (HitsDoor(box) || HitsDeck(box, 16 * Px) || HitsPipe(box) || Blocked(box) || Dry(box)) continue;
                var room = rooms.FirstOrDefault(r => r.Bounds.Contains(new Vector2(c.x, c.y + .1f)));
                if (room == null || room.id == "B" || used.Contains(room.id)) continue;
                if (Waters.Any(o => Mathf.Abs(o.X - c.x) < 300 * Px && Mathf.Abs(o.Floor - c.y) < 200 * Px)) continue;
                used.Add(room.id); Waters.Add(new Water(c.x, c.y));
            }
        }

        // 물웅덩이: 바닥 줄마다 9u 간격 후보(셋 중 둘), 폭 2·3·4u × 깊이 1u. 줄 끝에서 1u 안쪽, 아래·옆이 꽉 찬 땅, 문 발치·출발점·정비소 방 전체·출구·적 자리와 다른 웅덩이를 피한다.
        void PlanBasins(Vector2[] keepClear)
        {
            var runs = Runs(true).ToList();
            bool Ok(int y, int a, int b, int x0, int x1, int edge)
            {
                if (a < x0 + edge || b > x1 - edge) return false;
                for (int x = a - 1; x <= b; x++) if (!Solid(x, y - 1) || !Solid(x, y - 2)) return false;
                var hole = new Rect(a, y - 1, b - a, 1);
                if (Blocked(hole) || Dry(new Rect(a, y - 1, b - a, 1.2f)) || Doors.Any(d => Mathf.Approximately(d.Floor, y) && a < d.X + d.Radius + 30 * Px && b > d.X - d.Radius - 30 * Px)) return false;
                if (keepClear.Any(p => Mathf.Abs(p.y - y) < 1.5f && p.x > a - 1.25f && p.x < b + 1.25f)) return false;
                return !Basins.Any(o => o.Floor == y && a < o.X1 + 1.25f && b > o.X0 - 1.25f);
            }
            for (int i = Waters.Count - 1; i >= 0; i--)        // 물줄기 받는 웅덩이(폭 3·2u, 물이 판 안쪽 14px 이상)
            {
                var w = Waters[i]; bool done = false;
                var run = runs.FirstOrDefault(r => r.y == w.Floor && r.x0 <= w.X && r.x1 >= w.X);
                foreach (int width in new[] { 3, 2 })
                {
                    foreach (int shift in new[] { 0, -1, 1 })
                    {
                        int a = Mathf.RoundToInt(w.X - width / 2f) + shift, b = a + width;
                        if (a + 14 * Px > w.X || b - 14 * Px < w.X || !Ok(w.Floor, a, b, run.x0, run.x1, 1)) continue;
                        Basins.Add(new Basin(a, b, w.Floor, w.X)); done = true; break;
                    }
                    if (done) break;
                }
                if (!done) Waters.RemoveAt(i);                // 받을 웅덩이를 못 파면 물줄기도 두지 않는다
            }
            foreach (var (y, x0, x1) in runs)
                for (int k = Mathf.FloorToInt(x0 / 9f); k * 9 < x1; k++)
                {
                    int hv = Hash(k * 9, y, 61); if (hv % 3 == 0) continue;
                    int w = 2 + hv % 3, a = k * 9 + (hv >> 9) % 4, b = a + w;
                    if (!Ok(y, a, b, x0, x1, 1)) continue;
                    Basins.Add(new Basin(a, b, y));
                }
        }

        void Cut(C2Layout layout)
        {
            if (Basins.Count == 0) return;
            var holes = Basins.Select(b => new Rect(b.X0, b.Floor - 1, b.Width, 1)).ToArray();
            var result = new List<C2Surface>();
            foreach (var s in layout.surfaces)
            {
                if (s.oneWay) { result.Add(s); continue; }
                IEnumerable<Rect> parts = new[] { s.Bounds };
                foreach (var h in holes) parts = parts.SelectMany(r => Subtract(r, h)).ToList();
                var list = parts.ToList();
                for (int k = 0; k < list.Count; k++)
                    result.Add(new C2Surface { id = list.Count == 1 ? s.id : s.id + "~basin" + k, x = list[k].x, y = list[k].y, w = list[k].width, h = list[k].height, oneWay = false });
            }
            layout.surfaces = result.ToArray();
            foreach (var h in holes) for (int x = (int)h.x; x < h.xMax; x++) solid.Remove(new Vector2Int(x, (int)h.y));
        }

        static IEnumerable<Rect> Subtract(Rect a, Rect h)
        {
            if (!a.Overlaps(h)) { yield return a; yield break; }
            const float e = 1e-4f;
            float x0 = Mathf.Max(a.xMin, h.xMin), x1 = Mathf.Min(a.xMax, h.xMax), y0 = Mathf.Max(a.yMin, h.yMin), y1 = Mathf.Min(a.yMax, h.yMax);
            if (y1 < a.yMax - e) yield return Rect.MinMaxRect(a.xMin, y1, a.xMax, a.yMax);
            if (y0 > a.yMin + e) yield return Rect.MinMaxRect(a.xMin, a.yMin, a.xMax, y0);
            if (x0 > a.xMin + e) yield return Rect.MinMaxRect(a.xMin, y0, x0, y1);
            if (x1 < a.xMax - e) yield return Rect.MinMaxRect(x1, y0, a.xMax, y1);
        }
    }
}
