using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ActorRecoveryIntegrationTests
    {
        [UnityTest] public IEnumerator GameRejectsBlockedNewestAndUsesOlderGround() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "blocked");
        [UnityTest] public IEnumerator LabRejectsBlockedNewestAndUsesOlderGround() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "blocked");
        [UnityTest] public IEnumerator GameRejectsRemovedNewestSupport() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "support");
        [UnityTest] public IEnumerator LabRejectsRemovedNewestSupport() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "support");
        [UnityTest] public IEnumerator GameRejectsCurrentHazardAtNewestSample() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "hazard");
        [UnityTest] public IEnumerator LabRejectsCurrentHazardAtNewestSample() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "hazard");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static BoxCollider2D Floor(string name, float x)
        {
            var root = new GameObject(name); root.transform.position = new Vector3(x, 999.5f, 0);
            root.AddComponent<ArenaSurface>(); var box = root.AddComponent<BoxCollider2D>(); box.size = new Vector2(2, 1); return box;
        }
        static IEnumerator Exercise(string scene, string fault)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var oldFloor = Floor("RecoveryOlderFloor", 0); var newestFloor = Floor("RecoveryNewestFloor", 5);
            var obstruction = new GameObject("RecoveryCurrentFault");
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                    for (int tick = 0; tick < 20; tick++)
                    {
                        player.Position = new Vector2(tick < 10 ? 0 : 5, 1000); player.velocity = Vector2.zero;
                        Physics2D.SyncTransforms(); Assert.That(player.ProbeSafeFloor(out _), Is.True); driver.Step(.02f);
                    }
                    player.Position = new Vector2(35, 1000); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                    for (int tick = 0; tick < 160; tick++) driver.Step(.02f);
                    if (fault == "support") newestFloor.enabled = false;
                    else
                    {
                        obstruction.transform.position = new Vector3(5.1875f, 1001, 0);
                        var box = obstruction.AddComponent<BoxCollider2D>(); box.size = new Vector2(2, 2);
                        if (fault == "blocked") obstruction.AddComponent<ArenaSurface>();
                        else { box.isTrigger = true; arena.pits.Add(box); }
                    }
                    Physics2D.SyncTransforms(); float health = player.health; player.Fall();
                    Assert.That(player.Position.x, Is.EqualTo(0).Within(.001f), "Invalid newest sample must be skipped in favor of the older currently safe support.");
                    Assert.That(player.Position.y, Is.EqualTo(1000).Within(.001f));
                    Assert.That(player.health, Is.EqualTo(health - player.maxHealth * .1f));
                }
            }
            finally { Object.Destroy(oldFloor.gameObject); Object.Destroy(newestFloor.gameObject); Object.Destroy(obstruction); }
        }
    }
}
