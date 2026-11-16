using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class HammerSkillFxTests
    {
        static MotionSourceRegistry Registry()
        {
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            return new MotionSourceRegistry(media.motionRegistry, combat);
        }

        [Test] public void FollowUpSlamsOnceAndRangeHoldsTheRaisedPoseBeforeTheSameSlam()
        {
            var registry = Registry();
            Assert.That(registry.Find(new MotionActionKey("hammer_follow_up", MotionActionKind.Special)).MotionId, Is.EqualTo("hammer/attack_default_slam"));
            Assert.That(registry.Find(new MotionActionKey("hammer_range", MotionActionKind.Special)).MotionId, Is.EqualTo("hammer/attack_range_slam"));
            Assert.That(registry.Find(new MotionActionKey("hammer_basic_0", MotionActionKind.Basic)).MotionId, Is.EqualTo("hammer/attack1"));
            var motion = registry.Motion("hammer/attack_range_slam");
            Assert.That(motion.Frames.SequenceEqual(registry.Motion("hammer/attack_default_slam").Frames), "same approved slam art");
            var timing = motion.Compile(.5f, 1.1f);
            Assert.That(timing.Impact, Is.EqualTo(.5f).Within(.00001f));
            Assert.That(timing.End, Is.EqualTo(1.1f).Within(.00001f));
            Assert.That(timing.FrameAt(.16f), Is.EqualTo(2), "raised pose starts");
            Assert.That(timing.FrameAt(.35f), Is.EqualTo(2), "raised pose is held 0.1s longer to gather");
            Assert.That(timing.FrameAt(.5f), Is.EqualTo(7), "hammer meets the ground on the range hit");
        }

        [Serializable] class ClipData { public string skill, layer, anchor; public float nominalImpact, startTime, fps; public string[] frames; }
        [Serializable] class ScreenData { public string skill; public float invertFrom, invertTo, darkenFrom, darkenTo, darkenAlpha; }
        [Serializable] class FxCatalog { public string format; public ClipData[] clips; public ScreenData[] screens; }

        [Test] public void EveryHammerSkillHasLoadablePixelFramesOnTheFootPivotScale()
        {
            var text = Resources.Load<TextAsset>("HammerSkillFx/clips");
            Assert.That(text, Is.Not.Null);
            var catalog = JsonUtility.FromJson<FxCatalog>(text.text);
            Assert.That(catalog.format, Is.EqualTo("deviant-hammer-skill-fx-v1"));
            foreach (var skill in new[] { "hammer_default", "hammer_movement", "hammer_follow_up", "hammer_range" })
                Assert.That(catalog.clips.Any(clip => clip.skill == skill && clip.layer == "front"), skill);
            foreach (var clip in catalog.clips)
            {
                Assert.That(clip.fps, Is.EqualTo(30), clip.skill);
                Assert.That(clip.frames.Length, Is.GreaterThan(0), clip.skill);
                foreach (var path in clip.frames)
                {
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(64), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                }
            }
            var follow = catalog.clips.Where(clip => clip.skill == "hammer_follow_up").Max(clip => clip.startTime + clip.frames.Length / clip.fps);
            Assert.That(follow, Is.GreaterThan(1.3f), "the last aftershock is drawn");
            var range = catalog.screens.Single(value => value.skill == "hammer_range");
            Assert.That(range.invertFrom, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(range.darkenFrom, Is.EqualTo(range.invertTo).Within(.0001f));
            Assert.That(Resources.Load<Shader>("CombatScreenInvert"), Is.Not.Null);
        }
    }
}
