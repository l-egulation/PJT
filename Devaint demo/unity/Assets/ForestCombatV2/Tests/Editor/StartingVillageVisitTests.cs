using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The village artisan's visits (docs/design/progression/village-palette-pieces.md) with real key presses: back with the
    // first palette piece, visit 1 plays (the artisan walks in, the stand appears, both walk to the palette workbench, its
    // lock breaks, the artisan leaves); then two more clears make visit 3 next, which Esc held skips to its end state.
    // Captures go to outputs/hub/capture/visit-*.png.
    public sealed class StartingVillageVisitTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        Keyboard keyboard;

        [System.Serializable] sealed class BlotLayout { public StartingVillageWorld.Blot[] colourBlots; public Spot[] buildings, stations; }
        [System.Serializable] sealed class Spot { public string id; public float x; }
        // The workshop that a palette piece opens is fully inside that piece's colour blots, with its workbench: opening it
        // shows it in colour. Same reach rule as VillageBlotColour.shader without the grain (radius - distance >= 40 px / 2).
        [TestCase("palette_atelier", "artisan_hub", 1)]
        public void TheWorkshopAPieceOpensIsInsideThatPiecesColour(string building, string station, int piece)
        {
            var layout = JsonUtility.FromJson<BlotLayout>(Resources.Load<TextAsset>("HubLayout").text);
            Assert.That(JsonUtility.FromJson<ProgressionData>(Resources.Load<TextAsset>("RogueliteProgression").text).buildings.Single(b => b.id == building).openAtPieces, Is.EqualTo(piece));
            void Covered(string sprite, float x)
            {
                var size = Resources.Load<Sprite>(sprite).bounds.size;
                for (int i = 0; i <= 10; i++) for (int j = 0; j <= 10; j++)
                {
                    var at = new Vector2(x - size.x / 2 + size.x * i / 10, size.y * j / 10);
                    float reach = layout.colourBlots.Where(b => b.piece <= piece).Max(b => b.r - Vector2.Distance(at, new Vector2(b.x, b.y)));
                    Assert.That(reach, Is.GreaterThanOrEqualTo(.625f), $"{sprite} at {at} stays sketch after piece {piece}");
                }
            }
            Covered("Hub/buildings/color/" + building.Replace('_', '-'), layout.buildings.Single(b => b.id == building).x);
            Covered("Hub/props/color/palette-bench", layout.stations.Single(s => s.id == station).x);
        }
        static string Bubble(MonoBehaviour gui) => (string)gui.GetType().GetProperty("BubbleShowing").GetValue(gui);
        static string Banner(MonoBehaviour gui) => (string)gui.GetType().GetProperty("BannerShowing").GetValue(gui);
        static int Visit(MonoBehaviour gui) => (int)gui.GetType().GetProperty("VisitPlaying").GetValue(gui);

        IEnumerator Press(Key key, float seconds)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < seconds;) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            for (float at = Time.realtimeSinceStartup; !done() && Time.realtimeSinceStartup - at < seconds;) yield return null;
        }

        // Reads every line that comes up until the visit is over or `until` holds.
        IEnumerator ReadUntil(MonoBehaviour gui, System.Func<bool> until, float seconds)
        {
            for (float at = Time.realtimeSinceStartup; !until() && Time.realtimeSinceStartup - at < seconds;)
            {
                if (Bubble(gui) == null) { yield return null; continue; }
                yield return WaitFor(() => false, Mathf.Min(2, Bubble(gui).Length / 30f + .25f));
                yield return Press(Key.F, .05f);
                yield return WaitFor(() => false, .1f);
            }
        }

        // Each piece brings back colour in blots fixed in the world: around the well first, then wider, then everywhere.
        [UnityTest] public IEnumerator EachPieceBringsBackMoreColourInBlots()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            try
            {
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData
                {
                    profileId = System.Guid.NewGuid().ToString(), chapter1BossFirstClear = true, chapter1Clears = 1,
                    seenStories = new[] { "opening", "artisan-first-run", "artisan-visit-1", "artisan-visit-2", "artisan-visit-3", "artisan-visit-4", "artisan-visit-5",
                        "village-ink-reward-1", "village-ink-reward-2", "village-ink-reward-3", "village-ink-reward-4", "village-ink-reward-5", "village-executioner-note" },
                }));
                StartingVillageWorld village = null;
                for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                gui.GetType().GetField("storyChecked", Private).SetValue(gui, false);
                // The GUI may have read the real editor profile before the test store was in: an opening started from it
                // belongs to that profile, not this one.
                if ((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui)) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
                yield return WaitFor(() => village.ColourPieces == 1, 3);
                Assert.That(village.ColourPieces, Is.EqualTo(1));
                int Twins() => village.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Colour blot" && r.enabled);
                Assert.That(Twins(), Is.GreaterThan(0), "sketched pieces show their colour twin under the blots");
                Assert.That(village.SketchAt(14.5f), Is.False, "the well is coloured by the first piece");
                Assert.That(village.SketchAt(70), Is.True, "the far end is not yet");
                var player = village.World.arena.Player;
                player.Position = new Vector2(14.5f, player.Position.y); Physics2D.SyncTransforms(); village.World.RefreshCamera(0, true);
                yield return null; yield return null;
                StartingVillageCaptureTests.Capture(gui, village, "blots-1");
                var profile = Chapter1ProfileHost.Service;
                for (int i = 0; i < 2; i++) profile.SettleRun(profile.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, profile.ChaptersCleared + 1);
                yield return WaitFor(() => village.ColourPieces == 3, 2);
                yield return null;
                StartingVillageCaptureTests.Capture(gui, village, "blots-3");
                Assert.That(village.SketchAt(64), Is.True, "three pieces still leave the ground past the reclaim workbench sketched");
                for (int i = 0; i < 2; i++) profile.SettleRun(profile.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, profile.ChaptersCleared + 1);
                yield return WaitFor(() => village.ColourPieces == 5, 2);
                Assert.That(village.ColourPieces, Is.EqualTo(5));
                // The fifth piece spreads from the well over the whole village.
                village.BloomPiece(5);
                Assert.That(village.Blooming, Is.True);
                yield return WaitFor(() => false, 1.2f);
                StartingVillageCaptureTests.Capture(gui, village, "blots-5-spreading");
                yield return WaitFor(() => !village.Blooming, 2);
                yield return null;
                Assert.That(village.SketchAt(70), Is.False, "the fifth piece colours the whole village");
                StartingVillageCaptureTests.Capture(gui, village, "blots-5");
                village.SetSketch(true);
                Assert.That(Twins(), Is.Zero, "a preview hides the blots");
                village.ClearPreview();
            }
            finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }

        // After the first run (a death, the artisan not met in the forest): it notices the hero, greets it as a stranger,
        // shows the magic pigment (the chip appears on that line) and opens the supply office.
        [UnityTest] public IEnumerator AfterTheFirstRunTheArtisanShowsThePigmentAndOpensTheSupplyOffice()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                var firstRunProfile = new ProfileData
                {
                    profileId = System.Guid.NewGuid().ToString(), pigment = 14, runsStarted = 1, runsDied = 1, seenStories = new[] { "opening", "first-return" },
                };
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(firstRunProfile));
                StartingVillageWorld village = null;
                for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(village && village.Ready, Is.True);
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                gui.GetType().GetField("storyChecked", Private).SetValue(gui, false);
                if ((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui))
                {
                    gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
                    // Skipping a scene started before the test store was installed must not mark this fixture as seen.
                    Chapter1ProfileHost.UseStore(new MemoryProfileStore(firstRunProfile));
                }
                yield return WaitFor(() => Visit(gui) == 100, 3);
                Assert.That(Visit(gui), Is.EqualTo(100), "the first run's visit plays");
                var supply = village.Find(StartingVillageWorld.SupplyCounter);
                Assert.That(village.HeldClosed, Is.EqualTo(StartingVillageWorld.SupplyCounter));
                Assert.That(village.StationOpen(supply), Is.False, "the office waits for its scene");
                var chip = (GameObject)gui.GetType().GetField("hubCurrency", Private).GetValue(gui);
                Assert.That(chip.activeSelf, Is.False, "no pigment chip before the artisan names it");

                yield return WaitFor(() => Bubble(gui) == "어머, 이 마을에 손님이라니.", 10);
                Assert.That(Bubble(gui), Is.EqualTo("어머, 이 마을에 손님이라니."), "a stranger's greeting: the forest workshop was never reached");
                Assert.That(village.ArtisanX - village.World.arena.Player.Position.x, Is.EqualTo(2.3f).Within(.15f), "it walked over after noticing the hero");
                StartingVillageCaptureTests.Capture(gui, village, "first-run-visit-greet");
                yield return ReadUntil(gui, () => Bubble(gui) == "...", 20);
                Assert.That(chip.activeSelf, Is.True, "the chip appears as the artisan notices the pigment");
                StartingVillageCaptureTests.Capture(gui, village, "first-run-visit-pigment");
                yield return ReadUntil(gui, () => village.Panning, 20);
                yield return WaitFor(() => !village.Panning, 10);
                Assert.That(village.World.arena.Player.Position.x, Is.EqualTo(supply.Position.x - 1.9f).Within(.3f), "the hero walked to the office");
                yield return WaitFor(() => village.StationOpen(supply), 3);
                Assert.That(village.StationOpen(supply), Is.True);
                yield return WaitFor(() => false, .5f);
                StartingVillageCaptureTests.Capture(gui, village, "first-run-visit-supply");
                yield return ReadUntil(gui, () => Visit(gui) == 0, 45);
                yield return WaitFor(() => Visit(gui) == 0, 6);
                Assert.That(Visit(gui), Is.Zero);
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("artisan-first-run"), Is.True);
                Assert.That(village.HeldClosed, Is.Null);
                Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.False);
                yield return WaitFor(() => !village.ArtisanShown, 4);
                Assert.That(village.ArtisanShown, Is.False, "it went back to the forest");
                Assert.That(chip.activeSelf, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                Chapter1ProfileHost.UseStore(null);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator TheFirstPieceBringsTheArtisanWhoOpensThePaletteWorkbench()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                // Back from the first chapter-1 clear, the opening long seen.
                ProfileData Fresh() => new ProfileData
                {
                    profileId = System.Guid.NewGuid().ToString(), pigment = 40, chapter1BossFirstClear = true, chapter1Clears = 1, seenStories = new[] { "opening", "artisan-first-run" },
                };
                Chapter1ProfileHost.UseStore(new MemoryProfileStore(Fresh()));
                StartingVillageWorld village = null;
                for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(village && village.Ready, Is.True);
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                gui.GetType().GetField("storyChecked", Private).SetValue(gui, false);
                // The GUI may have read the real editor profile before the test store was in: an opening started from it
                // belongs to that profile, not this one.
                // Skipping it records that scene (an artisan visit of the editor profile's pieces) in the test profile, so
                // the test starts again from a clean one.
                if ((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui))
                {
                    gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
                    Chapter1ProfileHost.UseStore(new MemoryProfileStore(Fresh()));
                }
                yield return WaitFor(() => Visit(gui) == 1, 3);
                Assert.That(Visit(gui), Is.EqualTo(1), "the first piece's visit plays");
                var bench = village.Find(StartingVillageWorld.PaletteBench);
                Assert.That(village.HeldClosed, Is.EqualTo(StartingVillageWorld.PaletteBench));
                Assert.That(village.StationOpen(bench), Is.False, "the workbench waits for its scene");
                Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.True, "the visit holds the controls");

                // The artisan walks in from the right edge and greets the hero.
                yield return WaitFor(() => Bubble(gui) == "어, 또 보네!", 15);
                Assert.That(Bubble(gui), Is.EqualTo("어, 또 보네!"), "they met after the first run");
                Assert.That(village.ArtisanShown, Is.True); Assert.That(village.ArtisanMoving, Is.False);
                Assert.That(village.ArtisanX - village.World.arena.Player.Position.x, Is.EqualTo(2.3f).Within(.1f));
                // The artisan speaks in the workshop's bubble (name tag 「마을 장인」), the one every speaker uses.
                var workshop = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "MaintenanceBubbleView");
                Assert.That(workshop, Is.Not.Null, "the artisan has the maintenance bubble");
                var speech = workshop.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Speech bubble");
                Assert.That(speech.gameObject.activeInHierarchy, Is.True);
                Assert.That(speech.GetComponentsInChildren<UnityEngine.UI.Text>().First(t => t.name == "Merchant name").text, Is.EqualTo("마을 장인"));
                Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Count(b => b.GetType().Name == "MaintenanceBubbleView"), Is.EqualTo(1), "one bubble for every speaker");
                yield return WaitFor(() => false, .8f);
                StartingVillageCaptureTests.Capture(gui, village, "visit-1-greet");
                Assert.That(village.PaletteShown, Is.False, "village ink restores colour without installing a palette stand");
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("village-ink-return-1"), Is.False, "the artisan explains the unknown ink before it is used");
                Assert.That(village.ColourPieces, Is.Zero);

                // Both walk over to the workbench (no cut) and its lock breaks there.
                yield return ReadUntil(gui, () => village.Panning, 55);
                yield return WaitFor(() => !village.Panning, 8);
                Assert.That(village.World.arena.Player.Position.x, Is.EqualTo(bench.Position.x - 1.9f).Within(.3f), "the hero walked to the bench");
                Assert.That(village.ArtisanX, Is.EqualTo(bench.Position.x + 1.7f).Within(.1f));
                yield return WaitFor(() => (bool)gui.GetType().GetProperty("StationUnlockPlaying").GetValue(gui), 2);
                yield return WaitFor(() => false, .45f);
                StartingVillageCaptureTests.Capture(gui, village, "visit-1-unlock");
                Assert.That(village.StationOpen(bench), Is.True);

                // The rest of the lines, then the artisan walks off and the screen dims for its leaving.
                yield return ReadUntil(gui, () => Visit(gui) == 0, 45);
                yield return WaitFor(() => Visit(gui) == 0, 6);
                Assert.That(Visit(gui), Is.Zero);
                yield return WaitFor(() => (bool)gui.GetType().GetProperty("VillageInkRewardShowing").GetValue(gui), 3);
                // One F closes the ink reward; no notebook follows (user 2026-11-13).
                yield return new WaitForSecondsRealtime(.8f); yield return Press(Key.F,.05f);
                Assert.That((bool)gui.GetType().GetProperty("VillageInkModalShowing").GetValue(gui), Is.False);
                Assert.That(village.ArtisanShown, Is.False, "it left to trade");
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("artisan-visit-1"), Is.True);
                Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.False);
                Assert.That(village.HeldClosed, Is.Null);
                StartingVillageCaptureTests.Capture(gui, village, "visit-1-after");

                // The chapter-2 clear: visit 2 shows the wider supply office, then the artisan goes to hammer at the reclaim
                // workshop's door; holding Esc skips the rest to its end state, the workshop open.
                var profile = Chapter1ProfileHost.Service;
                Assert.That(profile.SettleRun(profile.PlanRun(new[] { "sword", "blunt" }).RunId, RunOutcome.ChapterCleared, false, profile.ChaptersCleared + 1), Is.True);
                yield return WaitFor(() => false, .5f);
                Assert.That(Visit(gui),Is.Zero,"the second ink waits for the well");
                var returnPlayer=village.World.arena.Player;
                returnPlayer.Position=village.Find(StartingVillageWorld.ReturnWell).Position+Vector2.right; returnPlayer.velocity=Vector2.zero;
                Assert.That(village.TryInteract(),Is.True);
                yield return WaitFor(() => (bool)gui.GetType().GetProperty("VillageInkRewardShowing").GetValue(gui),10);
                yield return new WaitForSecondsRealtime(.8f); yield return Press(Key.F,.05f);
                yield return WaitFor(() => Visit(gui) == 2, 3);
                Assert.That(Visit(gui), Is.EqualTo(2));
                Assert.That(village.StationOpen(village.Find(StartingVillageWorld.ReclaimBench)), Is.False, "the logical service waits for its introduction");
                Assert.That(village.Find(StartingVillageWorld.ReclaimBench).Position,
                    Is.EqualTo(village.Find(StartingVillageWorld.ServiceHub).Position));
                yield return ReadUntil(gui, () => (Bubble(gui) ?? "").Contains("회수 작업"), 15);
                StringAssert.Contains("회수 작업", Bubble(gui));
                Assert.That(village.ArtisanBusy, Is.Not.EqualTo("hammer"), "no separate door repair");
                yield return Press(Key.Escape, 1.2f);
                yield return WaitFor(() => Visit(gui) == 0, 2);
                Assert.That(Visit(gui), Is.Zero, "Esc held skips the visit");
                Assert.That(profile.Snapshot.HasSeen("artisan-visit-2"), Is.True);
                Assert.That(village.StationOpen(village.Find(StartingVillageWorld.ReclaimBench)), Is.True);
                Assert.That(village.StationOpen(village.Find(StartingVillageWorld.SupplyCounter)), Is.True);
                Assert.That(village.ArtisanShown, Is.False);
                Assert.That(Banner(gui), Is.Null);
                Assert.That(village.World.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                Chapter1ProfileHost.UseStore(null);
            }
            yield return new ExitPlayMode();
        }
    }
}
