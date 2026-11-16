using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class ShurikenSkillFxTests
    {
        [Test] public void EveryShurikenShotFliesAsTheApprovedStar()
        {
            Assert.That(ShurikenSkillFx.ShotClip("shuriken"), Is.EqualTo("star_basic"), "basic throws use the small star");
            Assert.That(ShurikenSkillFx.ShotClip("shuriken_special"), Is.EqualTo("star"));
            Assert.That(ShurikenSkillFx.ShotClip("shuriken_range"), Is.EqualTo("star"));
            Assert.That(ShurikenSkillFx.ShotClip("shuriken_move"), Is.EqualTo("star"), "every flying star is the same b look");
            Assert.That(ShurikenSkillFx.ShotClip("shuriken_followup"), Is.EqualTo("roll"));
            Assert.That(ShurikenSkillFx.ShotClip("ball"), Is.Null, "other projectiles keep their own art");
        }

        [Serializable] class ClipData { public string name; public bool loop; public float fps; public string[] frames; }
        [Serializable] class Catalog { public string format; public ClipData[] clips; }

        [Test] public void EveryEffectClipLoadsAtGamePixelsAroundItsCentre()
        {
            var text = Resources.Load<TextAsset>("ShurikenSkillFx/clips");
            Assert.That(text, Is.Not.Null);
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            Assert.That(catalog.format, Is.EqualTo("deviant-shuriken-skill-fx-v1"));
            Assert.That(catalog.clips.Select(c => c.name), Is.EquivalentTo(new[] { "star", "star_basic", "sweep", "blast", "step_out", "step_in", "roll", "star_fast", "star_fast_sparks" }));
            foreach (var clip in catalog.clips)
            {
                Assert.That(clip.fps, Is.EqualTo(50), clip.name);
                foreach (var path in clip.frames)
                {
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(sprite.pivot / sprite.rect.size, Is.EqualTo(new Vector2(.5f, .5f)), path);
                }
            }
            Assert.That(catalog.clips.Where(c => c.loop).Select(c => c.name), Is.EquivalentTo(new[] { "star", "star_basic", "roll", "star_fast", "star_fast_sparks" }), "flying stars spin for as long as they fly");
        }

        [Test] public void ShurikenBodyMotionsStandOnTheSameFeet()
        {
            var catalog = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var set = catalog.sets.Single(s => s.form == "shuriken");
            foreach (var name in new[] { "attack1", "attack1_chain", "attack2", "recover1", "recover2", "attack_pierce", "attack_explosive", "attack_step_throw", "attack_roll", "arrive_behind" })
            {
                var clip = set.clips.Single(c => c.name == name);
                Assert.That(clip.frames.Length, Is.EqualTo(clip.seconds.Length), name);
                foreach (var path in clip.frames)
                {
                    Assert.That(path, Does.StartWith("CharacterShurikenSkills/"), name);
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(320f / 3).Within(.001f), path);
                    Assert.That(sprite.pivot, Is.EqualTo(new Vector2(220, 33)).Using(new Vec2Near()), path);
                }
            }
            Assert.That(set.clips.Single(c => c.name == "arrive_behind").seconds.Sum(), Is.EqualTo(.4f).Within(.001f));
        }

        class Vec2Near : System.Collections.Generic.IEqualityComparer<Vector2>
        {
            public bool Equals(Vector2 a, Vector2 b) => (a - b).magnitude < .51f;
            public int GetHashCode(Vector2 value) => 0;
        }
    }
}
