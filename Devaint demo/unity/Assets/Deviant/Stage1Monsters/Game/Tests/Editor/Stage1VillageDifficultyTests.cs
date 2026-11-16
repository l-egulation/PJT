using System.Collections;
using System.Linq;
using Deviant.Combat;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Stage1Monsters.Game.Tests
{
    // Village normals notice the player from 13 units (1.3x the shared 10); the broom sweep reaches 4.25 units and the thorn dashes 3.
    public sealed class Stage1VillageDifficultyTests
    {
        [UnityTest] public IEnumerator NormalsSenseFromThirteenUnitsAndTheBroomSweepsFromFarther()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode(); yield return null; yield return null;
            var world = Object.FindAnyObjectByType<ForestCombatWorld>(); Assert.That(world.Ready, Is.True);
            var arena = world.arena; var player = arena.Player; player.UseTestInput = true;
            player.maxHealth = player.health = 100000;
            var enemies = arena.enemies.OfType<FieldMouseEnemy>().ToArray();
            foreach (var e in enemies) { e.aggressive = false; e.CancelAttack(); }
            var floor = new GameObject("Temporary difficulty verification floor"); floor.transform.position = new Vector3(-500, -.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            foreach (var index in enemies.Select(e => e.SurfaceIndex).Distinct()) index.Rebuild(Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None));

            int parked = 0;
            foreach (string id in new[] { "01_thorn-root", "02_broom-stalker", "03_gutter-spitter" })
            {
                var actor = enemies.First(e => e.Definition.id == id);
                Assert.That(actor.Definition.senseX, Is.EqualTo(13f), id);
                // Melee pursuit walkers widen the first detection by data/combat/enemy_pursuit.json detectionScale.
                float range = 13f * (actor.Pursuit != null ? actor.Pursuit.settings.detectionScale : 1f);
                Assert.That(actor.Pursuit != null, Is.EqualTo(id != "03_gutter-spitter"), id + " pursuit opt-in");
                actor.spawn = new Vector2(-500, .003f); actor.ResetEnemy(); actor.aggressive = false;
                player.Position = new Vector2(-500 + range - 1, .003f); Physics2D.SyncTransforms();
                Assert.That(actor.Visible(player, true), Is.True, id + " sees the player just inside its range");
                player.Position = new Vector2(-500 + range + .5f, .003f); Physics2D.SyncTransforms();
                Assert.That(actor.Visible(player, true), Is.False, id + " does not start chasing beyond its range");
                actor.spawn = new Vector2(-540 - 10 * ++parked, .003f); actor.ResetEnemy();
            }
            Assert.That(enemies.First(e => e.Definition.id == "04_well-warden").Definition.senseX, Is.EqualTo(10f), "the elite keeps the shared range");

            var broom = enemies.First(e => e.Definition.id == "02_broom-stalker");
            using (var simulation = arena.AcquireManualSimulation())
            {
                arena.ResetTest(); foreach (var e in enemies) { e.aggressive = false; e.CancelAttack(); }
                broom.spawn = new Vector2(-500, .003f); broom.ResetEnemy(); broom.SyncFacing(1);
                Assert.That(broom.Definition.attackSize[0], Is.EqualTo(4.4f).Within(.001f), "sweep width widened from 3.38 to 4.4");
                Assert.That(broom.Definition.attackOffset[0] + broom.Definition.attackSize[0] / 2, Is.EqualTo(4.25f).Within(.001f), "front edge moved from 3.23 to 4.25");
                var thorn = enemies.First(e => e.Definition.id == "01_thorn-root").Definition;
                Assert.That(thorn.chargeDistance, Is.EqualTo(3f).Within(.001f), "the thorn dash doubled from 1.5 units");
                Assert.That(thorn.chargeDistance / thorn.chargeSpeed, Is.EqualTo(.2f).Within(.001f), "it still covers the dash in the 0.2 s slice window");
                Assert.That(broom.Definition.attackOffset[0] - broom.Definition.attackSize[0] / 2, Is.EqualTo(-.15f).Within(.001f), "back edge is unchanged");
                // The broom walks in and starts its sweep from the widened 2.55-unit gap instead of the old 1.95.
                player.Position = new Vector2(-494, .003f); player.velocity = Vector2.zero; player.maxHealth = player.health = 100000;
                Physics2D.SyncTransforms();
                float before = player.health;
                broom.aggressive = true;
                for (int i = 0; i < 1000 && player.health >= before; i++) { simulation.Step(.01f); player.Position = new Vector2(-494, .003f); player.velocity = Vector2.zero; }
                Assert.That(player.health, Is.LessThan(before), "the sweep lands");
                Assert.That(player.Position.x - broom.Position.x, Is.GreaterThan(2.3f), "it hit from beyond the old 1.95-unit start gap");
                Assert.That(player.Position.x - broom.Position.x, Is.LessThan(4.25f));
            }
            yield return new ExitPlayMode();
        }
    }
}
