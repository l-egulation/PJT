using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The first return (docs/design/story/opening/opening-playable.md part 5): the hero falls in chapter 1, 「마을로」 on the result
    // brings it home, and instead of the usual spawn it wakes at the well out of gathering ink; one line
    // follows, once. Holding Esc's skip leaves the hero standing at the well. The profile uses a memory store. Captures go to
    // outputs/hub/capture/first-return-*.png.
    public sealed class StartingVillageFirstReturnTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const float Well = 14.5f;
        const string Line = "또 우물 앞이야..?";

        // Play mode ends before the test store goes (see Chapter1BossExitHomeTests).
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
        static T Property<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetProperty(name).GetValue(gui);
        static bool Returning(MonoBehaviour gui) => Property<bool>(gui, "FirstReturnPlaying");
        static StartingVillageWorld Village() { var v = Object.FindAnyObjectByType<StartingVillageWorld>(); return v && v.Ready ? v : null; }

        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            for (float at = Time.realtimeSinceStartup; !done() && Time.realtimeSinceStartup - at < seconds;) yield return null;
        }

        // In play mode: a fresh memory profile that has seen the opening, no clears.
        static IEnumerator StartProfile()
        {
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            Assert.That(Chapter1ProfileHost.Service.MarkStorySeen("opening"), Is.True);
            Chapter1ProfileHost.Service.MarkStorySeen(Chapter1FirstRunInkGuide.GuideBagStory); // the first-run ink guide has its own test
            for (int frame = 0; frame < 120 && !Chapter1RunHost.Instance; frame++) yield return null;
        }

        // The village GUI as the test store sees it: an opening started from the real editor profile is not this one's,
        // and a check made before the test store came in is made again.
        static IEnumerator VillageGui()
        {
            StartingVillageWorld village = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(village = Village());) yield return null;
            Assert.That(village, Is.Not.Null, "the village loads");
            gui = Gui();
            if ((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui) && !Returning(gui)) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
        }

        // Fields: locals captured before EnterPlayMode are lost to the domain reload.
        static MonoBehaviour gui;
        static Chapter1RunBinding result;
        static StartingVillageWorld village;

        [UnityTest] public IEnumerator TheFirstDeathWakesTheHeroAtTheWellOnce()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            yield return StartProfile();
            var host = Chapter1RunHost.Instance;
            Assert.That(host, Is.Not.Null, "title prepares the run host");
            host.StartNewRun(Chapter1ProfileHost.Service.PlanRun(new[] { "sword", "blunt" }));
            var run = host.Flow.Current;
            ForestCombatWorld world = null;
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < 30;)
            {
                world = Object.FindAnyObjectByType<ForestCombatWorld>();
                var binding = world ? world.GetComponent<Chapter1RunBinding>() : null;
                if (world && world.Ready && binding && binding.Run == run) break;
                world = null;
                yield return null;
            }
            Assert.That(world, Is.Not.Null, "the planned run loads chapter 1");
            yield return null;
            // The hero falls; the result comes up after the game-over delay.
            world.arena.Player.health = 0;
            result = world.GetComponent<Chapter1RunBinding>();
            yield return WaitFor(() => result.ShowingResult, 10);
            Assert.That(result.ShowingResult, Is.True, "the death shows the result");
            Assert.That(run.Result?.Outcome, Is.EqualTo(RunOutcome.Died));
            yield return null; yield return null;
            var home = (Button)Gui().GetType().GetField("menuPrimary", Private).GetValue(Gui());
            Assert.That(home.GetComponentInChildren<Text>().text, Is.EqualTo("마을로"));
            home.onClick.Invoke();

            yield return VillageGui();
            village = Village();
            yield return WaitFor(() => Returning(gui), 3);
            Assert.That(Returning(gui), Is.True, "the first death plays the first return");
            var player = village.World.arena.Player;
            Assert.That(player.Position.x, Is.EqualTo(Well).Within(.01f), "the hero wakes at the well, not the usual spawn");
            Assert.That(village.HeroGathering, Is.True, "ink gathers into the hero's shape");
            yield return WaitFor(() => false, .35f);
            StartingVillageCaptureTests.Capture(gui, village, "first-return-gather");
            yield return WaitFor(() => !village.HeroGathering, 3);
            yield return WaitFor(() => false, .25f);
            StartingVillageCaptureTests.Capture(gui, village, "first-return-rise");
            yield return WaitFor(() => Property<string>(gui, "BubbleShowing") == Line, 5);
            Assert.That(Property<string>(gui, "BubbleShowing"), Is.EqualTo(Line));
            Assert.That(player.Position.x, Is.EqualTo(Well).Within(.01f), "nothing moved the hero while it got up");
            // No banner (demo review 2026-11-09): waking at the well already shows it, so the line stays up on its own.
            yield return WaitFor(() => false, 1f);
            Assert.That(Property<string>(gui, "BubbleShowing"), Is.EqualTo(Line), "the line is still readable before the scene ends");
            Assert.That(Property<string>(gui, "BannerShowing"), Is.Null);
            StartingVillageCaptureTests.Capture(gui, village, "first-return-line");
            yield return WaitFor(() => !Returning(gui), 8);
            Assert.That(Returning(gui), Is.False);
            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("first-return"), Is.True, "seen once, for good");
            Assert.That(Property<string>(gui, "BannerShowing"), Is.Null);

            // Then the artisan's first-run visit (never met at the shop: the stranger greeting). The well scene must not
            // record the supply office as already opened, or the visit is taken for an old save and skipped (playtest
            // 2026-11-09: 「또 우물 앞이야..?」 and nothing after it).
            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("station-opened-supply_counter"), Is.False, "the artisan opens the office");
            yield return WaitFor(() => Property<string>(gui, "BubbleShowing") == "어머, 이 마을에 손님이라니.", 15);
            Assert.That(Property<string>(gui, "BubbleShowing"), Is.EqualTo("어머, 이 마을에 손님이라니."), "the artisan comes after the first return");
            StartingVillageCaptureTests.Capture(gui, village, "first-run-visit-greet");
            gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
            yield return WaitFor(() => !(bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui), 5);
            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("artisan-first-run"), Is.True);

            // Back again from the same death (the title, say): no second time.
            Chapter1RunHost.Instance.GoToVillage();
            yield return null;
            yield return VillageGui();
            yield return WaitFor(() => false, .5f);
            Assert.That(Returning(gui), Is.False, "the first return plays only once");
        }

        [UnityTest] public IEnumerator SkippingTheFirstReturnLeavesTheHeroStandingAtTheWell()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            yield return StartProfile();
            var flow = Chapter1RunHost.Instance.Flow;
            flow.StartNewRun(Chapter1RunHost.Now);
            Assert.That(flow.TryDie(Chapter1RunHost.Now), Is.True);
            Chapter1RunHost.Instance.GoToVillage();
            yield return null;
            yield return VillageGui();
            var village = Village();
            yield return WaitFor(() => Returning(gui), 3);
            Assert.That(Returning(gui), Is.True);
            yield return WaitFor(() => false, .3f);
            gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
            Assert.That(Returning(gui), Is.False);
            Assert.That(village.HeroAnimating, Is.False, "the gathering ink and the pose go");
            var player = village.World.arena.Player;
            Assert.That(player.visual.forceRenderingOff, Is.False, "the live hero is drawn again");
            Assert.That(player.Position.x, Is.EqualTo(Well).Within(.01f));
            Assert.That(Property<string>(gui, "BubbleShowing"), Is.Null);
            Assert.That(Property<string>(gui, "BannerShowing"), Is.Null);
            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("first-return"), Is.True, "a skipped first return counts as seen");
        }

        [System.Serializable] sealed class MaintenanceLines { public string[] first, firstAfterVillage, second; }

        // A hero who died before the chapter 1 shop meets the artisan in the village first; the shop then greets it as that
        // customer (demo review 2026-11-09). Only the greeting differs: the shop line and the four portal call-back lines
        // that MaintenanceRoomPresentation counts from the end stay the same.
        [Test]
        public void TheShopGreetsAVillageCustomerWithTheSameShopAndPortalLines()
        {
            var lines = JsonUtility.FromJson<MaintenanceLines>(Resources.Load<TextAsset>("MaintenanceShops/MaintenanceDialogue").text);
            Assert.That(lines.first.Length, Is.EqualTo(10));
            Assert.That(lines.firstAfterVillage.Length, Is.EqualTo(8));
            Assert.That(lines.firstAfterVillage[0], Does.Contain("우물가에서 본"));
            Assert.That(lines.firstAfterVillage.Skip(2), Is.EqualTo(lines.first.Skip(4)), "from the shop's explanation on, both greetings match");
            Assert.That(lines.firstAfterVillage[lines.firstAfterVillage.Length - 5], Is.EqualTo(lines.first[lines.first.Length - 5]), "the shop opens on the same line");
        }
    }
}
