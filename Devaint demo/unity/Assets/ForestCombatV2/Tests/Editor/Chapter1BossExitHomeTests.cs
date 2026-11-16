using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // A village-planned run leaving chapter 1 through the real boss exit (docs/design/progression/village-palette-pieces.md):
    // before the palette is full the portcullis ends the run, the result shows the new piece and the run goes home to the
    // artisan's visit; with every piece the same exit carries the run on to chapter 2. The profile uses a memory store.
    public sealed class Chapter1BossExitHomeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Play mode ends before the test store goes: closing a chapter 2 scene still reads the profile, and a cleared
        // store would build a fresh one with a new profile host during the scene close.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        static StartingVillageWorld Village() { var v = Object.FindAnyObjectByType<StartingVillageWorld>(); return v && v.Ready ? v : null; }
        static ForestCombatWorld Chapter(RunSession run)
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var binding = world ? world.GetComponent<Chapter1RunBinding>() : null;
            return world && world.Ready && binding && binding.Run == run ? world : null;
        }

        static void OpenTitle()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
        }

        // In play mode: a fresh memory profile that has seen the opening and has beaten chapters 1..`chapters`
        // (one piece each, user decision 2026-11-04).
        static IEnumerator StartProfile(int chapters)
        {
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            var profile = Chapter1ProfileHost.Service;
            Assert.That(profile.MarkStorySeen("opening"), Is.True);
            profile.MarkStorySeen(Chapter1FirstRunInkGuide.GuideBagStory); // the first-run ink guide has its own test
            // The first run's automatic attack and reward guides have their own tests (FirstRunTutorialTriggerTests).
            profile.MarkStorySeen(SketchRunBinding.AttackTutorialRecord); profile.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
            for (int i = 0; i < chapters; i++)
                Assert.That(profile.SettleRun(profile.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, i + 1), Is.True);
            Assert.That(profile.PalettePieces, Is.EqualTo(chapters));
            for (int frame = 0; frame < 120 && !Chapter1RunHost.Instance; frame++) yield return null;
        }

        // The run and chapter ClearThroughTheExit left: fields, since a test's captured locals are made before
        // EnterPlayMode and the domain reload drops them.
        static RunSession run;
        static ForestCombatWorld world;

        // Departs on a planned run, fells the boss and walks into the opened exit.
        static IEnumerator DefeatBoss()
        {
            var host = Chapter1RunHost.Instance;
            Assert.That(host, Is.Not.Null, "title prepares the run host");
            host.StartNewRun(Chapter1ProfileHost.Service.PlanRun(new[] { "sword", "blunt" }));
            run = host.Flow.Current;
            world = null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (!(world = Chapter(run)) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world, Is.Not.Null, "the planned run loads chapter 1");
            yield return null;
            Assert.That(world.GetComponent<Chapter1RunBinding>().ProfileRun, Is.True);
            var player = world.arena.Player;
            player.maxHealth = player.health = 100000;
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }

            var encounter = world.GetComponent<Chapter1BossEncounter>();
            bool firstClear = Chapter1ProfileHost.Service.ChapterOneClearEndsRun;
            int goldBefore = run.Economy.Gold, stampsBefore = run.Economy.StageStampsDropped(StampStages.Chapter1);
            encounter.Boss.health = 0;
            for (int frame = 0; frame < 60 && !run.BossDefeated; frame++) yield return null;
            Assert.That(run.BossDefeated && encounter.ExitOpen, Is.True, "the boss's fall opens the exit");
            // Chapter 1's exit always carries the run on (user 2026-11-13), so its boss always pays gold and leaves the altar.
            Assert.That(firstClear, Is.False, "chapter 1 never ends the run");
            Assert.That(run.Economy.Gold > goldBefore, Is.True, "boss gold when the run carries on");
            // Sketch-weapon runs drop no stamps from any monster, the boss included (Chapter1Rewards, rewards.md).
            Assert.That(run.Economy.StageStampsDropped(StampStages.Chapter1), Is.EqualTo(stampsBefore), "no stamp drops in a sketch-weapon run");
            // Sketch-weapon runs take the boss reward choice instead of the colour ink altar (Chapter1InkAltar.Available).
            Assert.That(encounter.Altar.Available, Is.False, "no colour ink altar in a sketch-weapon run");
            Assert.That(encounter.Altar.SkippedForFirstClear, Is.False);
            // The boss's sketch reward choice holds the world; choose it (and any banked reward) as Chapter1BossClearTests does.
            var sketch = world.GetComponent<Chapter1RunBinding>().Sketch;
            for (int pick = 0; pick < 20 && sketch && sketch.State != null && sketch.State.MustOpenBeforeExit; pick++)
            { if (sketch.State.PendingOffer == null) sketch.State.OpenBanked(); sketch.State.Choose(0); }
            yield return null; yield return null;
            // Chapter 1 leads on to chapter 2, so the boss weapon choice is followed by a free augment choice.
            var augments = world.GetComponent<AugmentRewardChoice>();
            for (int i = 0; i < 30 && augments && augments.Queued > 0 && !augments.IsOpen; i++) yield return null;
            if (augments && augments.IsOpen) augments.TryTake(0);
            yield return null;
        }

        static IEnumerator ClearThroughTheExit()
        {
            yield return DefeatBoss();
            var encounter = world.GetComponent<Chapter1BossEncounter>();
            var player = world.arena.Player;
            var pickup = encounter.VillageInkPickup;
            if (pickup.Required)
            {
                float deadline = Time.realtimeSinceStartup + 5;
                while (!pickup.Landed && Time.realtimeSinceStartup < deadline) yield return null;
                player.Position = pickup.Position; Physics2D.SyncTransforms();
                Assert.That(pickup.TryCollect(), Is.True);
                yield return null;
            }
            player.Position = encounter.ExitPosition; Physics2D.SyncTransforms();
            Assert.That(encounter.TryUseExit(), Is.True);
        }

        static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(.05f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(done(), Is.True, "timed out waiting for the red ink story");
        }
        static void CaptureDiscovery(string name)
        {
            Chapter1MaintenanceGuiCaptureTests.Capture(world, Gui(), name, 1920, 1080);
        }

        [UnityTest] public IEnumerator FirstRedInkDropsRequiresFAndIsExplainedBeforeTheVillageColours()
        {
            OpenTitle(); yield return new EnterPlayMode();
            var settings = InputSystem.settings; var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                yield return StartProfile(0); yield return DefeatBoss();
                yield return new WaitForSecondsRealtime(4); // the departure-palette introduction has finished
                var encounter = world.GetComponent<Chapter1BossEncounter>(); var pickup = encounter.VillageInkPickup; var player = world.arena.Player;
                yield return WaitFor(() => pickup.Landed, 5);
                Assert.That(pickup.Required && pickup.Spawned && !pickup.Collected, Is.True);
                player.Position = encounter.ExitPosition; Physics2D.SyncTransforms();
                Assert.That(encounter.TryUseExit(), Is.False, "the first red ink cannot be left behind");
                player.Position = pickup.Position + Vector2.left * 1.1f; player.velocity = Vector2.zero; Physics2D.SyncTransforms(); world.RefreshCamera(0, true);
                yield return null; yield return null;
                Assert.That(pickup.Key.Visible, Is.True); CaptureDiscovery("red-ink-boss-drop");
                yield return Press(keyboard, Key.F);
                Assert.That(pickup.Collected, Is.True);
                var pocket=player.GetComponent<VillageInkPocketGlow>();
                Assert.That(pocket,Is.Not.Null); Assert.That(pocket.Visible,Is.True);
                Assert.That(pocket.Piece,Is.EqualTo(1));
                CaptureDiscovery("red-ink-collected-pocket-glow");
                Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False, "collecting ink has no cutscene or pause");
                Assert.That(Gui().GetComponentsInChildren<Text>(true).Any(t=>t.name=="붉은 잉크 발견 물음표"), Is.False);
                Assert.That(pickup.TryCollect(), Is.False);
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen(Chapter1VillageInkPickup.DiscoveryStory), Is.True);
                Assert.That(Chapter1ProfileHost.Service.PalettePieces, Is.Zero, "progress settles only at the chapter exit");
                Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
                yield return null; // the pickup key cannot also take the chapter exit in the same frame
                player.Position = encounter.ExitPosition; Physics2D.SyncTransforms();
                Assert.That(encounter.TryUseExit(), Is.True);
                // The exit carries the run on to chapter 2 (user decision 2026-11-13); the piece is kept on the way.
                yield return WaitFor(() => SceneManager.GetActiveScene().path == Deviant.Chapter2.Chapter2EntryBridge.ScenePath &&
                    Object.FindAnyObjectByType<Deviant.Chapter2.Chapter2World>()?.Ready == true, 30);
                Assert.That(Chapter1ProfileHost.Service.PalettePieces, Is.EqualTo(1));
                Chapter1RunHost.Instance.GoToVillage();
                yield return WaitFor(() => Village() && (int)Gui().GetType().GetProperty("VisitPlaying").GetValue(Gui()) == 1, 25);
                var village = Village(); var gui = Gui();
                for (int i = 0; i < 9; i++)
                {
                    yield return WaitFor(() => gui.GetType().GetProperty("BubbleShowing").GetValue(gui) != null, 18);
                    string line = (string)gui.GetType().GetProperty("BubbleShowing").GetValue(gui);
                    Assert.That(village.ColourPieces, Is.Zero, "no colour returns before the artisan explains the ink");
                    Assert.That((bool)gui.GetType().GetProperty("VillageInkShowing").GetValue(gui), Is.False);
                    yield return new WaitForSecondsRealtime(line.Length / 30f + .25f);
                    if (i == 2) StartingVillageCaptureTests.Capture(gui, village, "red-ink-artisan-explains", 1920, 1080);
                    yield return Press(keyboard, Key.F);
                }
                yield return WaitFor(() => (bool)gui.GetType().GetProperty("VillageInkShowing").GetValue(gui), 3);
                Assert.That(village.ColourPieces, Is.Zero);
                yield return WaitFor(() => village.ColourPieces == 1, 5);
                Assert.That(village.Blooming, Is.True);
                gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null); yield return null;
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("artisan-first-run"), Is.True, "an unshown supply lesson joins the same arrival");
                Assert.That((int)gui.GetType().GetProperty("VisitPlaying").GetValue(gui), Is.Zero);
            }
            finally { InputSystem.RemoveDevice(keyboard); settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput; }
        }

        // The first chapter-1 clear no longer goes home: the exit carries the run on to chapter 2 and keeps the first
        // piece (user decision 2026-11-13).
        [UnityTest] public IEnumerator TheFirstChapterOneClearCarriesTheRunOnToChapterTwoWithTheFirstPiece()
        {
            OpenTitle();
            yield return new EnterPlayMode();
            yield return StartProfile(0);
            var profile = Chapter1ProfileHost.Service;
            yield return ClearThroughTheExit();
            float deadline = Time.realtimeSinceStartup + 30;
            while (SceneManager.GetActiveScene().path != Deviant.Chapter2.Chapter2EntryBridge.ScenePath && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Deviant.Chapter2.Chapter2EntryBridge.ScenePath), "no result screen, on to chapter 2");
            Assert.That(Chapter1RunHost.Instance.Flow.Current, Is.SameAs(run));
            Assert.That(run.Ended, Is.False); Assert.That(run.Chapter, Is.EqualTo(2));
            Assert.That(profile.PalettePieces, Is.EqualTo(1), "the first chapter-1 clear keeps the first piece");
            Assert.That(profile.ChaptersCleared, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator OnceChapterOneIsBeatenItsBossExitCarriesTheRunOnToChapterTwo()
        {
            OpenTitle();
            yield return new EnterPlayMode();
            yield return StartProfile(1);
            yield return ClearThroughTheExit();
            float deadline = Time.realtimeSinceStartup + 30;
            while (SceneManager.GetActiveScene().path != Deviant.Chapter2.Chapter2EntryBridge.ScenePath && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Deviant.Chapter2.Chapter2EntryBridge.ScenePath));
            Assert.That(Chapter1RunHost.Instance.Flow.Current, Is.SameAs(run));
            Assert.That(run.Ended, Is.False); Assert.That(run.Chapter, Is.EqualTo(2));
            Assert.That(Chapter1ProfileHost.Service.PalettePieces, Is.EqualTo(1), "passing chapter 1 again gives no new piece; beating chapter 2 will");
        }
    }
}
