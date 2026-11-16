using System;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    /// <summary>
    /// 전투 HUD의 플레이어 체력판을 960x540 게임 화소로 그린다: 검은 판·크림 선, 도트 숫자, 평행사변형 빨간
    /// 게이지와 그 위의 회복·보호막·피해 연출(HealthBarInk). 판 왼쪽 끝은 HUD가 앞에 그리는 캐릭터 초상 그림 뒤에 숨는다.
    /// 모양은 pixelfx 시안 전투테스트/체력바/_bar.js와 같은 계산(화소 중심 판정)이다.
    /// </summary>
    public sealed class HealthPlaque
    {
        public const int Width = 276, Height = 60;
        // 시안의 원점(지역 좌표 0,0)이 놓이는 텍스처 화소
        const int OriginX = 16, OriginY = 34;
        // 게이지: 지역 좌표 왼쪽 아래 (BarX, BarY+BarH), 위쪽은 Lean만큼 오른쪽으로 민 평행사변형
        public const int BarX = 30, BarY = 1, BarLength = 200, BarH = 9;
        public const float Lean = 4;
        // 숫자 줄의 위쪽: 판 안쪽 위 선(-28)과 게이지 틀(0) 사이 빈 칸의 가운데. "/ 최대"는 작은 도트 2배.
        public const int NumberX = 38, NumberY = -21, MaxScale = 2;

        static readonly Color32 Clear = new Color32(0, 0, 0, 0), White = new Color32(255, 255, 255, 255);
        static readonly Color32 Edge = Hex(0x0b0b0a), Plate = Hex(0x151513), Cream = Hex(0xefe6d4), Track = Hex(0x0d0d0c), TrackHi = Hex(0x1a1917);
        static readonly Color32 Muted = Hex(0xbdb6a6);
        public static readonly Color32 HealGreen = Hex(0xa6dcb1), DamageRed = Hex(0xff9a8c);

        static readonly int[,] Plate0 = { { 12, -29 }, { 238, -29 }, { 245, -22 }, { 255, 6 }, { 249, 19 }, { 12, 19 }, { 5, 12 }, { 8, -24 } };

        public readonly HealthBarInk Ink;
        public Color32[] Pixels { get; } = new Color32[Width * Height];
        readonly Color32[] still = new Color32[Width * Height];
        readonly Color32[] top = new Color32[Width * Height];

        public HealthPlaque(int seed = 1)
        {
            Ink = new HealthBarInk(BarLength, BarH, Lean, 12, 10, seed);
            BuildStill();
        }

        static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        void BuildStill()
        {
            // 판: 바깥 먹선 2화소, 크림 선 1화소, 판 바탕
            Poly(Plate0, 2, Edge); Poly(Plate0, 1, Cream); Poly(Plate0, 0, Plate);
            // 게이지 칸과 크림 틀
            for (int ty = 0; ty < Height; ty++) for (int tx = 0; tx < Width; tx++)
                {
                    int lx = tx - OriginX, ly = ty - OriginY;
                    if (InBar(lx, ly, out _, out float v)) still[ty * Width + tx] = v < 1 ? TrackHi : Track;
                    else if (InBar(lx - 1, ly, out _, out _) || InBar(lx + 1, ly, out _, out _) || InBar(lx, ly - 1, out _, out _) || InBar(lx, ly + 1, out _, out _))
                        still[ty * Width + tx] = Cream;
                }
        }

        /// <summary>지역 좌표 화소(lx, ly)가 게이지 칸 안인가. xs는 게이지를 따라 잰 위치(0..BarLength), v는 줄.</summary>
        static bool InBar(int lx, int ly, out float xs, out float v)
        {
            float x = lx + .5f, y = ly + .5f; v = y - BarY; xs = x - BarX - Lean * (1 - v / BarH);
            return v >= 0 && v < BarH && xs >= 0 && xs < BarLength;
        }

        void Poly(int[,] pts, int grow, Color32 c)
        {
            int n = pts.GetLength(0); var px = new float[n]; var py = new float[n];
            for (int i = 0; i < n; i++) { px[i] = pts[i, 0] + (pts[i, 0] > 130 ? grow : -grow); py[i] = pts[i, 1] + (pts[i, 1] > 0 ? grow : -grow); }
            for (int ty = 0; ty < Height; ty++) for (int tx = 0; tx < Width; tx++)
                {
                    float x = tx + .5f - OriginX, y = ty + .5f - OriginY; bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                        if (py[i] > y != py[j] > y && x < (px[j] - px[i]) * (y - py[i]) / (py[j] - py[i]) + px[i]) inside = !inside;
                    if (inside) still[ty * Width + tx] = c;
                }
        }

        static void Local(int lx, int ly, Color32[] buffer, Color32 c, float alpha = 1)
        {
            int tx = lx + OriginX, ty = ly + OriginY;
            if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) return;
            if (alpha < 1 && alpha <= Bayer(tx, ty)) return;
            buffer[ty * Width + tx] = c;
        }
        static float Bayer(int x, int y) { int[] m = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 }; return (m[(y & 3) * 4 + (x & 3)] + .5f) / 16; }

        /// <summary>
        /// 실제 값을 관측하고 이번 화면을 Pixels(아래쪽 행부터, Texture2D.SetPixels32 순서)에 그린다.
        /// hitFlash는 기존 피격 흔들림과 같은 시계의 게이지 흰 번쩍임(0..1).
        /// </summary>
        public void Draw(float health, float maxHealth, float shield, float now, bool struck, float hitFlash)
        {
            Ink.Observe(health, maxHealth, shield, now, struck);
            Array.Copy(still, top, still.Length);

            // 바닥 게이지(마른 빨강): 값의 끝도 게이지 기울기를 따라 자른다
            float solid = Ink.SolidFraction * BarLength; bool white = hitFlash > .5f;
            for (int ty = 0; ty < Height; ty++) for (int tx = 0; tx < Width; tx++)
                    if (InBar(tx - OriginX, ty - OriginY, out float xs, out float v) && xs < solid)
                        top[ty * Width + tx] = white ? White : Ink.RedRow(Mathf.FloorToInt(v));

            // 연출 층: 게이지 칸의 왼쪽 위가 (BarX, BarY)에 오도록 겹친다
            Ink.Paint();
            int ox = OriginX + BarX - Ink.MarginX, oy = OriginY + BarY - Ink.MarginY;
            for (int y = 0; y < Ink.Height; y++) for (int x = 0; x < Ink.Width; x++)
                {
                    var c = Ink.At(x, y); if (c.a == 0) continue;
                    int tx = ox + x, ty = oy + y;
                    if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) continue;
                    // 판 밖으로 튄 점은 판 안쪽까지만 보이게 한다
                    if (still[ty * Width + tx].a == 0) continue;
                    top[ty * Width + tx] = c;
                }

            // 숫자: 현재 체력(큰 도트 2배), "/ 최대"(작은 도트 2배, 아래쪽을 현재 체력에 맞춤), 회복량 "+N"
            var numberColor = Color32.Lerp(Color32.Lerp(Cream, HealGreen, Ink.HealTint), DamageRed, Ink.DamageTint);
            string current = Mathf.CeilToInt(Mathf.Max(0, health)).ToString(), max = "/ " + Mathf.CeilToInt(maxHealth);
            Text(top, current, NumberX, NumberY, numberColor, PixelFont.Big, 2);
            int maxRight = Text(top, max, NumberX + 2 * MaxScale + current.Length * 12, NumberY + 14 - 5 * MaxScale, Muted, PixelFont.Mini, MaxScale);
            float g = now - Ink.GainAt;
            if (g >= 0 && g < .85f)
            {
                float rise = 1 - Mathf.Pow(1 - Mathf.Clamp01(g / .6f), 3), a = 1 - Mathf.Clamp01((g - .5f) / .35f);
                Text(top, "+" + Ink.GainAmount, maxRight + 6, NumberY + 2 - Mathf.RoundToInt(6 * rise), HealGreen, PixelFont.Big, 1, Edge, a);
            }

            for (int ty = 0; ty < Height; ty++) Array.Copy(top, ty * Width, Pixels, (Height - 1 - ty) * Width, Width);
        }

        /// <summary>도트 글자를 지역 좌표 (x, y)(글자 위쪽 왼끝)에 쓰고 오른끝 지역 x를 돌려준다.</summary>
        static int Text(Color32[] buffer, string text, int x, int y, Color32 c, PixelFont.Face font, int scale, Color32? outline = null, float alpha = 1)
        {
            int left = x;
            for (int pass = outline.HasValue ? 0 : 1; pass < 2; pass++)
            {
                left = x;
                foreach (char ch in text)
                {
                    if (!font.Glyphs.TryGetValue(ch, out var g)) { left += font.Space * scale + font.Gap * scale; continue; }
                    for (int r = 0; r < g.Length; r++) for (int col = 0; col < g[r].Length; col++)
                        {
                            if (g[r][col] != '1') continue;
                            for (int sy = 0; sy < scale; sy++) for (int sx = 0; sx < scale; sx++)
                                {
                                    int px = left + col * scale + sx, py = y + r * scale + sy;
                                    if (pass == 0) { for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++) Local(px + ox, py + oy, buffer, outline.Value, alpha); }
                                    else Local(px, py, buffer, c, alpha);
                                }
                        }
                    left += (g[0].Length + font.Gap) * scale;
                }
            }
            return left;
        }
        /// <summary>시험용: 위에서 아래 순서 좌표의 화소.</summary>
        public Color32 At(int tx, int ty) => Pixels[(Height - 1 - ty) * Width + tx];
        /// <summary>시험용: 지역 좌표(시안 원점 기준)의 텍스처 위치.</summary>
        public static Vector2Int ToTexture(int lx, int ly) => new Vector2Int(lx + OriginX, ly + OriginY);
        /// <summary>시험용: 게이지 가운데 줄에서 체력 hp(최대 100 기준)가 놓이는 텍스처 위치.</summary>
        public static Vector2Int BarPoint(float hp, float total = 100)
        {
            float v = BarH / 2 + .5f; return new Vector2Int(Mathf.FloorToInt(OriginX + BarX + Lean * (1 - v / BarH) + hp / total * BarLength), OriginY + BarY + BarH / 2);
        }
    }

    /// <summary>pixelfx의 "big"(5x7)·"mini"(3x5) 도트 글꼴 중 체력판에 쓰는 글자.</summary>
    public static class PixelFont
    {
        public sealed class Face { public int Gap, Space; public System.Collections.Generic.Dictionary<char, string[]> Glyphs; }
        public static readonly Face Big = new Face
        {
            Gap = 1, Space = 3, Glyphs = new System.Collections.Generic.Dictionary<char, string[]>
            {
                ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
                ['1'] = new[] { "010", "110", "010", "010", "010", "010", "111" },
                ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
                ['3'] = new[] { "11111", "00010", "00100", "00010", "00001", "10001", "01110" },
                ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
                ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
                ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
                ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
                ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
                ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" },
                ['/'] = new[] { "00001", "00001", "00010", "00100", "01000", "10000", "10000" },
                ['+'] = new[] { "00000", "00100", "00100", "11111", "00100", "00100", "00000" },
            }
        };
        public static readonly Face Mini = new Face
        {
            Gap = 1, Space = 2, Glyphs = new System.Collections.Generic.Dictionary<char, string[]>
            {
                ['0'] = new[] { "111", "101", "101", "101", "111" },
                ['1'] = new[] { "010", "110", "010", "010", "111" },
                ['2'] = new[] { "110", "001", "010", "100", "111" },
                ['3'] = new[] { "110", "001", "010", "001", "110" },
                ['4'] = new[] { "101", "101", "111", "001", "001" },
                ['5'] = new[] { "111", "100", "110", "001", "110" },
                ['6'] = new[] { "011", "100", "111", "101", "111" },
                ['7'] = new[] { "111", "001", "010", "010", "010" },
                ['8'] = new[] { "111", "101", "111", "101", "111" },
                ['9'] = new[] { "111", "101", "111", "001", "110" },
                ['/'] = new[] { "001", "001", "010", "100", "100" },
                ['+'] = new[] { "000", "010", "111", "010", "000" },
            }
        };
    }
}
