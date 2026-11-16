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
    public sealed class ActorStatusProbe : CombatActor
    {
        public readonly List<int[]> Callbacks = new List<int[]>();
        public Action OnCancel;
        public override void CancelAttack() { Callbacks.Add(ActorStatusIntegrationTests.Values(this)); OnCancel?.Invoke(); base.CancelAttack(); }
    }
    public class ActorStatusIntegrationTests
    {
        [Serializable] sealed class Row { public string label; public bool hidden; public int[] bits; public List<int[]> callbacks; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        public static int[] Values(CombatActor actor) => new[] { Bits(actor.health), actor.ReceivedHitCount, Bits(actor.stunUntil), Bits(actor.rootUntil), Bits(actor.staggerUntil), Bits(actor.moveLockUntil), Bits(actor.knockUntil), Bits(actor.knockSpeed), Bits(actor.stealthUntil), Bits(actor.smokeUntil) };
        static void Record(Trace trace, string label, CombatActor actor, ActorStatusProbe probe = null) => trace.rows.Add(new Row { label = label, hidden = actor.Hidden, bits = Values(actor), callbacks = probe == null ? new List<int[]>() : new List<int[]>(probe.Callbacks) });
        [UnityTest] public IEnumerator GameKeepsStatusPhasesCallbacksAndPartialCleanup() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsStatusPhasesCallbacksAndPartialCleanup() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace(); var probe = new GameObject("ActorStatusProbe").AddComponent<ActorStatusProbe>(); probe.Configure(arena, new Vector2(0, 1000), new Vector2(1, 2), Vector2.up, false);
            var floor = new GameObject("StatusFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Action prepare = () =>
                    {
                        probe.OnCancel = null; driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); }
                        player.Position = new Vector2(3, 1000); player.velocity = Vector2.zero; player.SyncFacing(1); probe.isBoss = probe.isDecoy = false; probe.ResetActor(new Vector2(0, 1000)); probe.Callbacks.Clear(); Physics2D.SyncTransforms();
                    };
                    var control = new HitData { root = .6f, stun = .4f, push = 1, pushSeconds = .2f };
                    prepare(); Assert.That(probe.Receive(0, player, 1, true, control), Is.True); Assert.That(probe.Callbacks.Count, Is.EqualTo(2)); Assert.That(probe.Callbacks[0][3], Is.Zero); Assert.That(probe.Callbacks[1][3], Is.EqualTo(Bits(.6f))); Record(trace, "ordinary-control", probe, probe);
                    prepare(); int calls = 0; probe.OnCancel = () => { if (++calls == 1) probe.rootUntil = 9; else { probe.knockUntil = 8; probe.knockSpeed = -3; } };
                    probe.Receive(0, player, 1, true, control); Assert.That(probe.rootUntil, Is.EqualTo(9), "Later phases must read callback-edited current state."); Assert.That(probe.knockUntil, Is.EqualTo(.2f)); Assert.That(probe.knockSpeed, Is.EqualTo(5)); Record(trace, "reentrant-status-edit", probe, probe);
                    prepare(); probe.isBoss = true; probe.Receive(1, player, 1, true, control); Assert.That(probe.staggerUntil, Is.Zero); Assert.That(probe.knockUntil, Is.Zero); Assert.That(probe.rootUntil, Is.EqualTo(.6f)); Assert.That(probe.stunUntil, Is.EqualTo(.4f)); Record(trace, "boss", probe, probe);
                    prepare(); probe.isDecoy = true; probe.Receive(1, player, 1, true, control); Assert.That(probe.staggerUntil, Is.Zero); Assert.That(probe.knockUntil, Is.Zero); Record(trace, "decoy", probe, probe);
                    prepare(); probe.rootUntil = 4; probe.stunUntil = 3; probe.Receive(1, player, -1, false, control); Assert.That(probe.rootUntil, Is.EqualTo(4)); Assert.That(probe.stunUntil, Is.EqualTo(3)); Record(trace, "refresh", probe, probe);
                    prepare(); player.Receive(0, probe, 1, false, control); player.Receive(0, probe, -1, false, control); Assert.That(player.knockSpeed, Is.EqualTo(5)); Assert.That(player.rootUntil, Is.Zero); Assert.That(player.stunUntil, Is.Zero); Record(trace, "player-repeat", player);
                    prepare(); driver.Step(.02f, new Deviant.Combat.Contracts.CombatInput(0, Deviant.Combat.Contracts.CombatButtons.Dash)); for (int i = 0; i < 8; i++) driver.Step(.02f);
                    Assert.That(player.Dashing && !player.Invulnerable, Is.True); Assert.That(player.Receive(1, probe, -1, false, null), Is.True); Assert.That(player.knockUntil, Is.Zero); Assert.That(player.moveLockUntil, Is.GreaterThan(arena.Now)); Record(trace, "forced-hit", player);
                    prepare(); player.stunUntil = 4; player.rootUntil = 3; player.staggerUntil = 2; player.moveLockUntil = 5; player.knockUntil = 6; player.knockSpeed = -7; player.stealthUntil = 7; player.smokeUntil = 8;
                    player.Fall(); Assert.That(player.moveLockUntil, Is.Zero); Assert.That(player.knockUntil, Is.Zero); Assert.That(player.rootUntil, Is.EqualTo(3)); Assert.That(player.stunUntil, Is.EqualTo(4)); Assert.That(player.knockSpeed, Is.EqualTo(-7)); Assert.That(player.Hidden, Is.True); Record(trace, "fall-partial-clear", player);
                    prepare(); player.stealthUntil = 7; player.smokeUntil = 8; player.rootUntil = 3; Assert.That(player.Hidden, Is.True); Record(trace, "hidden", player); Assert.That(arena.EnemyTarget(arena.enemies[0]), Is.Null); arena.BreakStealth(player); Assert.That(player.Hidden, Is.False); Assert.That(player.rootUntil, Is.EqualTo(3)); Record(trace, "break-hidden-only", player);
                    prepare(); probe.stunUntil = probe.rootUntil = probe.staggerUntil = probe.moveLockUntil = probe.knockUntil = probe.stealthUntil = probe.smokeUntil = 3; probe.knockSpeed = -7; probe.ResetActor(new Vector2(0, 1000)); Assert.That(probe.knockSpeed, Is.EqualTo(-7)); Assert.That(probe.stunUntil + probe.rootUntil + probe.staggerUntil + probe.moveLockUntil + probe.knockUntil + probe.stealthUntil + probe.smokeUntil, Is.Zero); Record(trace, "reset", probe, probe);
                    prepare(); Assert.That(probe.Receive(0, player, 1, true, null), Is.True);
                    Assert.That(probe.staggerUntil, Is.EqualTo(.3f)); Assert.That(probe.knockUntil, Is.EqualTo(.1f)); Assert.That(probe.knockSpeed, Is.EqualTo(2.5f));
                    Assert.That(probe.Callbacks[0][4], Is.EqualTo(Bits(.3f))); Assert.That(probe.Callbacks[0][6], Is.Zero, "Basic cancellation observes committed stagger before push.");
                    driver.Step(.05f); Assert.That(probe.Receive(0, player, -1, true, null), Is.True);
                    Assert.That(probe.staggerUntil, Is.EqualTo(arena.Now + .3f)); Assert.That(probe.knockUntil, Is.EqualTo(arena.Now + .1f)); Assert.That(probe.knockSpeed, Is.EqualTo(-2.5f));
                    Record(trace, "ordinary-basic-repeat-refreshes-direction-and-window", probe, probe);
                    prepare(); probe.staggerUntil = 4; probe.Receive(0, player, 1, true, null);
                    Assert.That(probe.staggerUntil, Is.EqualTo(4), "A generic basic hit preserves longer existing stagger."); Record(trace, "basic-stagger-keeps-longer-lock", probe, probe);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                Assert.That(trace.rows.Count, Is.EqualTo(13)); var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-actorStatusTraceRoot");
                if (index >= 0 && index + 1 < args.Length) { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { probe.OnCancel = null; Object.Destroy(probe.gameObject); Object.Destroy(floor); }
        }
    }
}
