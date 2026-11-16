using System;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    // Converts world anchors through the same rectangle as a presented camera texture.
    // Public screen coordinates retain Unity's bottom-left origin; IMGUI flips Y afterwards.
    [DisallowMultipleComponent]
    public sealed class CameraDisplayProjection : MonoBehaviour
    {
        public RenderTexture PresentedTexture;
        public Func<Rect> DisplayRect;

        static bool TryRect(Camera camera, out Rect rect)
        {
            var output = camera.GetComponent<CameraDisplayProjection>();
            if (output && output.isActiveAndEnabled && output.PresentedTexture &&
                camera.targetTexture == output.PresentedTexture && output.DisplayRect != null)
            {
                rect = output.DisplayRect();
                return rect.width > 0 && rect.height > 0;
            }
            rect = default;
            return false;
        }

        public static Vector3 WorldToScreenPoint(Camera camera, Vector3 world)
        {
            if (!TryRect(camera, out var rect)) return camera.WorldToScreenPoint(world);
            var point = camera.WorldToViewportPoint(world);
            return new Vector3(rect.x + point.x * rect.width,
                Screen.height - rect.yMax + point.y * rect.height, point.z);
        }

        public static bool TryScreenToWorldPoint(Camera camera, Vector3 screen, out Vector3 world)
        {
            if (!TryRect(camera, out var rect))
            {
                world = camera.ScreenToWorldPoint(screen);
                return true;
            }
            var gui = new Vector2(screen.x, Screen.height - screen.y);
            if (!rect.Contains(gui)) { world = default; return false; }
            world = camera.ViewportToWorldPoint(new Vector3((gui.x - rect.x) / rect.width,
                1 - (gui.y - rect.y) / rect.height, screen.z));
            return true;
        }
    }
}
