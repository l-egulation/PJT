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
    public class ActorMotionIntegrationTests
    {
        [Serializable] sealed class Row { public int scenario, tick; public int[] bits; public bool grounded, wall; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        [UnityTest] public IEnumerator GameUsesSharedMotionProposalsAndExistingContacts() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabUsesSharedMotionProposalsAndExistingContacts() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static BoxCollider2D Surface(string name, Vector2 position, Vector2 size, bool oneWay = false)
        {
            var root = new GameObject(name); root.transform.position = position; root.AddComponent<ArenaSurface>().oneWay = oneWay; var box = root.AddComponent<BoxCollider2D>(); box.size = size; return box;
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var actor = new GameObject("ActorMotionProbe").AddComponent<CombatActor>();
            actor.Configure(arena, new Vector2(0, 1000), new Vector2(.9375f, 2), new Vector2(.1875f, 1), false);
            var floor = Surface("MotionFloor", new Vector2(0, 999.5f), new Vector2(100, 1));
            var wall = Surface("MotionWall", new Vector2(3, 1004), new Vector2(.4f, 8));
            var ceiling = Surface("MotionCeiling", new Vector2(0, 1002.75f), new Vector2(20, .5f));
            var platform = Surface("MotionOneWay", new Vector2(0, 1002), new Vector2(12, .25f), true); var trace = new Trace();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    for (int scenario = 0; scenario < 13; scenario++)
                    {
                        driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                        arena.Player.Position = new Vector2(35, 1000); arena.Player.velocity = Vector2.zero;
                        actor.isDecoy = false; actor.ResetActor(new Vector2(0, 1000)); actor.SyncFacing(1);
                        wall.enabled = scenario == 10; ceiling.enabled = scenario == 11; platform.enabled = scenario == 12;
                        actor.VelocityY = scenario == 1 || scenario == 5 || scenario == 8 || scenario == 9 || scenario == 11 || scenario == 12 ? 20 : 0;
                        if (scenario == 2) { actor.Position = new Vector2(0, 1006); actor.VelocityY = -1; }
                        if (scenario == 3) { actor.Position = new Vector2(0, 1003); actor.VelocityY = 20; }
                        if (scenario == 4) { actor.knockUntil = .5f; actor.knockSpeed = -7; actor.moveLockUntil = 1; }
                        if (scenario == 5) actor.rootUntil = .5f;
                        if (scenario == 6) actor.stunUntil = .5f;
                        if (scenario == 7)
                        {
                            actor.knockUntil = actor.moveLockUntil = actor.rootUntil = actor.stunUntil = 3; actor.knockSpeed = -7;
                            actor.currentCast = CombatCast.Create(arena, actor, new SkillData { id = "motion-force-fixture", activation = 0, end = 3, inputSpeed = 0, vertical = "normal", hits = Array.Empty<HitData>(), invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                                movement = new MoveData { type = "dash", start = 0, duration = 3, speed = 5 } }, new WeaponState(), false);
                            actor.currentCast.BeforeMovement(.02f); Assert.That(actor.IsForcedMoving, Is.True);
                        }
                        if (scenario == 8) actor.health = 0;
                        if (scenario == 9) actor.isDecoy = true;
                        Physics2D.SyncTransforms(); float highest = actor.Position.y; bool dropped = false;
                        for (int tick = 0; tick < 120; tick++)
                        {
                            if (scenario == 12 && tick == 50) dropped = actor.DropThrough();
                            float desired = scenario == 1 || scenario == 2 || scenario == 3 || scenario == 11 || scenario == 12 ? 0 : 5;
                            actor.PhysicsStep(.02f, desired, scenario == 3); highest = Mathf.Max(highest, actor.Position.y);
                            trace.rows.Add(new Row { scenario = scenario, tick = tick, bits = new[] { Bits(arena.Now), Bits(actor.Position.x), Bits(actor.Position.y), Bits(actor.velocity.x), Bits(actor.velocity.y) }, grounded = actor.grounded, wall = actor.wallHit });
                            if (scenario == 4 && tick == 0) Assert.That(actor.velocity.x, Is.EqualTo(-7));
                            if (scenario == 7) Assert.That(actor.velocity.x, Is.EqualTo(5), "Forced motion bypasses the ordinary locks/knock.");
                            if (scenario == 8 || scenario == 9) Assert.That(actor.velocity.x, Is.Zero);
                            driver.Step(.02f);
                        }
                        if (scenario == 0) Assert.That(actor.Position.x, Is.EqualTo(12).Within(.0001f));
                        if (scenario == 1 || scenario == 5) Assert.That(highest - 1000, Is.EqualTo(3).Within(.01f));
                        if (scenario == 3) { Assert.That(actor.Position.y, Is.EqualTo(1003)); Assert.That(actor.velocity.y, Is.Zero); }
                        if (scenario == 10) Assert.That(actor.Center.x + actor.size.x / 2, Is.LessThanOrEqualTo(wall.bounds.min.x + .01f));
                        if (scenario == 11) Assert.That(highest + actor.size.y, Is.LessThanOrEqualTo(ceiling.bounds.min.y + .01f));
                        if (scenario == 12) { Assert.That(dropped, Is.True); Assert.That(actor.Position.y, Is.EqualTo(1000).Within(.01f)); }
                        Assert.That(arena.SimulationFaulted, Is.False);
                    }
                }
                Assert.That(trace.rows.Count, Is.EqualTo(1560)); var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorMotionTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(actor.gameObject); Object.Destroy(floor.gameObject); Object.Destroy(wall.gameObject); Object.Destroy(ceiling.gameObject); Object.Destroy(platform.gameObject); }
        }
    }
}
