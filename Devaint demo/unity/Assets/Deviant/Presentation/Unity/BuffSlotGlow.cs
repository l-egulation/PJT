using System;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    /// <summary>
    /// 버프칸(24×24 게임 화소) 위에 겹치는 발동·지속 연출. 표시 전용: 전투의 발동·시계를 바꾸지 않는다.
    /// 사용자 선택 D「섬광 + 내려가는 수위」(2026-11-15): 발동하면 흰 섬광이 아이콘을 사선으로 훑고 오른쪽 위에 별빛이 터진다.
    /// 지속 중에는 아이콘 안이 잉크 빛으로 차 있고 수면이 남은 지속 비율만큼 위에서 아래로 내려가며, 수면에서 빛 알갱이가 떠오른다.
    /// 끝 무렵에는 깜빡이고, 끝나면 바닥 줄이 한 번 번쩍인다. 쿨타임이 끝나면 테두리를 한 번 훑는다.
    /// 표시 규칙은 docs/design/ui/screens.md의 버프칸 명세를 따른다.
    /// </summary>
    public sealed class BuffSlotGlow
    {
        public const int Icon = 24, Half = 12, Margin = 10, Width = Icon + Margin * 2, Height = Width;
        public const float Gleam = .22f, RiseFrom = .05f, Rise = .3f, StarFrom = .05f, Star = .45f, EndFlash = .2f, Ping = .3f;
        /// <summary>끝 무렵 깜빡임: 지속의 마지막 1/3, 최대 이 초.</summary>
        public const float BlinkMax = 1.5f;
        const float Edge = .05f;

        static readonly Color32 White = new Color32(255, 255, 255, 255);
        /// <summary>빛 알갱이 생성 간격(초). 알갱이마다 수명 MoteLife ± 0.3초 동안 MoteRise 화소를 올라간다.</summary>
        public const float MoteEvery = .1f, MoteLife = 1f, MoteRise = 14;
        const int MoteCatchUp = 4;
        // 태어난 자리·시각을 기억하는 실제 파티클: 수면이 갱신으로 다시 올라가도 이미 뜬 알갱이는 제 길을 마저 간다.
        struct Mote { public float X, Y, Born, Life, Phase; }
        readonly System.Collections.Generic.List<Mote> motes = new System.Collections.Generic.List<Mote>();
        readonly System.Random rng = new System.Random(7);
        float nextMote = float.NegativeInfinity;
        public int MoteCount => motes.Count;
        static readonly Vector2Int[] Ring = BuildRing();

        /// <summary>아래쪽 행부터(Texture2D.SetPixels32 순서). 아이콘 왼쪽 위가 (Margin, Margin)이다.</summary>
        public Color32[] Pixels { get; } = new Color32[Width * Height];
        readonly Color32[] top = new Color32[Width * Height];

        string key;
        bool observed;
        float active, duration, cooldown, now, previousActive, previousCooldown;
        public float TriggeredAt { get; private set; } = float.NegativeInfinity;
        public float EndedAt { get; private set; } = float.NegativeInfinity;
        public float ReadyAt { get; private set; } = float.NegativeInfinity;

        public bool Active => active > 0;
        /// <summary>남은 지속 비율(0..1).</summary>
        public float Left => duration > 0 ? Mathf.Clamp01(active / duration) : 0;
        /// <summary>지속 중의 수면 높이: 차오르는 동안은 아래에서 올라오고, 이후 남은 비율을 따른다.</summary>
        public float Fill { get { float a = now - TriggeredAt; return (a >= 0 && a < Rise ? EaseOutCubic(Seg(a, RiseFrom, Rise)) : 1) * Left; } }
        /// <summary>수면 줄(아이콘 기준 y, 위가 -12). 아이콘 아래 끝(12)이면 비었다.</summary>
        public int Level => Half - Mathf.RoundToInt(Icon * Fill);
        public bool Blinking => active > 0 && active < Mathf.Min(duration / 3, BlinkMax) && Mathf.FloorToInt(now * 6) % 2 == 1;
        public bool Animating => active > 0 || motes.Count > 0 || now - TriggeredAt < Star + .05f || now - EndedAt < EndFlash || now - ReadyAt < Ping;

        public void Reset()
        {
            observed = false; key = null; active = cooldown = previousActive = previousCooldown = 0;
            TriggeredAt = EndedAt = ReadyAt = nextMote = float.NegativeInfinity; motes.Clear();
        }

        /// <summary>
        /// 칸의 실제 남은 지속·쿨타임을 관측한다. 칸 열쇠(그림·조건 등)가 바뀌거나 처음 보면 연출 없이 기준값만 잡는다.
        /// 준비되어 있던 칸에서 지속이나 쿨타임이 새로 생기면 발동으로 본다. 지속 중 갱신(빨강)은 수면만 다시 올린다.
        /// </summary>
        public void Observe(string slotKey, float activeLeft, float activeDuration, float cooldownLeft, float time)
        {
            activeLeft = Mathf.Max(0, activeLeft); cooldownLeft = Mathf.Max(0, cooldownLeft);
            if (!observed || slotKey != key)
            {
                observed = true; key = slotKey;
                TriggeredAt = EndedAt = ReadyAt = nextMote = float.NegativeInfinity; motes.Clear();
            }
            else
            {
                bool idle = previousActive <= 0;
                if (idle && (activeLeft > previousActive + Edge || cooldownLeft > previousCooldown + Edge)) TriggeredAt = time;
                if (!idle && activeLeft <= 0) EndedAt = time;
                if (previousCooldown > 0 && cooldownLeft <= 0 && activeLeft <= 0) ReadyAt = time;
            }
            previousActive = active = activeLeft; previousCooldown = cooldown = cooldownLeft;
            duration = Mathf.Max(activeDuration, activeLeft); now = time;
            StepMotes();
        }

        // 지속 중(깜빡임 제외)에는 수면에서 일정 간격으로 알갱이를 낳는다. 태어난 시각은 간격대로 정확히 두어
        // 화면 주기와 상관없이 고르게 뜨고, 일시정지 뒤처럼 시간이 크게 뛰면 최근 몇 개만 낳는다.
        void StepMotes()
        {
            for (int i = motes.Count - 1; i >= 0; i--) if (now - motes[i].Born >= motes[i].Life || now < motes[i].Born) motes.RemoveAt(i);
            if (!(active > 0) || Blinking) { nextMote = float.NegativeInfinity; return; }
            if (float.IsNegativeInfinity(nextMote) || now - nextMote > MoteEvery * MoteCatchUp) nextMote = now - MoteEvery * .5f;
            float level = Level;
            for (; nextMote <= now; nextMote += MoteEvery)
                motes.Add(new Mote { X = -10 + (float)rng.NextDouble() * 19, Y = level, Born = nextMote, Life = MoteLife + ((float)rng.NextDouble() - .5f) * .6f, Phase = (float)rng.NextDouble() });
        }

        /// <summary>
        /// 이번 화면을 그린다. ramp: 잉크 색 6단계(어두움 → 흰색). iconMask: 24×24 위에서 아래 순서의 아이콘 불투명 여부(null 이면 전체).
        /// </summary>
        public void Paint(Color32[] ramp, bool[] iconMask)
        {
            if (ramp == null || ramp.Length < 6) throw new ArgumentException("six ramp steps", nameof(ramp));
            Array.Clear(top, 0, top.Length);
            float a = now - TriggeredAt;
            bool live = active > 0, blink = Blinking, gleaming = a >= 0 && a < Gleam;
            float fill = Fill; int level = Level;
            float sweep = Seg(a, 0, Gleam) * 60 - 30;

            // 아이콘 층: 섬광 띠, 수면 아래 잉크 빛, 수면 줄
            for (int y = -Half; y < Half; y++)
                for (int x = -Half; x < Half; x++)
                {
                    if (iconMask != null && !iconMask[(y + Half) * Icon + x + Half]) continue;
                    if (gleaming)
                    {
                        float d = Mathf.Abs(x + y - sweep);
                        if (d < 2) { Blend(x, y, White, 1); continue; }
                        if (d < 4) { Blend(x, y, ramp[5], .6f); continue; }
                    }
                    if (live && !blink && y >= level) Blend(x, y, y == level ? ramp[5] : ramp[4], y == level ? .75f : .18f);
                }

            if (live && !blink)
            {
                // 수면 물결 빛, 잠긴 쪽 은은한 빛, 떠오르는 빛 알갱이
                for (int x = -Half; x < Half; x++) Blend(x, level - (Mathf.Sin(x * .7f + now * 9) > .6f ? 1 : 0), ramp[4], .3f);
                Glow(0, (level + Half) * .5f, 12, ramp[3], .22f * fill + .05f);
            }
            // 떠오르는 빛 알갱이: 지속이 끝나거나 깜빡이거나 갱신돼도 이미 뜬 것은 끝까지 올라가며 사라진다.
            foreach (var m in motes)
            {
                float u = Mathf.Clamp01((now - m.Born) / m.Life), my = m.Y - u * MoteRise, mx = m.X + Mathf.Sin(u * Mathf.PI * 2 + m.Phase * 9) * 1.5f;
                // 2×2 알갱이(왼쪽 위가 가장 밝다)로 떠올라, 위로 갈수록 1화소로 줄며 성기게 사라진다.
                var head = u < .3f ? ramp[5] : ramp[4];
                if (u < .55f)
                {
                    int ix = Mathf.FloorToInt(mx), iy = Mathf.FloorToInt(my);
                    Blend(ix, iy, head, 1); Blend(ix + 1, iy, ramp[4], 1); Blend(ix, iy + 1, ramp[4], 1); Blend(ix + 1, iy + 1, ramp[3], 1);
                }
                else Dot(mx, my, head, 1 - u);
                Glow(mx + .5f, my + .5f, 3, ramp[4], .35f * (1 - u));
            }

            // 별빛(오른쪽 위)
            if (a >= StarFrom && a < Star)
            {
                float u = Seg(a, StarFrom, Star); int arm = Mathf.RoundToInt(6 * Mathf.Sin(u * Mathf.PI));
                Glow(9, -10, 8, ramp[4], .6f * (1 - u));
                for (int i = -arm; i <= arm; i++)
                {
                    var c = Mathf.Abs(i) < 2 ? White : ramp[4];
                    Blend(9 + i, -10, c, 1); Blend(9, -10 + i, c, 1);
                }
            }

            // 지속 끝: 바닥 줄이 한 번 번쩍
            float e = now - EndedAt;
            if (e >= 0 && e < EndFlash) for (int x = -Half; x < Half; x++) Dot(x, Half - 1, ramp[4], 1 - e / EndFlash);

            // 준비 신호: 아이콘 테두리를 12시부터 시계방향으로 한 번 훑는다
            float r = now - ReadyAt;
            if (r >= 0 && r < Ping)
            {
                int head = Mathf.FloorToInt(r / Ping * Ring.Length * 1.2f);
                for (int i = 0; i < Ring.Length; i++)
                {
                    int d = head - i;
                    if (d >= 0 && d < 10) Blend(Ring[i].x, Ring[i].y, d < 3 ? White : ramp[4], .8f * (1 - d / 10f));
                }
            }

            for (int ty = 0; ty < Height; ty++) Array.Copy(top, ty * Width, Pixels, (Height - 1 - ty) * Width, Width);
        }

        /// <summary>시험용: 아이콘 기준 좌표(왼쪽 위 -12, -12)의 화소.</summary>
        public Color32 At(int x, int y) => Pixels[(Height - 1 - (y + Half + Margin)) * Width + x + Half + Margin];

        void Blend(int x, int y, Color32 c, float a)
        {
            int tx = x + Half + Margin, ty = y + Half + Margin;
            if (tx < 0 || ty < 0 || tx >= Width || ty >= Height || a <= 0) return;
            a = Mathf.Clamp01(a);
            ref var d = ref top[ty * Width + tx];
            float da = d.a / 255f, oa = a + da * (1 - a);
            if (oa <= 0) return;
            float k = da * (1 - a);
            d = new Color32((byte)((c.r * a + d.r * k) / oa), (byte)((c.g * a + d.g * k) / oa), (byte)((c.b * a + d.b * k) / oa), (byte)Mathf.RoundToInt(oa * 255));
        }
        // 순서 디더(4×4 Bayer)로 점을 찍는다: 반투명 대신 화소가 성기게 빠진다.
        void Dot(float x, float y, Color32 c, float a)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            if (a >= 1 || a > Bayer(ix + Half + Margin, iy + Half + Margin)) Blend(ix, iy, c, 1);
        }
        void Glow(float cx, float cy, float radius, Color32 c, float k)
        {
            if (k <= .004f) return;
            int x0 = Mathf.FloorToInt(cx - radius), x1 = Mathf.CeilToInt(cx + radius), y0 = Mathf.FloorToInt(cy - radius), y1 = Mathf.CeilToInt(cy + radius);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - cx) * (x + .5f - cx) + (y + .5f - cy) * (y + .5f - cy)) / radius;
                    if (d < 1) Blend(x, y, c, k * (1 - d) * (1 - d));
                }
        }

        static readonly int[] BayerMatrix = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Bayer(int x, int y) => (BayerMatrix[(y & 3) * 4 + (x & 3)] + .5f) / 16;
        static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        static float EaseOutCubic(float t) { t = 1 - t; return 1 - t * t * t; }
        // 아이콘 가장자리 한 줄, 12시(위 가운데)부터 시계방향
        static Vector2Int[] BuildRing()
        {
            var ring = new System.Collections.Generic.List<Vector2Int>();
            int l = -Half, r = Half - 1, t = -Half, b = Half - 1;
            for (int x = 0; x <= r; x++) ring.Add(new Vector2Int(x, t));
            for (int y = t + 1; y <= b; y++) ring.Add(new Vector2Int(r, y));
            for (int x = r - 1; x >= l; x--) ring.Add(new Vector2Int(x, b));
            for (int y = b - 1; y >= t; y--) ring.Add(new Vector2Int(l, y));
            for (int x = l + 1; x < 0; x++) ring.Add(new Vector2Int(x, t));
            return ring.ToArray();
        }
    }
}
