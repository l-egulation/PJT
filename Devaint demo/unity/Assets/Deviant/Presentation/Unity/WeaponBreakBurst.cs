using System;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    /// <summary>
    /// 무기가 내구도로 부서질 때 캐릭터 손 자리에서 터지는 도트 연출(사용자 결정 2026-11-13, 시안 「파괴 연출 3차」).
    /// 무기 그림은 건드리지 않고(무기마다 따로 만들지 않는다), 손 자리에서 큰 하얀 원과 십자 빛이 번쩍이고 파티클 44개가 튄다.
    /// 게임 화소(32 = 1유닛)로 한 장의 텍스처에 칸마다 다시 그린다. 한 칸 60ms, 14칸.
    /// </summary>
    public sealed class WeaponBreakBurst : MonoBehaviour
    {
        public const int PixelsPerUnit = 32, Frames = 14, Width = 260, Height = 240, AnchorX = 130, AnchorY = 120;
        public const float FrameSeconds = .06f;
        static readonly int[] FlashRadius = { 16, 24, 30, 34 };
        static readonly Color32 White = new Color32(255, 255, 255, 255), Cream = new Color32(239, 230, 212, 255), Ink = new Color32(20, 18, 16, 255);
        struct Particle { public float vx, vy; public int size; public Color32 colour; }

        Particle[] particles;
        Texture2D texture; SpriteRenderer view; Color32[] top, pixels;
        float born; int drawn = -1;
        public int Frame => Mathf.FloorToInt((Time.unscaledTime - born) / FrameSeconds);
        public bool Finished => Frame >= Frames;

        /// <summary>손 자리(world)에 연출을 띄운다. 정렬은 캐릭터 그림 위.</summary>
        public static WeaponBreakBurst Play(Vector2 hand, int sortingLayerId, int sortingOrder, int seed = 11)
        {
            var go = new GameObject("Weapon break burst");
            // 게임 화소 격자에 맞춘다.
            go.transform.position = new Vector3(Mathf.Round(hand.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(hand.y * PixelsPerUnit) / PixelsPerUnit, 0);
            var burst = go.AddComponent<WeaponBreakBurst>();
            burst.Begin(seed, sortingLayerId, sortingOrder);
            return burst;
        }

        void Begin(int seed, int sortingLayerId, int sortingOrder)
        {
            var random = new System.Random(seed); float R() => (float)random.NextDouble();
            Color32[] colours = { White, White, White, White, Cream, Cream, Ink, Ink };
            int[] sizes = { 1, 1, 1, 2, 2, 3 };
            particles = new Particle[44];
            for (int i = 0; i < particles.Length; i++)
            {
                float angle = R() * Mathf.PI * 2, speed = 3 + R() * 5.5f;
                particles[i] = new Particle { vx = Mathf.Cos(angle) * speed, vy = Mathf.Sin(angle) * speed - 2, size = sizes[random.Next(sizes.Length)], colour = colours[random.Next(colours.Length)] };
            }
            top = new Color32[Width * Height]; pixels = new Color32[Width * Height];
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = "Weapon break burst", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            view = gameObject.AddComponent<SpriteRenderer>();
            view.sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(AnchorX / (float)Width, 1 - AnchorY / (float)Height), PixelsPerUnit);
            view.sortingLayerID = sortingLayerId; view.sortingOrder = sortingOrder;
            born = Time.unscaledTime; Redraw(0);
        }

        void Update()
        {
            int frame = Frame;
            if (frame >= Frames) { Destroy(gameObject); return; }
            if (frame != drawn) Redraw(frame);
        }
        void OnDestroy() { if (texture) Destroy(texture); }

        /// <summary>f 번째 칸(위에서 아래로 쌓은 버퍼, 손 자리가 AnchorX·AnchorY).</summary>
        public static void DrawFrame(int f, Color32[] buffer, Func<int, (float vx, float vy, int size, Color32 colour)> particle, int count)
        {
            Array.Clear(buffer, 0, buffer.Length);
            void Put(int x, int y, Color32 c) { x += AnchorX; y += AnchorY; if (x >= 0 && y >= 0 && x < Width && y < Height) buffer[y * Width + x] = c; }
            if (f < 4)
            {
                // 처음 두 칸은 꽉 찬 하얀 원, 그다음은 퍼지며 엷어지는 고리
                int r = FlashRadius[f]; byte alpha = (byte)(f < 2 ? 255 : 200 - f * 30);
                for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    if (d <= r && (f < 2 || d >= r - (5 - f))) Put(x, y, new Color32(255, 255, 255, alpha));
                }
            }
            if (f < 3)
            {
                int length = 30 - f * 8;   // 긴 십자 빛(두께 2)
                for (int i = -length; i <= length; i++) for (int k = 0; k < 2; k++) { Put(i, k, White); Put(k, i, White); }
            }
            float t = f + 1, fade = Mathf.Max(0, 1 - f / 13f);
            for (int i = 0; i < count; i++)
            {
                var p = particle(i);
                int px = Mathf.FloorToInt(p.vx * t), py = Mathf.FloorToInt(p.vy * t + .45f * t * t);
                var c = p.colour; c.a = (byte)Mathf.RoundToInt(255 * fade);
                for (int y = 0; y < p.size; y++) for (int x = 0; x < p.size; x++) Put(px + x, py + y, c);
            }
        }
        void Redraw(int frame)
        {
            drawn = frame;
            DrawFrame(frame, top, i => (particles[i].vx, particles[i].vy, particles[i].size, particles[i].colour), particles.Length);
            HudPixels.Flip(top, pixels, Width, Height);
            texture.SetPixels32(pixels); texture.Apply(false);
        }
    }
}
