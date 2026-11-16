using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // First-30-seconds guidance (user decisions 2026-10-29): the starting room exit opens only after two blueprints,
    // and the first vertical connector shows the climb keys once.
    public sealed class Chapter1StartingGuideTests
    {
        static void OpenSeededStage()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
        }

        static ForestCombatWorld ReadyWorld()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            return world && world.Ready ? world : null;
        }

        static ForestCombatWorld CalmWorld()
        {
            var world = ReadyWorld();
            Assert.That(world, Is.Not.Null);
            foreach (var enemy in world.arena.enemies) if (enemy) enemy.aggressive = false;
            return world;
        }

        static void WalkRight(CombatArena.ManualSimulation driver, Chapter1StartingBlueprints offer, float seconds)
        {
            var player = offer.GetComponent<ForestCombatWorld>().arena.Player;
            player.Position = new Vector2(offer.ExitDoor.x - 3, offer.ExitDoor.y);
            for (int tick = 0; tick < 120 && !player.grounded; tick++) driver.Step(1f / 120);
            for (int tick = 0; tick < seconds * 120; tick++) driver.Step(1f / 120, new CombatInput(1));
        }

        [UnityTest] public IEnumerator StartingRoomExitOpensOnlyAfterTwoBlueprints()
        {
            OpenSeededStage();
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            var world = CalmWorld();
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            var player = world.arena.Player;
            Assert.That(offer.ExitLocked, Is.True, "a new run starts with the exit shut");
            using (var driver = world.arena.AcquireManualSimulation())
            {
                WalkRight(driver, offer, 2);
                Assert.That(player.Position.x, Is.LessThan(offer.ExitDoor.x), "walking right stops at the locked exit");
                Assert.That(offer.PlayerAtLockedExit, Is.True);
                StringAssert.Contains("골라야 출구가 열립니다", offer.ExitGuide);

                player.Position = offer.OfferPosition(0); Assert.That(offer.TryChoose(0), Is.True);
                Assert.That(offer.ExitLocked, Is.True, "one blueprint is not enough");
                player.Position = offer.OfferPosition(2); Assert.That(offer.TryChoose(2), Is.True);
                Assert.That(offer.ExitLocked, Is.False, "the second blueprint opens the exit");

                WalkRight(driver, offer, 2);
                Assert.That(player.Position.x, Is.GreaterThan(offer.ExitDoor.x + 1), "the player now walks out");
            }
            yield return new ExitPlayMode();
        }
    }
}
