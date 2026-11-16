using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ActorPlatformIntegrationTests
    {
        [Serializable] sealed class Row { public string phase; public int[] bits; public bool result, grounded; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        [UnityTest] public IEnumerator GameKeepsPlatformLifetimeAndDropBehavior() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsPlatformLifetimeAndDropBehavior() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var actor = new GameObject("PlatformProbe").AddComponent<CombatActor>(); actor.Configure(arena, new Vector2(0, 1002), new Vector2(.9375f, 2), new Vector2(.1875f, 1), false);
            var roots = new List<GameObject>(); var trace = new Trace();
            BoxCollider2D MakePlatform()
            { var root = new GameObject("LifetimePlatform"); roots.Add(root); root.transform.position = new Vector2(0, 1001.95f); root.AddComponent<ArenaSurface>().oneWay = true; var box = root.AddComponent<BoxCollider2D>(); box.size = new Vector2(12, .1f); return box; }
            var platform = MakePlatform();
            void Reset()
            { actor.ResetActor(new Vector2(0, 1002)); actor.velocity = Vector2.zero; if (!platform) platform = MakePlatform(); platform.enabled = true; platform.transform.position = new Vector2(0, 1001.95f); Physics2D.SyncTransforms(); }
            bool Stand()
            { actor.Position = new Vector2(0, platform.bounds.max.y); actor.velocity = Vector2.zero; Physics2D.SyncTransforms(); return actor.ProbeGround(); }
            void TickAt(float y)
            { actor.Position = new Vector2(0, y); actor.velocity = Vector2.zero; Physics2D.SyncTransforms(); actor.PhysicsStep(.001f, 0, true); }
            void Record(string phase, bool result)
            { trace.rows.Add(new Row { phase = phase, bits = new[] { Bits(arena.Now), Bits(actor.Position.x), Bits(actor.Position.y), Bits(actor.velocity.x), Bits(actor.velocity.y) }, result = result, grounded = actor.grounded }); }
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    void Begin()
                    { driver.Reset(); arena.Player.Position = new Vector2(35, 1000); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); } Reset(); Assert.That(actor.ProbeGround(), Is.True); }
                    Begin(); bool dropped = actor.DropThrough(); Assert.That(dropped, Is.True); Assert.That(actor.Position.y, Is.EqualTo(1001.92f)); Assert.That(actor.velocity.y, Is.EqualTo(-2)); Record("drop", dropped); driver.Step(.3f); TickAt(1001.9f); bool ground = Stand(); Assert.That(ground, Is.False); Record("time-without-clearance", ground);
                    Begin(); Assert.That(actor.DropThrough(), Is.True); driver.Step(.1f); TickAt(998); ground = Stand(); Assert.That(ground, Is.False); Record("clearance-before-time", ground); driver.Step(.15f); Assert.That(arena.Now, Is.EqualTo(.25f)); TickAt(998); ground = Stand(); Assert.That(ground, Is.True); Record("time-and-clearance", ground);
                    Begin(); Assert.That(actor.DropThrough(), Is.True); float originalPlane = actor.Platforms.Entries[0].PlaneY;
                    driver.Step(.3f); platform.transform.position += Vector3.up * 2; Physics2D.SyncTransforms(); TickAt(1000.5f);
                    Assert.That(actor.Platforms.Entries.Count, Is.EqualTo(1)); Assert.That(actor.Platforms.Entries[0].PlaneY, Is.EqualTo(originalPlane));
                    ground = Stand(); Assert.That(ground, Is.False); Record("moving-platform-preserves-original-plane", ground);
                    TickAt(originalPlane - actor.size.y - .03f); ground = Stand(); Assert.That(ground, Is.True); Record("original-plane-clearance-restores", ground);
                    Begin(); Assert.That(actor.DropThrough(), Is.True); platform.enabled = false; TickAt(998); platform.enabled = true; Physics2D.SyncTransforms(); ground = Stand(); Assert.That(ground, Is.False); Record("disable-keeps-live-ignore", ground); driver.Step(.3f); TickAt(998); ground = Stand(); Assert.That(ground, Is.True); Record("reenabled-restoration", ground);
                    Begin(); Assert.That(actor.DropThrough(), Is.True); actor.ResetActor(new Vector2(0, 1002)); Physics2D.SyncTransforms(); ground = actor.ProbeGround(); Assert.That(ground, Is.True); Record("reset-clears-ignore", ground);
                    Begin(); Assert.That(actor.DropThrough(), Is.True); Object.DestroyImmediate(platform.gameObject); TickAt(998); platform = MakePlatform(); Physics2D.SyncTransforms(); ground = Stand(); Assert.That(ground, Is.True); Record("destroy-and-new-binding", ground);
                    Begin(); var stacked = MakePlatform(); Physics2D.SyncTransforms(); Assert.That(actor.DropThrough(), Is.True); ground = Stand(); Assert.That(ground, Is.True); Record("stacked-other-support", ground); Assert.That(actor.DropThrough(), Is.True); ground = Stand(); Assert.That(ground, Is.True); Record("repeat-keeps-first-selection", ground); stacked.enabled = false;
                    Begin(); actor.VelocityY = 20; dropped = actor.DropThrough(); Assert.That(dropped, Is.False); Assert.That(actor.Position.y, Is.EqualTo(1002)); Record("rising-no-drop", dropped); Assert.That(arena.SimulationFaulted, Is.False);
                }
                Assert.That(trace.rows.Count, Is.EqualTo(13)); var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorPlatformTraceRoot"); if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(actor.gameObject); foreach (var root in roots) if (root) Object.Destroy(root); }
        }
    }
}
