using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CastActivationExecutionTests
    {
        sealed class Target
        {
            public readonly int Id;
            public bool Exists = true, Dead;
            public Target(int id) { Id = id; }
        }
        sealed class Spawn
        {
            public string Kind;
            public float X, Y, Damage;
            public int Direction;
            public SkillEffectDefinition Effect;
        }
        sealed class Source : IActionResourceSource
        {
            readonly State state;
            public int Calls;
            public Source(State state) { this.state = state; }
            public ActionResourceResult TryActivate(ActionResourceKey key, float now)
            {
                Calls++; state.Signal("consume");
                if (state.Reject) return new ActionResourceResult(ActionResourceStatus.Rejected, reason: "fixture rejection");
                state.Charges--;
                return new ActionResourceResult(ActionResourceStatus.Succeeded, new ActionResourceSnapshot(key, now, state.StatMultiplier));
            }
        }
        sealed class State
        {
            public readonly SkillExecutionDefinition Definition;
            public readonly Source Source;
            public readonly ActionResourceExecution Resources;
            public CastLifecycleExecution Lifecycle;
            public RangedRecoilExecution Recoil;
            public SmokeEffectExecution Smoke;
            public readonly List<string> Events = new List<string>();
            public readonly List<Spawn> Spawns = new List<Spawn>();
            public Target CircleTarget = new Target(1), BoxTarget = new Target(2), FirstHitTarget = new Target(3), Marked;
            public float Now = .375f, Started = .125f, PositionX = 2, PositionY = 4;
            public float SavedX = -10, SavedY = -20, InstallX = -30, InstallY = -40;
            public float StatMultiplier = 1.5f, Multiplier, VerticalVelocity = -7;
            public float StealthUntil, Shield, GuardUntil, GuardReduction, FloorDistance, CircleRadius;
            public float QueryX, QueryY;
            public float? Floor = 3;
            public int Direction = -1, Charges = 3, Failures, Records, Replacements;
            public bool Grounded = true, Solid, Occluded, Reject, PreviousSmokeActive = true;
            public Action<string> Callback;
            public State(SkillData input)
            {
                Definition = new SkillExecutionDefinition(input);
                Lifecycle = new CastLifecycleExecution(Definition.Activation, Definition.End);
                Smoke = new SmokeEffectExecution(Definition.Effect);
                Source = new Source(this);
                Resources = new ActionResourceExecution(new ActionResourceKey(Guid.NewGuid(), Guid.NewGuid(), false), Source);
            }
            public void Signal(string operation) { Events.Add(operation); Callback?.Invoke(operation); }
            public void Activate() { var host = new Host(this); CastActivationExecution.Activate(Definition, ref host); }
            public void Before(float step = 1) { var host = new LifetimeHost(this); Lifecycle.BeforeMovement(step, Definition, null, ref host); }
            public void Cancel() { var host = new LifetimeHost(this); Lifecycle.Cancel(ref host); }
            public void Spawn(string kind)
            {
                Spawns.Add(new Spawn { Kind = kind, X = PositionX, Y = PositionY, Direction = Direction, Damage = Definition.Hits[0].Damage * Multiplier });
                Signal(kind);
            }
        }
        struct Host : ICastActivationHost
        {
            readonly State state;
            public Host(State state) { this.state = state; }
            public bool ProbeGround() { state.Signal("probe"); return state.Grounded; }
            public void SelectTeleportTargetInCircle(float radius)
            { state.CircleRadius = radius; state.Marked = state.CircleTarget; state.Signal("circle"); }
            public void SelectTeleportTargetInBox() { state.Marked = state.BoxTarget; state.Signal("target-box"); }
            public void SelectFirstHitTarget() { state.Marked = state.FirstHitTarget; state.Signal("nearest-hit"); }
            public bool MarkedTargetExists { get { state.Signal("target-exists"); return state.Marked != null && state.Marked.Exists; } }
            public void SaveOwnerPose() { state.SavedX = state.PositionX; state.SavedY = state.PositionY; state.Signal("save"); }
            public void CaptureInstallCenter(float x, float y)
            { state.InstallX = state.PositionX + x * state.Direction; state.InstallY = state.PositionY + y; state.Signal("install"); }
            public float? FindInstallFloor(float distance) { state.FloorDistance = distance; state.Signal("floor"); return state.Floor; }
            public void SetInstallHeight(float height) { state.InstallY = height; state.Signal("floor-height"); }
            public bool InstallationOverlapsSolid(float width, float height, float inset)
            {
                state.QueryX = state.InstallX; state.QueryY = state.InstallY;
                state.Signal("solid"); return state.Solid;
            }
            public bool InstallationOccluded { get { state.Signal("occluded"); return state.Occluded; } }
            public bool CommitResources()
            {
                state.Signal("commit"); var result = state.Resources.TryActivate(state.Resources.Key.ExecutionId, state.Now);
                if (result.Status != ActionResourceStatus.Succeeded) return false;
                state.Multiplier = result.Snapshot.DamageMultiplier; return true;
            }
            public void Fail() { var host = new LifetimeHost(state); state.Lifecycle.Fail(ref host); }
            public void ClaimActivation() { state.Lifecycle.ClaimActivation(); state.Signal("claim"); }
            public void LaunchMissile() => state.Spawn("missile");
            public void ReleaseDueBombs() => state.Spawn("bomb");
            public void FireRanged() => state.Spawn("ranged");
            public bool OwnerGrounded { get { state.Signal("grounded"); return state.Grounded; } }
            public float BeginRangedRecoil(float duration, float hopHeight)
            {
                float hop = state.Recoil.Begin(state.Now, state.Direction, duration, hopHeight, 66.6666667f, 104.1666667f);
                state.Signal("recoil"); return hop;
            }
            public void SetOwnerVerticalVelocity(float velocity) { state.VerticalVelocity = velocity; state.Signal("velocity"); }
            public void ActivateSkillWave() => state.Spawn("wave");
            public float Now { get { state.Signal("now"); return state.Now; } }
            public void SetStealthUntil(float until) { state.StealthUntil = until; state.Signal("stealth"); }
            public void SetShield(float hp) { state.Shield = hp; state.Signal("shield"); }
            public float GuardUntil { get { state.Signal("guard-read"); return state.GuardUntil; } }
            public void SetGuardUntil(float until) { state.GuardUntil = until; state.Signal("guard"); }
            public void SetGuardReduction(float reduction) { state.GuardReduction = reduction; state.Signal("reduction"); }
            public void ReplaceSmoke() { state.PreviousSmokeActive = false; state.Replacements++; state.Signal("replace"); }
            public float Activation { get { state.Signal("activation"); return state.Lifecycle.Activation; } }
            public void ActivateSmoke(float activation) { state.Smoke.Activate(activation); state.Signal("smoke"); }
            public void SpawnDecoy(float centerToFeet)
            {
                state.Spawns.Add(new Spawn { Kind = "decoy", X = state.InstallX, Y = state.InstallY - centerToFeet, Effect = state.Definition.Effect });
                state.Signal("decoy");
            }
            public void RecordActivation() { state.Records++; state.Signal("record"); }
        }
        readonly struct LifetimeHost : ICastLifecycleHost
        {
            readonly State state;
            public LifetimeHost(State state) { this.state = state; }
            public float Now => state.Now;
            public float Elapsed => state.Now - state.Started;
            public bool ChargeHeld => false;
            public bool Approaching => false;
            public void AdvanceApproach(float step) => throw new InvalidOperationException("No approach in activation fixture.");
            public void Activate() => state.Activate();
            public void PrepareMissile(float step) => throw new InvalidOperationException("No missile step in activation fixture.");
            public void ReleaseDueBombs() => throw new InvalidOperationException("No bomb step in activation fixture.");
            public void PrepareTeleport(float step)
            {
                if (state.Definition.Movement != null) throw new InvalidOperationException("No movement step in activation fixture.");
            }
            public void ReleaseResources() { state.Resources.Dispose(); state.Signal("release"); }
            public bool CancelMeleeAction() => false;
            public void Failed() { state.Failures++; state.Signal("fail"); }
            public bool MissileEnded => false;
            public void Complete() => throw new InvalidOperationException("No completion in activation fixture.");
            public float SmokeEnd => state.Smoke.End;
            public float RetainSkillWaveUntil(float last, float elapsed, float step) => throw new InvalidOperationException("No wave step in activation fixture.");
            public float RecastRemaining => 0;
            public float RecastDeadline => 0;
            public void Dispose() => state.Resources.Dispose();
        }
        static SkillData Input(string kind = null, string effect = null)
        {
            return new SkillData { id = "activation_fixture", kind = kind, activation = .25f, end = 2,
                hits = new[] { new HitData { damage = 8, targets = "all", box = new BoxData { width = 2, height = 3, x = 1, y = 2 } } },
                effect = effect == null ? null : new EffectData { type = effect, x = 3, y = 1.5f, duration = 2.5f, hp = 30, reduction = .4f },
                ranged = new RangedData { recoilDuration = .2f, recoilHopHeight = .5f } };
        }
        static State Installation(string effect = "decoy") => new State(Input(effect: effect));
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static void UncommittedFailure(State state)
        {
            Assert.That(state.Source.Calls, Is.Zero); Assert.That(state.Charges, Is.EqualTo(3));
            Assert.That(state.Resources.Current, Is.Null); Assert.That(state.Resources.IsActive, Is.False);
            Assert.That(state.Lifecycle.Activated, Is.False); Assert.That(state.Lifecycle.Cancelled, Is.True);
            Assert.That(state.Lifecycle.MotionEnd, Is.EqualTo(state.Now - state.Started + .1f));
            Assert.That(state.Failures, Is.EqualTo(1)); Assert.That(state.Records, Is.Zero); Assert.That(state.Spawns, Is.Empty);
        }
        static void Committed(State state)
        {
            Assert.That(state.Source.Calls, Is.EqualTo(1)); Assert.That(state.Charges, Is.EqualTo(2));
            Assert.That(state.Resources.Current.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
            Assert.That(state.Multiplier, Is.EqualTo(1.5f)); Assert.That(state.Lifecycle.Activated, Is.True);
        }

        [Test] public void GroundFailureStopsBeforeTargetPosePlacementOrConsumption()
        {
            var input = Input("ranged", "decoy"); input.vertical = "grounded_required";
            input.movement = new MoveData { type = "target_teleport_strike", targetRadius = 4 };
            input.hits[0].targets = "nearest";
            var state = new State(input) { Grounded = false }; state.Activate(); UncommittedFailure(state);
            Assert.That(state.SavedX, Is.EqualTo(-10)); Assert.That(state.InstallY, Is.EqualTo(-40)); Assert.That(state.Marked, Is.Null);
            Assert.That(state.Events, Is.EqualTo(new[] { "probe", "release", "fail" }));
        }
        [Test] public void RequiredGroundUsesTheCurrentProbeAndNormalVerticalDoesNotProbe()
        {
            var input = Input(); input.vertical = "grounded_required"; var state = new State(input) { Grounded = false };
            state.Callback = operation => { if (operation == "probe") state.Grounded = true; };
            state.Activate(); Committed(state); Assert.That(state.Failures, Is.Zero);
            state = new State(Input()) { Grounded = false }; state.Activate(); Committed(state);
            Assert.That(state.Events, Does.Not.Contain("probe"));
        }
        [TestCase(4, true)] [TestCase(0, false)] [TestCase(-1, false)] [TestCase(float.NaN, false)]
        public void TeleportSelectionKeepsTheActualReferenceAndExistingRadiusBranch(float radius, bool circle)
        {
            var input = Input(); input.movement = new MoveData { type = "target_teleport_strike", targetRadius = radius };
            var state = new State(input); var expected = circle ? state.CircleTarget : state.BoxTarget; expected.Dead = true;
            state.Activate(); Committed(state);
            Assert.That(state.Marked, Is.SameAs(expected), "Admission checks Native existence, not a new death or hit rule.");
            Assert.That(state.Marked.Dead, Is.True); Assert.That(state.Events, Does.Contain(circle ? "circle" : "target-box"));
            Assert.That(state.Events, Does.Not.Contain(circle ? "target-box" : "circle"));
            if (circle) Assert.That(Bits(state.CircleRadius), Is.EqualTo(Bits(radius)));
        }
        // A missing target no longer refuses activation: the skill spends its charge and whiffs in place.
        [Test] public void MissingTeleportTargetStillActivatesInPlace()
        {
            var input = Input(); input.movement = new MoveData { type = "target_teleport_strike", targetRadius = 4 };
            var state = new State(input) { CircleTarget = null }; state.Activate(); Committed(state);
            Assert.That(state.Marked, Is.Null); Assert.That(state.Failures, Is.Zero); Assert.That(state.SavedX, Is.EqualTo(2));
            Assert.That(state.Events.GetRange(0, 2), Is.EqualTo(new[] { "circle", "save" }));
        }
        [Test] public void NearestHitOverridesTheTeleportReferenceAndAMissingHitWhiffs()
        {
            var input = Input(); input.movement = new MoveData { type = "target_teleport_strike", targetRadius = 4 }; input.hits[0].targets = "nearest";
            var state = new State(input); state.Activate(); Committed(state);
            Assert.That(state.Marked, Is.SameAs(state.FirstHitTarget)); Assert.That(state.Marked, Is.Not.SameAs(state.CircleTarget));
            Assert.That(state.Events.GetRange(0, 3), Is.EqualTo(new[] { "circle", "nearest-hit", "save" }));
            state = new State(input) { FirstHitTarget = null }; state.Activate(); Committed(state);
            Assert.That(state.Marked, Is.Null); Assert.That(state.Failures, Is.Zero); Assert.That(state.SavedX, Is.EqualTo(2));
        }
        [Test] public void LostNearestTargetStillActivatesAndWhiffs()
        {
            var input = Input(); input.hits[0].targets = "nearest"; var state = new State(input);
            state.Callback = operation => { if (operation == "nearest-hit") state.FirstHitTarget.Exists = false; };
            state.Activate(); Committed(state); Assert.That(state.Marked, Is.SameAs(state.FirstHitTarget));
            Assert.That(state.Failures, Is.Zero); Assert.That(state.Events, Does.Contain("save"));
        }
        [Test] public void SavedPoseFollowsTargetCallbacksAndInstallationReadsTheOwnerAgain()
        {
            var input = Input(effect: "delayed_world_sword"); input.hits[0].targets = "nearest"; var state = new State(input);
            state.Callback = operation =>
            {
                if (operation == "nearest-hit") { state.PositionX = 7; state.PositionY = 9; }
                if (operation == "save") { state.PositionX = 11; state.PositionY = 13; state.Direction = 1; }
            };
            state.Activate(); Committed(state);
            Assert.That(state.SavedX, Is.EqualTo(7)); Assert.That(state.SavedY, Is.EqualTo(9));
            Assert.That(state.InstallX, Is.EqualTo(14)); Assert.That(state.InstallY, Is.EqualTo(14.5f));
            Assert.That(state.QueryX, Is.EqualTo(14)); Assert.That(state.QueryY, Is.EqualTo(14.5f));
            Assert.That(state.Events, Does.Not.Contain("floor"));
        }
        [Test] public void MissingCurrentDecoyFloorFailsAfterSavingTheCandidateWithoutConsuming()
        {
            var state = Installation(); state.Floor = null; state.Activate(); UncommittedFailure(state);
            Assert.That(state.SavedX, Is.EqualTo(2)); Assert.That(state.SavedY, Is.EqualTo(4));
            Assert.That(state.InstallX, Is.EqualTo(-1)); Assert.That(state.InstallY, Is.EqualTo(5.5f));
            Assert.That(state.FloorDistance, Is.EqualTo(3)); Assert.That(state.Events, Does.Not.Contain("solid").And.Not.Contain("occluded"));
        }
        [Test] public void FloorCallbackUsesTheCurrentInstallXAndTheReturnedFloorPlusThreeQuarters()
        {
            var state = Installation(); state.Callback = operation =>
            {
                if (operation == "floor") { state.InstallX = 12; state.InstallY = 99; state.Floor = 6.125f; }
            };
            state.Activate(); Committed(state);
            Assert.That(state.InstallX, Is.EqualTo(12)); Assert.That(Bits(state.InstallY), Is.EqualTo(Bits(6.125f + .75f)));
            Assert.That(state.QueryX, Is.EqualTo(12)); Assert.That(state.QueryY, Is.EqualTo(6.875f));
            Assert.That(state.Spawns[0].Kind, Is.EqualTo("decoy")); Assert.That(state.Spawns[0].X, Is.EqualTo(12));
            Assert.That(Bits(state.Spawns[0].Y), Is.EqualTo(Bits((6.125f + .75f) - .75f)));
        }
        [TestCase("decoy", true)] [TestCase("delayed_world_sword", true)]
        [TestCase("decoy", false)] [TestCase("delayed_world_sword", false)]
        public void PlacementRejectionConsumesNothingAndSolidOverlapShortCircuitsOcclusion(string effect, bool solid)
        {
            var state = Installation(effect); state.Solid = solid; state.Occluded = true;
            if (solid) state.Callback = operation => { if (operation == "occluded") throw new InvalidOperationException("Must short circuit."); };
            state.Activate(); UncommittedFailure(state);
            Assert.That(state.SavedX, Is.EqualTo(2)); Assert.That(state.InstallX, Is.EqualTo(-1));
            Assert.That(state.Events.Contains("occluded"), Is.EqualTo(!solid));
        }
        [TestCase(null)] [TestCase("stealth")] [TestCase("smoke")] [TestCase("owner_guard")] [TestCase("projectile")] [TestCase("other")]
        public void OtherEffectsDoNotInventInstallationAdmission(string effect)
        {
            var state = new State(Input(effect: effect)) { Solid = true, Occluded = true, Floor = null };
            state.Activate(); Committed(state); Assert.That(state.InstallX, Is.EqualTo(-30)); Assert.That(state.InstallY, Is.EqualTo(-40));
            Assert.That(state.Events, Does.Not.Contain("floor").And.Not.Contain("solid").And.Not.Contain("occluded"));
        }
        [TestCase(false)] [TestCase(true)]
        public void RejectedAndThrowingResourceSourcesLeavePlacementButNoActivationOrEffects(bool throws)
        {
            var state = Installation(); state.Reject = !throws;
            if (throws) state.Callback = operation => { if (operation == "consume") throw new InvalidOperationException("resource callback"); };
            state.Activate();
            Assert.That(state.Source.Calls, Is.EqualTo(1)); Assert.That(state.Charges, Is.EqualTo(3)); Assert.That(state.Multiplier, Is.Zero);
            Assert.That(state.Resources.Current.Status, Is.EqualTo(throws ? ActionResourceStatus.Failed : ActionResourceStatus.Rejected));
            Assert.That(state.Resources.IsActive, Is.False); Assert.That(state.Lifecycle.Cancelled, Is.True); Assert.That(state.Lifecycle.Activated, Is.False);
            Assert.That(state.SavedX, Is.EqualTo(2)); Assert.That(state.InstallY, Is.EqualTo(3.75f));
            Assert.That(state.Spawns, Is.Empty); Assert.That(state.Records, Is.Zero); Assert.That(state.Failures, Is.EqualTo(1));
        }
        [TestCase(null)] [TestCase("missile")] [TestCase("bomb")] [TestCase("ranged")]
        public void ResourceSnapshotAndLifecycleClaimPrecedeEveryKindSpawnAndProjectileEffect(string kind)
        {
            var state = new State(Input(kind, "projectile")); state.Callback = operation =>
            {
                if (operation == "consume") Assert.That(state.Lifecycle.Activated, Is.False);
                if (operation == "claim" || operation == "missile" || operation == "bomb" || operation == "ranged" || operation == "wave")
                { Assert.That(state.Resources.Current.Status, Is.EqualTo(ActionResourceStatus.Succeeded)); Assert.That(state.Lifecycle.Activated, Is.True); }
            };
            state.Activate(); Committed(state);
            Assert.That(state.Spawns.Count, Is.EqualTo(kind == null ? 1 : 2)); Assert.That(state.Spawns[0].Kind, Is.EqualTo(kind ?? "wave"));
            Assert.That(state.Spawns[state.Spawns.Count - 1].Kind, Is.EqualTo("wave"));
            foreach (var spawn in state.Spawns) { Assert.That(spawn.Damage, Is.EqualTo(12)); Assert.That(spawn.Direction, Is.EqualTo(-1)); }
            Assert.That(state.Events.IndexOf("consume"), Is.LessThan(state.Events.IndexOf("claim")));
            Assert.That(state.Events.IndexOf("claim"), Is.LessThan(state.Events.IndexOf(kind ?? "wave")));
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo("record")); Assert.That(state.Records, Is.EqualTo(1));
        }
        [Test] public void ResourceCallbackChangesAreLiveForSpawnButNotTheSavedPoseOrCapturedResourceTime()
        {
            var state = new State(Input("ranged", "stealth")); state.Grounded = false;
            state.Callback = operation =>
            {
                if (operation == "consume") { state.Now = 4; state.PositionX = 20; state.Direction = 1; }
                if (operation == "ranged") { state.Now = 5; state.Direction = -1; }
                if (operation == "velocity") state.Now = 6;
            };
            state.Activate(); Committed(state);
            Assert.That(state.SavedX, Is.EqualTo(2)); Assert.That(state.Spawns[0].X, Is.EqualTo(20)); Assert.That(state.Spawns[0].Direction, Is.EqualTo(1));
            Assert.That(state.Resources.Current.Snapshot.ActivatedAt, Is.EqualTo(.375f));
            Assert.That(state.Recoil.Read().StartedAt, Is.EqualTo(5)); Assert.That(state.Recoil.Read().Direction, Is.EqualTo(-1));
            Assert.That(state.StealthUntil, Is.EqualTo(8.5f)); Assert.That(state.VerticalVelocity, Is.GreaterThan(0));
            Assert.That(state.Events.IndexOf("ranged"), Is.LessThan(state.Events.IndexOf("recoil")));
            Assert.That(state.Events.IndexOf("velocity"), Is.LessThan(state.Events.IndexOf("stealth")));
        }
        [TestCase(.2f, true)] [TestCase(0, false)] [TestCase(-1, false)] [TestCase(float.NaN, false)]
        public void RecoilDurationShortCircuitsTheCurrentGroundedRead(float duration, bool readsGround)
        {
            var input = Input("ranged"); input.ranged.recoilDuration = duration; var state = new State(input) { Grounded = true };
            state.Activate(); Committed(state); Assert.That(state.Events.Contains("grounded"), Is.EqualTo(readsGround));
            Assert.That(state.Recoil.Read().StartedAt, Is.EqualTo(-1)); Assert.That(state.VerticalVelocity, Is.EqualTo(-7));
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)]
        public void NonpositiveOrUnorderedHopStillBeginsAirRecoilWithoutReplacingVerticalVelocity(float hopHeight)
        {
            var input = Input("ranged"); input.ranged.recoilHopHeight = hopHeight; var state = new State(input) { Grounded = false };
            state.Activate(); Committed(state); Assert.That(state.Recoil.Read().StartedAt, Is.EqualTo(.375f));
            Assert.That(state.Recoil.Read().Duration, Is.EqualTo(.2f)); Assert.That(state.VerticalVelocity, Is.EqualTo(-7));
            Assert.That(state.Events, Does.Not.Contain("velocity"));
        }
        [Test] public void RangedSpawnMayLandOrCancelBeforeRecoilWithoutReadmission()
        {
            var state = new State(Input("ranged", "stealth")) { Grounded = false };
            state.Callback = operation => { if (operation == "ranged") { state.Grounded = true; state.VerticalVelocity = 9; state.Cancel(); } };
            state.Activate(); Committed(state); Assert.That(state.Lifecycle.Cancelled, Is.True); Assert.That(state.Resources.IsActive, Is.False);
            Assert.That(state.Recoil.Read().StartedAt, Is.EqualTo(-1)); Assert.That(state.VerticalVelocity, Is.EqualTo(9));
            Assert.That(state.StealthUntil, Is.EqualTo(.375f + 2.5f)); Assert.That(state.Records, Is.EqualTo(1));
            Assert.That(state.Failures, Is.Zero); Assert.That(state.Spawns.Count, Is.EqualTo(1));
        }
        [Test] public void AirRecoilAndEffectsContinueAfterSpawnCallbackCancelsTheEnteredActivation()
        {
            var state = new State(Input("ranged", "stealth")) { Grounded = false };
            state.Callback = operation => { if (operation == "ranged") { state.Cancel(); state.Now = 7; } };
            state.Activate(); Committed(state); Assert.That(state.Lifecycle.Cancelled, Is.True); Assert.That(state.Lifecycle.MotionDone, Is.True);
            Assert.That(state.Recoil.Read().StartedAt, Is.EqualTo(7)); Assert.That(state.VerticalVelocity, Is.GreaterThan(0));
            Assert.That(state.StealthUntil, Is.EqualTo(9.5f)); Assert.That(state.Records, Is.EqualTo(1)); Assert.That(state.Failures, Is.Zero);
        }
        [Test] public void GuardAppliesShieldThenCapturesOldDeadlineBeforeReadingCurrentTimeAndReduction()
        {
            var state = new State(Input("ranged", "owner_guard")) { GuardUntil = 20, Shield = 4, GuardReduction = .1f };
            state.Callback = operation =>
            {
                if (operation == "ranged") { state.Now = 2; state.GuardUntil = 7; }
                if (operation == "shield") { state.Cancel(); state.Now = 5; }
                if (operation == "now") state.GuardUntil = 100;
            };
            state.Activate(); Committed(state);
            Assert.That(state.Shield, Is.EqualTo(30)); Assert.That(state.GuardUntil, Is.EqualTo(7.5f)); Assert.That(state.GuardReduction, Is.EqualTo(.4f));
            Assert.That(state.Lifecycle.Cancelled, Is.True); Assert.That(state.Records, Is.EqualTo(1));
            Assert.That(state.Events.GetRange(state.Events.IndexOf("shield"), 7),
                Is.EqualTo(new[] { "shield", "release", "guard-read", "now", "guard", "reduction", "record" }));
        }
        [TestCase(20, .375f, 20)] [TestCase(1, .375f, 2.875f)]
        public void GuardUsesTheExistingOrderedMaxWithoutClampingOrAddingShield(float oldDeadline, float now, float expected)
        {
            var state = new State(Input(effect: "owner_guard")) { GuardUntil = oldDeadline, Now = now, Shield = 11 };
            state.Activate(); Assert.That(Bits(state.GuardUntil), Is.EqualTo(Bits(expected))); Assert.That(state.Shield, Is.EqualTo(30));
        }
        [Test] public void SmokeReplacementPrecedesReadingTheExistingActivationAndDoesNotUseWallClockTime()
        {
            var state = new State(Input("ranged", "smoke")); state.Callback = operation =>
            {
                if (operation == "replace") { state.Lifecycle = new CastLifecycleExecution(.625f, 3); state.Lifecycle.ClaimActivation(); state.Now = 100; }
            };
            state.Activate(); Committed(state); Assert.That(state.PreviousSmokeActive, Is.False); Assert.That(state.Replacements, Is.EqualTo(1));
            Assert.That(state.Smoke.End, Is.EqualTo(3.125f)); Assert.That(state.Records, Is.EqualTo(1));
            Assert.That(state.Events.GetRange(state.Events.IndexOf("replace"), 4), Is.EqualTo(new[] { "replace", "activation", "smoke", "record" }));
        }
        [Test] public void DecoySpawnUsesTheLiveInstallCenterAfterTheResourceCallbackAndOriginalEffectReference()
        {
            var input = Input(effect: "decoy"); var state = new State(input);
            state.Callback = operation => { if (operation == "consume") { state.InstallX = 40; state.InstallY = 8.125f; input.effect.hp = 999; } };
            state.Activate(); Committed(state);
            Assert.That(state.Spawns.Count, Is.EqualTo(1)); Assert.That(state.Spawns[0].X, Is.EqualTo(40)); Assert.That(state.Spawns[0].Y, Is.EqualTo(7.375f));
            Assert.That(state.Spawns[0].Effect, Is.SameAs(state.Definition.Effect)); Assert.That(state.Spawns[0].Effect.Hp, Is.EqualTo(30));
            Assert.That(state.QueryX, Is.EqualTo(-1)); Assert.That(state.QueryY, Is.EqualTo(3.75f));
        }
        [Test] public void ResourceReentryPreservesTheOriginalBusyFailureAndTheNestedPoseWrite()
        {
            var state = new State(Input(effect: "stealth")); int reentries = 0;
            state.Callback = operation =>
            {
                if (operation != "consume") return;
                Assert.That(state.Lifecycle.Activated, Is.False); state.PositionX = 12; state.Now = .5f;
                reentries++; state.Before();
            };
            state.Before(); Assert.That(reentries, Is.EqualTo(1)); Assert.That(state.Source.Calls, Is.EqualTo(1));
            Assert.That(state.Resources.Current.Status, Is.EqualTo(ActionResourceStatus.Failed)); Assert.That(state.Resources.IsActive, Is.False);
            Assert.That(state.Charges, Is.EqualTo(3)); Assert.That(state.Lifecycle.Activated, Is.False); Assert.That(state.Lifecycle.Cancelled, Is.True);
            Assert.That(state.Lifecycle.MotionEnd, Is.EqualTo(.5f - .125f + .1f)); Assert.That(state.SavedX, Is.EqualTo(12));
            Assert.That(state.StealthUntil, Is.Zero); Assert.That(state.Records, Is.Zero); Assert.That(state.Failures, Is.EqualTo(1));
        }
        [Test] public void PostClaimReentryUsesTheExistingLifecycleAndDoesNotConsumeOrApplyTwice()
        {
            var state = new State(Input(effect: "owner_guard")); int reentries = 0;
            state.Callback = operation =>
            {
                if (operation != "shield" && operation != "record") return;
                Assert.That(state.Lifecycle.Activated, Is.True); reentries++; state.Before();
            };
            state.Before(); Committed(state); Assert.That(reentries, Is.EqualTo(2)); Assert.That(state.Records, Is.EqualTo(1));
            Assert.That(state.Shield, Is.EqualTo(30)); Assert.That(state.GuardUntil, Is.EqualTo(2.875f)); Assert.That(state.Failures, Is.Zero);
        }
        [TestCase("save", false, false)] [TestCase("install", true, false)]
        [TestCase("floor", true, false)] [TestCase("floor-height", true, true)]
        [TestCase("solid", true, true)] [TestCase("occluded", true, true)] [TestCase("commit", true, true)]
        public void AdmissionExceptionsKeepEveryEarlierPoseWriteWithoutInventingFailureOrConsumption(string boundary, bool installWritten, bool floorWritten)
        {
            var state = Installation(); var error = new InvalidOperationException(boundary);
            state.Callback = operation => { if (operation == boundary) throw error; };
            Assert.That(Assert.Throws<InvalidOperationException>(state.Activate), Is.SameAs(error));
            Assert.That(state.SavedX, Is.EqualTo(2)); Assert.That(state.SavedY, Is.EqualTo(4));
            Assert.That(state.InstallX, Is.EqualTo(installWritten ? -1 : -30));
            Assert.That(state.InstallY, Is.EqualTo(floorWritten ? 3.75f : installWritten ? 5.5f : -40));
            Assert.That(state.Source.Calls, Is.Zero); Assert.That(state.Charges, Is.EqualTo(3)); Assert.That(state.Resources.Current, Is.Null);
            Assert.That(state.Resources.IsActive, Is.True); Assert.That(state.Lifecycle.Activated, Is.False); Assert.That(state.Lifecycle.Cancelled, Is.False);
            Assert.That(state.Failures, Is.Zero); Assert.That(state.Records, Is.Zero); Assert.That(state.Spawns, Is.Empty);
        }
        [TestCase("claim", 0)] [TestCase("ranged", 1)] [TestCase("recoil", 1)]
        [TestCase("velocity", 1)] [TestCase("wave", 2)] [TestCase("record", 2)]
        public void CommittedExceptionsDoNotRollbackChargeClaimSpawnsOrRecoil(string boundary, int spawns)
        {
            var state = new State(Input("ranged", "projectile")) { Grounded = false }; var error = new InvalidOperationException(boundary);
            state.Callback = operation => { if (operation == boundary) throw error; };
            Assert.That(Assert.Throws<InvalidOperationException>(state.Activate), Is.SameAs(error)); Committed(state);
            Assert.That(state.Spawns.Count, Is.EqualTo(spawns)); Assert.That(state.Resources.IsActive, Is.True); Assert.That(state.Lifecycle.Cancelled, Is.False);
            Assert.That(state.Failures, Is.Zero); Assert.That(state.Records, Is.EqualTo(boundary == "record" ? 1 : 0));
            bool recoilStarted = boundary == "recoil" || boundary == "velocity" || boundary == "wave" || boundary == "record";
            Assert.That(state.Recoil.Read().StartedAt, Is.EqualTo(recoilStarted ? .375f : -1));
            Assert.That(state.VerticalVelocity > 0, Is.EqualTo(boundary == "velocity" || boundary == "wave" || boundary == "record"));
            int count = state.Events.Count; state.Callback = null; state.Before();
            Assert.That(state.Source.Calls, Is.EqualTo(1)); Assert.That(state.Spawns.Count, Is.EqualTo(spawns)); Assert.That(state.Events.Count, Is.EqualTo(count));
        }
        [TestCase("shield", 30, 11, .1f)] [TestCase("guard", 30, 11, .1f)] [TestCase("reduction", 30, 11, .4f)]
        public void GuardExceptionsRetainTheExactAppliedPrefixAndPreventTheActivationRecord(string boundary, float shield, float guard, float reduction)
        {
            var state = new State(Input(effect: "owner_guard")) { Shield = 4, GuardUntil = 11, GuardReduction = .1f };
            var error = new InvalidOperationException(boundary); state.Callback = operation => { if (operation == boundary) throw error; };
            Assert.That(Assert.Throws<InvalidOperationException>(state.Activate), Is.SameAs(error)); Committed(state);
            Assert.That(state.Shield, Is.EqualTo(shield)); Assert.That(state.GuardUntil, Is.EqualTo(guard)); Assert.That(state.GuardReduction, Is.EqualTo(reduction));
            Assert.That(state.Events.Contains("guard"), Is.EqualTo(boundary != "shield"));
            Assert.That(state.Events.Contains("reduction"), Is.EqualTo(boundary == "reduction")); Assert.That(state.Records, Is.Zero);
        }
        [Test] public void SmokeReplacementExceptionLeavesThePreviousEffectRemovedWithoutStartingTheNewDeadline()
        {
            var state = new State(Input(effect: "smoke")); var error = new InvalidOperationException("replace");
            state.Callback = operation => { if (operation == "replace") throw error; };
            Assert.That(Assert.Throws<InvalidOperationException>(state.Activate), Is.SameAs(error)); Committed(state);
            Assert.That(state.PreviousSmokeActive, Is.False); Assert.That(state.Replacements, Is.EqualTo(1)); Assert.That(state.Smoke.End, Is.Zero);
            Assert.That(state.Events, Does.Not.Contain("activation")); Assert.That(state.Records, Is.Zero);
        }
        [Test] public void FailureCallbackExceptionKeepsTheResourceReturnAndExistingFailureRecovery()
        {
            var input = Input(); input.vertical = "grounded_required"; var state = new State(input) { Grounded = false };
            var error = new InvalidOperationException("fail"); state.Callback = operation => { if (operation == "fail") throw error; };
            Assert.That(Assert.Throws<InvalidOperationException>(state.Activate), Is.SameAs(error)); UncommittedFailure(state);
        }
        [Test] public void EmptyRangedHitsStillFailAfterCommitAndClaimRatherThanBecomingANewAdmissionPolicy()
        {
            var input = Input("ranged", "stealth"); input.hits = Array.Empty<HitData>(); var state = new State(input);
            Assert.Throws<ArgumentOutOfRangeException>(state.Activate); Committed(state);
            Assert.That(state.Resources.IsActive, Is.True); Assert.That(state.Spawns, Is.Empty); Assert.That(state.StealthUntil, Is.Zero);
            Assert.That(state.Failures, Is.Zero); Assert.That(state.Records, Is.Zero);
        }
    }
}
