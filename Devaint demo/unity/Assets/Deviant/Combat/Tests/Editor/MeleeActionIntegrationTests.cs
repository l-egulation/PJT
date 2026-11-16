using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class MeleeActionIntegrationTests
    {
        sealed class Trace
        {
            public readonly List<Vector2> Positions = new List<Vector2>();
            public readonly List<float> Health = new List<float>();
            public readonly List<string> Actions = new List<string>();
            public readonly List<float> Starts = new List<float>();
            public int Hits;
        }
        [UnityTest] public IEnumerator GameUsesSharedFirstHitTimeline() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesSharedFirstHitTimeline() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int speedSlots = 0, bool airborne = false)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var error), Is.True, error);
            for (int i = 0; i < speedSlots; i++) p.Weapon.StatSlots[i] = 3;
            p.Position = airborne ? new Vector2(0, 3) : Vector2.zero; p.grounded = !airborne; p.velocity = Vector2.zero; p.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(3, airborne ? 3 : 0); Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, bool migrated, float startAt = -1)
        {
            // Enter the real common basic compiler before movement, so both paths
            // share its payload, speed and combo index. The baseline then uses the
            // retained legacy Create overload, never both runners for one cast.
            typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena.Player, new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), 0, startAt });
            var cast = arena.Player.currentCast;
            Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Preparing)); Assert.That(cast.ActionTimelineVersion, Is.Not.Null.And.Not.Empty);
            if (!migrated)
            {
                var payload = SkillExecutionDefinitionIntegrationTests.BasicSource(arena, 0); string version = cast.DefinitionVersion; float started = cast.StartedAt;
                cast.Cancel(); arena.casts.Remove(cast); Object.Destroy(cast.gameObject);
                cast = CombatCast.Create(arena, arena.Player, payload, arena.Player.Weapon, true, started, version); arena.Player.currentCast = cast;
                Assert.That(cast.ActionTimelineVersion, Is.Null);
            }
            return cast;
        }
        static Trace Run(CombatArena arena, CombatArena.ManualSimulation driver, bool migrated, int speedSlots, bool airborne, bool queued, int cancelTick, bool jump, float startAt)
        {
            Setup(arena, driver, speedSlots, airborne); var p = arena.Player; var first = Start(arena, migrated, startAt); var trace = new Trace();
            for (int tick = 0; tick < 60; tick++)
            {
                var buttons = queued && tick == 2 ? CombatButtons.Basic : CombatButtons.None;
                if (tick == cancelTick) buttons |= jump ? CombatButtons.Jump : CombatButtons.Dash;
                driver.Step(.02f, new CombatInput(1, buttons));
                trace.Positions.Add(p.Position); trace.Health.Add(arena.enemies[0].health);
                trace.Actions.Add(p.currentCast ? p.currentCast.Definition.Id : ""); trace.Starts.Add(p.currentCast ? p.currentCast.StartedAt : -1);
            }
            trace.Hits = arena.enemies[0].ReceivedHitCount;
            if (migrated) Assert.That(first.ActionTimelineState, Is.EqualTo(cancelTick < 0 ? MeleeActionState.Completed : MeleeActionState.Cancelled));
            return trace;
        }
        static void Same(Trace legacy, Trace migrated)
        {
            Assert.That(migrated.Health, Is.EqualTo(legacy.Health)); Assert.That(migrated.Actions, Is.EqualTo(legacy.Actions)); Assert.That(migrated.Starts, Is.EqualTo(legacy.Starts)); Assert.That(migrated.Hits, Is.EqualTo(legacy.Hits));
            Assert.That(migrated.Positions.Count, Is.EqualTo(legacy.Positions.Count));
            for (int i = 0; i < legacy.Positions.Count; i++) Assert.That(Vector2.Distance(legacy.Positions[i], migrated.Positions[i]), Is.LessThan(.000001f), "Movement tick " + i);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var original = arena.BasicSwordFirst;
            // Explicit common ground: the product forest and lab do not share a Y=0 floor.
            var floor = new GameObject("MeleeTimelineFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms();
            try
            {
            using (var driver = arena.AcquireManualSimulation())
            {
                foreach (var scenario in new[] {
                    (speed:0, air:false, queued:false, cancel:-1, jump:false, start:0f),
                    (speed:5, air:false, queued:false, cancel:-1, jump:false, start:0f),
                    (speed:0, air:true, queued:false, cancel:-1, jump:false, start:0f),
                    (speed:0, air:false, queued:true, cancel:-1, jump:false, start:.015f),
                    (speed:0, air:false, queued:true, cancel:2, jump:true, start:0f),
                    (speed:0, air:false, queued:true, cancel:11, jump:false, start:0f)
                }) {
                    var legacy = Run(arena, driver, false, scenario.speed, scenario.air, scenario.queued, scenario.cancel, scenario.jump, scenario.start);
                    var migrated = Run(arena, driver, true, scenario.speed, scenario.air, scenario.queued, scenario.cancel, scenario.jump, scenario.start); Same(legacy, migrated);
                }
                var shortHit = original.ToData(); shortHit.impact = .001f; shortHit.active = .0002f; shortHit.duration = .003f;
                Assert.That(arena.TryApplyBasicSwordFirst(shortHit, true, out var error), Is.True, error);
                Same(Run(arena, driver, false, 5, false, false, -1, false, 0), Run(arena, driver, true, 5, false, false, -1, false, 0));
                Assert.That(arena.TryApplyBasicSwordFirst(original.ToData(), true, out error), Is.True, error);

                Setup(arena, driver); var first = Start(arena, true); var input = SkillExecutionDefinitionIntegrationTests.BasicSource(arena, 0);
                first = SkillExecutionDefinitionIntegrationTests.ReplaceBasic(arena, first, input, true); string version = first.ActionTimelineVersion;
                input.end = 99; input.activation = 99; input.hits[0].time = 99; input.hits[0].damage = 999; input.hits[0].box.width = 999; input.basicLungeDistance = 999;
                p.Weapon.StatSlots[0] = 1; // Basic bonus is still captured at actual activation.
                for (int tick = 0; tick < 20; tick++) driver.Step(.02f, new CombatInput(1));
                Assert.That(first.EndTime, Is.EqualTo(.4f)); Assert.That(first.MotionDuration, Is.EqualTo(.4f)); Assert.That(first.ImpactTime, Is.EqualTo(.2f));
                Assert.That(first.ActionTimelineVersion, Is.EqualTo(version)); Assert.That(first.ActionTimelineState, Is.EqualTo(MeleeActionState.Completed));
                Assert.That(arena.enemies[0].health, Is.EqualTo(88)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(p.Position.x, Is.EqualTo(.65f).Within(.00001f));
                Assert.That(p.currentCast, Is.Null);

                Setup(arena, driver); first = Start(arena, true); driver.Step(.02f); driver.Reset();
                Assert.That(first.ActionTimelineState, Is.EqualTo(MeleeActionState.Cancelled)); Assert.That(p.currentCast, Is.Null); Assert.That(arena.casts, Is.Empty);
                first = Start(arena, true); p.health = 0; p.Die(); Assert.That(first.ActionTimelineState, Is.EqualTo(MeleeActionState.Cancelled)); Assert.That(arena.casts, Is.Empty);
            }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
