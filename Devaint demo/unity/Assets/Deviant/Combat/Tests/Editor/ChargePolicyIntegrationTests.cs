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
    public class ChargePolicyIntegrationTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, action; public int tick, now, x, y, vx, vy, hp, hits, available, ready, impact, end, started; public bool resource, basic, sameWeapon;
        }
        [Serializable] sealed class Trace { public Row[] rows; }
        [UnityTest] public IEnumerator GameChargeReleaseConsumptionAndCancellationKeepTheirObservedOrder() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabChargeReleaseConsumptionAndCancellationKeepTheirObservedOrder() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void AssertHealthAfterDamage(TestEnemy target,float nominal)
        {
            var variance=CombatDamageVariance.Settings;
            Assert.That(target.health,Is.InRange(Mathf.Max(0,100-nominal*variance.maximum)-.001f,Mathf.Max(0,100-nominal*variance.minimum)+.001f));
        }
        static IEnumerator Exercise(string scene, string traceName)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var rows = new List<Row>();
            var floor = new GameObject("ChargePolicyFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (string form in new[] { "rapier", "greatsword" })
                {
                    int index = Array.FindIndex(arena.catalog.forms, f => f.id == form); var source = arena.catalog.forms[index].specials[0];
                    foreach (var scenario in new[] { (name:"early",hold:0,speed:0,cancel:-1,swap:false), (name:"early-held",hold:2,speed:0,cancel:-1,swap:false),
                        (name:"middle",hold:(int)((source.chargeMin+source.chargeMax)*.5f/.02f),speed:0,cancel:-1,swap:false), (name:"maximum",hold:100,speed:0,cancel:-1,swap:false),
                        (name:"speed-maximum",hold:100,speed:5,cancel:-1,swap:false), (name:"cancel-before",hold:100,speed:0,cancel:2,swap:false),
                        (name:"cancel-after",hold:100,speed:0,cancel:(int)(source.chargeMax/.02f)+2,swap:false), (name:"queued-swap",hold:100,speed:0,cancel:-1,swap:true) })
                    {
                        driver.Reset(); p.selected = 0; p.InputBlocked = false;
                        foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                        foreach (var equipped in p.weapons) equipped.StatSlots.Clear(); Assert.That(p.TryReconfigure(0, index, 0, out var setupError), Is.True, setupError); for (int i = 0; i < scenario.speed; i++) p.Weapon.StatSlots[i] = 3;
                        p.Position = Vector2.zero; p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
                        var weapon = p.Weapon; var identity = weapon.InstanceId; CombatCast cast = null;
                        for (int tick = 0; tick < 100; tick++)
                        {
                            var buttons = tick == 0 ? CombatButtons.Special : CombatButtons.None;
                            if (tick == scenario.cancel) buttons |= CombatButtons.Dash;
                            if (tick == 2 && scenario.swap) buttons |= CombatButtons.Swap;
                            driver.Step(.02f, new CombatInput(0, buttons, tick < scenario.hold)); if (tick == 0) { cast = p.currentCast; Assert.That(cast, Is.Not.Null); }
                            var receipt = cast.ResourceResult;
                            rows.Add(new Row { scenario = form + "/" + scenario.name, tick = tick, now = Bits(arena.Now), x = Bits(p.Position.x), y = Bits(p.Position.y), vx = Bits(p.velocity.x), vy = Bits(p.velocity.y), hp = Bits(arena.enemies[0].health), hits = arena.enemies[0].ReceivedHitCount,
                                available = weapon.Cooldown.Available, ready = Bits(weapon.Cooldown.NextReady), impact = Bits(cast.ImpactTime), end = Bits(cast.EndTime), started = Bits(cast.StartedAt), action = p.currentCast ? p.currentCast.Definition.Id : "", resource = receipt != null, basic = receipt?.Snapshot != null && receipt.Snapshot.Key.IsBasic, sameWeapon = receipt?.Snapshot == null || receipt.Snapshot.Key.WeaponId == identity });
                        }
                        if (scenario.cancel == 2) { Assert.That(cast.ResourceResult, Is.Null); Assert.That(weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero); }
                        else
                        {
                            Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(cast.ResourceResult.Snapshot.Key.IsBasic, Is.False); Assert.That(cast.ResourceResult.Snapshot.Key.WeaponId, Is.EqualTo(identity));
                            Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(weapon.Cooldown.Available, Is.Zero);
                            if (scenario.hold <= 2) { Assert.That(cast.ImpactTime, Is.EqualTo(source.chargeMin)); Assert.That(cast.EndTime-cast.StartedAt, Is.EqualTo(source.end)); AssertHealthAfterDamage(arena.enemies[0],source.chargeMinDamage); }
                            if (scenario.hold == 100) { Assert.That(cast.ImpactTime, Is.EqualTo(source.chargeMax)); Assert.That(cast.EndTime-cast.StartedAt, Is.EqualTo(source.end+source.chargeMax-source.chargeMin).Within(.000001f)); AssertHealthAfterDamage(arena.enemies[0],source.chargeMaxDamage); }
                        }
                        Assert.That(cast.ChargePolicy, Is.Not.Null); Assert.That(cast.ChargeActive, Is.False);
                        if (scenario.cancel == 2) Assert.That(cast.ChargeRelease, Is.Null);
                        else { Assert.That(cast.ChargeRelease.HasValue, Is.True); Assert.That(cast.ChargeRelease.Value.ReleaseTime, Is.EqualTo(cast.ImpactTime)); }
                        if (scenario.swap) Assert.That(p.selected, Is.EqualTo(1)); Assert.That(arena.SimulationFaulted, Is.False);
                    }
                }
                // Edit the exact DTO input retained by this fixture, not a fresh
                // definition export that the started action never consumed.
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); p.selected = 0; foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                    foreach (var equipped in p.weapons) equipped.StatSlots.Clear(); Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "rapier"), 0, out var setupError), Is.True, setupError);
                    p.Position = Vector2.zero; p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
                    var captured = arena.GetChargedMelee("rapier_default"); var skill = captured.ToData();
                    var cast = CombatCast.Create(arena, p, skill, p.Weapon, false, definitionVersion: captured.ContentHash, chargedDefinition: captured); p.currentCast = cast;
                    var definition = cast.ChargePolicy;
                    float minimum = skill.chargeMin, maximum = skill.chargeMax, lowDamage = skill.chargeMinDamage, highDamage = skill.chargeMaxDamage, end = skill.end;
                    try
                    {
                        skill.chargeMin = 998; skill.chargeMax = 999; skill.chargeMinDamage = 1000; skill.chargeMaxDamage = 2000; skill.end = 99; p.Weapon.StatSlots[0] = 2;
                        for (int i = 0; i < 100; i++) driver.Step(.02f, new CombatInput(0, CombatButtons.None, true));
                        Assert.That(cast.ChargePolicy, Is.SameAs(definition)); Assert.That(cast.ChargeRelease.Value.ReleaseTime, Is.EqualTo(maximum)); Assert.That(cast.ChargeRelease.Value.Damage, Is.EqualTo(highDamage));
                        Assert.That(cast.MotionDuration, Is.EqualTo(end)); Assert.That(cast.EndTime-cast.StartedAt, Is.EqualTo(end+(maximum-minimum))); Assert.That(cast.ChargeActive, Is.False);
                        AssertHealthAfterDamage(arena.enemies[0],highDamage*1.2f); Assert.That(cast.ResourceResult.Snapshot.DamageMultiplier, Is.EqualTo(1.2f));
                    }
                    finally { skill.chargeMin = minimum; skill.chargeMax = maximum; skill.chargeMinDamage = lowDamage; skill.chargeMaxDamage = highDamage; skill.end = end; }
                    for (int formIndex = 0; formIndex < 2; formIndex++)
                    {
                        driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                        Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == (formIndex == 0 ? "rapier" : "greatsword")), 0, out var nextSetupError), Is.True, nextSetupError);
                        p.Position = new Vector2(0, 3); p.velocity = Vector2.zero; p.grounded = false; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 3); Physics2D.SyncTransforms();
                        driver.Step(.02f, new CombatInput(1, CombatButtons.Special, true)); cast = p.currentCast; Assert.That(cast, Is.Not.Null);
                        for (int tick = 0; tick < 100 && ReferenceEquals(p.currentCast, cast); tick++)
                        { driver.Step(.02f, new CombatInput(1, CombatButtons.None, true)); Assert.That(p.Position.x, Is.EqualTo(0).Within(.000001f), "Charged special input speed remains zero in air."); }
                        Assert.That(cast.ChargeRelease.HasValue, Is.True);
                    }
                    driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                    p.Position = Vector2.zero; p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special, true)); cast = p.currentCast; Assert.That(cast, Is.Not.Null); Assert.That(cast.ChargeActive, Is.True);
                    arena.RemoveOwnedEffects(p); Assert.That(cast.ChargeActive, Is.False); Assert.That(cast.ChargeRelease, Is.Null); Assert.That(cast.ResourcesActive, Is.False);
                }
                string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-chargePolicyTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { string directory = Path.GetFullPath(args[at + 1]); Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, traceName + ".json"), JsonUtility.ToJson(new Trace { rows = rows.ToArray() }, false)); }
                Assert.That(rows.Count, Is.EqualTo(1600));
            }
            finally { Object.Destroy(floor); }
        }
    }
}
