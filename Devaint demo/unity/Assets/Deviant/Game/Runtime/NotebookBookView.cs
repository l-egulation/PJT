using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // The approved HTML geometry and timing, rendered directly into the game's overlay canvas.
    public sealed class NotebookBookView : MonoBehaviour
    {
        public const float IntroEnd = 4.43f, FirstReveal = 4.85f, RevealDuration = 1.35f;
        public const float TurnDuration = 1.05f, WrittenHold = .4f;
        const string ArtRoot = "NotebookOpening/";
        readonly List<NotebookLeafGraphic> leaves = new List<NotebookLeafGraphic>();
        Texture2D cover, liner, paper;
        Texture2D[] writing, drafts, pictures;
        Shader shader;
        int used;
        readonly int[] drawOrder = new int[7];
        RawImage shadow;
        Texture2D shadowTexture;
        float age, opening;
        public int PageCount => pictures?.Length ?? 0;
        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        public static float Ease(float t) { t = Mathf.Clamp01(t); return t < .5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2; }
        public static float Reveal(float pageAge, bool first) => Smooth((pageAge - (first ? FirstReveal : TurnDuration + WrittenHold)) / RevealDuration);
        public static float RevealEnd(bool first) => (first ? FirstReveal : TurnDuration + WrittenHold) + RevealDuration;

        // Book space (x across the spread from the binding, y down from the middle, z up off the desk) to the 960x540 overlay.
        public static Vector3 Project(float x, float y, float z, float age, float opening)
        {
            x -= Mathf.Lerp(175, 0, Smooth((age - 1.15f) / 2.1f));
            float roll = Mathf.Lerp(-.045f, 0, opening), tilt = Mathf.Lerp(.17f, .075f, opening);
            float xx = x * Mathf.Cos(roll) - y * Mathf.Sin(roll), yy = x * Mathf.Sin(roll) + y * Mathf.Cos(roll);
            float ty = yy * Mathf.Cos(tilt) - z * Mathf.Sin(tilt), tz = yy * Mathf.Sin(tilt) + z * Mathf.Cos(tilt);
            float p = 1550 / (1550 - tz) * Mathf.Lerp(1.06f, 1, opening);
            return new Vector3(480 + xx * p * .7f, -(246 + ty * p * .7f), 0);
        }

        // A pixel of the open, resting spread texture (1400x940, both pages) on the overlay, and overlay units per texel.
        public Vector2 SpreadPoint(float texelX, float texelY, out float scale)
        {
            float x = (texelX - 700) * 350 / 700, y = texelY * 470 / 940 - 235;
            var at = Project(x, y, 1, age, opening);
            scale = Vector2.Distance(at, Project(x + 350 / 700f, y, 1, age, opening));
            return at;
        }

        public void Initialize(int count)
        {
            shader = Resources.Load<Shader>(ArtRoot + "NotebookPage");
            if (!shader) throw new InvalidOperationException("Notebook page shader is missing");
            Texture2D Load(string name) => Resources.Load<Texture2D>(ArtRoot + name)
                ?? throw new InvalidOperationException("Notebook texture missing: " + name);
            cover = Load("cover"); liner = Load("liner"); paper = Load("paper");
            writing = new Texture2D[5];
            for (int i = 0; i < writing.Length; i++) writing[i] = Load("written-" + i);
            var shadowObject = new GameObject("Book shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            shadowObject.transform.SetParent(transform, false); shadow = shadowObject.GetComponent<RawImage>(); shadow.raycastTarget = false;
            shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax = shadow.rectTransform.pivot = new Vector2(0, 1);
            shadowTexture = new Texture2D(128, 64, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[128 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
            {
                float radius = Mathf.Sqrt(Mathf.Pow((x + .5f - 64) / 64, 2) + Mathf.Pow((y + .5f - 32) / 32, 2));
                pixels[y * 128 + x] = new Color(0, 0, 0, (1 - Smooth(radius)) * .6f);
            }
            shadowTexture.SetPixels32(pixels); shadowTexture.Apply(false, true); shadow.texture = shadowTexture;
            drafts = new Texture2D[count]; pictures = new Texture2D[count];
            for (int i = 0; i < count; i++) { drafts[i] = Load($"page-{i}-draft"); pictures[i] = Load($"page-{i}-final"); }
        }

        public void Draw(float introAge, int page, float pageAge)
        {
            age = introAge; opening = Ease((age - 1.3f) / 1.65f); used = 0;
            float shadowWidth = Mathf.Lerp(364, 644, opening);
            shadow.rectTransform.anchoredPosition = new Vector2(480 - shadowWidth / 2, -286);
            shadow.rectTransform.sizeDelta = new Vector2(shadowWidth, 150);
            // Stacked fore-edge under the right board, matching the preview's warm paper thickness.
            for (int k = 4; k >= 0; k--)
                Sheet(paper, paper, 1, -1, 0, -3 - k * 3, 0, new Color(.87f, .80f, .66f), 356, 480);
            if (opening > .999f) Sheet(liner, liner, 1, -1, Mathf.PI, -8, 0, Color.white);
            bool intro = age < IntroEnd;
            float turn = page > 0 ? Mathf.Clamp01(pageAge / TurnDuration) : 1;
            float reveal = Reveal(pageAge, page == 0);
            if (intro)
            {
                int landed = -1, pending = -1;
                for (int i = 0; i < 7; i++) { float u = Flutter(i); if (u >= 1) landed = i; if (u <= 0 && pending < 0) pending = i; }
                if (landed >= 0) Back(landed, Mathf.PI, 1, 0);
                else if (opening > .999f) Sheet(liner, liner, 1, -1, Mathf.PI, 1, 0, Color.white);
                Page(0, 1, 0, 1, 0, reveal);
                if (pending >= 0) Sheet(writing[pending % 5], writing[pending % 5], 1, -1, 0, 9 + pending * .45f, 0, Color.white);
                // Draw low leaves first and raised leaves last, as in the approved preview.
                for (int i = 0; i < 7; i++) drawOrder[i] = i;
                for (int i = 1; i < 7; i++) for (int j = i; j > 0 && Mathf.Sin(Flutter(drawOrder[j]) * Mathf.PI) < Mathf.Sin(Flutter(drawOrder[j - 1]) * Mathf.PI); j--)
                    (drawOrder[j], drawOrder[j - 1]) = (drawOrder[j - 1], drawOrder[j]);
                foreach (int i in drawOrder)
                {
                    float u = Flutter(i); if (u <= 0 || u >= 1) continue;
                    float angle = Ease(u) * Mathf.PI, z = Mathf.Lerp(9 + i * .45f, 1, Ease(u));
                    if (angle > Mathf.PI / 2) Back(i, angle, z, .46f);
                    else Sheet(writing[i % 5], writing[i % 5], 1, -1, angle, z, .46f, Color.white);
                }
            }
            else
            {
                Page(turn < 1 ? page - 1 : page, 0, Mathf.PI, 1, 0, turn < 1 ? 1 : reveal);
                Page(page, 1, 0, 1, 0, reveal);
                if (turn < 1)
                {
                    float angle = Ease(turn) * Mathf.PI;
                    // Outgoing illustration stays on the front; the incoming writing is on the reverse.
                    Page(angle <= Mathf.PI / 2 ? page - 1 : page, angle <= Mathf.PI / 2 ? 1 : 0,
                        angle, Mathf.Lerp(10, 1, Ease(turn)), .4f, angle <= Mathf.PI / 2 ? 1 : reveal);
                }
            }
            if (opening < 1)
            {
                // Generated cover has translucent leather pixels; an opaque board stops hidden writing bleeding through.
                if (opening <= .5f) Sheet(Texture2D.whiteTexture, Texture2D.whiteTexture, 1, -1, opening * Mathf.PI, 13, 0, new Color32(86, 53, 30, 255));
                Sheet(opening <= .5f ? cover : liner, opening <= .5f ? cover : liner, 1, -1, opening * Mathf.PI, 13, 0, Color.white);
            }
            for (int i = used; i < leaves.Count; i++) leaves[i].gameObject.SetActive(false);
        }
        void OnDestroy() { if (shadowTexture) Destroy(shadowTexture); }
        float Flutter(int i) => Mathf.Clamp01((age - (2.58f + i * .165f)) / .86f);
        void Back(int i, float angle, float z, float bend)
        {
            if (i == 6) Page(0, 0, angle, z, bend, 0);
            else Sheet(writing[(i + 1) % 5], writing[(i + 1) % 5], 1, -1, angle, z, bend, Color.white);
        }
        void Page(int index, int side, float angle, float z, float bend, float reveal)
            => Sheet(pictures[index], drafts[index], reveal, side, angle, z, bend, Color.white);
        void Sheet(Texture2D final, Texture2D draft, float reveal, int side, float angle, float z, float bend, Color tint, float width = 350, float height = 470)
        {
            if (used == leaves.Count)
            {
                var go = new GameObject("Notebook leaf", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(transform, false);
                var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.sizeDelta = new Vector2(960, 540);
                var leaf = go.AddComponent<NotebookLeafGraphic>(); leaf.Initialize(shader); leaves.Add(leaf);
            }
            var item = leaves[used++]; item.gameObject.SetActive(true);
            item.Set(final, draft, reveal, side, angle, z, bend, age, opening, tint, width, height);
        }
    }

    public sealed class NotebookLeafGraphic : MaskableGraphic
    {
        Texture texture;
        Material owned;
        float angle, depth, bend, age, opening, width, height;
        int side;
        public override Texture mainTexture => texture ? texture : Texture2D.whiteTexture;
        public void Initialize(Shader shader) { raycastTarget = false; owned = new Material(shader) { hideFlags = HideFlags.DontSave }; material = owned; }
        public void Set(Texture final, Texture draft, float reveal, int half, float a, float z, float b, float time, float open, Color tint, float w, float h)
        {
            texture = final; side = half; angle = a; depth = z; bend = b; age = time; opening = open; color = tint; width = w; height = h;
            owned.SetTexture("_DraftTex", draft); owned.SetFloat("_Reveal", reveal);
            SetVerticesDirty(); SetMaterialDirty();
        }
        protected override void OnDestroy() { if (owned) Destroy(owned); base.OnDestroy(); }
        Vector3 Project(float x, float y, float z) => NotebookBookView.Project(x, y, z, age, opening);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); const int nx = 22, ny = 2; float x = 0, z = depth;
            bool back = angle > Mathf.PI / 2;
            for (int i = 0; i <= nx; i++)
            {
                float u = (float)i / nx;
                if (i > 0) { float mid = (i - .5f) / nx, phi = angle + bend * Mathf.Sin(Mathf.PI * mid) * Mathf.Sin(angle); x += width / nx * Mathf.Cos(phi); z += width / nx * Mathf.Sin(phi); }
                float shade = Mathf.Sin(angle) * (.06f + .18f * Mathf.Pow(u, 1.3f));
                Color tint = color; tint.r *= 1 - shade; tint.g *= 1 - shade; tint.b *= 1 - shade;
                for (int j = 0; j <= ny; j++)
                {
                    float v = (float)j / ny, tx = back ? 1 - u : u;
                    if (side >= 0) tx = (side + tx) * .5f;
                    vh.AddVert(Project(x, (v - .5f) * height, z + Mathf.Sin(v * Mathf.PI) * Mathf.Sin(angle) * bend * 12), tint, new Vector2(tx, 1 - v));
                }
            }
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++)
            { int a = i * (ny + 1) + j, b = a + ny + 1; vh.AddTriangle(a, b, b + 1); vh.AddTriangle(a, b + 1, a + 1); }
        }
    }
}
