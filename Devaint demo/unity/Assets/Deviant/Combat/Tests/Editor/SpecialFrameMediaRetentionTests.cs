using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SpecialFrameMediaRetentionTests
    {
        [UnityTest] public IEnumerator GameActiveSpecialFramesIgnoreSharedWeightsAndRejectStructuralChanges() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabActiveSpecialFramesIgnoreSharedWeightsAndRejectStructuralChanges() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var originalRegistry = arena.MotionRegistry.ToDocument(); var original = arena.GetSpecialFrame("whip_default");
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
                var player = arena.Player; player.selected = 0;
                Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "whip"), 0, out var error), Is.True, error);
                player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = false; arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
                var source = ActionSpecialFrameCompiler.ProjectAuthored(original.NumericAuthored, original.Media);
                var active = ActionSpecialFrameCompiler.CompileAuthored(original.NumericAuthored, original.Media, source);
                Assert.That(arena.TryApplySpecialFrames(new[] { active }, false, out error), Is.True, error);
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cast = player.currentCast;
                string local = arena.GetSpecialFrame(original.Id).Timeline.ContentHash, version = cast.DefinitionVersion, body = arena.Presentation.CurrentAttackMotionVersion;
                var weights = arena.MotionRegistry.ToDocument(); weights.motions.Single(value => value.id == "whip/attack1").steps[0].seconds += .03125f;
                Assert.That(arena.TryApplyMotionRegistry(weights, false, out error), Is.True, error);
                Assert.That(arena.GetSpecialFrame(original.Id).Timeline.ContentHash, Is.EqualTo(local)); Assert.That(arena.GetSpecialFrame(original.Id).ContentHash, Is.EqualTo(version));
                Assert.That(player.currentCast, Is.SameAs(cast)); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(body));
                var structural = arena.MotionRegistry.ToDocument(); var clip = structural.motions.Single(value => value.id == "whip/attack1");
                clip.steps[0].id = "changed-stable-step"; float now = arena.Now; string registry = arena.MotionRegistry.ContentHash, allFrames = arena.SpecialFrames.ContentHash;
                Assert.That(arena.TryApplyMotionRegistry(structural, true, out error), Is.False);
                Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.MotionRegistry.ContentHash, Is.EqualTo(registry)); Assert.That(arena.SpecialFrames.ContentHash, Is.EqualTo(allFrames));
                Assert.That(player.currentCast, Is.SameAs(cast)); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(body));
                Assert.That(arena.TryApplySpecialFrames(new[] { original }, false, out error), Is.True, error);
                Assert.That(arena.TryApplyMotionRegistry(originalRegistry, true, out error), Is.True, error);
            }
        }
    }
}
