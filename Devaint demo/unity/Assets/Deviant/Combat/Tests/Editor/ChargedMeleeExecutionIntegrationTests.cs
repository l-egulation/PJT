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
    public class ChargedMeleeExecutionIntegrationTests
    {
        const int Ticks = 120;
        static readonly string[] Scenarios = { "minimum", "middle", "maximum", "variable-dt", "target-loss-before", "cancel-before", "cancel-after", "reset-before", "reset-after", "resource-rejected", "resource-failed", "activation-stats", "target-loss-after", "target-reentry", "all-in-range-at-release" };
        [Serializable] sealed class Row
        {
            public string scenario, phase, action;
            public int tick, targetHits, otherHits, available, resourceStatus, casts;
            public bool current, chargeActive, resourcesActive, released, dashing, faulted, sameExecution, sameWeapon;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        sealed class Source : IActionResourceSource
        {
            readonly IActionResourceSource inner;
            readonly string scenario;
            public Action Committed;
            public Source(WeaponState weapon, string scenario) { inner = new WeaponActionResourceSource(weapon); this.scenario = scenario; }
            public ActionResourceResult TryActivate(ActionResourceKey key, float now)
            {
                if (scenario == "resource-rejected") return new ActionResourceResult(ActionResourceStatus.Rejected, reason: "fixture admission rejected");
                if (scenario == "resource-failed") throw new InvalidOperationException("fixture admission failed");
                var result = inner.TryActivate(key, now); Committed?.Invoke(); return result;
            }
        }
        [UnityTest] public IEnumerator GameChargedLifecyclePreservesReleaseHitRecoveryCancellationAndResources() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabChargedLifecyclePreservesReleaseHitRecoveryCancellationAndResources() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); enemy.velocity = Vector2.zero; }
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, item => item.id == form), 0, out var error), Is.True, error);
            player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
        }
        static void Record(Trace trace, string form, string scenario, string phase, int tick, float step, CombatArena arena, CombatCast cast, WeaponState weapon)
        {
            var player = arena.Player; var target = arena.enemies[0]; var other = arena.enemies[1];
            var release = cast.ChargeRelease; var receipt = cast.ResourceResult; var snapshot = receipt?.Snapshot;
            trace.rows.Add(new Row { scenario = form + "/" + scenario, phase = phase, tick = tick,
                action = player.currentCast ? player.currentCast.Definition.Id : "", current = ReferenceEquals(player.currentCast, cast),
                targetHits = target.ReceivedHitCount, otherHits = other.ReceivedHitCount, available = weapon.Cooldown.Available,
                resourceStatus = receipt == null ? -1 : (int)receipt.Status, chargeActive = cast.ChargeActive, resourcesActive = cast.ResourcesActive,
                released = release.HasValue, dashing = player.Dashing, faulted = arena.SimulationFaulted,
                sameExecution = snapshot == null || snapshot.Key.ExecutionId == cast.ExecutionId,
                sameWeapon = snapshot == null || snapshot.Key.WeaponId == weapon.InstanceId, casts = arena.casts.Count,
                bits = new[] { Bits(arena.Now), Bits(step), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y),
                    Bits(player.health), Bits(target.health), Bits(target.Position.x), Bits(target.Position.y), Bits(other.health), Bits(other.Position.x), Bits(other.Position.y),
                    Bits(cast.StartedAt), Bits(cast.Age), Bits(cast.ImpactTime), Bits(cast.MotionDuration), Bits(cast.EndTime),
                    Bits(release?.ReleaseTime ?? -1), Bits(release?.Shift ?? -1), Bits(release?.MotionEnd ?? -1), Bits(release?.Damage ?? -1),
                    Bits(snapshot?.ActivatedAt ?? -1), Bits(snapshot?.DamageMultiplier ?? -1), Bits(weapon.Cooldown.NextReady), Bits(weapon.Cooldown.Debt(arena.Now)),
                    Bits(cast.HorizontalVelocity(1)), Bits(player.DashRemaining) } });
        }
        static float StepFor(string scenario, int tick)
        {
            if (scenario != "variable-dt") return .02f;
            switch (tick % 5) { case 0: return .007f; case 1: return .031f; case 2: return .011f; case 3: return .083f; default: return .019f; }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var trace = new Trace();
            var floor = new GameObject("ChargedExecutionFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (string form in new[] { "rapier", "greatsword" })
                foreach (string scenario in Scenarios)
                {
                    Setup(arena, driver, form); var definition = arena.GetChargedMelee(form + "_default"); var policy = definition.Policy;
                    var target = arena.enemies[0]; var other = arena.enemies[1]; var weapon = player.Weapon;
                    bool manual = scenario.StartsWith("resource-", StringComparison.Ordinal) || scenario.StartsWith("target-", StringComparison.Ordinal) && scenario != "target-loss-before";
                    CombatCast cast = null; bool command = false, statsChanged = false, returned = false, failureObserved = false;
                    float failureEnd = -1, committedAt = -1, heldUntil = scenario == "middle" || scenario == "activation-stats" ? (policy.Minimum + policy.Maximum) * .5f : scenario == "maximum" ? float.PositiveInfinity : 0;
                    if (manual)
                    {
                        var source = new Source(weapon, scenario);
                        if (scenario == "target-loss-after" || scenario == "target-reentry")
                        {
                            source.Committed = () => {
                                committedAt = arena.Now; target.Position = new Vector2(35, 0); other.Position = new Vector2(2, 0); Physics2D.SyncTransforms();
                            };
                        }
                        cast = CombatCast.Create(arena, player, definition.ToData(), weapon, false, definitionVersion: definition.ContentHash, resourceSource: source, chargedDefinition: definition);
                        player.currentCast = cast;
                    }
                    for (int tick = 0; tick < Ticks; tick++)
                    {
                        float step = StepFor(scenario, tick); var buttons = tick == 0 && !manual ? CombatButtons.Special : CombatButtons.None;
                        if (!command && tick > 0)
                        {
                            if (scenario == "target-loss-before" && tick == 1)
                            { target.Position = new Vector2(35, 0); command = true; Physics2D.SyncTransforms(); Record(trace, form, scenario, "target-left", tick, step, arena, cast, weapon); }
                            if (scenario == "all-in-range-at-release" && tick == 1)
                            { target.Position = new Vector2(2.8f, 0); other.Position = new Vector2(1.6f, 0); command = true; Physics2D.SyncTransforms(); Record(trace, form, scenario, "nearest-changed", tick, step, arena, cast, weapon); }
                            if ((scenario == "cancel-before" || scenario == "reset-before") && tick == 2 ||
                                (scenario == "cancel-after" || scenario == "reset-after") && cast.ResourceResult?.Status == ActionResourceStatus.Succeeded)
                            {
                                command = true;
                                if (scenario.StartsWith("cancel-", StringComparison.Ordinal)) buttons |= CombatButtons.Dash;
                                else
                                {
                                    Record(trace, form, scenario, "before-reset", tick, step, arena, cast, weapon); driver.Reset();
                                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); enemy.velocity = Vector2.zero; }
                                    player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; Physics2D.SyncTransforms();
                                    Record(trace, form, scenario, "after-reset", tick, step, arena, cast, weapon);
                                }
                            }
                        }
                        if (scenario == "activation-stats" && tick == 2) weapon.StatSlots[0] = 2;
                        if (scenario == "activation-stats" && !statsChanged && cast?.ResourceResult?.Status == ActionResourceStatus.Succeeded)
                        { statsChanged = true; weapon.StatSlots.Clear(); for (int i = 0; i < weapon.StatSlots.Length; i++) weapon.StatSlots[i] = 2; }
                        if (scenario == "target-reentry" && !returned && committedAt >= 0 && arena.Now > committedAt)
                        { returned = true; target.Position = new Vector2(2, 0); Physics2D.SyncTransforms(); Record(trace, form, scenario, "target-returned", tick, step, arena, cast, weapon); }
                        bool held = arena.Now < heldUntil; float beforeStep = arena.Now;
                        driver.Step(step, new CombatInput(scenario.StartsWith("cancel-", StringComparison.Ordinal) ? 1 : 0, buttons, held));
                        if (tick == 0 && !manual)
                        {
                            cast = player.currentCast; Assert.That(cast, Is.Not.Null, form + "/" + scenario);
                            Assert.That(cast.ChargedMelee, Is.SameAs(definition));
                        }
                        Record(trace, form, scenario, "step", tick, step, arena, cast, weapon);
                        bool rejected = scenario == "resource-rejected" || scenario == "resource-failed";
                        if (rejected && !failureObserved && cast.ChargeRelease.HasValue)
                        {
                            failureObserved = true; failureEnd = cast.EndTime;
                            Assert.That(ReferenceEquals(player.currentCast, cast), Is.True, "Failure keeps the cast current for its existing .10s recovery.");
                            Assert.That(Bits(failureEnd), Is.EqualTo(Bits(cast.StartedAt + (beforeStep - cast.StartedAt + .1f))));
                            Assert.That(cast.ResourcesActive || cast.ChargeActive, Is.False); Assert.That(weapon.Cooldown.Available, Is.EqualTo(1));
                            Assert.That(cast.HorizontalVelocity(1), Is.EqualTo(arena.Movement.RunSpeed), "Failure releases horizontal input using the current movement profile.");
                        }
                    }
                    Assert.That(arena.SimulationFaulted, Is.False, form + "/" + scenario); Assert.That(player.currentCast, Is.Null);
                    Assert.That(cast.ResourcesActive || cast.ChargeActive, Is.False); Assert.That(other.ReceivedHitCount, Is.EqualTo(scenario == "all-in-range-at-release" ? 1 : 0));
                    if (scenario == "cancel-before" || scenario == "reset-before")
                    { Assert.That(command, Is.True); Assert.That(cast.ChargeRelease, Is.Null); Assert.That(cast.ResourceResult, Is.Null); Assert.That(weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(target.ReceivedHitCount, Is.Zero); }
                    else if (scenario == "reset-after")
                    { Assert.That(command, Is.True); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(cast.ChargeRelease.HasValue, Is.True); Assert.That(weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(target.ReceivedHitCount, Is.Zero); }
                    else if (scenario == "resource-rejected" || scenario == "resource-failed")
                    {
                        Assert.That(failureObserved, Is.True); Assert.That(Bits(cast.EndTime), Is.EqualTo(Bits(failureEnd))); Assert.That(target.ReceivedHitCount, Is.Zero);
                        Assert.That(cast.ResourceResult.Status, Is.EqualTo(scenario == "resource-rejected" ? ActionResourceStatus.Rejected : ActionResourceStatus.Failed));
                    }
                    else
                    {
                        var receipt = cast.ResourceResult; Assert.That(receipt.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
                        Assert.That(receipt.Snapshot.Key.IsBasic, Is.False); Assert.That(receipt.Snapshot.Key.ExecutionId, Is.EqualTo(cast.ExecutionId));
                        Assert.That(receipt.Snapshot.Key.WeaponId, Is.EqualTo(weapon.InstanceId)); Assert.That(weapon.Cooldown.Available, Is.Zero);
                        // A target that left before release is a whiff: the swing spends the charge and hits nothing.
                        Assert.That(target.ReceivedHitCount, Is.EqualTo(scenario == "target-loss-after" || scenario == "target-loss-before" ? 0 : 1));
                        if (scenario == "target-loss-after" || scenario == "target-loss-before") Assert.That(target.health, Is.EqualTo(100));
                        else if (scenario == "all-in-range-at-release") Assert.That(other.health, Is.EqualTo(60 - cast.ChargeRelease.Value.Damage).Within(.00001f));
                        else Assert.That(target.health, Is.EqualTo(100 - cast.ChargeRelease.Value.Damage * (scenario == "activation-stats" ? 1.2f : 1)).Within(.00001f));
                        if (scenario == "minimum") { Assert.That(cast.ImpactTime, Is.EqualTo(policy.Minimum)); Assert.That(cast.ChargeRelease.Value.Damage, Is.EqualTo(policy.MinimumDamage)); }
                        if (scenario == "maximum") { Assert.That(cast.ImpactTime, Is.EqualTo(policy.Maximum)); Assert.That(cast.ChargeRelease.Value.Damage, Is.EqualTo(policy.MaximumDamage)); }
                        if (scenario == "middle" || scenario == "activation-stats") Assert.That(cast.ChargeRelease.Value.Damage, Is.GreaterThan(policy.MinimumDamage).And.LessThan(policy.MaximumDamage));
                        if (scenario == "target-reentry") Assert.That(returned, Is.True);
                        if (scenario == "cancel-after") Assert.That(command, Is.True);
                        if (scenario == "activation-stats") { Assert.That(statsChanged, Is.True); Assert.That(receipt.Snapshot.DamageMultiplier, Is.EqualTo(1.2f)); }
                    }
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-chargedExecutionTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
