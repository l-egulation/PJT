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
    // The village camera's right edge (docs/design/world/village/starting-village.md, the 44-unit ground and the 84-unit sky
    // path): an ordinary jump in the village keeps the view inside the village ground. Playtest 2026-11-15: a double jump by
    // the departure sign (about 6 units) passed the old "4 units up means the sky" rule, the edge widened to the sky's 84 units
    // and the screen slid sideways in mid-air.
    public sealed class StartingVillageCameraEdgeTests
    {
        const float Dt = 1f / 120;
        const string SkyDataAsset = "Assets/Deviant/Hub/Resources/HubSkyParkour.json";

        [Test] public void OrdinaryJumpHeightsKeepTheVillageEdgeAndTheSkyPathWidensIt()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SkyDataAsset);
            Assert.That(text, Is.Not.Null, SkyDataAsset);
            var sky = JsonUtility.FromJson<StartingVillageWorld.SkyData>(text.text);
            const float ground = 44;
            float lowest = sky.platforms.Min(p => p.y);
            Assert.That(sky.worldWidth, Is.GreaterThan(ground), "the sky path is wider than the village ground");

            foreach (float height in new[] { 0f, 3f, 4.5f, 6f, 6.5f })   // standing, one jump, the old rule's line, double jumps
                Assert.That(StartingVillageWorld.CameraRightUnits(sky, ground, false, false, height), Is.EqualTo(ground), $"a jump to {height} units keeps the village edge");
            Assert.That(StartingVillageWorld.CameraRightUnits(sky, ground, false, false, lowest), Is.EqualTo(sky.worldWidth), "born on the start stone (before the run's first step)");
            Assert.That(StartingVillageWorld.CameraRightUnits(sky, ground, false, false, 30), Is.EqualTo(sky.worldWidth), "a zone spawn above the clouds");
            Assert.That(StartingVillageWorld.CameraRightUnits(sky, ground, true, false, 0), Is.EqualTo(sky.worldWidth), "a run under way, even as the hero falls to the ground");
            Assert.That(StartingVillageWorld.CameraRightUnits(sky, ground, false, true, 2), Is.EqualTo(sky.worldWidth), "a cleared summit not yet left");
            Assert.That(StartingVillageWorld.CameraRightUnits(null, ground, true, true, 100), Is.EqualTo(ground), "no sky path: always the village");
        }

        [UnityTest] public IEnumerator DoubleJumpByTheDepartureSignNeverSlidesTheView()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            yield return null; yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
            Assert.That(gui, Is.Not.Null, "the village GUI is built");
            {
                const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var active = gui.GetType().GetField("storyActive", Private); var advance = gui.GetType().GetMethod("AdvanceStory", Private);
                gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
                for (int i = 0; i < 30 && (bool)active.GetValue(gui); i++) advance.Invoke(gui, null);
                yield return null;
            }
            // Skipping the opening may rebuild the village: find it again and wait for its player.
            for (int frame = 0; frame < 300; frame++)
            {
                village = Object.FindAnyObjectByType<StartingVillageWorld>();
                if (village && village.Ready && village.World && village.World.arena && village.World.arena.Player) break;
                yield return null;
            }
            Assert.That(village && village.World.arena.Player, Is.True, "the village and its player are ready");
            for (int frame = 0; frame < 30; frame++) yield return null;
            Assert.That(village.SkyBuilt, Is.True, "the sky path is built with the village");
            var world = village.World; var player = world.arena.Player;
            float ground = player.Position.y, villageEdge = village.GroundWidth * 32, groundMaxX = villageEdge - 960;
            float lowestStone = village.Sky.platforms.Min(p => p.y);

            // Right of the departure sign, where the ground view already rests on the village's right edge.
            float signX = village.Find(StartingVillageWorld.DepartureSign).Position.x;
            float standX = Mathf.Min(village.GroundWidth - 1.5f, signX + 6);
            player.Position = new Vector2(standX, ground); player.velocity = Vector2.zero; Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            yield return null;
            world.RefreshCamera(0, true);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(world.CameraX, Is.EqualTo(groundMaxX).Within(.5f), "on the ground the view rests on the village's right edge");

            float peak = 0, widest = 0, rightmost = float.NegativeInfinity;
            bool landed = false;
            using (var driver = world.arena.AcquireManualSimulation())
            {
                driver.Step(Dt, new CombatInput(0, CombatButtons.Jump));
                bool doubled = false;
                for (int tick = 0; tick < 360; tick++)
                {
                    var pressed = CombatButtons.None;
                    if (!doubled && tick > 4 && player.velocity.y < 2) { pressed = CombatButtons.Jump; doubled = true; }
                    driver.Step(Dt, new CombatInput(0, pressed));
                    yield return null;   // the village's Update sets the edge, the world's LateUpdate moves the camera
                    peak = Mathf.Max(peak, player.Position.y - ground);
                    widest = Mathf.Max(widest, world.CameraRightPixels ?? 0);
                    rightmost = Mathf.Max(rightmost, world.CameraX);
                    if (tick > 4 && player.grounded) { landed = true; break; }
                }
            }
            Assert.That(landed, Is.True, "the double jump comes back to the ground");
            Assert.That(peak, Is.GreaterThan(4), "the double jump climbs past the old 4-unit sky rule (the reported case)");
            Assert.That(peak, Is.LessThan(lowestStone - 1), "and stays far below the sky's lowest stone");
            Assert.That(widest, Is.EqualTo(villageEdge), "the camera edge stays the village's own through the jump");
            Assert.That(rightmost, Is.LessThanOrEqualTo(groundMaxX + .5f), "the view never slides past the village's right edge");
            Chapter1ProfileHost.UseStore(null);
            yield return new ExitPlayMode();
        }
    }
}
