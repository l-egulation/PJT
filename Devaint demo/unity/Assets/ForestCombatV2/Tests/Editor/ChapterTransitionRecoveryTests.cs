using System;
using System.Collections;
using System.Reflection;
using Deviant.Chapter2;
using Deviant.Control.Contracts;
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
    public sealed class ChapterTransitionRecoveryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        static ForestCombatWorld ReadyChapter()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            return world && world.Ready && world.GetComponent<Chapter1RunBinding>() ? world : null;
        }

        static IEnumerator Until(Func<bool> condition, string description, float seconds = 45)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, description);
        }

        [UnityTest]
        public IEnumerator ReadinessRetryKeepsTheLoadedWorldAndRun()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            yield return ExerciseReadinessRetry();
        }

        // Capturing delegates are created after EnterPlayMode's domain reload.
        static IEnumerator ExerciseReadinessRetry()
        {
            var host = Chapter1RunHost.Ensure();
            host.PrepareNewRun(Chapter1ProfileHost.Service.PlanUnarmedRun());
            var planned = host.Flow.Current;
            int loadAttempts = 0;
            bool failReadiness = true;
            ChapterSceneFade.Load(LoadingPlace.Chapter1,
                () => { loadAttempts++; ChapterSceneFade.LoadSceneBehind(Chapter1RunHost.ChapterScene); },
                () =>
                {
                    if (!ReadyChapter()) return false;
                    if (failReadiness) { failReadiness = false; throw new InvalidOperationException("fixture readiness failure after activation"); }
                    return true;
                });
            yield return Until(() => ChapterSceneFade.ShowingFailure, "the destination activates before its readiness check fails");
            yield return Until(() => planned.Sketch != null, "the destination binds its run-owned sketch state");
            var loaded = ReadyChapter();
            Assert.That(loaded, Is.Not.Null);
            Assert.That(host.Flow.Current, Is.SameAs(planned));
            var player = loaded.arena.Player;
            player.health = 57;
            planned.Economy.Earn(123);
            var sketch = planned.Sketch;
            typeof(ChapterSceneFade).GetMethod("Retry", Private).Invoke(Object.FindAnyObjectByType<ChapterSceneFade>(), null);
            yield return Until(() => !ChapterSceneFade.Busy, "retry completes the same destination's readiness check");
            Assert.That(loadAttempts, Is.EqualTo(1), "a readiness retry must not load the scene a second time");
            Assert.That(ReadyChapter(), Is.SameAs(loaded), "the already loaded destination is retained");
            Assert.That(ReadyChapter().arena.Player, Is.SameAs(player));
            Assert.That(host.Flow.Current, Is.SameAs(planned), "retry must not replace the prepared run");
            Assert.That(planned.Sketch, Is.SameAs(sketch));
            Assert.That(planned.Economy.Gold, Is.EqualTo(123));
            Assert.That(player.health, Is.EqualTo(57));
        }

        [UnityTest]
        public IEnumerator FailedLoadPausesTheWorldUntilRetryAndReleasesOnlyItsOwnPause()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            yield return ExerciseFailurePause();
        }

        static IEnumerator ExerciseFailurePause()
        {
            var profile = Chapter1ProfileHost.Service;
            profile.MarkStorySeen(Chapter1FirstRunInkGuide.GuideBagStory);
            profile.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
            profile.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
            var host = Chapter1RunHost.Ensure();
            host.PrepareNewRun(profile.PlanUnarmedRun());
            SceneManager.LoadScene(Chapter1RunHost.ChapterScene);
            yield return Until(() => ReadyChapter() && !ReadyChapter().arena.Controls.Current.SimulationPaused,
                "the source chapter is running before its transition");
            var world = ReadyChapter();
            var arena = world.arena;
            foreach (var enemy in arena.enemies) if (enemy) enemy.aggressive = false;
            var sourcePause = arena.Controls.OpenScope("fixture source transition");
            sourcePause.PauseSimulation();
            ChapterSceneFade.Load(LoadingPlace.Chapter2, () =>
            {
                // This is the chapter bridge's real ordering: release source pause, then try the load.
                sourcePause.Dispose();
                throw new InvalidOperationException("fixture load error after source pause release");
            });
            yield return Until(() => ChapterSceneFade.ShowingFailure, "the actual transition reports its thrown load error");
            var fade = Object.FindAnyObjectByType<ChapterSceneFade>();
            // Keep this forced-error job from automatically running again while inspecting Retry ownership.
            fade.StopAllCoroutines();
            Assert.That(sourcePause.IsActive, Is.False);
            Assert.That(arena.Controls.Current.SimulationPaused, Is.True,
                "a failure cannot leave AI and the run clock active behind the error choices");
            float before = arena.Now;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(arena.Now, Is.EqualTo(before), "waiting at the failure choices must not advance the run");
            using (var other = arena.Controls.OpenScope("fixture independent menu"))
            {
                other.PauseSimulation();
                typeof(ChapterSceneFade).GetMethod("Retry", Private).Invoke(fade, null);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.True, "retry must keep another owner's pause");
            }
            Assert.That(arena.Controls.Current.SimulationPaused, Is.False, "retry releases the failure's own pause");
            Assert.That(arena.Controls.Current.Blocks(InputChannels.Combat | InputChannels.Interaction), Is.True,
                "retry retains the loading input block");
            yield return Until(() => arena.Now > before, "normal loading simulation resumes after retry");
            typeof(ChapterSceneFade).GetMethod("TryLoad", Private).Invoke(fade,
                new object[] { (Action)(() => throw new InvalidOperationException("fixture second load error")) });
            Assert.That(arena.Controls.Current.SimulationPaused, Is.True, "a repeated failure acquires a fresh pause");
            var leases = (System.Collections.Generic.Dictionary<Deviant.Combat.CombatArena, IControlLease>)
                typeof(ChapterSceneFade).GetField("failurePauses", Private).GetValue(fade);
            var failureLease = leases[arena];
            typeof(ChapterSceneFade).GetMethod("ToTitle", Private).Invoke(fade, null);
            yield return Until(() => SceneManager.GetActiveScene().path == Chapter1RunHost.TitleScene && !ChapterSceneFade.Busy,
                "the failure choices return to the actual title");
            Assert.That(failureLease.IsActive, Is.False, "destroying the loading screen releases its failure pause");
        }

        [UnityTest]
        public IEnumerator ReturningToTitleAfterAnAbortedChapterLoadAllowsTheNextRunToContinue()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.ChapterScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            yield return ExerciseAbandonedBridge();
        }

        static IEnumerator ExerciseAbandonedBridge()
        {
            yield return Until(() => ReadyChapter(), "chapter one initializes");
            var host = Chapter1RunHost.Ensure();
            host.Flow.Current.MarkBossDefeated();
            Assert.That(Chapter2EntryBridge.TryEnterFromChapterOne(), Is.True);
            var fade = Object.FindAnyObjectByType<ChapterSceneFade>();
            Assert.That(fade, Is.Not.Null);
            // Inject a confirmed load fault at the adapter boundary after the real bridge has captured its carry.
            // Stop only this fixture's pending coroutine so the real target scene cannot finish during fault injection.
            fade.StopAllCoroutines();
            typeof(ChapterSceneFade).GetMethod("TryLoad", Private).Invoke(fade,
                new object[] { (Action)(() => throw new InvalidOperationException("fixture chapter-two load failure")) });
            Assert.That(ChapterSceneFade.ShowingFailure, Is.True);
            typeof(ChapterSceneFade).GetMethod("ToTitle", Private).Invoke(fade, null);
            yield return Until(() => SceneManager.GetActiveScene().path == Chapter1RunHost.TitleScene && !ChapterSceneFade.Busy,
                "the failure screen returns to the real title scene");
            host.StartNewRun();
            yield return Until(() => ReadyChapter(), "a fresh chapter-one run initializes");
            host.Flow.Current.MarkBossDefeated();
            Assert.That(Chapter2EntryBridge.TryEnterFromChapterOne(), Is.True,
                "an abandoned carry must not permanently reject the next run's chapter-two transition");
            yield return Until(() =>
            {
                var next = Object.FindAnyObjectByType<Chapter2World>();
                return next && next.Ready && !ChapterSceneFade.Busy;
            }, "the next run reaches the real chapter-two scene");
        }
    }
}
