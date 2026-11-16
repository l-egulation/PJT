using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    // 채찍 스킬 몸 모션·표시(2026-11-07 확정) 연결 검사.
    public class WhipSkillMotionTests
    {
        static CharacterAnimationCatalog Media => JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);

        [TestCase("whip_default", "attack_wrap", .25f, .60f)]
        [TestCase("whip_range", "attack_net", .35f, .80f)]
        [TestCase("whip_movement", "attack_knot", .15f, .50f)]
        [TestCase("whip_follow_up", "attack_tighten", .25f, .60f)]
        public void EachSkillPlaysItsOwnMotionOnTheActionClock(string action, string clip, float impact, float end)
        {
            var media = Media;
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var registry = new MotionSourceRegistry(media.motionRegistry, combat);
            var binding = registry.Actions.Single(b => b.Key.ActionId == action);
            Assert.That(binding.RequestedClip, Is.EqualTo(clip));
            Assert.That(binding.MotionId, Is.EqualTo("whip/" + clip));
            Assert.That(MotionSourceRegistry.ExpectedBindings(combat, registry.Palettes).Single(b => b.Key.ActionId == action).RequestedClip, Is.EqualTo(clip));
            var timing = registry.Motion("whip/" + clip).Compile(impact, end);
            Assert.That(timing.End, Is.EqualTo(end).Within(.00001f));
            Assert.That(timing.Impact, Is.EqualTo(impact).Within(.00001f));
            var body = media.sets.Single(s => s.form == "whip").Find(clip);
            Assert.That(body.seconds.Sum(), Is.EqualTo(end).Within(.001f), "그림 시간표가 시전 끝과 같아 1:1 로 재생된다");
            foreach (string frame in body.frames)
            {
                var sprite = Resources.Load<Sprite>(frame);
                Assert.That(sprite, Is.Not.Null, frame);
                Assert.That(sprite.pixelsPerUnit, Is.GreaterThan(70).And.LessThan(110), frame);
            }
        }

        [Test]
        public void HoldReleaseAndPullClipsAreNonAttackClips()
        {
            var whip = Media.sets.Single(s => s.form == "whip");
            foreach (string name in new[] { "release_wrap", "release_net", "release_knot", "release_tighten", "knot_pull" })
            {
                var clip = whip.Find(name);
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.name.StartsWith("attack"), Is.False, "attack* 이 아니어야 팔레트 1:1 검사 밖에 둔다");
            }
        }

        [Test]
        public void LineSkillsEndOnAStrippedPullFrameThatHasLineData()
        {
            var whip = Media.sets.Single(s => s.form == "whip");
            foreach (string clip in new[] { "attack_wrap", "attack_knot", "attack_tighten" })
            {
                var last = Resources.Load<Sprite>(whip.Find(clip).frames.Last());
                Assert.That(WhipSkillFx.TryLine(last, out var line), Is.True, clip + " 땡기는 장은 채찍을 지운 판이고 줄 자리가 있다");
                Assert.That(line.Grip.x, Is.GreaterThan(.5f).And.LessThan(2f));
                Assert.That(line.Width, Is.EqualTo(.0625f).Within(.0001f));
            }
            Assert.That(WhipSkillFx.TryLine(Resources.Load<Sprite>(whip.Find("attack_net").frames.Last()), out _), Is.False, "가시 그물은 그림 그대로");
        }

        [TestCase("snap")] [TestCase("bind_small_front")] [TestCase("bind_large_back")] [TestCase("loosen_small_front")] [TestCase("thorn_large_front")]
        [TestCase("knot_mark")] [TestCase("knot_tighten")] [TestCase("net_mark")]
        public void EffectClipsLoad(string name)
        {
            var clip = WhipSkillFx.Find(name);
            Assert.That(clip, Is.Not.Null, name);
            Assert.That(clip.Sprites.All(s => s && Mathf.Approximately(s.pixelsPerUnit, 32)), Is.True);
        }

        // 2026-11-13 사용자 확정: 가시 그물은 몸 모션에 구운 작은 그물 대신 판정을 덮는 부채 그물을 그린다.
        [Test]
        public void ThornNetFanStartsWhereTheBodyDropsItsNetAndCoversTheHit()
        {
            var fan = WhipSkillFx.Find("thorn_net");
            Assert.That(fan, Is.Not.Null);
            Assert.That(fan.Timed, Is.True);
            Assert.That(fan.Step, Is.EqualTo(1 / 30f).Within(1e-5f));
            Assert.That(fan.Sprites.Length, Is.EqualTo(33));
            foreach (var sprite in fan.Sprites)
            {
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(88.22f).Within(.01f), "몸 모션과 같은 화소 밀도");
                Assert.That(sprite.pivot.x, Is.EqualTo(0).Within(.5f), "피벗 = 발(왼쪽 끝): 뒤집으면 뒤쪽으로 펼쳐진다");
            }
            float reach = fan.Sprites[0].bounds.max.x;
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var hit = combat.forms.Single(f => f.id == "whip").specials.Single(s => s.id == "whip_range").hits[0].box;
            Assert.That(reach, Is.GreaterThanOrEqualTo(hit.x + hit.width * .5f), "그림판이 판정 앞 끝까지 닿는다");
            // 부채가 나오는 시각 = 몸 모션에서 그물이 든 첫 장(15번, 그물을 뺀 판)이 시작하는 시각
            var net = Media.sets.Single(s => s.form == "whip").Find("attack_net");
            int first = System.Array.FindIndex(net.frames, f => f.EndsWith("/15"));
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(net.seconds.Take(first).Sum(), Is.EqualTo(WhipSkillFx.NetFanStart).Within(.001f));
        }

        [Test]
        public void BindFramesCoverInHoldOutAndTimedClips()
        {
            var bind = WhipSkillFx.Find("bind_small_front");
            Assert.That(WhipSkillFx.BindFrame(bind, 0, 1), Is.SameAs(bind.Sprites[0]));
            Assert.That(WhipSkillFx.BindFrame(bind, .5f, .5f), Is.SameAs(bind.Sprites[bind.In + (Mathf.FloorToInt((.5f - bind.In * bind.Step) / bind.Step) % bind.Hold)]));
            Assert.That(WhipSkillFx.BindFrame(bind, 1, .01f), Is.SameAs(bind.Sprites[bind.Sprites.Length - 1]));
            var loosen = WhipSkillFx.Find("loosen_small_front");
            Assert.That(WhipSkillFx.BindFrame(loosen, .41f, .79f), Is.SameAs(loosen.Sprites[10]));
            Assert.That(WhipSkillFx.BindSet("whip_default"), Is.EqualTo("loosen"));
            Assert.That(WhipSkillFx.BindSet("whip_range"), Is.EqualTo("thorn"));
            Assert.That(WhipSkillFx.BindSet("whip_follow_up"), Is.EqualTo("bind"));
        }
    }
}
