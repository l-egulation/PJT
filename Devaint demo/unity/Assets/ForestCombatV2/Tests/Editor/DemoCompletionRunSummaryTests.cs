using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Chapter2;
using Deviant.Run;
using Deviant.Tutorial.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class DemoCompletionRunSummaryTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        [UnityTest]
        public IEnumerator FinalClearSummaryIncludesThePreviousChapter()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            var host = Chapter1RunHost.Ensure();
            host.PrepareNewRun(Chapter1ProfileHost.Service.PlanUnarmedRun());
            var run = host.Flow.Current;
            run.StartClock(0);
            run.RecordKill(); run.RecordKill(); run.RecordKill();
            run.RecordPigment(11);
            run.MarkBossDefeated();
            Assert.That(host.Flow.TryAdvanceChapter(65), Is.True);
            SceneManager.LoadScene(Chapter2EntryBridge.ScenePath);
            Chapter2World world = null;
            float deadline = Time.realtimeSinceStartup + 45;
            do { yield return null; world = Object.FindAnyObjectByType<Chapter2World>(); }
            while ((!world || !world.Ready || !world.ExternalPresentation) && Time.realtimeSinceStartup < deadline);
            Assert.That(world && world.Ready && world.ExternalPresentation, Is.True);
            // This targeted presentation check begins at the callback used by the actual opened exit.
            typeof(Chapter2World).GetMethod("CompleteThroughExit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(world, null);
            deadline = Time.realtimeSinceStartup + 10;
            while (!world.ShowingResult && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.ShowingResult, Is.True);
            yield return null; yield return null;
            Assert.That(run.Result.HasValue, Is.True);
            Assert.That(run.Result.Value.ElapsedSeconds, Is.GreaterThanOrEqualTo(65));
            var panel = Object.FindAnyObjectByType<DemoCompletionPanel>();
            Assert.That(panel && panel.isActiveAndEnabled, Is.True, "the actual final-clear UI is active");
            string summary = panel.GetComponentsInChildren<Text>(true).Single(t => t.text.StartsWith("플레이 시간 ")).text;
            StringAssert.Contains(" / 처치 3 / 마법안료 +11", summary,
                "the final summary uses settled run totals, including chapter one");
            StringAssert.StartsWith("플레이 시간 " + Chapter1RunBinding.FormatTime(run.Result.Value.ElapsedSeconds), summary);
        }
    }
}
