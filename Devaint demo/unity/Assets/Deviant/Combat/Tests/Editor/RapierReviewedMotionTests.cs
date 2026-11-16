using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class RapierReviewedMotionTests
    {
        // User-selected basic outline frames; special motions retain their approved media.
        [TestCase("attack1", .10f, .22f, 9)]
        [TestCase("attack2", .10f, .22f, 7)]
        [TestCase("attack3", .12f, .36f, 8)]
        [TestCase("attack_charge", .80f, 1.13f, 16)]
        [TestCase("attack_long_thrust", .30f, .70f, 8)]
        [TestCase("attack_held_thrust", .10f, .45f, 11)]
        [TestCase("attack_flurry", .15f, .95f, 19)]
        public void ReviewedBodyLoadsAtApprovedWorldScaleAndPreservesActionClock(string clip, float impact, float end, int count)
        {
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var registry = new MotionSourceRegistry(media.motionRegistry, combat);
            var body = media.sets.Single(s => s.form == "rapier").Find(clip);
            Assert.That(body.frames.Length, Is.EqualTo(count));
            var timing = registry.Motion("rapier/" + clip).Compile(impact, end);
            Assert.That(timing.End, Is.EqualTo(end).Within(.00001f));
            Assert.That(timing.Impact, Is.EqualTo(impact).Within(.00001f));
            foreach (string frame in body.frames) {
                var sprite = Resources.Load<Sprite>(frame);
                Assert.That(sprite, Is.Not.Null, frame);
                bool basic = clip == "attack1" || clip == "attack2" || clip == "attack3";
                Assert.That(frame, Does.StartWith(basic ? "CharacterBasicOutline/rapier/" : "CharacterRapierMotion/"));
                // The 70px basic ready pose uses PPU35 to match the 64px idle at PPU32.
                float ppu = basic ? 35 : 320f / 3;
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(ppu).Within(.01f), frame);
                var pivot = basic ? new Vector2(72f / 35, 16f / 35) : new Vector2(2.0625f, .3125f);
                Assert.That(Vector2.Distance(sprite.pivot / sprite.pixelsPerUnit, pivot), Is.LessThan(.002f), frame);
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), frame);
            }
        }
    }
}
