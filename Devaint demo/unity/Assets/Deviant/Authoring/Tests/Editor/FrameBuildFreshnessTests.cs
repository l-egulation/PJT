using System;
using System.IO;
using Deviant.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class FrameBuildFreshnessTests
    {
        string repository;
        Catalog combat;
        [SetUp] public void SetUp()
        {
            repository = FrameMotionSourceTests.CopyRepository();
            combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        }
        [TearDown] public void TearDown() { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        [Test] public void ChangedNumericSourceRejectsAnOtherwiseValidStaleGeneratedAction()
        {
            var generated = FrameMotionSourceTests.Generated(repository);
            var path = Path.Combine(repository, FrameMotionSource.ActionPath);
            var source = BasicSwordSource.Load(path, out var hash); source.hit.damage += 1;
            BasicSwordSource.Save(repository, hash, source);
            Assert.Catch<ArgumentException>(() => FrameMotionBuildValidation.ValidateCurrentSources(repository, generated, combat));
            BasicSwordSource.Regenerate(repository);
            var updated = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json")));
            Assert.That(FrameMotionBuildValidation.ValidateCurrentSources(repository, generated, updated), Is.Null);
        }
        [Test] public void SavedFrameTimingCannotBuildUntilItsGeneratedModeAndTimelineAreCurrent()
        {
            var fixedGenerated = FrameMotionSourceTests.Generated(repository);
            var candidate = FrameMotionSourceTests.Candidate(repository);
            FrameMotionSource.Load(repository, out var hash); FrameMotionSource.Save(repository, hash, candidate);
            Assert.Catch<ArgumentException>(() => FrameMotionBuildValidation.ValidateCurrentSources(repository, fixedGenerated, combat));
            AttackMotionSource.Regenerate(repository);
            var generated = FrameMotionSourceTests.Generated(repository);
            Assert.That(FrameMotionBuildValidation.ValidateCurrentSources(repository, generated, combat).ContentHash, Is.EqualTo(candidate.ContentHash));
            var next = FrameMotionSourceTests.Candidate(repository, .06f);
            FrameMotionSource.Load(repository, out hash); FrameMotionSource.Save(repository, hash, next);
            Assert.Catch<ArgumentException>(() => FrameMotionBuildValidation.ValidateCurrentSources(repository, generated, combat));
        }
        [Test] public void FixedMediaEditsRejectOldBindingsButFormattingDoesNotInventAContentChange()
        {
            var generated = FrameMotionSourceTests.Generated(repository);
            var path = Path.Combine(repository, AttackMotionSource.RelativePath);
            File.AppendAllText(path, "\n");
            Assert.That(FrameMotionBuildValidation.ValidateCurrentSources(repository, generated, combat), Is.Null);
            var document = AttackMotionSource.Load(path, out var hash); document.steps[0].seconds += .01f;
            AttackMotionSource.Save(path, hash, document);
            Assert.Catch<ArgumentException>(() => FrameMotionBuildValidation.ValidateCurrentSources(repository, generated, combat));
            AttackMotionSource.Regenerate(repository);
            Assert.That(FrameMotionBuildValidation.ValidateCurrentSources(repository, FrameMotionSourceTests.Generated(repository), combat), Is.Null);
        }
    }
}
