using Deviant.Combat;
using Deviant.Presentation.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace ForestCombatV2
{
    // 방 섬멸·충전소의 공통 회복 연출. 체력 증가와 수치는 호출자가 결정한다.
    // 몸에는 회복 파츠와 같은 heal_green 시트를 띄우고, 화면 외곽선에는 UI 초록빛을
    // 0.15초 동안 켠 뒤 0.75초 동안 흐린다. 전투 시계(arena.Now)를 따르므로 일시정지·피격 멈춤 중에는 진행하지 않는다.
    // 전투 리셋은 진행 중인 테두리를 지운다.
    public sealed class Chapter1RoomClearHealFx : MonoBehaviour
    {
        public const float RiseSeconds = .15f, FadeSeconds = .75f;
        // 회복은 초록, 피격은 빨강으로 같은 두께·불투명도·시간 곡선을 사용한다.
        const float Border = .16f, Opacity = .5f;
        static readonly Color Green = new Color(.36f, 1f, .45f);
        Chapter1RoomClearHealing healing;
        Chapter1InkPartFx bodyFx;
        CombatArena arena;
        Texture2D glow;
        Canvas canvas;
        RawImage edge;
        float startedAt = float.NegativeInfinity;
        public bool GlowActive => arena && arena.Now - startedAt < RiseSeconds + FadeSeconds;
        public bool GlowVisible => edge && edge.enabled && edge.color.a > 0;
        public int Played { get; private set; }

        public void Initialize(Chapter1RoomClearHealing source, Chapter1InkPartFx body, CombatArena owner)
        {
            healing = source; bodyFx = body; arena = owner;
            if (healing) healing.Healed += Play;
            if (arena) arena.ResetCompleted += StopGlow;
            EnsureOverlay();
        }

        void OnDestroy()
        {
            if (healing) healing.Healed -= Play;
            if (arena) arena.ResetCompleted -= StopGlow;
            if (glow) Destroy(glow);
            if (canvas) Destroy(canvas.gameObject);
        }

        public void Play(float restored)
        {
            if (!arena || restored <= 0) return;
            Played++;
            if (bodyFx) bodyFx.PlayHeal();
            startedAt = arena.Now;
            Refresh();
        }

        void StopGlow() { startedAt = float.NegativeInfinity; Refresh(); }
        void OnDisable() { if (edge) edge.enabled = false; }
        void LateUpdate() => Refresh();

        void EnsureOverlay()
        {
            if (canvas) return;
            var root = new GameObject("Room clear heal overlay", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var image = new GameObject("Green healing edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            image.transform.SetParent(root.transform, false);
            edge = image.GetComponent<RawImage>(); edge.texture = Glow(); edge.raycastTarget = false;
            edge.rectTransform.offsetMin = edge.rectTransform.offsetMax = Vector2.zero;
            edge.enabled = false;
        }

        // 64×64 테두리: 화면 가장자리가 가장 진하고 Border 안쪽으로 갈수록 투명해진다. 화면 크기에 맞춰 늘린다.
        Texture2D Glow()
        {
            if (glow) return glow;
            const int size = 64;
            glow = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Room clear heal edge", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f), v = y / (size - 1f), edge = Mathf.Min(u, 1 - u, v, 1 - v);
                    pixels[y * size + x] = new Color(Green.r, Green.g, Green.b, Mathf.Pow(Mathf.Clamp01((Border - edge) / Border), 2));
                }
            glow.SetPixels(pixels); glow.Apply(false, true);
            return glow;
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled || !GlowActive || !arena.ViewCamera)
            { if (edge) edge.enabled = false; return; }
            EnsureOverlay();
            float age = Mathf.Max(0, arena.Now - startedAt);
            float rise = Mathf.Clamp01(age / RiseSeconds), fade = Mathf.Clamp01((age - RiseSeconds) / FadeSeconds);
            var projection = arena.ViewCamera.GetComponent<CameraDisplayProjection>();
            var renderCamera = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
            var target = renderCamera ? renderCamera.targetTexture : null;
            float width = target ? target.width : Screen.width, height = target ? target.height : Screen.height;
            var pixel = renderCamera ? renderCamera.pixelRect : arena.ViewCamera.pixelRect;
            bool presented = !renderCamera && projection && projection.PresentedTexture &&
                arena.ViewCamera.targetTexture == projection.PresentedTexture && projection.DisplayRect != null;
            var view = presented ? projection.DisplayRect() : new Rect(pixel.x, height - pixel.yMax, pixel.width, pixel.height);
            edge.enabled = true;
            edge.color = new Color(1, 1, 1, Opacity * rise * (1 - fade * fade));
            edge.rectTransform.anchorMin = new Vector2(view.xMin / width, 1 - view.yMax / height);
            edge.rectTransform.anchorMax = new Vector2(view.xMax / width, 1 - view.yMin / height);
            edge.rectTransform.offsetMin = edge.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
