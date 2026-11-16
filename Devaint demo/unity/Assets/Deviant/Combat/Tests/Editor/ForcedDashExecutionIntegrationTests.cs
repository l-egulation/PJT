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
    public sealed class ForcedDashOrderProbe : CombatActor
    {
        public Action OnDeath;
        public override void CancelAttack() { if (Dead) OnDeath?.Invoke(); base.CancelAttack(); }
    }

    // Frozen before the extraction: this fixture uses only the existing Native
    // APIs, not the new execution state or a second movement implementation.
    public class ForcedDashExecutionIntegrationTests
    {
        const int Ticks = 75;
        [Serializable] sealed class Row
        {
            public string scenario, phase, action;
            public int tick, hits, otherHits, available, casts, resourceStatus;
            public bool current, moving, forced, freeze, grounded, wall, dashing, resources, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GamePreservesForcedDashContactsPushAndControlOrder() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabPreservesForcedDashContactsPushAndControlOrder() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Record(Trace trace, string scenario, string phase, int tick, float step, CombatArena arena, CombatCast cast, CombatActor target = null, CombatActor other = null)
        {
            var p = arena.Player; target = target ? target : arena.enemies[0]; other = other ? other : arena.enemies[1]; var receipt = cast.ResourceResult;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, tick = tick, action = p.currentCast ? p.currentCast.name : "",
                hits = target.ReceivedHitCount, otherHits = other.ReceivedHitCount, available = p.weapons[0].Cooldown.Available, casts = arena.casts.Count,
                resourceStatus = receipt == null ? -1 : (int)receipt.Status, current = ReferenceEquals(p.currentCast, cast), moving = cast.Moving,
                forced = p.IsForcedMoving, freeze = cast.FreezeVertical, grounded = p.grounded, wall = p.wallHit, dashing = p.Dashing,
                resources = cast.ResourcesActive, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(step), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y), Bits(p.health),
                    Bits(target.Position.x), Bits(target.Position.y), Bits(target.health), Bits(target.velocity.x), Bits(target.knockUntil), Bits(target.knockSpeed),
                    Bits(other.Position.x), Bits(other.Position.y), Bits(other.health), Bits(other.velocity.x), Bits(other.knockUntil), Bits(other.knockSpeed),
                    Bits(cast.StartedAt), Bits(cast.Age), Bits(cast.ImpactTime), Bits(cast.MotionDuration), Bits(cast.EndTime), Bits(cast.HorizontalVelocity(1)),
                    Bits(p.weapons[0].Cooldown.NextReady), Bits(p.moveLockUntil), Bits(p.knockUntil), Bits(p.knockSpeed), Bits(p.rootUntil), Bits(p.stunUntil), Bits(p.staggerUntil) } });
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int form, bool air = false)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, form, 2, out var error), Is.True, error);
            p.Position = new Vector2(0, air ? 1004 : 1000); p.velocity = new Vector2(0, air ? 7 : 0); p.grounded = !air; p.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(1.5f, p.Position.y); arena.enemies[1].Position = new Vector2(3.5f, p.Position.y);
            arena.enemies[0].health = arena.enemies[1].health = 1000; Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, SkillData source)
        {
            var cast = CombatCast.Create(arena, arena.Player, source, arena.Player.Weapon, false); arena.Player.currentCast = cast; return cast;
        }
        static void CatalogCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, BoxCollider2D wall)
        {
            int forms = 0;
            for (int form = 0; form < arena.catalog.forms.Length; form++)
            {
                var skills = arena.catalog.forms[form].specials;
                if (skills.Length <= 2 || skills[2].movement == null || skills[2].movement.type != "dash") continue;
                forms++;
                foreach (string scenario in new[] { "normal", "air", "wall-before", "wall-during", "long-step", "cancel-before", "cancel-after", "controls" })
                {
                    wall.enabled = false; Setup(arena, driver, form, scenario == "air");
                    wall.transform.position = new Vector3(scenario == "wall-before" ? .90625f : 2, 1007, 0);
                    wall.enabled = scenario == "wall-before" || scenario == "wall-during"; Physics2D.SyncTransforms();
                    var p = arena.Player; var cast = Start(arena, skills[2]); bool cancelled = false, sawWall = false;
                    if (scenario == "controls")
                    { p.knockUntil = .12f; p.knockSpeed = -5; p.moveLockUntil = p.rootUntil = p.stunUntil = p.staggerUntil = .65f; }
                    string id = arena.catalog.forms[form].id + "/" + scenario;
                    Record(trace, id, "start", -1, 0, arena, cast);
                    for (int tick = 0; tick < Ticks; tick++)
                    {
                        float step = scenario == "long-step" && tick == 0 ? .75f : .02f;
                        if (!cancelled && (scenario == "cancel-before" && tick == 1 || scenario == "cancel-after" && cast.ResourceResult != null))
                        { cancelled = true; p.CancelAction(); Record(trace, id, "cancel", tick, step, arena, cast); }
                        driver.Step(step, new CombatInput(1)); sawWall |= p.wallHit;
                        Record(trace, id, "step", tick, step, arena, cast);
                    }
                    Assert.That(arena.SimulationFaulted, Is.False, id); Assert.That(p.currentCast, Is.Null, id);
                    if (scenario == "cancel-before") { Assert.That(cancelled, Is.True); Assert.That(cast.ResourceResult, Is.Null); }
                    else Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded), id);
                    if (scenario == "wall-before" || scenario == "wall-during") Assert.That(sawWall, Is.True, id);
                }
            }
            Assert.That(forms, Is.EqualTo(5), "Only five of the seven melee movement skills are forced dashes; recast and teleports remain outside this extraction.");
        }
        static void PrimaryCases(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, BoxCollider2D wall)
        {
            int hammer = Array.FindIndex(arena.catalog.forms, form => form.id == "hammer"); var source = arena.catalog.forms[hammer].specials[2];
            foreach (string scenario in new[] { "normal-primary", "boss-primary", "wall-primary", "cancel-primary" })
            {
                wall.enabled = false; Setup(arena, driver, hammer); var p = arena.Player; var target = arena.enemies[0]; var other = arena.enemies[1];
                bool oldBoss = target.isBoss; target.isBoss = scenario == "boss-primary"; other.Position = new Vector2(40, 1000);
                wall.transform.position = new Vector3(2.5f, 1007, 0); wall.enabled = scenario == "wall-primary"; Physics2D.SyncTransforms();
                var cast = Start(arena, source); bool changed = false;
                try
                {
                    for (int tick = 0; tick < Ticks; tick++)
                    {
                        if (!changed && target.ReceivedHitCount > 0)
                        {
                            changed = true;
                            if (scenario == "boss-primary") { other.Position = p.Position + new Vector2(1.5f, 0); Physics2D.SyncTransforms(); }
                            if (scenario == "cancel-primary") p.CancelAction();
                            Record(trace, scenario, "after-primary", tick, .02f, arena, cast);
                        }
                        driver.Step(.02f); Record(trace, scenario, "step", tick, .02f, arena, cast);
                    }
                    Assert.That(changed, Is.True, scenario); Assert.That(target.ReceivedHitCount, Is.EqualTo(scenario == "cancel-primary" ? 1 : 2), scenario);
                    if (scenario == "boss-primary") Assert.That(other.ReceivedHitCount, Is.EqualTo(1), "Boss stopping does not install the wall hit cutoff.");
                }
                finally { target.isBoss = oldBoss; }
            }
        }
        static void FinalSweepOrder(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, BoxCollider2D wall)
        {
            wall.enabled = false; int hammer = Array.FindIndex(arena.catalog.forms, form => form.id == "hammer"); Setup(arena, driver, hammer);
            foreach (var enemy in arena.enemies) enemy.Position = new Vector2(40, 1000);
            var primary = new GameObject("ForcedDashFinalPrimary").AddComponent<ForcedDashOrderProbe>();
            var other = new GameObject("ForcedDashFinalOther").AddComponent<CombatActor>();
            primary.Configure(arena, new Vector2(1.5f, 1000), new Vector2(.5f, 1.8f), new Vector2(0, .9f), false);
            other.Configure(arena, new Vector2(40, 1000), new Vector2(.5f, 1.8f), new Vector2(0, .9f), false);
            primary.health = 40; other.health = 1000; var source = arena.catalog.forms[hammer].specials[2]; var cast = Start(arena, source); bool ended = false;
            primary.OnDeath = () => { ended = true; Assert.That(other.ReceivedHitCount, Is.EqualTo(1), "The final sweep precedes the primary end hit."); Record(trace, "final-sweep-order", "primary-end", -1, .02f, arena, cast, primary, other); };
            try
            {
                for (int tick = 0; tick < Ticks; tick++)
                {
                    if (!ended && arena.Now + .02f >= source.movement.start + source.movement.duration)
                    { other.Position = arena.Player.Position + new Vector2(1.5f, 0); Physics2D.SyncTransforms(); }
                    driver.Step(.02f); Record(trace, "final-sweep-order", "step", tick, .02f, arena, cast, primary, other);
                }
                Assert.That(ended, Is.True); Assert.That(primary.ReceivedHitCount, Is.EqualTo(2));
            }
            finally { primary.OnDeath = null; Object.Destroy(primary.gameObject); Object.Destroy(other.gameObject); }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("ForcedDashFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            var wallObject = new GameObject("ForcedDashFixtureWall"); wallObject.AddComponent<ArenaSurface>(); var wall = wallObject.AddComponent<BoxCollider2D>(); wall.size = new Vector2(.5f, 16); wall.enabled = false; Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                { CatalogCases(trace, arena, driver, wall); PrimaryCases(trace, arena, driver, wall); FinalSweepOrder(trace, arena, driver, wall); Assert.That(arena.SimulationFaulted, Is.False); }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-forcedDashExecutionTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(wallObject); Object.Destroy(floor); }
        }
    }
}
