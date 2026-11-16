using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class DaggerSkillFxTests
    {
        [Test] public void EveryDaggerSpecialPlaysItsApprovedEffect()
        {
            Assert.That(DaggerSkillFx.ClipFor("dagger_default"), Is.EqualTo("hold_breath"));
            Assert.That(DaggerSkillFx.ClipFor("dagger_range"), Is.EqualTo("ink_smoke"));
            Assert.That(DaggerSkillFx.ClipFor("dagger_movement"), Is.EqualTo("triple_leap"));
            Assert.That(DaggerSkillFx.ClipFor("dagger_follow_up"), Is.EqualTo("flurry"));
            Assert.That(DaggerSkillFx.ClipFor("dagger_basic_0"), Is.Null, "basic hits keep the existing slash effects");
        }

        [Test] public void HoldBreathInksTheBodyThenFadesIntoStealth()
        {
            Assert.That(DaggerSkillFx.BodyInk(.05f), Is.Zero);
            Assert.That(DaggerSkillFx.BodyInk(.10f), Is.EqualTo(1));
            Assert.That(DaggerSkillFx.BodyInk(.19f), Is.EqualTo(.5f).Within(.001f));
            Assert.That(DaggerSkillFx.BodyInk(.26f), Is.Zero);
        }

        [Serializable] class ClipData { public string name, anchor; public bool loop; public float fps; public string[] frames; }
        [Serializable] class Catalog { public string format; public ClipData[] clips; }

        [Test] public void EveryEffectClipLoadsAtGamePixels()
        {
            var text = Resources.Load<TextAsset>("DaggerSkillFx/clips");
            Assert.That(text, Is.Not.Null);
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            Assert.That(catalog.format, Is.EqualTo("deviant-dagger-skill-fx-v1"));
            Assert.That(catalog.clips.Select(c => c.name), Is.EquivalentTo(new[] { "hold_breath", "ink_smoke", "triple_leap", "flurry", "ink_smoke_fog", "ink_smoke_fog_ink" }));
            foreach (var clip in catalog.clips)
                foreach (var path in clip.frames)
                {
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                }
            var smoke = catalog.clips.Single(c => c.name == "ink_smoke");
            Assert.That(smoke.frames.Length / smoke.fps, Is.GreaterThanOrEqualTo(4f), "the curtain lasts as long as the 4 s smoke area");
            Assert.That(catalog.clips.Single(c => c.name == "flurry").anchor, Is.EqualTo("center"));
        }

        [Test] public void DaggerMotionsMatchTheirActionTimings()
        {
            var catalog = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var set = catalog.sets.Single(s => s.form == "dagger");
            // clip, cast end, activation (first hit) from the dagger definitions.
            var expected = new (string clip, float end, float impact)[]
            {
                ("attack1", .165f, .045f), ("attack2", .165f, .045f), ("attack3", .26f, .10f), ("attack4", .41f, .255f),
                ("attack_hold_breath", .35f, .10f), ("attack_ink_smoke", .50f, .15f), ("attack_triple_leap", .38f, .08f), ("attack_flurry", .95f, .15f),
            };
            foreach (var (name, end, impact) in expected)
            {
                var clip = set.clips.Single(c => c.name == name);
                Assert.That(clip.seconds.Sum(), Is.EqualTo(end).Within(.001f), name);
                Assert.That(clip.sourceImpact, Is.EqualTo(impact).Within(.001f), name);
                foreach (var path in clip.frames)
                {
                    Assert.That(path, Does.StartWith("CharacterDaggerSkills/"), name);
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(64), path);
                    Assert.That(sprite.pivot.y, Is.EqualTo(56).Within(.51f), path + " stands on the 304px foot line");
                }
            }
        }
    }
}
