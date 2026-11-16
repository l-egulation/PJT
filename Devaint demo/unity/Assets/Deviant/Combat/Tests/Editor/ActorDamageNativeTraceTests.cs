using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class ActorDamageProbe : CombatActor
    {
        public readonly List<int[]> Callbacks = new List<int[]>();
        public readonly List<int[]> AcceptedCallbacks = new List<int[]>();
        public Action OnCancel;
        public Action<CombatActor, int> OnAccepted;
        public override void CancelAttack()
        {
            Callbacks.Add(ActorDamageNativeTraceTests.Values(this));
            OnCancel?.Invoke(); base.CancelAttack();
        }
        protected override void OnAcceptedHit(CombatActor source, int hitDirection)
        {
            base.OnAcceptedHit(source, hitDirection);
            AcceptedCallbacks.Add(ActorDamageNativeTraceTests.Values(this));
            OnAccepted?.Invoke(source, hitDirection);
        }
    }

    public class ActorDamageNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string label;
            public bool accepted, failed;
            public int[] state, player;
            public List<int[]> callbacks, records;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        public static int[] Values(CombatActor actor) => new[]
        {
            Bits(actor.health), actor.ReceivedHitCount, Bits(actor.staggerUntil), Bits(actor.rootUntil), Bits(actor.stunUntil),
            Bits(actor.moveLockUntil), Bits(actor.knockUntil), Bits(actor.knockSpeed), actor.isPlayer ? 1 : 0,
            actor.isBoss ? 1 : 0, actor.isDecoy ? 1 : 0, actor.visual ? Bits(actor.visual.color.r) : 0
        };
        static bool Observe(ActorDamageProbe probe, List<int[]> records, Func<bool> receive, Action recorded = null)
        {
            Application.LogCallback observe = (message, stack, type) =>
            {
                if (!message.Contains(probe.name + " damage=")) return;
                records.Add(Values(probe)); recorded?.Invoke();
            };
            Application.logMessageReceived += observe;
            // Bind only during delivery: the fixture publishes real accepted
            // facts without becoming a decoy owned by arena reset/stepping.
            var arena = probe.arena;
            arena.decoys.Add(probe);
            try { return receive(); }
            finally { Application.logMessageReceived -= observe; arena.decoys.Remove(probe); }
        }
        static void Record(Trace trace, string label, ActorDamageProbe probe, CombatPlayer player, bool accepted, List<int[]> records, bool failed = false)
            => trace.rows.Add(new Row { label = label, accepted = accepted, failed = failed, state = Values(probe), player = new[] { Bits(player.health), player.ReceivedHitCount, Bits(player.axis) }, callbacks = new List<int[]>(probe.Callbacks), records = new List<int[]>(records) });

        [UnityTest] public IEnumerator GameKeepsLiveDamageControlsCallbacksAndDeathPhases() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsLiveDamageControlsCallbacksAndDeathPhases() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace();
            var probe = new GameObject("ActorDamageProbe").AddComponent<ActorDamageProbe>();
            probe.Configure(arena, new Vector2(0, 1000), Vector2.one, Vector2.zero, false);
            probe.visual = probe.gameObject.AddComponent<SpriteRenderer>();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    var records = new List<int[]>();
                    Action prepare = () =>
                    {
                        probe.OnCancel = null; probe.OnAccepted = null; probe.isPlayer = probe.isBoss = probe.isDecoy = false;
                        driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                        probe.ResetActor(new Vector2(0, 1000)); probe.visual.color = Color.white;
                        probe.Callbacks.Clear(); probe.AcceptedCallbacks.Clear(); records.Clear();
                    };
                    prepare();
                    var mutable = new HitData { root = .1f, pushSeconds = .1f };
                    probe.OnCancel = () =>
                    {
                        if (probe.Callbacks.Count == 1) { mutable.root = .75f; mutable.stun = .5f; mutable.push = 2; mutable.pushSeconds = .25f; }
                        else { mutable.root = 8; mutable.stun = 9; mutable.push = 1.5f; mutable.pushSeconds = .5f; probe.health = 23; }
                    };
                    bool accepted = Observe(probe, records, () => probe.Receive(20, player, -1, true, mutable));
                    Assert.That(accepted, Is.True); Assert.That(probe.Callbacks.Count, Is.EqualTo(2));
                    Assert.That(probe.Callbacks[0][3], Is.Zero); Assert.That(probe.Callbacks[1][3], Is.EqualTo(Bits(.75f)));
                    Assert.That(probe.rootUntil, Is.EqualTo(.75f)); Assert.That(probe.stunUntil, Is.EqualTo(.5f));
                    Assert.That(probe.knockUntil, Is.EqualTo(.5f)); Assert.That(probe.knockSpeed, Is.EqualTo(-3));
                    Assert.That(records.Count, Is.EqualTo(1)); Assert.That(records[0][0], Is.EqualTo(Bits(23)));
                    Record(trace, "mutable-control-after-each-cancel", probe, player, accepted, records);

                    foreach (string mode in new[] { "boss-to-ordinary", "decoy-to-ordinary", "ordinary-to-boss", "ordinary-to-decoy" })
                    {
                        prepare(); probe.isBoss = mode == "boss-to-ordinary"; probe.isDecoy = mode == "decoy-to-ordinary";
                        probe.OnCancel = () => { probe.isBoss = mode == "ordinary-to-boss"; probe.isDecoy = mode == "ordinary-to-decoy"; };
                        accepted = Observe(probe, records, () => probe.Receive(0, player, 1, false, new HitData { stun = .25f, push = 1, pushSeconds = .5f }));
                        Assert.That(probe.Callbacks.Count, Is.EqualTo(1));
                        Assert.That(probe.knockUntil, Is.EqualTo(probe.isBoss || probe.isDecoy ? 0 : .5f));
                        Record(trace, mode, probe, player, accepted, records);
                    }

                    prepare(); mutable = new HitData { root = .625f, stun = .375f, push = 1, pushSeconds = .25f };
                    var immutable = new SkillHitDefinition(mutable);
                    probe.OnCancel = () => { mutable.root = mutable.stun = mutable.push = mutable.pushSeconds = 0; };
                    accepted = Observe(probe, records, () => probe.ReceiveControl(0, player, 1, true, immutable));
                    Assert.That(probe.Callbacks.Count, Is.EqualTo(2)); Assert.That(probe.rootUntil, Is.EqualTo(.625f));
                    Assert.That(probe.stunUntil, Is.EqualTo(.375f)); Assert.That(probe.knockSpeed, Is.EqualTo(4));
                    Record(trace, "immutable-control-reuses-start-definition", probe, player, accepted, records);

                    prepare(); probe.OnCancel = () => probe.isPlayer = true;
                    accepted = Observe(probe, records, () => probe.Receive(1, player, 1, true, new HitData { root = .5f, stun = .25f, push = 1, pushSeconds = .5f }));
                    Assert.That(probe.Callbacks.Count, Is.EqualTo(2)); Assert.That(probe.moveLockUntil, Is.Zero);
                    Assert.That(probe.rootUntil, Is.EqualTo(.5f)); Assert.That(probe.knockSpeed, Is.EqualTo(2));
                    Record(trace, "entered-enemy-branch-survives-player-promotion", probe, player, accepted, records);

                    prepare(); bool nestedAccepted = false;
                    probe.OnCancel = () => nestedAccepted = probe.Receive(7, player, 1, false, null);
                    accepted = Observe(probe, records, () => probe.Receive(10, player, 1, true, new HitData { root = .5f, push = 1, pushSeconds = .5f }));
                    Assert.That(nestedAccepted && accepted, Is.True); Assert.That(probe.health, Is.EqualTo(83));
                    Assert.That(probe.ReceivedHitCount, Is.EqualTo(2)); Assert.That(records.Count, Is.EqualTo(2));
                    Assert.That(records[0][3], Is.Zero); Assert.That(records[1][3], Is.EqualTo(Bits(.5f)));
                    Record(trace, "nested-hit-before-outer-control", probe, player, accepted, records);

                    var deathCastData = new SkillData { id = "actor_damage_death_probe", end = 1, vertical = "normal", invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(), hits = Array.Empty<HitData>() };
                    prepare(); probe.health = 5; player.axis = 1;
                    var deathCast = CombatCast.Create(arena, player, deathCastData, player.Weapon, false); player.currentCast = deathCast;
                    probe.OnCancel = () => { probe.health = 25; probe.isPlayer = true; };
                    accepted = Observe(probe, records, () => probe.Receive(10, player, 1, false, null));
                    Assert.That(probe.health, Is.EqualTo(25)); Assert.That(probe.Callbacks.Count, Is.EqualTo(1));
                    Assert.That(records[0][0], Is.EqualTo(Bits(0))); Assert.That(records[0][8], Is.Zero);
                    Assert.That(player.currentCast, Is.Null); Assert.That(probe.visual.color, Is.EqualTo(Color.gray));
                    Record(trace, "death-cancel-revives-and-promotes-before-native-die", probe, player, accepted, records);

                    prepare(); probe.isPlayer = true; player.axis = 1;
                    deathCast = CombatCast.Create(arena, player, deathCastData, player.Weapon, false); player.currentCast = deathCast;
                    probe.OnCancel = () => { probe.health = 25; probe.isPlayer = false; };
                    accepted = Observe(probe, records, () => probe.Receive(0, player, 1, false, null), () => probe.health = 0);
                    Assert.That(probe.Callbacks.Count, Is.EqualTo(1)); Assert.That(records[0][0], Is.EqualTo(Bits(100)));
                    Assert.That(probe.moveLockUntil, Is.EqualTo(.12f)); Assert.That(probe.health, Is.EqualTo(25));
                    Assert.That(player.currentCast, Is.SameAs(deathCast)); Assert.That(probe.visual.color, Is.EqualTo(Color.gray));
                    Record(trace, "record-callback-kills-then-death-cancel-demotes", probe, player, accepted, records);

                    foreach (bool immutableControl in new[] { false, true })
                    {
                        prepare(); probe.shield = 5;
                        Assert.That(arena.EnemyHitReaction.BasicStaggerSeconds, Is.EqualTo(.3f));
                        Assert.That(arena.EnemyHitReaction.BasicPushDistance, Is.EqualTo(.25f));
                        Assert.That(arena.EnemyHitReaction.BasicPushSeconds, Is.EqualTo(.1f));
                        Assert.That(arena.HitEvents.TryGetCursor(out var cursor), Is.True);
                        Assert.That(arena.TryBindActor(player, out var sourceHandle), Is.True);
                        var origin = new CombatHitOrigin(sourceHandle, default, Guid.NewGuid(), 7, "accepted-hook", Guid.NewGuid(), CombatHitKind.Projectile);
                        var hit = new HitData { root = .4f };
                        probe.OnAccepted = (source, direction) =>
                        {
                            Assert.That(source, Is.SameAs(player)); Assert.That(direction, Is.EqualTo(-1));
                            Assert.That(probe.ReceivedHitCount, Is.EqualTo(1)); Assert.That(probe.health, Is.EqualTo(100));
                            Assert.That(probe.rootUntil, Is.EqualTo(.4f)); Assert.That(probe.knockSpeed, Is.EqualTo(-2.5f));
                            Assert.That(records.Count, Is.EqualTo(1), "The common hook runs after normal damage/control processing.");
                            var committed = new CombatAcceptedHit[4]; Assert.That(arena.HitEvents.TryRead(cursor, committed, out var page), Is.True);
                            Assert.That(page.Count, Is.EqualTo(1)); Assert.That(committed[0].Origin.ExecutionId, Is.EqualTo(origin.ExecutionId));
                            Assert.That(committed[0].Origin.HitIndex, Is.EqualTo(7)); Assert.That(committed[0].Absorbed, Is.EqualTo(4));
                        };
                        accepted = Observe(probe, records, () => immutableControl
                            ? probe.ReceiveControl(4, player, -1, true, new SkillHitDefinition(hit), projectileHit: true, origin: origin)
                            : probe.Receive(4, player, -1, true, hit, projectileHit: true, origin: origin));
                        Assert.That(accepted, Is.True); Assert.That(probe.AcceptedCallbacks.Count, Is.EqualTo(1));
                        Record(trace, immutableControl ? "immutable-common-accepted-hook" : "mutable-common-accepted-hook", probe, player, accepted, records);

                        probe.OnAccepted = null; probe.health = 0;
                        Assert.That(immutableControl ? probe.ReceiveControl(1, player, 1, false, new SkillHitDefinition(hit)) : probe.Receive(1, player, 1, false, hit), Is.False);
                        Assert.That(probe.AcceptedCallbacks.Count, Is.EqualTo(1), "Rejected hits cannot invoke the common hook.");

                        prepare(); probe.health = 1;
                        probe.OnAccepted = (source, direction) => Assert.That(probe.visual.color, Is.EqualTo(Color.gray), "Death processing precedes the accepted hook.");
                        accepted = Observe(probe, records, () => immutableControl
                            ? probe.ReceiveControl(2, player, 1, false, new SkillHitDefinition(hit))
                            : probe.Receive(2, player, 1, false, hit));
                        Assert.That(accepted, Is.True); Assert.That(probe.AcceptedCallbacks.Count, Is.EqualTo(1));

                        prepare(); probe.isPlayer = true;
                        driver.Step(.02f, new CombatInput(0, CombatButtons.Dash)); Assert.That(player.Invulnerable, Is.True);
                        accepted = Observe(probe, records, () => immutableControl
                            ? probe.ReceiveControl(1, player, 1, false, new SkillHitDefinition(hit))
                            : probe.Receive(1, player, 1, false, hit));
                        Assert.That(accepted, Is.False); Assert.That(probe.AcceptedCallbacks, Is.Empty); Assert.That(probe.ReceivedHitCount, Is.Zero);
                        Assert.That(records, Is.Empty, "Invulnerability cannot run damage recording or the post-hit hook.");
                    }
                    foreach (float step in new[] { .02f, .03f })
                    {
                        prepare(); Assert.That(probe.Receive(0, player, 1, true, null), Is.True);
                        for (int tick = 0; arena.Now < .1f; tick++) { probe.PhysicsStep(step, 9, true); driver.Step(step); }
                        Assert.That(probe.Position.x, Is.EqualTo(.25f).Within(.000001f));
                        probe.PhysicsStep(step, 9, true); Assert.That(probe.VelocityX, Is.Zero, "The remaining stagger still blocks voluntary movement after push expiry.");
                        Record(trace, "ordinary-basic-push-partial-tick-" + step, probe, player, true, records);
                    }
                    prepare(); var hookCounts = new List<int>();
                    probe.OnAccepted = (source, direction) =>
                    {
                        hookCounts.Add(probe.ReceivedHitCount);
                        if (hookCounts.Count == 1) Assert.That(probe.ReceiveControl(7, player, -1, false, new SkillHitDefinition(new HitData())), Is.True);
                    };
                    Assert.That(arena.HitEvents.TryGetCursor(out var nestedCursor), Is.True);
                    Assert.That(Observe(probe, records, () => probe.Receive(10, player, 1, false, null)), Is.True);
                    Assert.That(hookCounts, Is.EqualTo(new[] { 1, 2 })); Assert.That(probe.health, Is.EqualTo(83));
                    Assert.That(records.Count, Is.EqualTo(2)); Assert.That(probe.AcceptedCallbacks.Count, Is.EqualTo(2));
                    var nestedHits = new CombatAcceptedHit[4]; Assert.That(arena.HitEvents.TryRead(nestedCursor, nestedHits, out var nestedPage), Is.True);
                    Assert.That(nestedPage.Count, Is.EqualTo(2)); Assert.That(nestedHits[0].After.ReceivedHits, Is.EqualTo(1)); Assert.That(nestedHits[1].After.ReceivedHits, Is.EqualTo(2));

                    prepare(); var acceptedFailure = new InvalidOperationException("accepted hook");
                    Assert.That(arena.HitEvents.TryGetCursor(out var failureCursor), Is.True);
                    probe.OnAccepted = (source, direction) => throw acceptedFailure;
                    Assert.That(Assert.Throws<InvalidOperationException>(() => Observe(probe, records, () => probe.Receive(2, player, 1, false, null))), Is.SameAs(acceptedFailure));
                    Assert.That(probe.health, Is.EqualTo(98)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(1)); Assert.That(records.Count, Is.EqualTo(1));
                    var failureHits = new CombatAcceptedHit[4]; Assert.That(arena.HitEvents.TryRead(failureCursor, failureHits, out var failurePage), Is.True);
                    Assert.That(failurePage.Count, Is.EqualTo(1), "A throwing post-hit hook cannot retract or duplicate the accepted fact.");

                    foreach (bool immutableControl in new[] { false, true })
                    {
                        prepare(); Assert.That(arena.HitEvents.TryGetCursor(out var oldCursor), Is.True); bool reset = false;
                        accepted = Observe(probe, records, () => immutableControl
                            ? probe.ReceiveControl(2, player, 1, false, new SkillHitDefinition(new HitData()))
                            : probe.Receive(2, player, 1, false, null), () =>
                        {
                            if (reset) return; reset = true;
                            // The standalone fixture is not owned by arena reset.
                            arena.decoys.Remove(probe); arena.ResetTest(); probe.ResetActor(new Vector2(0, 1000));
                        });
                        Assert.That(reset && accepted, Is.True); Assert.That(probe.AcceptedCallbacks, Is.Empty, "An old accepted hit cannot postprocess a reset generation.");
                        Assert.That(probe.health, Is.EqualTo(100)); Assert.That(probe.ReceivedHitCount, Is.Zero);
                        Assert.That(arena.HitEvents.TryRead(oldCursor, new CombatAcceptedHit[4], out _), Is.False);
                        Assert.That(arena.ReadModel.TryGetSession(out var currentSession), Is.True);
                        var resetHits = new CombatAcceptedHit[4];
                        Assert.That(arena.HitEvents.TryRead(new CombatOutcomeCursor(currentSession, oldCursor.AfterEventSequence), resetHits, out var resetPage), Is.True);
                        Assert.That(resetPage.Count, Is.EqualTo(1)); Assert.That(resetHits[0].Target.Session.Equals(oldCursor.Session), Is.True);
                        Assert.That(resetHits[0].After.Health, Is.EqualTo(98), "The original accepted fact remains committed before reset.");
                    }

                    foreach (string phase in new[] { "basic", "stun", "death" })
                    {
                        prepare(); var failure = new InvalidOperationException("damage callback " + phase);
                        probe.OnCancel = () => throw failure;
                        if (phase == "death") probe.health = 1;
                        var control = phase == "death" ? null : new HitData { root = .5f, stun = .25f, push = 1, pushSeconds = .5f };
                        var thrown = Assert.Throws<InvalidOperationException>(() => Observe(probe, records, () => probe.Receive(2, player, 1, phase == "basic", control)));
                        Assert.That(thrown, Is.SameAs(failure)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(1));
                        Assert.That(probe.Callbacks.Count, Is.EqualTo(1)); Assert.That(probe.knockUntil, Is.Zero);
                        Assert.That(probe.AcceptedCallbacks, Is.Empty, "A failed existing phase cannot run the post-hit extension.");
                        Assert.That(probe.rootUntil, Is.EqualTo(phase == "stun" ? .5f : 0));
                        Assert.That(probe.stunUntil, Is.EqualTo(phase == "stun" ? .25f : 0));
                        Assert.That(records.Count, Is.EqualTo(phase == "death" ? 1 : 0));
                        Assert.That(probe.visual.color, Is.EqualTo(Color.white));
                        Record(trace, "throw-at-" + phase, probe, player, false, records, true);
                    }
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorDamageTraceRoot");
                if (index >= 0 && index + 1 < args.Length)
                {
                    Directory.CreateDirectory(args[index + 1]);
                    File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace));
                }
            }
            finally { probe.OnCancel = null; probe.OnAccepted = null; Object.Destroy(probe.gameObject); }
        }
    }
}
