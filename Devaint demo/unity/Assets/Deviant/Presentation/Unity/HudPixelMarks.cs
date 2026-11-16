using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    // 체력판(HealthPlaque)과 같은 960×540 게임 화소 도트 표식. 먹 0b0b0a, 크림 efe6d4, 빈 칸 0d0d0c/1a1917.
    public static class HudPixels
    {
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        public static readonly Color32 Ink = Hex(0x0b0b0a), Cream = Hex(0xefe6d4), Track = Hex(0x0d0d0c), TrackHi = Hex(0x1a1917);
        public static readonly Color32 Plate = Hex(0x151513), Well = Hex(0x2a2929), Paper = Hex(0xfdf0df);
        public static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        // 위에서 아래로 쌓은 버퍼를 Unity 텍스처 순서(아래에서 위)로 옮긴다.
        public static void Flip(Color32[] top, Color32[] pixels, int width, int height)
        {
            for (int y = 0; y < height; y++) Array.Copy(top, y * width, pixels, (height - 1 - y) * width, width);
        }
        public static Color32[] Flipped(Color32[] top, int width, int height)
        {
            var pixels = new Color32[top.Length]; Flip(top, pixels, width, height); return pixels;
        }
    }

    /// <summary>
    /// 전투 HUD 부품의 도트판. 모두 위에서 아래로 쌓은 960×540 게임 화소 버퍼를 돌려준다.
    /// 승인 그림의 구조(무기 원·미니맵: 먹 2·크림 1·어두운 속, 키캡·버튼·팔레트 판: 먹 테 + 크림 속)를 그대로 화소로 옮긴다.
    /// 원은 재기 규칙(계단 길이가 45°로 갈수록 줄기만)으로 그린다.
    /// </summary>
    public static class HudPixelKit
    {
        // ---- 재기 규칙 원(로딩 창 원 LoadingWindowCircle 과 같은 방법, 표준 픽셀 원 표와 같은 모양) ----
        // 화소 중심 판정만 쓰면 계단 길이가 6 4 3 3 2 3 2 2 1 2 …처럼 줄었다 늘어 울퉁불퉁하다. 12시~45°의 8분의 1에서
        // 계단 높이는 두고 계단 길이만 긴 순서로 다시 세워 45°로 갈수록 줄기만 하게 하고, 나머지 7개는 거울로 옮긴다.
        /// <summary>size×size(짝수) 가운데 반지름 radius 원의 안쪽 칸(행 우선).</summary>
        public static bool[] CircleMask(int size, float radius)
        {
            int half = size / 2; var heights = new int[half]; var plain = new int[half];
            for (int x = 0; x < half; x++) for (int y = 0; y < half; y++)
                if ((x + .5f) * (x + .5f) + (y + .5f) * (y + .5f) <= radius * radius) plain[x]++;
            var steps = new List<int>(); var lengths = new List<int>();
            for (int x = 0; x < half && plain[x] > x; x++)
            {
                if (steps.Count > 0 && steps[steps.Count - 1] == plain[x]) lengths[lengths.Count - 1]++;
                else { steps.Add(plain[x]); lengths.Add(1); }
            }
            lengths.Sort((p, q) => q.CompareTo(p));
            int column = 0;
            for (int i = 0; i < steps.Count; i++) for (int n = 0; n < lengths[i]; n++) heights[column++] = steps[i];
            var mask = new bool[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int qx = x >= half ? x - half : half - 1 - x, qy = y >= half ? y - half : half - 1 - y;
                mask[y * size + x] = qx <= qy ? qy < heights[qx] : qx < heights[qy];
            }
            return mask;
        }
        /// <summary>안쪽 칸이 바깥(위아래좌우)에서 몇 겹째인지(0부터, 밖은 -1). 겹의 안쪽 경계도 바깥 계단을 그대로 따른다.</summary>
        public static int[] Layers(bool[] mask, int size)
        {
            var layer = new int[size * size];
            for (int i = 0; i < layer.Length; i++) layer[i] = mask[i] ? int.MaxValue : -1;
            var frontier = new List<int>();
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (!mask[i]) continue;
                if (x == 0 || y == 0 || x == size - 1 || y == size - 1 || !mask[i - 1] || !mask[i + 1] || !mask[i - size] || !mask[i + size])
                { layer[i] = 0; frontier.Add(i); }
            }
            for (int depth = 1; frontier.Count > 0; depth++)
            {
                var next = new List<int>();
                foreach (int i in frontier)
                {
                    int x = i % size;
                    if (x > 0) Visit(i - 1); if (x < size - 1) Visit(i + 1); Visit(i - size); Visit(i + size);
                }
                frontier = next;
                void Visit(int j) { if (j >= 0 && j < layer.Length && layer[j] == int.MaxValue) { layer[j] = depth; next.Add(j); } }
            }
            return layer;
        }
        // 원 가장자리는 서브픽셀로 그린다(사용자 결정 2026-11-13). 칸마다 4×4 = 16점에서 진짜 원(중심 거리)으로 색을 보고,
        // 가장 많은 색에 두 번째 색을 걸친 비율만큼 25·50·75% 네 단계로만 섞는다(색이 늘어나는 것을 막는다). 투명과 섞이면
        // 불투명도로 섞는다. 띠는 반지름 두 개 사이라 어느 방향이든 같은 굵기다. 1화소 크림 선은 섞으면 점선처럼 끊어지므로
        // 재기 규칙 원(CircleMask)의 테두리로 또렷하게 덧그린다.
        public delegate Color32 PointColour(float x, float y);
        public static Color32[] Supersample(int size, PointColour at)
        {
            var top = new Color32[size * size]; var colours = new Color32[16]; var counts = new int[16];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int kinds = 0;
                for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++)
                {
                    var c = at(x + (i + .5f) / 4, y + (j + .5f) / 4); int k = 0;
                    while (k < kinds && !Same(colours[k], c)) k++;
                    if (k == kinds) { colours[kinds] = c; counts[kinds++] = 0; }
                    counts[k]++;
                }
                int a = 0, b = -1;
                for (int k = 1; k < kinds; k++) if (counts[k] > counts[a]) a = k;
                for (int k = 0; k < kinds; k++) if (k != a && (b < 0 || counts[k] > counts[b])) b = k;
                Color32 A = colours[a], result = A;
                if (b >= 0)
                {
                    Color32 B = colours[b]; float w = Mathf.Round(counts[b] / 16f * 4) / 4;
                    if (A.a == 0) result = w > 0 ? new Color32(B.r, B.g, B.b, (byte)Mathf.RoundToInt(B.a * w)) : HudPixels.Clear;
                    else if (B.a == 0) result = new Color32(A.r, A.g, A.b, (byte)Mathf.RoundToInt(A.a * (1 - w)));
                    else result = Color32.Lerp(A, B, w);
                }
                top[y * size + x] = result;
            }
            return top;
        }
        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        static float Radius(float x, float y, float centre) { float dx = x - centre, dy = y - centre; return Mathf.Sqrt(dx * dx + dy * dy); }

        /// <summary>무기·스킬 원: 먹 원판(가장자리 서브픽셀), 바깥에서 2화소 안쪽에 1화소 크림 선, 속은 먹(승인 원판과 같은 구조).</summary>
        public static Color32[] Disc(int radius)
        {
            int size = 2 * radius;
            var top = Supersample(size, (x, y) => Radius(x, y, radius) <= radius ? HudPixels.Ink : HudPixels.Clear);
            var line = Layers(CircleMask(size, radius - 2), size);
            for (int i = 0; i < top.Length; i++) if (line[i] == 0) top[i] = HudPixels.Cream;
            // 또렷한 선의 계단 모서리 옆 칸에만 크림을 걸친 만큼(25·50%, 최대 50%) 번지게 해 계단을 누그러뜨린다(사용자 요청 2026-11-13).
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (line[i] == 0 || top[i].a == 0) continue;
                int n = 0;
                for (int j = 0; j < 4; j++) for (int k = 0; k < 4; k++)
                {
                    float d = Radius(x + (k + .5f) / 4, y + (j + .5f) / 4, radius);
                    if (d > radius - 3 && d <= radius - 2) n++;
                }
                float w = Mathf.Min(Mathf.Round(n / 16f * 4) / 4, .5f);
                if (w > 0) { var c = Color32.Lerp(HudPixels.Ink, HudPixels.Cream, w); c.a = top[i].a; top[i] = c; }
            }
            return top;
        }
        /// <summary>size×size 가운데 반지름 outer 와 outer-thickness 사이의 띠(가장자리 서브픽셀).</summary>
        public static Color32[] Ring(int size, float outer, int thickness, Color32 colour)
        {
            float c = size / 2f, inner = outer - thickness;
            return Supersample(size, (x, y) => { float d = Radius(x, y, c); return d <= outer && d > inner ? colour : HudPixels.Clear; });
        }
        internal static float Angle(float x, float y, float centre, float start) =>
            Mathf.Repeat(Mathf.Repeat(Mathf.Atan2(y - centre, x - centre) * Mathf.Rad2Deg, 360) - start, 360);
        internal static float RadiusAt(float x, float y, float centre) => Radius(x, y, centre);

        /// <summary>모서리를 cut 화소 계단으로 깎은 판: 먹 edge 화소, 그 안 크림 line 화소(0이면 없음), 속 fill.</summary>
        public static Color32[] Plate(int w, int h, int cut, int edge, int line, Color32 fill)
        {
            var top = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int depth = Depth(x, y, w, h, cut);
                if (depth < 0) continue;
                top[y * w + x] = depth < edge ? HudPixels.Ink : depth < edge + line ? HudPixels.Cream : fill;
            }
            return top;
        }
        // 깎은 사각형 안쪽으로 몇 화소 들어왔는지(밖이면 -1). 모서리는 x+y 대각선으로 깎는다.
        static int Depth(int x, int y, int w, int h, int cut)
        {
            int dx = Math.Min(x, w - 1 - x), dy = Math.Min(y, h - 1 - y);
            int diagonal = dx + dy - cut;
            if (diagonal < 0) return -1;
            return Math.Min(Math.Min(dx, dy), diagonal);
        }

        public static Color32[] Keycap(int w, int h) => Plate(w, h, 2, 2, 0, HudPixels.Paper);
        // Clean corner-bracket frame. All strokes are whole native game pixels, without distressed marks.
        public static Color32[] Minimap(int w, int h)
        {
            var top = new Color32[w * h];
            var edge = HudPixels.Hex(0x77736a);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int dx = Math.Min(x, w - 1 - x), dy = Math.Min(y, h - 1 - y);
                var colour = dx == 0 || dy == 0 ? HudPixels.Ink : dx == 1 || dy == 1 ? edge : HudPixels.Well;
                // A one-pixel ink surround keeps the ivory brackets legible over either scene.
                bool cornerShadow = dx < 12 && dy < 12 && (dx < 5 || dy < 5);
                bool bracket = dx >= 1 && dy >= 1 && dx < 11 && dy < 11 && (dx < 4 || dy < 4);
                if (cornerShadow) colour = HudPixels.Ink;
                if (bracket) colour = HudPixels.Cream;
                top[y * w + x] = colour;
            }
            return top;
        }

        static readonly string[] BagGlyph = {
            "..................",
            "..................",
            "..................",
            "......######......",
            ".....##....##.....",
            ".....##....##.....",
            ".....##....##.....",
            "....##########....",
            "....##########....",
            "...############...",
            "...############...",
            "...############...",
            "..##############..",
            "..##############..",
            "..##############..",
            "..##############..",
            "..##############..",
            "..##############..",
            "...############...",
            "..................",
        };
        static readonly string[] PauseGlyph = {
            "####....####", "####....####", "####....####", "####....####", "####....####", "####....####", "####....####",
            "####....####", "####....####", "####....####", "####....####", "####....####", "####....####", "####....####", "####....####" };
        public enum ButtonGlyph { Bag, Pause }
        public static Color32[] Button(int size, ButtonGlyph glyph)
        {
            // Match the health plaque: dark ink plate, a single ivory inset contour and light glyph.
            var top = Plate(size, size, 4, 1, 1, HudPixels.Plate);
            var rows = glyph == ButtonGlyph.Bag ? BagGlyph : PauseGlyph;
            int ox = (size - rows[0].Length) / 2, oy = (size - rows.Length) / 2;
            // The approved plain trapezoid bag sits one native pixel above its centred position.
            if (glyph == ButtonGlyph.Bag)
            {
                oy--;
                // Four-neighbour ink outline, identical to the approved native-pixel preview.
                for (int y = 0; y < rows.Length; y++) for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == '#')
                        foreach (var step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                        {
                            int px = x + step.x, py = y + step.y;
                            if (px >= 0 && px < rows[0].Length && py >= 0 && py < rows.Length)
                                top[(oy + py) * size + ox + px] = HudPixels.Ink;
                        }
            }
            for (int y = 0; y < rows.Length; y++) for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] != '.') top[(oy + y) * size + ox + x] = rows[y][x] == 's' ? HudPixels.Hex(0xa29e92) : HudPixels.Cream;
            return top;
        }

        // 5×7 도트 글꼴(체력판 PixelFont.Big)의 숫자에 키 이름용 영문 대문자와 . % - 를 더한다.
        static readonly System.Collections.Generic.Dictionary<char, string[]> Letters = new System.Collections.Generic.Dictionary<char, string[]>
        {
            ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" }, ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
            ['C'] = new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" }, ['D'] = new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
            ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" }, ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
            ['G'] = new[] { "01110", "10001", "10000", "10111", "10001", "10001", "01111" }, ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
            ['I'] = new[] { "111", "010", "010", "010", "010", "010", "111" }, ['J'] = new[] { "00111", "00010", "00010", "00010", "00010", "10010", "01100" },
            ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" }, ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
            ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" }, ['N'] = new[] { "10001", "10001", "11001", "10101", "10011", "10001", "10001" },
            ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" }, ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
            ['Q'] = new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" }, ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
            ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" }, ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
            ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" }, ['V'] = new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
            ['W'] = new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" }, ['X'] = new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
            ['Y'] = new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" }, ['Z'] = new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" },
            ['.'] = new[] { "0", "0", "0", "0", "0", "0", "1" }, ['%'] = new[] { "11001", "11010", "00010", "00100", "01000", "01011", "10011" },
            ['-'] = new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" },
        };
        static string[] Glyph(char c) => PixelFont.Big.Glyphs.TryGetValue(c, out var g) ? g : Letters.TryGetValue(char.ToUpperInvariant(c), out g) ? g : null;
        public static string[] GlyphOf(char c) => Glyph(c);
        public static bool CanWrite(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            foreach (char c in text) if (c != ' ' && Glyph(c) == null) return false;
            return true;
        }
        /// <summary>도트 글자. outline 이 있으면 8방향 1화소 테를 두른다(테 포함 크기를 돌려준다).</summary>
        public static Color32[] Text(string text, int scale, Color32 colour, Color32? outline, out int width, out int height)
        {
            int pad = outline.HasValue ? 1 : 0, inner = 0;
            foreach (char c in text) { var g = Glyph(c); inner += (g == null ? 3 : g[0].Length) * scale + scale; }
            inner = Math.Max(0, inner - scale);
            width = Math.Max(1, inner + 2 * pad); height = 7 * scale + 2 * pad;
            var top = new Color32[width * height]; int w = width, h = height;
            void Stamp(int ox, int oy, Color32 col, bool onlyEmpty)
            {
                int left = pad + ox;
                foreach (char c in text)
                {
                    var g = Glyph(c);
                    if (g != null)
                        for (int y = 0; y < 7; y++) for (int x = 0; x < g[y].Length; x++)
                            if (g[y][x] == '1')
                                for (int sy = 0; sy < scale; sy++) for (int sx = 0; sx < scale; sx++)
                                {
                                    int px = left + x * scale + sx, py = pad + oy + y * scale + sy;
                                    if (px < 0 || py < 0 || px >= w || py >= h) continue;
                                    if (!onlyEmpty || top[py * w + px].a == 0) top[py * w + px] = col;
                                }
                    left += (g == null ? 3 : g[0].Length) * scale + scale;
                }
            }
            if (outline.HasValue)
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++) if (ox != 0 || oy != 0) Stamp(ox, oy, outline.Value, true);
            Stamp(0, 0, colour, false);
            return top;
        }

        // ---- 손으로 찍은 작은 그림: 직선은 같은 마디(1:1 빗변), 곡선은 마디가 한 방향으로만 변한다 ----
        static Color32[] Paint(string[] rows, System.Func<char, Color32?> colour)
        {
            int w = rows[0].Length, h = rows.Length; var top = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { var c = colour(rows[y][x]); if (c.HasValue) top[y * w + x] = c.Value; }
            return top;
        }
        /// <summary>동전(21×21): 1:1 빗변 팔각형, 위·왼쪽 밝은 테, 아래·오른쪽 어두운 테, 안쪽 팔각 홈.</summary>
        public static Color32[] Coin(out int size)
        {
            size = 21; var top = new Color32[size * size];
            Color32 rimHi = HudPixels.Hex(0xffe08a), gold = HudPixels.Hex(0xf7b731), rimLo = HudPixels.Hex(0xc47a10), well = HudPixels.Hex(0xd98a14), wellEdge = HudPixels.Hex(0x8a4a0c);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int d = OctagonDepth(x, y, size, size, 6);
                if (d < 0) continue;
                Color32 c = d == 0 ? HudPixels.Ink : d == 1 ? (x + y < size - 1 ? rimHi : rimLo) : gold;
                int ix = x - 5, iy = y - 5, inner = ix >= 0 && iy >= 0 && ix < 11 && iy < 11 ? OctagonDepth(ix, iy, 11, 11, 3) : -1;
                if (inner == 0) c = wellEdge; else if (inner > 0) c = well;
                top[y * size + x] = c;
            }
            return top;
        }
        // 팔각형(모서리 cut 의 1:1 빗변) 안쪽으로 몇 칸 들어왔는지, 밖이면 -1.
        static int OctagonDepth(int x, int y, int w, int h, int cut)
        {
            int dx = Math.Min(x, w - 1 - x), dy = Math.Min(y, h - 1 - y), diagonal = dx + dy - cut;
            return diagonal < 0 ? -1 : Math.Min(Math.Min(dx, dy), diagonal);
        }
        static readonly string[] PaletteRows = {
            "......#########.......",
            "....##ppppppppp##.....",
            "...#ppprrpppyyppp#....",
            "..#ppprrrrpyyyyppp#...",
            ".#pppprrrrpyyyypppp#..",
            ".#ppppprrpppyyppppp#..",
            "#pp##pppppppppppggpp#.",
            "#p#..#ppppppppppgggg#.",
            "#p#..#pppppppppggggp#.",
            "#pp##pppppppppppggpp#.",
            "#ppppppppppppppppppp#.",
            ".#ppppvvppppbbppppp#..",
            ".#pppvvvvppbbbbpppp#..",
            "..#pppvvvvpbbbbppp#...",
            "...#pppvvpppbbppp#....",
            "....##ppppppppp##.....",
            "......#########.......",
        };
        /// <summary>마법안료 팔레트(22×17): 나무 판에 엄지 구멍과 다섯 색 물감.</summary>
        public static Color32[] Pigment(out int width, out int height)
        {
            width = PaletteRows[0].Length; height = PaletteRows.Length;
            Color32 wood = HudPixels.Hex(0xf3d9a4), r = HudPixels.Hex(0xe0483c), yl = HudPixels.Hex(0xf2c230), g = HudPixels.Hex(0x4fb06a), b = HudPixels.Hex(0x3f7fd8), v = HudPixels.Hex(0x8a5ad0);
            return Paint(PaletteRows, c => c == '#' ? HudPixels.Ink : c == 'p' ? wood : c == 'r' ? r : c == 'y' ? yl : c == 'g' ? g : c == 'b' ? b : c == 'v' ? v : (Color32?)null);
        }
        // 팔레트 칸 얼룩(21×14, 사용자 선택 2026-11-13 「A3 붓 터치, 붓결 없이」): 잉크를 넣은 칸은 그 잉크 색으로 칠한 얼룩
        // (아래쪽 가장자리만 같은 색의 짙은 톤), 빈 칸은 같은 모양의 1화소 테두리(무색 칸은 먹, 강화 칸은 그 색의 짙은 톤).
        // 모양은 시안(04_테스트/전투 HUD 도트 편집기/ink_daub_layout.py)과 화소까지 같다.
        static readonly string[] BlobRows = {
            ".....................", "........####.........", "......#########......", "....############.....",
            "....##############...", "...################..", "..#################..", "..##################.",
            "...#################.", "...################..", "...################..", "....##########.##....",
            ".......####..........", ".....................",
        };
        public const int BlobWidth = 21, BlobHeight = 14, BlobStep = 22;
        static readonly System.Collections.Generic.Dictionary<string, Color32> InkColours = new System.Collections.Generic.Dictionary<string, Color32>
        {
            ["red"] = HudPixels.Hex(0xe0483c), ["yellow"] = HudPixels.Hex(0xf2c230), ["green"] = HudPixels.Hex(0x4fb06a),
            ["blue"] = HudPixels.Hex(0x3f7fd8), ["purple"] = HudPixels.Hex(0x8a5ad0),
        };
        static bool InBlob(int x, int y) => x >= 0 && y >= 0 && x < BlobWidth && y < BlobHeight && BlobRows[y][x] == '#';
        static bool BlobEdge(int x, int y) => InBlob(x, y) && (!InBlob(x + 1, y) || !InBlob(x - 1, y) || !InBlob(x, y + 1) || !InBlob(x, y - 1));
        static Color32 Scale(Color32 c, float k) => new Color32((byte)Mathf.RoundToInt(c.r * k), (byte)Mathf.RoundToInt(c.g * k), (byte)Mathf.RoundToInt(c.b * k), 255);
        /// <summary>잉크를 넣은 칸: 잉크 색 얼룩.</summary>
        public static Color32[] InkBlob(string colour)
        {
            var fill = InkColours.TryGetValue(colour ?? "", out var c) ? c : HudPixels.Hex(0xe2d8c6); var dark = Scale(fill, .74f);
            int top = BlobRows.Length, bottom = 0;
            for (int y = 0; y < BlobHeight; y++) if (BlobRows[y].Contains("#")) { top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
            float mid = (top + bottom) / 2f; var pixels = new Color32[BlobWidth * BlobHeight];
            for (int y = 0; y < BlobHeight; y++) for (int x = 0; x < BlobWidth; x++)
                if (InBlob(x, y)) pixels[y * BlobWidth + x] = BlobEdge(x, y) && y > mid ? dark : fill;
            return pixels;
        }
        /// <summary>빈 칸: 얼룩 모양 테두리. 무색(neutral)은 먹, 강화 칸은 그 색의 짙은 톤.</summary>
        public static Color32[] EmptyBlob(string colour)
        {
            var line = InkColours.TryGetValue(colour ?? "", out var c) ? Scale(c, .78f) : HudPixels.Ink; var pixels = new Color32[BlobWidth * BlobHeight];
            for (int y = 0; y < BlobHeight; y++) for (int x = 0; x < BlobWidth; x++) if (BlobEdge(x, y)) pixels[y * BlobWidth + x] = line;
            return pixels;
        }
        /// <summary>팔레트 판: 모서리 반지름 7 의 둥근 판(서브픽셀), 먹 2화소 테, 종이 속, 아래쪽 안에 1화소 그늘.</summary>
        public static Color32[] PaletteTray(int w, int h)
        {
            Color32 shade = HudPixels.Hex(0xe6d6bd);
            return Supersample2(w, h, (x, y) =>
            {
                float d = RoundRect(x, y, w, h, 7);
                if (d > 0) return HudPixels.Clear;
                if (d > -2) return HudPixels.Ink;
                return RoundRect(x, y - 1.6f, w, h, 7) > -2 ? shade : HudPixels.Paper;
            });
        }
        static float RoundRect(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2) - (w / 2 - r), qy = Mathf.Abs(y - h / 2) - (h / 2 - r);
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }
        /// <summary>Supersample 의 직사각형판(같은 4×4점·네 단계 섞기).</summary>
        public static Color32[] Supersample2(int w, int h, PointColour at)
        {
            int size = Math.Max(w, h); var square = Supersample(size, (x, y) => x < w && y < h ? at(x, y) : HudPixels.Clear);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++) Array.Copy(square, y * size, pixels, y * w, w);
            return pixels;
        }
    }

    /// <summary>
    /// 체력판 아래의 보상 게이지 줄: 체력바와 같은 방향으로 기운 금색 막대(크림 1화소 틀과 그 바깥 먹 1화소)와
    /// 받아 둔 보상 수(종이 표식 ×N), 받아 둔 보상이 있으면 그 옆에 여는 키(G) 키캡. 막대에는 숫자를 쓰지 않는다(docs/design/run/rewards.md).
    /// 보상을 하나 받아 두는 순간(사용자 결정 2026-11-13, 시안 「게이지 가득 연출」): 막대가 가득 찬 채 두 칸 하얗게 번쩍 →
    /// 밝은 빛줄기가 왼쪽에서 오른쪽으로 훑는다. 그동안 표식이 3화소 튀어 올랐다 내려오고 ×N 테와 키캡 테가 금색, 표식 둘레 반짝임 넷.
    /// 한 칸 60ms. 위쪽 3화소는 튀어오름·반짝임 자리다.
    /// </summary>
    public sealed class RewardGaugeStrip
    {
        public const int Width = 250, Height = 17, TopMargin = 3;
        public const int BarLength = 181, BarHeight = 5;
        public const float FrameSeconds = .06f; public const int FlashFrames = 16;
        const int BarX = 2, BarY = 3 + TopMargin, TokenX = 195, TokenY = 1 + TopMargin;
        static readonly Color32 GoldHi = HudPixels.Hex(0xffdc78), Gold = HudPixels.Hex(0xf7c34c), GoldLo = HudPixels.Hex(0xc48a2a), White = new Color32(255, 255, 255, 255);
        static readonly string[] Sheet = { "#######.", "#ccccc#.", "#c###c##", "#cccccc#", "#c###cc#", "#cccccc#", "#cgggcc#", "#cgggcc#", "########" };
        static readonly string[] Times = { "00000", "00000", "10001", "01010", "00100", "01010", "10001" };
        static readonly int[] Pop = { 0, 2, 3, 2, 1, 0 };

        public Color32[] Pixels { get; } = new Color32[Width * Height];
        readonly Color32[] top = new Color32[Width * Height];
        int drawnFill = -1, drawnBanked = -1, drawnFrame = -2, seenBanked = -1; string drawnKey;
        float flashAt = float.NegativeInfinity;
        public int Filled { get; private set; }
        /// <summary>보상을 하나 받아 둔 순간부터 지난 칸 수(연출 중이 아니면 -1).</summary>
        public int FlashFrame { get; private set; } = -1;
        /// <summary>막 받아 둔 순간(연출 시작, unscaled 시각).</summary>
        public float FlashStartedAt => flashAt;

        /// <returns>그림이 바뀌었으면 true.</returns>
        public bool Draw(float fraction, int banked, float now, string openKey = "G")
        {
            banked = Mathf.Clamp(banked, 0, 99);
            if (seenBanked >= 0 && banked > seenBanked) flashAt = now;
            seenBanked = banked;
            int f = now - flashAt < FlashFrames * FrameSeconds ? Mathf.FloorToInt((now - flashAt) / FrameSeconds) : -1;
            FlashFrame = f;
            Filled = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(fraction) * BarLength), 0, BarLength);
            int fill = f >= 0 && f < 10 ? BarLength : Filled;
            if (fill == drawnFill && banked == drawnBanked && f == drawnFrame && openKey == drawnKey) return false;
            drawnFill = fill; drawnBanked = banked; drawnFrame = f; drawnKey = openKey;
            bool white = f >= 0 && f < 2; int sweep = f >= 2 && f < 10 ? -6 + (f - 2) * 26 : int.MinValue;
            bool lit = f >= 1 && f < 8; int pop = f >= 0 && f < Pop.Length ? Pop[f] : 0;
            Array.Clear(top, 0, top.Length);
            for (int j = -2; j < BarHeight + 2; j++)
            {
                int off = Offset(j);
                for (int i = -2; i < BarLength + 2; i++)
                {
                    bool edge = j == -2 || j == BarHeight + 1 || i == -2 || i == BarLength + 1;
                    bool frame = j == -1 || j == BarHeight || i == -1 || i == BarLength;
                    Color32 c;
                    if (edge) c = HudPixels.Ink;
                    else if (frame) c = HudPixels.Cream;
                    else if (i < fill)
                    {
                        c = j == 0 ? GoldHi : j == BarHeight - 1 ? GoldLo : Gold;
                        if (white) c = White;
                        else if (sweep != int.MinValue) { int k = i - sweep + j; if (k >= 0 && k < 7) c = k % 3 == 0 ? White : GoldHi; }
                    }
                    else c = j == 0 ? HudPixels.TrackHi : HudPixels.Track;
                    Put(BarX + off + i, BarY + j, c);
                }
            }
            if (banked > 0)
            {
                for (int y = 0; y < Sheet.Length; y++) for (int x = 0; x < Sheet[y].Length; x++)
                {
                    char ch = Sheet[y][x];
                    if (ch != '.') Put(TokenX + x, TokenY + y - pop, ch == '#' ? HudPixels.Ink : ch == 'g' ? Gold : HudPixels.Cream);
                }
                var ring = lit ? Gold : HudPixels.Cream;
                int left = TokenX + 10;
                left = Glyph(Times, left, TokenY + 1 - pop, ring);
                foreach (char d in banked.ToString()) left = Glyph(PixelFont.Big.Glyphs[d], left, TokenY + 1 - pop, ring);
                if (!string.IsNullOrEmpty(openKey)) Keycap(left + 3, TokenY - 2, openKey, lit);
                if (f >= 1 && f <= 5)
                {
                    int s = f - 1; int[,] at = { { -4, -4 }, { 10, -6 }, { 6, 11 }, { 19, 2 } };
                    for (int k = 0; k < 4; k++)
                    {
                        int r = 3 - Math.Abs(s - k % 2 - 1);
                        for (int d = -r; d <= r && r > 0; d++) { Put(TokenX + at[k, 0] + d, TokenY + at[k, 1], White); Put(TokenX + at[k, 0], TokenY + at[k, 1] + d, White); }
                    }
                }
            }
            HudPixels.Flip(top, Pixels, Width, Height);
            return true;
        }
        // 13×13 키캡: 먹 테(막 받아 둔 순간은 금색), 종이 속, 1배 도트 글자.
        void Keycap(int x, int y, string key, bool lit)
        {
            var edge = lit ? Gold : HudPixels.Ink;
            for (int j = 0; j < 13; j++) for (int i = 0; i < 13; i++)
            {
                if ((i == 0 || i == 12) && (j == 0 || j == 12)) continue;
                bool rim = i == 0 || i == 12 || j == 0 || j == 12 || ((i == 1 || i == 11) && (j == 1 || j == 11));
                Put(x + i, y + j, rim ? edge : HudPixels.Paper);
            }
            char ch = string.IsNullOrEmpty(key) ? 'G' : char.ToUpperInvariant(key[0]);
            var glyph = HudPixelKit.GlyphOf(ch);
            if (glyph == null) return;
            int gx = x + (13 - glyph[0].Length) / 2, gy = y + 3;
            for (int r = 0; r < glyph.Length; r++) for (int c = 0; c < glyph[r].Length; c++) if (glyph[r][c] == '1') Put(gx + c, gy + r, HudPixels.Ink);
        }
        // 위 줄일수록 오른쪽으로 민다(체력바와 같은 방향의 기울기). 양 끝 마디는 위에서부터 4·2·2·1줄
        // (사용자가 편집기에서 오른쪽 끝을 고친 모양, 2026-11-13). 틀 9줄(j = -2..6)마다 민 칸 수.
        static readonly int[] RowShift = { 3, 3, 3, 3, 2, 2, 1, 1, 0 };
        static int Offset(int row) => RowShift[Mathf.Clamp(row + 2, 0, RowShift.Length - 1)];
        int Glyph(string[] rows, int x, int y, Color32 ring)
        {
            // 크림 바탕(하늘·땅) 위라 먹 글자에 테를 둘러 어느 배경에서도 읽히게 한다(막 받아 둔 순간은 금색 테).
            for (int r = 0; r < rows.Length; r++) for (int c = 0; c < rows[r].Length; c++)
                if (rows[r][c] == '1')
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        if (At(x + c + dx, y + r + dy).a == 0) Put(x + c + dx, y + r + dy, ring);
            for (int r = 0; r < rows.Length; r++) for (int c = 0; c < rows[r].Length; c++)
                if (rows[r][c] == '1') Put(x + c, y + r, HudPixels.Ink);
            return x + rows[0].Length + 1;
        }
        Color32 At(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? HudPixels.Ink : top[y * Width + x];
        void Put(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < Width && y < Height) top[y * Width + x] = c; }
    }

    /// <summary>
    /// 무기 원 바깥 테두리의 내구도 원호. 화면 기준 7시 반(팔레트 판 바로 위)에서 시계방향으로 12시 직전까지,
    /// 남은 비율만큼 아래쪽부터 칠한다. 25% 이하는 빨강. 먹 테 안쪽 3.6화소 폭이 색이다.
    /// </summary>
    public sealed class DurabilityArc
    {
        public const float StartDegrees = 132, EndDegrees = 268, LowFraction = .25f;
        static readonly Color32 Hi = HudPixels.Hex(0x9fe0bb), Mid = HudPixels.Hex(0x5ab189);
        static readonly Color32 LowHi = HudPixels.Hex(0xff8a7a), LowMid = HudPixels.Hex(0xe0483c);
        public int Radius { get; }
        public int Size { get; }
        public Color32[] Pixels { get; }
        readonly Color32[] top;
        int drawnStep = -1; bool drawnLow;
        const int Steps = 240;

        public DurabilityArc(int discRadius)
        {
            Radius = discRadius; Size = 2 * (discRadius + 8);
            Pixels = new Color32[Size * Size]; top = new Color32[Size * Size];
        }
        public bool Draw(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            int step = Mathf.CeilToInt(fraction * Steps); bool low = fraction <= LowFraction;
            if (step == drawnStep && low == drawnLow) return false;
            drawnStep = step; drawnLow = low;
            // 반지름으로 나눈 띠(서브픽셀): 바깥 먹(R+5~R+6), 색 3화소(R+2~R+5, 바깥 1화소는 밝은 색),
            // 안쪽 먹(R-1~R+2, 원판 뒤로 들어간다). 어느 방향이든 같은 굵기다. 먹 테는 양 끝을 2도씩 더 둘러 색 띠의 끝을 막는다.
            float span = EndDegrees - StartDegrees, filled = span * step / Steps, c = Size / 2f, r = Radius;
            Color32 hi = low ? LowHi : Hi, mid = low ? LowMid : Mid;
            var drawn = HudPixelKit.Supersample(Size, (x, y) =>
            {
                float d = HudPixelKit.RadiusAt(x, y, c), t = HudPixelKit.Angle(x, y, c, StartDegrees);
                if (d > r + 6 || d <= r - 1 || (t > span + 2 && t < 358)) return HudPixels.Clear;
                if (d > r + 2 && d <= r + 5 && t <= span)
                    return t <= filled ? (d > r + 4 ? hi : mid) : (d > r + 4 ? HudPixels.TrackHi : HudPixels.Track);
                return HudPixels.Ink;
            });
            Array.Copy(drawn, top, top.Length);
            HudPixels.Flip(top, Pixels, Size, Size);
            return true;
        }
    }
}
