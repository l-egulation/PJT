using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class GameIntegrationTests
    {
        CombatArena arena;
        [UnitySetUp] public IEnumerator SetUp()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            // Structural integration runs without external PNG packages; art loading has a separate check.
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode();
            yield return null;
            yield return new WaitForFixedUpdate();
            arena = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(arena, Is.Not.Null);
            foreach(var enemy in arena.enemies) enemy.aggressive = false;
            arena.Player.UseTestInput = true;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator PauseStopsClockAndResetPreservesCommonPlayer()
        {
            var composition = arena.GetComponent("GameComposition");
            var hud = (MonoBehaviour)composition.GetType().GetField("hud").GetValue(composition);
            var player = arena.Player;
            hud.SendMessage("TogglePause");
            float clock = arena.Now;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(arena.Now, Is.EqualTo(clock));
            Assert.That(arena.Player.InputBlocked, Is.True);
            hud.SendMessage("TogglePause");
            yield return null;
            arena.ResetTest();
            Assert.That(arena.Player, Is.SameAs(player));
            Assert.That(arena.Player.health, Is.EqualTo(100));
            float deadline = Time.realtimeSinceStartup + 5;
            while (arena.Now <= clock)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Combat must resume after pause");
                yield return null;
            }
        }
        [UnityTest] public IEnumerator RecycledTerrainStillSupportsCommonPlayer()
        {
            var composition = arena.GetComponent("GameComposition");
            var forest = (MonoBehaviour)composition.GetType().GetField("forest").GetValue(composition);
            var data = forest.GetType().GetField("data").GetValue(forest);
            foreach(float x in new[] { 520f, 4.6875f })
            {
                float floor = (float)data.GetType().GetMethod("FloorTop").Invoke(data,new object[] { x });
                forest.SendMessage("EnsureChunks",x);
                arena.Player.Position = new Vector2(x,floor+.03f);
                arena.Player.velocity = Vector2.zero;
                Physics2D.SyncTransforms();
                float start = arena.Now, target = start + 10 * Time.fixedDeltaTime;
                float deadline = Time.realtimeSinceStartup + 5;
                while (arena.Now < target)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Terrain probe needs actual simulation ticks.");
                    yield return null;
                }
                Assert.That(arena.Player.grounded, Is.True,
                    $"x={x}, floor={floor}, position={arena.Player.Position}, simulated={arena.Now - start}");
            }
        }
        [UnityTest] public IEnumerator SharedPlayerMovesOnForestAndHudReadsRealHealth()
        {
            Assert.That(Object.FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Game));
            var composition = arena.GetComponent("GameComposition");
            Assert.That(composition, Is.Not.Null);
            var read = composition.GetType().GetMethod("Read");
            arena.Player.health = 73;
            var state = read.Invoke(composition, null);
            Assert.That((int)state.GetType().GetField("hp").GetValue(state), Is.EqualTo(73));
            float start = arena.Now;
            float timeout = Time.realtimeSinceStartup + 10;
            while (arena.Now < start + .5f)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(timeout));
                yield return new WaitForFixedUpdate();
            }
            Assert.That(arena.Player.grounded, Is.True, "Combat movement must recognize forest colliders");
            var hud = (MonoBehaviour)composition.GetType().GetField("hud").GetValue(composition);
            hud.SendMessage("OpenBag");
            yield return null;
            Assert.That(arena.Player.InputBlocked, Is.True);
            arena.Player.axis = 1;
            arena.Player.QueueTestInput(basic: true);
            for(int i=0;i<3;i++) yield return new WaitForFixedUpdate();
            Assert.That(arena.Player.axis, Is.EqualTo(0));
            Assert.That(arena.Player.currentCast, Is.Null);
            hud.SendMessage("CloseDialog");
            yield return null;
            Assert.That(arena.Player.InputBlocked, Is.False);
        }
    }

    public class GameConstructionIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator GameRequestsOnlyItsTemporaryForestEncountersAtTheirCreationPositions()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            var configured = Object.FindAnyObjectByType<CombatArena>();
            configured.loadCharacterArt = false;
            configured.enabled = false; // No automatic combat tick can relocate an actor.
            ((MonoBehaviour)configured.GetComponent("GameComposition")).enabled = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var composition = (MonoBehaviour)arena.GetComponent("GameComposition");
            Assert.That(arena.Player, Is.Null);
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Unconfigured));
            composition.enabled = true;
            yield return null;
            Assert.That(arena.Player, Is.Not.Null);
            Assert.That(arena.enemies.Count, Is.EqualTo(3));
            Assert.That(arena.enemies.All(enemy => !enemy.aggressive), Is.True, "Game AI remains unarmed during its initial construction frame.");
            var initialPlayerPosition = arena.Player.Position;
            var initialEnemyPositions = arena.enemies.Select(enemy => enemy.Position).ToArray();
            yield return null;
            var forest = (MonoBehaviour)composition.GetType().GetField("forest").GetValue(composition);
            var data = forest.GetType().GetField("data").GetValue(forest);
            var playerSpawn = (Vector2)composition.GetType().GetField("playerSpawn").GetValue(composition);
            Assert.That(initialPlayerPosition, Is.EqualTo(playerSpawn));
            Assert.That(arena.SimulationTickCount, Is.Zero);
            Assert.That(arena.Player.Position, Is.EqualTo(playerSpawn));
            Assert.That(arena.Player.spawn, Is.EqualTo(playerSpawn));
            Assert.That(arena.Player.visual.sortingOrder, Is.EqualTo(35));
            Assert.That(Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Game));
            Assert.That(arena.enemies.Count, Is.EqualTo(3));
            var sizes = new[] { new Vector2(2.65f, 1.9f), new Vector2(1.3f, 1.2f), new Vector2(5.3f, 3.8f) };
            var offsets = new[] { new Vector2(.18f, .95f), new Vector2(.45f, 1.25f), new Vector2(.36f, 1.9f) };
            var health = new[] { 100f, 60f, 1000f };
            var colors = new[] { new Color(.95f, .65f, .2f), Color.magenta, new Color(.8f, .35f, .3f) };
            for (int i = 0; i < arena.enemies.Count; i++)
            {
                var enemy = arena.enemies[i]; float x = playerSpawn.x + 6 + i * 8;
                float floor = (float)data.GetType().GetMethod("FloorTop").Invoke(data, new object[] { x });
                var expected = new Vector2(x, floor + .03f);
                Assert.That(enemy.Position, Is.EqualTo(expected)); Assert.That(enemy.spawn, Is.EqualTo(expected));
                Assert.That(initialEnemyPositions[i], Is.EqualTo(expected), "The unarmed construction frame already uses final forest positions.");
                Assert.That(enemy.profile, Is.EqualTo(i)); Assert.That(enemy.isBoss, Is.EqualTo(i == 2));
                Assert.That(enemy.size, Is.EqualTo(sizes[i])); Assert.That(enemy.offset, Is.EqualTo(offsets[i]));
                Assert.That(enemy.maxHealth, Is.EqualTo(health[i])); Assert.That(enemy.health, Is.EqualTo(health[i]));
                Assert.That(enemy.aggressive, Is.True); Assert.That(enemy.visual.sortingOrder, Is.EqualTo(34));
                var skin = enemy.GetComponent("ForestEnemyPresentation");
                Assert.That(enemy.visual.color, Is.EqualTo(skin ? Color.white : colors[i]));
            }
            var surfaces = Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None);
            Assert.That(surfaces.Length, Is.GreaterThan(0));
            Assert.That(surfaces.All(surface => surface.transform.IsChildOf(forest.transform)), Is.True, "Only authored/streamed forest terrain is supplied.");
            Assert.That(arena.pits.Count, Is.EqualTo(1));
            Assert.That(arena.pits[0].size, Is.EqualTo(new Vector2(200000, 3)));
            Assert.That(arena.pits[0].transform.position, Is.EqualTo(new Vector3(0, -30, 0)));
            Assert.That(Object.FindObjectsByType<ArenaPit>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(arena.Player.GetComponent<CombatKeyboardInput>().Player, Is.SameAs(arena.Player));
        }
    }
}
