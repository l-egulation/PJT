using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class RangedLaunchExecutionTests
    {
        sealed class Actor
        {
            public readonly int Id;
            public float X, Offset = .25f, Width = 1, Health = 100;
            public bool Player, Occluded;
            public object Weapon = new object();
            public int Hits;
            public Actor(int id, float x = 0) { Id = id; X = x; }
        }
        sealed class SurfaceHit
        {
            public bool Surface = true, OneWay;
            public float Distance;
            public SurfaceHit(float distance) { Distance = distance; }
        }
        sealed class Shot : IPlayerProjectileExecutionHost
        {
            public readonly Launch Launch;
            public readonly PlayerProjectileExecution Execution;
            public readonly float Origin, Angle;
            public float X, SweepDestination;
            public object Weapon;
            public int Sweeps, Removals;
            public Shot(Launch launch, float origin, float angle)
            {
                Launch = launch; Origin = X = origin; Angle = angle;
                Execution = new PlayerProjectileExecution(launch.Definition);
            }
            public void Sweep(float destination)
            {
                if (Execution.Removed) return;
                Sweeps++; SweepDestination = destination; Launch.Mark("sweep:" + Launch.Shots.IndexOf(this));
                foreach (var target in Launch.Targets)
                {
                    if (Math.Abs(target.X - destination) > Execution.Definition.Radius) continue;
                    var host = new HitHost(this, target);
                    if (PlayerProjectileHitExecution.Resolve(-1, ref host)) return;
                }
                X = destination;
            }
            public bool Receive(Actor target)
            {
                target.Health -= Launch.Damage; target.Hits++;
                Launch.Mark("receive:" + target.Id); return true;
            }
            public void Detonate() { var host = new BlastHost(this); PlayerProjectileHitExecution.Detonate(ref host); }
            public void Release() => Execution.Release(this);
            public void Remove() { Removals++; Launch.Mark("remove:" + Launch.Shots.IndexOf(this)); }
            public void SweepByTime(float duration) => throw new InvalidOperationException("Not a flight test.");
            public void SweepByDistance(float distance) => throw new InvalidOperationException("Not a flight test.");
            public void SweepCurrent() => Sweep(X);
            public void Rotate(float duration) => throw new InvalidOperationException("Not a flight test.");
        }
        readonly struct HitHost : IPlayerProjectileHitHost
        {
            readonly Shot shot;
            readonly Actor target;
            public HitHost(Shot shot, Actor target) { this.shot = shot; this.target = target; }
            public bool ValidContact => target != shot.Launch.Owner && !target.Player && target.Health > 0;
            public float BlastRadius => shot.Execution.Definition.BlastRadius;
            public void Detonate() => shot.Detonate();
            public bool CaptureHitRegion() => true;
            public bool TryClaimActor(float age) => shot.Execution.TryClaimActor(target.Id, 10, age);
            public bool Receive() => shot.Receive(target);
            public float ReturnDelay => shot.Execution.Definition.ReturnDelay;
            public bool TargetDead => target.Health <= 0;
            public bool CaptureReturnPlayer() => shot.Launch.Owner.Player;
            public void VisitTarget()
            { shot.Launch.ReturnWeapons.Add(shot.Weapon); shot.Launch.Mark("return:" + target.Id); }
            public bool Pierce => shot.Execution.Definition.Pierce;
            public void Release() => shot.Release();
        }
        struct BlastHost : IPlayerProjectileBlastHost
        {
            readonly Shot shot;
            HashSet<int> seen;
            Actor[] targets;
            Actor target;
            int index;
            public BlastHost(Shot shot) { this.shot = shot; seen = null; targets = null; target = null; index = -1; }
            public bool Removed => shot.Execution.Removed;
            public float BlastRadius => shot.Execution.Definition.BlastRadius;
            public void Release() => shot.Release();
            public void BeginCandidates() { seen = new HashSet<int>(); targets = shot.Launch.Targets.ToArray(); }
            public bool NextCandidate()
            { if (++index >= targets.Length) return false; target = targets[index]; return true; }
            public bool RegionExists => true;
            public bool ActorExists => true;
            public bool ActorIsOwner => target == shot.Launch.Owner;
            public bool ActorIsPlayer => target.Player;
            public bool ActorIsDecoy => false;
            public bool ActorDead => target.Health <= 0;
            public bool TryClaimActor() => seen.Add(target.Id);
            public void CaptureContact() { }
            public float ContactDistance => Math.Abs(target.X - shot.X);
            public bool ContactOccluded => target.Occluded;
            public void Receive() => shot.Receive(target);
            public void ShowExplosion() { shot.Launch.Explosions++; shot.Launch.Mark("explosion"); }
            public void RecordExplosion() => shot.Launch.Mark("explosion-record");
        }
        sealed class Launch
        {
            public Actor Owner = new Actor(100) { Player = true };
            public RangedProjectileDefinition Definition;
            public string Type;
            public float SpawnX, Gap = .05f, Damage = 6;
            public int Direction = 1, LaserCalls, Explosions, BlockedRecords, FiredRecords, RecordedCount = -1;
            public object Source;
            public Action<string> Callback;
            public Action Laser;
            public readonly List<float> Angles = new List<float>();
            public readonly List<SurfaceHit> Hits = new List<SurfaceHit>();
            public readonly List<Shot> Shots = new List<Shot>();
            public readonly List<Actor> Targets = new List<Actor>();
            public readonly List<object> ReturnWeapons = new List<object>();
            public readonly List<string> Calls = new List<string>();
            public float CapturedOrigin, CapturedMuzzle;
            public Launch(string type = "shuriken", float blast = 0, bool pierce = false, float delay = 0)
            {
                Type = type; Definition = new RangedProjectileDefinition(new RangedData
                { type = type, radius = .2f, distance = 6, blastRadius = blast, pierce = pierce, returnDelay = delay });
            }
            public void Mark(string name) { Calls.Add(name); Callback?.Invoke(name); }
            public void Fire() { var host = new Host(this); RangedLaunchExecution.Fire(ref host); }
            public List<string> Effects() => Calls.FindAll(name => name.StartsWith("spawn:", StringComparison.Ordinal) ||
                name.StartsWith("assign:", StringComparison.Ordinal) || name.StartsWith("sweep:", StringComparison.Ordinal) ||
                name.StartsWith("receive:", StringComparison.Ordinal) || name.StartsWith("remove:", StringComparison.Ordinal) ||
                name.StartsWith("return:", StringComparison.Ordinal) || name == "fired" || name == "blocked" ||
                name == "detonate" || name == "explosion" || name == "explosion-record");
        }
        struct Host : IRangedLaunchHost
        {
            readonly Launch launch;
            float origin, muzzle, delta;
            SurfaceHit hit;
            Shot shot;
            Actor shooter;
            public Host(Launch launch)
            { this.launch = launch; origin = muzzle = delta = 0; hit = null; shot = null; shooter = null; }
            public void CaptureOrigin()
            { launch.Mark("origin"); launch.CapturedOrigin = origin = launch.Owner.X + launch.Owner.Offset * launch.Direction; }
            public float SpawnX { get { launch.Mark("spawn-x"); return launch.SpawnX; } }
            public float OwnerOffsetX { get { launch.Mark("offset"); return launch.Owner.Offset; } }
            public float OwnerWidth { get { launch.Mark("width"); return launch.Owner.Width; } }
            public float ProjectileRadius { get { launch.Mark("radius"); return launch.Definition.Radius; } }
            public float SpawnGap { get { launch.Mark("gap"); return launch.Gap; } }
            public void CaptureMuzzle(float offsetX)
            { launch.Mark("muzzle"); launch.CapturedMuzzle = muzzle = launch.Owner.X + offsetX * launch.Direction; }
            public string ProjectileType { get { launch.Mark("type"); return launch.Type; } }
            public bool IgnoresTerrain => launch.Definition.IgnoresTerrain;
            public void FireLaser() { launch.Mark("laser"); launch.LaserCalls++; launch.Laser?.Invoke(); }
            public void CaptureMuzzleDelta() { launch.Mark("delta"); delta = muzzle - origin; }
            public int QueryMuzzlePath() { launch.Mark("query"); return launch.Hits.Count; }
            public void SelectMuzzleHit(int index) { launch.Mark("select:" + index); hit = launch.Hits[index]; }
            public bool MuzzleSurfaceExists { get { launch.Mark("surface"); return hit.Surface; } }
            public bool MuzzleSurfaceIsOneWay { get { launch.Mark("one-way"); return hit.OneWay; } }
            public float MuzzleHitDistance { get { launch.Mark("distance"); return hit.Distance; } }
            public float BlastRadius { get { launch.Mark("blast-radius"); return launch.Definition.BlastRadius; } }
            void Spawn(float at, float angle)
            { shot = new Shot(launch, at, angle); launch.Shots.Add(shot); launch.Mark("spawn:" + (launch.Shots.Count - 1)); }
            public void SpawnBlocked(float distance)
                => Spawn(origin + Math.Sign(delta) * Math.Max(0, Math.Min(distance, Math.Abs(delta))), 0);
            public void DetonateShot() { launch.Mark("detonate"); shot.Detonate(); }
            public void RecordBlocked() { launch.Mark("blocked"); launch.BlockedRecords++; }
            public void SpawnAtMuzzle() => Spawn(muzzle, 0);
            public bool HasSourceWeapon { get { launch.Mark("has-source"); return launch.Source != null; } }
            public bool CapturePlayerOwner()
            { launch.Mark("player-owner"); if (!launch.Owner.Player) return false; shooter = launch.Owner; return true; }
            public void AssignSourceWeapon(bool useCurrentPlayerWeapon)
            {
                if (useCurrentPlayerWeapon) launch.Mark("current-weapon");
                shot.Weapon = useCurrentPlayerWeapon ? shooter.Weapon : launch.Source;
                launch.Mark("assign:" + launch.Shots.IndexOf(shot));
            }
            public void SweepShotToMuzzle() => shot.Sweep(muzzle);
            public int AngleCount { get { launch.Mark("count"); return launch.Angles.Count; } }
            public float AngleAt(int index) { launch.Mark("angle:" + index); return launch.Angles[index]; }
            public void SpawnAtOrigin(float angle) => Spawn(origin, angle);
            public void RecordFired(int count) { launch.Mark("fired"); launch.FiredRecords++; launch.RecordedCount = count; }
        }

        [TestCase("shuriken")] [TestCase("shuriken_special")] [TestCase("shuriken_range")]
        [TestCase("shuriken_move")] [TestCase("shuriken_followup")]
        public void EveryShurikenChecksTheWholePathAndABlockedNonBlastNeverCreatesAnImaginaryShot(string type)
        {
            var launch = new Launch(type); var target = new Actor(1, .9f); launch.Targets.Add(target);
            launch.Hits.AddRange(new[] { new SurfaceHit(.5f), new SurfaceHit(.1f) }); launch.Fire();
            Assert.That(launch.Shots, Is.Empty); Assert.That(target.Health, Is.EqualTo(100));
            Assert.That(launch.BlockedRecords, Is.EqualTo(1)); Assert.That(launch.FiredRecords, Is.Zero);
            Assert.That(launch.Calls.FindAll(name => name.StartsWith("select:", StringComparison.Ordinal)), Is.EqualTo(new[] { "select:0", "select:1" }));
            Assert.That(launch.Calls, Does.Not.Contain("has-source")); Assert.That(launch.Calls, Does.Not.Contain("count"));
        }
        [TestCase(-2f, .25f)] [TestCase(.3f, .55f)] [TestCase(4f, 1f)]
        public void BlockedBlastUsesTheNearestSolidClampsItsPositionAndDetonatesBeforeRecording(float distance, float expectedX)
        {
            var launch = new Launch("shuriken_range", blast: 3) { Source = new object(), SpawnX = 1, Damage = 24 };
            var exposed = new Actor(1, .4f); var blocked = new Actor(2, .4f) { Occluded = true };
            launch.Targets.AddRange(new[] { exposed, exposed, blocked, launch.Owner });
            launch.Hits.AddRange(new[] { new SurfaceHit(5), new SurfaceHit(distance), new SurfaceHit(0) { OneWay = true }, new SurfaceHit(0) { Surface = false } });
            launch.Fire(); var shot = launch.Shots[0];
            Assert.That(launch.Shots, Has.Count.EqualTo(1)); Assert.That(shot.Origin, Is.EqualTo(expectedX).Within(.000001f));
            Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Removals, Is.EqualTo(1)); Assert.That(shot.Sweeps, Is.Zero);
            Assert.That(shot.Weapon, Is.Null); Assert.That(exposed.Health, Is.EqualTo(76)); Assert.That(exposed.Hits, Is.EqualTo(1));
            Assert.That(blocked.Health, Is.EqualTo(100)); Assert.That(launch.Owner.Health, Is.EqualTo(100));
            Assert.That(launch.Effects(), Is.EqualTo(new[] { "spawn:0", "detonate", "remove:0", "receive:1", "explosion", "explosion-record", "blocked" }));
            Assert.That(launch.Calls, Does.Not.Contain("player-owner")); Assert.That(launch.FiredRecords, Is.Zero);
        }
        [Test] public void NonSurfacesAndOneWayHitsNeverReadTheirDistanceOrPreventTheMuzzleOverlapHit()
        {
            var launch = new Launch(); var target = new Actor(1, 1); launch.Targets.AddRange(new[] { target, target });
            launch.Hits.AddRange(new[] { new SurfaceHit(0) { Surface = false }, new SurfaceHit(0) { OneWay = true } });
            launch.Callback = name => { if (name == "distance") throw new InvalidOperationException("Ineligible hit distance"); };
            launch.Fire(); var shot = launch.Shots[0];
            Assert.That(target.Health, Is.EqualTo(94)); Assert.That(target.Hits, Is.EqualTo(1));
            Assert.That(shot.Origin, Is.EqualTo(launch.CapturedMuzzle)); Assert.That(shot.Execution.Removed, Is.True);
            Assert.That(launch.RecordedCount, Is.EqualTo(1)); Assert.That(launch.BlockedRecords, Is.Zero);
            Assert.That(launch.Effects(), Is.EqualTo(new[] { "spawn:0", "assign:0", "sweep:0", "receive:1", "remove:0", "fired" }));
        }
        [TestCase(false)] [TestCase(true)]
        public void ReturnVisitSeesTheWeaponAssignedAfterSpawnAndBeforeImmediateContact(bool explicitSource)
        {
            var launch = new Launch("shuriken_move", delay: 5); var target = new Actor(1, 1); launch.Targets.Add(target);
            var previous = launch.Owner.Weapon; var current = new object(); var source = new object();
            if (explicitSource) launch.Source = source;
            launch.Callback = name => { if (name == "spawn:0") launch.Owner.Weapon = current; };
            launch.Fire();
            Assert.That(target.Health, Is.EqualTo(94)); Assert.That(launch.ReturnWeapons, Has.Count.EqualTo(1));
            Assert.That(launch.ReturnWeapons[0], Is.SameAs(explicitSource ? source : current));
            Assert.That(launch.Shots[0].Weapon, Is.Not.SameAs(previous));
            Assert.That(launch.Effects(), Is.EqualTo(new[] { "spawn:0", "assign:0", "sweep:0", "receive:1", "return:1", "remove:0", "fired" }));
            Assert.That(launch.Calls.Contains("player-owner"), Is.EqualTo(!explicitSource));
            Assert.That(launch.Calls.Contains("current-weapon"), Is.EqualTo(!explicitSource));
        }
        [Test] public void NonPlayerOwnerDoesNotReadAWeaponAndExplicitMuzzleSkipsFallbackBodyGeometry()
        {
            var launch = new Launch { SpawnX = 3, Direction = -1 }; launch.Owner.Player = false;
            launch.Callback = name => { if (name == "offset" || name == "width" || name == "radius" || name == "gap" || name == "current-weapon") throw new InvalidOperationException(name); };
            launch.Fire();
            Assert.That(launch.CapturedOrigin, Is.EqualTo(-.25f)); Assert.That(launch.CapturedMuzzle, Is.EqualTo(-3));
            Assert.That(launch.Shots[0].Weapon, Is.Null); Assert.That(launch.Shots[0].Origin, Is.EqualTo(-3));
        }
        [Test] public void OriginAndMuzzleReadOwnerSeparatelyBeforeLaserDispatchWithoutRangedWork()
        {
            var launch = new Launch("laser") { SpawnX = 2 }; var target = new Actor(1);
            launch.Callback = name => { if (name == "spawn-x") launch.Owner.X = 8; };
            launch.Laser = () => { target.Health -= 12; };
            launch.Fire();
            Assert.That(launch.CapturedOrigin, Is.EqualTo(.25f)); Assert.That(launch.CapturedMuzzle, Is.EqualTo(10));
            Assert.That(target.Health, Is.EqualTo(88)); Assert.That(launch.LaserCalls, Is.EqualTo(1));
            Assert.That(launch.Shots, Is.Empty); Assert.That(launch.FiredRecords, Is.Zero); Assert.That(launch.BlockedRecords, Is.Zero);
            Assert.That(launch.Calls, Is.EqualTo(new[] { "origin", "spawn-x", "spawn-x", "muzzle", "type", "laser" }));
        }
        [TestCase("shotgun")] [TestCase("launcher_ball")] [TestCase("Shuriken")] [TestCase(null)]
        public void OtherTypesSpawnAuthoredAnglesInOrderAtCapturedOriginAndSweepEachBeforeTheNext(string type)
        {
            var launch = new Launch(type) { SpawnX = 1, Source = new object() }; launch.Angles.AddRange(new[] { 20f, -10f, 0f });
            var target = new Actor(1, 1); launch.Targets.AddRange(new[] { target, target });
            launch.Callback = name => { if (name == "receive:1") launch.Owner.X += 7; };
            launch.Fire();
            Assert.That(target.Health, Is.EqualTo(82)); Assert.That(target.Hits, Is.EqualTo(3));
            Assert.That(launch.Shots.ConvertAll(shot => shot.Angle), Is.EqualTo(new[] { 20f, -10f, 0f }));
            foreach (var shot in launch.Shots)
            { Assert.That(shot.Origin, Is.EqualTo(.25f)); Assert.That(shot.SweepDestination, Is.EqualTo(1)); Assert.That(shot.Weapon, Is.Null); }
            Assert.That(launch.RecordedCount, Is.EqualTo(3)); Assert.That(launch.Calls, Does.Not.Contain("query"));
            Assert.That(launch.Calls, Does.Not.Contain("has-source"));
            Assert.That(launch.Effects(), Is.EqualTo(new[] { "spawn:0", "sweep:0", "receive:1", "remove:0", "spawn:1", "sweep:1", "receive:1", "remove:1", "spawn:2", "sweep:2", "receive:1", "remove:2", "fired" }));
        }
        [Test] public void AngleLoopReadsCurrentCountAndValuesAfterEachSynchronousSweep()
        {
            var launch = new Launch("shotgun"); launch.Angles.Add(20);
            launch.Callback = name => { if (name == "sweep:0") { launch.Angles[0] = 90; launch.Angles.Add(-15); } };
            launch.Fire();
            Assert.That(launch.Shots.ConvertAll(shot => shot.Angle), Is.EqualTo(new[] { 20f, -15f }));
            Assert.That(launch.RecordedCount, Is.EqualTo(2)); Assert.That(launch.Calls.FindAll(name => name == "count"), Has.Count.EqualTo(4));
        }
        [Test] public void EmptyAngleListRecordsZeroWithoutCreatingAProjectile()
        {
            var launch = new Launch("shotgun"); launch.Fire();
            Assert.That(launch.Shots, Is.Empty); Assert.That(launch.RecordedCount, Is.Zero); Assert.That(launch.FiredRecords, Is.EqualTo(1));
        }
        [Test] public void NestedLaunchDuringSweepKeepsTheOuterShotAndMuzzleLocal()
        {
            var launch = new Launch("shotgun") { SpawnX = 1 }; launch.Angles.AddRange(new[] { -20f, 20f });
            var nested = new Launch { SpawnX = 3 }; var target = new Actor(1, 1); var nestedTarget = new Actor(2, 3);
            launch.Targets.Add(target); nested.Targets.Add(nestedTarget);
            launch.Callback = name => { if (name == "sweep:0") nested.Fire(); }; launch.Fire();
            Assert.That(target.Health, Is.EqualTo(88)); Assert.That(nestedTarget.Health, Is.EqualTo(94));
            Assert.That(launch.Shots, Has.Count.EqualTo(2)); Assert.That(nested.Shots, Has.Count.EqualTo(1));
            Assert.That(launch.Shots[1].SweepDestination, Is.EqualTo(1)); Assert.That(nested.Shots[0].SweepDestination, Is.EqualTo(3));
        }
        [TestCase("query")] [TestCase("spawn:0")] [TestCase("current-weapon")] [TestCase("receive:1")]
        public void ErrorsPropagateWithoutCompensatingRemovalOrSuccessRecords(string boundary)
        {
            var launch = new Launch(pierce: true); var target = new Actor(1, 1); launch.Targets.AddRange(new[] { target, target });
            var failure = new InvalidOperationException(boundary); launch.Callback = name => { if (name == boundary) throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => launch.Fire()), Is.SameAs(failure));
            Assert.That(launch.FiredRecords, Is.Zero); Assert.That(launch.BlockedRecords, Is.Zero);
            if (boundary == "query") { Assert.That(launch.Shots, Is.Empty); Assert.That(target.Health, Is.EqualTo(100)); return; }
            var shot = launch.Shots[0]; Assert.That(shot.Execution.Removed, Is.False); Assert.That(shot.Removals, Is.Zero);
            Assert.That(target.Health, Is.EqualTo(boundary == "receive:1" ? 94 : 100));
            if (boundary != "receive:1") { Assert.That(shot.Weapon, Is.Null); Assert.That(shot.Sweeps, Is.Zero); return; }
            launch.Callback = null; shot.Sweep(1);
            Assert.That(target.Health, Is.EqualTo(94)); Assert.That(target.Hits, Is.EqualTo(1), "The already-entered hit claim is not rolled back on Receive failure.");
        }
        [Test] public void BlockedDetonationFailureKeepsItsTerminalClaimAndDamageButSkipsBlockedRecord()
        {
            var launch = new Launch("shuriken_range", blast: 3); var target = new Actor(1, .4f); launch.Targets.Add(target);
            launch.Hits.Add(new SurfaceHit(.2f)); var failure = new InvalidOperationException("Receive");
            launch.Callback = name => { if (name == "receive:1") throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => launch.Fire()), Is.SameAs(failure));
            var shot = launch.Shots[0]; Assert.That(shot.Execution.Removed, Is.True); Assert.That(shot.Removals, Is.EqualTo(1));
            Assert.That(target.Health, Is.EqualTo(94)); Assert.That(shot.Weapon, Is.Null); Assert.That(shot.Sweeps, Is.Zero);
            Assert.That(launch.BlockedRecords, Is.Zero); Assert.That(launch.Explosions, Is.Zero);
            launch.Callback = null; shot.Detonate(); Assert.That(target.Health, Is.EqualTo(94)); Assert.That(launch.Explosions, Is.Zero);
        }
        [Test] public void AngleSweepFailureLeavesEarlierPelletsAndTheFailingPelletWithoutLaunchingLaterAngles()
        {
            var launch = new Launch("shotgun"); launch.Angles.AddRange(new[] { -20f, 0f, 20f });
            var failure = new InvalidOperationException("second sweep"); launch.Callback = name => { if (name == "sweep:1") throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => launch.Fire()), Is.SameAs(failure));
            Assert.That(launch.Shots, Has.Count.EqualTo(2)); Assert.That(launch.Shots[0].Sweeps, Is.EqualTo(1));
            Assert.That(launch.Shots[1].Sweeps, Is.EqualTo(1)); Assert.That(launch.Shots[1].Removals, Is.Zero); Assert.That(launch.FiredRecords, Is.Zero);
        }

        sealed class Bomb
        {
            public float Origin, Destination, Multiplier = 1;
            public int Sweeps;
        }
        sealed class BombLaunch
        {
            public float X = 4, Offset = .25f, Width = 1, Radius = .2f, Gap = .05f;
            public int Direction = -1;
            public Bomb Spawned;
            public Action<string> Callback;
            public readonly List<string> Calls = new List<string>();
            public void Mark(string name) { Calls.Add(name); Callback?.Invoke(name); }
            public Bomb Fire(float multiplier = 1)
            { var host = new BombHost(this); RangedLaunchExecution.SpawnBomb(multiplier, ref host); return host.Bomb; }
        }
        struct BombHost : IBombLaunchHost
        {
            readonly BombLaunch launch;
            float origin;
            public Bomb Bomb { get; private set; }
            public BombHost(BombLaunch launch) { this.launch = launch; origin = 0; Bomb = null; }
            public void CaptureRadians() => launch.Mark("radians");
            public void CaptureOrigin() { launch.Mark("origin"); origin = launch.X + launch.Offset * launch.Direction; }
            public void Spawn() { Bomb = launch.Spawned = new Bomb { Origin = origin }; launch.Mark("spawn"); }
            public void SetFragmentMultiplier(float multiplier) { Bomb.Multiplier = multiplier; launch.Mark("multiplier"); }
            public float OwnerWidth { get { launch.Mark("width"); return launch.Width; } }
            public float ProjectileRadius { get { launch.Mark("radius"); return launch.Radius; } }
            public float SpawnGap { get { launch.Mark("gap"); return launch.Gap; } }
            public void SweepToMuzzle(float distance)
            { Bomb.Destination = origin + launch.Direction * distance; Bomb.Sweeps++; launch.Mark("sweep"); }
        }
        [Test] public void BombReturnsTheActualSpawnAfterMultiplierAndImmediateSweepWithCurrentOwnerWidth()
        {
            var launch = new BombLaunch();
            launch.Callback = name => { if (name == "spawn") { launch.X = 90; launch.Width = 3; } };
            var result = launch.Fire(1.5f);
            Assert.That(result, Is.SameAs(launch.Spawned)); Assert.That(result.Origin, Is.EqualTo(3.75f));
            Assert.That(result.Destination, Is.EqualTo(2)); Assert.That(result.Multiplier, Is.EqualTo(1.5f)); Assert.That(result.Sweeps, Is.EqualTo(1));
            Assert.That(launch.Calls, Is.EqualTo(new[] { "radians", "origin", "spawn", "multiplier", "width", "radius", "gap", "sweep" }));
        }
        [TestCase("radians")] [TestCase("origin")] [TestCase("spawn")] [TestCase("multiplier")] [TestCase("sweep")]
        public void BombErrorsLeaveTheNativePartialObjectAndDoNotReturnAReplacement(string boundary)
        {
            var launch = new BombLaunch(); var failure = new InvalidOperationException(boundary); Bomb result = null;
            launch.Callback = name => { if (name == boundary) throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => result = launch.Fire(1.5f)), Is.SameAs(failure));
            Assert.That(result, Is.Null);
            if (boundary == "radians" || boundary == "origin") { Assert.That(launch.Spawned, Is.Null); return; }
            Assert.That(launch.Spawned.Multiplier, Is.EqualTo(boundary == "spawn" ? 1 : 1.5f));
            Assert.That(launch.Spawned.Sweeps, Is.EqualTo(boundary == "sweep" ? 1 : 0));
        }
    }
}
