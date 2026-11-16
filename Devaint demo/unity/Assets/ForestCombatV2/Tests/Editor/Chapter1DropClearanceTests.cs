using System.Collections;
using System.Linq;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1DropClearanceTests
    {
        static BoxCollider2D Solid(string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name); go.transform.position = center;
            go.AddComponent<ArenaSurface>();
            var box = go.AddComponent<BoxCollider2D>(); box.size = size;
            return box;
        }

        [UnityTest] public IEnumerator LootBesideAWallLandsOnTheOpenFloorInsteadOfInsideTheWall()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            yield return null;

            // An isolated floor with a wall on its right, far from the generated map.
            Solid("Test floor", new Vector2(-500, -.5f), new Vector2(20, 1));
            var wall = Solid("Test wall", new Vector2(-498, 2), new Vector2(1, 4));
            Physics2D.SyncTransforms();
            var arena = world.arena;
            var origin = new Vector2(-499.2f, 0);

            var spread = arena.DropPoint(origin, 1.6f);
            Assert.That(spread.x + .25f, Is.LessThanOrEqualTo(wall.bounds.min.x + .01f), "spread stops before the wall");
            Assert.That(spread.x, Is.GreaterThanOrEqualTo(origin.x));
            Assert.That(arena.DropPoint(origin, -1.6f).x, Is.EqualTo(origin.x - 1.6f).Within(.01f), "open side keeps the full spread");
            Assert.That(arena.DropPoint(origin, 0), Is.EqualTo(origin));

            // With a chosen blueprint the boss's guaranteed stamp always drops, whether weapon or skill.
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            arena.Player.Position = offer.OfferPosition(1);
            Assert.That(offer.TryChoose(1), Is.True);
            var rewards = world.GetComponent<Chapter1Rewards>();
            using (var simulation = arena.AcquireManualSimulation())
            {
            rewards.Grant(Chapter1EnemyRole.Boss, origin);
            var dropped = rewards.Pickups.ToArray();
            Assert.That(dropped.Length, Is.EqualTo(2), "gold and the guaranteed stamp");
            var inks = arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>().Drops.ToArray();
            Assert.That(inks, Is.Empty, "chapter 1 drops no bag inks");
            simulation.Step(.61f);
            yield return null; yield return null;
            foreach (var pickup in dropped)
            {
                Vector2 at = pickup.transform.position;
                Assert.That(wall.OverlapPoint(at), Is.False, pickup.name + " must not sit inside the wall");
                Assert.That(at.x, Is.LessThan(wall.bounds.min.x), pickup.name + " stays on the open side");
                var art = pickup.GetComponent<SpriteRenderer>();
                Assert.That(art.bounds.max.x, Is.LessThanOrEqualTo(wall.bounds.min.x + .01f), "the entire picture clears the wall");
                Assert.That(at.y, Is.EqualTo(art.bounds.extents.y).Within(.01f), pickup.name + " rests on the floor at its own height");
                foreach (var grain in pickup.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Currency powder"))
                    Assert.That(grain.bounds.max.x, Is.LessThanOrEqualTo(wall.bounds.min.x + .01f), "gold powder bounces off the wall");
            }
            foreach (var ink in inks)
            {
                Vector2 at = ink.transform.position;
                Assert.That(wall.OverlapPoint(at), Is.False, "ink must not sit inside the wall");
                Assert.That(at.x, Is.LessThan(wall.bounds.min.x), "ink stays on the open side");
                Assert.That(ink.Visual.bounds.max.x, Is.LessThanOrEqualTo(wall.bounds.min.x + .01f), "the entire ink picture clears the wall");
            }
            }
            yield return new ExitPlayMode();
        }
    }
}
