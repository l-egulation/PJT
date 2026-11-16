using System;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class BombSkillFxTests
    {
        static CombatHitOrigin Origin(string action, int hit = 0, bool basic = false)
            => new CombatHitOrigin(default, default, Guid.NewGuid(), hit, null, Guid.Empty, CombatHitKind.Blast, actionId: action, isBasicAction: basic);

        [Test] public void EveryBombSkillPicksItsOwnApprovedBlast()
        {
            Assert.That(BombSkillFx.BlastClip(Origin("bomb_basic_0", 0, true)), Is.EqualTo("basic"));
            Assert.That(BombSkillFx.BlastClip(Origin("bomb_basic_special", 0)), Is.EqualTo("triple_0"));
            Assert.That(BombSkillFx.BlastClip(Origin("bomb_basic_special", 1)), Is.EqualTo("triple_1"), "second bomb: pink, thrown up");
            Assert.That(BombSkillFx.BlastClip(Origin("bomb_basic_special", 2)), Is.EqualTo("triple_2"), "third bomb: sky blue, wide");
            Assert.That(BombSkillFx.BlastClip(Origin("bomb_range_special")), Is.EqualTo("range"));
            Assert.That(BombSkillFx.BlastClip(Origin("bomb_followup_special")), Is.EqualTo("timed"));
            Assert.That(BombSkillFx.BlastClip(Origin("bomb_move_special")), Is.EqualTo("missile_hit"));
            Assert.That(BombSkillFx.BlastClip(Origin("shuriken_range_special")), Is.Null, "other weapons keep their own presentation");
            Assert.That(BombSkillFx.BlastClip(default), Is.Null, "enemy and scripted bombs keep the placeholder");
        }

        [Serializable] class ClipData { public string name, anchor; public bool facing, loop; public float segment, fps; public string[] frames; }
        [Serializable] class Catalog { public string format; public ClipData[] clips; }

        [Test] public void EveryClipLoadsAtGamePixelsWithItsPivotInside()
        {
            var text = Resources.Load<TextAsset>("BombSkillFx/clips");
            Assert.That(text, Is.Not.Null);
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            Assert.That(catalog.format, Is.EqualTo("deviant-bomb-skill-fx-v1"));
            var names = catalog.clips.Select(c => c.name).ToArray();
            Assert.That(names, Is.EquivalentTo(new[] { "basic", "triple_0", "triple_1", "triple_2", "range", "timed_fuse", "timed", "missile_launch", "missile_body", "missile_trail", "missile_hit", "timed_clock" }));
            foreach (var clip in catalog.clips)
            {
                Assert.That(clip.fps, Is.EqualTo(30), clip.name);
                Assert.That(clip.frames, Is.Not.Empty, clip.name);
                foreach (var path in clip.frames)
                {
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                    var pivot = sprite.pivot / sprite.rect.size;
                    Assert.That(pivot.x, Is.InRange(0f, 1f), path); Assert.That(pivot.y, Is.InRange(0f, 1f), path);
                }
            }
            Assert.That(catalog.clips.Single(c => c.name == "range").anchor, Is.EqualTo("ground"));
            Assert.That(catalog.clips.Single(c => c.name == "timed").anchor, Is.EqualTo("ground"));
            Assert.That(catalog.clips.Single(c => c.name == "missile_body").loop, Is.True);
            Assert.That(catalog.clips.Single(c => c.name == "missile_trail").segment, Is.EqualTo(2).Within(.0001f), "a 64px fire segment every 2 units");
            Assert.That(catalog.clips.Single(c => c.name == "timed_fuse").frames.Length / 30f, Is.EqualTo(1.5f).Within(.04f), "the heat-up spans the 1.5s fuse");
        }

        // 2026-11-10: the four bomb specials stop reusing the basic throw. Each plays its own Aether body motion
        // (640x360 cells, feet at (220,327), PPU 106.667) for the data-driven cast time, plus the missile emerge pose.
        [Test] public void BombSpecialsPlayTheirOwnBodyMotions()
        {
            var catalog = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var set = catalog.sets.Single(s => s.form == "bomb");
            var seconds = new System.Collections.Generic.Dictionary<string, float>
                { ["attack_triple_kick"] = .9f, ["attack_big_heave"] = .9f, ["attack_missile"] = .2f, ["attack_timed_toss"] = .6f, ["missile_emerge"] = .52f };
            foreach (var pair in seconds)
            {
                var clip = set.clips.Single(c => c.name == pair.Key);
                Assert.That(clip.frames.Length, Is.EqualTo(clip.seconds.Length), pair.Key);
                Assert.That(clip.seconds.Sum(), Is.EqualTo(pair.Value).Within(.001f), pair.Key);
                foreach (var path in clip.frames)
                {
                    Assert.That(path, Does.StartWith("CharacterBombSkills/"), pair.Key);
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(320f / 3).Within(.001f), path);
                    Assert.That((sprite.pivot - new Vector2(220, 33)).magnitude, Is.LessThan(.51f), path);
                }
            }
            // The game builds this registry on arena start and refuses to start on any unexpected binding.
            var combat = JsonUtility.FromJson<Deviant.Combat.Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var registry = new Deviant.Definitions.MotionSourceRegistry(catalog.motionRegistry, combat);
            foreach (var pair in new[] { ("bomb_basic_special", "attack_triple_kick"), ("bomb_range_special", "attack_big_heave"), ("bomb_move_special", "attack_missile"), ("bomb_followup_special", "attack_timed_toss") })
            {
                var binding = registry.Actions.Single(a => a.Key.ActionId == pair.Item1);
                Assert.That(binding.MotionId, Is.EqualTo("bomb/" + pair.Item2), pair.Item1);
                Assert.That(binding.IsFallback, Is.False, pair.Item1);
            }
            Assert.That(registry.Actions.Single(a => a.Key.ActionId == "bomb_basic_0").MotionId, Is.EqualTo("bomb/attack1"), "the basic throw stays");
            Assert.That(set.clips.Single(c => c.name == "attack_triple_kick").sourceImpact, Is.EqualTo(.2f).Within(.001f), "first kick at the first release");
            Assert.That(set.clips.Single(c => c.name == "attack_big_heave").sourceImpact, Is.EqualTo(.3f).Within(.001f));
        }

        [Test] public void TimedClockGoesOnceRoundOverTheFuse()
        {
            const float fuse = 1.5f;
            Assert.That(BombSkillFx.TimedClockFrames, Is.EqualTo(13), "pop-in frame + twelve hand steps");
            Assert.That(BombSkillFx.TimedClockFrame(0, fuse), Is.EqualTo(0), "pops in first");
            Assert.That(BombSkillFx.TimedClockFrame(.07f, fuse), Is.EqualTo(1));
            Assert.That(BombSkillFx.TimedClockFrame(.13f, fuse), Is.EqualTo(2), "one 30 degree step every fuse/12");
            Assert.That(BombSkillFx.TimedClockFrame(.76f, fuse), Is.EqualTo(7), "half the fuse: half way round");
            Assert.That(BombSkillFx.TimedClockFrame(1.49f, fuse), Is.EqualTo(12), "last step right before the blast");
            Assert.That(BombSkillFx.TimedClockFrame(9f, fuse), Is.EqualTo(12));
        }

        [Test] public void TripleKickBombsHangUntilEachKickAndEndAtTheSpawn()
        {
            Assert.That(BombSkillFx.TripleKickBombAt(0, .05f, out _), Is.False, "still in the hand before the toss");
            Assert.That(BombSkillFx.TripleKickBombAt(0, .14f, out var up), Is.True);
            Assert.That(up.y, Is.GreaterThan(1.2f), "tossed up in front");
            Assert.That(BombSkillFx.TripleKickBombAt(0, .1999f, out var kick), Is.True);
            Assert.That((kick - new Vector2(.5f, 1f)).magnitude, Is.LessThan(.02f), "meets the foot where the game spawns the bomb");
            Assert.That(BombSkillFx.TripleKickBombAt(0, .2f, out _), Is.False, "gone once kicked: the projectile takes over");
            Assert.That(BombSkillFx.TripleKickBombAt(2, .45f, out _), Is.True, "the third waits for the third kick");
            Assert.That(BombSkillFx.TripleKickBombAt(2, .5f, out _), Is.False);
        }

        [Test] public void TimedBombShakesHarderAsTheFuseRunsOut()
        {
            float Peak(float k) { float m = 0; for (int i = 0; i < 400; i++) m = Mathf.Max(m, Mathf.Abs(BombSkillFx.ShakePixels(k, i * .005f))); return m; }
            Assert.That(Peak(.1f), Is.LessThanOrEqualTo(1));
            Assert.That(Peak(.95f), Is.GreaterThanOrEqualTo(2));
        }
    }
}
