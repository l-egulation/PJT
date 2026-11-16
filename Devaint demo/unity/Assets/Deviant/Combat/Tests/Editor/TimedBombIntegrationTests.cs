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
    public class TimedBombIntegrationTests
    {
        const int Scenarios = 15;
        [Serializable] sealed class Row
        {
            public string phase;
            public int scenario, tick, explosions, selected, hits, charges;
            public bool ownerDead;
            public int[] bits, bombs;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static BombData Copy(BombData data) => JsonUtility.FromJson<BombData>(JsonUtility.ToJson(data));
        static GameObject Surface(string name, Vector2 position, Vector2 size, bool oneWay = false)
        {
            var surface = new GameObject(name); surface.transform.position = position;
            surface.AddComponent<BoxCollider2D>().size = size;
            surface.AddComponent<ArenaSurface>().oneWay = oneWay;
            return surface;
        }
        static void Record(Trace trace, string phase, int scenario, int tick, CombatArena arena, WeaponState source, List<BombProjectile> emitted)
        {
            var bombs = new List<int>();
            foreach (var bomb in emitted)
            {
                bombs.Add(arena.bombs.Contains(bomb) ? 1 : 0);
                bombs.Add(bomb.TimedLanded ? 1 : 0);
                bombs.Add(Bits(bomb.Position.x)); bombs.Add(Bits(bomb.Position.y));
                bombs.Add(Bits(bomb.velocity.x)); bombs.Add(Bits(bomb.velocity.y));
                // Remaining fuse and the configured flight limit expose both
                // deadline paths without reading either implementation's fields.
                bombs.Add(Bits(bomb.TimedFuseRemaining)); bombs.Add(Bits(bomb.Definition.Lifetime));
                bombs.Add(Bits(bomb.Definition.FuseSeconds)); bombs.Add(Bits(bomb.damage));
            }
            var player = arena.Player; var target = arena.enemies[0];
            trace.rows.Add(new Row { phase = phase, scenario = scenario, tick = tick, explosions = arena.BombExplosionCount,
                selected = player.selected, hits = target.ReceivedHitCount, charges = source.Cooldown.Available,
                ownerDead = player.Dead, bombs = bombs.ToArray(), bits = new[] {
                    Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y),
                    Bits(player.health), Bits(target.health), Bits(source.Cooldown.NextReady), Bits(source.Cooldown.Period),
                    Bits(source.Cooldown.Debt(arena.Now)), Bits(arena.LastBombExplosion.x), Bits(arena.LastBombExplosion.y) } });
        }
        [UnityTest] public IEnumerator GameKeepsNativeTimedBombFlightFuseAndLifetime()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            Exercise("game");
        }
        [UnityTest] public IEnumerator LabKeepsNativeTimedBombFlightFuseAndLifetime()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            Exercise("lab");
        }
        [UnityTest] public IEnumerator GameResetAndDeathCloseRetainedBombsBeforeDeferredDestruction()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            ExerciseRetainedCleanup();
        }
        [UnityTest] public IEnumerator LabResetAndDeathCloseRetainedBombsBeforeDeferredDestruction()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            ExerciseRetainedCleanup();
        }
        // This is intentionally separate from the equivalence trace: old
        // ResetTest queued Destroy but left retained Step/SweepTo calls active.
        static void ExerciseRetainedCleanup()
        {
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = Surface("RetainedTimedBombFloor", new Vector2(0, 999.5f), new Vector2(200, 1));
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    for (int death = 0; death < 2; death++)
                    {
                        driver.Reset();
                        foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                        int form = Array.FindIndex(arena.catalog.forms, item => item.id == "bomb");
                        var data = Copy(arena.catalog.forms[form].specials[3].bomb);
                        player.Position = new Vector2(-20, 1000); player.velocity = Vector2.zero;
                        var landed = BombProjectile.Spawn(arena, player, new Vector2(0, 1000.3f), Vector2.down * 2, data, 50);
                        var flying = BombProjectile.Spawn(arena, player, new Vector2(4, 1003), Vector2.right * 2, Copy(data), 50);
                        Physics2D.SyncTransforms(); landed.Step(.1f); flying.Step(.01f);
                        Assert.That(landed.TimedLanded, Is.True); Assert.That(flying.TimedLanded, Is.False);
                        Vector2 landedPosition = landed.Position, flyingPosition = flying.Position, flyingVelocity = flying.velocity;
                        float remaining = landed.TimedFuseRemaining; int explosions = arena.BombExplosionCount;
                        if (death == 0) arena.ResetTest(); else { player.health = 0; player.Die(); }
                        Assert.That(arena.bombs, Is.Empty);
                        // No yield: every call runs before Unity flushes Destroy.
                        flying.SweepTo(new Vector2(4, 999)); flying.Step(10); landed.Step(10);
                        Assert.That(flying.TimedLanded, Is.False); Assert.That(flying.Position, Is.EqualTo(flyingPosition));
                        Assert.That(flying.velocity, Is.EqualTo(flyingVelocity)); Assert.That(landed.Position, Is.EqualTo(landedPosition));
                        Assert.That(landed.TimedFuseRemaining, Is.EqualTo(remaining)); Assert.That(arena.BombExplosionCount, Is.EqualTo(explosions));
                        Assert.That(arena.bombs, Is.Empty);
                        if (death == 1) { player.health = player.maxHealth; player.Position = new Vector2(-20, 1000); }
                        var next = BombProjectile.Spawn(arena, player, new Vector2(8, 1000.3f), Vector2.down * 2, Copy(data), 50);
                        Assert.That(next.TimedLanded, Is.False); next.Step(.1f);
                        Assert.That(next.TimedLanded, Is.True); Assert.That(next.TimedFuseRemaining, Is.GreaterThan(1.3f));
                        next.Step(2); Assert.That(arena.BombExplosionCount, Is.EqualTo(explosions + 1));
                    }
                }
            }
            finally { Object.Destroy(floor); }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Exercise(string label)
        {
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace();
            var floor = Surface("TimedBombFixtureFloor", new Vector2(0, 999.5f), new Vector2(200, 1));
            bool oldBoxes = arena.showBoxes;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    arena.showBoxes = false;
                    int form = Array.FindIndex(arena.catalog.forms, item => item.id == "bomb");
                    for (int scenario = 0; scenario < Scenarios; scenario++)
                    {
                        driver.Reset(); player.InputBlocked = false; player.selected = 0;
                        foreach (var enemy in arena.enemies) enemy.aggressive = false;
                        player.Weapon.StatSlots.Clear(); player.Weapon.StatSlots[0] = 1; player.Weapon.StatSlots[1] = 2;
                        player.Weapon.StatSlots[2] = 3; player.Weapon.StatSlots[3] = 4;
                        Assert.That(player.TryReconfigure(0, form, 3, out string error), Is.True, error);
                        player.Position = new Vector2(-20, 1000); player.velocity = Vector2.zero;
                        player.SyncFacing(1); player.ClearHitMotionLocks();
                        foreach (var enemy in arena.enemies)
                        { enemy.aggressive = false; enemy.health = 1000; enemy.Position = new Vector2(-35, 1000); enemy.velocity = Vector2.zero; }
                        var target = arena.enemies[0]; var source = player.Weapon; var data = Copy(player.Special.bomb);
                        var emitted = new List<BombProjectile>(); var geometry = new List<GameObject>();
                        BombProjectile first = null, second = null; bool command = false, sawLanding = false, sawWall = false, sawCeiling = false;
                        float fuseBeforeSecond = 0, consumedAt = 0;
                        int ticks = scenario == 0 || scenario == 6 ? 260 : scenario == 1 ? 340 : 150;
                        try
                        {
                            switch (scenario)
                            {
                                case 0: first = BombProjectile.Spawn(arena, player, new Vector2(0, 2000), Vector2.zero, data, 50); break;
                                case 1:
                                    data.gravity = 0; target.Position = new Vector2(0, 1000);
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1005.1f), Vector2.down, data, 50); break;
                                case 2:
                                    target.Position = new Vector2(0, 1000);
                                    var duplicate = new GameObject("TimedBombFixtureDuplicate"); duplicate.transform.SetParent(target.transform, false);
                                    var collider = duplicate.AddComponent<BoxCollider2D>(); collider.size = target.size; collider.offset = target.offset; collider.isTrigger = true;
                                    duplicate.AddComponent<HurtRegion>().actor = target; geometry.Add(duplicate);
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1000.3f), Vector2.down * 2, data, 50); break;
                                case 3:
                                    geometry.Add(Surface("TimedBombFixtureWall", new Vector2(2, 1004), new Vector2(.1f, 8)));
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1002), Vector2.right * 10, data, 50); break;
                                case 4:
                                    geometry.Add(Surface("TimedBombFixtureCeiling", new Vector2(0, 1003), new Vector2(20, .2f)));
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1002), new Vector2(3, 10), data, 50); break;
                                case 5:
                                    geometry.Add(Surface("TimedBombFixtureOneWay", new Vector2(0, 1003), new Vector2(10, .2f), true));
                                    first = BombProjectile.Spawn(arena, player, new Vector2(-1, 1004), Vector2.down * 2, data, 50);
                                    second = BombProjectile.Spawn(arena, player, new Vector2(1, 1002), Vector2.up * 10, data, 50); break;
                                case 6:
                                    data.gravity = 0;
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1002), Vector2.zero, data, 50);
                                    first.SweepTo(first.Position); first.Step(0); first.Step(.000001f);
                                    first.SweepTo(first.Position + Vector2.right);
                                    Assert.That(first.TimedLanded, Is.False); Assert.That(first.TimedFuseRemaining, Is.Zero); break;
                                case 7:
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1001), new Vector2(7, -2), data, 50); break;
                                case 8:
                                case 12:
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1000.3f), Vector2.down * 2, data, 50); break;
                                case 13:
                                    target.Position = new Vector2(2, 1000);
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1001), new Vector2(2, -2), data, 50); break;
                                case 14:
                                    first = BombProjectile.Spawn(arena, player, new Vector2(0, 1001), Vector2.down, data, 50);
                                    Physics2D.SyncTransforms(); first.SweepTo(new Vector2(0, 999));
                                    Assert.That(first.TimedLanded, Is.True);
                                    Assert.That(first.TimedFuseRemaining, Is.EqualTo(1.5f)); break;
                                case 9:
                                case 10:
                                case 11:
                                    player.Position = new Vector2(0, 1000);
                                    if (scenario == 10) target.Position = new Vector2(6, 1000);
                                    break;
                            }
                            if (first) emitted.Add(first); if (second) emitted.Add(second);
                            Physics2D.SyncTransforms(); Record(trace, "start", scenario, -1, arena, source, emitted);
                            for (int tick = 0; tick < ticks; tick++)
                            {
                                if (scenario == 8 && tick == 20)
                                {
                                    fuseBeforeSecond = first.TimedFuseRemaining;
                                    second = BombProjectile.Spawn(arena, player, new Vector2(1, 1000.3f), Vector2.down * 2, Copy(data), 50);
                                    emitted.Add(second); Record(trace, "second-spawn", scenario, tick, arena, source, emitted);
                                    Assert.That(first.TimedFuseRemaining, Is.EqualTo(fuseBeforeSecond));
                                }
                                if (scenario == 9 && tick == 5)
                                { player.CancelAction(); command = true; Record(trace, "cancel-before-emission", scenario, tick, arena, source, emitted); }
                                if ((scenario == 10 || scenario == 11) && first && !command)
                                {
                                    command = true; consumedAt = source.Cooldown.NextReady;
                                    Assert.That(first.damage, Is.EqualTo(60).Within(.001f)); Assert.That(source.Cooldown.Available, Is.Zero);
                                    if (scenario == 10)
                                    {
                                        player.CancelAction(); source.StatSlots[1] = 0; player.selected = 1;
                                        Assert.That(arena.bombs.Contains(first), Is.True); Assert.That(first.damage, Is.EqualTo(60).Within(.001f));
                                    }
                                    else { player.health = 0; player.Die(); }
                                    Record(trace, scenario == 10 ? "cancel-and-swap" : "owner-death", scenario, tick, arena, source, emitted);
                                }
                                if (scenario == 12 && first.TimedLanded && !command)
                                {
                                    command = true; float now = arena.Now; arena.ResetTest();
                                    Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.bombs, Is.Empty);
                                    Assert.That(source.Cooldown.Available, Is.EqualTo(1));
                                    Record(trace, "reset-during-fuse", scenario, tick, arena, source, emitted);
                                    player.Position = new Vector2(-20, 1000);
                                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                                    second = BombProjectile.Spawn(arena, player, new Vector2(1, 1000.3f), Vector2.down * 2, Copy(data), 50);
                                    emitted.Add(second); Assert.That(second.TimedLanded, Is.False); Assert.That(second.TimedFuseRemaining, Is.Zero);
                                    Physics2D.SyncTransforms();
                                }
                                float dt = scenario == 13 && tick == 0 ? 2 : scenario == 7 ? tick == 0 ? .000001f : tick % 3 == 0 ? .047f : .013f : .02f;
                                driver.Step(dt, new CombatInput(0, (scenario == 9 || scenario == 10 || scenario == 11) && tick == 0 ? CombatButtons.Special : CombatButtons.None));
                                if (scenario == 13 && tick == 0)
                                {
                                    Assert.That(first.TimedLanded, Is.True); Assert.That(arena.BombExplosionCount, Is.EqualTo(1));
                                    Assert.That(first.velocity, Is.EqualTo(Vector2.zero)); Assert.That(first.TimedFuseRemaining, Is.InRange(0f, .000001f));
                                }
                                foreach (var bomb in arena.bombs) if (!emitted.Contains(bomb)) { emitted.Add(bomb); if (!first) first = bomb; }
                                if (first && first.TimedLanded)
                                {
                                    if (!sawLanding) Record(trace, "first-landing", scenario, tick, arena, source, emitted);
                                    sawLanding = true;
                                    if (scenario == 1 && arena.Now < 5.2f) Assert.That(arena.BombExplosionCount, Is.Zero, "Landing replaces the flight deadline with a complete fuse.");
                                }
                                if (scenario == 3 && first.velocity.x == 0 && !first.TimedLanded) sawWall = true;
                                if (scenario == 4 && first.velocity.y <= 0 && first.velocity.x == 3 && !first.TimedLanded) sawCeiling = true;
                                if (scenario == 0 && tick == 248 || scenario == 6 && tick == 248)
                                    Assert.That(arena.BombExplosionCount, Is.Zero, "Flight does not expire at an earlier tick.");
                                if (scenario == 5 && tick == 7) Assert.That(second.TimedLanded, Is.False, "An upward one-way contact cannot arm the fuse.");
                                if (scenario == 10 && command) Assert.That(source.Cooldown.NextReady, Is.EqualTo(consumedAt));
                                Record(trace, "tick", scenario, tick, arena, source, emitted);
                            }
                            Assert.That(arena.SimulationFaulted, Is.False);
                            if (scenario == 9)
                            {
                                Assert.That(command, Is.True); Assert.That(emitted, Is.Empty); Assert.That(source.Cooldown.Available, Is.EqualTo(1));
                                Assert.That(arena.BombExplosionCount, Is.Zero);
                            }
                            else if (scenario == 11)
                            { Assert.That(command, Is.True); Assert.That(player.Dead, Is.True); Assert.That(arena.bombs, Is.Empty); Assert.That(arena.BombExplosionCount, Is.Zero); }
                            else
                            {
                                Assert.That(arena.BombExplosionCount, Is.EqualTo(scenario == 5 || scenario == 8 ? 2 : 1), "Scenario " + scenario);
                                Assert.That(arena.bombs, Is.Empty);
                                Assert.That(sawLanding, Is.EqualTo(scenario != 0 && scenario != 6));
                            }
                            if (scenario == 0 || scenario == 6) Assert.That(first.TimedLanded, Is.False);
                            if (scenario == 1 || scenario == 2) { Assert.That(target.health, Is.EqualTo(950)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); }
                            if (scenario == 2) Assert.That(first.velocity, Is.EqualTo(Vector2.zero));
                            if (scenario == 3) Assert.That(sawWall, Is.True);
                            if (scenario == 4) Assert.That(sawCeiling, Is.True);
                            if (scenario == 10) { Assert.That(command, Is.True); Assert.That(target.health, Is.EqualTo(940).Within(.001f)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); }
                            if (scenario == 12) Assert.That(command, Is.True);
                            Record(trace, "complete", scenario, ticks, arena, source, emitted);
                        }
                        finally { foreach (var item in geometry) if (item) Object.DestroyImmediate(item); }
                    }
                }
                // The same fixture runs with the exact old BombProjectile and
                // BombProjectileTimed pair restored, not a copied timing model.
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-timedBombTraceRoot");
                if (index >= 0 && index + 1 < args.Length)
                { Directory.CreateDirectory(args[index + 1]); File.WriteAllText(Path.Combine(args[index + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { arena.showBoxes = oldBoxes; Object.Destroy(floor); }
        }
    }
}
