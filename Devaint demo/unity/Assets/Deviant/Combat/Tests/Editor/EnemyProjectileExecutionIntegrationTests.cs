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
    public class EnemyProjectileExecutionIntegrationTests
    {
        [UnityTest] public IEnumerator GameKeepsNativeEnemyProjectileContactsAndLifetime()
            => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabKeepsNativeEnemyProjectileContactsAndLifetime()
            => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTest] public IEnumerator GameEqualDistanceCandidatesAbsorbOneShotWithoutSpecifyingANativeTieWinner()
            => EqualDistanceContacts("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabEqualDistanceCandidatesAbsorbOneShotWithoutSpecifyingANativeTieWinner()
            => EqualDistanceContacts("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Serializable] sealed class Row
        {
            public string phase;
            public int scenario;
            public int[] clock, actors, shots, resources;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void CenterAt(CombatActor actor, Vector2 center)
            => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        static GameObject Surface(Vector2 position, bool oneWay)
        {
            var surface = new GameObject("EnemyProjectileFixtureSurface"); surface.transform.position = position;
            surface.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 8);
            surface.AddComponent<ArenaSurface>().oneWay = oneWay; return surface;
        }
        static GameObject Duplicate(CombatActor actor)
        {
            var duplicate = new GameObject("EnemyProjectileFixtureDuplicate"); duplicate.transform.SetParent(actor.transform, false);
            var box = duplicate.AddComponent<BoxCollider2D>(); box.size = actor.hurtCollider.size;
            box.offset = actor.hurtCollider.offset; box.isTrigger = true;
            duplicate.AddComponent<HurtRegion>().actor = actor; return duplicate;
        }
        static CombatActor FixtureActor(CombatArena arena, Vector2 center, bool player, bool decoy)
        {
            var actor = new GameObject("EnemyProjectileFixtureActor").AddComponent<CombatActor>();
            actor.Configure(arena, Vector2.zero, new Vector2(.8f, 1.5f), new Vector2(0, .75f), player);
            actor.isDecoy = decoy; CenterAt(actor, center); return actor;
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset();
            foreach (var enemy in arena.enemies)
            {
                enemy.aggressive = false; enemy.ResetActor(new Vector2(-35, 1000));
                enemy.health = 1000; enemy.velocity = Vector2.zero;
            }
            var player = arena.Player; player.InputBlocked = false; player.UseTestInput = true; player.selected = 0;
            player.Position = new Vector2(70, 1000); player.velocity = Vector2.zero;
            player.SyncFacing(1); player.grounded = true; player.axis = 0;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            int form = Array.FindIndex(arena.catalog.forms, item => item.id == "shield");
            Assert.That(player.TryReconfigure(0, form, 0, out string error), Is.True, error);
            Physics2D.SyncTransforms();
        }
        static void Record(Trace trace, string phase, int scenario, CombatArena arena, WeaponState weapon,
            List<CombatActor> extras, List<ArenaProjectile> emitted)
        {
            var actors = new List<int>();
            void Actor(CombatActor actor)
            {
                actors.Add(Bits(actor.Position.x)); actors.Add(Bits(actor.Position.y));
                actors.Add(Bits(actor.velocity.x)); actors.Add(Bits(actor.velocity.y));
                actors.Add(Bits(actor.health)); actors.Add(Bits(actor.shield)); actors.Add(actor.ReceivedHitCount);
                actors.Add(Bits(actor.moveLockUntil)); actors.Add(Bits(actor.knockUntil)); actors.Add(Bits(actor.knockSpeed));
                actors.Add(Bits(actor.staggerUntil)); actors.Add(Bits(actor.stunUntil)); actors.Add(actor.Dead ? 1 : 0);
            }
            Actor(arena.Player); foreach (var actor in arena.enemies) Actor(actor);
            foreach (var actor in extras) if (actor) Actor(actor);
            var shots = new List<int>();
            foreach (var shot in emitted)
            {
                shots.Add(arena.projectiles.Contains(shot) ? 1 : 0);
                shots.Add(Bits(shot.transform.position.x)); shots.Add(Bits(shot.transform.position.y));
                shots.Add(Bits(shot.velocity.x)); shots.Add(Bits(shot.velocity.y));
                shots.Add(shot.source == arena.Player ? 1 : shot.source == arena.enemies[0] ? 2 : shot.source ? 3 : 0);
            }
            trace.rows.Add(new Row { phase = phase, scenario = scenario, actors = actors.ToArray(), shots = shots.ToArray(),
                clock = new[] { Bits(arena.Now), arena.projectiles.Count, arena.Player.Dashing ? 1 : 0, arena.Player.currentCast ? 1 : 0 },
                resources = new[] { arena.Player.selected, weapon.Cooldown.Available, Bits(weapon.Cooldown.NextReady),
                    Bits(weapon.Cooldown.Period), Bits(weapon.Cooldown.Debt(arena.Now)), Bits(weapon.SpecialBonus) } });
        }

        // Uses only pre-extraction runtime APIs. The same fixture can execute
        // against the exact old ArenaProjectile; damage/reflection are observed
        // through actual Receive, source, velocity and resource/state effects.
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace();
            var floor = new GameObject("EnemyProjectileFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(300, 1); floor.AddComponent<ArenaSurface>();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    for (int scenario = 0; scenario < 18; scenario++)
                    {
                        Setup(arena, driver); var enemy = arena.enemies[0]; var weapon = player.Weapon;
                        var geometry = new List<GameObject>(); var extras = new List<CombatActor>(); var emitted = new List<ArenaProjectile>();
                        var origin = new Vector2(0, 1004);
                        ArenaProjectile Spawn(CombatActor source, Vector2 at, Vector2 velocity)
                        {
                            arena.FireEnemyProjectile(source, at, velocity, 8);
                            var shot = arena.projectiles[arena.projectiles.Count - 1]; emitted.Add(shot); return shot;
                        }
                        CombatActor Extra(Vector2 center, bool isPlayer = false, bool isDecoy = true)
                        {
                            var actor = FixtureActor(arena, center, isPlayer, isDecoy); extras.Add(actor); geometry.Add(actor.gameObject); return actor;
                        }
                        void Observe(string phase) => Record(trace, phase, scenario, arena, weapon, extras, emitted);
                        void Step(ArenaProjectile shot, float dt, string phase)
                        { Physics2D.SyncTransforms(); shot.Step(dt); Observe(phase); }
                        try
                        {
                            Observe("setup");
                            switch (scenario)
                            {
                                case 0:
                                {
                                    var shot = Spawn(enemy, origin, Vector2.right * 8); Observe("spawn");
                                    driver.Step(3); Observe("long-tick-before-entry-expiry");
                                    Assert.That(arena.projectiles.Contains(shot), Is.True);
                                    Assert.That((Vector2)shot.transform.position, Is.EqualTo(origin + Vector2.right * 24));
                                    Step(shot, 0, "exact-entry-expiry"); Assert.That(arena.projectiles, Is.Empty);
                                    Assert.That((Vector2)shot.transform.position, Is.EqualTo(origin + Vector2.right * 24)); break;
                                }
                                case 1:
                                {
                                    var shot = Spawn(enemy, origin, Vector2.right * 8); shot.source = null;
                                    Step(shot, .2f, "missing-source"); Assert.That(arena.projectiles, Is.Empty);
                                    Assert.That((Vector2)shot.transform.position, Is.EqualTo(origin)); break;
                                }
                                case 2:
                                {
                                    var shot = Spawn(enemy, origin, Vector2.right * 8); enemy.health = 0;
                                    Step(shot, .125f, "dead-nonplayer-flight"); Assert.That(arena.projectiles.Contains(shot), Is.True);
                                    Assert.That((Vector2)shot.transform.position, Is.EqualTo(origin + Vector2.right));
                                    CenterAt(player, origin + Vector2.right * 2); Step(shot, .25f, "dead-nonplayer-hit");
                                    Assert.That(player.health, Is.EqualTo(92)); Assert.That(player.ReceivedHitCount, Is.EqualTo(1));
                                    Assert.That(arena.projectiles, Is.Empty); break;
                                }
                                case 3:
                                {
                                    var shot = Spawn(player, origin, Vector2.right * 8); player.health = 0;
                                    Step(shot, .2f, "dead-player-source"); Assert.That(arena.projectiles, Is.Empty);
                                    Assert.That((Vector2)shot.transform.position, Is.EqualTo(origin)); break;
                                }
                                case 4:
                                {
                                    CenterAt(enemy, origin); CenterAt(arena.enemies[1], origin + Vector2.right);
                                    var dead = Extra(origin + Vector2.right * 2); dead.health = 0;
                                    var decoy = Extra(origin + Vector2.right * 3); geometry.Add(Duplicate(decoy));
                                    CenterAt(player, origin + Vector2.right * 5);
                                    var shot = Spawn(enemy, origin, Vector2.right * 8); Step(shot, 1, "filter-and-decoy-absorption");
                                    Assert.That(enemy.health, Is.EqualTo(1000)); Assert.That(arena.enemies[1].health, Is.EqualTo(1000));
                                    Assert.That(dead.ReceivedHitCount, Is.Zero); Assert.That(decoy.health, Is.EqualTo(92));
                                    Assert.That(decoy.ReceivedHitCount, Is.EqualTo(1)); Assert.That(player.health, Is.EqualTo(100)); break;
                                }
                                case 5:
                                {
                                    CenterAt(player, origin + Vector2.right); geometry.Add(Duplicate(player)); geometry.Add(Duplicate(player));
                                    var first = Spawn(enemy, origin, Vector2.right * 8); var second = Spawn(enemy, origin, Vector2.right * 8);
                                    Step(first, .5f, "duplicate-regions-first-shot"); Assert.That(player.ReceivedHitCount, Is.EqualTo(1));
                                    Step(second, .5f, "independent-second-shot"); Assert.That(player.ReceivedHitCount, Is.EqualTo(2));
                                    Assert.That(player.health, Is.EqualTo(84)); Assert.That(arena.projectiles, Is.Empty); break;
                                }
                                case 6:
                                case 7:
                                {
                                    geometry.Add(Surface(origin + Vector2.right * 2, scenario == 7)); CenterAt(player, origin + Vector2.right * 4);
                                    var shot = Spawn(enemy, origin, Vector2.right * 8); Step(shot, 1, scenario == 6 ? "solid-wall" : "one-way-pass");
                                    Assert.That(arena.projectiles, Is.Empty);
                                    Assert.That(player.health, Is.EqualTo(scenario == 6 ? 100 : 92));
                                    if (scenario == 6) Assert.That(shot.transform.position.x, Is.InRange(1.7f, 1.95f)); break;
                                }
                                case 8:
                                {
                                    player.Position = new Vector2(0, 1000); player.SyncFacing(1);
                                    weapon.StatSlots[0] = 2; Physics2D.SyncTransforms(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
                                    Assert.That(player.currentCast, Is.Not.Null); Assert.That(weapon.Cooldown.Available, Is.Zero);
                                    float ready = weapon.Cooldown.NextReady;
                                    weapon.StatSlots[1] = 2; weapon.StatSlots[2] = 2;
                                    var shot = Spawn(enemy, new Vector2(.9f, 1001), Vector2.left * 8);
                                    Step(shot, .02f, "front-parry-live-bonus");
                                    Assert.That(shot.source, Is.SameAs(player)); Assert.That(shot.velocity, Is.EqualTo(Vector2.right * 8));
                                    Assert.That((Vector2)shot.transform.position, Is.EqualTo(new Vector2(.9f, 1001)));
                                    Assert.That(player.health, Is.EqualTo(100)); Assert.That(player.ReceivedHitCount, Is.Zero);
                                    var secondPlayer = Extra(new Vector2(2, 1001), true, false); secondPlayer.currentCast = player.currentCast;
                                    var decoy = Extra(new Vector2(2.5f, 1001));
                                    CenterAt(enemy, new Vector2(4, 1001)); geometry.Add(Duplicate(enemy));
                                    weapon.StatSlots.Clear(); player.CancelAction(); player.selected = 1;
                                    Step(shot, 1, "reflected-filter-and-frozen-damage");
                                    Assert.That(secondPlayer.health, Is.EqualTo(100)); Assert.That(decoy.health, Is.EqualTo(100));
                                    Assert.That(enemy.health, Is.EqualTo(987.2f)); Assert.That(enemy.ReceivedHitCount, Is.EqualTo(1));
                                    Assert.That(weapon.Cooldown.NextReady, Is.EqualTo(ready)); Assert.That(arena.projectiles, Is.Empty); break;
                                }
                                case 9:
                                case 10:
                                case 11:
                                {
                                    player.Position = new Vector2(0, 1000); player.SyncFacing(1); Physics2D.SyncTransforms();
                                    float dt = scenario == 10 ? .15f : scenario == 11 ? .149f : .02f;
                                    driver.Step(dt, new CombatInput(0, CombatButtons.Special));
                                    bool rear = scenario == 9; var at = new Vector2(rear ? -.9f : .9f, 1001);
                                    var shot = Spawn(enemy, at, (rear ? Vector2.right : Vector2.left) * 8);
                                    Step(shot, .1f, rear ? "rear-parry-rejected" : scenario == 10 ? "closed-parry-window" : "inside-parry-window");
                                    bool parried = scenario == 11;
                                    Assert.That(shot.source == player, Is.EqualTo(parried)); Assert.That(player.health, Is.EqualTo(parried ? 100 : 92));
                                    Assert.That(player.ReceivedHitCount, Is.EqualTo(parried ? 0 : 1));
                                    Assert.That(arena.projectiles.Contains(shot), Is.EqualTo(parried));
                                    Assert.That(weapon.Cooldown.Available, Is.Zero); break;
                                }
                                case 12:
                                {
                                    player.Position = new Vector2(0, 1000); Physics2D.SyncTransforms();
                                    driver.Step(.02f, new CombatInput(0, CombatButtons.Dash)); Assert.That(player.Invulnerable, Is.True);
                                    var at = player.Center + Vector2.right * 2; var shot = Spawn(enemy, at, Vector2.left * 8);
                                    Step(shot, .5f, "invulnerable-receive-rejection"); Assert.That(player.health, Is.EqualTo(100));
                                    Assert.That(player.ReceivedHitCount, Is.Zero); Assert.That(arena.projectiles, Is.Empty); break;
                                }
                                case 13:
                                {
                                    CenterAt(player, origin + Vector2.right); player.guardUntil = arena.Now + 1; player.guardReduction = .5f; player.shield = 3;
                                    var shot = Spawn(enemy, origin, Vector2.right * 8); Step(shot, .5f, "guard-shield-and-hit-state");
                                    Assert.That(player.health, Is.EqualTo(99)); Assert.That(player.shield, Is.Zero);
                                    Assert.That(player.ReceivedHitCount, Is.EqualTo(1)); Assert.That(player.moveLockUntil, Is.EqualTo(arena.Now + .12f));
                                    Assert.That(player.knockUntil, Is.EqualTo(arena.Now + .1f)); Assert.That(player.knockSpeed, Is.EqualTo(5)); break;
                                }
                                case 14:
                                {
                                    var first = Extra(origin + Vector2.right); var second = Extra(origin + Vector2.right * 1.1f); geometry.Add(Duplicate(first));
                                    var shot = Spawn(enemy, origin, Vector2.right * 8); Step(shot, .5f, "native-nearest-distance-candidates");
                                    Assert.That(first.ReceivedHitCount, Is.EqualTo(1)); Assert.That(second.ReceivedHitCount, Is.Zero);
                                    Assert.That(first.health, Is.EqualTo(92)); Assert.That(second.health, Is.EqualTo(100)); Assert.That(arena.projectiles, Is.Empty); break;
                                }
                                case 15:
                                {
                                    CenterAt(player, origin); geometry.Add(Duplicate(player));
                                    var shot = Spawn(enemy, origin, Vector2.zero); Step(shot, 0, "stationary-zero-duration-overlap");
                                    Assert.That(player.health, Is.EqualTo(92)); Assert.That(player.ReceivedHitCount, Is.EqualTo(1));
                                    Assert.That(arena.projectiles, Is.Empty); break;
                                }
                                case 16:
                                {
                                    CenterAt(player, origin - Vector2.right); var shot = Spawn(enemy, origin, Vector2.left * 8);
                                    Step(shot, .5f, "leftward-hit-state"); Assert.That(player.health, Is.EqualTo(92));
                                    Assert.That(player.knockSpeed, Is.EqualTo(-5)); Assert.That(player.knockUntil, Is.EqualTo(arena.Now + .1f)); break;
                                }
                                case 17:
                                {
                                    var source = Extra(origin + Vector2.left * 3, false, false);
                                    var shot = Spawn(source, origin, Vector2.right * 8); Object.DestroyImmediate(source.gameObject);
                                    Step(shot, .5f, "destroyed-native-source"); Assert.That(arena.projectiles, Is.Empty);
                                    Assert.That((Vector2)shot.transform.position, Is.EqualTo(origin)); break;
                                }
                            }
                            Assert.That(arena.SimulationFaulted, Is.False); Observe("complete");
                        }
                        finally { for (int i = geometry.Count - 1; i >= 0; i--) if (geometry[i]) Object.DestroyImmediate(geometry[i]); }
                    }
                }
                var args = Environment.GetCommandLineArgs(); int traceArgument = Array.IndexOf(args, "-enemyProjectileExecutionTraceRoot");
                if (traceArgument >= 0 && traceArgument + 1 < args.Length)
                {
                    Directory.CreateDirectory(args[traceArgument + 1]);
                    File.WriteAllText(Path.Combine(args[traceArgument + 1], label + ".json"), JsonUtility.ToJson(trace));
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
        // Equal-distance native query order can vary between Editor instances.
        // This behavior assertion intentionally does not enter the bit trace.
        static IEnumerator EqualDistanceContacts(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation())
            {
                Setup(arena, driver); var origin = new Vector2(0, 1004);
                var first = FixtureActor(arena, origin + Vector2.right, false, true);
                var second = FixtureActor(arena, origin + Vector2.right, false, true);
                var duplicate = Duplicate(first);
                try
                {
                    arena.FireEnemyProjectile(arena.enemies[0], origin, Vector2.right * 8, 8);
                    var shot = arena.projectiles[0]; Physics2D.SyncTransforms(); shot.Step(.5f);
                    Assert.That(first.ReceivedHitCount + second.ReceivedHitCount, Is.EqualTo(1));
                    Assert.That(first.health + second.health, Is.EqualTo(192)); Assert.That(arena.projectiles, Is.Empty);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { Object.DestroyImmediate(duplicate); Object.DestroyImmediate(first.gameObject); Object.DestroyImmediate(second.gameObject); }
            }
        }
    }
}
