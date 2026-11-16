using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace Deviant.Chapter2
{
    // v26 지하수로 화풍에서 주인공·몹이 배경 조명에 녹아들게 한다(2026-11-12 사용자 요청).
    //  - 색: 다른 연출이 그 프레임에 정한 색(죽음 회색·투명·잉크 등) 위에, 맨 마지막에 그 자리의 빛을 곱한다.
    //    어두운 곳은 청록으로 가라앉고, 등 둘레는 따뜻하게, 채광 아래는 맑게. 맞을 때 번쩍임(재질)은 그대로다.
    //  - 그림자: 땅에 서 있을 때 발밑에 v26 와 같은 어두운 띠(폭 40px)를 둔다.
    //  - 주인공 빛: 몸 둘레 원형으로 뒷벽·배관 돌 단계를 올린다(SewerWall 셰이더 전역 값 _SewerPlayerLight). 덮개가 아니라 등빛과 같은 방식.
    // 보스 전투장 안의 배우는 손대지 않는다(보스전 연출이 따로 있다).
    [DefaultExecutionOrder(10000)]
    public sealed class Chapter2SewerActorLight : MonoBehaviour
    {
        public static readonly Color Dark = new Color(.76f, .84f, .88f), Warm = new Color(1f, .9f, .76f), Cool = new Color(.96f, 1f, 1f);
        public const float PlayerRadiusPx = 160, PlayerLevels = 2.4f, PlayerTintFloor = .3f;
        static readonly int PlayerLightId = Shader.PropertyToID("_SewerPlayerLight");
        sealed class Entry { public Color applied, baseColor; }
        readonly Dictionary<SpriteRenderer, Entry> tinted = new Dictionary<SpriteRenderer, Entry>();
        readonly Dictionary<CombatActor, (SpriteRenderer[] renderers, float at)> cache = new Dictionary<CombatActor, (SpriteRenderer[], float)>();
        readonly Dictionary<CombatActor, SpriteRenderer> shadows = new Dictionary<CombatActor, SpriteRenderer>();
        Chapter2World world; Chapter2SewerDressing dressing; Sprite shadowSprite, auraSprite; Texture2D shadowTex, auraTex; SpriteRenderer aura;
        // 주인공 빛 무리: 몸 가운데를 중심으로 반지름 80px 둥근 빛. 옅은 민트빛 흰색, 가운데 32% 에서 4단계 디더로 옅어진다. 몸 바로 뒤에 둔다.
        public const int AuraRadiusPx = 80; public const float AuraAlpha = .32f; public static readonly Color32 AuraColor = new Color32(214, 240, 230, 255);
        public SpriteRenderer Aura => aura;

        public void Bind(Chapter2World owner, Chapter2SewerDressing art)
        {
            world = owner; dressing = art;
            // 발밑 그림자: 40×3px, 가운데가 진하고 끝이 4단계 디더로 옅어진다.
            shadowTex = new Texture2D(40, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "v26 actor foot shadow" };
            var px = new Color32[40 * 3];
            for (int y = 0; y < 3; y++) for (int x = 0; x < 40; x++)
            {
                float d = Mathf.Abs(x - 19.5f) / 20f + (y == 1 ? 0 : .35f), k = Mathf.Clamp01(1 - d);
                float b = ((x + y * 2) % 4 + .5f) / 4, q = Mathf.Floor(k * 4 + b * .9f) / 4;
                px[y * 40 + x] = new Color32(4, 8, 10, (byte)Mathf.RoundToInt(q * .45f * 255));
            }
            shadowTex.SetPixels32(px); shadowTex.Apply(false, true);
            shadowSprite = Sprite.Create(shadowTex, new Rect(0, 0, 40, 3), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
            int n = AuraRadiusPx * 2 + 1; auraTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "v26 hero aura" };
            var ap = new Color32[n * n]; float[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - AuraRadiusPx) * (x - AuraRadiusPx) + (y - AuraRadiusPx) * (y - AuraRadiusPx)), k = Mathf.Pow(Mathf.Clamp01(1 - d / AuraRadiusPx), 1.4f);
                float q = Mathf.Floor(k * 4 + (bayer[(y % 4) * 4 + x % 4] + .5f) / 16 * .9f) / 4;
                var c = AuraColor; c.a = (byte)Mathf.RoundToInt(q * AuraAlpha * 255); ap[y * n + x] = c;
            }
            auraTex.SetPixels32(ap); auraTex.Apply(false, true);
            auraSprite = Sprite.Create(auraTex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
            var go = new GameObject("Sewer v26 · hero aura"); go.transform.SetParent(transform, false);
            aura = go.AddComponent<SpriteRenderer>(); aura.sprite = auraSprite; aura.sharedMaterial = world.spriteMaterial; aura.enabled = false;
        }

        public Color TintAt(Vector2 at, bool player)
        {
            var (L, W) = dressing.LightAt(at);
            float lit = Mathf.Clamp01(L / 3f); if (player) lit = Mathf.Max(lit, PlayerTintFloor);
            return Color.Lerp(Dark, Color.Lerp(Cool, Warm, W), lit);
        }

        void LateUpdate()
        {
            if (!world || !dressing || world.arena == null) return;
            var player = world.arena.Player;
            if (player) { var c = player.Center; Shader.SetGlobalVector(PlayerLightId, new Vector4(c.x, c.y, PlayerRadiusPx, PlayerLevels)); }
            if (player) Apply(player, true);
            if (aura)
            {
                bool show = player && !player.Dead && player.visual && player.visual.enabled && (world.Sewer == null || !world.Sewer.InBossArea(player.Position));
                aura.enabled = show;
                if (show)
                {
                    var c = player.Center; aura.transform.position = new Vector3(Mathf.Round(c.x * 32) / 32, Mathf.Round(c.y * 32) / 32, 0);   // 화소에 맞춰 디더가 떨리지 않게
                    aura.sortingOrder = player.visual.sortingOrder - 1;
                    aura.color = new Color(1, 1, 1, .9f + .1f * Mathf.Sin(Time.time * 2.2f));                                             // 아주 약하게 숨 쉰다
                }
            }
            foreach (var enemy in world.arena.enemies) if (enemy) Apply(enemy, false);
        }

        void Apply(CombatActor actor, bool player)
        {
            bool boss = world.Sewer != null && world.Sewer.InBossArea(actor.Position);
            if (!cache.TryGetValue(actor, out var entry) || Time.unscaledTime - entry.at > .5f)
            {
                entry = (actor.GetComponentsInChildren<SpriteRenderer>(true), Time.unscaledTime); cache[actor] = entry;
            }
            var tint = boss ? Color.white : TintAt(actor.Center, player);
            foreach (var r in entry.renderers)
            {
                if (!r || r.sortingOrder > 20 || (shadows.TryGetValue(actor, out var own) && own == r)) continue;
                if (!tinted.TryGetValue(r, out var t)) tinted[r] = t = new Entry { applied = new Color(-1, -1, -1, -1) };
                if (r.color != t.applied) t.baseColor = r.color;              // 다른 연출이 이번에 정한 색을 바탕으로
                t.applied = new Color(t.baseColor.r * tint.r, t.baseColor.g * tint.g, t.baseColor.b * tint.b, t.baseColor.a);
                r.color = t.applied;
            }
            if (!shadows.TryGetValue(actor, out var shadow) || !shadow)
            {
                var go = new GameObject("Sewer v26 · foot shadow"); go.transform.SetParent(transform, false);
                shadow = go.AddComponent<SpriteRenderer>(); shadow.sprite = shadowSprite; shadow.sharedMaterial = world.spriteMaterial; shadows[actor] = shadow;
            }
            bool visible = !boss && actor.gameObject.activeInHierarchy && actor.grounded && !actor.Dead && actor.visual && actor.visual.enabled;
            shadow.enabled = visible;
            if (visible) { shadow.transform.position = actor.Position + Vector2.down * (1 / 32f); shadow.sortingOrder = (actor.visual ? actor.visual.sortingOrder : 10) - 1; }
        }

        void OnDisable() { Shader.SetGlobalVector(PlayerLightId, Vector4.zero); }
        void OnDestroy()
        {
            Shader.SetGlobalVector(PlayerLightId, Vector4.zero);
            if (shadowSprite) Destroy(shadowSprite); if (shadowTex) Destroy(shadowTex); if (auraSprite) Destroy(auraSprite); if (auraTex) Destroy(auraTex);
        }
    }
}
