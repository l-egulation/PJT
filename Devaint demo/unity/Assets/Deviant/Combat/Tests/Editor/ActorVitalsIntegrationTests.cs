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
    public sealed class ActorVitalsProbe : CombatActor
    {
        public readonly List<int[]> Cancellations = new List<int[]>();
        public override void CancelAttack()
        {
            Cancellations.Add(new[] { ActorVitalsIntegrationTests.Bits(health), ReceivedHitCount, ActorVitalsIntegrationTests.Bits(staggerUntil), ActorVitalsIntegrationTests.Bits(rootUntil), ActorVitalsIntegrationTests.Bits(stunUntil), ActorVitalsIntegrationTests.Bits(knockUntil) });
            base.CancelAttack();
        }
    }
    public class ActorVitalsIntegrationTests
    {
        [Serializable] sealed class Row { public string label; public bool accepted; public int[] bits; public int hits; public List<int[]> callbacks; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        public static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        static void Record(Trace trace, string label, CombatActor actor, bool accepted, ActorVitalsProbe probe = null)
        {
            trace.rows.Add(new Row { label = label, accepted = accepted, hits = actor.ReceivedHitCount, bits = new[] { Bits(actor.maxHealth), Bits(actor.health), Bits(actor.shield), Bits(actor.guardUntil), Bits(actor.guardReduction), Bits(actor.moveLockUntil), Bits(actor.knockUntil), Bits(actor.knockSpeed), Bits(actor.rootUntil), Bits(actor.stunUntil), Bits(actor.staggerUntil) }, callbacks = probe == null ? new List<int[]>() : new List<int[]>(probe.Cancellations) });
        }
        [UnityTest] public IEnumerator GameKeepsDamageGuardShieldAndCallbackOrder() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsDamageGuardShieldAndCallbackOrder() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace();
            var probe = new GameObject("VitalsProbe").AddComponent<ActorVitalsProbe>(); probe.Configure(arena, new Vector2(0, 1000), new Vector2(1, 2), Vector2.up, false);
            var floor = new GameObject("VitalsFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Action prepare = () =>
                    {
                        driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); }
                        player.Position = new Vector2(3, 1000); player.velocity = Vector2.zero; player.SyncFacing(1); probe.maxHealth = 100; probe.isBoss = probe.isDecoy = false; probe.ResetActor(new Vector2(0, 1000)); probe.guardReduction = 0; probe.Cancellations.Clear(); Physics2D.SyncTransforms();
                    };
                    prepare(); bool accepted = probe.Receive(20, player, 1, false, null); Assert.That(probe.health, Is.EqualTo(80)); Record(trace, "plain", probe, accepted, probe);
                    prepare(); probe.shield = 8; probe.guardUntil = 2; probe.guardReduction = .5f; accepted = probe.Receive(20, player, 1, false, null); Assert.That(probe.health, Is.EqualTo(98)); Assert.That(probe.shield, Is.Zero); Record(trace, "guard-shield", probe, accepted, probe);
                    prepare(); probe.shield = 30; probe.guardUntil = 2; accepted = probe.Receive(10, player, 1, true, null); Assert.That(probe.health, Is.EqualTo(100)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(1)); Assert.That(probe.Cancellations.Count, Is.EqualTo(1)); Record(trace, "absorbed-control", probe, accepted, probe);
                    prepare(); probe.shield = 8; probe.guardUntil = 2; probe.guardReduction = .5f; accepted = probe.Receive(20, player, 1, false, null, true); Assert.That(probe.health, Is.EqualTo(80)); Assert.That(probe.shield, Is.EqualTo(8)); Record(trace, "bypass", probe, accepted, probe);
                    prepare(); probe.guardUntil = arena.Now; probe.guardReduction = .5f; accepted = probe.Receive(20, player, 1, false, null); Assert.That(probe.health, Is.EqualTo(80)); Record(trace, "guard-expired", probe, accepted, probe);
                    prepare(); probe.shield = 10; probe.guardUntil = 1; accepted = probe.Receive(0, player, -1, true, new HitData { root = .6f, stun = .4f, push = 1, pushSeconds = .2f }); Assert.That(probe.health, Is.EqualTo(100)); Assert.That(probe.Cancellations.Count, Is.EqualTo(2)); Assert.That(probe.Cancellations[0][3], Is.Zero); Assert.That(probe.Cancellations[1][3], Is.EqualTo(Bits(.6f))); Record(trace, "zero-control-order", probe, accepted, probe);
                    prepare(); probe.health = 5; accepted = probe.Receive(20, player, 1, true, new HitData { root = .6f, stun = .4f, push = 1, pushSeconds = .2f }); Assert.That(probe.Dead, Is.True); Assert.That(probe.Cancellations.Count, Is.EqualTo(3)); Record(trace, "lethal-order", probe, accepted, probe); accepted = probe.Receive(1, player, 1, false, null); Assert.That(accepted, Is.False); Record(trace, "dead-reject", probe, accepted, probe);
                    prepare(); probe.isBoss = true; accepted = probe.Receive(10, player, 1, true, new HitData { root = .6f, stun = .4f, push = 1, pushSeconds = .2f }); Assert.That(probe.staggerUntil, Is.Zero); Assert.That(probe.knockUntil, Is.Zero); Assert.That(probe.stunUntil, Is.EqualTo(.4f)); Record(trace, "boss-controls", probe, accepted, probe);
                    prepare(); probe.shield = 8; probe.guardUntil = .02f; probe.guardReduction = .25f; probe.Receive(4, player, 1, false, null); driver.Step(.02f); probe.PhysicsStep(.02f, 0, false); Assert.That(probe.shield, Is.Zero); Record(trace, "shield-expiry", probe, true, probe); probe.ResetActor(new Vector2(0, 1000)); Assert.That(probe.guardReduction, Is.EqualTo(.25f)); Assert.That(probe.ReceivedHitCount, Is.Zero); Record(trace, "reset", probe, true, probe);

                    prepare(); player.shield = 12; player.guardUntil = 2; player.guardReduction = .25f; accepted = player.Receive(20, probe, -1, false, null); Assert.That(player.health, Is.EqualTo(97)); Assert.That(player.moveLockUntil, Is.EqualTo(.12f)); Assert.That(player.knockSpeed, Is.EqualTo(-5)); Record(trace, "player-first", player, accepted); accepted = player.Receive(10, probe, 1, false, null); Assert.That(player.knockSpeed, Is.EqualTo(-5)); Assert.That(player.ReceivedHitCount, Is.EqualTo(2)); Record(trace, "player-repeat", player, accepted);
                    prepare(); driver.Step(.02f, new Deviant.Combat.Contracts.CombatInput(0, Deviant.Combat.Contracts.CombatButtons.Dash)); accepted = player.Receive(20, probe, 1, false, null); Assert.That(accepted, Is.False); Assert.That(player.ReceivedHitCount, Is.Zero); Record(trace, "dash-reject", player, accepted);
                    prepare(); player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "shield"), 0); driver.Step(.02f, new Deviant.Combat.Contracts.CombatInput(0, Deviant.Combat.Contracts.CombatButtons.Special)); Assert.That(player.currentCast, Is.Not.Null); probe.Position = player.Position + Vector2.right; Physics2D.SyncTransforms(); accepted = player.Receive(20, probe, -1, false, null); Assert.That(accepted, Is.False); Assert.That(probe.health, Is.EqualTo(70)); Assert.That(player.ReceivedHitCount, Is.Zero); Record(trace, "parry-target", player, accepted); Record(trace, "parry-source", probe, false, probe);
                    accepted = player.Receive(20, probe, -1, false, null, projectileHit: true); Assert.That(accepted, Is.True); Record(trace, "projectile-gate", player, accepted);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorVitalsTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(probe.gameObject); Object.Destroy(floor); }
        }
    }
}
