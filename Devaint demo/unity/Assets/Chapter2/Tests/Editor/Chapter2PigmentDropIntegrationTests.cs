using System.Collections;
using System.Linq;
using Deviant.Chapter2.Editor;
using Deviant.Run;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2PigmentDropIntegrationTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            // Chapter2 flushes the profile when destroyed: keep the memory store until play mode has ended.
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        static IEnumerator WaitForChapterTwo()
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < deadline)
            {
                var world = Object.FindAnyObjectByType<Chapter2World>();
                if (world && world.Ready) { yield return null; yield return null; yield break; }
                yield return null;
            }
            Assert.Fail("The existing Chapter2 scene did not initialize.");
        }

        [UnityTest] public IEnumerator ProfileKillsShowPigmentOnceAndTrainingShowsNone()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            var profile = Chapter1ProfileHost.Service;
            var host = Chapter1RunHost.Ensure();
            var plan = profile.PlanRun(new[] { "sword", "blunt" });
            host.PrepareNewRun(plan);
            host.Flow.Current.MarkBossDefeated();
            Assert.That(host.Flow.TryAdvanceChapter(Chapter1RunHost.Now), Is.True);
            SceneManager.LoadScene(Chapter2Tools.ScenePath, LoadSceneMode.Single);
            yield return WaitForChapterTwo();
            var world = Object.FindAnyObjectByType<Chapter2World>();
            Assert.That(world.SourceRun, Is.SameAs(host.Flow.Current));
            Assert.That(world.SourceRun.Plan, Is.SameAs(plan));
            Assert.That(world.SourceRun.Plan.Training, Is.False);
            Assert.That(world.SourceRun.Chapter, Is.EqualTo(2));

            using (var simulation = world.arena.AcquireManualSimulation())
            {
                world.arena.Player.maxHealth = world.arena.Player.health = 1000;
                world.WarpToRoom("F01");
                var enemy = world.arena.enemies.First(e => e && !e.Dead);
                var before = profile.Snapshot.Pigment;
                int kills = world.Kills;
                enemy.health = 0;
                simulation.Step(.03f);
                yield return null; yield return null;

                Assert.That(world.Kills, Is.EqualTo(kills + 1));
                Assert.That(profile.Snapshot.Pigment, Is.EqualTo(before + 1), "a real Chapter2 normal kill credits its profile once");
                Assert.That(world.PigmentEarned, Is.EqualTo(1));
                var effect = world.arena.GetComponent<Chapter1PigmentPickupEffect>();
                Assert.That(effect, Is.Not.Null, "Chapter2 creates the shared presentation when pigment is actually awarded");
                Assert.That(effect.Shown, Is.EqualTo(1));
                Assert.That(effect.ActiveCount, Is.EqualTo(1));
                var images = effect.GetComponentsInChildren<SpriteRenderer>();
                Assert.That(images.Any(r => r.sprite && r.sprite.name == "pigment-palette-flat"), Is.True);
                Assert.That(images.Any(r => r.sprite && r.sprite.name == "+1"), Is.True, "the view uses the actual awarded amount");

                simulation.Step(1.1f);
                yield return null; yield return null;
                Assert.That(effect.ActiveCount, Is.Zero);
                Assert.That(effect.Shown, Is.EqualTo(1), "the same dead actor cannot create repeated views on later simulation steps");
                Assert.That(profile.Snapshot.Pigment, Is.EqualTo(before + 1));

                // Reset revives the actor and clears Chapter2's kill set, but the profile's payout receipt remains.
                world.arena.ResetTest();
                world.WarpToRoom("F01");
                Assert.That(enemy.Dead, Is.False);
                enemy.health = 0;
                simulation.Step(.03f);
                yield return null; yield return null;
                Assert.That(world.Kills, Is.EqualTo(1), "the revived actor still counts as a new combat kill");
                Assert.That(world.PigmentEarned, Is.EqualTo(1));
                Assert.That(profile.Snapshot.Pigment, Is.EqualTo(before + 1));
                Assert.That(effect.Shown, Is.EqualTo(1), "a rejected duplicate award must not show a second palette or amount");
                Assert.That(effect.ActiveCount, Is.Zero);
            }

            var earned = profile.Snapshot.Pigment;
            var training = profile.PlanRun(new[] { "sword", "blunt" }, training: true);
            host.PrepareNewRun(training);
            host.Flow.Current.MarkBossDefeated();
            Assert.That(host.Flow.TryAdvanceChapter(Chapter1RunHost.Now), Is.True);
            SceneManager.LoadScene(Chapter2Tools.ScenePath, LoadSceneMode.Single);
            yield return WaitForChapterTwo();
            world = Object.FindAnyObjectByType<Chapter2World>();
            Assert.That(world.SourceRun.Plan, Is.SameAs(training));
            Assert.That(world.SourceRun.Plan.Training, Is.True);
            using (var simulation = world.arena.AcquireManualSimulation())
            {
                world.WarpToRoom("F01");
                world.arena.enemies.First(e => e && !e.Dead).health = 0;
                simulation.Step(.03f);
                yield return null; yield return null;
                Assert.That(world.Kills, Is.EqualTo(1));
                Assert.That(world.PigmentEarned, Is.Zero);
                Assert.That(profile.Snapshot.Pigment, Is.EqualTo(earned), "training cannot credit the persistent profile");
                var effect = world.arena.GetComponent<Chapter1PigmentPickupEffect>();
                Assert.That(effect ? effect.Shown : 0, Is.Zero, "training creates no pigment view");
            }
            yield return new ExitPlayMode();
        }
    }
}
