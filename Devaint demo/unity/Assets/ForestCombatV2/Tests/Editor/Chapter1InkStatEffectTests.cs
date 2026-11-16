using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Equips real stat inks through the bag commands inside a facility and measures the next real hit.
    public sealed class Chapter1InkStatEffectTests
    {
        static float lastHit;

        static IEnumerator Hit(CombatArena arena, TestEnemy dummy)
        {
            var player = arena.Player;
            dummy.ResetEnemy(); dummy.aggressive = false;
            dummy.Position = player.Position + Vector2.right * 1.1f; player.facing = 1;
            Physics2D.SyncTransforms();
            float before = dummy.health;
            var savedRandom = Random.state;
            try
            {
                // Compare the ink multiplier under the same combat roll each time.
                Random.InitState(732);
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Basic));
                    for (int tick = 0; tick < 240 && dummy.health >= before; tick++) driver.Step(1f / 120);
                    // Maintenance is refused while an action runs, so let the attack finish.
                    for (int tick = 0; tick < 240 && player.currentCast; tick++) driver.Step(1f / 120);
                }
            }
            finally { Random.state = savedRandom; }
            lastHit = before - dummy.health;
            yield return null;
        }

        [UnityTest] public IEnumerator StatInksEquippedInAFacilityMultiplyTheNextRealHit()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            InventoryRuntimeHost host = null;
            for (int frame = 0; frame < 600 && !host; frame++) { yield return null; host = Object.FindAnyObjectByType<InventoryRuntimeHost>(); }
            Assert.That(world && world.Ready && host, Is.True);
            var arena = world.arena; var player = arena.Player;
            player.maxHealth = player.health = 100000;
            foreach (var enemy in arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }

            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            player.Position = offer.OfferPosition(0); Assert.That(offer.TryChoose(0), Is.True, "sword blueprint into slot 1");
            var room = world.Data.rooms.First(r => Chapter1MaintenanceFacility.IsFacility(r.kind));
            player.Position = new Vector2(room.x + room.w / 2f, -(room.y + room.entryY));
            Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            Assert.That(player.EditingAllowed, Is.True);

            var dummy = arena.SpawnEnemy(0, player.Position + Vector2.right * 1.1f);
            dummy.maxHealth = 100000;
            yield return Hit(arena, dummy);
            float plain = lastHit;
            Assert.That(plain, Is.GreaterThan(0), "the basic attack must land on the dummy");

            for (int count = 1; count <= 2; count++)
            {
                var drop = host.SpawnDrop("stat.basic", player.Position, true);
                Assert.That(host.TryCollect(drop), Is.True);
                host.Presenter.SelectWeapon(0);
                host.Presenter.SelectBag(host.Inventory.VisibleSlots(null).First());
                Assert.That(host.Presenter.EquipSelected(), Is.True, host.Presenter.Message);
                Assert.That(player.weapons[0].StatSlots.Count(kind => kind == 1), Is.EqualTo(count), "stat slot holds the equipped ink");
                yield return Hit(arena, dummy);
                Assert.That(lastHit / plain, Is.EqualTo(Mathf.Pow(1.2f, count)).Within(.01f), count + " basic damage inks multiply the real hit");
            }
            yield return new ExitPlayMode();
        }
    }
}
