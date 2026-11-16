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
    public class PlayerProjectileExecutionIntegrationTests
    {
        [UnityTest] public IEnumerator GameResetClosesRetainedProjectileExecution()
            => RetainedCleanup("Assets/Deviant/Game/Scenes/deviant.unity", false);
        [UnityTest] public IEnumerator LabResetClosesRetainedProjectileExecution()
            => RetainedCleanup("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", false);
        [UnityTest] public IEnumerator GameDeathClosesRetainedProjectileExecution()
            => RetainedCleanup("Assets/Deviant/Game/Scenes/deviant.unity", true);
        [UnityTest] public IEnumerator LabDeathClosesRetainedProjectileExecution()
            => RetainedCleanup("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", true);
        [UnityTest] public IEnumerator GameLateContactsKeepNativePerActorIntervals()
            => NativeContacts("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabLateContactsKeepNativePerActorIntervals()
            => NativeContacts("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void CenterAt(CombatActor actor, Vector2 center)
            => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int special)
        {
            driver.Reset();
            foreach (var enemy in arena.enemies)
            {
                enemy.aggressive = false; enemy.ResetActor(new Vector2(70, 1000)); enemy.health = 1000;
            }
            var player = arena.Player; player.InputBlocked = false; player.UseTestInput = true; player.selected = 0;
            player.Position = new Vector2(-20, 1000); player.velocity = Vector2.zero; player.axis = 0;
            int form = Array.FindIndex(arena.catalog.forms, item => item.id == "shuriken");
            Assert.That(player.TryReconfigure(0, form, special, out string error), Is.True, error);
            Physics2D.SyncTransforms();
        }
        [Serializable] sealed class Row
        {
            public string phase;
            public int scenario, active, emitted, bursts, hits;
            public int[] actors, shots;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Record(Trace trace, string phase, int scenario, CombatArena arena, List<PlayerProjectile> emitted)
        {
            var shots = new List<int>();
            foreach (var shot in emitted)
            {
                shots.Add(arena.playerShots.Contains(shot) ? 1 : 0);
                shots.Add(Bits(shot.Position.x)); shots.Add(Bits(shot.Position.y));
                shots.Add(Bits(shot.velocity.x)); shots.Add(Bits(shot.velocity.y));
                shots.Add(Bits(shot.traveled)); shots.Add(Bits(shot.damage));
                shots.Add(Bits(shot.transform.eulerAngles.z));
            }
            var actors = new List<int>();
            foreach (var actor in arena.enemies)
            {
                actors.Add(Bits(actor.Position.x)); actors.Add(Bits(actor.Position.y));
                actors.Add(Bits(actor.health)); actors.Add(actor.ReceivedHitCount);
                actors.Add(Bits(actor.staggerUntil)); actors.Add(Bits(actor.knockUntil));
            }
            trace.rows.Add(new Row { phase = phase, scenario = scenario, active = arena.playerShots.Count,
                emitted = arena.PlayerShotCount, bursts = arena.bombBursts.Count, hits = arena.enemies[0].ReceivedHitCount,
                shots = shots.ToArray(), actors = actors.ToArray() });
        }
        static GameObject Duplicate(CombatActor actor)
        {
            var duplicate = new GameObject("PlayerProjectileExecutionDuplicate"); duplicate.transform.SetParent(actor.transform, false);
            var collider = duplicate.AddComponent<BoxCollider2D>(); collider.size = actor.hurtCollider.size;
            collider.offset = actor.hurtCollider.offset; collider.isTrigger = true;
            duplicate.AddComponent<HurtRegion>().actor = actor; return duplicate;
        }
        // Like the existing broad ranged trace, this runs unchanged with the
        // exact 3014ab8 PlayerProjectile consumer. No timing model is substituted.
        static IEnumerator NativeContacts(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            using (var driver = arena.AcquireManualSimulation())
            {
                for (int scenario = 0; scenario < 2; scenario++)
                {
                    Setup(arena, driver, 3); var target = arena.enemies[0]; var emitted = new List<PlayerProjectile>();
                    var source = arena.Player.Weapon; var data = arena.Player.Special.ranged;
                    var origin = new Vector2(0, 1004); var duplicate = Duplicate(target);
                    PlayerProjectile Spawn()
                    {
                        var shot = PlayerProjectile.Spawn(arena, arena.Player, origin, Vector2.right * 3, data, 5);
                        shot.sourceWeapon = source; emitted.Add(shot); return shot;
                    }
                    void Observe(string phase) => Record(trace, phase, scenario, arena, emitted);
                    void Step(PlayerProjectile shot, float dt, string phase) { shot.Step(dt); Observe(phase); }
                    try
                    {
                        var first = Spawn(); Observe("spawn");
                        if (scenario == 0)
                        {
                            // Put the target's left edge at x=4. Contact begins
                            // during a cast, not in the spawn/endpoint overlap.
                            CenterAt(target, new Vector2(4 + target.hurtCollider.size.x * .5f, 1004)); Physics2D.SyncTransforms();
                            first.SweepTo(origin); Observe("empty-spawn-overlap"); Assert.That(target.ReceivedHitCount, Is.Zero);
                            Step(first, .8f, "before-entry"); Assert.That(target.ReceivedHitCount, Is.Zero);
                            Step(first, .04f, "cast-entry"); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                            first.SweepTo(first.Position); Observe("duplicate-contact"); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                            Step(first, .24f, "before-contact-interval"); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                            Step(first, .01f, "contact-interval"); Assert.That(target.ReceivedHitCount, Is.EqualTo(2));
                            target.Position = new Vector2(70, 1000); Physics2D.SyncTransforms(); Step(first, .61f, "absent-past-deadline");
                            CenterAt(target, first.Position); Physics2D.SyncTransforms(); first.SweepTo(first.Position); Observe("overdue-reentry");
                            Assert.That(target.ReceivedHitCount, Is.EqualTo(3), "Reentry gives one hit, never a catch-up burst.");
                            Step(first, .24f, "before-reentry-interval"); Assert.That(target.ReceivedHitCount, Is.EqualTo(3));
                            Step(first, .01f, "reentry-interval"); Assert.That(target.ReceivedHitCount, Is.EqualTo(4));
                            Step(first, .1f, "range-end"); Assert.That(target.ReceivedHitCount, Is.EqualTo(4));
                            Assert.That(arena.playerShots, Is.Empty); Assert.That(first.traveled, Is.EqualTo(6).Within(.0001f));
                        }
                        else
                        {
                            Step(first, .8f, "older-flight"); var second = Spawn();
                            CenterAt(target, new Vector2(1.2f, 1004)); Physics2D.SyncTransforms();
                            first.SweepTo(first.Position); second.SweepTo(second.Position); Observe("independent-first-contacts");
                            Assert.That(target.ReceivedHitCount, Is.EqualTo(2));
                            Step(first, .25f, "older-interval"); Assert.That(target.ReceivedHitCount, Is.EqualTo(3));
                            Step(second, .24f, "younger-before-interval"); Assert.That(target.ReceivedHitCount, Is.EqualTo(3));
                            Step(second, .01f, "younger-interval"); Assert.That(target.ReceivedHitCount, Is.EqualTo(4));
                            Step(first, 2, "older-range-end"); Assert.That(arena.playerShots.Contains(first), Is.False);
                            Assert.That(arena.playerShots.Contains(second), Is.True); Assert.That(second.traveled, Is.EqualTo(.75f).Within(.0001f));
                            Step(second, 2, "younger-range-end"); Assert.That(arena.playerShots, Is.Empty);
                        }
                        Assert.That(arena.SimulationFaulted, Is.False); Observe("complete");
                    }
                    finally { Object.DestroyImmediate(duplicate); }
                }
            }
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-playerProjectileExecutionTraceRoot");
            if (at >= 0 && at + 1 < args.Length)
            {
                Directory.CreateDirectory(args[at + 1]);
                File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace));
            }
        }
        // Kept separate from the unchanged 15-scenario bit trace. This fixture
        // intentionally fails with 3014ab8's deferred-Destroy cleanup, and uses
        // only its public APIs so the parent can reproduce it in a baseline clone.
        static IEnumerator RetainedCleanup(string scene, bool death)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            {
                for (int kind = 0; kind < 4; kind++)
                {
                    int special = kind == 1 ? 3 : kind == 2 ? 1 : kind == 3 ? 2 : 0;
                    Setup(arena, driver, special); var source = player.Weapon;
                    var data = kind == 0 ? player.Form.basics[0].ranged : player.Special.ranged;
                    var shot = PlayerProjectile.Spawn(arena, player, new Vector2(0, 1004), Vector2.right * data.speed, data, 5);
                    shot.sourceWeapon = source; shot.Step(.01f);
                    Vector2 position = shot.Position, velocity = shot.velocity;
                    float traveled = shot.traveled, rotation = shot.transform.eulerAngles.z;
                    if (!death) arena.ResetTest(); else { player.health = 0; player.Die(); }
                    Assert.That(arena.playerShots, Is.Empty);
                    var target = arena.enemies[0]; target.aggressive = false; target.health = 1000;
                    CenterAt(target, position + Vector2.right * .5f); Physics2D.SyncTransforms();
                    float health = target.health; int hits = target.ReceivedHitCount, bursts = arena.bombBursts.Count;
                    Vector2 ownerPosition = player.Position;
                    // No yield: these all precede Unity's deferred destruction.
                    shot.SweepTo(position + Vector2.right * 2); shot.Step(10); shot.Detonate(); shot.SweepTo(position);
                    string label = "cleanup=" + (death ? "death" : "reset") + " kind=" + kind;
                    Assert.That(shot.Position, Is.EqualTo(position), label);
                    Assert.That(shot.velocity, Is.EqualTo(velocity), label);
                    Assert.That(shot.traveled, Is.EqualTo(traveled), label);
                    Assert.That(shot.transform.eulerAngles.z, Is.EqualTo(rotation), label);
                    Assert.That(target.health, Is.EqualTo(health), label);
                    Assert.That(target.ReceivedHitCount, Is.EqualTo(hits), label);
                    Assert.That(arena.bombBursts.Count, Is.EqualTo(bursts), label);
                    Assert.That(player.Position, Is.EqualTo(ownerPosition), label);
                    Assert.That(player.ReturnReservations, Is.Empty, label);
                    Assert.That(arena.playerShots, Is.Empty, label);
                    Assert.That(player.Dead, Is.EqualTo(death), label);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
        }
    }
}
