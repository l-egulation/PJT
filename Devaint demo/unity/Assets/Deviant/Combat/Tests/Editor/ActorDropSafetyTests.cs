using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ActorDropSafetyTests
    {
        [UnityTest] public IEnumerator GameRejectsDropThatWouldEnterASolid() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabRejectsDropThatWouldEnterASolid() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var actor = new GameObject("DropSafetyProbe").AddComponent<CombatActor>(); actor.Configure(arena, new Vector2(0, 1002), new Vector2(.9375f, 2), new Vector2(.1875f, 1), false); var roots = new List<GameObject>();
            BoxCollider2D Surface(string name, float top, bool oneWay, float height)
            { var root = new GameObject(name); roots.Add(root); root.transform.position = new Vector2(0, top - height / 2); root.AddComponent<ArenaSurface>().oneWay = oneWay; var box = root.AddComponent<BoxCollider2D>(); box.size = new Vector2(12, height); return box; }
            Surface("DropSafetyPlatform", 1002, true, .1f); var solid = Surface("DropSafetySolid", 1001.96f, false, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); arena.Player.Position = new Vector2(35, 1000); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                    foreach (float solidTop in new[] { 1001.96f, 1001.93f })
                    {
                        solid.transform.position = new Vector2(0, solidTop - .5f); actor.ResetActor(new Vector2(0, 1002)); Physics2D.SyncTransforms(); Assert.That(actor.ProbeGround(), Is.True);
                        var at = actor.Position; Assert.That(actor.DropThrough(), Is.False, "The full 0.08-unit drop must reject a solid even when endpoint overlap is below the ordinary foot-margin tolerance."); Assert.That(actor.Position, Is.EqualTo(at)); Assert.That(actor.velocity, Is.EqualTo(Vector2.zero));
                    }
                    solid.enabled = false; Physics2D.SyncTransforms(); Assert.That(actor.DropThrough(), Is.True, "A rejected request must leave no ignore that prevents a fresh legal drop.");
                }
            }
            finally { Object.Destroy(actor.gameObject); foreach (var root in roots) Object.Destroy(root); }
        }
    }
}
