using System;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // A static acquisition card; the village has already been coloured by the world bottle.
    public sealed class VillageInkRevealView : MonoBehaviour
    {
        public bool Playing { get; private set; }
        public int Piece { get; private set; }
        public float Elapsed { get; private set; }
        public string Phase => Playing ? "acquire" : "idle";
        VillageInkAssets art;
        CanvasGroup content;
        Image bottle;
        Text count;
        Image[] marks;
        static readonly Color32[] ProgressColors = { new Color32(255,93,103,255), new Color32(255,218,73,255), new Color32(98,233,141,255), new Color32(85,200,255,255), new Color32(200,133,255,255) };
        static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0,1);
            rt.anchoredPosition = new Vector2(x,-y); rt.sizeDelta = new Vector2(w,h); return rt;
        }
        static Image ImageAt(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
        {
            var image = Box(parent,name,x,y,w,h).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false; return image;
        }
        public void Configure(Font font)
        {
            art = new VillageInkAssets(); content = gameObject.AddComponent<CanvasGroup>(); content.blocksRaycasts = false;
            var dim = ImageAt(transform,"잉크 획득 음영",null,0,0,960,540); dim.color = new Color(0,0,0,.52f);
            ImageAt(transform,"마을 잉크 획득 제목",art.Get("ui/title-banner"),290,84,380,64);
            bottle = ImageAt(transform,"획득한 닫힌 잉크 병",null,370,166,220,220); bottle.preserveAspect = true;
            bottle.rectTransform.pivot = Vector2.one*.5f; bottle.rectTransform.anchoredPosition = new Vector2(480,-276);
            Box(transform,"잉크 개수 그라데이션",400,388,160,46).gameObject.AddComponent<VillageInkCountShade>().raycastTarget = false;
            ImageAt(transform,"잉크 방울 테두리",art.Get("ui/counter-banner-blank"),385,434,190,42);
            count = Box(transform,"마을 잉크 개수",428,394,104,32).gameObject.AddComponent<Text>();
            count.font = font; count.fontSize = 24; count.alignment = TextAnchor.MiddleCenter; count.color = new Color32(248,239,217,255);
            count.horizontalOverflow = HorizontalWrapMode.Overflow; count.verticalOverflow = VerticalWrapMode.Overflow;
            marks = new Image[5];
            for(int i=0;i<5;i++)
            {
                marks[i]=ImageAt(transform,"마을 잉크 방울 "+(i+1),null,405+i*32,440,22,28);
                marks[i].rectTransform.pivot=Vector2.one*.5f;
                marks[i].rectTransform.anchoredPosition=new Vector2(416+i*32,-454);
            }
            gameObject.SetActive(false);
        }
        public void Begin(int piece, Vector2 unused)
        {
            Piece = Mathf.Clamp(piece,1,5); Elapsed = 0; Playing = true;
            bottle.sprite = art.Get("acquisition/"+VillageInkAssets.Colors[Piece-1]+"-closed"); count.text = $"{Piece} / 5";
            for(int i=0;i<5;i++)
            {
                marks[i].sprite = art.Get(i<Piece ? "ui/progress-drop-filled" : "ui/progress-drop-empty");
                marks[i].color = i<Piece ? (Color)ProgressColors[i] : new Color32(166,171,184,255); marks[i].preserveAspect = true;
            }
            content.alpha = 0; gameObject.SetActive(true); transform.SetAsLastSibling();
        }
        public void Step(float dt) { if (!Playing) return; Elapsed += Mathf.Max(0,dt); content.alpha = Mathf.SmoothStep(0,1,Elapsed/.55f); }
        public void Cancel() { Playing = false; gameObject.SetActive(false); }
        void OnDestroy() { art?.Dispose(); }
    }

    // Soft black backing for the count, independent of the framed collection marks below it.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VillageInkCountShade : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            const int columns = 16, rows = 6;
            var rect = rectTransform.rect;
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns, v = y / (float)rows;
                    float edge = Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) * 4);
                    var shade = new Color(0, 0, 0, .85f * edge * Mathf.Sin(Mathf.PI * v));
                    vh.AddVert(new Vector3(rect.xMin + u * rect.width, rect.yMin + v * rect.height, 0), shade, new Vector2(u, v));
                    if (x == 0 || y == 0) continue;
                    int n = y * (columns + 1) + x, previous = n - columns - 1;
                    vh.AddTriangle(previous - 1, previous, n - 1); vh.AddTriangle(previous, n, n - 1);
                }
        }
    }

    // A moving ink ribbon is geometry, so the five original bottle PNGs stay unmodified.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VillageInkRibbon : MaskableGraphic
    {
        Vector2 start, end; Color ink; float amount;
        public void Set(Vector2 from, Vector2 to, Color color, float progress)
        { start = from; end = to; ink = color; amount = progress; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (amount <= 0) return;
            const int steps = 32;
            Vector2 c1 = start + new Vector2(-45, 55), c2 = end + new Vector2(45, -65);
            Vector2 Point(float t)
            {
                float u = 1 - t;
                var p = u*u*u*start + 3*u*u*t*c1 + 3*u*t*t*c2 + t*t*t*end;
                return new Vector2(p.x, -p.y);
            }
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1), next = Mathf.Min(1, t + .01f);
                var p = Point(t); var tangent = Point(next) - Point(Mathf.Max(0, t - .01f));
                var normal = new Vector2(-tangent.y, tangent.x).normalized * (1.2f + Mathf.Sin(t * Mathf.PI) * 1.4f) * amount;
                var col = ink; col.a = amount * Mathf.Lerp(.8f, .25f, t);
                vh.AddVert(p - normal, col, Vector2.zero); vh.AddVert(p + normal, col, Vector2.one);
                if (i == 0) continue;
                int n = i * 2; vh.AddTriangle(n-2, n-1, n); vh.AddTriangle(n-1, n+1, n);
            }
        }
    }
}
