using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class StartingVillageWorld
    {
        // Same composited stops as the approved title HTML (900px scene, gradient ends at y730).
        // Only the colour twin changes. The sketch, palette restoration, erasure and upper sky path keep their rules.
        static Color DaylightSkyColour(float fromTop)
        {
            var original = new Color(188 / 255f, 214 / 255f, 230 / 255f, 1);
            Color Over(byte r, byte g, byte b, float alpha) =>
                Color.Lerp(original, new Color(r / 255f, g / 255f, b / 255f, 1), alpha);
            var top = Over(215, 246, 255, .72f);
            var middle = Over(237, 251, 247, .80f);
            var horizon = Over(255, 252, 223, .88f);
            float t = Mathf.Clamp01(fromTop * 900 / 730);
            return t <= .58f ? Color.Lerp(top, middle, t / .58f)
                : Color.Lerp(middle, horizon, (t - .58f) / .42f);
        }

        Sprite MakeDaylightSky(Sprite original)
        {
            if (!original) return null;
            int width = Mathf.RoundToInt(original.rect.width), height = Mathf.RoundToInt(original.rect.height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Village daylight sky", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                Color32 colour = DaylightSkyColour(1 - y / (float)(height - 1));
                for (int x = 0; x < width; x++) pixels[y * width + x] = colour;
            }
            texture.SetPixels32(pixels); texture.Apply(false);
            skyBackdropMade.Add(texture);
            var sprite = Make(texture, original.pivot / original.rect.size);
            sprite.name = "Village daylight sky";
            return sprite;
        }

        void InstallDaylightSky()
        {
            if (!sky || !sky.sprite) return;
            var bright = MakeDaylightSky(sky.sprite);
            // Override the ordinary loader, rather than treating the sky as a positioned ground column.
            // This also supplies the opening erasure and palette-blot colour twins.
            var piece = paired.Find(p => p.renderer == sky);
            if (piece != null) wrapped[piece.color + "@" + piece.pivot] = bright;
            sky.sprite = bright;
        }
    }
}
