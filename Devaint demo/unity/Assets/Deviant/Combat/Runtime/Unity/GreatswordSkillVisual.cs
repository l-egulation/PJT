using UnityEngine;

namespace Deviant.Combat
{
    // World effects have a separate visual lifetime. The cast remains the
    // authority for installation admission, timing and hit ownership.
    [DefaultExecutionOrder(110)]
    internal sealed class GreatswordSkillVisual : MonoBehaviour
    {
        const string Root = "CharacterGreatswordReviewed/";
        static readonly float[] FallTimes = { .260f, .310f, .360f, .400f, .429f, .450f, .472f, .500f, .530f, .580f, .640f, .715f, .810f, .900f, 1f, 1.120f, 1.210f, 1.270f };
        static readonly float[] WaveTimes = { 0, .020f, .040f, .060f, .080f, .110f, .140f, .170f, .200f, .230f, .260f, 1.440f, 1.600f, 1.750f };
        static Sprite[] falling, flying, shockwave;
        CombatArena arena;
        CombatCast cast;
        CombatActor owner;
        SpriteRenderer view;
        LineRenderer[] suction;
        string id;
        int facing;
        int ownerLife;
        float age, lastNow, anchorTime, impact, fallScale = 1;
        // 반월참 소환 검(2026-11-11 확정 시안): 칼 그림 한 장 + 코드로 그리는 기울임·옆면·후광·빛줄기·반짝임 + 땅 강타 프레임
        static Sprite summoned, bladeSprite, glow, square, cross;
        // 반월참 소환 칼과 땅 강타는 캐릭터·몬스터 몸(PlayerSortingOrder 10, 몬스터 10 안팎)보다 앞에 그린다(2026-11-13 사용자: 몬스터 뒤에 가려지면 안 된다).
        internal const int SummonFrontOffset = 30;
        static Sprite[] slam, cracks;
        Transform summon; SpriteRenderer swordView, haloView, slamView, crackView; SpriteRenderer[] rays, glints; SpriteMask mask;
        float boxTop = 10;
        Vector2 origin, destination;
        bool released;

        internal static bool Supports(string action) =>
            action == "greatsword_default" || action == "greatsword_range" || action == "greatsword_follow_up";

        internal static GreatswordSkillVisual Attach(CombatCast cast)
        {
            var node = new GameObject("Greatsword " + cast.Definition.Id);
            node.transform.SetParent(cast.arena.transform, false);
            var effect = node.AddComponent<GreatswordSkillVisual>();
            effect.Bind(cast);
            return effect;
        }

        void Bind(CombatCast value)
        {
            cast = value;
            arena = cast.arena;
            owner = cast.owner;
            ownerLife = owner.LifeVersion;
            id = cast.Definition.Id;
            lastNow = arena.Now;
            facing = cast.FeelFacing;
            view = gameObject.AddComponent<SpriteRenderer>();
            view.sharedMaterial = arena.spriteMaterial;
            view.sortingLayerID = owner.visual.sortingLayerID;
            view.sortingOrder = owner.visual.sortingOrder - 1;
            view.enabled = false;
            if (id == "greatsword_default")
            {
                shockwave ??= Load("wave", WaveTimes.Length);
                suction = new LineRenderer[12];
                for (int i = 0; i < suction.Length; i++)
                {
                    var node = new GameObject("Charging ink");
                    node.transform.SetParent(transform, false);
                    var line = suction[i] = node.AddComponent<LineRenderer>();
                    line.sharedMaterial = arena.spriteMaterial;
                    line.positionCount = 7;
                    line.sortingLayerID = view.sortingLayerID;
                    line.sortingOrder = view.sortingOrder;
                    line.startWidth = .012f;
                    line.endWidth = .042f;
                    line.enabled = false;
                }
            }
            else if (id == "greatsword_range")
            {
                falling ??= Load("fall", FallTimes.Length);
                summoned ??= Resources.Load<Sprite>(Root + "summoned-sword");
                if (slam == null) { var list = new System.Collections.Generic.List<Sprite>(); for (int i = 0; i < 99; i++) { var f = Resources.Load<Sprite>(Root + "slam/" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture)); if (!f) break; list.Add(f); } slam = list.ToArray(); }
                if (summoned && slam.Length > 0) BuildSummon();
                // 반월참 그림은 판정 폭 8u 에 맞춰 구워졌다. 지금 판정 폭으로 발 기준 균일 확대한다(SkillFxHitScale).
                var box = cast.Definition.Hits.Count > 0 ? cast.Definition.Hits[0].Box : null;
                fallScale = SkillFxHitScale.GreatswordFall(box?.Width ?? 0);
                if (box != null) boxTop = box.Y + box.Height * .5f;   // 칼은 판정 위쪽 선에서 나타난다
            }
            else flying ??= Load("flight", 12);
        }

        static Sprite[] Load(string stem, int count)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
                frames[i] = Resources.Load<Sprite>(Root + stem + "-" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            return frames;
        }

        static int FrameAt(float age, float[] times)
        {
            int frame = 0;
            while (frame + 1 < times.Length && age >= times[frame + 1]) frame++;
            return frame;
        }

        void LateUpdate()
        {
            if (!owner || owner.Dead || owner.LifeVersion != ownerLife || !arena || !arena.FeelFeedback || !arena.FeelFeedback.EffectsEnabled)
            { Destroy(gameObject); return; }
            if (!released && (!cast || cast.PresentationCancelled)) { Destroy(gameObject); return; }
            age = cast ? cast.Age : age + Mathf.Max(0, arena.Now - lastNow);
            lastNow = arena.Now;
            switch (id)
            {
                case "greatsword_default": DrawCharge(); break;
                case "greatsword_range": DrawFall(); break;
                default: DrawFlight(); break;
            }
        }

        void DrawCharge()
        {
            bool drawingCharge = !released && cast && !cast.ChargeRelease.HasValue;
            Vector2 blade = owner.Position + new Vector2(-.82f * facing, 1.12f);
            for (int i = 0; i < suction.Length; i++)
            {
                var line = suction[i];
                float cycle = age - i * .039f;
                line.enabled = drawingCharge && cycle >= 0;
                if (!line.enabled) continue;
                float t = cycle % .62f / .62f;
                var color = new Color(.13f, .10f, .10f, Mathf.Sin(t * Mathf.PI) * .85f);
                line.startColor = line.endColor = color;
                for (int j = 0; j < line.positionCount; j++)
                {
                    float progress = Mathf.Max(0, t - .16f + .16f * j / (line.positionCount - 1));
                    float radius = (2.2f + .21f * (i % 4)) * (1 - progress * progress);
                    float angle = i * 2.39996f + progress * progress * 4.6f;
                    line.SetPosition(j, blade + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            if (!released && cast && cast.PresentationActivated && age >= cast.ImpactTime)
            {
                released = true;
                impact = cast.ImpactTime;
                origin = owner.Position + Vector2.right * (1.35f * facing);
            }
            if (!released) return;
            float time = age - impact;
            if (time >= 1.78f) { Destroy(gameObject); return; }
            Present(shockwave[FrameAt(time, WaveTimes)], origin, 1, 1);
        }

        void DrawFall()
        {
            if (!released)
            {
                impact = cast.ImpactTime;
                var feet = owner.Position;
                float ground = arena.FindFloor(feet + Vector2.up * .12f, .5f) ?? feet.y;
                origin = new Vector2(feet.x, ground);
                // 반월참 시전은 PresentationActivated 를 켜지 않는다. 그걸 기다리면 놓아 주지 못해 시전이 끝나는 순간(타격 뒤 0.6초)
                // LateUpdate 가 칼·연기를 통째로 지웠다. 타격이 지나면 취소만 아니면 놓아 주고, 그 뒤엔 그림이 제 시간표대로 사라진다.
                released = age >= impact && !cast.PresentationCancelled;
            }
            if (summon) { DrawSummon(age - impact); return; }
            float sample = age - impact + .45f;
            if (sample >= 1.285f) { Destroy(gameObject); return; }
            if (sample < FallTimes[0]) return;
            Present(falling[FrameAt(sample, FallTimes)], origin, fallScale, 1);
        }

        // ---- 반월참 소환 검 -------------------------------------------------------------------------------------
        // 시각은 타격(ImpactTime) 기준: -0.30초에 판정 위쪽 선(boxTop)에 나타나 빛을 모으고, -0.10초부터 0.1초 만에 내리꽂혀
        // 2.4u 박힌다. 박힌 칼은 1.0~2.2초에 서서히 사라지고, 땅 강타 프레임은 타격부터 재생한 뒤 마지막 장을 붙든 채 흐려지며,
        // 땅 갈라짐은 장을 넘기며 작아진다(시간은 SummonedSword).
        const float SummonAppear = SummonedSword.Appear, SummonDrop = SummonedSword.Drop, SummonFadeFrom = SummonedSword.FadeFrom, SummonGone = SummonedSword.Gone, SlamFps = SummonedSword.SlamFps;
        const float SlamFadeFrom = SummonedSword.SlamFadeFrom, SlamGone = SummonedSword.SlamGone;
        const float SummonLean = SummonedSword.Lean, SummonSquash = SummonedSword.Squash;
        static readonly Color SideGold = new Color32(0xb8, 0x78, 0x0e, 255), HaloGold = new Color(1f, .86f, .32f, 1f), RayGold = new Color32(0xff, 0xcf, 0x3a, 255);
        static readonly Vector2[] GlintSpots = { new Vector2(-.5f, .18f), new Vector2(.118f, .561f), new Vector2(-.264f, .942f), new Vector2(.354f, .543f), new Vector2(-.028f, .705f), new Vector2(-.41f, .3f), new Vector2(.208f, .86f), new Vector2(-.174f, .45f), new Vector2(.444f, .25f) };
        static float SummonTip(float t, float top) => SummonedSword.Tip(t, top);

        static Sprite Generated(ref Sprite cache, int size, System.Func<int, int, float> alpha, float ppu)
        {
            if (cache) return cache;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255));
            tex.SetPixels32(px); tex.Apply(false);
            return cache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), ppu);
        }

        SpriteRenderer Child(Transform parent, string name, Sprite sprite, int order, bool masked)
        {
            var node = new GameObject(name); node.transform.SetParent(parent, false);
            var r = node.AddComponent<SpriteRenderer>(); r.sprite = sprite;
            if (arena.spriteMaterial) r.sharedMaterial = arena.spriteMaterial;
            r.sortingLayerID = owner.visual.sortingLayerID; r.sortingOrder = owner.visual.sortingOrder + SummonFrontOffset + order;
            if (masked) r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            r.enabled = false;
            return r;
        }

        // 칼 본체와 짙은 금색 옆면을 구운 한 장(SummonedSword.BakeBlade)을 한 번만 만든다.
        static Sprite BladeWithSide(Sprite source) => bladeSprite ? bladeSprite : bladeSprite = SummonedSword.BakeBlade(source, SideGold);

        void BuildSummon()
        {
            Generated(ref glow, 64, (x, y) => { float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32)) / 32; return d >= 1 ? 0 : Mathf.Pow(1 - d, 1.6f); }, 64);
            Generated(ref square, 4, (x, y) => 1, 4);
            Generated(ref cross, 15, (x, y) => (x == 7 || y == 7) ? 1 - Mathf.Max(Mathf.Abs(x - 7), Mathf.Abs(y - 7)) / 8f : 0, 16);
            summon = new GameObject("Summoned sword").transform; summon.SetParent(transform, false);
            haloView = Child(summon, "Halo", glow, -5, true);
            rays = new SpriteRenderer[10]; for (int i = 0; i < rays.Length; i++) rays[i] = Child(summon, "Ray", square, -5, true);
            swordView = Child(summon, "Sword", summoned.texture.isReadable ? BladeWithSide(summoned) : summoned, -2, true);
            glints = new SpriteRenderer[GlintSpots.Length]; for (int i = 0; i < glints.Length; i++) glints[i] = Child(summon, "Glint", cross, -1, true);
            slamView = Child(transform, "Ground slam", null, -4, false);
            // 땅 갈라짐: 땅 강타 그림에서 떼어 끝부터 오므라드는 장들(SummonedSword.ShrinkCrack). 그림이 읽을 수 없으면 갈라짐 없이 간다.
            var crackSource = slam.Length > SummonedSword.CrackSource ? slam[SummonedSword.CrackSource] : null;
            if (cracks == null && crackSource && crackSource.texture.isReadable)
            { cracks = new Sprite[SummonedSword.CrackSteps + 1]; for (int i = 0; i < cracks.Length; i++) cracks[i] = SummonedSword.ShrinkCrack(crackSource, i); }
            if (cracks != null) crackView = Child(transform, "Ground crack", null, -3, false);
            var maskNode = new GameObject("Above ground"); maskNode.transform.SetParent(transform, false);
            mask = maskNode.AddComponent<SpriteMask>(); mask.sprite = square;
        }

        void DrawSummon(float t)
        {
            bool live = t >= SummonAppear && t < SummonGone, slamLive = t >= 0 && t < SlamGone;
            int slamFrame = slamLive ? Mathf.Min(Mathf.FloorToInt(t * SlamFps + 1e-4f), slam.Length - 1) : -1;
            // 칼·땅 강타·갈라짐이 모두 끝난 뒤에만(시작 전에는 셋 다 아직 없다)
            if (t >= SummonGone && t >= SlamGone && (crackView == null || t >= SummonedSword.CrackGone)) { Destroy(gameObject); return; }
            // 땅 위만 보이게: 원점(땅)부터 위로 덮는 마스크
            mask.transform.position = new Vector3(origin.x, origin.y + 8, 0); mask.transform.localScale = new Vector3(16, 16, 1);
            // 땅 강타: 마지막 장을 붙든 채 SlamFadeFrom~SlamGone 동안 흐려진다
            slamView.enabled = slamLive;
            if (slamLive) { slamView.sprite = slam[slamFrame]; slamView.flipX = facing < 0; slamView.transform.position = origin; slamView.color = new Color(1, 1, 1, t < SlamFadeFrom ? 1 : Mathf.Clamp01(1 - (t - SlamFadeFrom) / (SlamGone - SlamFadeFrom))); }
            // 땅 갈라짐: 가장 또렷한 장에서부터 붙들고 있다가 장을 넘기며 작아진다(투명도는 그대로)
            if (crackView)
            {
                crackView.enabled = t >= SummonedSword.CrackFrom && t < SummonedSword.CrackGone;
                if (crackView.enabled) { crackView.sprite = cracks[SummonedSword.CrackFrame(t)]; crackView.flipX = facing < 0; crackView.transform.position = origin; }
            }
            summon.gameObject.SetActive(live);
            if (!live) return;
            float tip = SummonTip(t, boxTop);
            float shake = t >= 0 && t < .14f ? Mathf.Sin(t * 140) * .08f * (1 - t / .14f) : 0;
            float alpha = Mathf.Clamp01((t - SummonAppear) / .06f) * (t > SummonFadeFrom ? Mathf.Clamp01(1 - (t - SummonFadeFrom) / (SummonGone - SummonFadeFrom)) : 1);
            float glowAmount = t < SummonDrop ? .6f : 1;
            // 기울여 비튼 원근: 살짝 눕히고 가로를 줄인다. 옆면은 짙은 금색 복사본을 뒤로 조금 밀어 두께를 낸다
            summon.position = new Vector3(origin.x + shake * facing, origin.y + tip, 0);
            summon.rotation = Quaternion.Euler(0, 0, -SummonLean * facing);
            summon.localScale = new Vector3(SummonSquash, 1, 1);
            float h = summoned.bounds.size.y, w = summoned.bounds.size.x;
            // 칼 본체+옆면을 구운 한 장(BladeWithSide). 뒤집기는 피벗(칼끝) 기준이라 옆면도 바라보는 쪽 반대편으로 함께 간다.
            swordView.enabled = true;
            swordView.flipX = facing < 0;
            swordView.color = new Color(1, 1, 1, alpha * SummonedSword.BladeOpacity);
            // 후광: 가드 둘레에서 맥동하는 금빛 원, 그 위로 도는 빛줄기 열 개
            float pulse = .55f + .25f * Mathf.Sin(age * 14);
            var guard = new Vector3(0, h * .72f, 0);
            haloView.enabled = true; haloView.transform.localPosition = guard; haloView.transform.localScale = Vector3.one * w * 2.3f;
            var halo = HaloGold; halo.a = alpha * glowAmount * pulse * .9f; haloView.color = halo;
            for (int i = 0; i < rays.Length; i++)
            {
                float a = age * 1.6f + i * Mathf.PI / 5, len = w * (.95f + .25f * Mathf.Sin(age * 9 + i));
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                rays[i].enabled = true;
                rays[i].transform.localPosition = guard + dir * (w * .35f + len * .5f);
                rays[i].transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                rays[i].transform.localScale = new Vector3(len, .06f, 1);
                var rc = RayGold; rc.a = alpha * glowAmount * (.3f + .2f * Mathf.Sin(age * 11 + i * 2)); rays[i].color = rc;
            }
            // 반짝임: 칼날 위 아홉 자리에서 번갈아 터지는 십자 빛
            for (int i = 0; i < glints.Length; i++)
            {
                float k = Mathf.Sin(age * 9 + i * .37f * 17);
                glints[i].enabled = k > .2f;
                if (!glints[i].enabled) continue;
                glints[i].transform.localPosition = new Vector3(GlintSpots[i].x * w * .6f, GlintSpots[i].y * h, 0);
                glints[i].transform.localScale = Vector3.one * (4 + (i % 3) * 2) * (k - .2f) * 2.2f / 7f;
                glints[i].color = new Color(1, .98f, .9f, alpha * glowAmount);
            }
        }


        void DrawFlight()
        {
            if (!released)
            {
                if (!cast.PresentationActivated) return;
                released = true;
                anchorTime = cast.PresentationImpactTime;
                origin = owner.Position + new Vector2(.49f * facing, .75f);
                destination = cast.FeelInstallCenter;
            }
            float time = age - anchorTime;
            if (time >= 1.25f) { Destroy(gameObject); return; }
            float progress = Mathf.Clamp01(time / .33f);
            float returning = Mathf.Clamp01((time - .96f) / .29f);
            Vector2 point = Vector2.Lerp(origin, destination, 1 - Mathf.Pow(1 - progress, 3));
            point.y += Mathf.Sin(Mathf.PI * progress) * .5f;
            point = Vector2.Lerp(point, owner.Position + new Vector2(.49f * facing, .75f), returning * returning);
            int frame = Mathf.FloorToInt(Mathf.Max(0, time) * flying.Length / .245f) % flying.Length;
            Present(flying[frame], point, Mathf.Lerp(.8f, 1, progress * (1 - returning)), 1 - returning * .75f);
        }

        void Present(Sprite sprite, Vector2 point, float scale, float alpha)
        {
            view.enabled = sprite;
            view.sprite = sprite;
            view.flipX = facing < 0;
            transform.position = point;
            transform.localScale = Vector3.one * scale;
            view.color = new Color(1, 1, 1, alpha);
        }
    }
}
