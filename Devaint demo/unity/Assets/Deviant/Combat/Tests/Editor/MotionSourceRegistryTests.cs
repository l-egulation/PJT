using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class MotionSourceRegistryTests
    {
        static Catalog Combat() => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        static CharacterAnimationCatalog Media() => JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
        static MotionRegistryDocument Source() => Media().motionRegistry;
        [Test] public void RegistryCoversEveryExecutableActionAndEveryConversionWithoutInventedMedia()
        {
            var combat = Combat(); var source = Source(); var registry = new MotionSourceRegistry(source, combat);
            Assert.That(registry.Motions.Count(value => value.IsDelegated), Is.EqualTo(1));
            Assert.That(registry.Motion(FixedAttackMotion.FirstSwordId).DelegatedSource, Is.EqualTo(FixedBodyMotion.FirstSourcePath));
            var expected = MotionSourceRegistry.ExpectedBindings(combat, registry.Palettes);
            Assert.That(registry.Actions.Select(value => value.Key), Is.EquivalentTo(expected.Select(value => value.Key)));
            foreach (var binding in registry.Actions)
            {
                Assert.That(registry.Motion(binding.MotionId), Is.Not.Null);
                Assert.That(binding.IsFallback, Is.EqualTo(binding.MotionId != binding.RequestedForm + "/" + binding.RequestedClip));
            }
            Assert.That(registry.Actions.Where(value => value.Key.Kind == MotionActionKind.Conversion).Select(value => value.Key.Variant), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            foreach (var form in combat.forms)
                foreach (var skill in form.specials.Where(value => value.unavailable)) Assert.That(registry.Find(new MotionActionKey(skill.id, MotionActionKind.Special)), Is.Null);
        }
        [Test] public void StrongSlashUsesDedicatedMediaWithoutChangingBasicSwordCombo()
        {
            var registry = new MotionSourceRegistry(Source(), Combat());
            var skill = registry.Find(new MotionActionKey("base_sword_default", MotionActionKind.Special));
            Assert.That(skill.MotionId, Is.EqualTo("base_sword/attack_strong_slash"));
            Assert.That(skill.IsFallback, Is.False);
            var motion = registry.Motion(skill.MotionId);
            Assert.That(motion.Frames.Count, Is.EqualTo(12));
            var timing = motion.Compile(.155f, .37f);
            Assert.That(motion.Frames[timing.FrameAt(.10f)], Does.EndWith("default-02"), "hold the lowered preparation pose");
            Assert.That(motion.Frames[timing.FrameAt(.155f)], Does.EndWith("default-05"), "the slash meets the existing damage time");
            Assert.That(motion.Frames[timing.FrameAt(.18f)], Does.EndWith("default-06"), "hold the extended blade after contact");
            Assert.That(motion.Frames[timing.FrameAt(.36f)], Does.EndWith("default-11"), "finish recovery before the existing action ends");
            Assert.That(registry.Find(new MotionActionKey("base_sword_basic_0", MotionActionKind.Basic)).MotionId, Is.EqualTo("base_sword/attack1"));
            Assert.That(registry.Find(new MotionActionKey("base_sword_basic_1", MotionActionKind.Basic)).MotionId, Is.EqualTo("base_sword/attack2"));
            var changed = Source();
            changed.actions.Single(value => value.actionId == "base_sword_default").motion = "base_sword/attack1";
            Assert.Throws<ArgumentException>(() => new MotionSourceRegistry(changed, Combat()));
        }
        [Test] public void HammerSlamUsesDedicatedImportedArtAndPreservesBasicBindings()
        {
            var registry = new MotionSourceRegistry(Source(), Combat());
            Assert.That(registry.Find(new MotionActionKey("hammer_default", MotionActionKind.Special)).MotionId, Is.EqualTo("hammer/attack_default_slam"));
            Assert.That(registry.Find(new MotionActionKey("hammer_basic_0", MotionActionKind.Basic)).MotionId, Is.EqualTo("hammer/attack1"));
            Assert.That(registry.Find(new MotionActionKey("hammer_basic_1", MotionActionKind.Basic)).MotionId, Is.EqualTo("hammer/attack2"));
            var motion = registry.Motion("hammer/attack_default_slam");
            Assert.That(motion.Frames.Count, Is.EqualTo(14));
            var timing = motion.Compile(.4f, .95f);
            Assert.That(timing.FrameAt(.4f), Is.EqualTo(7));
            foreach (var frame in motion.Frames)
            {
                var sprite = Resources.Load<Sprite>(frame);
                Assert.That(sprite, Is.Not.Null, frame);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(128));
                Assert.That(Vector2.Distance(sprite.pivot, new Vector2(384, 164)), Is.LessThan(.001f));
            }
        }
        [Test] public void HammerSlamKeepsLegacyCatalogFallbackAndRejectsWrongDedicatedBinding()
        {
            var legacy = Source();
            legacy.palettes = legacy.palettes.Where(value => value.id != "hammer/attack_default_slam").ToArray();
            legacy.motions = legacy.motions.Where(value => value.id != "hammer/attack_default_slam").ToArray();
            var binding = legacy.actions.Single(value => value.actionId == "hammer_default");
            binding.clip = "attack1"; binding.motion = "hammer/attack1";
            // 여진도 같은 내려찍기를 쓰므로, 내려찍기가 없는 옛 카탈로그에서는 함께 attack1 로 돌아간다.
            binding = legacy.actions.Single(value => value.actionId == "hammer_follow_up");
            binding.clip = "attack1"; binding.motion = "hammer/attack1";
            var registry = new MotionSourceRegistry(legacy, Combat());
            Assert.That(registry.Find(new MotionActionKey("hammer_default", MotionActionKind.Special)).MotionId, Is.EqualTo("hammer/attack1"));
            var wrong = Source();
            binding = wrong.actions.Single(value => value.actionId == "hammer_default");
            binding.clip = "attack1"; binding.motion = "hammer/attack1";
            Assert.Throws<ArgumentException>(() => new MotionSourceRegistry(wrong, Combat()));
        }
        [TestCase("base_sword", "attack_dash_thrust", "base_sword_movement", 10, .15f, .5333333f, .25f, 4)]
        [TestCase("hammer", "attack_rush_push", "hammer_movement", 14, .2f, .85f, .4f, 7)]
        public void MovingSkillsUseApprovedHeldPosesAndPreserveBasicBindings(string form, string clip, string action, int count, float impact, float end, float heldAt, int heldFrame)
        {
            var registry = new MotionSourceRegistry(Source(), Combat());
            string id = form + "/" + clip;
            Assert.That(registry.Find(new MotionActionKey(action, MotionActionKind.Special)).MotionId, Is.EqualTo(id));
            Assert.That(registry.Find(new MotionActionKey(form + "_basic_0", MotionActionKind.Basic)).MotionId, Is.EqualTo(form + "/attack1"));
            var motion = registry.Motion(id);
            Assert.That(motion.Frames.Count, Is.EqualTo(count));
            Assert.That(motion.Compile(impact, end).FrameAt(heldAt), Is.EqualTo(heldFrame));
            foreach (var frame in motion.Frames)
            {
                var sprite = Resources.Load<Sprite>(frame);
                Assert.That(sprite, Is.Not.Null, frame);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(128));
                Assert.That(Vector2.Distance(sprite.pivot, new Vector2(384, 164)), Is.LessThan(.001f));
            }
            var legacy = Source();
            legacy.palettes = legacy.palettes.Where(value => value.id != id).ToArray();
            legacy.motions = legacy.motions.Where(value => value.id != id).ToArray();
            var binding = legacy.actions.Single(value => value.actionId == action);
            binding.clip = "attack1"; binding.motion = form + "/attack1";
            Assert.That(new MotionSourceRegistry(legacy, Combat()).Find(new MotionActionKey(action, MotionActionKind.Special)).MotionId, Is.EqualTo(form + "/attack1"));
            var wrong = Source(); binding = wrong.actions.Single(value => value.actionId == action);
            binding.clip = "attack1"; binding.motion = form + "/attack1";
            Assert.Throws<ArgumentException>(() => new MotionSourceRegistry(wrong, Combat()));
        }
        [Test] public void DefaultRegistryReproducesEverySuppliedClipAndAllBodyMappings()
        {
            var media = Media(); var registry = new MotionSourceRegistry(media.motionRegistry, Combat());
            foreach (var set in media.sets)
                foreach (var clip in set.clips.Where(value => value.name.StartsWith("attack", StringComparison.Ordinal)))
                {
                    var source = registry.Motion(set.form + "/" + clip.name); if (source.IsDelegated) continue;
                    Assert.That(source.Frames, Is.EqualTo(clip.frames)); Assert.That(source.Seconds, Is.EqualTo(clip.seconds));
                    var expected = new AttackMotionTiming(source.Id, clip.frames, clip.seconds, clip.contact, clip.sourceImpact, clip.fxStart, .2f, .7f);
                    var actual = source.Compile(.2f, .7f);
                    Assert.That(actual.ContentHash, Is.EqualTo(expected.ContentHash));
                    for (int tick = 0; tick < 80; tick++) { float age = tick * .01f; Assert.That(actual.SourceTime(age), Is.EqualTo(expected.SourceTime(age))); Assert.That(actual.FrameAt(age), Is.EqualTo(expected.FrameAt(age))); }
                }
        }
        [Test] public void RegistrySnapshotsAndCanonicalHashDoNotRetainMutableDocuments()
        {
            var source = Source(); var registry = new MotionSourceRegistry(source, Combat()); string hash = registry.ContentHash;
            var motion = registry.Motions.First(value => !value.IsDelegated); float seconds = motion.Seconds[0];
            source.motions.First(value => value.id == motion.Id).steps[0].seconds = 90; source.actions[0].motion = "changed"; source.palettes[0].frames[0] = "changed";
            Assert.That(registry.ContentHash, Is.EqualTo(hash)); Assert.That(motion.Seconds[0], Is.EqualTo(seconds));
            var copied = registry.ToDocument(); copied.motions.First(value => value.id == motion.Id).steps[0].frame = "changed";
            Assert.That(motion.Frames[0], Is.Not.EqualTo("changed"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)motion.StepIds)[0] = "changed");
            copied = registry.ToDocument(); Array.Reverse(copied.actions); Array.Reverse(copied.motions); Array.Reverse(copied.palettes);
            Assert.That(new MotionSourceRegistry(copied, Combat()).ContentHash, Is.EqualTo(hash));
        }
        [Test] public void MissingDuplicateUnknownAndUnavailableBindingsAreRejected()
        {
            var mutations = new Action<MotionRegistryDocument>[] {
                value => value.actions = value.actions.Skip(1).ToArray(),
                value => value.actions = value.actions.Concat(new[] { value.actions[0] }).ToArray(),
                value => value.actions[0].actionId = "unknown",
                value => value.actions[0].motion = "invented/attack1",
                value => value.actions[0].fallback = !value.actions[0].fallback,
                value => value.actions.First(action => action.kind == MotionActionKind.Conversion).variant = 9,
                value => value.motions = value.motions.Skip(1).ToArray(),
                value => value.palettes.Single(palette => palette.id == value.motions.First(motion => string.IsNullOrEmpty(motion.delegatedSource)).palette).frames = new[] { "invented" },
                value => value.motions.First(motion => !string.IsNullOrEmpty(motion.delegatedSource)).steps = new[] { new AttackMotionStep { id = "duplicate", frame = "duplicate", seconds = .1f } }
            };
            for (int index = 0; index < mutations.Length; index++) { var source = Source(); mutations[index](source); Assert.Catch<ArgumentException>(() => new MotionSourceRegistry(source, Combat()), "Invalid registry mutation " + index); }
        }
        [Test] public void KnownStableStepsCanInsertRemoveReorderAndRebindWithoutGameplayFrameMode()
        {
            var source = Source(); var motion = source.motions.First(value => string.IsNullOrEmpty(value.delegatedSource)); var palette = source.palettes.Single(value => value.id == motion.palette);
            motion.steps = new[] { new AttackMotionStep { id = "known-added-step", frame = palette.frames.Last(), seconds = .07f } }.Concat(motion.steps).ToArray();
            motion.steps[1].frame = palette.frames.Last(); var registry = new MotionSourceRegistry(source, Combat());
            Assert.That(registry.Motion(motion.id).StepIds[0], Is.EqualTo("known-added-step")); Assert.That(registry.Motion(motion.id).Frames[1], Is.EqualTo(palette.frames.Last()));
            motion.mode = FrameAttackMotionSource.FrameMode; Assert.Throws<ArgumentException>(() => new MotionSourceRegistry(source, Combat()));
            motion.mode = FixedAttackMotion.Mode; motion.steps[0].seconds = 0; Assert.Catch<ArgumentException>(() => new MotionSourceRegistry(source, Combat()));
            motion.steps[0].seconds = .07f; motion.steps[0].frame = "not-supplied"; Assert.Throws<ArgumentException>(() => new MotionSourceRegistry(source, Combat()));
        }
        [Test] public void CombinedBindingIdentityIncludesActualAppliedFirstSourceNotAFileHash()
        {
            var registry = new MotionSourceRegistry(Source(), Combat()); var clip = Media().sets.Single(value => value.form == "base_sword").Find("attack1");
            var document = new AttackMotionDocument { schemaVersion = 1, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FixedAttackMotion.Mode,
                sourceImpact = clip.sourceImpact, fxStart = clip.fxStart, steps = clip.frames.Select((frame, index) => new AttackMotionStep { id = clip.stepIds[index], frame = frame, seconds = clip.seconds[index] }).ToArray() };
            string before = registry.BindingHash(new FixedAttackMotion(document)); document.steps[0].seconds += .03f;
            Assert.That(registry.BindingHash(new FixedAttackMotion(document)), Is.Not.EqualTo(before));
        }
    }
}
