using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SwordComboTimelineTests
    {
        sealed class Trace
        {
            public readonly List<Vector2> Positions = new List<Vector2>();
            public readonly List<float> Health = new List<float>();
            public readonly List<int> Hits = new List<int>();
            public readonly List<string> Actions = new List<string>();
            public readonly List<float> Starts = new List<float>();
        }
        [Test] public void SecondSwordDefinitionUsesExistingCatalogNumbersAndOwnsItsCopies()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var source = Array.Find(catalog.forms, f => f.id == "base_sword").basics[1];
            Assert.That(BasicMeleeDefinition.TryCreate("base_sword_basic_1", source, out var definition, out var error), Is.True, error);
            Assert.That(definition.Damage, Is.EqualTo(13)); Assert.That(definition.Duration, Is.EqualTo(.37f)); Assert.That(definition.Impact, Is.EqualTo(.13f)); Assert.That(definition.Active, Is.EqualTo(.08f));
            Assert.That(definition.Width, Is.EqualTo(3.1875f)); Assert.That(definition.OffsetX, Is.EqualTo(1.0625f));
            source.box.width = 999; source.impact = 99; var copy = definition.ToData(); copy.box.width = 88;
            var timeline = new MeleeActionDefinition(definition, 1.5f, .65f, .16f / 1.5f, .08f / 1.5f);
            Assert.That(timeline.End, Is.EqualTo(.37f / 1.5f)); Assert.That(timeline.Impact, Is.EqualTo(.13f / 1.5f)); Assert.That(timeline.CreateHit().box.width, Is.EqualTo(3.1875f));
            BasicMeleeDefinition.TryCreate("base_sword_basic_0", definition.ToData(), out var otherId, out _);
            Assert.That(otherId.ContentHash, Is.Not.EqualTo(definition.ContentHash), "The same numbers cannot alias another hit's identity.");
        }
        [Test] public void FactoryRejectsOtherPoliciesAndMismatchedVersionsBeforeCreatingACast()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var source = Array.Find(catalog.forms, f => f.id == "base_sword").basics[1];
            BasicMeleeDefinition.TryCreate("base_sword_basic_1", source, out var definition, out _);
            var timeline = new MeleeActionDefinition(definition, 1, .65f, .16f, .08f); var weapon = new WeaponState();
            var data = new SkillData { id = definition.Id }; int count = Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, data, weapon, false, definitionVersion: definition.ContentHash, timeline: timeline));
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, data, weapon, true, definitionVersion: "stale", timeline: timeline));
            data.id = "base_sword_basic_0";
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, data, weapon, true, definitionVersion: definition.ContentHash, timeline: timeline));
            BasicMeleeDefinition.TryCreate("dagger_basic_1", source, out var other, out _); data.id = other.Id; data.kind = "ranged";
            Assert.Throws<ArgumentException>(() => CombatCast.Create(null, null, data, weapon, true, definitionVersion: other.ContentHash, timeline: new MeleeActionDefinition(other, 1, .65f, .16f, .08f)));
            Assert.That(Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(count));
        }
        [UnityTest] public IEnumerator GameSecondHitMatchesRetainedRunnerAndComboRules() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabSecondHitMatchesRetainedRunnerAndComboRules() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int speed = 0, bool air = false, int facing = 1)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var error), Is.True, error);
            for (int i = 0; i < speed; i++) p.Weapon.StatSlots[i] = 3;
            p.Position = air ? new Vector2(0, 3) : Vector2.zero; p.grounded = !air; p.velocity = Vector2.zero; p.SyncFacing(facing);
            arena.enemies[0].Position = new Vector2(3 * facing, air ? 3 : 0); Physics2D.SyncTransforms();
        }
        static CombatCast StartSecond(CombatArena arena, bool migrated = true, float startAt = 0)
        {
            typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena.Player, new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), 1, startAt });
            var cast = arena.Player.currentCast;
            Assert.That(cast.DefinitionVersion, Is.EqualTo(arena.BasicSwordSecond.ContentHash)); Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Preparing)); Assert.That(cast.FrameTiming, Is.Null);
            if (!migrated)
            {
                // The same actual compiler payload with the retained legacy runner;
                // never execute both implementations for one cast.
                var data = SkillExecutionDefinitionIntegrationTests.BasicSource(arena, 1); float started = cast.StartedAt; string version = cast.DefinitionVersion;
                cast.Cancel(); arena.casts.Remove(cast); Object.Destroy(cast.gameObject);
                cast = CombatCast.Create(arena, arena.Player, data, arena.Player.Weapon, true, started, version); arena.Player.currentCast = cast;
                Assert.That(cast.ActionTimelineVersion, Is.Null);
            }
            return cast;
        }
        static void UntilActionChanges(CombatArena arena, CombatArena.ManualSimulation driver, CombatCast cast)
        {
            for (int tick = 0; tick < 100 && ReferenceEquals(arena.Player.currentCast, cast); tick++) driver.Step(.02f);
            Assert.That(ReferenceEquals(arena.Player.currentCast, cast), Is.False, "Action completion must progress within the bounded fixture.");
        }
        static Trace Run(CombatArena arena, CombatArena.ManualSimulation driver, bool migrated, int speed, bool air, bool queued, int cancel, bool jump, float start, int facing)
        {
            Setup(arena, driver, speed, air, facing); var first = StartSecond(arena, migrated, start); var trace = new Trace();
            for (int tick = 0; tick < 60; tick++)
            {
                var buttons = queued && tick == 2 ? CombatButtons.Basic : CombatButtons.None;
                if (tick == cancel) buttons |= jump ? CombatButtons.Jump : CombatButtons.Dash;
                driver.Step(.02f, new CombatInput(facing, buttons)); var p = arena.Player;
                trace.Positions.Add(p.Position); trace.Health.Add(arena.enemies[0].health); trace.Hits.Add(arena.enemies[0].ReceivedHitCount);
                trace.Actions.Add(p.currentCast ? p.currentCast.Definition.Id : ""); trace.Starts.Add(p.currentCast ? p.currentCast.StartedAt : -1);
            }
            if (migrated) Assert.That(first.ActionTimelineState, Is.EqualTo(cancel < 0 ? MeleeActionState.Completed : MeleeActionState.Cancelled));
            return trace;
        }
        static void Same(Trace legacy, Trace migrated)
        {
            Assert.That(migrated.Health, Is.EqualTo(legacy.Health)); Assert.That(migrated.Hits, Is.EqualTo(legacy.Hits)); Assert.That(migrated.Actions, Is.EqualTo(legacy.Actions)); Assert.That(migrated.Starts, Is.EqualTo(legacy.Starts));
            for (int i = 0; i < legacy.Positions.Count; i++) Assert.That(Vector2.Distance(legacy.Positions[i], migrated.Positions[i]), Is.LessThan(.000001f), "Movement tick " + i);
        }
        static void ActualCombo(CombatArena arena, CombatArena.ManualSimulation driver, int speed)
        {
            Setup(arena, driver, speed); CombatCast first = null, second = null; int firstHit = -1, secondHit = -1;
            for (int tick = 0; tick < 60; tick++)
            {
                driver.Step(.02f, new CombatInput(0, tick == 0 || tick == 2 ? CombatButtons.Basic : CombatButtons.None));
                var cast = arena.Player.currentCast;
                if (cast && cast.Definition.Id == "base_sword_basic_0") first = cast;
                if (cast && cast.Definition.Id == "base_sword_basic_1") second = cast;
                int hits = arena.enemies[0].ReceivedHitCount;
                if (hits > 0 && firstHit < 0) firstHit = tick;
                if (hits > 1 && secondHit < 0) secondHit = tick;
            }
            Assert.That(first, Is.Not.Null); Assert.That(second, Is.Not.Null); Assert.That(second.ExecutionId, Is.Not.EqualTo(first.ExecutionId));
            Assert.That(second.StartedAt, Is.EqualTo(first.EndTime)); Assert.That(second.ActionTimelineState, Is.EqualTo(MeleeActionState.Completed));
            Assert.That(firstHit, Is.GreaterThanOrEqualTo(0)); Assert.That(secondHit, Is.GreaterThan(firstHit)); Assert.That(arena.enemies[0].health, Is.EqualTo(74)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2));
            Assert.That(second.MotionDuration, Is.EqualTo(.37f / arena.Player.Weapon.Speed)); Assert.That(second.ImpactTime, Is.EqualTo(.13f / arena.Player.Weapon.Speed));
            Assert.That(arena.Player.currentCast, Is.Null, "No new press means no automatic third attack.");
        }
        static void LateAndReservedInputs(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (int speed in new[] { 0, 5 }) foreach (bool late in new[] { false, true })
            {
                Setup(arena, driver, speed); driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var first = arena.Player.currentCast;
                float resumeAt = first.EndTime + (late ? .32f : .28f);
                while (arena.Now < resumeAt) driver.Step(.02f);
                Assert.That(arena.Player.currentCast, Is.Null); driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                Assert.That(arena.Player.currentCast.Definition.Id, Is.EqualTo(late ? "base_sword_basic_0" : "base_sword_basic_1"), "The .30s grace is not scaled by basic speed.");
            }
            Setup(arena, driver); var second = StartSecond(arena);
            driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
            UntilActionChanges(arena, driver, second);
            Assert.That(arena.Player.currentCast.isBasic, Is.False); Assert.That(arena.Player.currentCast.StartedAt, Is.EqualTo(second.EndTime));
            Setup(arena, driver); second = StartSecond(arena);
            driver.Step(.02f, new CombatInput(0, CombatButtons.Basic | CombatButtons.Special | CombatButtons.Swap));
            UntilActionChanges(arena, driver, second);
            Assert.That(arena.Player.selected, Is.EqualTo(1)); Assert.That(arena.Player.currentCast, Is.Null);
        }
        static void FrameFirstDoesNotBecomeTheSecondHit(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver); arena.characterAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            Assert.That(arena.TryGetBasicSwordMotion(out var motion, out var error), Is.True, error);
            var projected = MeleeFrameCompiler.ProjectFirst(arena.BasicSwordFirst, motion, 2.5f); var frames = new TimelineFrame[projected.Frames.Count];
            for (int i = 0; i < frames.Length; i++) frames[i] = new TimelineFrame(projected.Frames[i].Id, projected.Frames[i].Seconds + (i == 0 ? .03f : 0));
            var frame = MeleeFrameCompiler.CompileFirst(arena.BasicSwordFirst, motion, new FrameTimeline(frames, projected.Markers), 2.5f);
            Assert.That(arena.TryApplySwordFrameTiming(frame, false, out error), Is.True, error);
            driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var first = arena.Player.currentCast;
            driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
            UntilActionChanges(arena, driver, first);
            var second = arena.Player.currentCast;
            Assert.That(first.FrameTiming, Is.SameAs(frame)); Assert.That(second.Definition.Id, Is.EqualTo("base_sword_basic_1")); Assert.That(second.FrameTiming, Is.Null);
            Assert.That(second.StartedAt, Is.EqualTo(first.EndTime)); Assert.That(second.ImpactTime, Is.EqualTo(.13f)); Assert.That(second.MotionDuration, Is.EqualTo(.37f));
            Assert.That(second.DefinitionVersion, Is.EqualTo(arena.BasicSwordSecond.ContentHash)); Assert.That(second.ActionTimelineVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(arena.TryRestoreSwordFixedTiming(true, out error), Is.True, error);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player;
            var floor = new GameObject("SwordComboFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    foreach (var s in new[] {
                        (speed:0, air:false, queued:false, cancel:-1, jump:false, start:0f, facing:1),
                        (speed:5, air:false, queued:false, cancel:-1, jump:false, start:0f, facing:1),
                        (speed:0, air:true, queued:false, cancel:-1, jump:false, start:0f, facing:1),
                        (speed:0, air:false, queued:true, cancel:-1, jump:false, start:.015f, facing:-1),
                        (speed:5, air:false, queued:true, cancel:-1, jump:false, start:.015f, facing:1),
                        (speed:0, air:false, queued:true, cancel:2, jump:true, start:0f, facing:1),
                        (speed:0, air:false, queued:true, cancel:11, jump:false, start:0f, facing:1)
                    }) Same(Run(arena, driver, false, s.speed, s.air, s.queued, s.cancel, s.jump, s.start, s.facing), Run(arena, driver, true, s.speed, s.air, s.queued, s.cancel, s.jump, s.start, s.facing));
                    ActualCombo(arena, driver, 0); ActualCombo(arena, driver, 5); LateAndReservedInputs(arena, driver); FrameFirstDoesNotBecomeTheSecondHit(arena, driver);

                    Setup(arena, driver); var cast = StartSecond(arena); var input = SkillExecutionDefinitionIntegrationTests.BasicSource(arena, 1);
                    cast = SkillExecutionDefinitionIntegrationTests.ReplaceBasic(arena, cast, input, true); string version = cast.ActionTimelineVersion;
                    input.activation = input.end = input.hits[0].time = 99; input.hits[0].active = 99; input.hits[0].damage = 999; input.hits[0].box.width = 999; input.basicLungeDistance = 999;
                    p.Weapon.StatSlots[0] = 1; p.Weapon.StatSlots[1] = 3; // Damage is activation-time; start speed stays fixed.
                    for (int tick = 0; tick < 40; tick++) driver.Step(.02f, new CombatInput(p.currentCast ? 1 : 0));
                    Assert.That(cast.MotionDuration, Is.EqualTo(.37f)); Assert.That(cast.ImpactTime, Is.EqualTo(.13f)); Assert.That(cast.EndTime, Is.EqualTo(.37f));
                    Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Completed)); Assert.That(cast.ActionTimelineVersion, Is.EqualTo(version));
                    Assert.That(arena.enemies[0].health, Is.EqualTo(84.4f).Within(.00001f)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(p.Position.x, Is.EqualTo(.65f).Within(.00001f));

                    Setup(arena, driver); var source = p.Form.basics[1]; p.Form.basics[1] = null;
                    try { cast = StartSecond(arena); Assert.That(cast.MotionDuration, Is.EqualTo(.37f)); Assert.That(cast.ImpactTime, Is.EqualTo(.13f)); }
                    finally { p.Form.basics[1] = source; }
                    Setup(arena, driver); cast = StartSecond(arena); driver.Reset(); Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Cancelled)); Assert.That(cast.ResourcesActive, Is.False);
                    cast = StartSecond(arena); p.health = 0; p.Die(); Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Cancelled)); Assert.That(cast.ResourcesActive, Is.False); Assert.That(arena.casts, Is.Empty);
                }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
