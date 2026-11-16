using UnityEngine;

namespace Deviant.Combat
{
    // A small ring over the hero's head while it holds a wall (user request 2026-11-10): each wall gives a second of
    // stamina (data/combat/locomotion/player.json wallSlide.staminaDuration) and the hero drops off when it is spent. The
    // ring is a pie that empties clockwise from the top, yellow, red for its last third; it stays (empty) while the hero
    // falls from a spent wall and goes on landing.
    public sealed class WallStaminaRing : MonoBehaviour
    {
        // Over the hitbox top (2 units above the feet), in world units.
        public const float Above = .5f;
        CombatPlayer player;
        SpriteRenderer ring;
        public bool Showing => ring && ring.enabled;
        public float Left { get; private set; } = float.NaN;

        void LateUpdate()
        {
            if (!player) player = GetComponent<CombatPlayer>();
            var visual = player ? player.visual : null;
            Left = player ? player.WallStaminaLeft : float.NaN;
            bool show = visual && !float.IsNaN(Left) && !player.grounded && !player.Dead && visual.enabled && !visual.forceRenderingOff;
            if (!show) { if (ring) ring.enabled = false; return; }
            if (!ring)
            {
                ring = new GameObject("Wall stamina ring").AddComponent<SpriteRenderer>();
                ring.transform.SetParent(transform, false);
            }
            // A plain sprite material: the hero's own draws the hero's picture whatever the sprite.
            if (player.arena && player.arena.spriteMaterial) ring.sharedMaterial = player.arena.spriteMaterial;
            ring.sortingLayerID = visual.sortingLayerID; ring.sortingOrder = visual.sortingOrder + 2;
            ring.sprite = StaminaRing.For(Left); ring.enabled = true;
            var top = new Vector2(player.Center.x, player.Center.y + player.size.y / 2 + Above);
            ring.transform.position = new Vector3(Mathf.Round(top.x * 32) / 32, Mathf.Round(top.y * 32) / 32, 0);
        }
    }

    // The ring's pictures: 13 px across at 32 px a unit, an ink rim round a pale disc with the remaining stamina as a
    // pie from 12 o'clock clockwise, in 24 steps. Shared with the opening's demonstrations and the controls pictures.
    public static class StaminaRing
    {
        public const int Size = 13, Steps = 24;
        static Sprite[] sprites;

        public static Sprite For(float left)
        {
            sprites ??= Make();
            return sprites[Mathf.Clamp(Mathf.CeilToInt(left * Steps - .001f), 0, Steps)];
        }

        static Sprite[] Make()
        {
            var made = new Sprite[Steps + 1];
            Color32 ink = new Color32(0x1f, 0x1b, 0x19, 255), pale = new Color32(0xef, 0xe6, 0xcf, 255),
                full = new Color32(0xf2, 0xc1, 0x4e, 255), low = new Color32(0xd9, 0x4a, 0x38, 255);
            float c = Size / 2f;
            for (int s = 0; s <= Steps; s++)
            {
                float left = s / (float)Steps;
                var px = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = x + .5f - c, dy = y + .5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > c) continue;
                        if (d > c - 1.4f) { px[y * Size + x] = ink; continue; }
                        // Clockwise from the top: 0 at 12 o'clock.
                        float angle = Mathf.Repeat(Mathf.Atan2(dx, dy), Mathf.PI * 2) / (Mathf.PI * 2);
                        px[y * Size + x] = angle < left ? (left <= 1 / 3f ? low : full) : pale;
                    }
                var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                t.SetPixels32(px); t.Apply(false, true);
                made[s] = Sprite.Create(t, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
                made[s].hideFlags = HideFlags.DontSave;
            }
            return made;
        }
    }
}
