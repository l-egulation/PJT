using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using UnityEngine;

namespace ForestCombatV2
{
    // The palette lands, disappears, then shows the actual paid amount there. This view never grants currency.
    public sealed class Chapter1PigmentPickupEffect : MonoBehaviour
    {
        public const float DropSeconds = .2f, RestSeconds = .12f, LabelSeconds = .6f;
        public const float Duration = DropSeconds + RestSeconds + LabelSeconds;
        sealed class Burst { public SpriteRenderer body, label; public Vector2 origin; public float born; public bool labelShown; }
        readonly List<Burst> bursts = new List<Burst>();
        static readonly Dictionary<int, Sprite> amountSprites = new Dictionary<int, Sprite>();
        Sprite palette;
        CombatArena arena;
        Chapter1RunBinding binding;
        public int ActiveCount => bursts.Count;
        public int Shown { get; private set; }

        public void Initialize(Chapter1RunBinding owner)
        {
            binding = owner;
            Initialize(owner.World.arena);
            binding.PigmentAwarded += Show;
        }

        public void Initialize(CombatArena owner)
        {
            arena = owner;
            palette = GroundLootArt.Load(GroundLootArt.Pigment);
            if (!palette) Debug.LogWarning("The village palette pickup sprite is missing from LootPickups.");
        }

        void OnDestroy() { if (binding) binding.PigmentAwarded -= Show; }

        public void Show(Vector2 position, int amount)
        {
            if (!palette || !arena || amount <= 0) return;
            var go = new GameObject("PigmentPickup+" + amount);
            go.transform.SetParent(transform, false);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = palette;
            body.transform.localScale = Vector3.one;
            if (arena.spriteMaterial) body.sharedMaterial = arena.spriteMaterial;
            // Drawn on the actors' layer, above them.
            var actor = arena.Player ? arena.Player.visual : null;
            if (actor) { body.sortingLayerID = actor.sortingLayerID; body.sortingOrder = actor.sortingOrder + 20; }
            else body.sortingOrder = 60;
            var floor = arena.FindFloor(position + Vector2.up * .5f, 64);
            if (floor.HasValue) position.y = floor.Value;
            go.transform.position = position + Vector2.up * body.bounds.extents.y;
            var label = new GameObject("Pigment amount +" + amount).AddComponent<SpriteRenderer>();
            label.transform.SetParent(go.transform, false);
            label.sprite = AmountSprite(amount);
            label.transform.localScale = Vector3.one; // the numeric atlas keeps its own 32 PPU pixel size
            label.transform.localPosition = Vector3.zero;
            label.enabled = false;
            label.sharedMaterial = body.sharedMaterial;
            label.sortingLayerID = body.sortingLayerID; label.sortingOrder = body.sortingOrder + 1;
            var burst = new Burst { body = body, label = label, origin = go.transform.position, born = arena.Now };
            bursts.Add(burst); Shown++;
        }

        void Update()
        {
            if (!arena || arena.Controls.Current.SimulationPaused) return;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var burst = bursts[i];
                float elapsed = Mathf.Max(0, arena.Now - burst.born);
                if (!burst.body || elapsed >= Duration)
                { if (burst.body) Destroy(burst.body.gameObject); bursts.RemoveAt(i); }
                else if (elapsed < DropSeconds + RestSeconds)
                {
                    float t = Mathf.Clamp01(elapsed / DropSeconds);
                    burst.body.transform.position = Snap(burst.origin + Vector2.up * (4 * .42f * t * (1 - t)));
                }
                else
                {
                    if (!burst.labelShown)
                    {
                        burst.labelShown = true;
                        burst.body.enabled = false;
                        burst.body.transform.position = Snap(burst.origin);
                        burst.label.enabled = true;
                        PresentationSfx.PlayPigmentPickup();
                    }
                    float t = Mathf.Clamp01((elapsed - DropSeconds - RestSeconds) / LabelSeconds);
                    burst.label.transform.position = Snap(burst.origin + Vector2.up * (.5f * (1 - (1 - t) * (1 - t))));
                    var color = Color.white; color.a = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1, t));
                    burst.label.color = color;
                }
            }
        }

        static Vector2 Snap(Vector2 at) => new Vector2(Mathf.Round(at.x * 32), Mathf.Round(at.y * 32)) / 32;

        // Tiny 3x5 numeric glyphs at 2x integer scale; no font atlas filtering or extra art asset is needed.
        static Sprite AmountSprite(int amount)
        {
            if (amountSprites.TryGetValue(amount, out var sprite) && sprite) return sprite;
            string text = "+" + amount;
            string[] digits = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
                "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111" };
            int width = (text.Length * 4 - 1) * 2 + 2, height = 12;
            var mask = new bool[width * height];
            for (int c = 0; c < text.Length; c++)
            {
                string glyph = text[c] == '+' ? "000010111010000" : digits[text[c] - '0'];
                for (int y = 0; y < 5; y++) for (int x = 0; x < 3; x++) if (glyph[y * 3 + x] == '1')
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                        mask[(1 + (4 - y) * 2 + dy) * width + 1 + c * 8 + x * 2 + dx] = true;
            }
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                bool edge = false;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    if (x + dx >= 0 && x + dx < width && y + dy >= 0 && y + dy < height && mask[(y + dy) * width + x + dx]) edge = true;
                pixels[y * width + x] = mask[y * width + x] ? new Color32(255, 238, 181, 255) : edge ? new Color32(58, 47, 39, 255) : new Color32(0, 0, 0, 0);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, 32);
            sprite.name = text; amountSprites[amount] = sprite;
            return sprite;
        }
    }
}
