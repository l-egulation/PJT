using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deviant.Chapter2
{
    // v26 지하수로 화풍의 뒷벽 곁들임: 기둥 받침·머리돌, 땅에 닿는 접촉 그늘, 터널 문, 벽등·매단 등과 둘레 밝음, 천장 채광,
    // 통과 발판의 벽 받침·사슬, 물웅덩이의 물과 물 앞면. 자리는 Chapter2SewerPlan 이 정한다. 충돌·전투는 바꾸지 않는다.
    public sealed class Chapter2SewerDressing : MonoBehaviour
    {
        public const string Root = Chapter2WaterwayArt.Root + "Modern/";
        public const int WallOrder = -50, PipeOrder = -42, ShadowOrder = -46, PillarOrder = -44, DoorOrder = -40, CeilingOrder = -37, WetOrder = -37, FogOrder = -35, FixtureOrder = -33, LampOrder = -31, WaterOrder = 3, WaterFrontOrder = 15;
        const float Px = 1 / 32f;
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Material material;
        public int DoorCount { get; private set; }
        public int LampCount { get; private set; }
        public int BasinCount { get; private set; }

        Sprite Sprite(string key, Vector2 pivotPx)
        {
            string id = key + "@" + pivotPx;
            if (sprites.TryGetValue(id, out var s)) return s;
            var t = Resources.Load<Texture2D>(Root + key); if (!t) throw new InvalidOperationException("Missing v26 sewer part: " + key);
            s = UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(pivotPx.x / t.width, pivotPx.y / t.height), 32, 0, SpriteMeshType.FullRect);
            sprites[id] = s; owned.Add(s); return s;
        }
        SpriteRenderer Put(string name, string key, Vector2 pivotPx, Vector2 at, int order, Vector2? tiledSize = null)
        {
            var go = new GameObject("Sewer v26 · " + name); go.transform.SetParent(transform, false); go.transform.position = at;
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = Sprite(key, pivotPx); r.sharedMaterial = material; r.sortingOrder = order;
            if (tiledSize.HasValue) { r.drawMode = SpriteDrawMode.Tiled; r.size = tiledSize.Value; }
            return r;
        }

        public void Build(Chapter2World world, Chapter2WaterwayBackground background, Rect bounds)
        {
            material = world.spriteMaterial; var plan = world.Sewer; var d = world.Layout;
            BuildWall(plan, bounds); BuildPipes(plan);
            // 기둥(월드 x 9u 배수, 폭 28px)이 바닥·천장에 닿는 자리: 받침 36×14·머리돌 36×10, 기둥보다 양쪽 4px 넓다.
            int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
            foreach (var s in d.surfaces) { if (s.oneWay) continue; x0 = Mathf.Min(x0, Mathf.FloorToInt(s.x)); x1 = Mathf.Max(x1, Mathf.CeilToInt(s.x + s.w)); y0 = Mathf.Min(y0, Mathf.FloorToInt(s.y)); y1 = Mathf.Max(y1, Mathf.CeilToInt(s.y + s.h)); }
            for (int c = Mathf.CeilToInt(x0 / 9f) * 9; c < x1; c += 9)
                for (int y = y0; y < y1; y++)
                {
                    if (!plan.Solid(c, y)) continue;
                    if (!plan.Solid(c, y + 1)) background.AddLit(Put("pillar base", "pillar-base", Vector2.zero, new Vector2(c - 4 * Px, y + 1), PillarOrder));
                    if (!plan.Solid(c, y - 1)) background.AddLit(Put("pillar cap", "pillar-cap", new Vector2(0, 10), new Vector2(c - 4 * Px, y), PillarOrder));
                }
            BuildContactShadow(plan, x0, x1, y0, y1);
            foreach (var door in plan.Doors)
            {
                var t = Resources.Load<Texture2D>(Root + "door-" + door.Size);
                background.AddLit(Put("tunnel door", "door-" + door.Size, new Vector2(t.width / 2, 0), new Vector2(door.X, door.Floor), DoorOrder)); DoorCount++;
            }
            // 등빛은 그림으로 덮지 않는다. 뒷벽 셰이더가 빛 지도(BuildWall)로 돌 색 단계를 올린다.
            foreach (var lamp in plan.Lamps)
            {
                var parts = new List<GameObject>();
                if (lamp.Hanging)
                {
                    float top = lamp.HangFrom, chain = top - (lamp.At.y + 10 * Px);
                    if (chain > 0) parts.Add(Put("lamp chain", "lamp-hang-chain", new Vector2(4, 0), new Vector2(lamp.At.x, lamp.At.y + 10 * Px), LampOrder, new Vector2(8 * Px, chain)).gameObject);
                    parts.Add(Put("hanging lamp", "lamp-hang", new Vector2(8, 9), lamp.At, LampOrder).gameObject);
                }
                else parts.Add(Put("wall lamp", "lamp-wall", new Vector2(8, 9), lamp.At, LampOrder).gameObject);
                lamps.Add((lamp.At, parts));
            }
            LampCount = lamps.Count;
            BuildDepth(plan, x0, x1, y0, y1);
            foreach (var at in plan.Shafts) Put("ceiling grate", "grate", new Vector2(18, 4), at, LampOrder);
            foreach (var water in plan.Waters) Put("waterfall", "waterfall", new Vector2(20, 0), new Vector2(water.X, water.Floor), LampOrder);
            // 통과 발판: 벽에 붙은 끝은 쇠 받침, 양끝이 다 떠 있으면 천장까지 사슬 둘(10u 안에 천장이 있을 때).
            foreach (var s in d.surfaces)
            {
                if (!s.oneWay) continue;
                float top = s.y + s.h; bool lw = plan.Solid(Mathf.FloorToInt(s.x - .01f), Mathf.FloorToInt(top - .1f)), rw = plan.Solid(Mathf.FloorToInt(s.x + s.w + .01f), Mathf.FloorToInt(top - .1f));
                if (lw) Put("deck bracket", "bracket-left", new Vector2(0, 23), new Vector2(s.x, top), FixtureOrder);
                if (rw) Put("deck bracket", "bracket-right", new Vector2(13, 23), new Vector2(s.x + s.w, top), FixtureOrder);
                if (lw || rw) continue;
                foreach (float cx in new[] { s.x + 10 * Px, s.x + s.w - 11 * Px })
                {
                    int ceiling = -1; for (int y = Mathf.CeilToInt(top); y < top + 10; y++) if (plan.Solid(Mathf.FloorToInt(cx), y)) { ceiling = y; break; }
                    if (ceiling < 0) continue;
                    Put("deck chain", "chain", new Vector2(4, 0), new Vector2(cx, top), FixtureOrder, new Vector2(8 * Px, ceiling - top));
                }
            }
            foreach (var b in plan.Basins)
            {
                Put("basin water", "basin-" + b.Width, Vector2.zero, new Vector2(b.X0, b.Floor - 1), WaterOrder);
                Put("basin water front", "basin-front-" + b.Width, Vector2.zero, new Vector2(b.X0, b.Floor - 1), WaterFrontOrder);
                if (b.Fall) Put("basin splash", "basin-splash", new Vector2(12, 3), new Vector2(Mathf.Round(b.FallX * 32) * Px, b.Floor), WaterOrder + 1);
                BasinCount++;
            }
        }

        // 땅에 닿는 뒷벽 그늘(v26: 땅까지 0~10px 를 어둡게). 공기 칸 쪽으로 10px 띠를 땅 면마다 놓는다(한 메시).
        void BuildContactShadow(Chapter2SewerPlan plan, int x0, int x1, int y0, int y1)
        {
            var tex = Resources.Load<Texture2D>(Root + "contact-shadow"); if (!tex) return;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); const float h = 10 * Px;
            void Strip(Vector2 a, Vector2 b, Vector2 c, Vector2 d)   // a·b = 땅 쪽(진함), c·d = 바깥(옅음)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                {
                    if (!plan.Solid(x, y)) continue;
                    if (!plan.Solid(x, y + 1)) Strip(new Vector2(x, y + 1), new Vector2(x + 1, y + 1), new Vector2(x + 1, y + 1 + h), new Vector2(x, y + 1 + h));
                    if (!plan.Solid(x, y - 1)) Strip(new Vector2(x + 1, y), new Vector2(x, y), new Vector2(x, y - h), new Vector2(x + 1, y - h));
                    if (!plan.Solid(x - 1, y)) Strip(new Vector2(x, y), new Vector2(x, y + 1), new Vector2(x - h, y + 1), new Vector2(x - h, y));
                    if (!plan.Solid(x + 1, y)) Strip(new Vector2(x + 1, y + 1), new Vector2(x + 1, y), new Vector2(x + 1 + h, y), new Vector2(x + 1 + h, y + 1));
                }
            var go = new GameObject("Sewer v26 · contact shadow"); go.transform.SetParent(transform, false);
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
            var mat = new Material(material) { mainTexture = tex }; owned.Add(m); owned.Add(mat);
            go.AddComponent<MeshFilter>().sharedMesh = m; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.sortingOrder = ShadowOrder;
        }

        readonly List<(Vector2 at, List<GameObject> parts)> lamps = new List<(Vector2, List<GameObject>)>();

        // 뒷벽: 맵 전체를 덮는 사각형 하나를 SewerWall 셰이더로 칠한다. 빛 지도는 8px 격자, 등(반지름 210px, 3.8단)과
        // 채광(기운 빛줄기 2.3단 + 격자 둘레 1.5단)을 v26 렌더러 식 그대로 더한다. R = 빛 단계 / 6.5, G = 따뜻한 정도(등 150px).
        // v26 그대로는 예전 화면보다 40% 어둡다(사용자 「맵이 너무 어둡다」 11-12). 뒷벽·배관 기본 밝기를 1.4단 올린다. 돌 무늬·등빛 대비는 그대로다.
        public const float AmbientLevels = 1.4f;
        public Texture2D LightMap { get; private set; }
        float[] lightL, lightW; int lightW_, lightH_; Vector2 lightOrigin;
        // 그 자리의 빛 단계(L, 등 0~3.8 남짓)와 따뜻한 정도(W, 0~1). 캐릭터 색 맞춤(Chapter2SewerActorLight)이 쓴다.
        public (float L, float W) LightAt(Vector2 at)
        {
            if (lightL == null) return (0, 0);
            int i = Mathf.FloorToInt((at.x - lightOrigin.x) * 4), j = Mathf.FloorToInt((at.y - lightOrigin.y) * 4);
            if (i < 0 || j < 0 || i >= lightW_ || j >= lightH_) return (0, 0);
            return (Mathf.Min(lightL[j * lightW_ + i], 6.5f), lightW[j * lightW_ + i]);
        }
        void BuildWall(Chapter2SewerPlan plan, Rect bounds)
        {
            var shader = Resources.Load<Shader>(Root + "SewerWall"); var level = Resources.Load<Texture2D>(Root + "wall-level"); var ramp = Resources.Load<Texture2D>(Root + "ramp");
            if (!shader || !level || !ramp) throw new InvalidOperationException("v26 sewer wall shader or maps missing; restore deployment assets.");
            const int Cell = 8; int w = Mathf.CeilToInt(bounds.width * 32 / Cell), h = Mathf.CeilToInt(bounds.height * 32 / Cell);
            var L = new float[w * h]; var W = new float[w * h];
            float ox = bounds.xMin * 32, oy = bounds.yMin * 32;   // 월드 화소, 위로 +
            foreach (var at in plan.Lamps.Select(l => l.At).Concat(plan.PaintedLamps))
            {
                float lx = at.x * 32, ly = at.y * 32;
                int i0 = Mathf.Max(0, (int)((lx - 212 - ox) / Cell)), i1 = Mathf.Min(w - 1, (int)((lx + 212 - ox) / Cell));
                int j0 = Mathf.Max(0, (int)((ly - 186 - oy) / Cell)), j1 = Mathf.Min(h - 1, (int)((ly + 186 - oy) / Cell));
                for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++)
                {
                    float dx = ox + (i + .5f) * Cell - lx, dy = oy + (j + .5f) * Cell - ly, d = Mathf.Sqrt(dx * dx + dy * 1.15f * dy * 1.15f);
                    if (d >= 210) continue; int k = j * w + i;
                    L[k] += Mathf.Pow(1 - d / 210, 1.9f) * 3.8f; if (d < 150) W[k] = Mathf.Max(W[k], Mathf.Pow(1 - d / 150, 1.6f));
                }
            }
            foreach (var at in plan.Shafts)
            {
                float sx = at.x * 32, sy = at.y * 32;
                int i0 = Mathf.Max(0, (int)((sx - 80 - ox) / Cell)), i1 = Mathf.Min(w - 1, (int)((sx + 200 - ox) / Cell));
                int j0 = Mathf.Max(0, (int)((sy - 430 - oy) / Cell)), j1 = Mathf.Min(h - 1, (int)((sy + 70 - oy) / Cell));
                for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++)
                {
                    float x = ox + (i + .5f) * Cell, dy = sy - (oy + (j + .5f) * Cell), cxl = sx + dy * .22f, half = 14 + dy * .09f; int k = j * w + i;
                    if (dy > 0 && Mathf.Abs(x - cxl) < half) L[k] += Mathf.Clamp01(1 - dy / 420) * Mathf.Clamp01((half - Mathf.Abs(x - cxl)) / 6) * 2.3f;
                    float d2 = Mathf.Sqrt((x - sx) * (x - sx) + dy * 1.6f * dy * 1.6f); L[k] += Mathf.Clamp01(1 - d2 / 60) * 1.5f;
                }
            }
            foreach (var water in plan.Waters)
            {
                float sx = water.X * 32, fy = water.Floor * 32, outY = water.Outlet * 32, mid = (outY + fy) / 2, half = (outY - fy) / 2;
                int i0 = Mathf.Max(0, (int)((sx - 50 - ox) / Cell)), i1 = Mathf.Min(w - 1, (int)((sx + 50 - ox) / Cell));
                int j0 = Mathf.Max(0, (int)((fy - 50 - oy) / Cell)), j1 = Mathf.Min(h - 1, (int)((outY + 50 - oy) / Cell));
                for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++)
                {
                    float dx = ox + (i + .5f) * Cell - sx, dy = Mathf.Max(0, Mathf.Abs(oy + (j + .5f) * Cell - mid) - half);
                    L[j * w + i] += Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) / 46) * 1.1f;
                }
            }
            lightL = L; lightW = W; lightW_ = w; lightH_ = h; lightOrigin = bounds.min;
            var px = new Color32[w * h];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(L[k] / 6.5f) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(W[k]) * 255), 0, 255);
            LightMap = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "v26 sewer light map" };
            LightMap.SetPixels32(px); LightMap.Apply(false, true); owned.Add(LightMap);
            var mat = new Material(shader) { name = "v26 sewer wall" }; owned.Add(mat);
            mat.SetTexture("_Level", level); mat.SetTexture("_Ramp", ramp); mat.SetTexture("_Light", LightMap); mat.SetFloat("_Ambient", AmbientLevels); mat.SetTexture("_WallLevel", level);
            mat.SetVector("_LightRect", new Vector4(bounds.xMin, bounds.yMin, w * Cell / 32f, h * Cell / 32f));
            var go = new GameObject("Sewer v26 · rear wall"); go.transform.SetParent(transform, false);
            var m = new Mesh(); m.SetVertices(new List<Vector3> { new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax), new Vector2(bounds.xMin, bounds.yMax) });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0); m.RecalculateBounds(); owned.Add(m);
            go.AddComponent<MeshFilter>().sharedMesh = m; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.sortingOrder = WallOrder;
            wallMaterial = mat;
        }
        Material wallMaterial;

        // 가로 배관: 뒷벽 셰이더에 배관 단계 지도(160×22, 아래 4줄은 관 그늘)를 주어 등빛을 같이 받는다. 땅·문 앞 칸은 끊는다.
        void BuildPipes(Chapter2SewerPlan plan)
        {
            var level = Resources.Load<Texture2D>(Root + "pipe-level"); if (!level || plan.Pipes.Count == 0) return;
            foreach (var pipe in plan.Pipes)
            {
                float y0 = pipe.Top - 22 * Px; var v = new List<Vector3>(); var t = new List<int>();
                int start = int.MinValue;
                for (int x = Mathf.FloorToInt(pipe.X0); x <= Mathf.CeilToInt(pipe.X1); x++)
                {
                    bool open = x < pipe.X1 && !plan.Solid(x, Mathf.FloorToInt(y0)) && !plan.Solid(x, Mathf.FloorToInt(pipe.Top - .01f))
                        && !plan.Doors.Exists(d => d.Frame.Overlaps(new Rect(x, y0, 1, 22 * Px)));
                    if (open && start == int.MinValue) start = x;
                    if (!open && start != int.MinValue)
                    {
                        int i = v.Count; v.Add(new Vector2(start, y0)); v.Add(new Vector2(x, y0)); v.Add(new Vector2(x, pipe.Top)); v.Add(new Vector2(start, pipe.Top));
                        t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); start = int.MinValue;
                    }
                }
                if (v.Count == 0) continue;
                var mat = new Material(wallMaterial) { name = "v26 sewer pipe" }; owned.Add(mat);
                mat.SetTexture("_Level", level); mat.SetTexture("_WallLevel", Resources.Load<Texture2D>(Root + "wall-level"));
                mat.SetVector("_Tile", new Vector4(160, 22, 0, Mathf.Round(y0 * 32)));
                var go = new GameObject("Sewer v26 · pipe"); go.transform.SetParent(transform, false);
                var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); owned.Add(m);
                go.AddComponent<MeshFilter>().sharedMesh = m; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.sortingOrder = PipeOrder;
            }
        }

        // 깊이감(v26 맵 렌더러와 같은 값): 천장에서 220px 안은 어둡게, 바닥에서 140px 안은 안개, 바닥 위 26~35px 은 물때.
        // 공기 칸 세로 줄마다 띠를 놓는다. 땅에 걸치는 부분은 땅 그림이 덮는다.
        void BuildDepth(Chapter2SewerPlan plan, int x0, int x1, int y0, int y1)
        {
            var ceiling = new List<Rect>(); var floor = new List<Rect>();
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                {
                    if (plan.Solid(x, y)) continue;
                    if (plan.Solid(x, y + 1)) ceiling.Add(new Rect(x, y + 1 - 224 * Px, 1, 224 * Px));
                    if (plan.Solid(x, y - 1)) floor.Add(new Rect(x, y, 1, 1));
                }
            Strips("ceiling shade", "ceiling-shade", ceiling, false, CeilingOrder);
            Strips("floor fog", "floor-fog", floor.ConvertAll(r => new Rect(r.x, r.y, 1, 144 * Px)), false, FogOrder);
            Strips("wet band", "wet-band", floor.ConvertAll(r => new Rect(r.x, r.y, 1, 40 * Px)), true, WetOrder);
        }
        void Strips(string name, string key, List<Rect> rects, bool worldU, int order)
        {
            var tex = Resources.Load<Texture2D>(Root + key); if (!tex || rects.Count == 0) return;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            foreach (var r in rects)
            {
                int i = v.Count; float u0 = worldU ? r.x * 32f / tex.width : 0, u1 = worldU ? u0 + 32f / tex.width : 1;
                v.Add(new Vector2(r.xMin, r.yMin)); v.Add(new Vector2(r.xMax, r.yMin)); v.Add(new Vector2(r.xMax, r.yMax)); v.Add(new Vector2(r.xMin, r.yMax));
                uv.Add(new Vector2(u0, 0)); uv.Add(new Vector2(u1, 0)); uv.Add(new Vector2(u1, 1)); uv.Add(new Vector2(u0, 1));
                t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            var go = new GameObject("Sewer v26 · " + name); go.transform.SetParent(transform, false);
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
            var mat = new Material(material) { mainTexture = tex }; owned.Add(m); owned.Add(mat);
            go.AddComponent<MeshFilter>().sharedMesh = m; var rr = go.AddComponent<MeshRenderer>(); rr.sharedMaterial = mat; rr.sortingOrder = order;
        }

        void OnDestroy() { foreach (var o in owned) if (o) Destroy(o); }
    }
}
