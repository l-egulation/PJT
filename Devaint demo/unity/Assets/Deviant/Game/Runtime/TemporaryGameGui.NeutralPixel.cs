using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        Image NeutralPixelPanel(Transform parent, float x, float y, float w, float h)
        {
            var root = Plain(parent, x, y, w, h, Color.clear, "White pixel panel");
            Plain(root.transform, 2, 2, w - 4, h - 4, Color.white, "Paper");
            var ink = new Color32(63, 62, 60, 255);
            Plain(root.transform, 4, 0, w - 8, 2, ink, "Top");
            Plain(root.transform, 4, h - 2, w - 8, 2, ink, "Bottom");
            Plain(root.transform, 0, 4, 2, h - 8, ink, "Left");
            Plain(root.transform, w - 2, 4, 2, h - 8, ink, "Right");
            foreach (var at in new[] { new Vector2(2, 2), new Vector2(w - 4, 2), new Vector2(2, h - 4), new Vector2(w - 4, h - 4) })
                Plain(root.transform, at.x, at.y, 2, 2, ink, "Corner");
            return root;
        }
    }
}
