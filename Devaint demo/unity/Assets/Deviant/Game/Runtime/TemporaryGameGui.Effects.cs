using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // One-shot screen effects over GUI items (bag ink destruction, altar candidate acquisition). Played on real time so
    // they run while the screen pauses the world; they only present a result that the model has already committed.
    public sealed partial class TemporaryGameGui
    {
        readonly List<(Image image, SpriteFlipbook clip)> uiEffects = new List<(Image, SpriteFlipbook)>();
        public int ActiveUiEffects => uiEffects.Count;

        // Centred on the given item at native 2x size (64 px frames drawn 64 px on the 960x540 stage).
        // Returns the playing clip so a screen can wait for it, or null when nothing plays.
        SpriteFlipbook PlayUiEffect(string folder, RectTransform over)
        {
            var clip = new SpriteFlipbook(folder);
            if (!clip.Ready || !over) return null;
            var go = new GameObject("Effect " + folder, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(stage, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(64, 64);
            rect.position = over.TransformPoint(over.rect.center);
            var image = go.GetComponent<Image>(); image.raycastTarget = false; image.type = Image.Type.Simple; image.preserveAspect = true;
            clip.Play(); image.sprite = clip.Current;
            uiEffects.Add((image, clip));
            return clip;
        }

        void TickUiEffects()
        {
            for (int i = uiEffects.Count - 1; i >= 0; i--)
            {
                var (image, clip) = uiEffects[i];
                if (!image || !clip.Playing) { if (image) Destroy(image.gameObject); uiEffects.RemoveAt(i); continue; }
                image.sprite = clip.Current; image.transform.SetAsLastSibling();
            }
        }
    }
}
