using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The boss room and the installed portal have their own icons on both maps (playtest 2026-11-03), apart from the
    // red enemy dots.
    public sealed class Chapter1MapIconTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static List<Image> Icons(MonoBehaviour gui, string view)
        {
            var map = gui.GetType().GetField(view, Hidden).GetValue(gui);
            if(view=="fullMap")
            {
                var clip=(RectTransform)map.GetType().GetField("Clip").GetValue(map);
                return clip.GetComponentsInChildren<Image>().Where(i=>i.transform.parent.name.StartsWith("Room ")||i.transform.parent.name.StartsWith("Portal ")).ToList();
            }
            return (List<Image>)map.GetType().GetField("Icons").GetValue(map);
        }

        [UnityTest] public IEnumerator NumberedPortalsKeepTheirIdentityWhenEitherPositionChanges()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready); yield return null; yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            var player = world.arena.Player; var map = world.FullMap;
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            player.maxHealth = player.health = 100000;
            map.SetOpen(true);
            // The map consumes committed portal state, including a single installed endpoint.
            var installed = (bool[])typeof(Deviant.Combat.CombatPlayer).GetField("portalInstalled", Hidden).GetValue(player);
            var positions = (Vector2[])typeof(Deviant.Combat.CombatPlayer).GetField("portalPositions", Hidden).GetValue(player);
            positions[0] = player.Position + Vector2.left * 5;
            positions[1] = player.Position + Vector2.right * 5;
            installed[0] = true;
            void Refresh()
            {
                gui.GetType().GetField("nextMiniMarkers", Hidden).SetValue(gui, 0d);
                gui.GetType().GetMethod("Refresh").Invoke(gui, null);
            }
            Image Pin(string view, int slot) => Icons(gui, view).Single(i => i.gameObject.activeSelf && i.name == (slot + 1) + "번 포탈");
            Vector2 PinPosition(string view,int slot)=>view=="fullMap"?((RectTransform)Pin(view,slot).transform.parent).anchoredPosition:Pin(view,slot).rectTransform.anchoredPosition;
            int CountPins(string view) => Icons(gui, view).Count(i => i.gameObject.activeSelf && (i.name == "1번 포탈" || i.name == "2번 포탈"));
            Refresh();
            Assert.That(CountPins("fullMap"), Is.EqualTo(1));
            Assert.That(CountPins("miniMap"), Is.EqualTo(1));
            installed[1] = true; Refresh();
            Assert.That(CountPins("fullMap"), Is.EqualTo(2));
            Assert.That(CountPins("miniMap"), Is.EqualTo(2));
            Assert.That(Pin("fullMap", 0).sprite, Is.Not.SameAs(Pin("fullMap", 1).sprite), "different numbered sprites, independent of colour");
            Assert.That(Pin("fullMap", 0).sprite.name, Does.EndWith("portal-1"), "full map uses the approved numbered source");
            var full = gui.GetType().GetField("fullMap", Hidden).GetValue(gui);
            var clip = (RectTransform)full.GetType().GetField("Clip").GetValue(full);
            Assert.That(clip.GetComponentsInChildren<Text>().Any(t => t.text == "1번 포탈" || t.text == "2번 포탈"), Is.False, "names belong in the legend, not on the terrain");
            Assert.That(Pin("fullMap", 0).rectTransform.sizeDelta, Is.EqualTo(new Vector2(25, 25)));
            Assert.That(Pin("fullMap", 1).rectTransform.sizeDelta, Is.EqualTo(new Vector2(25, 25)));
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "portal-map-numbered");
            var oneBefore = PinPosition("fullMap",0);
            var twoBefore = PinPosition("fullMap",1);
            var miniTwoBefore = PinPosition("miniMap",1);
            positions[0] += Vector2.left * 3; Refresh();
            Assert.That(PinPosition("fullMap",0), Is.Not.EqualTo(oneBefore), "reinstalling 1 moves its marker");
            Assert.That(PinPosition("fullMap",1), Is.EqualTo(twoBefore), "2 keeps its own location");
            Assert.That(PinPosition("miniMap",1), Is.EqualTo(miniTwoBefore));
            oneBefore = PinPosition("fullMap",0);
            positions[1] += Vector2.right * 3; Refresh();
            Assert.That(PinPosition("fullMap",1), Is.Not.EqualTo(twoBefore), "reinstalling 2 moves its marker");
            Assert.That(PinPosition("fullMap",0), Is.EqualTo(oneBefore));
            Assert.That(CountPins("fullMap"), Is.EqualTo(2), "no stale markers at the old positions");
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "portal-map-reinstalled");
            map.SetOpen(false); Refresh();
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "portal-minimap-numbered");
            player.ClearCheckpoint(); map.SetOpen(true); Refresh();
            Assert.That(CountPins("fullMap"), Is.Zero); Assert.That(CountPins("miniMap"), Is.Zero);
            map.SetOpen(false);
        }

        [UnityTest] public IEnumerator BossRoomAndInstalledPortalShowTheirOwnIcons()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready); yield return null; yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            var map = world.FullMap; var player = world.arena.Player;
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            player.maxHealth = player.health = 100000;

            // Nothing yet: the boss room is undiscovered and no portal stands.
            var boss = world.Data.rooms.First(r => r.kind == "boss");
            Assert.That(map.IsRoomDiscovered(boss.id), Is.False);
            gui.GetType().GetMethod("Refresh").Invoke(gui, null); yield return null;
            Assert.That(Icons(gui, "miniMap").Count(i => i.gameObject.activeSelf), Is.EqualTo(0), "no icons before the boss room is found or a portal is set");

            // Walk the boss room into view and set a portal where the hero stood before.
            var stood = player.Position;
            player.Position = new Vector2(boss.x + boss.w / 2f, -boss.y - boss.h / 2f); Physics2D.SyncTransforms();
            map.SendMessage("Update"); yield return null;
            Assert.That(map.IsRoomDiscovered(boss.id), Is.True);
            player.Position = stood; Physics2D.SyncTransforms(); map.SendMessage("Update");
            ((bool[])typeof(Deviant.Combat.CombatPlayer).GetField("portalInstalled", Hidden).GetValue(player))[0] = true;
            ((Vector2[])typeof(Deviant.Combat.CombatPlayer).GetField("portalPositions", Hidden).GetValue(player))[0] = stood + Vector2.left * 2;

            map.SetOpen(true); yield return null; gui.GetType().GetMethod("Refresh").Invoke(gui, null); yield return new WaitForSecondsRealtime(.15f); yield return null;
            var full = Icons(gui, "fullMap").Where(i => i.gameObject.activeSelf).Select(i => i.sprite.name.Length > 0 ? i.sprite : i.sprite).ToArray();
            Assert.That(full.Length, Is.GreaterThanOrEqualTo(2), "the boss and portal icons (and a found shop's) on the full map");
            Assert.That(full.Distinct().Count(), Is.EqualTo(full.Length), "every kind of icon differs");
            Assert.That(Icons(gui, "fullMap").Where(i => i.gameObject.activeSelf).All(i => i.rectTransform.sizeDelta == Vector2.one * 25), Is.True);
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "map-icons-full");
            map.SetOpen(false); yield return new WaitForSecondsRealtime(.15f); yield return null;
            Assert.That(Icons(gui, "miniMap").Count(i => i.gameObject.activeSelf), Is.GreaterThanOrEqualTo(1), "the nearby portal shows on the minimap");
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "map-icons-mini");
        }

        // Opening the map frames the explored strip, not the whole chapter (playtest 2026-11-04).
        [UnityTest] public IEnumerator OpeningTheMapFramesTheExploredArea()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready); yield return null; yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            var map = world.FullMap; var well = new Vector2(595, 340);
            var explored = map.ExploredBounds;
            Assert.That(explored.width * explored.height, Is.LessThan(map.Viewport.Size.x * map.Viewport.Size.y * .5f), "a fresh run has explored a small part");
            map.SetOpen(true); yield return null; gui.GetType().GetMethod("Refresh").Invoke(gui, null); yield return null;
            Assert.That(map.Viewport.Zoom, Is.GreaterThan(1.5f), "zoomed in on what is known");
            var a = map.Viewport.ToScreen(explored.min, well); var b = map.Viewport.ToScreen(explored.max, well);
            Assert.That(a.x >= -1 && a.y >= -1 && b.x <= well.x + 1 && b.y <= well.y + 1, Is.True, $"explored area inside the well: {a} {b}");
            Assert.That(Mathf.Max(b.x - a.x, b.y - a.y) / Mathf.Max(well.x, well.y), Is.GreaterThan(.4f), "and it fills a good part of it");
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "map-explored-fit");
            map.Viewport.Fit(); gui.GetType().GetMethod("FitMapExplored", Hidden).Invoke(gui, null);
            Assert.That(map.Viewport.Zoom, Is.GreaterThan(1.5f), "전체 보기 frames the explored area too");
            map.SetOpen(false); yield return null;
        }
    }

    public sealed class BossMapMarkerTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator OpenScene()
        {
            bool secondChapter = TestContext.CurrentContext.Test.MethodName.StartsWith("Chapter2");
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(secondChapter ? "Assets/Chapter2/Scenes/Chapter2Procedural.unity" : Chapter1MapSelector.ProceduralScene);
            if (secondChapter) Object.FindAnyObjectByType<Deviant.Chapter2.Chapter2World>().seed = 17;
            else Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator Chapter1BossSkullDisappearsOnDefeatAndReturnsOnReset()
        { yield return CheckBossMarker(false); }

        [UnityTest] public IEnumerator Chapter2BossSkullDisappearsOnlyAfterTheFinalPhase()
        { yield return CheckBossMarker(true); }

        static IEnumerator CheckBossMarker(bool secondChapter)
        {
            Deviant.Combat.CombatArena arena = null; Chapter1FullMap map = null;
            for (int i = 0; i < 900 && !arena; i++)
            {
                var forest = Object.FindAnyObjectByType<ForestCombatWorld>();
                var waterway = Object.FindAnyObjectByType<Deviant.Chapter2.Chapter2World>();
                if (secondChapter && waterway && waterway.Ready) { arena = waterway.arena; map = waterway.FullMap; }
                if (!secondChapter && forest && forest.Ready) { arena = forest.arena; map = forest.FullMap; }
                if (!arena) yield return null;
            }
            Assert.That(arena && map, Is.True); yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            using (arena.AcquireManualSimulation())
            {
                var room = map.MapData.rooms.Single(r => r.kind == "boss");
                Assert.That(map.IsRoomDiscovered(room.id), Is.False);
                Paint(); AssertSkulls(0);
                var stood = arena.Player.Position;
                var waterway = Object.FindAnyObjectByType<Deviant.Chapter2.Chapter2World>();
                var revealed = secondChapter ? waterway.Layout.Room("B").Bounds.center : new Vector2(room.x + room.w / 2f, -room.y - room.h / 2f);
                arena.Player.Position = revealed;
                map.SendMessage("Update"); arena.Player.Position = stood;
                Assert.That(map.IsRoomDiscovered(room.id), Is.True);
                Paint(); AssertSkulls(1);
                // A live ordinary enemy must keep its red dot, even when the boss icon disappears.
                var ordinary = arena.enemies.First(e => e && !e.isBoss && !e.Dead);
                ordinary.Position = revealed;
                Assert.That(map.TryGetEnemyMapPoint(ordinary, out _), Is.True, "ordinary enemy is inside the discovered boss room");
                Paint(); int miniEnemies = Count("miniMap", "Marks"), fullEnemies = Count("fullMap", "Marks");
                Assert.That(miniEnemies, Is.GreaterThan(0)); Assert.That(fullEnemies, Is.GreaterThan(0));
                if (secondChapter)
                {
                    var encounter = waterway.Boss;
                    encounter.BeginFight(); encounter.StartTestPhase(2);
                    Assert.That(encounter.Machine.Dead, Is.True);
                    Paint(); AssertSkulls(1); // Defeating the machine is not the end of the encounter.
                    // Finish phase 2 and its absorption cutscene through the encounter's real transitions.
                    encounter.Rose.health = 0; encounter.Step(1f / 120);
                    encounter.Step(2);
                    encounter.Step(3);
                    encounter.Blue.health = 0; encounter.Step(1f / 120);
                    Assert.That(encounter.Dead, Is.True);
                }
                else
                {
                    var boss = Object.FindAnyObjectByType<ForestCombatWorld>().GetComponent<Chapter1BossEncounter>().Boss;
                    boss.health = 0; Assert.That(boss.Dead, Is.True);
                }
                Paint(); AssertSkulls(0);
                Assert.That(Count("miniMap", "Marks"), Is.GreaterThan(0));
                Assert.That(Count("fullMap", "Marks"), Is.GreaterThan(0));
                if (!secondChapter)
                {
                    Object.FindAnyObjectByType<ForestCombatWorld>().GetComponent<Chapter1BossEncounter>().Boss.ResetEnemy();
                    Paint(); AssertSkulls(1);
                }
                map.SetOpen(false);
            }
            void Paint()
            {
                map.SetOpen(true);
                gui.GetType().GetField("nextMiniMarkers", Hidden).SetValue(gui, 0d);
                gui.GetType().GetMethod("Refresh").Invoke(gui, null);
            }
            int Count(string view, string list)
            {
                var data = gui.GetType().GetField(view, Hidden).GetValue(gui);
                if(view=="fullMap")
                {
                    var clip=(RectTransform)data.GetType().GetField("Clip").GetValue(data);
                    return list=="Icons"?clip.GetComponentsInChildren<Image>().Count(i=>i.transform.parent.name.StartsWith("Room ")&&i.transform.parent.name.EndsWith(" boss")):
                        clip.GetComponentsInChildren<RectTransform>().Count(t=>t.name=="Living enemy");
                }
                return ((List<Image>)data.GetType().GetField(list).GetValue(data)).Count(i => i.gameObject.activeSelf && (list != "Icons" || (i.sprite.texture.name == "Map icon boss" || i.sprite.name.EndsWith("boss-crimson"))));
            }
            void AssertSkulls(int count)
            { Assert.That(Count("miniMap", "Icons"), Is.EqualTo(count), "minimap boss skull"); Assert.That(Count("fullMap", "Icons"), Is.EqualTo(count), "full map boss skull"); }
        }
    }
}
