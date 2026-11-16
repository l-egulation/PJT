using System.Collections;
using System.Linq;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Locals captured by lambdas do not survive the domain reload of EnterPlayMode, so waits are plain loops.
    public sealed class Chapter1RunFlowTests
    {
        static int settled;
        static void CountSettlement(RunResult _) => settled++;

        static ForestCombatWorld ReadyWorld()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            return world && world.Ready && world.GetComponent<Chapter1RunBinding>() ? world : null;
        }

        [UnityTest] public IEnumerator ChapterSceneRunsWithItsSeedCountsKillsAndSettlesDeathOnce()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            var world = ReadyWorld();
            Assert.That(world, Is.Not.Null, "chapter 1 world did not become ready");
            var binding = world.GetComponent<Chapter1RunBinding>();
            var host = Chapter1RunHost.Instance;
            Assert.That(host, Is.Not.Null);
            Assert.That(binding.Run, Is.SameAs(host.Flow.Current));
            Assert.That(binding.Run.Seed, Is.EqualTo(42));
            Assert.That(world.Data.generationSeed, Is.EqualTo(42));

            settled = 0;
            host.Flow.Ended += CountSettlement;
            foreach (var enemy in world.arena.enemies.Where(e => e && !e.Dead).Take(2)) enemy.health = 0;
            for (int frame = 0; frame < 120 && binding.Run.Kills < 2; frame++) yield return null;
            Assert.That(binding.Run.Kills, Is.EqualTo(2));

            world.arena.Player.health = 0;
            float diedAt = Time.realtimeSinceStartup;
            for (int frame = 0; frame < 30 && !binding.AwaitingGameOver; frame++) yield return null;
            Assert.That(binding.AwaitingGameOver, Is.True, "death starts the game-over wait");
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(binding.Run.Ended || binding.ShowingResult, Is.False, "the death motion plays before the result screen");
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False, "the world keeps running during the death motion");
            while (!binding.Run.Ended && Time.realtimeSinceStartup - diedAt < 6) yield return null;
            Assert.That(Time.realtimeSinceStartup - diedAt, Is.GreaterThanOrEqualTo(Chapter1RunBinding.GameOverDelaySeconds - .05f));
            yield return null; yield return null;
            Assert.That(binding.Run.Ended, Is.True);
            Assert.That(settled, Is.EqualTo(1));
            Assert.That(binding.Run.Result.Value.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(binding.Run.Result.Value.Kills, Is.EqualTo(2));
            Assert.That(binding.ShowingResult, Is.True);
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True);
            Assert.That(host.Flow.TryDie(Chapter1RunHost.Now), Is.False);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator RetryAfterDeathStartsAFreshRunWithANewSeedAndBlueprintOffer()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 7;
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            var world = ReadyWorld();
            Assert.That(world, Is.Not.Null);
            var host = Chapter1RunHost.Instance;
            var first = host.Flow.Current;
            world.arena.Player.health = 0;
            // The result follows the death motion after GameOverDelaySeconds of real time.
            float diedAt = Time.realtimeSinceStartup;
            while (!first.Ended && Time.realtimeSinceStartup - diedAt < 6) yield return null;
            Assert.That(first.Ended, Is.True);

            host.StartNewRun();
            yield return null;
            for (int frame = 0; frame < 600 && (!ReadyWorld() || ReadyWorld() == world); frame++) yield return null;
            var next = ReadyWorld();
            Assert.That(next, Is.Not.Null.And.Not.SameAs(world), "retry should load a new chapter scene");
            var second = host.Flow.Current;
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Ended, Is.False);
            Assert.That(second.Kills, Is.Zero);
            Assert.That(next.proceduralSeed, Is.EqualTo(second.Seed));
            Assert.That(next.Data.generationSeed, Is.EqualTo(second.Seed));
            Assert.That(next.arena.Player.health, Is.EqualTo(next.arena.Player.maxHealth));
            Assert.That(next.GetComponent<Chapter1StartingBlueprints>().Complete, Is.False);
            Assert.That(next.GetComponent<Chapter1RunBinding>().ShowingResult, Is.False);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator TitleStartsTheChapterAndAClearReturnsToTheChapterTwoPlaceholder()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            yield return null;
            Assert.That(Object.FindAnyObjectByType<Chapter1TitleScreen>(), Is.Not.Null);
            var host = Chapter1RunHost.Instance;
            Assert.That(host, Is.Not.Null);

            host.StartNewRun();
            var prepared = host.Flow.Current;
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            var world = ReadyWorld();
            Assert.That(world, Is.Not.Null);
            Assert.That(host.Flow.Current, Is.SameAs(prepared), "the run prepared by the title is used once");
            Assert.That(world.Data.generationSeed, Is.EqualTo(prepared.Seed));

            var binding = world.GetComponent<Chapter1RunBinding>();
            // The binding attaches to the arena on its first Update, a frame after the world is ready;
            // the bare-handed departure is applied at the same time.
            var offers = world.GetComponent<Chapter1StartingBlueprints>();
            for (int frame = 0; frame < 60 && !offers.Complete; frame++) yield return null;
            Assert.That(offers.Complete && offers.SelectedCount == 0, Is.True, "a run without a village plan still starts bare-handed");
            Assert.That(binding.TryClearChapter(), Is.False, "the exit needs the boss defeated first");
            prepared.MarkBossDefeated();
            Assert.That(binding.TryClearChapter(), Is.True);
            yield return null;
            Assert.That(prepared.Result.Value.Outcome, Is.EqualTo(RunOutcome.ChapterCleared));
            Assert.That(binding.ShowingResult, Is.True);

            host.GoToTitle(true);
            for (int frame = 0; frame < 300 && SceneManager.GetActiveScene().path != Chapter1RunHost.TitleScene; frame++) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Chapter1RunHost.TitleScene));
            Assert.That(host.ChapterTwoPending, Is.True);
            host.LeaveChapterTwoPending();
            Assert.That(host.ChapterTwoPending, Is.False);
            yield return new ExitPlayMode();
        }
    }
}
