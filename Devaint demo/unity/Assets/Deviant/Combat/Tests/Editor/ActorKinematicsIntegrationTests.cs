using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class ActorKinematicsResetProbe : CombatActor
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }
    public class ActorKinematicsIntegrationTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int[] bits;
            public int facing;
            public bool grounded, wall;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        sealed class Query : IActorPlacementQuery
        {
            public readonly List<ActorBoxCast> Requests = new List<ActorBoxCast>();
            public Func<ActorBoxCast, IReadOnlyList<ActorSurfaceContact>> Cast;
            public IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request) { Requests.Add(request); return Cast(request); }
            public IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request) => throw new InvalidOperationException("This motor regression must not query placement overlaps.");
            public IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request) => throw new InvalidOperationException("This motor regression must not query floor rays.");
        }
        static readonly ActorSurfaceContact[] Empty = Array.Empty<ActorSurfaceContact>();
        static readonly FieldInfo Surfaces = typeof(CombatActor).GetField("surfaces", BindingFlags.Instance | BindingFlags.NonPublic);
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Record(Trace trace, string scenario, string phase, CombatActor actor)
        {
            var state = actor.Kinematics; var native = actor.transform.position;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, facing = state.Facing, grounded = state.Grounded, wall = state.WallHit,
                bits = new[] { Bits(state.X), Bits(state.Y), Bits(state.VelocityX), Bits(state.VelocityY), Bits(native.x), Bits(native.y), Bits(native.z) } });
        }
        [Test] public void NativePortsUseOneOwnerAndWholeVectorReadsAreOnlyCopies()
        {
            var root = new GameObject("KinematicsPorts"); var otherRoot = new GameObject("OtherKinematicsPorts");
            try
            {
                var actor = root.AddComponent<CombatActor>(); var other = otherRoot.AddComponent<CombatActor>();
                actor.SetPosition(new Vector3(2, 3, 9)); actor.velocity = new Vector2(5, -7); actor.facing = -1; actor.grounded = actor.wallHit = true;
                var before = actor.Kinematics; var velocityCopy = actor.velocity; velocityCopy.y = 123;
                Assert.That(actor.VelocityY, Is.EqualTo(-7)); actor.VelocityX = 11; actor.VelocityY = 13;
                Assert.That(actor.velocity, Is.EqualTo(new Vector2(11, 13))); Assert.That(actor.Position, Is.EqualTo(new Vector2(2, 3)));
                Assert.That(actor.transform.position, Is.EqualTo(new Vector3(2, 3, 9)));
                actor.Position = new Vector2(17, 19); Assert.That(actor.transform.position, Is.EqualTo(new Vector3(17, 19, 0)));
                Assert.That(before.X, Is.EqualTo(2)); Assert.That(before.Y, Is.EqualTo(3)); Assert.That(before.VelocityX, Is.EqualTo(5)); Assert.That(before.VelocityY, Is.EqualTo(-7));
                Assert.That(before.Facing, Is.EqualTo(-1)); Assert.That(before.Grounded && before.WallHit, Is.True);
                Assert.That(other.Position, Is.EqualTo(Vector2.zero)); Assert.That(other.velocity, Is.EqualTo(Vector2.zero)); Assert.That(other.facing, Is.EqualTo(1)); Assert.That(other.grounded || other.wallHit, Is.False);
                foreach (string name in new[] { "velocity", "facing", "grounded", "wallHit" }) Assert.That(typeof(CombatActor).GetField(name), Is.Null);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(otherRoot); }
        }
        [Test] public void ConfigureAndResetKeepOriginalBoundariesAndCancellationFailures()
        {
            var root = new GameObject("KinematicsReset");
            try
            {
                var actor = root.AddComponent<ActorKinematicsResetProbe>(); actor.velocity = new Vector2(5, -7); actor.facing = -1; actor.grounded = actor.wallHit = true;
                actor.Configure(null, new Vector2(2, 3), Vector2.one, Vector2.up, false);
                Assert.That(actor.Position, Is.EqualTo(new Vector2(2, 3))); Assert.That(actor.velocity, Is.EqualTo(new Vector2(5, -7))); Assert.That(actor.facing, Is.EqualTo(-1)); Assert.That(actor.grounded && actor.wallHit, Is.True);
                actor.SetPosition(new Vector3(2, 3, 9)); actor.health = 40; var before = actor.Kinematics;
                actor.OnCancel = () =>
                {
                    Assert.That(actor.Position, Is.EqualTo(new Vector2(2, 3))); Assert.That(actor.transform.position, Is.EqualTo(new Vector3(2, 3, 9))); Assert.That(actor.velocity, Is.EqualTo(new Vector2(5, -7))); Assert.That(actor.health, Is.EqualTo(40));
                    actor.Position = new Vector2(11, 13); actor.velocity = new Vector2(17, 19); actor.facing = 23;
                };
                actor.ResetActor(new Vector2(-2, -3));
                Assert.That(actor.Position, Is.EqualTo(new Vector2(-2, -3))); Assert.That(actor.transform.position, Is.EqualTo(new Vector3(-2, -3, 0))); Assert.That(actor.velocity, Is.EqualTo(Vector2.zero));
                Assert.That(actor.facing, Is.EqualTo(23)); Assert.That(actor.wallHit, Is.True); Assert.That(actor.grounded, Is.False); Assert.That(actor.health, Is.EqualTo(100));
                Assert.That(before.X, Is.EqualTo(2)); Assert.That(before.Y, Is.EqualTo(3)); Assert.That(before.VelocityY, Is.EqualTo(-7)); Assert.That(before.Facing, Is.EqualTo(-1)); Assert.That(before.Grounded && before.WallHit, Is.True);
                var failure = new InvalidOperationException("Reset cancellation failed"); actor.health = 70; actor.grounded = true;
                actor.OnCancel = () => { actor.Position = new Vector2(7, 11); actor.VelocityY = 13; throw failure; };
                Assert.That(Assert.Throws<InvalidOperationException>(() => actor.ResetActor(new Vector2(-5, -7))), Is.SameAs(failure));
                Assert.That(actor.Position, Is.EqualTo(new Vector2(7, 11))); Assert.That(actor.transform.position, Is.EqualTo(new Vector3(7, 11, 0))); Assert.That(actor.VelocityY, Is.EqualTo(13));
                Assert.That(actor.health, Is.EqualTo(70)); Assert.That(actor.grounded && actor.wallHit, Is.True); Assert.That(actor.facing, Is.EqualTo(23));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void RootVisualAliasCommitsWorldPoseBeforeItsOriginalLocalOutput()
        {
            var parent = new GameObject("KinematicsAliasParent");
            try
            {
                parent.transform.position = new Vector3(10, 20, 7); parent.transform.localScale = new Vector3(2, 3, 1);
                var root = new GameObject("KinematicsAliasActor"); root.transform.SetParent(parent.transform, false); var actor = root.AddComponent<CombatActor>();
                actor.Configure(null, new Vector2(30, 40), Vector2.one, new Vector2(.1875f, 1), false); actor.visual = root.AddComponent<SpriteRenderer>();
                Assert.That(actor.SyncFacing(1), Is.True); var expected = parent.transform.TransformPoint(new Vector3(.1875f, 1, 0));
                Assert.That(root.transform.localPosition, Is.EqualTo(new Vector3(.1875f, 1, 0))); Assert.That(root.transform.position, Is.EqualTo(expected)); Assert.That(actor.Position, Is.EqualTo((Vector2)expected));
                actor.isPlayer = true; actor.SetPosition(new Vector3(30, 40, 9)); Assert.That(actor.SyncFacing(1), Is.True);
                Assert.That(actor.Position, Is.EqualTo(new Vector2(30, 40))); Assert.That(root.transform.position, Is.EqualTo(new Vector3(30, 40, 9)));
            }
            finally { Object.DestroyImmediate(parent); }
        }
        [Test] public void NativePosePortsDoNotAllocatePerReadOrWrite()
        {
            var root = new GameObject("KinematicsAllocation");
            try
            {
                var actor = root.AddComponent<CombatActor>(); for (int i = 0; i < 32; i++) ExercisePorts(actor, i);
                GC.GetAllocatedBytesForCurrentThread(); long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 2048; i++) ExercisePorts(actor, i);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero); Assert.That(actor.Position, Is.EqualTo(new Vector2(2047, -2047)));
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void ExercisePorts(CombatActor actor, int value)
        {
            actor.SetPosition(new Vector3(value, -value, 3)); var at = actor.Position; actor.Position = at;
            actor.velocity = new Vector2(value, -value); actor.VelocityX = -value; actor.VelocityY = value;
            actor.facing = value; actor.grounded = (value & 1) == 0; actor.wallHit = (value & 2) == 0;
            var state = actor.Kinematics; actor.velocity = new Vector2(state.VelocityX, state.VelocityY);
        }
        [UnityTest] public IEnumerator GameIgnoresTransformTamperingAndResynchronizesTheNextMotorOutput() => ExerciseMotor("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabIgnoresTransformTamperingAndResynchronizesTheNextMotorOutput() => ExerciseMotor("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator ExerciseMotor(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var root = new GameObject("KinematicsMotorProbe"); var actor = root.AddComponent<CombatActor>();
            actor.Configure(arena, new Vector2(.125f, 1000), new Vector2(.9375f, 2), new Vector2(.1875f, 1), false); var trace = new Trace();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); arena.Player.Position = new Vector2(35, 1000); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                    var query = new Query(); Surfaces.SetValue(actor, query);
                    foreach (bool moving in new[] { false, true })
                    {
                        actor.ResetActor(new Vector2(.125f, 1000)); actor.SetPosition(new Vector3(.125f, 1000, 3)); actor.velocity = new Vector2(19, -7); actor.grounded = actor.wallHit = true;
                        string scenario = moving ? "tamper-moving-axis" : "tamper-both-axes-skipped"; var before = actor.Kinematics;
                        actor.transform.position = new Vector3(27, 2000, 9); Record(trace, scenario, "tampered-view", actor);
                        Assert.That(actor.Position, Is.EqualTo(new Vector2(.125f, 1000))); Assert.That(actor.Center, Is.EqualTo(new Vector2(.3125f, 1001))); Assert.That(actor.BodyFrame.X, Is.EqualTo(.125f));
                        Assert.That(actor.Kinematics.VelocityY, Is.EqualTo(before.VelocityY)); Assert.That(actor.Kinematics.Grounded && actor.Kinematics.WallHit, Is.True);
                        if (!moving)
                        {
                            yield return null;
                            Assert.That(actor.Position, Is.EqualTo(new Vector2(.125f, 1000)), "An intervening frame must not pull Transform edits into the owner.");
                            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(27, 2000, 9)), "No scheduled shadow-sync may disguise the next motor output boundary.");
                        }
                        query.Requests.Clear(); query.Cast = request =>
                        {
                            Assert.That(request.Y, Is.EqualTo(request.Height == .04f ? 1000.025f : 1001)); Assert.That(request.X, Is.EqualTo(.3125f));
                            Assert.That(actor.Position, Is.EqualTo(new Vector2(.125f, 1000))); Assert.That(actor.transform.position, Is.EqualTo(new Vector3(27, 2000, 9)), "Queries read the owner without silently pulling or preemptively rewriting the native view.");
                            Record(trace, scenario, request.Height == .04f ? "ground-query" : "x-query", actor); return Empty;
                        };
                        actor.PhysicsStep(.02f, moving ? 5 : 0, true);
                        Assert.That(query.Requests.Count, Is.EqualTo(moving ? 2 : 1)); Assert.That(actor.Position.x, Is.EqualTo(moving ? .225f : .125f)); Assert.That(actor.Position.y, Is.EqualTo(1000));
                        Assert.That((Vector2)actor.transform.position, Is.EqualTo(actor.Position)); Assert.That(actor.transform.position.z, Is.EqualTo(moving ? 0 : 9)); Assert.That(actor.grounded || actor.wallHit, Is.False);
                        Record(trace, scenario, "motor-output", actor);
                    }
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                Assert.That(trace.rows.Count, Is.EqualTo(7)); var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorKinematicsTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(root); }
        }
    }
}
