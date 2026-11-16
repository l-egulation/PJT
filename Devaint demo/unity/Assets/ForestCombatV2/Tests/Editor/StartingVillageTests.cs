using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // The starting village is a safe flat lobby: every station is reachable on foot and nothing can be attacked.
    public sealed class StartingVillageTests
    {
        public const string ScenePath = "Assets/Deviant/Hub/Scenes/StartingVillage.unity";
        static string requested;

        [UnityTest] public IEnumerator EveryStationIsReachableOnFootAndTheLobbyHasNoCombat()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True, "the village builds on the lobby world");
            // A fresh test profile plays the first-visit opening; read it to the end so the lobby resumes.
            yield return null; yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
            if (gui)
            {
                const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var active = gui.GetType().GetField("storyActive", Private); var advance = gui.GetType().GetMethod("AdvanceStory", Private);
                gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
                for (int i = 0; i < 30 && (bool)active.GetValue(gui); i++) advance.Invoke(gui, null);
                yield return null;
            }
            // The world picture is shown through a uGUI canvas below the village GUI (as in chapter 1), not by the
            // viewport's own IMGUI draw on top of it.
            var viewport = Object.FindAnyObjectByType<Stage1VillageExperiment.VillagePixelViewport>();
            Assert.That(viewport, Is.Not.Null, "the village renders through the pixel viewport like chapter 1");
            viewport.enableInBatchMode = true; viewport.enabled = true; // batch runs disable the viewport unless asked
            for (int frame = 0; frame < 30 && !viewport.Ready; frame++) yield return null;
            yield return null;
            Assert.That(viewport.ExternalPresentation, Is.True, "the viewport hands its picture to the GUI output");
            var output = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Native world behind GUI");
            Assert.That(output && output.gameObject.activeInHierarchy, Is.True, "the native world canvas exists");
            Assert.That(output.sortingOrder, Is.LessThan(gui.GetComponent<Canvas>().sortingOrder), "the world canvas is drawn behind the GUI");
            Assert.That(output.GetComponentInChildren<UnityEngine.UI.RawImage>().texture, Is.SameAs(viewport.NativeTexture));
            var world = village.World; var arena = world.arena; var player = arena.Player;
            Assert.That(world.villageHub && !world.proceduralRooms, Is.True);
            Assert.That(arena.enemies.Count(e => e), Is.Zero, "no enemies in the village");
            Assert.That(world.FullMap, Is.Null, "no chapter map");
            CollectionAssert.AreEquivalent(new[] { "record_desk", "return_well", "artisan_hub", "departure_sign" }, village.Stations.Select(s => s.Id));
            Assert.That(player.grounded, Is.True, "the player starts on the ground");
            requested = null; village.StationRequested += station => requested = station.Id;

            // No village palette piece yet: the workbenches wait for theirs and do not answer at all.
            var bench = village.Find(StartingVillageWorld.PaletteBench); var spawn = player.Position;
            player.Position = bench.Position; player.velocity = Vector2.zero; Physics2D.SyncTransforms();
            yield return null;
            Assert.That(village.StationOpen(bench), Is.False);
            Assert.That(village.NearestStation.Id, Is.EqualTo(StartingVillageWorld.ServiceHub), "the shared counter can show locked services");
            // Three chapter-1 clears bring three pieces: every workbench opens.
            var profile = Chapter1ProfileHost.Service;
            for (int i = 1; i <= 3; i++) { profile.MarkStorySeen("artisan-visit-" + i); profile.MarkStorySeen("village-ink-reward-" + i); } // visits and rewards have their own tests
            profile.MarkStorySeen("artisan-first-run"); profile.MarkStorySeen("village-executioner-note");
            for (int i = 0; i < 3; i++) Assert.That(profile.SettleRun(profile.PlanRun(new[] { "sword", "blunt" }).RunId, Deviant.Run.RunOutcome.ChapterCleared, false, profile.ChaptersCleared + 1), Is.True);
            yield return null;
            Assert.That(village.Stations.All(village.StationOpen), Is.True);
            Assert.That(village.StationOpen(village.Find(StartingVillageWorld.DepartureSign)), Is.True, "the sign is the departure interaction");
            player.Position = spawn; Physics2D.SyncTransforms();
            yield return null;

            // Walk the lane with real input from the spawn to the far end and back, visiting each station.
            using (var driver = arena.AcquireManualSimulation())
            {
                foreach (var station in village.Stations.OrderBy(s => s.Position.x).Concat(village.Stations.OrderByDescending(s => s.Position.x)))
                {
                    for (int tick = 0; tick < 2400 && Mathf.Abs(player.Position.x - station.Position.x) > .4f; tick++)
                        driver.Step(1f / 120, new CombatInput(player.Position.x < station.Position.x ? 1 : -1));
                    driver.Step(1f / 120);
                    Assert.That(Mathf.Abs(player.Position.x - station.Position.x), Is.LessThanOrEqualTo(.4f), station.Id + " is reachable by walking");
                    Assert.That(player.grounded && Mathf.Abs(player.Position.y - station.Position.y) < .05f, Is.True, station.Id + " stands on the same flat ground");
                    Assert.That(village.NearestStation?.Id, Is.EqualTo(station.Id));
                    yield return null;
                    Assert.That(village.StationKey.Visible, Is.True, station.Id + " shows the F keycap");
                    Assert.That(Mathf.Abs(village.StationKey.transform.position.x - station.Position.x), Is.LessThan(.01f));
                    Assert.That(village.StationKey.transform.position.y, Is.GreaterThan(station.Position.y + .5f), "above the workbench");
                }
                // No attack starts in the lobby.
                driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Basic));
                for (int tick = 0; tick < 30; tick++) driver.Step(1f / 120);
                Assert.That(player.currentCast, Is.Null, "weapons are not usable in the village");
            }
            yield return null;
            Assert.That(village.TryInteract(), Is.True);
            Assert.That(requested, Is.EqualTo(village.NearestStation.Id), "F hands the nearest station to the village screens");
            player.Position = new Vector2(village.Find("return_well").Position.x + 8.5f, player.Position.y);
            Assert.That(village.NearestStation, Is.Null, "between stations nothing is targeted");
            yield return null;
            Assert.That(village.StationKey.Visible, Is.False, "no keycap between stations");
            Chapter1ProfileHost.UseStore(null);
            yield return new ExitPlayMode();
        }
    }
}
