using System;
using System.Collections;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class EnemyProjectileRetainedCleanupIntegrationTests
    {
        [UnityTest] public IEnumerator GameResetClosesRetainedEnemyShotBeforeDeferredDestroy()
            => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", false);
        [UnityTest] public IEnumerator LabResetClosesRetainedEnemyShotBeforeDeferredDestroy()
            => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", false);
        [UnityTest] public IEnumerator GameDeathClosesRetainedReflectedShotBeforeDeferredDestroy()
            => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", true);
        [UnityTest] public IEnumerator LabDeathClosesRetainedReflectedShotBeforeDeferredDestroy()
            => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", true);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void CenterAt(CombatActor actor, Vector2 center)
            => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        static IEnumerator Exercise(string scene, bool death)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset();
                foreach (var enemy in arena.enemies)
                { enemy.aggressive = false; enemy.health = 1000; enemy.Position = new Vector2(-35, 1000); enemy.velocity = Vector2.zero; }
                player.InputBlocked = false; player.UseTestInput = true; player.selected = 0;
                player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.SyncFacing(1);
                int form = Array.FindIndex(arena.catalog.forms, item => item.id == "shield");
                Assert.That(player.TryReconfigure(0, form, 0, out string error), Is.True, error);
                Physics2D.SyncTransforms(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
                var source = arena.enemies[0]; arena.FireEnemyProjectile(source, new Vector2(.9f, 1001), Vector2.left * 8, 8);
                var shot = arena.projectiles[0]; shot.Step(.02f);
                Assert.That(shot.source, Is.SameAs(player)); Assert.That(shot.velocity, Is.EqualTo(Vector2.right * 8));
                Vector2 at = shot.transform.position, velocity = shot.velocity;
                if (death) { player.health = 0; player.Die(); } else arena.ResetTest();
                Assert.That(arena.projectiles, Is.Empty);
                // Restore a live source before using the retained reference so
                // the old dead-player guard cannot hide missing effect cleanup.
                // No yield occurs before any of these calls.
                if (death) player.ResetPlayer();
                foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                CenterAt(source, at + Vector2.right * 3); player.Position = new Vector2(70, 1000);
                Physics2D.SyncTransforms(); float health = source.health; int hits = source.ReceivedHitCount;
                shot.Step(.01f);
                Assert.That((Vector2)shot.transform.position, Is.EqualTo(at), "A cleaned retained shot must not resume flight in the same frame.");
                shot.Step(1); shot.Step(0);
                Assert.That((Vector2)shot.transform.position, Is.EqualTo(at)); Assert.That(shot.velocity, Is.EqualTo(velocity));
                Assert.That(source.health, Is.EqualTo(health)); Assert.That(source.ReceivedHitCount, Is.EqualTo(hits));
                Assert.That(arena.projectiles, Is.Empty);
                arena.FireEnemyProjectile(source, at, Vector2.right * 8, 8); var fresh = arena.projectiles[0]; fresh.Step(.01f);
                Assert.That((Vector2)fresh.transform.position, Is.EqualTo(at + Vector2.right * .08f));
                Assert.That(arena.projectiles.Contains(fresh), Is.True); Assert.That(arena.SimulationFaulted, Is.False);
            }
        }
    }
}
