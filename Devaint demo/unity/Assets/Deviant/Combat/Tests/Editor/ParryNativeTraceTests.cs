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
    public sealed class ParryResponseOrderProbe : CombatActor
    {
        public Action DuringCancel;
        public override void CancelAttack() { DuringCancel?.Invoke(); base.CancelAttack(); }
    }

    // Uses only the pre-extraction Cast/Actor API, so the same two-scene fixture
    // captures the original window, Native centers and synchronous Receive order.
    public class ParryNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public bool accepted, current, listed, resources, sourceExists, sourceDead, faulted;
            public int hits, charges;
            public int[] bits;
            public string[] records;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        [UnityTest] public IEnumerator GameParryNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabParryNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static void CenterAt(CombatActor actor, Vector2 center)
            => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        static ParryResponseOrderProbe Setup(CombatArena arena, CombatArena.ManualSimulation driver, int facing)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            int shield = Array.FindIndex(arena.catalog.forms, item => item.id == "shield");
            Assert.That(player.TryReconfigure(0, shield, 0, out var error), Is.True, error);
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(facing);
            var source = new GameObject("ParryNativeSource").AddComponent<ParryResponseOrderProbe>();
            source.Configure(arena, Vector2.zero, new Vector2(.5f, 1.8f), new Vector2(.125f, .9f), false);
            source.health = 1000; source.SyncFacing(-facing); CenterAt(source, player.Center + new Vector2(2 * facing, 0));
            Physics2D.SyncTransforms(); return source;
        }
        static SkillData Skill(CombatArena arena) => JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(arena.Player.Special));
        static CombatCast Start(CombatArena arena, SkillData skill = null, float startAt = -1)
        {
            var player = arena.Player; var cast = CombatCast.Create(arena, player, skill ?? Skill(arena), player.Weapon, false, startAt);
            player.currentCast = cast; return cast;
        }
        static void Record(Trace trace, List<string> records, string scenario, string phase, bool accepted,
            CombatArena arena, CombatCast cast, CombatActor source)
        {
            var player = arena.Player;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, accepted = accepted,
                current = ReferenceEquals(player.currentCast, cast), listed = arena.casts.Contains(cast), resources = cast && cast.ResourcesActive,
                sourceExists = source, sourceDead = source && source.Dead, faulted = arena.SimulationFaulted,
                hits = source ? source.ReceivedHitCount : -1, charges = player.Weapon.Cooldown.Available,
                records = records.ToArray(), bits = new[] { Bits(arena.Now), Bits(player.Position.x), Bits(player.Position.y),
                    Bits(player.Center.x), player.facing, Bits(player.health), player.ReceivedHitCount,
                    Bits(source ? source.Position.x : 0), Bits(source ? source.Position.y : 0), Bits(source ? source.Center.x : 0),
                    source ? source.facing : 0, Bits(source ? source.health : 0), Bits(source ? source.moveLockUntil : 0),
                    Bits(source ? source.knockUntil : 0), Bits(source ? source.staggerUntil : 0),
                    Bits(cast ? cast.Age : 0), Bits(cast ? cast.EndTime : 0), Bits(player.Weapon.Cooldown.NextReady),
                    Bits(player.Weapon.SpecialBonus) } }); records.Clear();
        }
        static void Remove(ParryResponseOrderProbe source)
        {
            if (!source) return; source.DuringCancel = null; source.gameObject.SetActive(false); Object.Destroy(source.gameObject);
        }
        static void Eligibility(Trace trace, List<string> records, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (string scenario in new[] { "before-activation", "negative-age", "zero", "before-end", "exact-end", "after-end", "cancelled", "no-effect", "other-effect", "motion-complete", "missing-source", "destroyed-source" })
            {
                var source = Setup(arena, driver, 1); var skill = Skill(arena); CombatCast cast;
                if (scenario == "no-effect") skill.effect = null;
                if (scenario == "other-effect") skill.effect.type = "owner_guard";
                if (scenario == "motion-complete")
                {
                    skill.end = .03125f;
                    skill.hits = new[] { new HitData { time = 1, active = 1, anchor = "ranged_projectile" } };
                }
                cast = Start(arena, skill, scenario == "negative-age" ? .03125f : -1);
                try
                {
                    if (scenario != "before-activation") cast.BeforeMovement(scenario == "negative-age" ? .0625f : 0);
                    if (scenario == "before-end") driver.Step(Before(.15f));
                    if (scenario == "exact-end") driver.Step(.15f);
                    if (scenario == "after-end") driver.Step(.25f);
                    if (scenario == "cancelled") cast.Cancel();
                    if (scenario == "motion-complete")
                    {
                        cast.Step(.03125f); Assert.That(arena.Player.currentCast, Is.Null);
                        Assert.That(arena.casts.Contains(cast), Is.True);
                    }
                    if (scenario == "destroyed-source") Object.DestroyImmediate(source.gameObject);
                    bool accepted = cast.TryParry(scenario == "missing-source" ? null : source, false);
                    bool expected = scenario == "zero" || scenario == "before-end" || scenario == "motion-complete";
                    Assert.That(accepted, Is.EqualTo(expected), scenario);
                    if (source) Assert.That(source.ReceivedHitCount, Is.EqualTo(expected ? 1 : 0), scenario);
                    Record(trace, records, scenario, "attempt", accepted, arena, cast, source);
                }
                finally { Remove(source); }
            }
        }
        static void FrontAndLiveReads(Trace trace, List<string> records, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (int facing in new[] { 1, -1 })
            foreach (string scenario in new[] { "front", "back", "front-edge", "behind-edge", "override-front", "override-back", "owner-moved", "source-moved", "owner-turned" })
            {
                var source = Setup(arena, driver, facing); var cast = Start(arena); cast.BeforeMovement(0);
                try
                {
                    var player = arena.Player; Vector2? origin = null;
                    if (scenario == "back" || scenario == "override-front") CenterAt(source, player.Center - new Vector2(2 * facing, 0));
                    if (scenario == "front-edge") CenterAt(source, new Vector2(player.Center.x, player.Center.y + 100));
                    if (scenario == "behind-edge") CenterAt(source, player.Center - new Vector2(.000244140625f * facing, 0));
                    if (scenario == "override-front") origin = player.Center + new Vector2(2 * facing, 0);
                    if (scenario == "override-back") origin = player.Center - new Vector2(2 * facing, 0);
                    if (scenario == "owner-moved") player.Position += new Vector2(4 * facing, 0);
                    if (scenario == "source-moved") source.Position -= new Vector2(4 * facing, 0);
                    if (scenario == "owner-turned") player.SyncFacing(-facing);
                    bool accepted = cast.TryParry(source, false, origin);
                    bool expected = scenario == "front" || scenario == "front-edge" || scenario == "override-front" || scenario == "owner-turned";
                    Assert.That(accepted, Is.EqualTo(expected), scenario + "/" + facing);
                    Assert.That(source.health, Is.EqualTo(expected ? 970 : 1000));
                    Record(trace, records, scenario + "/" + facing, "attempt", accepted, arena, cast, source);
                }
                finally { Remove(source); }
            }
        }
        static void Responses(Trace trace, List<string> records, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (string scenario in new[] { "repeat-melee", "projectile-no-damage", "dead-source-rejects", "invulnerable-source-rejects", "activation-damage", "receive-cancels-cast", "receive-reenters", "receive-throws" })
            {
                var source = Setup(arena, driver, 1); var player = arena.Player;
                if (scenario == "activation-damage") player.Weapon.StatSlots[0] = 2;
                if (scenario == "invulnerable-source-rejects")
                {
                    source.isPlayer = true;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Dash)); Assert.That(player.Invulnerable, Is.True);
                }
                var cast = Start(arena); cast.BeforeMovement(0); float ready = player.Weapon.Cooldown.NextReady;
                try
                {
                    if (scenario == "activation-damage") player.Weapon.StatSlots.Clear();
                    if (scenario == "dead-source-rejects") source.health = 0;
                    if (scenario == "receive-cancels-cast" || scenario == "receive-reenters" || scenario == "receive-throws")
                    {
                        source.health = 20; source.DuringCancel = () =>
                        {
                            Assert.That(source.Dead, Is.True); Assert.That(source.ReceivedHitCount, Is.EqualTo(1));
                            Record(trace, records, scenario, "receive-callback", false, arena, cast, source);
                            if (scenario == "receive-cancels-cast")
                            {
                                cast.Cancel(); player.Position += Vector2.right * 4; player.SyncFacing(-1);
                                Assert.That(cast.TryParry(source, false), Is.False);
                            }
                            if (scenario == "receive-reenters")
                            {
                                Assert.That(cast.TryParry(source, false), Is.True);
                                Assert.That(source.ReceivedHitCount, Is.EqualTo(1));
                            }
                            if (scenario == "receive-throws") throw new InvalidOperationException("parry Native Receive failure");
                        };
                    }
                    if (scenario == "receive-throws")
                    {
                        Assert.Throws<InvalidOperationException>(() => cast.TryParry(source, false));
                        Assert.That(source.Dead, Is.True); Assert.That(source.health, Is.Zero); Assert.That(source.ReceivedHitCount, Is.EqualTo(1));
                        Record(trace, records, scenario, "throw", false, arena, cast, source);
                        source.DuringCancel = null; Assert.That(cast.TryParry(source, false), Is.True);
                        Record(trace, records, scenario, "retry-dead-source", true, arena, cast, source); continue;
                    }
                    bool projectile = scenario == "projectile-no-damage";
                    bool accepted = cast.TryParry(source, projectile); Assert.That(accepted, Is.True, scenario);
                    if (scenario == "repeat-melee" || projectile) Assert.That(cast.TryParry(source, projectile), Is.True);
                    if (scenario == "repeat-melee") { Assert.That(source.health, Is.EqualTo(940)); Assert.That(source.ReceivedHitCount, Is.EqualTo(2)); }
                    if (projectile || scenario == "invulnerable-source-rejects") { Assert.That(source.health, Is.EqualTo(1000)); Assert.That(source.ReceivedHitCount, Is.Zero); }
                    if (scenario == "dead-source-rejects") { Assert.That(source.health, Is.Zero); Assert.That(source.ReceivedHitCount, Is.Zero); }
                    if (scenario == "activation-damage") { Assert.That(source.health, Is.EqualTo(964)); Assert.That(source.ReceivedHitCount, Is.EqualTo(1)); }
                    Assert.That(player.Weapon.Cooldown.NextReady, Is.EqualTo(ready), "Parry itself does not spend another charge.");
                    Assert.That(player.ReceivedHitCount, Is.Zero); Record(trace, records, scenario, "response", accepted, arena, cast, source);
                }
                finally { Remove(source); }
            }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace(); var records = new List<string>();
            var floor = new GameObject("ParryNativeFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            Application.LogCallback observe = (message, stack, type) => { if (message.StartsWith("[CombatV2] ")) records.Add(message); };
            Application.logMessageReceived += observe;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Eligibility(trace, records, arena, driver); FrontAndLiveReads(trace, records, arena, driver); Responses(trace, records, arena, driver);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-parryTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Application.logMessageReceived -= observe; Object.Destroy(floor); }
        }
    }
}
