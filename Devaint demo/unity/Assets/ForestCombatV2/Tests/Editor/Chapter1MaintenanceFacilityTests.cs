using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1MaintenanceFacilityTests
    {
        [UnityTest] public IEnumerator InkMaintenanceIsAllowedOnlyInsideTheUnifiedFacilities()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            yield return null;

            var facility = world.GetComponent<Chapter1MaintenanceFacility>();
            Assert.That(facility, Is.Not.Null);
            var facilityRooms = world.Data.rooms.Where(r => Chapter1MaintenanceFacility.IsFacility(r.kind)).ToArray();
            Assert.That(facilityRooms.Length, Is.EqualTo(1), "the random stage has one main-route facility");
            Assert.That(facility.Rooms.Count, Is.EqualTo(1));
            Assert.That(world.Data.landmarks.Count(l => Chapter1MaintenanceFacility.IsFacility(l.kind) && l.name == "정비소"), Is.EqualTo(1));
            Assert.That(world.Data.landmarks.Any(l => l.name == "상점"), Is.False);

            // Location decides maintenance, not whether enemies are fighting.
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            var player = world.arena.Player;
            Assert.That(facility.PlayerInside(), Is.False, "the start room is not a facility");
            Assert.That(player.EditingAllowed, Is.False);
            Assert.That(player.MaintenanceUnavailableReason, Does.Contain("정비소"));

            foreach (var room in facilityRooms)
            {
                player.Position = new Vector2(room.x + room.w / 2f, -(room.y + room.entryY));
                Physics2D.SyncTransforms();
                Assert.That(facility.PlayerInside(), Is.True, room.name + " should contain its center floor");
                Assert.That(player.EditingAllowed, Is.True, "maintenance is allowed inside " + room.name);
            }

            var combat = world.Data.rooms.First(r => r.kind == "combat");
            player.Position = new Vector2(combat.x + combat.w / 2f, -(combat.y + combat.entryY));
            Assert.That(facility.PlayerInside(), Is.False);
            Assert.That(player.EditingAllowed, Is.False, "combat rooms never allow maintenance");
            yield return new ExitPlayMode();
        }
    }
}
