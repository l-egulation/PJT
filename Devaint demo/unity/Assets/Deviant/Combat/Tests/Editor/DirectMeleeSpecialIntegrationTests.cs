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
    public class DirectMeleeSpecialIntegrationTests
    {
        sealed class Trace
        {
            public readonly List<Vector2> Positions = new List<Vector2>(); public readonly List<float> Health = new List<float>();
            public readonly List<int> Hits = new List<int>(), Charges = new List<int>(), Facings = new List<int>();
            public readonly List<float> Ready = new List<float>(), Starts = new List<float>(); public readonly List<string> Actions = new List<string>();
        }
        [UnityTest] public IEnumerator GameDirectSpecialsMatchRetainedExecutionAndKeepTheirStartDefinition() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabDirectSpecialsMatchRetainedExecutionAndKeepTheirStartDefinition() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form, int slot, int speed = 0, bool air = false, int facing = 1)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear(); Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), slot, out var error), Is.True, error);
            for (int i = 0; i < speed; i++) p.Weapon.StatSlots[i] = 3;
            p.Position = air ? new Vector2(0, 3) : Vector2.zero; p.grounded = !air; p.velocity = Vector2.zero; p.SyncFacing(facing);
            arena.enemies[0].Position = new Vector2(2 * facing, air ? 3 : 0); Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, bool migrated = true, float startAt = 0)
        {
            typeof(CombatPlayer).GetMethod("BeginSpecial", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena.Player, new object[] { default(Deviant.Combat.Contracts.CombatCommandKey), startAt });
            var cast = arena.Player.currentCast; Assert.That(cast, Is.Not.Null); Assert.That(cast.isBasic, Is.False); Assert.That(cast.DirectMeleeSpecial, Is.Not.Null); Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Preparing));
            if (!migrated)
            {
                var payload = cast.DirectMeleeSpecial.ToData(); string version = cast.DefinitionVersion; float started = cast.StartedAt;
                cast.Cancel(); arena.casts.Remove(cast); Object.Destroy(cast.gameObject);
                cast = CombatCast.Create(arena, arena.Player, payload, arena.Player.Weapon, false, started, version); arena.Player.currentCast = cast; Assert.That(cast.ActionTimelineVersion, Is.Null);
            }
            return cast;
        }
        static Trace Run(CombatArena arena, CombatArena.ManualSimulation driver, string form, int slot, bool migrated, int speed, bool air, int facing, float start, bool queued, int cancel)
        {
            Setup(arena, driver, form, slot, speed, air, facing); var cast = Start(arena, migrated, start); var trace = new Trace();
            for (int tick = 0; tick < 100; tick++)
            {
                var buttons = queued && tick == 2 ? CombatButtons.Basic | CombatButtons.Special | CombatButtons.Swap : CombatButtons.None;
                if (tick == cancel) buttons |= CombatButtons.Dash;
                driver.Step(.02f, new CombatInput(air ? 0 : tick < 4 ? facing : -facing, buttons)); var p = arena.Player;
                trace.Positions.Add(p.Position); trace.Health.Add(arena.enemies[0].health); trace.Hits.Add(arena.enemies[0].ReceivedHitCount); trace.Charges.Add(p.weapons[0].Cooldown.Available); trace.Ready.Add(p.weapons[0].Cooldown.NextReady); trace.Facings.Add(p.facing);
                trace.Actions.Add(p.currentCast ? p.currentCast.Definition.Id : ""); trace.Starts.Add(p.currentCast ? p.currentCast.StartedAt : -1);
            }
            if (migrated) Assert.That(cast.ActionTimelineState, Is.EqualTo(cancel < 0 ? MeleeActionState.Completed : MeleeActionState.Cancelled));
            return trace;
        }
        static void Same(Trace legacy, Trace current, string context)
        {
            Assert.That(current.Health, Is.EqualTo(legacy.Health), context + " damage"); Assert.That(current.Hits, Is.EqualTo(legacy.Hits), context + " hits"); Assert.That(current.Charges, Is.EqualTo(legacy.Charges), context + " charges");
            Assert.That(current.Ready, Is.EqualTo(legacy.Ready), context + " recovery"); Assert.That(current.Actions, Is.EqualTo(legacy.Actions), context + " actions"); Assert.That(current.Starts, Is.EqualTo(legacy.Starts), context + " starts"); Assert.That(current.Facings, Is.EqualTo(legacy.Facings), context + " facing");
            for (int i = 0; i < legacy.Positions.Count; i++) Assert.That(Vector2.Distance(current.Positions[i], legacy.Positions[i]), Is.LessThan(.000001f), context + " position tick " + i);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player;
            var floor = new GameObject("DirectSpecialFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Assert.That(arena.DirectMeleeSpecialCount, Is.EqualTo(3));
                    foreach (var action in new[] { (form:"base_sword",slot:0,id:"base_sword_default"), (form:"rapier",slot:1,id:"rapier_range"), (form:"greatsword",slot:1,id:"greatsword_range") })
                    {
                        var definition = arena.GetDirectMeleeSpecial(action.id);
                        foreach (var s in new[] {
                            (speed:0,air:false,facing:1,start:0f,queued:false,cancel:-1), (speed:5,air:false,facing:1,start:0f,queued:false,cancel:-1),
                            (speed:0,air:true,facing:1,start:0f,queued:false,cancel:-1), (speed:0,air:false,facing:-1,start:.015f,queued:false,cancel:-1),
                            (speed:0,air:false,facing:1,start:0f,queued:true,cancel:-1), (speed:0,air:false,facing:1,start:0f,queued:true,cancel:2),
                            (speed:0,air:false,facing:1,start:0f,queued:true,cancel:(int)(definition.Impact/.02f)+2)
                        }) Same(Run(arena, driver, action.form, action.slot, false, s.speed, s.air, s.facing, s.start, s.queued, s.cancel), Run(arena, driver, action.form, action.slot, true, s.speed, s.air, s.facing, s.start, s.queued, s.cancel), action.id + " " + s);

                        // The approved zero-input-speed special also blocks air
                        // input. The old host's generic air override violated it.
                        Setup(arena, driver, action.form, action.slot, air: true); var airborne = Start(arena);
                        for (int tick = 0; tick < 100 && ReferenceEquals(p.currentCast, airborne); tick++)
                        {
                            driver.Step(.02f, new CombatInput(1)); Assert.That(p.Position.x, Is.EqualTo(0).Within(.000001f));
                        }
                        Assert.That(airborne.ActionTimelineState, Is.EqualTo(MeleeActionState.Completed));
                        Setup(arena, driver, action.form, action.slot, 5); var input = definition.ToData(); var cast = CombatCast.Create(arena, p, input, p.Weapon, false, 0, definition.ContentHash, definition.Compile(), specialDefinition: definition); p.currentCast = cast;
                        Assert.That(cast.MotionDuration, Is.EqualTo(definition.Duration)); Assert.That(cast.ImpactTime, Is.EqualTo(definition.Impact));
                        var source = p.Form.specials[action.slot]; float oldEnd = source.end, oldDamage = source.hits[0].damage, oldWidth = source.hits[0].box.width;
                        try
                        {
                            source.end = 99; source.hits[0].damage = 999; source.hits[0].box.width = 999;
                            input.activation = input.end = input.hits[0].time = 99; input.hits[0].damage = 999; input.hits[0].box.width = 999; input.vertical = "grounded_required"; input.invulStart = new[] { 0f }; input.invulEnd = new[] { 99f };
                            p.Weapon.StatSlots[0] = 2;
                            for (int tick = 0; tick < 80; tick++) driver.Step(.02f);
                            Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Completed)); Assert.That(cast.EndTime, Is.EqualTo(definition.Duration)); Assert.That(cast.Invulnerable, Is.False); Assert.That(cast.VerticalPolicy, Is.EqualTo("normal"));
                            Assert.That(arena.enemies[0].health, Is.EqualTo(100 - definition.Damage * 1.2f).Within(.00001f)); Assert.That(cast.ResourceResult.Snapshot.DamageMultiplier, Is.EqualTo(1.2f)); Assert.That(cast.ResourceResult.Snapshot.Key.IsBasic, Is.False); Assert.That(p.Weapon.Cooldown.Available, Is.Zero);
                        }
                        finally { source.end = oldEnd; source.hits[0].damage = oldDamage; source.hits[0].box.width = oldWidth; }
                        Setup(arena, driver, action.form, action.slot); cast = Start(arena); driver.Reset(); Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Cancelled)); Assert.That(cast.ResourcesActive, Is.False);
                        cast = Start(arena); p.health = 0; p.Die(); Assert.That(cast.ActionTimelineState, Is.EqualTo(MeleeActionState.Cancelled)); Assert.That(cast.ResourcesActive, Is.False);
                    }
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
