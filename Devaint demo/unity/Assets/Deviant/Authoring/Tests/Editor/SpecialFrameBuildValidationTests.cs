using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class SpecialFrameBuildValidationTests
    {
        static string Repository()
        {
            var directory = new DirectoryInfo(Application.dataPath);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, SpecialFrameSource.RelativePath))) directory = directory.Parent;
            Assert.That(directory, Is.Not.Null); return directory.FullName;
        }
        [Test] public void GeneratedWholeModeGraphMustMatchCanonicalNumericAndStructuralMediaSources()
        {
            string repository = Repository();
            var combat = MotionRegistrySource.ReadCombat(repository);
            var generated = JsonUtility.FromJson<CharacterAnimationCatalog>(File.ReadAllText(Path.Combine(repository, AttackMotionSource.CatalogPath)));
            var canonical = SpecialFrameSource.Load(repository, out _);
            Assert.DoesNotThrow(() => SpecialFrameSource.ValidateGenerated(repository, generated, combat, generated.specialFrames));
            Assert.Throws<ArgumentException>(() => SpecialFrameSource.ValidateGenerated(repository, generated, combat, null));
            var missing = canonical.ToDocument(); missing.actions = missing.actions.Skip(1).ToArray();
            Assert.Throws<ArgumentException>(() => SpecialFrameSource.ValidateGenerated(repository, generated, combat, missing));
            var stale = canonical.ToDocument(); stale.actions[0].mediaBindingVersion = "stale-applied-context";
            Assert.Throws<ArgumentException>(() => SpecialFrameSource.ValidateGenerated(repository, generated, combat, stale));
            var unknown = canonical.ToDocument(); unknown.actions[0].actionId = "not-a-policy";
            Assert.Throws<ArgumentException>(() => SpecialFrameSource.ValidateGenerated(repository, generated, combat, unknown));
            var foreign = canonical.ToDocument(); foreign.actions[0].kind = MotionActionKind.Conversion; foreign.actions[0].variant = 2;
            Assert.Throws<ArgumentException>(() => SpecialFrameSource.ValidateGenerated(repository, generated, combat, foreign));
        }
        [Test] public void UnsupportedTimelineOrNumericVersionDoesNotSilentlyRebaseSourceMode()
        {
            string repository = Repository(); var canonical = SpecialFrameSource.Load(repository, out _); var current = canonical.Find("whip_default");
            var source = current.ToDocument(); source.mode = MeleeTimingMode.FrameTime;
            source.timeline = new FrameTimelineDocument { frames = current.Media.StepIds.Select(id => new FrameTimelineStepDocument { id = id, seconds = .125f }).ToArray(),
                markers = new[] { new FrameTimelineMarkerDocument { id = "unknown", anchor = TimelineAnchor.Seconds, reference = "", offset = 0 } } };
            var document = canonical.ToDocument(); document.actions[Array.FindIndex(document.actions, value => value.actionId == current.Id)] = source;
            Assert.Throws<ArgumentException>(() => SpecialFrameSource.Compile(repository, document));
            Assert.That(canonical.Find(current.Id).Mode, Is.EqualTo(MeleeTimingMode.CombatTimeFixed));
        }
    }
}
