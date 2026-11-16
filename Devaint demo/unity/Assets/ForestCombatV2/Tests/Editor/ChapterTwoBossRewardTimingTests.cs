using System;
using System.Collections;
using Deviant.Chapter2;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ChapterTwoBossRewardTimingTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        static void OpenEmptyScene()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator RunBoundRewardWaitsForTheDefeatPoseAndOpensOnce()
        {
            OpenEmptyScene();
            yield return new EnterPlayMode();
            yield return CheckNativeBossReward(true);
        }

        [UnityTest]
        public IEnumerator StandaloneFallbackWaitsForTheDefeatPoseAndOpensOnce()
        {
            OpenEmptyScene();
            yield return new EnterPlayMode();
            yield return CheckNativeBossReward(false);
        }

        static IEnumerator CheckNativeBossReward(bool planned)
        {
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
            if (planned)
            {
                var host = Chapter1RunHost.Ensure();
                host.PrepareNewRun(Chapter1ProfileHost.Service.PlanUnarmedRun());
                host.Flow.Current.MarkBossDefeated();
                Assert.That(host.Flow.TryAdvanceChapter(Chapter1RunHost.Now), Is.True);
            }
            else Assert.That(!Chapter1RunHost.Instance || Chapter1RunHost.Instance.Flow.Current == null, Is.True);
            SceneManager.LoadScene(Chapter2EntryBridge.ScenePath);
            Chapter2World world = null;
            float deadline = Time.realtimeSinceStartup + 45;
            do { yield return null; world = Object.FindAnyObjectByType<Chapter2World>(); }
            while ((!world || !world.Ready || !world.ExternalPresentation) && Time.realtimeSinceStartup < deadline);
            Assert.That(world && world.Ready && world.ExternalPresentation, Is.True);
            Assert.That(world.SourceRun != null, Is.EqualTo(planned));
            var arena = world.arena;
            arena.Player.maxHealth = arena.Player.health = 100000;
            world.WarpToRoom("B");
            world.Boss.BeginFight();
            Assert.That(world.Boss.InIntroduction, Is.True);
            // Use the runtime's explicit phase fixture; the final defeat itself still traverses native simulation.
            world.Boss.StartTestPhase(3);
            using (var simulation = arena.AcquireManualSimulation())
            {
                world.Boss.Blue.health = 0;
                simulation.Step(1f / 120);
            }
            Assert.That(world.Boss.Dead && world.Boss.DefeatPresentationPlaying, Is.True);
            arena.Player.Position = world.Exit.Position;
            arena.Player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            Assert.That(world.Exit.Unlocked, Is.True);
            if (!planned)
                Assert.That(world.BossOfferPending || world.Exit.VillageInkPickup.BlocksExit, Is.False,
                    "the standalone fixture has no run-bound reward or required ink blocking the exit");
            Assert.That(world.TryUseExit(), Is.False,
                "standing at the actual exit cannot skip the native defeat pose");
            Assert.That(world.Exit.Opening, Is.False);
            var state = arena.Sketch.State;
            int weaponsBefore = state.Arsenal.Weapons.Count;
            yield return null; yield return null;
            Assert.That(state.PendingOffer, Is.Null,
                "the exit must not create a reward modal before the native defeat pose finishes");
            Assert.That(arena.Controls.Current.SimulationPaused, Is.False,
                "the arena clock must continue so the defeat pose can finish");
            deadline = Time.realtimeSinceStartup + 10;
            while (world.Boss.DefeatPresentationPlaying && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(state.PendingOffer, Is.Null);
                yield return null;
            }
            Assert.That(world.Boss.DefeatPresentationPlaying, Is.False);
            deadline = Time.realtimeSinceStartup + 5;
            while (state.PendingOffer == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(state.PendingIsBoss, Is.True, "the owned or fallback boss reward still opens after the pose");
            Assert.That(world.BossOfferPending, Is.False);
            var binding = world.GetComponent<SketchRunBinding>();
            if (binding)
            {
                Assert.That(binding.TryChooseReward(0), Is.True);
                binding.ContinueReward();
            }
            else arena.Sketch.ChooseReward(0);
            yield return null; yield return null;
            Assert.That(state.Arsenal.Weapons.Count, Is.EqualTo(weaponsBefore + 1));
            Assert.That(state.PendingOffer, Is.Null, "the exit fallback must not reopen the already consumed reward");
            Assert.That(state.BossOfferTaken, Is.True);
        }
    }
}
