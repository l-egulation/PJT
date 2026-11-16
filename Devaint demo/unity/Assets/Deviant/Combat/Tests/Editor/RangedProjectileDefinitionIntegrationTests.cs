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
    public class RangedProjectileDefinitionIntegrationTests
    {
        [Serializable] sealed class Row
        {
            public string phase;
            public int scenario, tick, emitted, active, bursts, selected, charges, returns;
            public bool dead;
            public int[] bits, actors, shots, reservations, enemyShots;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static int Form(CombatArena arena, string id) => Array.FindIndex(arena.catalog.forms, item => item.id == id);
        static GameObject Surface(string name, Vector2 position, Vector2 size, bool oneWay = false)
        {
            var surface = new GameObject(name); surface.transform.position = position;
            surface.AddComponent<BoxCollider2D>().size = size;
            surface.AddComponent<ArenaSurface>().oneWay = oneWay;
            return surface;
        }
        static GameObject Duplicate(CombatActor actor)
        {
            var duplicate = new GameObject("RangedDefinitionDuplicate"); duplicate.transform.SetParent(actor.transform, false);
            var box = duplicate.AddComponent<BoxCollider2D>(); box.size = actor.size; box.offset = actor.hurtCollider.offset; box.isTrigger = true;
            duplicate.AddComponent<HurtRegion>().actor = actor; return duplicate;
        }
        static void CenterAt(CombatActor actor, Vector2 center) => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        static void ParkActors(CombatArena arena)
        {
            var player = arena.Player; player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero;
            player.SyncFacing(1); player.grounded = true; player.axis = 0;
            for (int i = 0; i < arena.enemies.Count; i++)
            {
                var enemy = arena.enemies[i]; enemy.aggressive = false; enemy.ResetActor(new Vector2(70 + i * 10, 1000));
                enemy.health = 1000; enemy.SyncFacing(1);
            }
            Physics2D.SyncTransforms();
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int special, string form = "shuriken")
        {
            driver.Reset(); var player = arena.Player; player.InputBlocked = false; player.UseTestInput = true; player.selected = 0;
            ParkActors(arena);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            Assert.That(player.TryReconfigure(0, Form(arena, form), special, out string error), Is.True, error);
        }
        static void Record(Trace trace, string phase, int scenario, int tick, CombatArena arena, WeaponState source, List<PlayerProjectile> emitted)
        {
            var shots = new List<int>();
            foreach (var shot in emitted)
            {
                shots.Add(arena.playerShots.Contains(shot) ? 1 : 0);
                shots.Add(Bits(shot.Position.x)); shots.Add(Bits(shot.Position.y));
                shots.Add(Bits(shot.velocity.x)); shots.Add(Bits(shot.velocity.y));
                shots.Add(Bits(shot.damage)); shots.Add(Bits(shot.traveled));
                shots.Add(Bits(shot.transform.localScale.x)); shots.Add(Bits(shot.transform.eulerAngles.z));
                shots.Add(shot.sourceWeapon == source ? 1 : 0);
            }
            var actors = new List<int>();
            foreach (var actor in arena.enemies)
            {
                actors.Add(Bits(actor.Position.x)); actors.Add(Bits(actor.Position.y));
                actors.Add(Bits(actor.health)); actors.Add(actor.ReceivedHitCount);
                actors.Add(Bits(actor.staggerUntil)); actors.Add(Bits(actor.knockUntil));
            }
            var reservations = new List<int>();
            foreach (var pending in arena.Player.ReturnReservations)
            {
                reservations.Add(Bits(pending.Origin.X)); reservations.Add(Bits(pending.Origin.Y));
                reservations.Add(pending.Origin.Facing); reservations.Add(Bits(pending.Due)); reservations.Add(Bits(pending.Gap));
                reservations.Add(pending.WeaponId == source.InstanceId ? 1 : 0);
            }
            var enemyShots = new List<int>();
            foreach (var shot in arena.projectiles)
            {
                enemyShots.Add(Bits(shot.transform.position.x)); enemyShots.Add(Bits(shot.transform.position.y));
                var state = shot.Execution;
                enemyShots.Add(Bits(shot.velocity.x)); enemyShots.Add(Bits(shot.velocity.y)); enemyShots.Add(Bits(state.Damage));
                enemyShots.Add(state.Reflected ? 1 : 0); enemyShots.Add(shot.source == arena.Player ? 1 : 0);
            }
            var player = arena.Player;
            trace.rows.Add(new Row { phase = phase, scenario = scenario, tick = tick, emitted = arena.PlayerShotCount,
                active = arena.playerShots.Count, bursts = arena.bombBursts.Count, selected = player.selected,
                charges = source.Cooldown.Available, returns = player.ReturnReservations.Count, dead = player.Dead,
                shots = shots.ToArray(), actors = actors.ToArray(), reservations = reservations.ToArray(), enemyShots = enemyShots.ToArray(),
                bits = new[] { Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y),
                    Bits(player.health), Bits(source.Cooldown.NextReady), Bits(source.Cooldown.Period), Bits(source.Cooldown.Debt(arena.Now)) } });
        }
        [UnityTest] public IEnumerator GameUneditedProjectilesKeepNativeContactsAndLifetime() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game", false);
        [UnityTest] public IEnumerator LabUneditedProjectilesKeepNativeContactsAndLifetime() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab", false);
        [UnityTest] public IEnumerator GameSourceEditsAffectOnlySubsequentProjectiles() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game", true);
        [UnityTest] public IEnumerator LabSourceEditsAffectOnlySubsequentProjectiles() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab", true);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label, bool mutations)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var floor = Surface("RangedDefinitionFixtureFloor", new Vector2(0, 999.5f), new Vector2(300, 1));
            bool oldBoxes = arena.showBoxes;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    arena.showBoxes = false;
                    if (mutations) Mutations(arena, driver); else Unedited(arena, driver, label);
                }
            }
            finally { arena.showBoxes = oldBoxes; Object.Destroy(floor); }
        }
        static void Unedited(CombatArena arena, CombatArena.ManualSimulation driver, string label)
        {
            var trace = new Trace(); var player = arena.Player;
            for (int scenario = 0; scenario < 15; scenario++)
            {
                Setup(arena, driver, scenario == 5 || scenario == 6 || scenario == 12 || scenario == 13 ? 3 : scenario == 9 ? 2 : scenario == 7 || scenario == 8 ? 1 : 0, scenario == 14 ? "shield" : "shuriken");
                var source = player.Weapon; var emitted = new List<PlayerProjectile>(); var geometry = new List<GameObject>(); int tick = 0;
                var first = arena.enemies[0]; var second = arena.enemies[1]; var wide = arena.enemies[2];
                void Observe(string phase) => Record(trace, phase, scenario, tick, arena, source, emitted);
                void Step(float dt, CombatButtons buttons = CombatButtons.None)
                {
                    driver.Step(dt, new CombatInput(0, buttons));
                    foreach (var shot in arena.playerShots) if (!emitted.Contains(shot)) emitted.Add(shot);
                    Record(trace, "tick", scenario, tick++, arena, source, emitted);
                }
                void Ticks(int count) { for (int i = 0; i < count; i++) Step(.02f); }
                PlayerProjectile Cast(bool basic = false)
                {
                    Step(.02f, basic ? CombatButtons.Basic : CombatButtons.Special);
                    for (int i = 0; i < 40 && arena.playerShots.Count == 0; i++) Step(.02f);
                    Assert.That(arena.playerShots.Count, Is.EqualTo(1)); return arena.playerShots[0];
                }
                try
                {
                    Observe("start");
                    switch (scenario)
                    {
                        case 0:
                            first.Position = new Vector2(4, 1000); second.Position = new Vector2(8, 1000); geometry.Add(Duplicate(first)); Physics2D.SyncTransforms();
                            Cast(true); Ticks(40);
                            Assert.That(first.health, Is.EqualTo(994)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1)); Assert.That(second.health, Is.EqualTo(1000)); break;
                        case 1:
                            CenterAt(first, new Vector2(1.4f, 1001)); CenterAt(second, new Vector2(.9f, 1001)); geometry.Add(Duplicate(second)); Physics2D.SyncTransforms();
                            arena.FirePlayerRanged(player, player.Form.basics[0].ranged, 6, 1);
                            Assert.That(second.health, Is.EqualTo(994)); Assert.That(first.health, Is.EqualTo(1000)); Assert.That(second.ReceivedHitCount, Is.EqualTo(1)); break;
                        case 2:
                            var overlap = new Vector2(4, 1003); CenterAt(first, overlap);
                            geometry.Add(Surface("RangedDefinitionOverlapWall", overlap, new Vector2(.1f, 4))); Physics2D.SyncTransforms();
                            var blocked = PlayerProjectile.Spawn(arena, player, overlap, Vector2.right * 22, player.Form.basics[0].ranged, 6); emitted.Add(blocked);
                            blocked.SweepTo(overlap); blocked.SweepTo(overlap);
                            Assert.That(first.health, Is.EqualTo(1000)); Assert.That(arena.playerShots, Is.Empty); break;
                        case 3:
                            first.Position = new Vector2(4, 1000); second.Position = new Vector2(8, 1000); geometry.Add(Duplicate(first));
                            geometry.Add(Surface("RangedDefinitionSweepWall", new Vector2(6, 1002), new Vector2(.1f, 6))); Physics2D.SyncTransforms();
                            Cast(); Ticks(40);
                            Assert.That(first.health, Is.EqualTo(976)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1)); Assert.That(first.staggerUntil, Is.Zero);
                            Assert.That(second.health, Is.EqualTo(1000)); Assert.That(arena.playerShots, Is.Empty); break;
                        case 4:
                            geometry.Add(Surface("RangedDefinitionOneWay", new Vector2(3, 1002), new Vector2(.1f, 6), true)); Physics2D.SyncTransforms();
                            arena.FirePlayerRanged(player, player.Special.ranged, 24, 1, source); var endpoint = arena.playerShots[0]; emitted.Add(endpoint); float start = endpoint.Position.x;
                            Ticks(40); Assert.That(endpoint.Position.x - start, Is.EqualTo(8).Within(.0001f)); Assert.That(arena.playerShots, Is.Empty); Assert.That(arena.bombBursts, Is.Empty); break;
                        case 5:
                            wide.Position = new Vector2(5.14f, 1000); geometry.Add(Duplicate(wide)); Physics2D.SyncTransforms();
                            arena.FirePlayerRanged(player, player.Special.ranged, 5, 1, source); emitted.Add(arena.playerShots[0]);
                            Assert.That(wide.health, Is.EqualTo(995)); Observe("initial-contact"); Step(.24f); Assert.That(wide.health, Is.EqualTo(995));
                            Step(.01f); Assert.That(wide.health, Is.EqualTo(990)); Step(.27f); Assert.That(wide.health, Is.EqualTo(985)); Step(1.48f);
                            Assert.That(wide.health, Is.EqualTo(960)); Assert.That(wide.ReceivedHitCount, Is.EqualTo(8)); Assert.That(wide.staggerUntil, Is.Zero);
                            Assert.That(emitted[0].traveled, Is.EqualTo(6).Within(.0001f)); Assert.That(arena.playerShots.Contains(emitted[0]), Is.True);
                            Observe("rounded-flight-end"); Step(.02f); Assert.That(wide.ReceivedHitCount, Is.EqualTo(8)); Assert.That(arena.playerShots, Is.Empty); break;
                        case 6:
                            wide.Position = new Vector2(5.14f, 1000); geometry.Add(Duplicate(wide)); Physics2D.SyncTransforms();
                            arena.FirePlayerRanged(player, player.Special.ranged, 5, 1, source); emitted.Add(arena.playerShots[0]); Step(.1f);
                            wide.Position = new Vector2(70, 1000); Physics2D.SyncTransforms(); Step(.14f);
                            wide.Position = new Vector2(5.14f, 1000); Physics2D.SyncTransforms(); emitted[0].SweepTo(emitted[0].Position);
                            Assert.That(wide.health, Is.EqualTo(995)); Step(.01f); Assert.That(wide.health, Is.EqualTo(990));
                            arena.FirePlayerRanged(player, player.Special.ranged, 5, 1, source); emitted.Add(arena.playerShots[1]); Observe("independent-spawn");
                            Assert.That(wide.health, Is.EqualTo(985)); Step(.24f); Assert.That(wide.health, Is.EqualTo(985)); Step(.01f); Assert.That(wide.health, Is.EqualTo(975));
                            Step(2.02f); Assert.That(wide.health, Is.EqualTo(920)); Assert.That(wide.ReceivedHitCount, Is.EqualTo(16)); Assert.That(arena.playerShots, Is.Empty); break;
                        case 7:
                            var blastOrigin = new Vector2(0, 1004); player.Position = new Vector2(-20, 1000);
                            CenterAt(first, blastOrigin); CenterAt(second, blastOrigin + Vector2.right * 2.8f); CenterAt(wide, blastOrigin + Vector2.right * 7.5f); geometry.Add(Duplicate(first));
                            geometry.Add(Surface("RangedDefinitionBlastWall", blastOrigin + Vector2.right * 2, new Vector2(.1f, 8))); Physics2D.SyncTransforms();
                            var blast = PlayerProjectile.Spawn(arena, player, blastOrigin, Vector2.right * 18, player.Special.ranged, 24); emitted.Add(blast);
                            blast.SweepTo(blastOrigin); blast.SweepTo(blastOrigin); blast.Detonate(); blast.Detonate();
                            Assert.That(first.health, Is.EqualTo(976)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1)); Assert.That(second.health, Is.EqualTo(1000)); Assert.That(wide.health, Is.EqualTo(1000));
                            Assert.That(arena.bombBursts.Count, Is.EqualTo(1)); Assert.That(arena.bombBursts[0].radius, Is.EqualTo(3)); Assert.That(player.health, Is.EqualTo(100)); break;
                        case 8:
                            CenterAt(first, new Vector2(0, 1001)); CenterAt(second, new Vector2(3, 1001));
                            geometry.Add(Surface("RangedDefinitionMuzzleWall", new Vector2(1.4f, 1001), new Vector2(.1f, 4))); Physics2D.SyncTransforms();
                            arena.FirePlayerRanged(player, player.Special.ranged, 24, 1, source);
                            Assert.That(arena.playerShots, Is.Empty); Assert.That(arena.bombBursts.Count, Is.EqualTo(1)); Assert.That(first.health, Is.EqualTo(976)); Assert.That(second.health, Is.EqualTo(1000)); break;
                        case 9:
                            first.Position = new Vector2(6, 1000); Physics2D.SyncTransforms(); Cast();
                            for (int i = 0; i < 40 && !player.ShurikenReturnPending(source); i++) Step(.02f);
                            Assert.That(player.ShurikenReturnPending(source), Is.True); Assert.That(first.health, Is.EqualTo(976));
                            Assert.That(first.hurtCollider.bounds.min.x - player.hurtCollider.bounds.max.x, Is.EqualTo(.25f).Within(.001f)); Observe("visit");
                            player.Position = new Vector2(12, 1000); Physics2D.SyncTransforms();
                            for (int i = 0; i < 260 && player.ShurikenReturnPending(source); i++) Step(.02f);
                            Assert.That(player.ShurikenReturnPending(source), Is.False); Assert.That(player.Position.x, Is.EqualTo(0).Within(.001f)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1)); break;
                        case 10:
                            source.StatSlots[1] = 2; var independent = Cast(); float consumedAt = source.Cooldown.NextReady;
                            Assert.That(independent.damage, Is.EqualTo(28.8f).Within(.00001f)); Assert.That(independent.sourceWeapon, Is.SameAs(source));
                            player.CancelAction(); source.StatSlots[1] = 0; player.selected = 1; first.Position = new Vector2(6, 1000); Physics2D.SyncTransforms(); Observe("cancel-and-swap"); Ticks(40);
                            Assert.That(first.health, Is.EqualTo(971.2f).Within(.0001f)); Assert.That(independent.damage, Is.EqualTo(28.8f).Within(.00001f)); Assert.That(source.Cooldown.NextReady, Is.EqualTo(consumedAt)); break;
                        case 11:
                            Step(.02f, CombatButtons.Special); Ticks(3); player.CancelAction(); Observe("cancel-before-spawn"); Ticks(40);
                            Assert.That(arena.PlayerShotCount, Is.Zero); Assert.That(source.Cooldown.Available, Is.EqualTo(1)); Assert.That(arena.playerShots, Is.Empty); break;
                        case 12:
                            Cast(); player.health = 0; player.Die(); Observe("owner-death"); Assert.That(arena.playerShots, Is.Empty); Ticks(120);
                            Assert.That(first.health, Is.EqualTo(1000)); Assert.That(arena.bombBursts, Is.Empty); Assert.That(player.ReturnReservations, Is.Empty); break;
                        case 13:
                            Cast(); float beforeReset = arena.Now; arena.ResetTest(); Assert.That(arena.Now, Is.EqualTo(beforeReset)); ParkActors(arena); Observe("reset");
                            Assert.That(arena.playerShots, Is.Empty); Assert.That(source.Cooldown.Available, Is.EqualTo(1)); Ticks(120);
                            Assert.That(first.health, Is.EqualTo(1000)); Assert.That(arena.bombBursts, Is.Empty); Assert.That(player.ReturnReservations, Is.Empty); break;
                        case 14:
                            source.StatSlots[1] = 2; first.Position = new Vector2(4, 1000); Physics2D.SyncTransforms(); Step(.02f, CombatButtons.Special);
                            arena.FireEnemyProjectile(first, new Vector2(.9f, 1001), Vector2.left * 8, 8); var reflected = arena.projectiles[0]; Step(.02f);
                            Assert.That(reflected.Execution.Reflected, Is.True); Assert.That(reflected.source, Is.SameAs(player)); Assert.That(reflected.velocity, Is.EqualTo(Vector2.right * 8)); Ticks(30);
                            Assert.That(first.health, Is.EqualTo(990.4f).Within(.0001f)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1)); Assert.That(player.health, Is.EqualTo(100)); Assert.That(arena.projectiles, Is.Empty); break;
                    }
                    Assert.That(arena.SimulationFaulted, Is.False); Observe("complete");
                }
                finally { foreach (var item in geometry) if (item) Object.DestroyImmediate(item); }
            }
            // The fixture uses only pre-existing runtime APIs so the exact old
            // PlayerProjectile/return consumer can run it without a timing model.
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-rangedProjectileTraceRoot");
            if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
        }
        static PlayerProjectile Release(CombatArena arena, CombatArena.ManualSimulation driver, bool basic = false)
        {
            driver.Step(.02f, new CombatInput(0, basic ? CombatButtons.Basic : CombatButtons.Special));
            for (int i = 0; i < 40 && arena.playerShots.Count == 0; i++) driver.Step(.02f);
            Assert.That(arena.playerShots.Count, Is.EqualTo(1)); var shot = arena.playerShots[0]; arena.Player.CancelAction(); return shot;
        }
        // These assertions intentionally fail with the live-DTO consumer. They
        // are separate from the unedited equivalence trace above.
        static void Mutations(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            var player = arena.Player;
            for (int scenario = 0; scenario < 5; scenario++)
            {
                Setup(arena, driver, scenario == 1 ? 3 : scenario == 2 ? 1 : scenario == 3 ? 2 : 0);
                var source = player.Weapon; var first = arena.enemies[0]; var second = arena.enemies[1]; var wide = arena.enemies[2];
                var data = scenario == 4 ? player.Form.basics[0].ranged : player.Special.ranged; string original = JsonUtility.ToJson(data);
                var geometry = new List<GameObject>();
                try
                {
                    if (scenario == 0)
                    {
                        source.StatSlots[1] = 2; var shot = Release(arena, driver); float start = shot.Position.x;
                        data.speed = 1; data.distance = .05f; data.radius = .001f; data.pierce = false; data.noBasicStagger = false;
                        source.StatSlots[1] = 0; player.selected = 1; shot.Step(.02f);
                        Assert.That(shot.Position.x - start, Is.EqualTo(.36f).Within(.0001f)); Assert.That(arena.playerShots.Contains(shot), Is.True);
                        CenterAt(first, shot.Position + Vector2.up * (first.size.y / 2 + .2f)); geometry.Add(Duplicate(first)); Physics2D.SyncTransforms(); shot.SweepTo(shot.Position);
                        Assert.That(first.health, Is.EqualTo(971.2f).Within(.0001f)); Assert.That(first.staggerUntil, Is.Zero); Assert.That(arena.playerShots.Contains(shot), Is.True);
                        CenterAt(second, shot.Position + Vector2.right * 2); Physics2D.SyncTransforms(); shot.SweepTo(shot.Position + Vector2.right * 3);
                        Assert.That(second.health, Is.EqualTo(971.2f).Within(.0001f)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1));
                        first.Position = second.Position = new Vector2(70, 1000); Physics2D.SyncTransforms(); shot.Step(1);
                        Assert.That(shot.traveled, Is.EqualTo(8).Within(.0001f)); Assert.That(shot.damage, Is.EqualTo(28.8f).Within(.00001f)); Assert.That(shot.sourceWeapon, Is.SameAs(source));
                        arena.FirePlayerRanged(player, data, 24, 1, source); var next = arena.playerShots[0]; float nextStart = next.Position.x; next.Step(1);
                        Assert.That(next.Position.x - nextStart, Is.EqualTo(.05f).Within(.0001f)); Assert.That(next.velocity.x, Is.EqualTo(1)); Assert.That(arena.playerShots, Is.Empty);
                        arena.FirePlayerRanged(player, data, 24, 1, source); next = arena.playerShots[0];
                        CenterAt(first, next.Position + Vector2.up * (first.size.y / 2 + .2f)); Physics2D.SyncTransforms(); next.SweepTo(next.Position);
                        Assert.That(first.ReceivedHitCount, Is.EqualTo(1), "The next spawn uses the smaller radius.");
                        CenterAt(first, next.Position); Physics2D.SyncTransforms(); next.SweepTo(next.Position);
                        Assert.That(first.ReceivedHitCount, Is.EqualTo(2)); Assert.That(first.staggerUntil, Is.GreaterThan(arena.Now)); Assert.That(arena.playerShots, Is.Empty);
                    }
                    else if (scenario == 1)
                    {
                        wide.Position = new Vector2(5.14f, 1000); geometry.Add(Duplicate(wide)); Physics2D.SyncTransforms(); var shot = Release(arena, driver);
                        Assert.That(wide.ReceivedHitCount, Is.EqualTo(1)); data.hitInterval = .5f; data.speed = 6;
                        shot.Step(.25f); Assert.That(wide.ReceivedHitCount, Is.EqualTo(2)); Assert.That(shot.velocity.x, Is.EqualTo(3));
                        arena.FirePlayerRanged(player, data, 5, 1, source); var next = arena.playerShots[1]; Assert.That(wide.ReceivedHitCount, Is.EqualTo(3));
                        next.Step(.25f); Assert.That(wide.ReceivedHitCount, Is.EqualTo(3)); next.Step(.25f); Assert.That(wide.ReceivedHitCount, Is.EqualTo(4)); Assert.That(next.velocity.x, Is.EqualTo(6));
                        shot.Step(2); Assert.That(wide.ReceivedHitCount, Is.EqualTo(10)); Assert.That(wide.health, Is.EqualTo(950)); next.Step(2); Assert.That(wide.ReceivedHitCount, Is.EqualTo(10));
                        Assert.That(arena.playerShots, Is.Empty); Assert.That(wide.staggerUntil, Is.Zero);
                    }
                    else if (scenario == 2)
                    {
                        var shot = Release(arena, driver); data.blastRadius = .1f; data.noBasicStagger = false;
                        CenterAt(first, shot.Position); CenterAt(second, shot.Position + Vector2.right * 2.8f); geometry.Add(Duplicate(first)); Physics2D.SyncTransforms();
                        shot.SweepTo(shot.Position); shot.Detonate();
                        Assert.That(first.health, Is.EqualTo(976)); Assert.That(second.health, Is.EqualTo(976)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1));
                        Assert.That(first.staggerUntil, Is.Zero); Assert.That(arena.bombBursts.Count, Is.EqualTo(1)); Assert.That(arena.bombBursts[0].radius, Is.EqualTo(3));
                        first.Position = second.Position = new Vector2(70, 1000); Physics2D.SyncTransforms(); arena.FirePlayerRanged(player, data, 24, 1, source); var next = arena.playerShots[0];
                        CenterAt(first, next.Position); CenterAt(second, next.Position + Vector2.right * 2.8f); Physics2D.SyncTransforms(); next.SweepTo(next.Position); next.Detonate();
                        Assert.That(first.health, Is.EqualTo(952)); Assert.That(second.health, Is.EqualTo(976)); Assert.That(first.staggerUntil, Is.GreaterThan(arena.Now));
                        Assert.That(arena.bombBursts.Count, Is.EqualTo(2)); Assert.That(arena.bombBursts[1].radius, Is.EqualTo(.1f)); Assert.That(arena.playerShots, Is.Empty);
                    }
                    else if (scenario == 3)
                    {
                        var shot = Release(arena, driver); data.returnDelay = .2f; data.behindGap = 1.25f; player.selected = 1;
                        first.Position = new Vector2(6, 1000); Physics2D.SyncTransforms(); shot.Step(.4f);
                        Assert.That(player.ReturnReservations.Count, Is.EqualTo(1)); var pending = player.ReturnReservations[0];
                        Assert.That(pending.WeaponId, Is.EqualTo(source.InstanceId)); Assert.That(pending.Due - arena.Now, Is.EqualTo(5).Within(.00001f)); Assert.That(pending.Gap, Is.EqualTo(.25f));
                        Assert.That(first.hurtCollider.bounds.min.x - player.hurtCollider.bounds.max.x, Is.EqualTo(.25f).Within(.001f));
                        first.Position = new Vector2(70, 1000); player.Position = new Vector2(0, 1000); player.SyncFacing(1); Physics2D.SyncTransforms();
                        var nextSource = player.Weapon; arena.FirePlayerRanged(player, data, 24, 1, nextSource); var next = arena.playerShots[0];
                        first.Position = new Vector2(6, 1000); Physics2D.SyncTransforms(); next.Step(.4f);
                        Assert.That(player.ReturnReservations.Count, Is.EqualTo(2)); var nextPending = player.ReturnReservations[1];
                        Assert.That(nextPending.WeaponId, Is.EqualTo(nextSource.InstanceId)); Assert.That(nextPending.Due - arena.Now, Is.EqualTo(.2f).Within(.00001f)); Assert.That(nextPending.Gap, Is.EqualTo(1.25f));
                        Assert.That(first.hurtCollider.bounds.min.x - player.hurtCollider.bounds.max.x, Is.EqualTo(1.25f).Within(.001f)); Assert.That(first.ReceivedHitCount, Is.EqualTo(2));
                    }
                    else
                    {
                        var shot = Release(arena, driver, true); data.type = "ball"; data.pierce = true; data.noBasicStagger = true;
                        CenterAt(first, shot.Position + Vector2.right * .55f); CenterAt(second, shot.Position + Vector2.right * .05f); geometry.Add(Duplicate(second)); Physics2D.SyncTransforms(); shot.SweepTo(shot.Position);
                        Assert.That(first.health, Is.EqualTo(1000)); Assert.That(second.health, Is.EqualTo(994)); Assert.That(second.ReceivedHitCount, Is.EqualTo(1));
                        Assert.That(second.staggerUntil, Is.GreaterThan(arena.Now)); Assert.That(arena.playerShots, Is.Empty);
                        first.Position = second.Position = new Vector2(70, 1000); Physics2D.SyncTransforms();
                        var next = PlayerProjectile.Spawn(arena, player, new Vector2(0, 1004), Vector2.right * data.speed, data, 6);
                        CenterAt(first, next.Position + Vector2.right * .55f); CenterAt(second, next.Position + Vector2.right * .05f); Physics2D.SyncTransforms(); next.SweepTo(next.Position);
                        Assert.That(first.health, Is.EqualTo(994)); Assert.That(second.health, Is.EqualTo(988)); Assert.That(first.staggerUntil, Is.Zero); Assert.That(arena.playerShots.Contains(next), Is.True);
                    }
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { JsonUtility.FromJsonOverwrite(original, data); foreach (var item in geometry) if (item) Object.DestroyImmediate(item); }
            }
        }
    }
}
