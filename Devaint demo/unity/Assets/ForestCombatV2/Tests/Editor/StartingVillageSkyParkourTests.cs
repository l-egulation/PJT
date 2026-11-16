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
    // 하늘길 (docs/design/world/village/starting-village.md): no sky stone shows over the village; F at the farmhouse door puts the
    // hero on the start stone above the screen and the run's clock starts there, the steps climb to the clouds with ordinary jumps, an arrow pushes 0.9 units without harm, a crumbling stone gives
    // way, a moving board carries the hero, and the summit writes a record that the board reads.
    public sealed class StartingVillageSkyParkourTests
    {
        const float Dt = 1f / 120;

        static void Stand(CombatPlayer player, Vector2 at)
        {
            player.Position = at; player.velocity = Vector2.zero; Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
        }

        // One jump toward a platform's middle (optionally a double jump at the top), true when it lands on that top.
        static bool Hop(CombatArena.ManualSimulation driver, CombatPlayer player, StartingVillageWorld.SkyPlatform to, float groundTop, bool second)
        {
            float centre = to.x + to.w / 2; bool doubled = false; var trace = new System.Text.StringBuilder($"hop to ({to.x},{to.y}) from {player.Position - new Vector2(0, groundTop)} g={player.grounded}:");
            int Axis() => Mathf.Abs(centre - player.Position.x) < .25f ? 0 : player.Position.x < centre ? 1 : -1;
            driver.Step(Dt, new CombatInput(Axis(), CombatButtons.Jump));
            for (int tick = 0; tick < 360; tick++)
            {
                var pressed = CombatButtons.None;
                if (second && !doubled && tick > 4 && player.velocity.y < 2) { pressed = CombatButtons.Jump; doubled = true; }
                driver.Step(Dt, new CombatInput(Axis(), pressed));
                if (tick % 8 == 0) trace.Append($" {player.Position.x:0.0},{player.Position.y - groundTop:0.0}{(player.grounded ? "g" : "")}");
                if (tick > 4 && player.grounded)
                {
                    bool ok = Mathf.Abs(player.Position.y - (groundTop + to.y)) < .1f;   // the motor stands about .05 above a top
                    if (!ok) Debug.Log("SKYHOP " + trace + " landed " + (player.Position.y - groundTop));
                    return ok;
                }
            }
            Debug.Log("SKYHOP " + trace + " timeout");
            return false;
        }

        [UnityTest] public IEnumerator DoorRoofStepsRunArrowsCrumbleBoardAndRecord()
        {
            // EnterPlayMode must be yielded by the test itself (a nested enumerator does not enter play mode).
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
            for (int frame = 0; frame < 30; frame++) yield return null;   // as StartingVillageTests: let the lobby settle
            Assert.That(village.SkyBuilt, Is.True, "the sky path is built with the village");
            var sky = village.Sky; var world = village.World; var arena = world.arena; var player = arena.Player;
            Assert.That(sky, Is.Not.Null, "the course data is loaded");
            float ground = player.Position.y;   // the hero starts on the village ground (GroundTop)
            Assert.That(world.CameraTopPixels, Is.LessThan(-(ground + sky.goalY) * 32), "the camera can rise to the bell tower");

            // Before the first run the door is only a door: no band, no F.
            Stand(player, new Vector2(sky.doorX, ground));
            for (int frame = 0; frame < 20; frame++) yield return null;
            Assert.That(village.SkyNear, Is.EqualTo(StartingVillageWorld.SkyTarget.None), "the way up waits for the first run");
            Assert.That(village.StationKey.Visible, Is.False, "no keycap at the door before the first run");
            Assert.That(village.TryInteract(), Is.False);
            var loadout = Chapter1ProfileHost.Service.Catalog.Data.startLoadout.availableBlueprints;
            Chapter1ProfileHost.Service.PlanRun(new[] { loadout[0], loadout[1] });

            // The door: F at the farmhouse door (no station there) fades and stands the hero on the start stone.
            Stand(player, new Vector2(sky.doorX, ground));
            yield return null;
            Assert.That(village.SkyNear, Is.EqualTo(StartingVillageWorld.SkyTarget.Door));
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(village.StationKey.Visible, Is.True, "the door shows the F keycap when the hero is near");
            Assert.That(Mathf.Abs(village.StationKey.transform.position.x - sky.doorX), Is.LessThan(.01f), "over the door");
            Assert.That(village.InteractionAllowed, Is.True, "no village screen holds the interaction at the door");
            // No sky stone shows over the village (playtest 2026-11-13: the start stone peeking in at the top looked odd);
            // the door leads straight up to the start stone, above the screen.
            var start = sky.platforms.Single(p => p.kind == "start");
            for (int frame = 0; frame < 10; frame++) yield return null;
            float viewTop = world.View.transform.position.y + world.View.orthographicSize;
            bool allHidden = true;   // a loop, not a lambda: lambdas over this iterator's locals break it (see Hop)
            foreach (var stone in sky.platforms) if (ground + stone.y - 1 <= viewTop) allHidden = false;
            Assert.That(allHidden, Is.True, "no sky stone shows over the village");
            StartingVillageCaptureTests.Capture(gui, village, "sky-01-door");
            // A real F press goes through the village's input, the door fade and the move (as a player would).
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>(); keyboard.MakeCurrent();
            try
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F));
                for (float until = Time.realtimeSinceStartup + .05f; Time.realtimeSinceStartup < until;) yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState()); yield return null;
                for (float until = Time.realtimeSinceStartup + .12f; Time.realtimeSinceStartup < until;) yield return null;
                Assert.That(player.Position.y, Is.LessThan(ground + 1), "the hero waits in the door's fade");
                StartingVillageCaptureTests.Capture(gui, village, "sky-01b-door-fade");
                for (float until = Time.realtimeSinceStartup + 2; Time.realtimeSinceStartup < until && player.Position.y < ground + 5;) yield return null;
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput; }
            Assert.That(player.Position.y, Is.EqualTo(ground + start.y).Within(.1f), "F at the door leads onto the start stone");
            Assert.That(player.Position.x, Is.InRange(start.x, start.x + start.w), "on the start stone");
            // The fade lifts in 0.32 s of real time.
            for (float until = Time.realtimeSinceStartup + .5f; Time.realtimeSinceStartup < until;) yield return null;
            Assert.That(village.SkyRunActive, Is.True, "born on the start stone, the run starts there");
            StartingVillageCaptureTests.Capture(gui, village, "sky-02-start");

            using (var driver = arena.AcquireManualSimulation())
            {
                // Start stone → steps → the first two cloud stones, each with an ordinary jump (as the HTML prototype).
                var chain = sky.platforms.Where(p => p.kind == "step").OrderBy(p => p.y).ToList();
                chain.AddRange(sky.platforms.Where(p => p.kind == "stone" && p.y >= 30).OrderBy(p => p.y).Take(2));
                foreach (var to in chain)
                {
                    bool landed = Hop(driver, player, to, ground, false) || Hop(driver, player, to, ground, true);
                    Assert.That(landed, Is.True, $"reaches the stone at ({to.x}, {to.y})");
                }
                Assert.That(village.SkyClock, Is.GreaterThan(0));

                // An arrow pushes the hero aside 0.9 units over 0.18 s and counts once.
                var shooter = sky.launchers.First(l => l.edge && l.dir > 0);
                StartingVillageWorld.SkyPlatform pad = null;
                foreach (var p in sky.platforms)
                    if (p.kind == "stone" && Mathf.Abs(p.y + 1 - shooter.y) < 1.2f && (pad == null || p.x < pad.x)) pad = p;
                Stand(player, new Vector2(pad.x + pad.w / 2, ground + pad.y));
                int hits = village.SkyHits; float x0 = player.Position.x; float pushed = 0;
                for (int tick = 0; tick < 120 * 8 && village.SkyHits == hits; tick++) { driver.Step(Dt); x0 = village.SkyHits == hits ? player.Position.x : x0; }
                Assert.That(village.SkyHits, Is.EqualTo(hits + 1), "the launcher's arrow reached the hero");
                for (int tick = 0; tick < 40; tick++) driver.Step(Dt);
                pushed = player.Position.x - x0;
                Assert.That(pushed, Is.EqualTo(.9f).Within(.25f), "pushed like a monster's hit, the arrow's way");
                Assert.That(player.Dead, Is.False);

                // A crumbling stone gives way under the hero after its shake.
                var crumble = sky.platforms.First(p => p.kind == "crumble" && p.w >= 2);
                Stand(player, new Vector2(crumble.x + crumble.w / 2, ground + crumble.y));
                for (int tick = 0; tick < 120; tick++) driver.Step(Dt);
                Assert.That(player.Position.y, Is.LessThan(ground + crumble.y - .5f), "the crumbled stone no longer holds the hero");

                // A moving board carries the hero standing on it.
                var lift = sky.movers.First(m => Mathf.Abs(m.x1 - m.x0) > 1);
                for (int tick = 0; tick < 2; tick++) driver.Step(Dt);
                // Find the board where it is now (its art root) and stand on its middle.
                // A plain loop: a lambda here would capture locals of this iterator (ground) into a closure.
                Transform board = null;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name == "Sky moving board" && (!board || Mathf.Abs(t.position.y - (ground + lift.y0)) < Mathf.Abs(board.position.y - (ground + lift.y0)))) board = t;
                Stand(player, new Vector2(board.position.x + lift.w / 2, board.position.y));
                float startX = player.Position.x, boardX = board.position.x;
                for (int tick = 0; tick < 60; tick++) driver.Step(Dt);
                Assert.That(player.Position.x - startX, Is.EqualTo(board.position.x - boardX).Within(.1f), "the board carries the hero");

                // The summit: landing on the goal stone ends the run and writes its record.
                var goal = sky.platforms.Single(p => p.kind == "goal");
                Stand(player, new Vector2(goal.x + 1, ground + goal.y + .02f));
                for (int tick = 0; tick < 10; tick++) driver.Step(Dt);
                Assert.That(village.SkyCleared && !village.SkyRunActive, Is.True, "the top ends the run");
                Assert.That(village.SkyCelebrating, Is.True, "the arrival is celebrated");
            }
            var records = Chapter1ProfileHost.Service.Snapshot.SkyParkourRecords;
            Assert.That(records.Count, Is.EqualTo(1), "the record is saved");
            Assert.That(records[0].hits, Is.GreaterThanOrEqualTo(1));
            world.RefreshCamera(0, true);
            // The bell tower stands on its stone: its picture's foot is within the stone's width.
            var towerArt = GameObject.Find("Sky bell tower").GetComponent<SpriteRenderer>();
            var summit = sky.platforms.Single(p => p.kind == "goal");
            Assert.That(towerArt.bounds.center.x, Is.EqualTo(summit.x + summit.w / 2).Within(.6f), "the tower is centred on its stone");
            // Fireworks and confetti (unscaled time), then the welcome bubble.
            for (float until = Time.unscaledTime + .9f; Time.unscaledTime < until;) yield return null;
            StartingVillageCaptureTests.Capture(gui, village, "sky-03-party");
            for (float until = Time.unscaledTime + 1.2f; Time.unscaledTime < until;) yield return null;
            StartingVillageCaptureTests.Capture(gui, village, "sky-03-cleared");
            // The summit board opens the records.
            Stand(player, new Vector2(sky.summitBoardX, ground + sky.platforms.Single(p => p.kind == "goal").y));
            yield return null;
            Assert.That(village.SkyNear, Is.EqualTo(StartingVillageWorld.SkyTarget.SummitBoard));
            Assert.That(village.TryInteract(), Is.True);
            yield return null; yield return null;
            StartingVillageCaptureTests.Capture(gui, village, "sky-04-board");
            gui.GetType().GetMethod("CloseHubPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(gui, null);
            // The sky: clouds at the cloud path, an ink night with stars near the top.
            village.PlaceInSky(1); for (int frame = 0; frame < 20; frame++) yield return null;
            StartingVillageCaptureTests.Capture(gui, village, "sky-05-clouds");
            // The floating walls are built of the combat lawn tiles: every cell of every wall is drawn, plus its foot row.
            // (Loops, not lambdas: lambdas over this iterator's locals break it.)
            int wallGroups = 0, drawn = 0, cells = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t.name != "Sky wall art") continue;
                wallGroups++;
                foreach (var r in t.GetComponentsInChildren<SpriteRenderer>()) if (r.enabled && r.sprite && r.sprite.texture) drawn++;
            }
            foreach (var w in sky.walls) cells += Mathf.RoundToInt(w.w) * Mathf.RoundToInt(w.h) + Mathf.RoundToInt(w.w);
            Assert.That(wallGroups, Is.EqualTo(sky.walls.Length), "one tile group per floating wall");
            Assert.That(drawn, Is.EqualTo(cells), "every floating wall cell is drawn");
            // The 2x2-dot pictures: the pixel bell tower, the summit's own record board and single-tile crumbling frames.
            Assert.That(towerArt.sprite.name, Is.EqualTo("bell-tower"));
            Assert.That(GameObject.Find("Sky summit record board").GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("summit-record-board"));
            int singleFrames = 0, singles = 0;
            foreach (var p in sky.platforms) if (p.kind == "crumble" && Mathf.RoundToInt(p.w) % 2 == 1) singles++;
            foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
                if (r.name == "Sky crumbling stone" && r.sprite && r.sprite.name.StartsWith("crumbling-1tile-0")) singleFrames++;
            Assert.That(singleFrames, Is.EqualTo(singles), "every single tile or odd end of a crumbling stone has its own frames");
            village.PlaceInSky(3); for (int frame = 0; frame < 20; frame++) yield return null;
            StartingVillageCaptureTests.Capture(gui, village, "sky-05-walls");
            village.PlaceInSky(4); for (int frame = 0; frame < 20; frame++) yield return null;
            StartingVillageCaptureTests.Capture(gui, village, "sky-06-dusk");
            Stand(player, new Vector2(sky.summitBoardX - 3, ground + sky.platforms.Single(p => p.kind == "goal").y)); world.RefreshCamera(0, true);
            for (int frame = 0; frame < 20; frame++) yield return null;
            StartingVillageCaptureTests.Capture(gui, village, "sky-07-night");
            // Back on the village ground the finished run is over.
            Stand(player, new Vector2(75, ground));
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(village.SkyCleared || village.SkyRunActive, Is.False);
            Assert.That(player.Position.x, Is.LessThan(village.GroundWidth), "landing beyond the compact ground returns safely to the well");
            Chapter1ProfileHost.UseStore(null);
            yield return new ExitPlayMode();
        }
    }
}
