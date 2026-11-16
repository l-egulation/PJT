using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class RangedLaunchContactProbe : CombatActor
    {
        public Action OnCancel;
        public override void CancelAttack() { OnCancel?.Invoke(); base.CancelAttack(); }
    }

    public class RangedLaunchIntegrationTests
    {
        [UnityTest] public IEnumerator GameKeepsActualLaunchContactsWeaponsAndFailurePrefixes()
            => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabKeepsActualLaunchContactsWeaponsAndFailurePrefixes()
            => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void SameVector(Vector2 actual, Vector2 expected)
        { Assert.That(Bits(actual.x), Is.EqualTo(Bits(expected.x))); Assert.That(Bits(actual.y), Is.EqualTo(Bits(expected.y))); }
        static void CenterAt(CombatActor actor, Vector2 center)
            => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        static RangedData Ranged(string type = "shuriken") => new RangedData
        { type = type, speed = 22, radius = .2f, distance = 6, spawnHeight = 1, spawnX = 2, spawnGap = .05f };
        static BombData Grenade() => new BombData
        {
            angle = 30, speed = 10, gravity = 20, radius = .2f, blastRadius = 1.2f, lifetime = 5, spawnHeight = 1, spawnGap = .05f,
            fragmentDamage = 4, fragmentSpeed = 6, fragmentGravity = 20, fragmentRadius = .1f, fragmentBlastRadius = .6f,
            fragmentLifetime = .6f, fragmentSpawnOffset = .2f, fragmentAngles = new[] { 30f, 60f, 120f, 150f }
        };
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, RangedLaunchContactProbe probe)
        {
            probe.OnCancel = null; driver.Reset(); var p = arena.Player;
            p.selected = 0; p.InputBlocked = false; p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.SyncFacing(1);
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(50, 1000); }
            probe.ResetActor(new Vector2(40, 1000)); probe.health = probe.maxHealth = 100; probe.SyncFacing(1);
            Physics2D.SyncTransforms();
        }
        static GameObject Surface(Vector2 at)
        {
            var wall = new GameObject("RangedLaunchMuzzleWall"); wall.transform.position = at;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 4); wall.AddComponent<ArenaSurface>(); return wall;
        }
        static void BlockedMuzzle(CombatArena arena, CombatArena.ManualSimulation driver, RangedLaunchContactProbe probe, List<string> records)
        {
            Prepare(arena, driver, probe); CenterAt(probe, new Vector2(2, 1001));
            var wall = Surface(new Vector2(1.1f, 1001));
            try
            {
                Physics2D.SyncTransforms(); records.Clear(); int shots = arena.PlayerShotCount;
                arena.FirePlayerRanged(arena.Player, new SkillRangedLaunchDefinition(Ranged()), 6, 1, arena.Player.Weapon);
                Assert.That(arena.PlayerShotCount, Is.EqualTo(shots)); Assert.That(arena.playerShots, Is.Empty);
                Assert.That(probe.health, Is.EqualTo(100)); Assert.That(probe.ReceivedHitCount, Is.Zero);
                Assert.That(records[records.Count - 1], Does.Contain("shuriken muzzle blocked by solid"));
                Assert.That(records.Exists(item => item.Contains("ranged fired")), Is.False);

                Prepare(arena, driver, probe); CenterAt(probe, new Vector2(.35f, 1001)); Physics2D.SyncTransforms();
                var source = Ranged("shuriken_range"); source.blastRadius = 3; var p = arena.Player;
                Vector2 origin = p.Position + new Vector2(p.offset.x, source.spawnHeight), muzzle = p.Position + new Vector2(source.spawnX, source.spawnHeight);
                Vector2 delta = muzzle - origin; float nearest = float.PositiveInfinity;
                foreach (var hit in Physics2D.CircleCastAll(origin, source.radius, delta.normalized, delta.magnitude))
                    if (hit.collider.GetComponent<ArenaSurface>() is ArenaSurface surface && !surface.oneWay) nearest = Mathf.Min(nearest, hit.distance);
                Vector2 expected = origin + delta.normalized * Mathf.Clamp(nearest, 0, delta.magnitude);
                PlayerProjectile detonated = null;
                probe.OnCancel = () =>
                {
                    foreach (var candidate in Object.FindObjectsByType<PlayerProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (candidate.owner == p && candidate.Definition.Type == "shuriken_range") detonated = candidate;
                    Assert.That(detonated, Is.Not.Null); Assert.That(detonated.sourceWeapon, Is.Null);
                    SameVector(detonated.Position, expected); Assert.That(arena.playerShots.Contains(detonated), Is.False);
                };
                shots = arena.PlayerShotCount; records.Clear();
                arena.FirePlayerRanged(p, new SkillRangedLaunchDefinition(source), 24, 1, p.Weapon);
                Assert.That(arena.PlayerShotCount, Is.EqualTo(shots + 1)); Assert.That(arena.playerShots, Is.Empty);
                Assert.That(probe.health, Is.EqualTo(76)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(1));
                Assert.That(arena.bombBursts, Has.Count.EqualTo(1)); SameVector(arena.bombBursts[0].transform.position, expected);
                Assert.That(records[records.Count - 2], Does.Contain("shuriken range explosion"));
                Assert.That(records[records.Count - 1], Does.Contain("shuriken muzzle blocked by solid"));
            }
            finally { probe.OnCancel = null; Object.DestroyImmediate(wall); }
        }
        static void ReturnWeapon(CombatArena arena, CombatArena.ManualSimulation driver, RangedLaunchContactProbe probe, bool explicitSource)
        {
            Prepare(arena, driver, probe); CenterAt(probe, new Vector2(2, 1001)); Physics2D.SyncTransforms();
            var p = arena.Player; var expected = explicitSource ? new WeaponState() : p.Weapon;
            var source = Ranged("shuriken_move"); source.returnDelay = 5; source.behindGap = .25f;
            PlayerProjectile shot = null;
            probe.OnCancel = () =>
            {
                shot = arena.playerShots[0]; Assert.That(shot.sourceWeapon, Is.SameAs(expected));
                p.selected = 1;
            };
            arena.FirePlayerRanged(p, new SkillRangedLaunchDefinition(source), 6, 1, explicitSource ? expected : null);
            Assert.That(probe.health, Is.EqualTo(94)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(1));
            Assert.That(shot.sourceWeapon, Is.SameAs(expected)); Assert.That(p.Weapon, Is.Not.SameAs(expected));
            Assert.That(p.ReturnReservations, Has.Count.EqualTo(1)); Assert.That(p.ReturnReservations[0].WeaponId, Is.EqualTo(expected.InstanceId));
            Assert.That(arena.playerShots, Is.Empty); probe.OnCancel = null;
        }
        static void AuthoredAngles(CombatArena arena, CombatArena.ManualSimulation driver, RangedLaunchContactProbe probe)
        {
            Prepare(arena, driver, probe); CenterAt(probe, new Vector2(2, 1001)); Physics2D.SyncTransforms();
            var p = arena.Player; Vector2 size = p.size, offset = p.offset;
            var data = Ranged("shotgun"); data.speed = 20; data.angles = new[] { 20f, -10f, 0f };
            int contact = 0, shots = arena.PlayerShotCount;
            probe.OnCancel = () =>
            {
                var shot = arena.playerShots[0]; float angle = data.angles[contact++];
                SameVector(shot.velocity, new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * data.speed);
                Assert.That(shot.sourceWeapon, Is.Null);
                p.Position += Vector2.right * 20; p.size = new Vector2(10, 2); p.offset = new Vector2(3, 1);
            };
            try
            {
                arena.FirePlayerRanged(p, new SkillRangedLaunchDefinition(data), 4, 1, p.Weapon);
                Assert.That(contact, Is.EqualTo(3)); Assert.That(probe.health, Is.EqualTo(88)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(3));
                Assert.That(arena.PlayerShotCount, Is.EqualTo(shots + 3)); Assert.That(arena.playerShots, Is.Empty);
            }
            finally { probe.OnCancel = null; p.size = size; p.offset = offset; }
        }
        static void LaserDispatch(CombatArena arena, CombatArena.ManualSimulation driver, RangedLaunchContactProbe probe, List<string> records)
        {
            Prepare(arena, driver, probe); CenterAt(probe, new Vector2(3, 1001));
            var second = arena.enemies[0]; CenterAt(second, new Vector2(5, 1001)); second.health = 100;
            var data = Ranged("laser"); data.thickness = .25f; data.distance = 8; Physics2D.SyncTransforms();
            int shots = arena.PlayerShotCount; records.Clear();
            arena.FirePlayerRanged(arena.Player, new SkillRangedLaunchDefinition(data), 12, 1);
            Assert.That(probe.health, Is.EqualTo(88)); Assert.That(second.health, Is.EqualTo(88));
            Assert.That(probe.ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.PlayerShotCount, Is.EqualTo(shots));
            Assert.That(arena.playerShots, Is.Empty); Assert.That(arena.lasers, Has.Count.EqualTo(1));
            Assert.That(records[records.Count - 1], Does.Contain("laser fired damage=12 targets=2"));
            Assert.That(records.Exists(item => item.Contains("ranged fired")), Is.False);
        }
        static void BombLiveWidth(CombatArena arena, CombatArena.ManualSimulation driver, RangedLaunchContactProbe probe)
        {
            Prepare(arena, driver, probe); var p = arena.Player; var data = Grenade(); data.fragmentAngles = null;
            Vector2 oldSize = p.size; Vector2 origin = p.Position + new Vector2(-p.offset.x, data.spawnHeight); BombProjectile spawned = null;
            Application.LogCallback observe = (message, stack, type) =>
            {
                if (!message.Contains("bomb launched damage=12")) return;
                spawned = arena.bombs[arena.bombs.Count - 1]; Assert.That(spawned.fragmentMultiplier, Is.EqualTo(1));
                SameVector(spawned.Position, origin); p.Position = new Vector2(40, 1000); p.size = new Vector2(3, oldSize.y);
            };
            Application.logMessageReceived += observe;
            try
            {
                var result = arena.SpawnBomb(p, new SkillBombLaunchDefinition(data), 12, -1, 1.5f);
                Assert.That(result, Is.SameAs(spawned)); Assert.That(result.fragmentMultiplier, Is.EqualTo(1.5f));
                SameVector(result.Position, origin + Vector2.left * (3f / 2 + data.radius + data.spawnGap));
                float radians = data.angle * Mathf.Deg2Rad;
                SameVector(result.velocity, new Vector2(-Mathf.Cos(radians), Mathf.Sin(radians)) * data.speed);
            }
            finally { Application.logMessageReceived -= observe; p.size = oldSize; }
        }
        static void BombInitialOverlap(CombatArena arena, CombatArena.ManualSimulation driver, RangedLaunchContactProbe probe)
        {
            Prepare(arena, driver, probe); var p = arena.Player; var data = Grenade();
            Vector2 origin = p.Position + new Vector2(p.offset.x, data.spawnHeight); CenterAt(probe, origin); Physics2D.SyncTransforms();
            BombProjectile actual = null;
            probe.OnCancel = () =>
            {
                if (actual) return;
                actual = arena.bombs[0]; Assert.That(actual.fragmentMultiplier, Is.EqualTo(1.5f)); SameVector(actual.Position, origin);
            };
            var result = arena.SpawnBomb(p, new SkillBombLaunchDefinition(data), 12, 1, 1.5f); probe.OnCancel = null;
            Assert.That(result, Is.SameAs(actual)); Assert.That(arena.bombs.Contains(result), Is.False);
            Assert.That(probe.health, Is.EqualTo(64)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(5));
            Assert.That(arena.BombExplosionCount, Is.EqualTo(5)); Assert.That(p.health, Is.EqualTo(100));
            result.SweepTo(result.Position); Assert.That(probe.health, Is.EqualTo(64)); Assert.That(arena.BombExplosionCount, Is.EqualTo(5));
        }
        static void NativeFailures(CombatArena arena, CombatArena.ManualSimulation driver, RangedLaunchContactProbe probe, List<string> records)
        {
            Prepare(arena, driver, probe); CenterAt(probe, new Vector2(2, 1001)); Physics2D.SyncTransforms();
            var p = arena.Player; var data = Ranged("shuriken_move"); data.pierce = true; data.returnDelay = 5; data.behindGap = .25f;
            var failure = new InvalidOperationException("Actual launch Receive cancellation");
            probe.OnCancel = () => throw failure; records.Clear();
            Assert.That(Assert.Throws<InvalidOperationException>(() => arena.FirePlayerRanged(p, new SkillRangedLaunchDefinition(data), 6, 1)), Is.SameAs(failure));
            Assert.That(probe.health, Is.EqualTo(94)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(1));
            Assert.That(arena.playerShots, Has.Count.EqualTo(1)); var shot = arena.playerShots[0];
            Assert.That(shot.sourceWeapon, Is.SameAs(p.Weapon)); Assert.That(p.ReturnReservations, Is.Empty);
            Assert.That(records.Exists(item => item.Contains("ranged fired")), Is.False);
            probe.OnCancel = null; shot.SweepTo(new Vector2(2, 1001)); Assert.That(probe.health, Is.EqualTo(94));
            Assert.That(probe.ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.playerShots.Contains(shot), Is.True);

            Prepare(arena, driver, probe); var bombData = Grenade();
            Vector2 origin = p.Position + new Vector2(p.offset.x, bombData.spawnHeight); CenterAt(probe, origin); Physics2D.SyncTransforms();
            probe.OnCancel = () => throw failure; BombProjectile returned = null;
            Assert.That(Assert.Throws<InvalidOperationException>(() => returned = arena.SpawnBomb(p, new SkillBombLaunchDefinition(bombData), 12, 1, 1.5f)), Is.SameAs(failure));
            Assert.That(returned, Is.Null); Assert.That(arena.bombs, Has.Count.EqualTo(1)); var partial = arena.bombs[0];
            Assert.That(partial.fragmentMultiplier, Is.EqualTo(1.5f)); SameVector(partial.Position, origin);
            Assert.That(probe.health, Is.EqualTo(88)); Assert.That(probe.ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.BombExplosionCount, Is.Zero);
            probe.OnCancel = null; partial.SweepTo(partial.Position); partial.Step(.1f);
            Assert.That(probe.health, Is.EqualTo(88)); Assert.That(arena.bombs.Contains(partial), Is.True);
            Assert.That(arena.BombExplosionCount, Is.Zero);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var records = new List<string>();
            var floor = new GameObject("RangedLaunchFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(300, 1); floor.AddComponent<ArenaSurface>();
            var probe = new GameObject("RangedLaunchContactProbe").AddComponent<RangedLaunchContactProbe>();
            probe.Configure(arena, new Vector2(40, 1000), arena.Player.size, arena.Player.offset, false);
            Application.LogCallback observe = (message, stack, type) => { if (message.StartsWith("[CombatV2]", StringComparison.Ordinal)) records.Add(message); };
            Application.logMessageReceived += observe;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    BlockedMuzzle(arena, driver, probe, records); ReturnWeapon(arena, driver, probe, false); ReturnWeapon(arena, driver, probe, true);
                    AuthoredAngles(arena, driver, probe); LaserDispatch(arena, driver, probe, records);
                    BombLiveWidth(arena, driver, probe); BombInitialOverlap(arena, driver, probe); NativeFailures(arena, driver, probe, records);
                    Prepare(arena, driver, probe);
                }
            }
            finally
            {
                Application.logMessageReceived -= observe; probe.OnCancel = null;
                Object.Destroy(probe.gameObject); Object.Destroy(floor);
            }
        }
    }
}
