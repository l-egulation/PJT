using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // Frozen against the pre-extraction public Cast/Player API. Existing all-skill
    // and policy-specific traces remain the catalogue-wide consumer freeze.
    public class CastLifecycleNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string scenario, phase, action;
            public int hits, available, casts, shots, bombs, status, calls, slot;
            public bool current, listed, resources, charge, released, moving, frozen, invulnerable, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        sealed class Source : IActionResourceSource
        {
            readonly IActionResourceSource inner;
            public Action Committing;
            public bool Reject;
            public int Calls;
            public Source(WeaponState weapon) { inner = new WeaponActionResourceSource(weapon); }
            public ActionResourceResult TryActivate(ActionResourceKey key, float now)
            {
                Calls++; Committing?.Invoke();
                return Reject ? new ActionResourceResult(ActionResourceStatus.Rejected, reason: "lifecycle fixture rejected") : inner.TryActivate(key, now);
            }
        }
        [UnityTest] public IEnumerator GameCastLifecycleNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabCastLifecycleNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void Record(Trace trace, string scenario, string phase, CombatArena arena, CombatCast cast, int calls = 0)
        {
            var p = arena.Player; var receipt = cast.ResourceResult; var snapshot = receipt?.Snapshot;
            trace.rows.Add(new Row { scenario = scenario, phase = phase, action = p.currentCast ? p.currentCast.Definition.Id : "",
                hits = arena.enemies[0].ReceivedHitCount, available = p.weapons[0].Cooldown.Available, casts = arena.casts.Count,
                shots = arena.playerShots.Count, bombs = arena.bombs.Count, status = receipt == null ? -1 : (int)receipt.Status, calls = calls, slot = p.selected,
                current = ReferenceEquals(p.currentCast, cast), listed = arena.casts.Contains(cast), resources = cast.ResourcesActive,
                charge = cast.ChargeActive, released = cast.ChargeRelease.HasValue, moving = cast.Moving, frozen = cast.FreezeVertical,
                invulnerable = cast.Invulnerable, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(cast.StartedAt), Bits(cast.Age), Bits(cast.ImpactTime), Bits(cast.MotionDuration), Bits(cast.EndTime),
                    Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y), Bits(p.health), Bits(arena.enemies[0].health),
                    Bits(cast.HorizontalVelocity(1)), Bits(p.weapons[0].Cooldown.NextReady), Bits(snapshot?.ActivatedAt ?? -1), Bits(snapshot?.DamageMultiplier ?? -1),
                    Bits(cast.ChargeRelease?.ReleaseTime ?? -1), Bits(cast.ChargeRelease?.MotionEnd ?? -1), Bits(cast.RecastRemaining) } });
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form = "base_sword", int special = 0)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, item => item.id == form), special, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(2, 1000); arena.enemies[0].health = 1000; Physics2D.SyncTransforms();
        }
        static SkillData Copy(SkillData source) => JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(source));
        static void SetFollowUpTimes(SkillData source, float echoStart, float echoActive)
        {
            // These are edited versions of the actual migrated action. Its
            // active timing belongs to the same track compiler, not provenance.
            var document = source.actionTracks;
            document.timeline.frames[0].seconds = document.timeline.frames[1].seconds = .125f;
            foreach (var marker in document.timeline.markers)
            {
                if (marker.id == "contact") { marker.anchor = TimelineAnchor.Seconds; marker.reference = ""; marker.offset = 0; }
                if (marker.id == "echo-start") marker.offset = echoStart;
                if (marker.id == "echo-end") marker.offset = echoActive;
            }
        }
        static CombatCast Start(CombatArena arena, SkillData source = null, Source resources = null, float startAt = -1, bool basic = false)
        {
            var p = arena.Player; var cast = CombatCast.Create(arena, p, source ?? Copy(p.Special), p.Weapon, basic, startAt, resourceSource: resources);
            p.currentCast = cast; return cast;
        }
        static void Boundaries(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver); var p = arena.Player; var source = Copy(p.Special);
            source.activation = source.hits[0].time = 0; source.end = .25f; source.hits[0].active = .0625f;
            var cast = Start(arena, source); cast.BeforeMovement(.03125f); cast.BeforeMovement(.03125f); cast.Step(.03125f);
            Record(trace, "zero", "activated-and-hit-once", arena, cast);
            Assert.That(cast.ResourceResult.Snapshot.ActivatedAt, Is.Zero); Assert.That(p.Weapon.Cooldown.Available, Is.Zero); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));

            Setup(arena, driver); driver.Step(.09375f); source = Copy(p.Special); source.activation = source.hits[0].time = .125f; source.end = .5f;
            cast = Start(arena, source, startAt: .0625f); cast.BeforeMovement(.09375f); cast.Step(.09375f);
            Record(trace, "partial-start", "crossed-impact", arena, cast);
            Assert.That(cast.ResourceResult.Snapshot.ActivatedAt, Is.EqualTo(.09375f)); Assert.That(cast.Age, Is.EqualTo(.03125f)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));

            foreach (string kind in new[] { "direct", "ranged", "bomb", "missile" })
            {
                Setup(arena, driver, kind == "ranged" ? "shuriken" : kind == "bomb" || kind == "missile" ? "bomb" : "base_sword", kind == "missile" ? 2 : 0);
                source = Copy(p.Special); source.activation = .125f; source.end = .125f;
                foreach (var hit in source.hits) hit.time = .125f;
                cast = Start(arena, source); cast.BeforeMovement(.125f); Record(trace, kind, "tick-end-before-current-clock", arena, cast);
                Assert.That(cast.ResourceResult != null, Is.EqualTo(kind == "direct"));
                if (kind == "direct") continue;
                cast.Step(.125f); Assert.That(p.currentCast, Is.SameAs(cast), "A zero recovery release must happen before completion.");
                driver.Step(.125f); cast.BeforeMovement(.03125f); Record(trace, kind, "current-clock-release", arena, cast);
                Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
                cast.Step(.03125f); Record(trace, kind, "after-hit-completion", arena, cast);
                Assert.That(ReferenceEquals(p.currentCast, cast), Is.EqualTo(kind == "missile"), "Missile flight, unlike release-only attacks, retains the action until its end.");
            }
        }
        static void CancellationAndTail(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver); var p = arena.Player; var cast = Start(arena); p.CancelAction(); cast.BeforeMovement(1); cast.Step(.0625f);
            Record(trace, "cancel-before", "cancelled", arena, cast);
            Assert.That(cast.ResourceResult, Is.Null); Assert.That(cast.ResourcesActive, Is.False); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));

            Setup(arena, driver, special: 3); var source = Copy(p.Special); SetFollowUpTimes(source, .5f, .0625f);
            cast = Start(arena, source); cast.BeforeMovement(.0625f); cast.Step(.0625f); var receipt = cast.ResourceResult;
            Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); p.CancelAction(); p.selected = 1;
            Record(trace, "cancel-after", "independent-tail-retained", arena, cast);
            Assert.That(arena.casts.Contains(cast), Is.True); Assert.That(cast.ResourcesActive, Is.False);
            for (int tick = 0; tick < 16; tick++) { driver.Step(.0625f); Record(trace, "cancel-after", "tail-step-" + tick, arena, cast); }
            Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2)); Assert.That(arena.casts.Contains(cast), Is.False); Assert.That(cast.ResourceResult, Is.SameAs(receipt));
        }
        static void AdmissionAndResourceFailures(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            foreach (string scenario in new[] { "nearest-missing", "ground-missing", "install-blocked", "resource-rejected", "resource-exception", "cancel-in-resource" })
            {
                Setup(arena, driver, scenario == "nearest-missing" ? "whip" : scenario == "install-blocked" ? "greatsword" : "base_sword", scenario == "install-blocked" ? 3 : 0);
                var p = arena.Player; var source = Copy(p.Special); source.activation = 0; source.end = .5f;
                var resources = new Source(p.Weapon); GameObject obstruction = null;
                if (scenario == "nearest-missing") arena.enemies[0].Position = new Vector2(40, 1000);
                if (scenario == "ground-missing") { source.vertical = "grounded_required"; p.Position = new Vector2(0, 1010); p.grounded = false; }
                if (scenario == "install-blocked")
                {
                    obstruction = new GameObject("LifecycleInstallationBlock"); obstruction.transform.position = new Vector3(source.effect.x, 1000 + source.effect.y, 0);
                    obstruction.AddComponent<ArenaSurface>(); obstruction.AddComponent<BoxCollider2D>().size = new Vector2(3, 4);
                }
                resources.Reject = scenario == "resource-rejected";
                if (scenario == "resource-exception") resources.Committing = () => throw new InvalidOperationException("lifecycle fixture resource exception");
                Physics2D.SyncTransforms(); var cast = Start(arena, source, resources);
                if (scenario == "cancel-in-resource") resources.Committing = cast.Cancel;
                try
                {
                    cast.BeforeMovement(.03125f); Record(trace, scenario, "failed-admission", arena, cast, resources.Calls);
                    Assert.That(cast.EndTime, Is.EqualTo(.1f)); Assert.That(cast.ResourcesActive, Is.False); Assert.That(cast.HorizontalVelocity(1), Is.EqualTo(arena.Movement.RunSpeed));
                    Assert.That(p.currentCast, Is.SameAs(cast)); Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1));
                    if (scenario.StartsWith("resource-", StringComparison.Ordinal) || scenario == "cancel-in-resource")
                    { Assert.That(resources.Calls, Is.EqualTo(1)); Assert.That(cast.ResourceResult.Status, Is.EqualTo(resources.Reject ? ActionResourceStatus.Rejected : ActionResourceStatus.Failed)); }
                    else { Assert.That(resources.Calls, Is.Zero); Assert.That(cast.ResourceResult, Is.Null); }
                    cast.BeforeMovement(1); cast.Step(.0625f); Assert.That(p.currentCast, Is.SameAs(cast));
                    cast.Step(.125f); Record(trace, scenario, "failure-recovery-finished", arena, cast, resources.Calls);
                    Assert.That(p.currentCast, Is.Null); Assert.That(arena.casts.Contains(cast), Is.False); Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { if (obstruction) { obstruction.SetActive(false); Object.Destroy(obstruction); } }
            }
        }
        static void ReentrantCompletion(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver, special: 3); var p = arena.Player; var source = Copy(p.Special);
            SetFollowUpTimes(source, .75f, .08f);
            var cast = Start(arena, source); driver.Step(.0625f, new CombatInput(0, CombatButtons.Swap));
            bool entered = false;
            Application.LogCallback observe = (message, stack, type) =>
            {
                if (!message.Contains("weapon slot 2")) return;
                entered = true; Assert.That(p.currentCast, Is.Null); Assert.That(arena.casts.Contains(cast), Is.True);
                p.currentCast = cast; cast.Step(.0625f);
                Assert.That(p.currentCast, Is.SameAs(cast), "Completion is claimed before the callback, so reentry must not finish it again.");
                Record(trace, "reentrant-completion", "inside-callback", arena, cast); p.currentCast = null;
            };
            Application.logMessageReceived += observe;
            try { driver.Step(.1875f); }
            finally { Application.logMessageReceived -= observe; }
            Assert.That(entered, Is.True); Assert.That(arena.casts.Contains(cast), Is.True); Record(trace, "reentrant-completion", "after-callback", arena, cast);
            for (int tick = 0; tick < 16; tick++) driver.Step(.0625f);
            Record(trace, "reentrant-completion", "tail-finished", arena, cast);
            Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2)); Assert.That(arena.casts.Contains(cast), Is.False);
        }
        static void ChargeTiming(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver, "rapier"); var p = arena.Player; var source = Copy(p.Special); var cast = Start(arena, source);
            p.specialHeld = true; driver.Step(.3125f, new CombatInput(0, CombatButtons.None, true)); cast.BeforeMovement(.03125f); Record(trace, "generic-charge", "held", arena, cast);
            Assert.That(cast.ChargeRelease, Is.Null); p.specialHeld = false; cast.BeforeMovement(.03125f);
            Record(trace, "generic-charge", "released-once", arena, cast); var release = cast.ChargeRelease;
            Assert.That(release.HasValue, Is.True); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
            float end = cast.EndTime; cast.BeforeMovement(.03125f); Assert.That(cast.ChargeRelease, Is.EqualTo(release)); Assert.That(cast.EndTime, Is.EqualTo(end));
            p.CancelAction(); Record(trace, "generic-charge", "cancelled", arena, cast); Assert.That(cast.ChargeActive, Is.False); Assert.That(cast.ChargeRelease, Is.EqualTo(release));
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace();
            var floor = new GameObject("CastLifecycleFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Boundaries(trace, arena, driver); CancellationAndTail(trace, arena, driver); AdmissionAndResourceFailures(trace, arena, driver);
                    ReentrantCompletion(trace, arena, driver); ChargeTiming(trace, arena, driver);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-castLifecycleTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
