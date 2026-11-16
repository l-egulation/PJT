namespace Deviant.Combat
{
    // 스킬 이펙트 그림을 현재 판정 크기에 맞추는 균일 배율(표시 전용, 2026-11-10 전투 밸런스 후속).
    // 각 그림은 아래 Baked* 크기의 판정에 맞춰 구워졌다. 지금 데이터의 판정 크기 ÷ 구울 때 크기로 그림을
    // 피벗 기준으로 키우므로, data/combat 의 판정이 다시 바뀌어도 그림이 판정을 따라간다. 판정·피해·시간은 바꾸지 않는다.
    // 원본 픽셀은 그대로이고 Unity 표시 배율만 바뀐다(정수가 아닌 배율은 픽셀 굵기가 고르지 않을 수 있다).
    public static class SkillFxHitScale
    {
        // 단검 먹안개 ink_smoke: 6×3 연막 영역(보이는 폭 6.00u)에 맞춰 구움.
        public const float SmokeBakedWidth = 6f;
        // 채찍 가시 그물 net_mark: 판정 폭 5.1875u(보이는 폭 5.19u)에 맞춰 구움.
        public const float NetMarkBakedWidth = 5.1875f;
        // 폭탄 기본·3연속 투척 폭발 basic, triple_0..2: 폭발 반경 1.5u(보이는 지름 약 3.1~3.3u)에 맞춰 구움.
        public const float BlastBakedRadius = 1.5f;
        // 수리검 star(관통·폭발·뒤이동 투척): 탄 반지름 0.4u(보이는 높이 0.81u)에 맞춰 구움.
        public const float StarBakedRadius = .4f;
        // 대검 반월참 fall: 판정 폭 8u(보이는 폭 7.94u)에 맞춰 구움.
        public const float FallBakedWidth = 8f;

        public static float Ratio(float size, float baked) => size > 0 && baked > 0 ? size / baked : 1;

        public static float DaggerSmoke(float smokeWidth) => Ratio(smokeWidth, SmokeBakedWidth);
        // 먹안개 뭉게 연막(ink_smoke_fog·_ink, 2026-11-11): 판정 폭 10u 에 맞춰 구웠다.
        public const float FogBakedWidth = 10f;
        public static float DaggerFog(float smokeWidth) => Ratio(smokeWidth, FogBakedWidth);
        public static float WhipNetMark(float hitWidth) => Ratio(hitWidth, NetMarkBakedWidth);
        public static float GreatswordFall(float hitWidth) => Ratio(hitWidth, FallBakedWidth);
        // 검 검기 wave(158×90 PPU64, 보이는 높이 약 1.4u): 판정 높이 2u 보다 작아 보여서(2026-11-13 사용자 피드백)
        // 판정 높이 1.25u 기준 그림으로 보고 키운다. 지금 판정 1×2 → 1.6배(보이는 높이 약 2.2u·길이 약 4u).
        public const float SwordWaveBakedHeight = 1.25f;
        public static float SwordWave(float hitHeight) => Ratio(hitHeight, SwordWaveBakedHeight);

        // 폭탄 폭발: 기본·3연속만 반경을 따른다. 대형·시한·미사일 폭발은 이미 판정과 맞아 그대로 둔다.
        public static float BombBlast(string clip, float radius)
            => clip == "basic" || clip != null && clip.StartsWith("triple_", System.StringComparison.Ordinal) ? Ratio(radius, BlastBakedRadius) : 1;

        // 수리검 탄: star 만 탄 반지름을 따른다(반지름 0.4 인 폭발·뒤이동 투척은 1배).
        public static float ShurikenShot(string clip, float radius) => clip == "star" ? Ratio(radius, StarBakedRadius) : 1;

        // 망치 여진 follow_up(갈라짐·바위 솟음): 발 앞 -1.09~3.69u 로 구웠다(앞 끝 3.6875u). 판정은 발 기준 양쪽(폭 8, 가운데 x 0 =
        // -4~+4)이라 그림이 앞쪽 절반에만 있었다(2026-11-13 사용자: 범위에 맞게 이펙트도 늘려야). 판정 앞 끝까지 발 기준 균일 확대하고,
        // 판정이 뒤로도 구운 범위보다 넓으면 좌우를 뒤집은 한 벌을 뒤쪽에 같이 그린다.
        public const float AftershockBakedFront = 3.6875f, AftershockBakedBack = 1.09375f;
        public static float HammerAftershock(float boxX, float boxWidth) => Ratio(boxX + boxWidth * .5f, AftershockBakedFront);
        public static bool HammerAftershockBehind(float boxX, float boxWidth)
            => boxWidth > 0 && boxX - boxWidth * .5f < -AftershockBakedBack * HammerAftershock(boxX, boxWidth) - .5f;
    }

    // 검 검기 wave-01~14 장 고르기. 01~12 는 날아가는 몸, 13·14 는 사라지는 장(초당 25장).
    // 비행이 0.5초보다 길어져도(14u, 0.875초) 사라지는 장이 도중에 나오지 않게 몸 12장을 되풀이하고 마지막 0.08초에만 13·14 를 쓴다.
    public static class SwordWaveFrames
    {
        public const int Body = 12, Fps = 25;
        public static int At(float age, float flight)
        {
            if (flight > 0 && age >= flight - .04f) return 14;
            if (flight > 0 && age >= flight - .08f) return 13;
            int step = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.FloorToInt(age * Fps) - 1);
            return 1 + step % Body;
        }
    }

    // 대검 반월참 소환 검의 시간표(타격 기준 초, 2026-11-11 확정 시안). GreatswordSkillVisual 이 그린다.
    public static class SummonedSword
    {
        // -0.30초에 판정 위쪽 선에 나타나 빛을 모으고, -0.10초부터 0.1초 만에 내리꽂혀 2.4u 박힌다.
        // 2026-11-13 사용자 확정 시안: 칼은 박힌 채 1.0초까지 있다가 2.2초까지(1.2초) 서서히 사라지고, 칼 몸통(본체+옆면)은 55% 불투명도로 비친다.
        // 땅 강타 연기는 1.0초부터 흐려지기 시작해 마지막 장을 붙든 채 2.2초에 사라진다(예전엔 연기가 진한 채 1.63초에 끊겼다).
        // 땅 갈라짐은 투명해지지 않는다: 땅 강타 CrackSource 번째 장의 땅 아래만 떼어 붙들고 있다가, ShrinkFrom 부터 ShrinkStep 마다
        // 한 장씩(ShrinkCrack) 칼 자리에서 먼 끝부터 오므라들어 CrackGone 에 없어진다.
        public const float Appear = -.30f, Drop = -.10f, FadeFrom = 1f, Gone = 2.2f, Buried = 2.4f, SlamFps = 30, Lean = 6f, Squash = .86f;
        public const float BladeOpacity = .55f, SlamFadeFrom = 1f, SlamGone = 2.2f;
        public const int CrackSource = 22, CrackSteps = 14;
        public const float ShrinkFrom = 1.4f, ShrinkStep = .1f, CrackGone = ShrinkFrom + CrackSteps * ShrinkStep;
        public static float CrackFrom => CrackSource / SlamFps;
        public static int CrackFrame(float t) => t < ShrinkFrom ? 0 : UnityEngine.Mathf.Min(CrackSteps, UnityEngine.Mathf.FloorToInt((t - ShrinkFrom) / ShrinkStep + 1e-4f));

        // 땅 강타 한 장에서 땅 아래(땅 선보다 2화소 아래부터)의 갈라짐만 떼어, step 번째(0 = 전체, CrackSteps = 빈 장) 줄어든 장을 만든다.
        // 칼 자리(피벗)에서 먼 화소부터 지운다. 갈라짐은 옆으로 길고 얕아 세로 거리를 2.5배로 잰다. 화소는 있거나 없거나(반투명 없음),
        // 줄어들기 시작한 둘째 장부터 금빛 반짝임은 먹색으로 식는다. 알파 8 미만(눈에 안 보이는 압축 잔티)은 갈라짐으로 치지 않는다.
        // 그러지 않으면 BC7 압축이 만든 잔티가 끝 거리를 두 배 가까이 늘려 줄어듦이 늦게 시작한다. 시안 outputs/greatsword-halfmoon-front-fade-20261113/_작업/crack_shrink.py 와 같은 계산.
        const byte Faint = 8;
        public static UnityEngine.Sprite ShrinkCrack(UnityEngine.Sprite slam, int step)
        {
            var tex = slam.texture; var rect = slam.rect; int w = (int)rect.width, h = (int)rect.height, tw = tex.width, ox = (int)rect.x, oy = (int)rect.y;
            var src = tex.GetPixels32(); var px = new UnityEngine.Color32[w * h];
            float cx = slam.pivot.x, ground = slam.pivot.y;                 // 아래에서 센 땅 화소 줄
            int top = UnityEngine.Mathf.RoundToInt(ground) - 3;              // 이 줄부터 아래만(시안: 위에서 166행 = 땅 164행 + 2)
            float far = 0;
            for (int y = 0; y <= top; y++) for (int x = 0; x < w; x++)
                if (src[(oy + y) * tw + ox + x].a >= Faint) far = UnityEngine.Mathf.Max(far, Distance(x, y));
            float r = far * UnityEngine.Mathf.Pow(1 - (float)step / CrackSteps, 1.15f);
            for (int y = 0; y <= top; y++) for (int x = 0; x < w; x++)
            {
                var c = src[(oy + y) * tw + ox + x]; if (c.a < Faint || Distance(x, y) > r) continue;
                if (step >= 2 && c.r > 150 && c.b < 140) c = new UnityEngine.Color32(27, 22, 22, c.a);
                px[y * w + x] = c;
            }
            var baked = new UnityEngine.Texture2D(w, h, UnityEngine.TextureFormat.RGBA32, false) { hideFlags = UnityEngine.HideFlags.DontSave, filterMode = tex.filterMode, wrapMode = UnityEngine.TextureWrapMode.Clamp };
            baked.SetPixels32(px); baked.Apply(false);
            return UnityEngine.Sprite.Create(baked, new UnityEngine.Rect(0, 0, w, h), new UnityEngine.Vector2(slam.pivot.x / w, slam.pivot.y / h), slam.pixelsPerUnit);
            float Distance(int x, int y) => UnityEngine.Mathf.Sqrt((x - cx) * (x - cx) + (ground - 1 - y) * (ground - 1 - y) * 6.25f);
        }

        // 칼 본체와 짙은 금색 옆면(뒤로 0.08u, 아래로 0.03u 민 복사본)을 한 장으로 굽는다. 둘을 따로 반투명하게 하면 옆면이 본체 밑으로
        // 비쳐 칼이 탁해지므로, 한 장을 만든 뒤 그 장 전체에 불투명도를 건다. 원래 피벗(아래 가운데 = 칼끝)을 그대로 둔다.
        public static UnityEngine.Sprite BakeBlade(UnityEngine.Sprite source, UnityEngine.Color32 gold)
        {
            var tex = source.texture; var rect = source.rect; int w = (int)rect.width, h = (int)rect.height;
            float ppu = source.pixelsPerUnit; int dx = UnityEngine.Mathf.RoundToInt(.08f * ppu), dy = UnityEngine.Mathf.RoundToInt(.03f * ppu);
            var src = tex.GetPixels32();
            int tw = tex.width, ox = (int)rect.x, oy = (int)rect.y, W = w + dx, H = h + dy;
            var px = new UnityEngine.Color32[W * H];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                var c = src[(oy + y) * tw + ox + x]; if (c.a == 0) continue;
                px[y * W + x + dx] = new UnityEngine.Color32((byte)(c.r * gold.r / 255), (byte)(c.g * gold.g / 255), (byte)(c.b * gold.b / 255), c.a);
            }
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                var c = src[(oy + y) * tw + ox + x]; if (c.a == 0) continue;
                int i = (y + dy) * W + x; var under = px[i]; float a = c.a / 255f, keep = 1 - a, outA = a + under.a / 255f * keep;
                px[i] = outA <= 0 ? default : new UnityEngine.Color32((byte)((c.r * a + under.r * under.a / 255f * keep) / outA), (byte)((c.g * a + under.g * under.a / 255f * keep) / outA),
                    (byte)((c.b * a + under.b * under.a / 255f * keep) / outA), (byte)UnityEngine.Mathf.RoundToInt(outA * 255));
            }
            var baked = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGBA32, false) { hideFlags = UnityEngine.HideFlags.DontSave, filterMode = tex.filterMode, wrapMode = UnityEngine.TextureWrapMode.Clamp };
            baked.SetPixels32(px); baked.Apply(false);
            var pivot = source.pivot;
            return UnityEngine.Sprite.Create(baked, new UnityEngine.Rect(0, 0, W, H), new UnityEngine.Vector2(pivot.x / W, (pivot.y + dy) / H), ppu);
        }

        // 칼끝 높이(땅 기준 u). top = 판정 위쪽 선 높이
        public static float Tip(float t, float top)
        {
            if (t < Drop) return top + .25f * UnityEngine.Mathf.Sin(UnityEngine.Mathf.Clamp01((t - Appear) / (Drop - Appear)) * UnityEngine.Mathf.PI);
            float u = UnityEngine.Mathf.Clamp01((t - Drop) / -Drop);
            return top + (-Buried - top) * u * u * u;
        }
    }
}
