using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public class MeleeBasicFormsIntegrationTests
    {
        sealed class Trace
        {
            public readonly List<Vector2> Positions = new List<Vector2>();
            public readonly List<float> Health = new List<float>();
            public readonly List<int> Hits = new List<int>();
            public readonly List<string> Actions = new List<string>();
            public readonly List<float> Starts = new List<float>();
            public float Duration, Started;
        }
        [UnityTest] public IEnumerator GameAllMeleeBasicsMatchRetainedRunnerAndKeepStartDefinitions() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabAllMeleeBasicsMatchRetainedRunnerAndKeepStartDefinitions() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form, int speed = 0, bool air = false, int facing = 1)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), 0, out var error), Is.True, error);
            for (int i = 0; i < speed; i++) p.Weapon.StatSlots[i] = 3;
            p.Position = air ? new Vector2(0, 3) : Vector2.zero; p.grounded = !air; p.velocity = Vector2.zero; p.SyncFacing(facing);
            arena.enemies[0].Position = new Vector2(2 * facing, air ? 3 : 0); Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, int hit, bool migrated = true, float startAt = 0)
        {
            typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena.Player, new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), hit, startAt });
            var cast = arena.Player.currentCast;
            Assert.That(cast.DefinitionVersion, Is.EqualTo(arena.GetBasicMelee(arena.Player.Form.id, hit).ContentHash)); Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Preparing));
            if (!migrated)
            {
                // Retained runner, identical actual compiler payload. Each cast
                // runs one implementation; the next queued action is shared.
                var data = SkillExecutionDefinitionIntegrationTests.BasicSource(arena, hit); float started = cast.StartedAt; string version = cast.DefinitionVersion;
                cast.Cancel(); arena.casts.Remove(cast); Object.Destroy(cast.gameObject);
                cast = CombatCast.Create(arena, arena.Player, data, arena.Player.Weapon, true, started, version); arena.Player.currentCast = cast;
                Assert.That(cast.ActionTimelineVersion, Is.Null);
            }
            return cast;
        }
        static Trace Run(CombatArena arena, CombatArena.ManualSimulation driver, string form, int hit, bool migrated, int speed, bool air, bool queued, int cancel, bool jump, float start, int facing)
        {
            Setup(arena, driver, form, speed, air, facing); var first = Start(arena, hit, migrated, start); var trace = new Trace { Duration = first.MotionDuration, Started = first.StartedAt };
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
        static void Same(Trace old, Trace current, string context)
        {
            context += " duration " + old.Duration.ToString("R") + "/" + current.Duration.ToString("R") + " start " + old.Started.ToString("R") + "/" + current.Started.ToString("R");
            Assert.That(current.Health, Is.EqualTo(old.Health), context + " damage"); Assert.That(current.Hits, Is.EqualTo(old.Hits), context + " hits");
            Assert.That(current.Actions, Is.EqualTo(old.Actions), context + " action"); Assert.That(current.Starts, Is.EqualTo(old.Starts), context + " starts");
            for (int i = 0; i < old.Positions.Count; i++) Assert.That(Vector2.Distance(current.Positions[i], old.Positions[i]), Is.LessThan(.000001f), context + " movement tick " + i);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player;
            var floor = new GameObject("MeleeFormsFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Assert.That(arena.BasicMeleeCount, Is.EqualTo(17)); int tested = 0;
                    foreach (var form in arena.catalog.forms.Where(f => f.basics.Any(b => string.IsNullOrEmpty(b.kind))))
                    for (int hit = 0; hit < form.basics.Length; hit++)
                    {
                        tested++; var source = form.basics[hit]; var definition = arena.GetBasicMelee(form.id, hit);
                        foreach (var s in new[] {
                            (speed:0,air:false,queued:false,cancel:-1,jump:false,start:0f,facing:1),
                            (speed:5,air:false,queued:false,cancel:-1,jump:false,start:0f,facing:1),
                            (speed:0,air:true,queued:false,cancel:-1,jump:false,start:0f,facing:1),
                            (speed:0,air:false,queued:true,cancel:-1,jump:false,start:.015f,facing:-1),
                            (speed:5,air:false,queued:true,cancel:-1,jump:false,start:.015f,facing:1),
                            (speed:0,air:false,queued:true,cancel:0,jump:true,start:0f,facing:1),
                            (speed:0,air:false,queued:true,cancel:(int)(source.impact/.02f)+1,jump:false,start:0f,facing:1)
                        }) Same(Run(arena, driver, form.id, hit, false, s.speed, s.air, s.queued, s.cancel, s.jump, s.start, s.facing), Run(arena, driver, form.id, hit, true, s.speed, s.air, s.queued, s.cancel, s.jump, s.start, s.facing), form.id + "/" + hit + " " + s);

                        Setup(arena, driver, form.id); var cast = Start(arena, hit); var input = SkillExecutionDefinitionIntegrationTests.BasicSource(arena, hit);
                        cast = SkillExecutionDefinitionIntegrationTests.ReplaceBasic(arena, cast, input, true); string version = cast.ActionTimelineVersion;
                        input.activation = input.end = input.hits[0].time = 99; input.hits[0].active = 99; input.hits[0].damage = 999; input.hits[0].box.width = 999; input.basicLungeDistance = 999;
                        p.Weapon.StatSlots[0] = 1; p.Weapon.StatSlots[1] = 3;
                        for (int tick = 0; tick < 60; tick++) driver.Step(.02f, new CombatInput(p.currentCast ? 1 : 0));
                        Assert.That(cast.MotionDuration, Is.EqualTo(definition.Duration)); Assert.That(cast.ImpactTime, Is.EqualTo(definition.Impact)); Assert.That(cast.ActionTimelineVersion, Is.EqualTo(version));
                        Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Completed)); Assert.That(arena.enemies[0].health, Is.EqualTo(100 - definition.Damage * 1.2f).Within(.00001f)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                        Assert.That(p.Position.x, Is.EqualTo(form.inputSpeed > 0 ? .65f : 0).Within(.00001f));

                        Setup(arena, driver, form.id); form.basics[hit] = null;
                        try { cast = Start(arena, hit); Assert.That(cast.MotionDuration, Is.EqualTo(definition.Duration)); }
                        finally { form.basics[hit] = source; }
                        driver.Reset(); Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Cancelled)); Assert.That(cast.ResourcesActive, Is.False);
                    }
                    Assert.That(tested, Is.EqualTo(17));
                    // A delegated first-hit candidate does not retime other actions.
                    Setup(arena, driver, "base_sword"); arena.characterAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
                    Assert.That(arena.TryGetBasicSwordMotion(out var motion, out var error), Is.True, error);
                    float inputSpeed = arena.catalog.forms.Single(form => form.id == "base_sword").inputSpeed;
                    var frame = Deviant.Definitions.MeleeFrameCompiler.CompileFirst(arena.BasicSwordFirst, motion, Deviant.Definitions.MeleeFrameCompiler.ProjectFirst(arena.BasicSwordFirst, motion, inputSpeed), inputSpeed);
                    Assert.That(arena.TryApplySwordFrameTiming(frame, false, out error), Is.True, error);
                    Setup(arena, driver, "dagger"); var dagger = Start(arena, 0); Assert.That(dagger.FrameTiming, Is.Null); Assert.That(dagger.DefinitionVersion, Is.EqualTo(arena.GetBasicMelee("dagger", 0).ContentHash));
                    Assert.That(arena.TryRestoreSwordFixedTiming(true, out error), Is.True, error);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
