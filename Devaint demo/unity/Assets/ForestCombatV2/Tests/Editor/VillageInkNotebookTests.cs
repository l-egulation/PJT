using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deviant.Control.Contracts;
using Deviant.Run;
using Deviant.Narrative.Opening;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class VillageInkNotebookTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Read(object o, string name) => o.GetType().GetProperty(name).GetValue(o);
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Hidden).GetValue(o);
        static void Call(object o, string name, params object[] args) => o.GetType().GetMethods(Hidden)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(o, args);
        static IEnumerator Until(Func<bool> done, float seconds = 30)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(done(), Is.True, "timed out waiting for ink notebook UI");
        }
        static ProfileData Profile(int owned, int returned, bool note)
        {
            var seen = new List<string> { "opening", "first-return", "artisan-first-run" };
            for (int k = 1; k <= returned; k++) { seen.Add("artisan-visit-" + k); seen.Add("village-ink-return-" + k); seen.Add("village-ink-reward-" + k); }
            if (note) seen.Add("village-executioner-note");
            return new ProfileData { profileId = Guid.NewGuid().ToString(), chaptersCleared = owned,
                chapter1Clears = owned > 0 ? 1 : 0, chapter1BossFirstClear = owned > 0, runsStarted = owned > 0 ? 1 : 0, seenStories = seen.ToArray() };
        }
        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>().FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
        static void EnableCapture(StartingVillageWorld world)
        {
            var pixel = Object.FindObjectsByType<MonoBehaviour>().First(b => b.GetType().Name == "VillagePixelViewport");
            pixel.GetType().GetField("enableInBatchMode").SetValue(pixel, true); pixel.enabled = true;
            System.IO.Directory.CreateDirectory("../outputs/hub/capture");
        }
        [UnityTest] public IEnumerator FirstClearUsesOneConnectedPigmentConversationAndLaterClearsDoNotRepeatIt()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            {
                var first = Profile(1, 0, false);
                first.seenStories = new[] { "opening", "first-return" };
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(first));
                StartingVillageWorld world = null; MonoBehaviour gui = null;
                yield return Until(() => { world = Object.FindAnyObjectByType<StartingVillageWorld>(); gui = Gui(); return world && world.Ready && gui; });
                if ((bool)Read(gui, "OpeningActive")) Call(gui, "SkipOpening");
                Call(gui, "CloseVillageInkModal");
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(first));
                gui.GetType().GetProperty("VisitData", Hidden).GetValue(gui);
                Call(gui, "StartVisit", 1);
                var script = Field<OpeningScript>(Field<object>(gui, "opening"), "script");
                var actions = script.beats.SelectMany(b => b.actions).ToArray();
                Assert.That(OpeningScriptRules.Validate(script), Is.Empty);
                Assert.That(actions.Count(a => a.type == "pigment"), Is.EqualTo(1));
                var dialogue = actions.Where(a => a.type == "bubble").Select(a => a.text).ToArray();
                Assert.That(dialogue, Does.Contain("그럼 다음에 나갈 준비는 내가 좀 도와줄게."));
                Assert.That(dialogue.Any(t => t.Contains("이 우물가에서 다시 깨어날 거야")), Is.True);
                Assert.That(dialogue, Does.Contain("모아 둔 마법안료도 사라지지 않아. 잘 챙겨 둬."));
                Assert.That(dialogue.Count(t => t == "난 다시 정비소에 가 봐야겠네. 밖에서 만나면 들러."), Is.EqualTo(1));
                Assert.That((bool)gui.GetType().GetMethod("PigmentKnown", Hidden).Invoke(gui, new object[] { Chapter1ProfileHost.Service.Snapshot }), Is.False,
                    "the pigment HUD waits for its line instead of appearing when the combined visit begins");
                Assert.That(actions.Any(a => a.type == "bubble" && a.text == "그런데 네 주머니에 반짝이는 거, 그거 뭐야?"), Is.False,
                    "the ordinary first-return conversation is not appended after the workshop");
                int restored = Array.FindIndex(actions, a => a.type == "bloom");
                int pigment = Array.FindIndex(actions, a => a.type == "pigment");
                int facility = Array.FindIndex(actions, a => a.type == "pan" && a.id == "artisan_hub");
                int supply = Array.FindIndex(actions, a => a.type == "unlockStation" && a.id == "supply_counter");
                int atelier = Array.FindIndex(actions, a => a.type == "unlockStation" && a.id == "palette_bench");
                Assert.That(actions.Count(a => a.type == "pan"), Is.EqualTo(1), "one shared facility visit");
                Assert.That(restored, Is.LessThan(pigment)); Assert.That(pigment, Is.LessThan(facility));
                Assert.That(facility, Is.LessThan(supply)); Assert.That(supply, Is.LessThan(atelier));
                EnableCapture(world);
                bool capturedTransition = false;
                float until = Time.realtimeSinceStartup + 100;
                while ((int)Read(gui, "VisitPlaying") != 0 && Time.realtimeSinceStartup < until)
                {
                    var bubble = (string)Read(gui, "BubbleShowing");
                    if (bubble != null)
                    {
                        if (!capturedTransition && bubble.StartsWith("그럼 다음에 나갈 준비"))
                        {
                            yield return new WaitForSecondsRealtime(.7f);
                            StartingVillageCaptureTests.Capture(gui, world, "first-clear-connected-lesson", 1920, 1080);
                            capturedTransition = true;
                        }
                        Call(gui, "AdvanceBubble");
                    }
                    yield return new WaitForSecondsRealtime(.15f);
                }
                Assert.That((int)Read(gui, "VisitPlaying"), Is.Zero, "the connected supply and atelier visit must finish normally");
                Assert.That(capturedTransition, Is.True);
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("artisan-first-run"), Is.True);
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("village-ink-return-1"), Is.True);
                Call(gui, "CloseVillageInkModal");
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(Profile(1, 0, false)));
                Call(gui, "StartVisit", 1);
                script = Field<OpeningScript>(Field<object>(gui, "opening"), "script");
                actions = script.beats.SelectMany(b => b.actions).ToArray();
                Assert.That(actions.Any(a => a.type == "pigment" || a.type == "pan" && a.id == "supply_counter"), Is.False);
                Call(gui, "SkipOpening"); Call(gui, "CloseVillageInkModal");
            }
            yield return new ExitPlayMode();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }
    }
}
