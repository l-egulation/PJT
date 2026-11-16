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
    public sealed class SkillWaveNativeProbe : CombatActor
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    // Frozen against the public Native API before extracting the wave owner.
    // Neither observation nor scenario setup depends on the new execution API.
    public class SkillWaveNativeTraceTests
    {
        enum Case
        {
            BodyOffset, LockedFacing, FollowBasicFacing, ThinWall, FloorGrazing,
            OneWay, SpawnBlocked, SpawnPathBlocked, TargetOcclusion, LateHit,
            SweepMultiple, Duplicate, ExactRange, CoarseStep, CancelBefore,
            CancelAfter, SwapAfter, DeathBefore, DeathAfter, ResetBefore,
            ResetAfter, ActivationCapture, CallbackCancel, CallbackRemove, CallbackThrow
        }
        [Serializable] sealed class Row
        {
            public string scenario, phase;
            public int tick, hits, otherHits, available, casts, slot, resourceStatus, facing, callbacks;
            public bool current, retained, visual, resources, grounded, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GameWaveNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabWaveNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static BoxCollider2D Surface(List<GameObject> geometry, string name, Vector2 center, Vector2 size, bool oneWay = false)
        {
            var go = new GameObject(name); geometry.Add(go); go.transform.position = center;
            go.AddComponent<ArenaSurface>().oneWay = oneWay; var box = go.AddComponent<BoxCollider2D>(); box.size = size; return box;
        }
        static SpriteRenderer FindWave() => GameObject.Find("SwordWave")?.GetComponent<SpriteRenderer>();
        static void Record(Trace trace, string label, string phase, int tick, float step, CombatArena arena, CombatCast cast,
            WeaponState weapon, SkillWaveNativeProbe target, SkillWaveNativeProbe other, SpriteRenderer wave, int callbacks)
        {
            var p = arena.Player; var receipt = cast.ResourceResult; Vector2 at = wave ? (Vector2)wave.transform.position : default;
            trace.rows.Add(new Row { scenario = label, phase = phase, tick = tick, hits = target.ReceivedHitCount, otherHits = other.ReceivedHitCount,
                available = weapon.Cooldown.Available, casts = arena.casts.Count, slot = p.selected, resourceStatus = receipt == null ? -1 : (int)receipt.Status,
                facing = p.facing, callbacks = callbacks, current = ReferenceEquals(p.currentCast, cast), retained = arena.casts.Contains(cast),
                visual = wave, resources = cast.ResourcesActive, grounded = p.grounded, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(step), Bits(p.Position.x), Bits(p.Position.y), Bits(p.Center.x), Bits(p.Center.y), Bits(p.velocity.x), Bits(p.velocity.y), Bits(p.health),
                    Bits(target.Position.x), Bits(target.Position.y), Bits(target.health), Bits(other.Position.x), Bits(other.Position.y), Bits(other.health),
                    Bits(at.x), Bits(at.y), Bits(cast.StartedAt), Bits(cast.Age), Bits(cast.ImpactTime), Bits(cast.EndTime),
                    Bits(weapon.Cooldown.NextReady), Bits(weapon.Cooldown.Period), receipt?.Snapshot == null ? 0 : Bits(receipt.Snapshot.ActivatedAt),
                    receipt?.Snapshot == null ? 0 : Bits(receipt.Snapshot.DamageMultiplier) } });
        }
        static IEnumerator Run(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, Case scenario, int facing,
            SkillWaveNativeProbe target, SkillWaveNativeProbe other, Vector2 playerOffset)
        {
            target.OnCancel = other.OnCancel = null; driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword"), 1, out var error), Is.True, error);
            p.offset = scenario == Case.BodyOffset ? new Vector2(.4375f, playerOffset.y) : playerOffset;
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(facing);
            target.ResetActor(new Vector2(4 * facing, 1000)); other.ResetActor(new Vector2(6.5f * facing, 1000));
            target.SyncFacing(-facing); other.SyncFacing(facing);
            var geometry = new List<GameObject>(); var source = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(p.Form.specials[1]));
            bool callbackCase = scenario == Case.CallbackCancel || scenario == Case.CallbackRemove || scenario == Case.CallbackThrow;
            if (callbackCase)
            {
                source.hits[0].stun = .01f;
                foreach (var marker in source.actionTracks.timeline.markers)
                    if (marker.id == "contact") { marker.anchor = TimelineAnchor.Seconds; marker.reference = ""; marker.offset = 0; }
            }
            if (scenario == Case.FollowBasicFacing)
            {
                // This synthetic basic-policy probe is deliberately not the
                // facing-locked product special copied as fixture metadata.
                source.id = "wave_basic_facing_fixture"; source.actionTracks = null;
                source.activation = 0; source.chargeMin = source.chargeMax = 0;
            }
            if (scenario == Case.LateHit) target.Position = new Vector2(40, 1000);
            if (scenario == Case.SwapAfter) target.Position = new Vector2(8 * facing, 1000);
            if (scenario == Case.ExactRange)
            {
                float end = source.effect.x + source.effect.distance;
                target.Position = new Vector2((end + .45f) * facing, 1000);
                other.Position = new Vector2((end + .95f) * facing, 1000);
            }
            if (scenario == Case.SweepMultiple) { target.Position = new Vector2(2.5f * facing, 1000); other.Position = new Vector2(5.5f * facing, 1000); }
            BoxCollider2D barrier = null;
            if (scenario == Case.ThinWall || scenario == Case.TargetOcclusion)
                barrier = Surface(geometry, "WaveTraceThinWall", new Vector2(3 * facing, 1001), new Vector2(.04f, 4));
            if (scenario == Case.OneWay) Surface(geometry, "WaveTraceOneWay", new Vector2(3 * facing, 1001), new Vector2(.002f, 4), true);
            if (scenario == Case.FloorGrazing) Surface(geometry, "WaveTraceGrazingFloor", new Vector2(4 * facing, 999.999f), new Vector2(12, .002f));
            if (scenario == Case.SpawnBlocked) Surface(geometry, "WaveTraceSpawnBlocked", new Vector2(source.effect.x * facing, 1001), new Vector2(.02f, 3));
            if (scenario == Case.SpawnPathBlocked) Surface(geometry, "WaveTraceSpawnPathBlocked", new Vector2(.6f * facing, 1001), new Vector2(.01f, 3));
            if (scenario == Case.TargetOcclusion) target.Position = new Vector2(3.65f * facing, 1000);
            if (scenario == Case.Duplicate)
            {
                var duplicate = new GameObject("WaveTraceDuplicate"); geometry.Add(duplicate); duplicate.transform.SetParent(target.hurtCollider.transform, false);
                var box = duplicate.AddComponent<BoxCollider2D>(); box.size = target.hurtCollider.size; box.offset = target.hurtCollider.offset; box.isTrigger = true;
                duplicate.AddComponent<HurtRegion>().actor = target;
            }
            Physics2D.SyncTransforms(); var originalWeapon = p.Weapon;
            if (barrier)
            {
                Bounds hurt = target.hurtCollider.bounds, wall = barrier.bounds;
                float nearestHurt = facing == 1 ? hurt.min.x : -hurt.max.x;
                float farthestWall = facing == 1 ? wall.max.x : -wall.min.x;
                Assert.That(nearestHurt, Is.GreaterThan(farthestWall), scenario + ": the complete hurt region is behind the wall.");
                Assert.That(wall.min.y, Is.LessThan(1000)); Assert.That(wall.max.y, Is.GreaterThan(1002));
                Vector2 spawn = p.Position + new Vector2(source.effect.x * facing, source.effect.y);
                Vector2 size = new Vector2(source.hits[0].box.width, source.hits[0].box.height);
                Assert.That(arena.SkillProjectileBlocked(spawn, size), Is.False, scenario + ": the fixture must allow activation.");
                Assert.That(arena.Occluded(p.Center, spawn), Is.False, scenario + ": the spawn path is clear.");
                Assert.That(arena.Occluded(p.Center, target.hurtCollider.ClosestPoint(p.Center)), Is.True, scenario + ": Native sees the barrier.");
                float allowed = arena.SkillProjectileTravel(spawn, size, Vector2.right * facing, source.effect.distance);
                Assert.That(allowed, Is.GreaterThan(0).And.LessThan(source.effect.distance), scenario + ": Native travel stops before the target.");
            }
            var cast = CombatCast.Create(arena, p, source, originalWeapon, scenario == Case.FollowBasicFacing); p.currentCast = cast;
            string label = scenario + "/" + facing; SpriteRenderer wave = null; bool sawVisual = false; int callbacks = 0;
            try
            {
                Record(trace, label, "start", -1, 0, arena, cast, originalWeapon, target, other, wave, callbacks);
                if (callbackCase)
                {
                    target.OnCancel = () =>
                    {
                        callbacks++; Record(trace, label, "receive", 0, .5f, arena, cast, originalWeapon, target, other, wave, callbacks);
                        if (scenario == Case.CallbackCancel) p.CancelAction();
                        if (scenario == Case.CallbackRemove) arena.RemoveOwnedEffects(p);
                        if (scenario == Case.CallbackThrow) throw new InvalidOperationException("Wave native receive boundary");
                    };
                    cast.BeforeMovement(.5f); wave = FindWave(); Assert.That(wave, Is.Not.Null); Vector2 spawn = wave.transform.position;
                    Record(trace, label, "activated", 0, .5f, arena, cast, originalWeapon, target, other, wave, callbacks);
                    if (scenario == Case.CallbackThrow)
                    {
                        Assert.Throws<InvalidOperationException>(() => cast.Step(.5f));
                        Assert.That((Vector2)wave.transform.position, Is.EqualTo(spawn), "The moved gameplay state precedes damage, but the visual follows a successful callback.");
                        Assert.That(other.ReceivedHitCount, Is.Zero); target.OnCancel = null;
                        Record(trace, label, "throw", 0, .5f, arena, cast, originalWeapon, target, other, wave, callbacks);
                        cast.Step(0); Assert.That(other.ReceivedHitCount, Is.Zero, "A retry is not a replay of the aborted sweep.");
                    }
                    else { cast.Step(.5f); Assert.That(other.ReceivedHitCount, Is.EqualTo(1), "Native cancellation/removal does not introduce a new continuation guard."); }
                    Assert.That(target.health, Is.EqualTo(960)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                    Record(trace, label, "continued", 0, 0, arena, cast, originalWeapon, target, other, wave, callbacks);
                }
                else
                {
                    for (int tick = 0; tick < 55; tick++)
                    {
                        if (scenario == Case.CancelBefore && tick == 3 || scenario == Case.CancelAfter && tick == 12) p.CancelAction();
                        if (scenario == Case.DeathBefore && tick == 3 || scenario == Case.DeathAfter && tick == 12) { p.health = 0; p.Die(); }
                        if (scenario == Case.ResetBefore && tick == 3 || scenario == Case.ResetAfter && tick == 12) driver.Reset();
                        if (scenario == Case.LockedFacing && tick == 12) p.SyncFacing(-facing);
                        if (scenario == Case.FollowBasicFacing && tick == 4) { p.CancelAction(); cast.FollowBasicFacing(-facing); }
                        if (scenario == Case.LateHit && tick == 20) { target.Position = new Vector2(6.5f * facing, 1000); Physics2D.SyncTransforms(); }
                        if (scenario == Case.ActivationCapture && tick == 4) originalWeapon.StatSlots[1] = 2;
                        if (scenario == Case.ActivationCapture && tick == 12) originalWeapon.StatSlots.Clear();
                        float step = (scenario == Case.CoarseStep || scenario == Case.SweepMultiple) && tick == 0 ? .75f : .02f;
                        var buttons = scenario == Case.SwapAfter && tick == 12 ? CombatButtons.Swap : CombatButtons.None;
                        driver.Step(step, new CombatInput(0, buttons));
                        if (!wave) wave = FindWave(); sawVisual |= wave;
                        Record(trace, label, "step", tick, step, arena, cast, originalWeapon, target, other, wave, callbacks);
                        Assert.That(arena.SimulationFaulted, Is.False, label);
                    }
                    bool absent = scenario == Case.ThinWall || scenario == Case.SpawnBlocked || scenario == Case.SpawnPathBlocked || scenario == Case.TargetOcclusion ||
                        scenario == Case.CancelBefore || scenario == Case.DeathBefore || scenario == Case.DeathAfter || scenario == Case.ResetBefore || scenario == Case.ResetAfter || scenario == Case.FollowBasicFacing;
                    if (absent) Assert.That(target.ReceivedHitCount, Is.Zero, label);
                    else
                    {
                        Assert.That(target.ReceivedHitCount, Is.EqualTo(1), label);
                        Assert.That(target.health, Is.EqualTo(scenario == Case.ActivationCapture ? 952 : 960).Within(.0001f), label);
                    }
                    if (scenario == Case.ExactRange) Assert.That(other.ReceivedHitCount, Is.Zero, label);
                    if (scenario == Case.Duplicate || scenario == Case.SweepMultiple || scenario == Case.CoarseStep) Assert.That(other.ReceivedHitCount, Is.EqualTo(1), label);
                    if (scenario == Case.SpawnBlocked || scenario == Case.SpawnPathBlocked) { Assert.That(sawVisual, Is.False, label); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded), label); }
                    if (scenario == Case.SwapAfter) Assert.That(p.selected, Is.EqualTo(1), label);
                    if (scenario == Case.CancelBefore) Assert.That(originalWeapon.Cooldown.Available, Is.EqualTo(1), label);
                }
                yield return null;
                Assert.That(wave == null, Is.True, label + ": completed/dead/reset wave visual cleanup");
                Record(trace, label, "flushed", 55, 0, arena, cast, originalWeapon, target, other, wave, callbacks);
            }
            finally
            {
                target.OnCancel = other.OnCancel = null; p.offset = playerOffset;
                foreach (var go in geometry) if (go) { go.SetActive(false); Object.Destroy(go); }
                Physics2D.SyncTransforms();
            }
        }
        static IEnumerator Exercise(string scene, string traceName)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var trace = new Trace(); var geometry = new List<GameObject>();
            Surface(geometry, "WaveTraceFloor", new Vector2(0, 999.5f), new Vector2(200, 1));
            var target = new GameObject("WaveTraceTarget").AddComponent<SkillWaveNativeProbe>(); var other = new GameObject("WaveTraceOther").AddComponent<SkillWaveNativeProbe>();
            target.Configure(arena, new Vector2(40, 1000), new Vector2(.2f, 2), new Vector2(.08f, 1), false);
            other.Configure(arena, new Vector2(40, 1000), new Vector2(.2f, 2), new Vector2(.08f, 1), false);
            target.maxHealth = other.maxHealth = 1000; Physics2D.SyncTransforms(); Vector2 playerOffset = arena.Player.offset;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (Case scenario in Enum.GetValues(typeof(Case)))
                {
                    yield return Run(trace, arena, driver, scenario, 1, target, other, playerOffset);
                    yield return Run(trace, arena, driver, scenario, -1, target, other, playerOffset);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-skillWaveExecutionTraceRoot");
                if (at >= 0 && at + 1 < args.Length)
                {
                    string directory = Path.GetFullPath(args[at + 1]); Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, traceName + ".json"), JsonUtility.ToJson(trace, false));
                }
            }
            finally
            {
                target.OnCancel = other.OnCancel = null; Object.Destroy(target.gameObject); Object.Destroy(other.gameObject);
                foreach (var go in geometry) if (go) { go.SetActive(false); Object.Destroy(go); }
            }
        }
    }
}
