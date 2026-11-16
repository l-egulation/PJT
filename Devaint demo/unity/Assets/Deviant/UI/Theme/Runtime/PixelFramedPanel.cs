using UnityEngine;
using UnityEngine.UI;

namespace Deviant.UI.Theme
{
    // Keeps the approved painted interior, with a separate, screen-pixel-aligned outline.
    // The large source's antialiased outline and exterior dust are never sampled.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelFramedPanel : MaskableGraphic
    {
        Sprite source;
        PixelFrameTexture interior;
        Vector2 screenOrigin, screenEnd;
        float screenScaleX = 1, screenScaleY = 1;
        int columns, rows, thickness;
        byte[] cells;
        float referenceScale = 1;
        Vector2 ReferenceSize => rectTransform.rect.size / referenceScale;
        static readonly Color32 Edge = new Color32(239, 235, 224, 255);
        static readonly Color32 Keyline = new Color32(43, 44, 42, 255);
        public Texture SourceTexture => source ? source.texture : Texture2D.whiteTexture;

        public void SetReferenceScale(float scale)
        {
            referenceScale = Mathf.Max(.01f, scale); cells = null; SetVerticesDirty();
            if (interior) interior.SetVerticesDirty();
        }

        public void Configure(Sprite textureSource)
        {
            source = textureSource;
            if (!interior) interior = GetComponentInChildren<PixelFrameTexture>(true);
            if (!interior)
            {
                var child = new GameObject("Original interior texture", typeof(RectTransform));
                var rect = child.GetComponent<RectTransform>(); rect.SetParent(transform, false);
                rect.pivot = rectTransform.pivot;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                interior = child.AddComponent<PixelFrameTexture>();
                interior.raycastTarget = false;
            }
            interior.Frame = this;
            cells = null; SetAllDirty(); interior.SetAllDirty();
        }
        protected override void OnEnable()
        {
            base.OnEnable(); Canvas.preWillRenderCanvases += RefreshGrid;
        }
        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RefreshGrid; base.OnDisable();
        }
        void RefreshGrid()
        {
            if (EnsureGrid()) { SetVerticesDirty(); if (interior) interior.SetVerticesDirty(); }
        }
        bool EnsureGrid()
        {
            var rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return false;
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var topLeft = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(new Vector3(rect.xMin, rect.yMax)));
            var bottomRight = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(new Vector3(rect.xMax, rect.yMin)));
            if (cells != null && topLeft == screenOrigin && bottomRight == screenEnd) return false;
            screenOrigin = topLeft; screenEnd = bottomRight;
            screenScaleX = Mathf.Max(.001f, (bottomRight.x - topLeft.x) / rect.width);
            screenScaleY = Mathf.Max(.001f, (topLeft.y - bottomRight.y) / rect.height);
            columns = Mathf.Max(1, Mathf.RoundToInt(bottomRight.x) - Mathf.RoundToInt(topLeft.x));
            rows = Mathf.Max(1, Mathf.RoundToInt(topLeft.y) - Mathf.RoundToInt(bottomRight.y));
            // Small dialogs use 1 reference pixel; larger panels use 2, independent of display resolution.
            var reference = ReferenceSize;
            int referenceThickness = reference.x * reference.y >= 320 * 320 ? 2 : 1;
            thickness = Mathf.Max(1, Mathf.RoundToInt(referenceThickness * referenceScale * Mathf.Min(screenScaleX, screenScaleY)));
            cells = new byte[columns * rows];
            int insetX = Mathf.RoundToInt(2 * columns / reference.x), insetY = Mathf.RoundToInt(2 * rows / reference.y);
            int right = columns - insetX, bottom = rows - insetY;
            bool Inside(int x, int y) => x >= insetX && x < right && y >= insetY && y < bottom;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                bool body = Inside(x, y);
                bool a = Inside(x - thickness, y), b = Inside(x + thickness, y);
                bool c = Inside(x, y - thickness), d = Inside(x, y + thickness);
                cells[y * columns + x] = body ? (byte)(a && b && c && d ? 1 : 2) : (byte)(a || b || c || d ? 3 : 0);
            }
            return true;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); EnsureGrid(); if (cells == null) return;
            Runs((kind, x0, x1, y) => { if (kind >= 2) Quad(mesh, x0, y, x1, y + 1, kind == 2 ? Edge : Keyline, false); });
        }
        public void PopulateInterior(VertexHelper mesh)
        {
            mesh.Clear(); EnsureGrid(); if (cells == null || !source) return;
            float left = columns * 12 / ReferenceSize.x, right = columns - left;
            Runs((kind, x0, x1, y) =>
            {
                if (kind != 1) return;
                // Clamp the old nine-slice's centre UVs into the corners. This retains the
                // existing central texture mapping without reintroducing the old white line.
                float start = x0;
                if (start < left && x1 > left) { Quad(mesh, start, y, left, y + 1, Color.white, true); start = left; }
                if (start < right && x1 > right) { Quad(mesh, start, y, right, y + 1, Color.white, true); start = right; }
                Quad(mesh, start, y, x1, y + 1, Color.white, true);
            });
        }
        void Runs(System.Action<byte, int, int, int> emit)
        {
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns;)
                {
                    int start = x; byte kind = cells[y * columns + x++];
                    while (x < columns && cells[y * columns + x] == kind) x++;
                    if (kind != 0) emit(kind, start, x, y);
                }
        }
        Vector2 Position(float x, float y)
        {
            var rect = rectTransform.rect;
            return new Vector2(rect.xMin + (Mathf.Round(screenOrigin.x) + x - screenOrigin.x) / screenScaleX,
                rect.yMax + (Mathf.Round(screenOrigin.y) - y - screenOrigin.y) / screenScaleY);
        }
        Vector2 UV(float x, float y)
        {
            var size = ReferenceSize;
            float u = Mathf.Clamp01((x * size.x / columns - 12) / Mathf.Max(1, size.x - 24));
            float v = Mathf.Clamp01((y * size.y / rows - 12) / Mathf.Max(1, size.y - 24));
            var texture = source.texture;
            return new Vector2(Mathf.Lerp(260f / texture.width, 1 - 260f / texture.width, u),
                Mathf.Lerp(1 - 260f / texture.height, 260f / texture.height, v));
        }
        void Quad(VertexHelper mesh, float x0, float y0, float x1, float y1, Color32 tint, bool textured)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(Position(x0, y0), tint, textured ? UV(x0, y0) : Vector2.zero);
            mesh.AddVert(Position(x1, y0), tint, textured ? UV(x1, y0) : Vector2.zero);
            mesh.AddVert(Position(x1, y1), tint, textured ? UV(x1, y1) : Vector2.zero);
            mesh.AddVert(Position(x0, y1), tint, textured ? UV(x0, y1) : Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
