using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    // 2026-11-09 레이피어 이펙트: 평타는 세트 효과 G(RapierSkillFx/thrust_*, PPU64), 특수 넷은 RapierSkillFx/clips.json(PPU32).
    public class RapierSkillFxTests
    {
        [Serializable] class ClipData { public string name, skill, anchor, layer; public float startTime, fps; public string[] frames; }
        [Serializable] class Catalog { public string format; public ClipData[] clips; }

        [Test, Category("RequiresArt")] public void EverySpecialHasApprovedClipsAtGamePixels()
        {
            var text = Resources.Load<TextAsset>("RapierSkillFx/clips");
            Assert.That(text, Is.Not.Null);
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            Assert.That(catalog.format, Is.EqualTo("deviant-rapier-skill-fx-v1"));
            foreach (var skill in new[] { "rapier_default", "rapier_range", "rapier_movement", "rapier_follow_up" })
                Assert.That(catalog.clips.Any(c => c.skill == skill), Is.True, skill);
            foreach (var clip in catalog.clips)
            {
                Assert.That(clip.anchor, Is.EqualTo("character").Or.EqualTo("cast_start"), clip.name);
                Assert.That(clip.fps, Is.EqualTo(50), clip.name);
                foreach (var path in clip.frames)
                {
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                }
            }
            var slip = catalog.clips.Where(c => c.skill == "rapier_movement").ToArray();
            Assert.That(slip.Select(c => c.anchor), Is.EquivalentTo(new[] { "cast_start", "character" }), "slip: one where the body left, one where it arrived");
            Assert.That(slip.Single(c => c.anchor == "character").startTime, Is.GreaterThanOrEqualTo(.1f), "the arrival plays after the 0.10s teleport");
            Assert.That(catalog.clips.Single(c => c.skill == "rapier_default").startTime, Is.EqualTo(.2f).Within(.001f), "the gather starts at the minimum charge");
        }

        [Test, Category("RequiresArt")] public void BasicThrustsUseEffectGAtTheirFxStart()
        {
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var set = media.sets.Single(s => s.form == "rapier");
            foreach (var attack in new[] { "attack1", "attack2", "attack3" })
            {
                var effect = set.effects.Single(e => e.attack == attack);
                Assert.That(effect.frames.All(f => f.StartsWith(attack == "attack3" ? "RapierSkillFx/thrust_hit3/" : "RapierSkillFx/thrust_hit1/")), Is.True, attack);
                Assert.That(effect.x.All(v => v == 0) && effect.y.All(v => v == 0), Is.True, "frames are baked at the foot origin");
                foreach (var path in effect.frames) Assert.That(Resources.Load<Sprite>(path).pixelsPerUnit, Is.EqualTo(64), path);
                Assert.That(set.Find(attack).fxStart, Is.EqualTo(set.Find(attack).sourceImpact - .03f).Within(.0001f), attack);
            }
        }
    }
}
