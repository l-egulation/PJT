using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    // 2026-11-10 전투 밸런스 후속: 판정이 커진 스킬의 이펙트 그림이 지금 판정 크기를 따라 커지는지 확인한다.
    public class SkillFxHitScaleTests
    {
        static Catalog Source() => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        static SkillData Special(Catalog c, string form, string id) => c.forms.Single(f => f.id == form).specials.Single(s => s.id == id);

        [Test] public void BakedSizesKeepTheirOldScaleOfOne()
        {
            Assert.That(SkillFxHitScale.DaggerSmoke(6), Is.EqualTo(1).Within(1e-5f));
            Assert.That(SkillFxHitScale.WhipNetMark(5.1875f), Is.EqualTo(1).Within(1e-5f));
            Assert.That(SkillFxHitScale.BombBlast("basic", 1.5f), Is.EqualTo(1).Within(1e-5f));
            Assert.That(SkillFxHitScale.ShurikenShot("star", .4f), Is.EqualTo(1).Within(1e-5f));
            Assert.That(SkillFxHitScale.GreatswordFall(8), Is.EqualTo(1).Within(1e-5f));
            Assert.That(SkillFxHitScale.DaggerSmoke(0), Is.EqualTo(1), "no size from data: the baked size");
        }

        // 2026-11-13 사용자: 망치 여진은 판정(발 기준 양쪽 -4~+4)에 맞게 이펙트도 늘려야 한다.
        [Test] public void HammerAftershockSpansItsHitOnBothSides()
        {
            Assert.That(SkillFxHitScale.HammerAftershock(SkillFxHitScale.AftershockBakedFront * .5f, SkillFxHitScale.AftershockBakedFront), Is.EqualTo(1).Within(1e-5f), "baked reach keeps 1x");
            Assert.That(SkillFxHitScale.HammerAftershockBehind(1.5f, 3.5f), Is.False, "a hit only in front needs no copy behind");
            var hit = Special(Source(), "hammer", "hammer_follow_up").hits[0].box;
            float scale = SkillFxHitScale.HammerAftershock(hit.x, hit.width);
            Assert.That(SkillFxHitScale.AftershockBakedFront * scale, Is.EqualTo(hit.x + hit.width * .5f).Within(1e-4f), "front edge of the rocks meets the front edge of the hit");
            Assert.That(SkillFxHitScale.HammerAftershockBehind(hit.x, hit.width), Is.True, "the 8 wide hit centred on the feet also needs the mirrored copy behind");
            Assert.That(-SkillFxHitScale.AftershockBakedFront * scale, Is.EqualTo(hit.x - hit.width * .5f).Within(1e-4f), "and the mirrored copy reaches the back edge");
        }

        [Test] public void OnlyTheResizedClipsFollowTheirHitSize()
        {
            Assert.That(SkillFxHitScale.BombBlast("triple_2", 3), Is.EqualTo(2).Within(1e-5f));
            Assert.That(SkillFxHitScale.BombBlast("range", 4.5f), Is.EqualTo(1), "the large bomb blast already fits its 4.5 radius");
            Assert.That(SkillFxHitScale.BombBlast("timed", 3), Is.EqualTo(1));
            Assert.That(SkillFxHitScale.BombBlast(null, 3), Is.EqualTo(1));
            Assert.That(SkillFxHitScale.ShurikenShot("star_basic", .2f), Is.EqualTo(1));
            Assert.That(SkillFxHitScale.ShurikenShot("roll", 1.5f), Is.EqualTo(1));
        }

        // 지금 판정 데이터(MeleeCatalog, data/combat 에서 생성)로 계산한 배율이 측정한 그림 크기 차이와 같은지.
        [Test] public void CurrentCatalogScalesTheResizedSkillPictures()
        {
            var c = Source();
            Assert.That(SkillFxHitScale.DaggerSmoke(Special(c, "dagger", "dagger_range").effect.box.width), Is.EqualTo(10 / 6f).Within(1e-4f), "smoke 10x5: curtain 6.00 -> 10.00 wide");
            Assert.That(SkillFxHitScale.WhipNetMark(Special(c, "whip", "whip_range").hits[0].box.width), Is.EqualTo(10.53125f / 5.1875f).Within(1e-4f), "net mark spans the 10.53 hit");
            Assert.That(SkillFxHitScale.GreatswordFall(Special(c, "greatsword", "greatsword_range").hits[0].box.width), Is.EqualTo(1.25f).Within(1e-4f), "half-moon cut 10 wide");
            Assert.That(SkillFxHitScale.BombBlast("basic", c.forms.Single(f => f.id == "bomb").basics[0].bomb.blastRadius), Is.EqualTo(2).Within(1e-4f), "basic bomb radius 3");
            Assert.That(SkillFxHitScale.BombBlast("triple_0", Special(c, "bomb", "bomb_basic_special").bomb.blastRadius), Is.EqualTo(2).Within(1e-4f), "triple throw radius 3");
            Assert.That(SkillFxHitScale.ShurikenShot("star", Special(c, "shuriken", "shuriken_basic_special").ranged.radius), Is.EqualTo(2.5f).Within(1e-4f), "big pierce star diameter 2");
            Assert.That(SkillFxHitScale.ShurikenShot("star", Special(c, "shuriken", "shuriken_range_special").ranged.radius), Is.EqualTo(1).Within(1e-4f), "explosive star keeps 0.8");
        }

        // 2026-11-13 사용자: 검기 이펙트가 너무 작다 → 판정 높이 2u 에 맞춰 1.6배, 이동 거리는 8 → 14u.
        [Test] public void SwordWaveDrawsTallerThanItsTwoUnitHitAndTravelsFourteen()
        {
            Assert.That(SkillFxHitScale.SwordWave(SkillFxHitScale.SwordWaveBakedHeight), Is.EqualTo(1).Within(1e-5f));
            var wave = Special(Source(), "base_sword", "base_sword_range");
            Assert.That(SkillFxHitScale.SwordWave(wave.hits[0].box.height), Is.EqualTo(1.6f).Within(1e-4f), "1x2 wave hit -> 1.6x picture");
            Assert.That(wave.effect.distance, Is.EqualTo(14).Within(1e-4f));
            Assert.That(wave.effect.distance / wave.effect.speed, Is.EqualTo(wave.hits[0].active).Within(1e-4f), "the hit lasts exactly as long as the flight");
        }

        [Test] public void SwordWaveKeepsItsBodyFramesForTheWholeLongerFlight()
        {
            const float flight = 14 / 16f;
            Assert.That(SwordWaveFrames.At(0, flight), Is.EqualTo(1), "starts like before");
            Assert.That(SwordWaveFrames.At(.45f, flight), Is.EqualTo(11), "the old 0.5s timing is unchanged mid-flight");
            for (float age = 0; age < flight - .08f; age += .01f)
                Assert.That(SwordWaveFrames.At(age, flight), Is.InRange(1, 12), $"no fading frame mid-flight at {age:0.00}s");
            Assert.That(SwordWaveFrames.At(flight - .06f, flight), Is.EqualTo(13));
            Assert.That(SwordWaveFrames.At(flight - .01f, flight), Is.EqualTo(14));
        }
    
        // 2026-11-11 확정 시안: 먹안개 뭉게 연막·소환 검 낙하·큰 관통 수리검 스파크 규칙
        [Test] public void FogSmokeIsBakedAtTheTenUnitSmokeBox()
        {
            Assert.That(SkillFxHitScale.DaggerFog(Special(Source(), "dagger", "dagger_range").effect.box.width), Is.EqualTo(1).Within(1e-4f));
        }

        [Test] public void SmokeToneFollowsHowBrightTheRoomIs()
        {
            var paper = Enumerable.Repeat(new Color32(245, 234, 214, 255), 144).ToArray();   // 1스테이지 종이 배경
            var waterway = Enumerable.Repeat(new Color32(36, 46, 52, 255), 144).ToArray();   // 수로
            Assert.That(DaggerSkillFx.Luminance(paper), Is.GreaterThan(DaggerSkillFx.BrightBackdrop), "paper room gets the ink smoke");
            Assert.That(DaggerSkillFx.Luminance(waterway), Is.LessThan(DaggerSkillFx.BrightBackdrop), "dark room gets the bright smoke");
        }

        [Test] public void SummonedSwordStartsAtTheHitTopAndEndsBuried()
        {
            float top = 10;
            Assert.That(SummonedSword.Tip(SummonedSword.Appear, top), Is.EqualTo(top).Within(1e-4f));
            Assert.That(SummonedSword.Tip(SummonedSword.Drop, top), Is.EqualTo(top).Within(1e-3f));
            Assert.That(SummonedSword.Tip(0, top), Is.EqualTo(-SummonedSword.Buried).Within(1e-4f), "lands on the impact time");
            Assert.That(SummonedSword.Tip(-.05f, top), Is.GreaterThan(5), "most of the drop happens in the last moment");
        }

        // 2026-11-13 사용자 확정: 칼은 박힌 채 1초를 버틴 뒤 1.2초에 걸쳐 서서히 사라지고 55%로 비치며, 땅 강타 연기는 마지막 장에서 끊기지 않고 흐려진다.
        [Test] public void SummonedSwordAndSlamFadeSlowly()
        {
            Assert.That(SummonedSword.FadeFrom, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(SummonedSword.Gone - SummonedSword.FadeFrom, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(SummonedSword.BladeOpacity, Is.EqualTo(.55f).Within(1e-4f));
            Assert.That(SummonedSword.SlamGone, Is.GreaterThan(49 / SummonedSword.SlamFps), "the dust outlives the 49 baked slam frames");
            Assert.That(SummonedSword.SlamFadeFrom, Is.LessThan(49 / SummonedSword.SlamFps), "and starts thinning before the last frame");
        }

        [Test] public void SummonedBladeAndItsGoldSideBakeIntoOneSpriteOnTheSameTip()
        {
            var source = Resources.Load<Sprite>("CharacterGreatswordReviewed/summoned-sword");
            Assert.That(source, Is.Not.Null); Assert.That(source.texture.isReadable, Is.True, "the blade is baked from its pixels");
            var gold = new Color32(0xb8, 0x78, 0x0e, 255);
            var baked = SummonedSword.BakeBlade(source, gold);
            Assert.That(baked.pixelsPerUnit, Is.EqualTo(source.pixelsPerUnit));
            Assert.That(baked.rect.size, Is.EqualTo(source.rect.size + new Vector2(5, 2)), "0.08u back and 0.03u down at PPU64");
            Assert.That(baked.pivot, Is.EqualTo(source.pivot + new Vector2(0, 2)), "the tip stays the pivot");
            var px = baked.texture.GetPixels32(); int w = (int)baked.rect.width, h = (int)baked.rect.height;
            int right = Enumerable.Range(0, h).Select(y => Enumerable.Range(0, w).Last(x => px[y * w + x].a > 0 || x == 0)).Max();
            var edge = Enumerable.Range(0, h).Select(y => px[y * w + right]).First(c => c.a > 0);
            Assert.That(edge.r, Is.LessThanOrEqualTo(gold.r), "the edge poking out to the right is the gold side");
            Assert.That(edge.b, Is.LessThanOrEqualTo(gold.b));
            Object.DestroyImmediate(baked.texture); Object.DestroyImmediate(baked);
        }

        // 2026-11-13 사용자: 갈라짐은 투명해지지 말고 스프라이트 장으로 점점 작아지게. 칼 자리에서 먼 끝부터 오므라들어 빈 장으로 끝난다.
        [Test] public void GroundCrackShrinksTowardTheSwordFrameByFrame()
        {
            var slam = Resources.Load<Sprite>("CharacterGreatswordReviewed/slam/" + SummonedSword.CrackSource.ToString("00"));
            Assert.That(slam, Is.Not.Null); Assert.That(slam.texture.isReadable, Is.True, "the crack is cut from the slam pixels");
            int ground = Mathf.RoundToInt(slam.pivot.y), previous = int.MaxValue; float reach = float.MaxValue;
            var counts = new System.Collections.Generic.List<int>();
            for (int step = 0; step <= SummonedSword.CrackSteps; step++)
            {
                var crack = SummonedSword.ShrinkCrack(slam, step);
                Assert.That((crack.pivot - slam.pivot).magnitude, Is.LessThan(1e-3f), "the crack sits on the same ground point");
                Assert.That(crack.pixelsPerUnit, Is.EqualTo(slam.pixelsPerUnit));
                var px = crack.texture.GetPixels32(); int w = crack.texture.width, count = 0; float far = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a == 0) continue;
                    count++; int x = i % w, y = i / w;
                    Assert.That(y, Is.LessThan(ground - 2), "only below the ground line");
                    Assert.That(px[i].a, Is.EqualTo(slam.texture.GetPixel((int)slam.rect.x + x, (int)slam.rect.y + y).a * 255).Within(1), "never faded, the pixel is kept or removed");
                    far = Mathf.Max(far, Mathf.Abs(x - slam.pivot.x));
                }
                Assert.That(count, Is.LessThanOrEqualTo(previous)); Assert.That(far, Is.LessThanOrEqualTo(reach), "the far ends go first");
                previous = count; reach = count > 0 ? far : 0; counts.Add(count);
                Object.DestroyImmediate(crack.texture); Object.DestroyImmediate(crack);
            }
            TestContext.WriteLine("crack pixels " + string.Join(",", counts));
            Assert.That(counts[0], Is.GreaterThan(400), "the first frame is the whole crack (about 470 visible pixels)");
            Assert.That(counts[SummonedSword.CrackSteps / 2], Is.LessThan(counts[0] * .7f), "and it is clearly smaller halfway");
            Assert.That(counts[SummonedSword.CrackSteps], Is.EqualTo(0), "and the last is empty");
            Assert.That(SummonedSword.CrackFrame(SummonedSword.CrackFrom), Is.EqualTo(0));
            Assert.That(SummonedSword.CrackFrame(SummonedSword.ShrinkFrom + .25f), Is.EqualTo(2));
            Assert.That(SummonedSword.CrackGone, Is.GreaterThan(SummonedSword.SlamGone), "the crack outlives the dust");
        }

        [Test] public void ShurikenSparksOnlyNearTheGround()
        {
            Assert.That(ShurikenSkillFx.SparksTouchGround(1, 1, 0), Is.True, "thrown on the ground");
            Assert.That(ShurikenSkillFx.SparksTouchGround(3, 1, 0), Is.False, "thrown in the air");
            Assert.That(ShurikenSkillFx.SparksTouchGround(1, 1, null), Is.False, "no floor below");
        }
}
}
