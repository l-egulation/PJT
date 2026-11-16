using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    /// <summary>
    /// 전투 HUD 체력 게이지 위에 겹치는 잉크 연출(게임 화소 단위). 표시 전용: 체력·보호막을 바꾸지 않는다.
    /// 회복은 붓이 빈 칸을 칠하고(붓 그림 없음), 보호막은 틀을 민트로 덧그리며 체력 끝 뒤 칸을 칠하고,
    /// 피해는 깎인 칸이 연해진 뒤 오른쪽부터 지워진다(지우개 그림 없음). 원본 시안은 전투테스트/체력바(pixelfx)다.
    /// </summary>
    public sealed class HealthBarInk
    {
        public const float Stroke = .26f, Dry = .55f, RubDelay = .1f, Rub = .36f, ShieldDraw = .42f, Unravel = .3f;
        const float GainMerge = .5f;

        static readonly Color32 Clear = new Color32(0, 0, 0, 0), White = new Color32(255, 255, 255, 255);
        // 도트 체력판의 게이지 빨강(위 한 줄 밝게, 아래 두 줄 어둡게). 마른 붓칠도 같은 줄 색으로 끝난다.
        public static readonly Color32 Red = new Color32(255, 107, 94, 255);
        public static readonly Color32 RedDeep = new Color32(217, 71, 62, 255), RedLit = new Color32(255, 154, 140, 255), RedWet = new Color32(255, 214, 204, 255);
        static readonly Color32 Smudge = new Color32(58, 28, 26, 255), Pink = new Color32(233, 183, 176, 255), PinkDark = new Color32(167, 120, 112, 255);
        static readonly Color32 Mint = new Color32(95, 232, 192, 255), MintDeep = new Color32(53, 196, 154, 255), MintLit = new Color32(180, 247, 224, 255), MintWhite = new Color32(242, 255, 251, 255);

        public readonly int BarLength, FillWidth, FillHeight, MarginX, MarginY, Width, Height;
        /// <summary>게이지 위쪽이 오른쪽으로 밀린 화소 수(평행사변형). 값의 끝도 같은 기울기로 자른다.</summary>
        public readonly float Lean;
        public Color32[] Pixels { get; }

        sealed class HealStroke { public float From, To, At; public float[] Lag; public bool[] Streak; public float LastFront; }
        sealed class EraseChunk { public float From, To, At; public float[] Rag; public float CrumbClock; }
        struct Speck { public float X, Y, Vx, Vy, Age, Born, Life, Rise; public Color32 Color; public int Size; }

        readonly List<HealStroke> strokes = new List<HealStroke>();
        readonly List<EraseChunk> chunks = new List<EraseChunk>();
        readonly List<Speck> specks = new List<Speck>();
        readonly List<Vector2Int> ring = new List<Vector2Int>();
        readonly System.Random rng;
        bool observed;
        float health, maxHealth, shield, now, lastNow;
        // 보호막: 칠하기 시작 시각, 칠해진 양(성장 애니메이션 기준), 틀 덧그리기 시작점, 막음·깨짐 시각
        float shieldAt = float.NegativeInfinity, shieldGrowFrom, shieldGrowAt = float.NegativeInfinity, shieldHitAt = float.NegativeInfinity, breakAt = float.NegativeInfinity;
        int ringStart, ringShownAtBreak;
        float[] shieldLag;

        public HealthBarInk(int barLength, int fillHeight, float lean = 4, int marginX = 12, int marginY = 10, int seed = 1)
        {
            if (barLength < 8 || fillHeight < 3 || lean < 0) throw new ArgumentOutOfRangeException(nameof(barLength));
            BarLength = barLength; Lean = lean; FillWidth = barLength + Mathf.CeilToInt(lean); FillHeight = fillHeight; MarginX = marginX; MarginY = marginY;
            Width = FillWidth + marginX * 2; Height = fillHeight + marginY * 2;
            Pixels = new Color32[Width * Height];
            rng = new System.Random(seed);
            shieldLag = new float[fillHeight];
            for (int v = 0; v < fillHeight; v++) shieldLag[v] = Range(0, 5);
            BuildRing();
        }

        /// <summary>표시 중인 체력의 분모: 보호막이 최대 체력을 넘기면 게이지 전체를 그 합에 맞춰 줄인다.</summary>
        public float Total => Mathf.Max(1, Mathf.Max(maxHealth, health + shield));
        /// <summary>v(위에서 잰 높이, 화소 중심)에서 게이지 왼끝의 화소 위치.</summary>
        public float LeanAt(float v) => Lean * (1 - v / FillHeight);
        /// <summary>게이지 줄 v의 마른 빨강.</summary>
        public Color32 RedRow(int v) => v < 1 ? RedLit : v < FillHeight - 2 ? Red : RedDeep;
        /// <summary>바닥 게이지가 단색으로 그릴 몫(게이지 길이 비율). 아직 붓이 칠하지 않은 회복 칸은 빼 둔다.</summary>
        public float SolidFraction
        {
            get
            {
                float solid = health;
                foreach (var s in strokes) solid = Mathf.Min(solid, s.From);
                return Mathf.Clamp01(solid / Total);
            }
        }
        public bool Animating => strokes.Count > 0 || chunks.Count > 0 || specks.Count > 0 || shield > 0 || now - breakAt < Unravel + .05f;
        /// <summary>회복 숫자 색(0..1): 붓질 동안과 직후 초록.</summary>
        public float HealTint { get { float k = 0; foreach (var s in strokes) k = Mathf.Max(k, 1 - Seg(now - s.At, Stroke + .2f, Stroke + .45f)); return k; } }
        /// <summary>피해 숫자 색(0..1): 맞은 직후 0.35초 빨강.</summary>
        public float DamageTint { get { float k = 0; foreach (var c in chunks) k = Mathf.Max(k, 1 - Seg(now - c.At, .2f, .35f)); return k; } }
        /// <summary>초상 테두리: 붓질이 끝나 갈 때 초록, 맞은 직후 0.1초 빨강.</summary>
        public bool HealRing { get { foreach (var s in strokes) { float k = now - s.At; if (k > Stroke * .7f && k < Stroke + .12f) return true; } return false; } }
        public bool DamageRing { get { foreach (var c in chunks) if (now - c.At < .1f) return true; return false; } }
        public int GainAmount { get; private set; }
        public float GainAt { get; private set; } = float.NegativeInfinity;
        float gainRaw;

        public void Reset()
        {
            observed = false; strokes.Clear(); chunks.Clear(); specks.Clear(); GainAmount = 0; gainRaw = 0; GainAt = float.NegativeInfinity;
            shieldAt = shieldGrowAt = shieldHitAt = breakAt = float.NegativeInfinity; shield = 0;
        }

        /// <param name="struck">이번 관측 사이에 실제 피격이 있었는가. 아니면 줄어든 보호막은 시간 감소로 보고 칸만 줄인다.</param>
        public void Observe(float newHealth, float newMax, float newShield, float time, bool struck = true)
        {
            newHealth = Mathf.Max(0, newHealth); newMax = Mathf.Max(1, newMax); newShield = Mathf.Max(0, newShield);
            float dt = observed ? Mathf.Clamp(time - lastNow, 0, .1f) : 0; lastNow = now = time;
            if (!observed)
            {
                observed = true; health = newHealth; maxHealth = newMax; shield = newShield;
                if (shield > 0) { shieldAt = time - ShieldDraw; shieldGrowAt = float.NegativeInfinity; ringStart = RingIndexAt(health / Total); }
                return;
            }
            maxHealth = newMax;
            if (newHealth > health + .001f) OnHeal(health, newHealth, time);
            else if (newHealth < health - .001f) OnDamage(newHealth, health, time);
            health = newHealth;
            OnShield(shield, newShield, time, struck);
            shield = newShield;
            // 회복이 지운 칸을 다시 채우거나 피해가 칠한 칸을 깎으면 이전 연출은 남은 범위만 그린다.
            for (int i = strokes.Count - 1; i >= 0; i--) { var s = strokes[i]; s.To = Mathf.Min(s.To, health); if (s.To <= s.From || time - s.At > Stroke + Dry + .5f) strokes.RemoveAt(i); }
            for (int i = chunks.Count - 1; i >= 0; i--) { var c = chunks[i]; c.From = Mathf.Max(c.From, health); if (c.To <= c.From || time - c.At > RubDelay + Rub + .55f) chunks.RemoveAt(i); }
            Step(dt);
        }

        void OnHeal(float from, float to, float time)
        {
            var s = new HealStroke { From = from, To = to, At = time, Lag = new float[FillHeight], Streak = new bool[FillHeight], LastFront = from };
            for (int v = 0; v < FillHeight; v++) { s.Lag[v] = Range(0, 7); s.Streak[v] = v > 0 && v < FillHeight - 1 && rng.NextDouble() < .35; }
            strokes.Add(s);
            gainRaw = time - GainAt < GainMerge ? gainRaw + (to - from) : to - from;
            GainAmount = Mathf.Max(1, Mathf.RoundToInt(gainRaw)); GainAt = time;
        }

        void OnDamage(float to, float from, float time)
        {
            var c = new EraseChunk { From = to, To = from, At = time, Rag = new float[FillHeight] };
            for (int v = 0; v < FillHeight; v++) c.Rag[v] = Range(0, 3);
            chunks.Add(c);
        }

        void OnShield(float before, float after, float time, bool struck)
        {
            if (before <= 0 && after > 0)
            {
                shieldAt = time; shieldGrowAt = float.NegativeInfinity; breakAt = float.NegativeInfinity;
                ringStart = RingIndexAt(health / Total);
            }
            else if (before > 0 && after <= 0)
            {
                breakAt = time; ringShownAtBreak = RingShown(time - .0001f);
                if (struck) Burst(health, health + before, 26);
            }
            else if (after < before - .001f && struck) { shieldHitAt = time; Burst(health + after, health + before, Mathf.Clamp(Mathf.RoundToInt((before - after) / Total * BarLength), 6, 14)); }
            else if (after > before + .001f) { shieldGrowFrom = before; shieldGrowAt = time; }
        }

        void Burst(float from, float to, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float u = Range(from, to) / Total, v = Range(0, FillHeight), an = Range(-2.6f, -.4f), sp = Range(30, 110);
                specks.Add(new Speck { Born = now, X = MarginX + LeanAt(v) + u * BarLength, Y = MarginY + v, Vx = Mathf.Cos(an) * sp, Vy = Mathf.Sin(an) * sp, Life = Range(.3f, .7f), Rise = 20, Color = Pick(MintWhite, Mint, MintDeep), Size = rng.NextDouble() < .3 ? 2 : 1 });
            }
        }

        void Step(float dt)
        {
            // 붓 앞에서 잉크가 앞쪽으로 튄다(떨어지는 방울 없음).
            foreach (var s in strokes)
            {
                float f = HealFront(s);
                if (now - s.At > 0 && now - s.At < Stroke + .02f && f > s.LastFront + .01f)
                {
                    int n = rng.Next(0, 3);
                    for (int i = 0; i < n; i++)
                    {
                        float an = Range(-1.1f, 1.1f), sp = Range(60, 150);
                        float fy = Range(1, FillHeight - 1);
                        specks.Add(new Speck { Born = now, X = MarginX + LeanAt(fy) + f / Total * BarLength + 2, Y = MarginY + fy, Vx = Mathf.Cos(an) * sp, Vy = Mathf.Sin(an) * sp * .7f, Life = Range(.14f, .3f), Color = rng.NextDouble() < .5 ? RedLit : Red, Size = rng.NextDouble() < .3 ? 2 : 1 });
                    }
                }
                s.LastFront = f;
            }
            // 지워지는 자리에서 가루가 옆으로 튄다.
            foreach (var c in chunks)
            {
                float k = now - c.At - RubDelay;
                if (k <= 0 || k >= Rub) continue;
                c.CrumbClock += dt * 18;
                while (c.CrumbClock >= 1)
                {
                    c.CrumbClock -= 1;
                    float an = Range(-2.9f, -.2f), u = RubAt(c) / Total;
                    specks.Add(new Speck { Born = now, X = MarginX + u * BarLength, Y = MarginY + FillHeight, Vx = Mathf.Cos(an) * Range(30, 80), Vy = Mathf.Sin(an) * Range(20, 60), Life = Range(.2f, .4f), Color = Pick(Pink, PinkDark, RedDeep), Size = 1 });
                }
            }
            for (int i = specks.Count - 1; i >= 0; i--)
            {
                var p = specks[i]; p.Age = now - p.Born;
                if (p.Age >= p.Life) { specks.RemoveAt(i); continue; }
                p.X += p.Vx * dt; p.Y += p.Vy * dt; float drag = 1 - (p.Rise > 0 ? 3 : 5) * dt; p.Vx *= drag; p.Vy = p.Vy * drag - p.Rise * dt;
                specks[i] = p;
            }
        }

        float HealFront(HealStroke s) => s.From + (s.To - s.From) * OutCubic(Seg(now - s.At, 0, Stroke));
        float RubReached(EraseChunk c) => c.To - (c.To - c.From) * InOutQuad(Seg(now - c.At - RubDelay, 0, Rub));
        float RubAt(EraseChunk c) { float k = Seg(now - c.At - RubDelay, 0, Rub); return RubReached(c) + Mathf.Sin(k * Mathf.PI * 7) * 3 * (1 - k) * Total / BarLength; }

        /// <summary>현재 시각의 연출을 Pixels(아래쪽 행부터, Texture2D.SetPixels32 순서)에 그린다.</summary>
        public void Paint()
        {
            Array.Clear(Pixels, 0, Pixels.Length);
            float total = Total, pxPerHp = BarLength / total;
            float healthX = health * pxPerHp;

            // 보호막 틀 덧그리기(게이지 바로 바깥, 판의 크림 선 위)
            int shownRing = RingShown(now);
            for (int i = 0; i < shownRing; i++) { var q = ring[(ringStart + i) % ring.Count]; Put(q.x, q.y, Mint); }
            if (shownRing > 0 && shownRing < ring.Count && now - shieldAt < ShieldDraw) { var q = ring[(ringStart + shownRing - 1) % ring.Count]; Put(q.x, q.y, White); }

            for (int fy = 0; fy < FillHeight; fy++)
                for (int fx = 0; fx < FillWidth; fx++)
                {
                    if (!Inside(fx, fy)) continue;
                    float x = fx + .5f - LeanAt(fy + .5f); int v = fy;
                    Color32? c = null;
                    if (x < healthX) c = HealPixel(x, v, pxPerHp);
                    else c = ChunkPixel(x, v, pxPerHp) ?? ShieldPixel(x, v, pxPerHp);
                    if (c.HasValue) Put(fx + MarginX, fy + MarginY, c.Value);
                }

            foreach (var p in specks)
            {
                float a = 1 - p.Age / p.Life;
                if (a * 1.6f < .5f && ((int)p.X + (int)p.Y) % 2 == 0) continue; // 사라질 때는 화소가 듬성듬성 빠진다
                for (int dy = 0; dy < p.Size; dy++) for (int dx = 0; dx < p.Size; dx++) Put(Mathf.RoundToInt(p.X) + dx, Mathf.RoundToInt(p.Y) + dy, p.Color);
            }
            // 회복 끝점의 반짝임, 보호막이 다 칠해진 순간의 반짝임
            foreach (var s in strokes) { float g = now - s.At - Stroke; if (g > 0 && g < .3f) Sparkle(MarginX + LeanAt(1.5f) + s.To * pxPerHp + 1, MarginY + 1, Mathf.RoundToInt(4 * (1 - g / .3f)) + 1, RedWet); }
            float sg = now - shieldAt - ShieldDraw;
            if (shield > 0 && sg > 0 && sg < .25f) Sparkle(MarginX + LeanAt(.5f) + (health + shield) * pxPerHp, MarginY, Mathf.RoundToInt(4 * (1 - sg / .25f)) + 1, MintLit);
        }

        Color32? HealPixel(float x, int v, float pxPerHp)
        {
            foreach (var s in strokes)
            {
                float a = s.From * pxPerHp, b = s.To * pxPerHp;
                if (x < a || x >= b) continue;
                bool done = now - s.At >= Stroke;
                float front = HealFront(s) * pxPerHp;
                if (front - x - (done ? 0 : s.Lag[v]) < 0) return Clear;
                // 붓이 지나간 시각부터 마른다
                float passed = s.At + Stroke * InvOutCubic((x - a) / Mathf.Max(.001f, b - a));
                float age = now - passed;
                Color32 c;
                if (age < .05f) c = RedWet;
                else
                {
                    float wet = 1 - Seg(age, .05f, Dry);
                    if (wet > .66f) c = s.Streak[v] ? RedWet : RedLit;
                    else if (wet > .2f) c = s.Streak[v] ? RedLit : v < 1 ? RedWet : Red;
                    else c = RedRow(v);
                }
                // 마르고 나면 젖은 광택이 한 번 훑는다
                float sh = Seg(now - s.At, Stroke + .05f, Stroke + .45f);
                if (sh > 0 && sh < 1)
                {
                    float sx = a + (b - a) * InOutQuad(sh) + (FillHeight - v) * .6f;
                    if (Mathf.Abs(x - sx) < .5f) c = White; else if (Mathf.Abs(x - sx + 1) < .5f) c = RedWet;
                }
                return c;
            }
            return null;
        }

        Color32? ChunkPixel(float x, int v, float pxPerHp)
        {
            foreach (var c in chunks)
            {
                float a = c.From * pxPerHp, b = c.To * pxPerHp;
                if (x < a || x >= b) continue;
                float k = now - c.At;
                if (k < .07f) return White;
                if (x < RubReached(c) * pxPerHp - c.Rag[v] || k < RubDelay) return v < 1 ? White : RedWet;
                float age = k - RubDelay - Rub * ((b - x) / Mathf.Max(.001f, b - a));
                float s = 1 - Seg(age, .05f, .5f);
                // 지운 자리의 옅은 얼룩: 빗금 결로 듬성듬성, 옅어질수록 더 빠진다
                if (s > 0 && (v + Mathf.FloorToInt(x / 3)) % 3 != 0 && Dither((int)x, v) < s * .7f) return Smudge;
                return Clear;
            }
            return null;
        }

        Color32? ShieldPixel(float x, int v, float pxPerHp)
        {
            if (shield <= 0) return null;
            float a = health * pxPerHp, b = (health + shield) * pxPerHp;
            if (x < a || x >= b) return null;
            float dp = Seg(now - shieldAt, 0, ShieldDraw);
            float paint = a + (b - a) * OutCubic(dp);
            float gk = Seg(now - shieldGrowAt, 0, .2f);
            if (gk < 1) paint = Mathf.Min(paint, (health + shieldGrowFrom + (shield - shieldGrowFrom) * OutCubic(gk)) * pxPerHp);
            if (x > paint - (dp < 1 ? shieldLag[v] : 0)) return null;
            if (now - shieldHitAt < .08f && x > b - 3) return White;
            if (dp < 1 && paint - x < 4) return MintWhite;
            float sh = ((x - a + v * .6f - now * 30) % 40 + 40) % 40;
            if (sh < 1.5f) return MintWhite;
            return v < 1 ? MintLit : v < FillHeight - 2 ? Mint : MintDeep;
        }

        int RingShown(float t)
        {
            if (ring.Count == 0) return 0;
            if (breakAt > shieldAt && t >= breakAt) return Mathf.RoundToInt(ringShownAtBreak * (1 - InQuad(Seg(t - breakAt, 0, Unravel))));
            if (shield <= 0) return 0;
            return Mathf.RoundToInt(ring.Count * InOutQuad(Seg(t - shieldAt, 0, ShieldDraw)));
        }

        /// <summary>게이지 칸 안인가(fx, fy는 게이지 왼쪽 위 기준 화소, 위에서 아래로).</summary>
        public bool Inside(int fx, int fy)
        {
            if (fx < 0 || fy < 0 || fx >= FillWidth || fy >= FillHeight) return false;
            float xs = fx + .5f - LeanAt(fy + .5f);
            return xs >= 0 && xs < BarLength;
        }

        void BuildRing()
        {
            for (int ty = 0; ty < Height; ty++)
                for (int tx = 0; tx < Width; tx++)
                {
                    int fx = tx - MarginX, fy = ty - MarginY;
                    if (Inside(fx, fy)) continue;
                    if (Inside(fx - 1, fy) || Inside(fx + 1, fy) || Inside(fx, fy - 1) || Inside(fx, fy + 1)) ring.Add(new Vector2Int(tx, ty));
                }
            float cx = MarginX + FillWidth / 2f, cy = MarginY + FillHeight / 2f;
            ring.Sort((p, q) => Angle(p, cx, cy).CompareTo(Angle(q, cx, cy)));
        }
        static float Angle(Vector2Int p, float cx, float cy) => Mathf.Atan2((p.y + .5f - cy) * 20, p.x + .5f - cx);
        int RingIndexAt(float u)
        {
            float cx = MarginX + FillWidth / 2f, cy = MarginY + FillHeight / 2f, want = Mathf.Atan2((MarginY - .5f - cy) * 20, MarginX + Lean + u * BarLength - cx);
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < ring.Count; i++) { float d = Mathf.Abs(Mathf.DeltaAngle(Angle(ring[i], cx, cy) * Mathf.Rad2Deg, want * Mathf.Rad2Deg)); if (d < bestD) { bestD = d; best = i; } }
            return best;
        }

        void Sparkle(float x, float y, int arm, Color32 tip)
        {
            int cx = Mathf.RoundToInt(x), cy = Mathf.RoundToInt(y);
            Put(cx, cy, White);
            for (int i = 1; i <= arm; i++) { var c = i == arm ? tip : White; Put(cx + i, cy, c); Put(cx - i, cy, c); Put(cx, cy + i, c); Put(cx, cy - i, c); }
        }

        void Put(int tx, int ty, Color32 c)
        {
            if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) return;
            Pixels[(Height - 1 - ty) * Width + tx] = c;
        }
        /// <summary>시험용: 위에서 아래 순서 좌표의 화소.</summary>
        public Color32 At(int tx, int ty) => Pixels[(Height - 1 - ty) * Width + tx];

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        Color32 Pick(Color32 a, Color32 b, Color32 c) { int i = rng.Next(3); return i == 0 ? a : i == 1 ? b : c; }
        static float Dither(int x, int y) { int[] m = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 }; return (m[(y & 3) * 4 + (x & 3)] + .5f) / 16; }
        static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        static float OutCubic(float k) { k = 1 - k; return 1 - k * k * k; }
        static float InvOutCubic(float y) => 1 - Mathf.Pow(1 - Mathf.Clamp01(y), 1f / 3);
        static float InOutQuad(float k) => k < .5f ? 2 * k * k : 1 - Mathf.Pow(-2 * k + 2, 2) / 2;
        static float InQuad(float k) => k * k;
    }
}
