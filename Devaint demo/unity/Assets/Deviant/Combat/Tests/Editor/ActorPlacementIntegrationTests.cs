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
    public class ActorPlacementIntegrationTests
    {
        [Serializable] sealed class Row { public string scenario; public int[] bits; public bool result; public int facing; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        [UnityTest] public IEnumerator GameUsesSharedPlacementQueries() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabUsesSharedPlacementQueries() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var actor = new GameObject("PlacementProbe").AddComponent<CombatActor>();
            actor.Configure(arena, new Vector2(0, 1000), new Vector2(.9375f, 2), new Vector2(.1875f, 1), false);
            var roots = new List<GameObject>(); var surfaces = new List<BoxCollider2D>(); var trace = new Trace();
            BoxCollider2D Surface(string name, Vector3 at, Vector2 size, bool oneWay = false)
            { var root = new GameObject(name); roots.Add(root); root.transform.position = at; root.AddComponent<ArenaSurface>().oneWay = oneWay; var box = root.AddComponent<BoxCollider2D>(); box.size = size; surfaces.Add(box); return box; }
            var floor = Surface("PlacementFloor", new Vector3(0, 999.5f), new Vector2(100, 1));
            var narrow = Surface("PlacementNarrow", new Vector3(.1875f, 999.5f), new Vector2(.6f, 1));
            var left = Surface("PlacementLeft", new Vector3(-.5f, 999.5f), Vector2.one);
            var right = Surface("PlacementRight", new Vector3(.5f, 999.53f), Vector2.one);
            var platform = Surface("PlacementPlatform", new Vector3(0, 999.5f), new Vector2(12, 1), true);
            var wall = Surface("PlacementWall", new Vector3(3, 1005), new Vector2(.4f, 10));
            var volume = Surface("PlacementVolume", new Vector3(0, 1001), new Vector2(5, 2));
            void Reset(float x = 0, float y = 1000, int facing = 1)
            { foreach (var surface in surfaces) surface.enabled = false; Physics2D.SyncTransforms(); actor.ResetActor(new Vector2(x, y)); actor.facing = facing; actor.SyncFacing(facing); actor.velocity = new Vector2(5, 7); }
            void Record(string scenario, bool result, float height = 0)
            { trace.rows.Add(new Row { scenario = scenario, bits = new[] { Bits(actor.Position.x), Bits(actor.Position.y), Bits(actor.Center.x), Bits(actor.Center.y), Bits(actor.velocity.x), Bits(actor.velocity.y), Bits(height) }, result = result, facing = actor.facing }); }
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); arena.Player.Position = new Vector2(35, 1000); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                    Reset(); actor.VelocityY = 0; floor.enabled = true; Physics2D.SyncTransforms(); bool safe = actor.ProbeSafeFloor(out float height); Assert.That(safe, Is.True); Assert.That(height, Is.EqualTo(1000)); Assert.That(actor.Blocked(actor.Position, 1), Is.False); Record("flat-floor", safe, height);
                    Reset(); actor.VelocityY = 0; narrow.enabled = true; Physics2D.SyncTransforms(); Assert.That(actor.ProbeGround(), Is.True); safe = actor.ProbeSafeFloor(out height); Assert.That(safe, Is.False); Record("partial-support", safe, height);
                    Reset(y: 1000.03f); actor.VelocityY = 0; left.enabled = right.enabled = true; Physics2D.SyncTransforms(); safe = actor.ProbeSafeFloor(out height); Assert.That(safe, Is.False); Record("uneven-floor", safe, height);
                    Reset(); actor.VelocityY = 0; platform.enabled = true; Physics2D.SyncTransforms(); safe = actor.ProbeSafeFloor(out height); Assert.That(safe, Is.True); Record("platform-before-drop", safe, height); Assert.That(actor.DropThrough(), Is.True); safe = actor.ProbeSafeFloor(out height); Assert.That(safe, Is.False); Record("platform-after-drop", safe, height);
                    Reset(2.5f, facing: -1); floor.enabled = wall.enabled = true; Physics2D.SyncTransforms(); Assert.That(actor.Blocked(actor.Position, -1), Is.False); var center = actor.Center; actor.SyncFacing(1); Assert.That(actor.Center, Is.EqualTo(center)); Assert.That(actor.Blocked(actor.Position, 1), Is.False); Record("turn-at-wall", true);
                    Reset(); volume.transform.position = new Vector3(0, 1001, 10); volume.size = new Vector2(5, 2); volume.enabled = true; Physics2D.SyncTransforms(); Assert.That(Array.IndexOf(Physics2D.OverlapBoxAll(actor.Center, actor.size, 0), volume), Is.GreaterThanOrEqualTo(0)); bool blocked = actor.Blocked(actor.Position, 1); Assert.That(blocked, Is.False); Record("different-z-bounds", blocked);
                    volume.transform.position = new Vector3(0, 1001, 0); Physics2D.SyncTransforms(); blocked = actor.Blocked(actor.Position, 1); Assert.That(blocked, Is.True); Record("same-z-bounds", blocked);
                    Reset(); floor.enabled = true; Physics2D.SyncTransforms(); actor.TeleportToward(new Vector2(4, 1000)); Assert.That(actor.Position, Is.EqualTo(new Vector2(4, 1000))); Record("open-teleport", true);
                    Reset(); floor.enabled = wall.enabled = true; Physics2D.SyncTransforms(); actor.TeleportToward(new Vector2(4, 1000)); Assert.That(actor.Position.x, Is.InRange(2f, 2.2f)); Assert.That(actor.Blocked(actor.Position, 1), Is.False); Record("wall-limited-teleport", true);
                    Reset(); floor.enabled = wall.enabled = true; Physics2D.SyncTransforms(); actor.TeleportToward(new Vector2(4, 1004)); Assert.That(actor.Position.x, Is.InRange(2f, 2.3f)); Assert.That(actor.Position.y, Is.InRange(1002f, 1002.3f)); Record("diagonal-wall-teleport", true);
                    Reset(); platform.transform.position = new Vector3(0, 1002); platform.size = new Vector2(12, .25f); platform.enabled = true; Physics2D.SyncTransforms(); actor.TeleportToward(new Vector2(0, 1004)); Assert.That(actor.Position.y, Is.EqualTo(1004)); Record("one-way-teleport", true);
                    Reset(); volume.transform.position = new Vector3(0, 1001); volume.size = new Vector2(.4f, 2); volume.enabled = true; Physics2D.SyncTransforms(); actor.TeleportToward(new Vector2(4, 1000)); Assert.That(actor.Position.x, Is.EqualTo(4)); Record("leave-initial-overlap", true);
                    Reset(); volume.size = new Vector2(20, 10); volume.enabled = true; Physics2D.SyncTransforms(); actor.TeleportToward(new Vector2(4, 1000)); Assert.That(actor.Position.x, Is.Zero); Record("blocked-endpoint", false);
                    Reset(); Physics2D.SyncTransforms(); actor.TeleportToward(new Vector2(.0005f, 1000)); Assert.That(actor.Position.x, Is.Zero); Record("tiny-teleport", false);
                    Reset(.125f, 1000.03f, -1); Physics2D.SyncTransforms(); actor.TeleportToward(new Vector2(3.765f, 1004.215f)); Assert.That(actor.Position.x, Is.EqualTo(3.765f).Within(.00001f)); Assert.That(actor.Position.y, Is.EqualTo(1004.215f).Within(.0001f)); Record("negative-facing-free-diagonal", true);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                Assert.That(trace.rows.Count, Is.EqualTo(16)); var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorPlacementTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(actor.gameObject); foreach (var root in roots) Object.Destroy(root); }
        }
    }
}
