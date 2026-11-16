using System;
using System.Collections.Generic;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace ForestCombatV2
{
    // A pixel keycap showing which key to press, bobbing up and down above a world target (1 s period, 4 world px,
    // snapped to whole world pixels so it never blurs). Drawn at twice the world pixel size so the letter reads.
    public sealed class KeycapMarker : MonoBehaviour
    {
        public const int Ppu = 32, Scale = 2;
        public const float BobPixels = 4f;
        const float BobSeconds = 1f;
        static readonly Dictionary<char, Sprite> cache = new Dictionary<char, Sprite>();

        SpriteRenderer body;
        LocalInput input;
        GameAction action;
        GameObject bindingLabel;
        Text bindingText;
        bool visible = true, showingLabel;
        string currentLabel;
        Vector3 anchor;
        public char Key { get; private set; }
        public float Offset { get; private set; }
        public bool Visible => visible && body;

        public static KeycapMarker Create(Transform parent, char key, Vector2 position, Material material, int sortingOrder, LocalInput input = null, GameAction action = GameAction.Interact)
        {
            var go = new GameObject("Keycap " + key); go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(Scale, Scale, 1);
            var marker = go.AddComponent<KeycapMarker>();
            marker.Key = key; marker.body = go.AddComponent<SpriteRenderer>(); marker.input = input; marker.action = action;
            marker.body.sprite = SpriteFor(key); marker.body.sortingOrder = sortingOrder;
            if (material) marker.body.sharedMaterial = material;
            marker.MoveTo(position);
            return marker;
        }

        public void MoveTo(Vector2 position) { anchor = position; transform.position = anchor; }
        public void SetVisible(bool value)
        { visible = value; if (body) body.enabled = value && !showingLabel; if (bindingLabel) bindingLabel.SetActive(value && showingLabel); }

        void RefreshBinding()
        {
            if (!input) return;
            string label = input.Label(action);
            if (label == currentLabel) return;
            currentLabel = label; showingLabel = label != Key.ToString();
            if (showingLabel)
            {
                if (!bindingLabel)
                {
                    bindingLabel = new GameObject("Current binding", typeof(RectTransform), typeof(Canvas), typeof(Image));
                    bindingLabel.transform.SetParent(transform, false); bindingLabel.transform.localScale = Vector3.one / Ppu;
                    var canvas = bindingLabel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                    canvas.sortingOrder = body.sortingOrder; canvas.sortingLayerID = body.sortingLayerID;
                    bindingLabel.GetComponent<Image>().color = new Color32(255, 255, 255, 255);
                    var outline = bindingLabel.AddComponent<Outline>(); outline.effectColor = new Color32(40, 35, 26, 255); outline.effectDistance = Vector2.one;
                    bindingText = new GameObject("Key", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); bindingText.transform.SetParent(bindingLabel.transform, false);
                    bindingText.font = Resources.Load<Font>("PixelInventory/Fonts/Galmuri11") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    bindingText.fontSize = 12; bindingText.alignment = TextAnchor.MiddleCenter; bindingText.color = new Color32(40, 35, 26, 255);
                    bindingText.horizontalOverflow = HorizontalWrapMode.Overflow; bindingText.raycastTarget = false;
                    bindingText.rectTransform.anchorMin = Vector2.zero; bindingText.rectTransform.anchorMax = Vector2.one;
                    bindingText.rectTransform.offsetMin = bindingText.rectTransform.offsetMax = Vector2.zero;
                }
                bindingText.text = label;
                var rect = (RectTransform)bindingLabel.transform; rect.pivot = new Vector2(.5f, 0);
                rect.sizeDelta = new Vector2(Mathf.Max(17, bindingText.preferredWidth + 10), 18);
            }
            SetVisible(visible);
        }

        void LateUpdate()
        {
            RefreshBinding();
            if (!Visible) return;
            // Real time: the marker keeps moving while the game slows for hit stop or death accents.
            Offset = Mathf.Round((Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / BobSeconds) * .5f + .5f) * BobPixels) / Ppu;
            transform.position = anchor + Vector3.up * Offset;
        }

        // Top of the drawn pixels (sprite canvases often have empty space above the art). Unreadable textures are
        // copied through a temporary render texture once.
        public static float OpaqueTop(SpriteRenderer renderer)
        {
            var sprite = renderer.sprite; var texture = sprite.texture; var rect = sprite.textureRect;
            var pixels = Pixels(texture);
            if (pixels == null) return renderer.bounds.max.y;
            for (int y = (int)rect.yMax - 1; y >= (int)rect.yMin; y--)
                for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                    if (pixels[y * texture.width + x].a > 32)
                        return renderer.transform.position.y + (y + 1 - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit * renderer.transform.lossyScale.y;
            return renderer.bounds.max.y;
        }

        static Color32[] Pixels(Texture2D texture)
        {
            if (texture.isReadable) return texture.GetPixels32();
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, rt); RenderTexture.active = rt;
                var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                var pixels = copy.GetPixels32(); Destroy(copy); return pixels;
            }
            catch (Exception) { return null; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
        }

        // Keyboard keycap art (Resources/InputGlyphs/Keyboard/keycap-<key>, 17x18 PPU32, bottom-centre). The bob stays in
        // code; the PNG is a still frame.
        static Sprite WhiteInteractKey()
        {
            const int w = 17, h = 18;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[w * h]; var ink = new Color32(63, 62, 60, 255);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                pixels[y * w + x] = x == 0 || x == w - 1 || y == 0 || y == h - 1 || y == 3 ? ink : y < 3 ? new Color32(184, 184, 181, 255) : new Color32(255, 255, 255, 255);
            for (int y = 7; y <= 13; y++) for (int x = 6; x <= 10; x++)
                if (x == 6 || y == 13 || y == 10 && x < 10) pixels[y * w + x] = ink;
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, 0), Ppu, 0, SpriteMeshType.FullRect);
            sprite.name = "White interaction key"; sprite.hideFlags = HideFlags.DontSave; return sprite;
        }

        public static Sprite SpriteFor(char key)
        {
            if (cache.TryGetValue(key, out var cached) && cached) return cached;
            var sprite = char.ToUpperInvariant(key) == 'F' ? WhiteInteractKey() : Resources.Load<Sprite>("InputGlyphs/Keyboard/keycap-" + char.ToLowerInvariant(key));
            if (!sprite) Debug.LogWarning("Keycap art is missing for " + key + " (tools/assets/update_assets.py).");
            return cache[key] = sprite;
        }
    }
}
