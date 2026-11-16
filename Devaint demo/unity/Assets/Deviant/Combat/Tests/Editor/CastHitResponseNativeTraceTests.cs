using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class CastHitResponseNativeProbe : TestEnemy
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    // Frozen against the pre-extraction Cast/Actor public API. Actual Native
    // geometry, Receive, actor status and the existing simulation clock supply
    // every observation; no private execution-owner state is inspected.
    public class CastHitResponseNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int hits, otherHits, callbacks, available, resourceStatus;
            public bool moving, current, resources, targetBoss, targetPlayer, otherPlayer, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GamePreservesClaimsPrimaryResponsesAndLivePush() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabPreservesClaimsPrimaryResponsesAndLivePush() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static void Record(Trace trace, string scenario, string phase, CombatArena arena, CombatCast cast,
            CastHitResponseNativeProbe target, CastHitResponseNativeProbe other, int callbacks = 0)
        {
            var p = arena.Player; var receipt = cast.ResourceResult;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, hits = target.ReceivedHitCount,
                otherHits = other.ReceivedHitCount, callbacks = callbacks, available = p.Weapon.Cooldown.Available,
                resourceStatus = receipt == null ? -1 : (int)receipt.Status, moving = cast.Moving,
                current = ReferenceEquals(p.currentCast, cast), resources = cast.ResourcesActive,
                targetBoss = target.isBoss, targetPlayer = target.isPlayer, otherPlayer = other.isPlayer, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y),
                    Bits(target.Position.x), Bits(target.Position.y), Bits(target.health), Bits(target.knockUntil), Bits(target.knockSpeed),
                    Bits(target.stunUntil), Bits(other.Position.x), Bits(other.Position.y), Bits(other.health), Bits(other.knockUntil),
                    Bits(other.knockSpeed), Bits(other.stunUntil), Bits(cast.StartedAt), Bits(cast.EndTime), Bits(cast.RecastRemaining),
                    Bits(receipt?.Snapshot?.DamageMultiplier ?? -1), Bits(p.Weapon.Cooldown.NextReady) } });
        }
        static SkillData Setup(CombatArena arena, CombatArena.ManualSimulation driver, int facing,
            CastHitResponseNativeProbe target, CastHitResponseNativeProbe other)
        {
            target.OnCancel = other.OnCancel = null; target.isPlayer = target.isBoss = other.isPlayer = other.isBoss = false;
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(facing);
            target.ResetActor(new Vector2(1.25f * facing, 1000)); other.ResetActor(new Vector2(2.25f * facing, 1000));
            target.SyncFacing(-facing); other.SyncFacing(-facing); Physics2D.SyncTransforms();
            return new SkillData { id = "response-native", vertical = "normal", activation = 0, end = 1,
                cooldown = 1, capacity = 1, invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                hits = new[] { new HitData { time = 0, active = .75f, damage = 5, anchor = "actor", targets = "all",
                    box = new BoxData { x = 1.5f, y = 1, width = 4, height = 2 } } },
                movement = new MoveData { type = "dash", start = 0, duration = .5f, speed = 4, distance = 2,
                    wallRecovery = .25f, primaryPush = true, endDamage = 17 } };
        }
        static CombatCast Start(CombatArena arena, SkillData source)
        {
            var cast = CombatCast.Create(arena, arena.Player, source, arena.Player.Weapon, false);
            arena.Player.currentCast = cast; cast.BeforeMovement(.125f); return cast;
        }
        static void Finish(CombatCast cast)
        {
            cast.BeforeMovement(.5f); cast.AfterMovement(); cast.Step(.5f);
        }
        static void OrdinaryCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver,
            CastHitResponseNativeProbe target, CastHitResponseNativeProbe other)
        {
            foreach (int facing in new[] { 1, -1 })
            foreach (string mode in new[] { "regular", "boss", "dead-primary", "cancel-before", "cancel-after", "push-only", "nearest-recast" })
            {
                var source = Setup(arena, driver, facing, target, other); string scenario = mode + "/" + facing;
                target.isBoss = mode == "boss"; other.Position = new Vector2(40, 1000);
                if (mode == "push-only") { source.movement.primaryPush = false; source.hits[0].pushWithCaster = true; }
                if (mode == "nearest-recast")
                {
                    source.hits[0].targets = "nearest"; source.movement.type = "target_dash_recast";
                    source.movement.primaryPush = false; source.movement.recastWindow = .5f;
                }
                Physics2D.SyncTransforms(); var cast = Start(arena, source);
                if (mode == "cancel-before") cast.Cancel();
                Record(trace, scenario, "prepared", arena, cast, target, other);
                cast.Step(.125f); cast.PushTargets(.125f);
                Record(trace, scenario, "first-push", arena, cast, target, other);
                Assert.That(target.ReceivedHitCount, Is.EqualTo(mode == "cancel-before" ? 0 : mode == "boss" ? 2 : 1), scenario);
                if (mode == "regular" || mode == "push-only")
                { Assert.That(target.knockUntil, Is.EqualTo(.125f), scenario); Assert.That(target.knockSpeed, Is.EqualTo(4 * facing), scenario); }
                if (mode == "boss") { Assert.That(cast.Moving, Is.False); Assert.That(target.knockUntil, Is.Zero); Assert.That(cast.EndTime, Is.EqualTo(.375f)); }
                if (mode == "nearest-recast") Assert.That(cast.RecastRemaining, Is.EqualTo(.5f));
                if (mode == "dead-primary") target.health = 0;
                if (mode == "cancel-after") cast.Cancel();
                other.Position = new Vector2(2.25f * facing, 1000); Physics2D.SyncTransforms(); Finish(cast);
                if (mode == "dead-primary") { target.health = 100; cast.Step(.5f); }
                int expected = mode == "cancel-before" ? 0 : mode == "regular" || mode == "boss" ? 2 : 1;
                Assert.That(target.ReceivedHitCount, Is.EqualTo(expected), scenario);
                Assert.That(other.ReceivedHitCount, Is.EqualTo(mode == "cancel-before" || mode == "cancel-after" || mode == "nearest-recast" ? 0 : 1), scenario);
                if (mode == "dead-primary") Assert.That(target.health, Is.EqualTo(100), "A dead primary spends the end-hit claim before revival.");
                if (mode == "nearest-recast") Assert.That(target.health, Is.EqualTo(95));
                Record(trace, scenario, "final", arena, cast, target, other);
            }
        }
        static void LivePushCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver,
            CastHitResponseNativeProbe target, CastHitResponseNativeProbe other)
        {
            var source = Setup(arena, driver, -1, target, other); source.hits[0].pushWithCaster = true;
            var cast = Start(arena, source); cast.Step(.125f); cast.PushTargets(.125f);
            Assert.That(target.knockSpeed, Is.EqualTo(-4)); Assert.That(other.knockSpeed, Is.EqualTo(-4));
            Record(trace, "live-push", "both", arena, cast, target, other);
            arena.enemies.Remove(target); target.SetKnockMotion(0, 0); other.health = 0;
            try { cast.PushTargets(.25f); Assert.That(target.knockUntil, Is.Zero); Assert.That(other.knockUntil, Is.EqualTo(.125f)); }
            finally { arena.enemies.Add(target); }
            Record(trace, "live-push", "removed-and-dead", arena, cast, target, other);
            other.health = 100; cast.PushTargets(.375f);
            Assert.That(target.knockUntil, Is.EqualTo(.375f)); Assert.That(other.knockUntil, Is.EqualTo(.375f));
            Record(trace, "live-push", "restored", arena, cast, target, other);
            cast.Cancel(); cast.PushTargets(.5f);
            Assert.That(target.knockUntil, Is.EqualTo(.375f)); Assert.That(other.knockUntil, Is.EqualTo(.375f));
            Record(trace, "live-push", "cancelled", arena, cast, target, other);
        }
        static void ReceiveCallbackCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver,
            CastHitResponseNativeProbe target, CastHitResponseNativeProbe other)
        {
            foreach (string scenario in new[] { "promote-boss", "demote-boss", "reject-after-collection", "cancel-in-receive", "reenter-in-receive", "throw-in-receive" })
            {
                var source = Setup(arena, driver, 1, target, other); source.hits[0].stun = .01f; source.hits[0].pushWithCaster = true;
                target.isBoss = scenario == "demote-boss";
                if (scenario == "reject-after-collection") { source.invulStart = new[] { 0f }; source.invulEnd = new[] { 1f }; }
                var cast = Start(arena, source); int callbacks = 0; var failure = new InvalidOperationException("Native response Receive failure");
                target.OnCancel = () =>
                {
                    callbacks++; Record(trace, scenario, "receive", arena, cast, target, other, callbacks);
                    if (callbacks != 1) return;
                    if (scenario == "promote-boss") target.isBoss = true;
                    if (scenario == "demote-boss") target.isBoss = false;
                    if (scenario == "reject-after-collection") other.isPlayer = true;
                    if (scenario == "cancel-in-receive") cast.Cancel();
                    if (scenario == "reenter-in-receive") cast.Step(.125f);
                    if (scenario == "throw-in-receive") throw failure;
                };
                try
                {
                    if (scenario == "throw-in-receive") Assert.That(Assert.Throws<InvalidOperationException>(() => cast.Step(.125f)), Is.SameAs(failure));
                    else cast.Step(.125f);
                    Record(trace, scenario, "returned", arena, cast, target, other, callbacks);
                    target.OnCancel = null; other.isPlayer = false; cast.Step(.125f); cast.PushTargets(.125f);
                    Assert.That(target.ReceivedHitCount, Is.EqualTo(scenario == "promote-boss" ? 2 : 1), scenario);
                    Assert.That(other.ReceivedHitCount, Is.EqualTo(scenario == "reject-after-collection" ? 0 : 1), scenario);
                    if (scenario == "demote-boss") { Assert.That(cast.Moving, Is.True); Assert.That(target.knockSpeed, Is.EqualTo(4)); }
                    if (scenario == "promote-boss") { Assert.That(cast.Moving, Is.False); Assert.That(target.health, Is.EqualTo(78)); }
                    if (scenario == "throw-in-receive") Assert.That(target.knockUntil, Is.Zero, "The consumed target claim does not manufacture accepted response effects.");
                    Record(trace, scenario, "retry-and-push", arena, cast, target, other, callbacks);
                    Finish(cast);
                    Assert.That(target.ReceivedHitCount, Is.EqualTo(scenario == "promote-boss" || scenario == "demote-boss" || scenario == "reject-after-collection" ? 2 : 1), scenario);
                    Assert.That(other.ReceivedHitCount, Is.EqualTo(scenario == "reject-after-collection" ? 0 : scenario == "reenter-in-receive" || scenario == "throw-in-receive" ? 2 : 1), scenario);
                    Assert.That(callbacks, Is.EqualTo(1)); Record(trace, scenario, "final", arena, cast, target, other, callbacks);
                }
                finally { target.OnCancel = null; other.isPlayer = false; }
            }
        }
        static void FinalSweepCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver,
            CastHitResponseNativeProbe target, CastHitResponseNativeProbe other)
        {
            foreach (string scenario in new[] { "final-sweep", "end-reentry", "end-throw" })
            {
                var source = Setup(arena, driver, 1, target, other); target.health = 22; other.Position = new Vector2(40, 1000);
                Physics2D.SyncTransforms(); var cast = Start(arena, source); cast.Step(.125f);
                Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Assert.That(target.health, Is.EqualTo(17));
                other.Position = new Vector2(2.25f, 1000); Physics2D.SyncTransforms(); int callbacks = 0;
                var failure = new InvalidOperationException("Native response end-hit failure");
                target.OnCancel = () =>
                {
                    callbacks++; Assert.That(other.ReceivedHitCount, Is.EqualTo(1), "The final Native sweep must precede EndPrimary.");
                    Record(trace, scenario, "end-receive", arena, cast, target, other, callbacks);
                    if (scenario == "end-reentry") { target.health = 31; cast.Step(.5f); }
                    if (scenario == "end-throw") throw failure;
                };
                try
                {
                    if (scenario == "end-throw") Assert.That(Assert.Throws<InvalidOperationException>(() => Finish(cast)), Is.SameAs(failure));
                    else Finish(cast);
                    target.OnCancel = null; target.health = 31; cast.Step(.5f); cast.PushTargets(.5f);
                    Assert.That(callbacks, Is.EqualTo(1)); Assert.That(target.ReceivedHitCount, Is.EqualTo(2));
                    Assert.That(other.ReceivedHitCount, Is.EqualTo(1)); Assert.That(target.health, Is.EqualTo(31));
                    Record(trace, scenario, "repeated", arena, cast, target, other, callbacks);
                }
                finally { target.OnCancel = null; }
            }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("CastHitResponseNativeFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            var target = new GameObject("CastHitResponseNativeTarget").AddComponent<CastHitResponseNativeProbe>();
            var other = new GameObject("CastHitResponseNativeOther").AddComponent<CastHitResponseNativeProbe>();
            target.Configure(arena, new Vector2(40, 1000), new Vector2(.5f, 1.8f), new Vector2(0, .9f), false);
            other.Configure(arena, new Vector2(42, 1000), new Vector2(.5f, 1.8f), new Vector2(0, .9f), false);
            target.spawn = new Vector2(40, 1000); other.spawn = new Vector2(42, 1000); arena.enemies.Add(target); arena.enemies.Add(other);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    OrdinaryCases(trace, arena, driver, target, other); LivePushCases(trace, arena, driver, target, other);
                    ReceiveCallbackCases(trace, arena, driver, target, other); FinalSweepCases(trace, arena, driver, target, other);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-castHitResponseTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally
            {
                target.OnCancel = other.OnCancel = null; arena.enemies.Remove(target); arena.enemies.Remove(other);
                Object.Destroy(target.gameObject); Object.Destroy(other.gameObject); Object.Destroy(floor);
            }
        }
    }
}
