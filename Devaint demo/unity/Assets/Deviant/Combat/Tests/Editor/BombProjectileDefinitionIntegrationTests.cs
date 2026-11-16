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
    public class BombProjectileDefinitionIntegrationTests
    {
        [Serializable] sealed class Row
        {
            public string phase;
            public int scenario, tick, explosions, active, selected, charges;
            public bool dead;
            public int[] bits, actors, bombs, bursts;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static int Form(CombatArena arena, string id) => Array.FindIndex(arena.catalog.forms, item => item.id == id);
        static BombData Copy(BombData source) => JsonUtility.FromJson<BombData>(JsonUtility.ToJson(source));
        static void CenterAt(CombatActor actor, Vector2 center) => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        static GameObject Surface(string name, Vector2 position, Vector2 size, bool oneWay = false)
        {
            var result = new GameObject(name); result.transform.position = position;
            result.AddComponent<BoxCollider2D>().size = size; result.AddComponent<ArenaSurface>().oneWay = oneWay;
            return result;
        }
        static GameObject Duplicate(CombatActor actor)
        {
            var result = new GameObject("BombDefinitionDuplicate"); result.transform.SetParent(actor.transform, false);
            var box = result.AddComponent<BoxCollider2D>(); box.size = actor.size; box.offset = actor.hurtCollider.offset; box.isTrigger = true;
            result.AddComponent<HurtRegion>().actor = actor; return result;
        }
        static void ParkActors(CombatArena arena)
        {
            var player = arena.Player; player.Position = new Vector2(-20, 1000); player.velocity = Vector2.zero;
            player.SyncFacing(1); player.grounded = true; player.axis = 0;
            for (int i = 0; i < arena.enemies.Count; i++)
            {
                var enemy = arena.enemies[i]; enemy.aggressive = false; enemy.ResetActor(new Vector2(70 + i * 10, 1000));
                enemy.health = 1000; enemy.SyncFacing(1);
            }
            Physics2D.SyncTransforms();
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int special, string form = "bomb")
        {
            driver.Reset(); var player = arena.Player; player.InputBlocked = false; player.UseTestInput = true; player.selected = 0;
            ParkActors(arena);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            Assert.That(player.TryReconfigure(0, Form(arena, form), special, out string error), Is.True, error);
        }
        static void Record(Trace trace, string phase, int scenario, int tick, CombatArena arena, WeaponState source, List<BombProjectile> emitted)
        {
            var bombs = new List<int>();
            foreach (var bomb in emitted)
            {
                bombs.Add(arena.bombs.Contains(bomb) ? 1 : 0); bombs.Add(bomb.TimedLanded ? 1 : 0);
                bombs.Add(Bits(bomb.Position.x)); bombs.Add(Bits(bomb.Position.y));
                bombs.Add(Bits(bomb.velocity.x)); bombs.Add(Bits(bomb.velocity.y));
                bombs.Add(Bits(bomb.TimedFuseRemaining)); bombs.Add(Bits(bomb.damage)); bombs.Add(Bits(bomb.fragmentMultiplier));
            }
            var actors = new List<int>();
            foreach (var actor in arena.enemies)
            {
                actors.Add(Bits(actor.Position.x)); actors.Add(Bits(actor.Position.y));
                actors.Add(Bits(actor.health)); actors.Add(actor.ReceivedHitCount);
                actors.Add(Bits(actor.staggerUntil)); actors.Add(Bits(actor.knockUntil));
            }
            var bursts = new List<int>();
            foreach (var burst in arena.bombBursts)
            {
                bursts.Add(Bits(burst.transform.position.x)); bursts.Add(Bits(burst.transform.position.y));
                bursts.Add(Bits(burst.radius)); bursts.Add(Bits(burst.started));
                bursts.Add(Bits(burst.transform.localScale.x)); bursts.Add(Bits(burst.visual.color.a));
            }
            var player = arena.Player;
            trace.rows.Add(new Row { phase = phase, scenario = scenario, tick = tick, explosions = arena.BombExplosionCount,
                active = arena.bombs.Count, selected = player.selected, charges = source.Cooldown.Available, dead = player.Dead,
                bombs = bombs.ToArray(), actors = actors.ToArray(), bursts = bursts.ToArray(),
                bits = new[] { Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y),
                    Bits(player.health), Bits(source.Cooldown.NextReady), Bits(source.Cooldown.Period), Bits(source.Cooldown.Debt(arena.Now)),
                    Bits(arena.LastBombExplosion.x), Bits(arena.LastBombExplosion.y) } });
        }
        [UnityTest] public IEnumerator GameUneditedBombsKeepNativeContactsFragmentsAndLifetime()
            => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabUneditedBombsKeepNativeContactsFragmentsAndLifetime()
            => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var floor = Surface("BombDefinitionFloor", new Vector2(0, 999.5f), new Vector2(300, 1));
            bool oldBoxes = arena.showBoxes;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    arena.showBoxes = false; var player = arena.Player; var trace = new Trace();
                    for (int scenario = 0; scenario < 20; scenario++)
                    {
                        Setup(arena, driver, scenario == 5 || scenario == 6 || scenario == 7 || scenario == 16 ? 3 : scenario < 8 ? 0 : 3,
                            scenario == 5 || scenario == 6 || scenario == 7 || scenario == 16 ? "launcher" : "bomb");
                        var source = player.Weapon; var emitted = new List<BombProjectile>(); var geometry = new List<GameObject>(); int tick = 0;
                        var first = arena.enemies[0]; var second = arena.enemies[1]; var third = arena.enemies[2];
                        var data = scenario == 5 || scenario == 6 || scenario == 7 || scenario == 16 ? player.Form.specials[3].bomb : scenario < 8 ? player.Form.basics[0].bomb : player.Special.bomb;
                        void Observe(string phase)
                        {
                            foreach (var bomb in arena.bombs) if (!emitted.Contains(bomb)) emitted.Add(bomb);
                            Record(trace, phase, scenario, tick, arena, source, emitted);
                        }
                        void Step(float dt, CombatButtons buttons = CombatButtons.None)
                        { driver.Step(dt, new CombatInput(0, buttons)); Observe("tick"); tick++; }
                        void Ticks(int count) { for (int i = 0; i < count; i++) Step(.02f); }
                        BombProjectile Spawn(Vector2 position, Vector2 velocity, float damage, BombData settings = null)
                        {
                            var bomb = BombProjectile.Spawn(arena, player, position, velocity, settings ?? data, damage);
                            emitted.Add(bomb); return bomb;
                        }
                        BombProjectile Cast(bool basic = false)
                        {
                            player.Position = new Vector2(0, 1000); Physics2D.SyncTransforms();
                            Step(.02f, basic ? CombatButtons.Basic : CombatButtons.Special);
                            for (int i = 0; i < 40 && arena.bombs.Count == 0; i++) Step(.02f);
                            Assert.That(arena.bombs.Count, Is.EqualTo(1)); return arena.bombs[0];
                        }
                        try
                        {
                            Observe("start");
                            switch (scenario)
                            {
                                case 0:
                                    var center = new Vector2(0, 1004); CenterAt(first, center);
                                    CenterAt(second, center + Vector2.right * (1.5f + second.size.x / 2 - .01f));
                                    CenterAt(third, center + Vector2.right * (1.5f + third.size.x / 2 + .02f));
                                    geometry.Add(Duplicate(first)); Physics2D.SyncTransforms(); var contact = Spawn(center, Vector2.right * 10, 18);
                                    contact.SweepTo(center); contact.SweepTo(center); Observe("initial-contact");
                                    Assert.That(first.health, Is.EqualTo(982)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1));
                                    Assert.That(second.health, Is.EqualTo(982)); Assert.That(third.health, Is.EqualTo(1000));
                                    Assert.That(player.health, Is.EqualTo(100)); Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); break;
                                case 1:
                                    CenterAt(first, new Vector2(2.175f + first.size.x / 2, 1004)); geometry.Add(Surface("BombDefinitionThinWall", new Vector2(2, 1004), new Vector2(.1f, 8)));
                                    Physics2D.SyncTransforms(); var fast = Spawn(new Vector2(0, 1004), Vector2.right * 200, 18); Step(.04f);
                                    Assert.That(first.hurtCollider.bounds.min.x, Is.GreaterThan(2.05f));
                                    Assert.That(Vector2.Distance(fast.Position, first.hurtCollider.ClosestPoint(fast.Position)), Is.LessThan(data.blastRadius));
                                    // Contact bombs retain the Native CircleCast contact margin, unlike the timed wall-face clamp.
                                    Assert.That(fast.Position.x + data.radius, Is.EqualTo(2 - .1f / 2).Within(.01f)); Assert.That(first.health, Is.EqualTo(1000));
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(arena.bombs, Is.Empty); break;
                                case 2:
                                    player.Position = new Vector2(0, 1000); CenterAt(first, new Vector2(1.225f + first.size.x / 2, 1001));
                                    geometry.Add(Surface("BombDefinitionMuzzleWall", new Vector2(1.05f, 1001), new Vector2(.1f, 4))); Physics2D.SyncTransforms();
                                    var muzzle = arena.SpawnBomb(player, data, 18, 1); emitted.Add(muzzle); Observe("muzzle-contact");
                                    Assert.That(first.hurtCollider.bounds.min.x, Is.GreaterThan(1.1f));
                                    Assert.That(Vector2.Distance(muzzle.Position, first.hurtCollider.ClosestPoint(muzzle.Position)), Is.LessThan(data.blastRadius));
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1));
                                    Assert.That(arena.LastBombExplosion.x + data.radius, Is.EqualTo(1.05f - .1f / 2).Within(.01f));
                                    Assert.That(first.health, Is.EqualTo(1000)); Assert.That(player.health, Is.EqualTo(100)); Assert.That(arena.bombs, Is.Empty); break;
                                case 3:
                                    geometry.Add(Surface("BombDefinitionContactOneWay", new Vector2(0, 1003), new Vector2(10, .2f), true)); Physics2D.SyncTransforms();
                                    var crossing = Spawn(new Vector2(-1, 1002), Vector2.up * 10, 18); Step(.15f);
                                    Assert.That(arena.BombExplosionCount, Is.Zero); Assert.That(crossing.Position.y, Is.GreaterThan(1003));
                                    crossing.Position = new Vector2(-1, 1004); crossing.velocity = Vector2.down * 10; Step(.15f);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(crossing.Position.y, Is.EqualTo(1003.3f).Within(.01f)); break;
                                case 4:
                                case 7:
                                    Spawn(new Vector2(0, 2000), Vector2.zero, scenario == 4 ? 18 : 12); Ticks(260);
                                    Assert.That(arena.bombs, Is.Empty); Assert.That(arena.BombExplosionCount, Is.Zero); Assert.That(emitted.Count, Is.EqualTo(1)); break;
                                case 5:
                                    CenterAt(first, new Vector2(0, 1004)); geometry.Add(Duplicate(first)); Physics2D.SyncTransforms();
                                    var overlap = Spawn(first.Center, Vector2.zero, 12); overlap.SweepTo(overlap.Position); overlap.SweepTo(overlap.Position); Observe("fragment-overlaps");
                                    Assert.That(first.health, Is.EqualTo(964)); Assert.That(first.ReceivedHitCount, Is.EqualTo(5));
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(5)); Assert.That(arena.bombs, Is.Empty); Assert.That(player.health, Is.EqualTo(100)); break;
                                case 6:
                                    var origin = new Vector2(0, 1004); geometry.Add(Surface("BombDefinitionFragmentTrigger", origin, new Vector2(.02f, .02f))); Physics2D.SyncTransforms();
                                    var grenade = Spawn(origin, Vector2.zero, 12); grenade.SweepTo(origin); Observe("fragments-spawned");
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(arena.bombs.Count, Is.EqualTo(4));
                                    for (int i = 0; i < 4; i++)
                                    {
                                        float angle = data.fragmentAngles[i]; var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                                        Assert.That(arena.bombs[i].Position, Is.EqualTo(origin + direction * .2f));
                                        Assert.That(arena.bombs[i].velocity, Is.EqualTo(direction * 6)); Assert.That(arena.bombs[i].damage, Is.EqualTo(4));
                                    }
                                    Ticks(35); Assert.That(arena.BombExplosionCount, Is.EqualTo(5)); Assert.That(arena.bombs, Is.Empty); break;
                                case 8:
                                    var airborne = Spawn(new Vector2(0, 2000), Vector2.zero, 50); Ticks(249);
                                    Assert.That(arena.BombExplosionCount, Is.Zero); Ticks(11);
                                    Assert.That(airborne.TimedLanded, Is.False); Assert.That(arena.BombExplosionCount, Is.EqualTo(1));
                                    Assert.That(arena.LastBombExplosion.y, Is.EqualTo(1750).Within(.03f)); break;
                                case 9:
                                    first.Position = new Vector2(0, 1000); geometry.Add(Duplicate(first)); Physics2D.SyncTransforms();
                                    var landed = Spawn(new Vector2(0, 1000.3f), Vector2.down * 2, 50); Step(2);
                                    Assert.That(landed.TimedLanded, Is.True); Assert.That(landed.velocity, Is.EqualTo(Vector2.zero));
                                    Assert.That(first.health, Is.EqualTo(950)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1));
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); break;
                                case 10:
                                    var lateData = Copy(data); lateData.gravity = 0; first.Position = new Vector2(0, 1000); Physics2D.SyncTransforms();
                                    var late = Spawn(new Vector2(0, 1005.1f), Vector2.down, 50, lateData); Ticks(250);
                                    Assert.That(late.TimedLanded, Is.True); Assert.That(late.TimedFuseRemaining, Is.GreaterThan(1.3f)); Assert.That(arena.BombExplosionCount, Is.Zero);
                                    Ticks(60); Assert.That(arena.BombExplosionCount, Is.Zero); Ticks(30);
                                    Assert.That(first.health, Is.EqualTo(950)); Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); break;
                                case 11:
                                    geometry.Add(Surface("BombDefinitionTimedWall", new Vector2(2, 1004), new Vector2(.1f, 8))); Physics2D.SyncTransforms();
                                    var wall = Spawn(new Vector2(0, 1002), Vector2.right * 10, 50); Ticks(15);
                                    Assert.That(wall.velocity.x, Is.Zero); Assert.That(wall.velocity.y, Is.LessThan(0)); Assert.That(wall.TimedLanded, Is.False);
                                    Assert.That(wall.Position.x, Is.LessThanOrEqualTo(1.751f)); Ticks(135);
                                    Assert.That(wall.TimedLanded, Is.True); Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); break;
                                case 12:
                                    geometry.Add(Surface("BombDefinitionTimedCeiling", new Vector2(0, 1003), new Vector2(20, .2f))); Physics2D.SyncTransforms();
                                    var ceiling = Spawn(new Vector2(0, 1002), new Vector2(3, 10), 50); Step(.15f);
                                    Assert.That(ceiling.velocity.x, Is.EqualTo(3)); Assert.That(ceiling.velocity.y, Is.LessThanOrEqualTo(0));
                                    Assert.That(ceiling.Position.y, Is.LessThanOrEqualTo(1002.701f)); Assert.That(ceiling.TimedLanded, Is.False); Ticks(150);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); break;
                                case 13:
                                    geometry.Add(Surface("BombDefinitionTimedOneWay", new Vector2(0, 1003), new Vector2(10, .2f), true)); Physics2D.SyncTransforms();
                                    var down = Spawn(new Vector2(-1, 1004), Vector2.down * 2, 50); var up = Spawn(new Vector2(1, 1002), Vector2.up * 10, 50); Ticks(8);
                                    Assert.That(up.TimedLanded, Is.False); Ticks(142);
                                    Assert.That(down.TimedLanded, Is.True); Assert.That(up.TimedLanded, Is.True); Assert.That(arena.BombExplosionCount, Is.EqualTo(2)); break;
                                case 14:
                                    player.Position = new Vector2(0, 1000); Physics2D.SyncTransforms(); Step(.02f, CombatButtons.Special); Ticks(4);
                                    player.CancelAction(); Observe("cancel-before-spawn"); Ticks(40);
                                    Assert.That(emitted, Is.Empty); Assert.That(source.Cooldown.Available, Is.EqualTo(1)); Assert.That(arena.BombExplosionCount, Is.Zero); break;
                                case 15:
                                    source.StatSlots[1] = 2; first.Position = new Vector2(6, 1000); Physics2D.SyncTransforms(); var independent = Cast(); float ready = source.Cooldown.NextReady;
                                    Assert.That(independent.damage, Is.EqualTo(60).Within(.0001f)); Assert.That(source.Cooldown.Available, Is.Zero);
                                    player.CancelAction(); source.StatSlots[1] = 0; player.selected = 1; Observe("cancel-and-swap"); Ticks(150);
                                    Assert.That(independent.damage, Is.EqualTo(60).Within(.0001f)); Assert.That(first.health, Is.EqualTo(940).Within(.0001f));
                                    Assert.That(source.Cooldown.NextReady, Is.EqualTo(ready)); Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); break;
                                case 16:
                                    source.StatSlots[0] = 2; var converted = Cast(); Assert.That(converted.damage, Is.EqualTo(19.2f).Within(.0001f));
                                    Assert.That(converted.fragmentMultiplier, Is.EqualTo(1.2f)); player.CancelAction(); source.StatSlots[0] = 0; player.selected = 1;
                                    CenterAt(first, converted.Position); Physics2D.SyncTransforms(); converted.SweepTo(converted.Position); Observe("converted-cancel-and-swap");
                                    Assert.That(first.health, Is.EqualTo(952f).Within(.0001f)); Assert.That(first.ReceivedHitCount, Is.EqualTo(5));
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(5)); Assert.That(source.Cooldown.Available, Is.EqualTo(0)); break;
                                case 17:
                                case 18:
                                    var flying = Cast(); var waiting = Spawn(new Vector2(8, 1000.3f), Vector2.down * 2, 50); waiting.Step(.1f);
                                    Assert.That(waiting.TimedLanded, Is.True); Assert.That(flying.TimedLanded, Is.False);
                                    Vector2 flyingPosition = flying.Position, waitingPosition = waiting.Position; float remaining = waiting.TimedFuseRemaining, now = arena.Now, consumedAt = source.Cooldown.NextReady;
                                    if (scenario == 17) { player.health = 0; player.Die(); } else arena.ResetTest();
                                    Observe(scenario == 17 ? "owner-death" : "reset"); Assert.That(arena.bombs, Is.Empty); Assert.That(arena.Now, Is.EqualTo(now));
                                    flying.SweepTo(new Vector2(0, 999)); flying.Step(10); waiting.Step(10); Observe("retained-closed");
                                    Assert.That(flying.Position, Is.EqualTo(flyingPosition)); Assert.That(waiting.Position, Is.EqualTo(waitingPosition));
                                    Assert.That(waiting.TimedFuseRemaining, Is.EqualTo(remaining)); Assert.That(arena.BombExplosionCount, Is.Zero);
                                    if (scenario == 17) { Assert.That(source.Cooldown.NextReady, Is.EqualTo(consumedAt)); Ticks(10); }
                                    else
                                    {
                                        Assert.That(source.Cooldown.Available, Is.EqualTo(1)); ParkActors(arena);
                                        var fresh = Spawn(new Vector2(8, 1000.3f), Vector2.down * 2, 50); Step(2);
                                        Assert.That(fresh.TimedLanded, Is.True); Assert.That(arena.BombExplosionCount, Is.EqualTo(1));
                                    }
                                    break;
                                case 19:
                                    var earlier = Spawn(new Vector2(0, 1000.3f), Vector2.down * 2, 50); Ticks(20); float before = earlier.TimedFuseRemaining;
                                    var later = Spawn(new Vector2(1, 1000.3f), Vector2.down * 2, 50); Observe("second-spawn");
                                    Assert.That(earlier.TimedFuseRemaining, Is.EqualTo(before)); Assert.That(later.TimedFuseRemaining, Is.Zero); Ticks(100);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(2)); Assert.That(arena.bombs, Is.Empty); break;
                            }
                            Assert.That(arena.SimulationFaulted, Is.False); Observe("complete");
                        }
                        finally { foreach (var item in geometry) if (item) Object.DestroyImmediate(item); }
                    }
                    // Only pre-existing consumer APIs are observed. The exact old
                    // BombProjectile pair can run this unchanged in the baseline clone.
                    var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-bombProjectileDefinitionTraceRoot");
                    if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
                }
            }
            finally { arena.showBoxes = oldBoxes; Object.Destroy(floor); }
        }
    }
}
