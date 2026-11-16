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
    public class SpecialFrameStartRejectionTests
    {
        [UnityTest] public IEnumerator GameRejectsMismatchedFramePayloadBeforeCreatingCast() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabRejectsMismatchedFramePayloadBeforeCreatingCast() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false; player.selected = 0;
                Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "whip"), 0, out var error), Is.True, error);
                var original = arena.GetSpecialFrame("whip_default"); var source = ActionSpecialFrameCompiler.ProjectAuthored(original.NumericAuthored, original.Media);
                var next = ActionSpecialFrameCompiler.CompileAuthored(original.NumericAuthored, original.Media, source);
                Assert.That(arena.TryApplySpecialFrames(new[] { next }, false, out error), Is.True, error);
                var active = arena.GetSpecialFrame(original.Id); int count = arena.casts.Count; float now = arena.Now; long input = player.NextInputSequence;
                Assert.Throws<ArgumentException>(() => CombatCast.Create(arena, player, active.Authored.ToData(), player.Weapon, false,
                    definitionVersion: "foreign-version", authored: active.Authored, specialFrame: active));
                Assert.Throws<ArgumentException>(() => CombatCast.Create(arena, player, active.Authored.ToData(), player.Weapon, false,
                    definitionVersion: active.ContentHash, authored: active.NumericAuthored, specialFrame: active));
                Assert.That(arena.casts.Count, Is.EqualTo(count)); Assert.That(player.currentCast, Is.Null); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.NextInputSequence, Is.EqualTo(input));
                var changed = active.ToDocument(); changed.timeline.frames[0].seconds += .03125f;
                var stale = active;
                Assert.That(arena.TryApplySpecialFrames(new[] { ActionSpecialFrameCompiler.CompileAuthored(active.NumericAuthored, active.Media, changed) }, false, out error), Is.True, error);
                Assert.Throws<ArgumentException>(() => CombatCast.Create(arena, player, stale.Authored.ToData(), player.Weapon, false,
                    definitionVersion: stale.ContentHash, authored: stale.Authored, specialFrame: stale));
                Assert.That(arena.casts.Count, Is.EqualTo(count)); Assert.That(arena.Now, Is.EqualTo(now));
                Assert.That(arena.TryApplySpecialFrames(new[] { original }, false, out error), Is.True, error);
            }
        }
    }
}
