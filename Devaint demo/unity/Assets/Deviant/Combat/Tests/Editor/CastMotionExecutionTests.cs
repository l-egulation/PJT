using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CastMotionExecutionTests
    {
        sealed class State
        {
            public readonly SkillExecutionDefinition Definition;
            public CastMotionExecution Motion;
            public ForcedDashExecution Dash;
            public RangedRecoilExecution Recoil;
            public MissileExecution Missile;
            public ApproachRecastExecution ApproachExecution;
            public MeleeActionRunner Melee;
            public ChargedMeleeExecution Charged;
            public ChargeExecution Charge;
            public readonly List<string> Events = new List<string>();
            public Action<string> Callback;
            public float Now = .25f, Started, Step = .125f, RunSpeed = 7;
            public float HopVelocity, MotionEnd;
            public bool Cancelled, Done, Flying, MissileRecoil, Approach, Basic, DirectSpecial;
            public bool UseApproachOwner, TargetAlive = true;
            public bool Grounded = true, Activated = true;
            public float Elapsed => Now - Started;
            public State(SkillData input = null)
            {
                Definition = new SkillExecutionDefinition(input ?? Input());
                Dash = new ForcedDashExecution(Definition.Movement);
            }
            public void Signal(string operation) { Events.Add(operation); Callback?.Invoke(operation); }
            public float Velocity(int axis = 1)
            {
                var host = new Host(this); return Motion.HorizontalVelocity(axis, Definition, ref host);
            }
            public bool Moving() { var host = new Host(this); return CastMotionExecution.Moving(ref host); }
            public bool Freeze() { var host = new Host(this); return CastMotionExecution.FreezeVertical(Definition, ref host); }
            public bool Invulnerable() { var host = new Host(this); return CastMotionExecution.Invulnerable(Definition, ref host); }
            public void BeginRecoil()
            {
                Recoil.Begin(Now, 1, .5f, 0, 50, 50); Grounded = false;
            }
            public void BindMelee()
            {
                Assert.That(BasicMeleeDefinition.TryCreate("base_sword_basic_0", new BasicData { damage = 10, impact = .25f, active = .125f,
                    duration = .75f, box = new BoxData { width = 2, height = 2, x = 1, y = 1 } }, out var source, out var error), Is.True, error);
                Melee = new MeleeActionRunner(Guid.NewGuid(), new MeleeActionDefinition(source, 1, .5f, .25f, .125f), 0, new MeleeHost(this));
                Melee.Prepare(Now, Step);
            }
            public void BindCharge()
            {
                Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("cast_motion_charge", .25f, 1, 25, 75, .75f), 0);
                Charged = new ChargedMeleeExecution(Charge, .125f, new ChargeHost(this));
            }
            public void BindMissile()
            {
                Missile = new MissileExecution(Definition.Missile, 50, 50); Missile.Launch(Now);
            }
            public void BeginApproach()
            {
                UseApproachOwner = true; var host = new ApproachHost(this);
                ApproachExecution.MarkAccepted(ref host); Assert.That(ApproachExecution.TryStart(ref host), Is.True);
            }
        }

        struct Host : ICastMotionHost
        {
            readonly State state;
            public int Reads;
            public Host(State state) { this.state = state; Reads = 0; }
            void Signal(string operation) { Reads++; state.Signal(operation); }
            public bool HasMeleeAction { get { Signal("melee"); return state.Melee != null; } }
            public float MeleeHorizontalVelocity(int axis)
            {
                Signal("melee-velocity");
                return state.Melee.HorizontalVelocity(state.Now, state.Step, axis, state.Grounded, state.DirectSpecial ? 0 : state.RunSpeed);
            }
            public bool HasChargedMelee { get { Signal("charged-definition"); return state.Charged != null; } }
            public bool HasChargedMeleeExecution { get { Signal("charged-execution"); return state.Charged != null; } }
            public bool HasDirectMeleeSpecial { get { Signal("direct-special"); return state.DirectSpecial; } }
            public bool Cancelled { get { Signal("cancelled"); return state.Charged?.Cancelled ?? state.Cancelled; } }
            public bool MotionDone { get { Signal("done"); return state.Charged?.MotionDone ?? state.Done; } }
            public bool MissileFlying
            {
                get { Signal("flying"); if (!state.Missile.Initialized) return state.Flying; var host = new MissileHost(state); return state.Missile.IsFlying(ref host); }
            }
            public float MissileFlightVelocity()
            {
                Signal("flight-velocity"); return state.Missile.Initialized ? state.Missile.FlightVelocity(1, state.Step) : 12;
            }
            public bool MissileRecoiling
            {
                get { Signal("missile-recoil"); if (!state.Missile.Initialized) return state.MissileRecoil; var host = new MissileHost(state); return state.Missile.IsRecoiling(ref host); }
            }
            public float MissileRecoilVelocity()
            {
                Signal("missile-velocity"); return state.Missile.Initialized ? state.Missile.RecoilVelocity(1, state.Now, state.Step) : -6;
            }
            public bool Approaching
            {
                get { Signal("approaching"); if (!state.UseApproachOwner) return state.Approach; var host = new ApproachHost(state); return state.ApproachExecution.IsApproaching(ref host); }
            }
            public bool Recoiling
            {
                get { Signal("recoiling"); return MissileRecoiling || state.Recoil.IsActive(state.Now, state.Charged?.Cancelled ?? state.Cancelled); }
            }
            public float RangedRecoilVelocity()
            {
                Signal("ranged-velocity"); return state.Recoil.Velocity(state.Now, state.Step, state.Grounded, state.Definition.Ranged.RecoilDistance);
            }
            public bool Moving { get { Signal("moving"); return state.Moving(); } }
            public bool ForcedDashMoving
            {
                get { Signal("forced-moving"); return state.Dash.IsMoving(state.Activated, state.Elapsed, state.Step); }
            }
            public bool TryForcedDashVelocity(out float velocity)
            {
                Signal("forced-velocity"); return state.Dash.TryHorizontalVelocity(state.Activated, state.Elapsed, state.Step, -1, out velocity);
            }
            public bool IsBasic { get { Signal("basic"); return state.Basic; } }
            public float Elapsed { get { Signal("elapsed"); return state.Elapsed; } }
            public float StepDuration { get { Signal("step"); return state.Step; } }
            public bool OwnerGrounded { get { Signal("grounded"); return state.Grounded; } }
            public float RunSpeed { get { Signal("run-speed"); return state.RunSpeed; } }
        }

        sealed class MeleeHost : IMeleeActionHost
        {
            readonly State state;
            public MeleeHost(State state) { this.state = state; }
            public bool TryActivate() { state.Signal("melee-activate"); return true; }
            public void Sweep() => state.Signal("melee-sweep");
            public void Complete() => state.Signal("melee-complete");
        }
        sealed class ChargeHost : IChargedMeleeExecutionHost
        {
            readonly State state;
            public ChargeHost(State state) { this.state = state; }
            public float Now => state.Now;
            public bool SelectTarget() { state.Signal("charge-target"); return true; }
            public bool CommitResources() { state.Signal("charge-resources"); return true; }
            public void Activated() => state.Signal("charge-activate");
            public void ReleaseResources() => state.Signal("charge-release");
            public void Failed() => state.Signal("charge-fail");
            public void Sweep() => state.Signal("charge-sweep");
            public void Complete() => state.Signal("charge-complete");
            public void Dispose() { state.Signal("charge-dispose"); state.Charged.ReleaseResources(); }
        }
        struct MissileHost : IMissileExecutionHost
        {
            readonly State state;
            public MissileHost(State state) { this.state = state; }
            public bool Activated => state.Activated;
            public bool Cancelled => state.Cancelled;
            public bool MotionDone => state.Done;
            public bool OwnerAlive => true;
            public float Now => state.Now;
            public float StartedAt => state.Started;
            public float StepDuration => state.Step;
            public float MovedX => 0;
            public bool WallHit => false;
            public bool Grounded => state.Grounded;
            public void UpdateVisual() => state.Signal("missile-visual");
            public void Explode() => state.Signal("missile-explode");
            public void StartHop(float verticalSpeed) { state.HopVelocity = verticalSpeed; state.Signal("missile-hop"); }
            public void SetMotionEnd(float elapsed) { state.MotionEnd = elapsed; state.Signal("missile-motion-end"); }
            public void RecordEnd() => state.Signal("missile-end");
        }
        struct ApproachHost : IApproachRecastHost
        {
            readonly State state;
            public ApproachHost(State state) { this.state = state; }
            public bool SameWeapon => true;
            public bool Cancelled => state.Cancelled;
            public bool MotionDone => state.Done;
            public bool TargetHit => true;
            public bool MarkedTargetAlive => state.TargetAlive;
            public bool IsRecastMovement => true;
            public float Now => state.Now;
            public float Elapsed => state.Elapsed;
            public float RecastWindow => 3;
            public float Distance => 5;
            public float Speed => 15;
            public float DistanceToDestination => 4;
            public float MovedDistance => throw new InvalidOperationException("The lost-target fixture must not move.");
            public float RequestedDistance => throw new InvalidOperationException("The lost-target fixture must not move.");
            public void CaptureDestination() => state.Signal("approach-destination");
            public void CaptureOrigin() => throw new InvalidOperationException("The lost-target fixture must not move.");
            public void StartApproach() => state.Signal("approach-start");
            public void ResumeMotion() { state.Cancelled = state.Done = false; state.Signal("approach-resume"); }
            public void MoveTowardDestination(float distance) => throw new InvalidOperationException("The lost-target fixture must not move.");
            public void SetMotionEnd(float elapsed) { state.MotionEnd = elapsed; state.Signal("approach-motion-end"); }
            public void ClearVerticalSpeed() { state.HopVelocity = 0; state.Signal("approach-vertical"); }
            public void RecordApproach() => state.Signal("approach-record");
        }

        static SkillData Input(string kind = null, string vertical = "normal") => new SkillData
        {
            id = "cast_motion_fixture", kind = kind, vertical = vertical, activation = .25f, end = 2, inputSpeed = 3,
            basicLungeStart = .25f, basicLungeDuration = .5f, basicLungeDistance = 4,
            hits = Array.Empty<HitData>(), invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
            movement = new MoveData { type = "dash", start = .25f, duration = .5f, speed = 10 },
            ranged = new RangedData { recoilDistance = 4, recoilDuration = .5f }
        };

        [Test] public void ActualApproachOwnerSuppliesFreezeAndZeroVelocityWithoutInvulnerabilityAndEndsOnTargetLoss()
        {
            var input = Input(); input.movement = null;
            var state = new State(input); state.BeginApproach();
            Assert.That(state.Velocity(), Is.Zero); Assert.That(state.Moving(), Is.True);
            Assert.That(state.Freeze(), Is.True); Assert.That(state.Invulnerable(), Is.False);
            state.TargetAlive = false; var host = new ApproachHost(state); state.ApproachExecution.Advance(state.Step, ref host);
            Assert.That(state.Moving(), Is.False); Assert.That(state.Freeze(), Is.False); Assert.That(state.Invulnerable(), Is.False);
            Assert.That(state.MotionEnd, Is.EqualTo(state.Elapsed)); Assert.That(state.Velocity(), Is.EqualTo(3));
        }

        [Test] public void ActualMissileOwnerEndsProtectionBeforeExplosionAndRetainsItsOwnRecoil()
        {
            var input = Input("missile"); input.missile = new MissileData { speed = 12, distance = 10, manualDelay = 0,
                recoilHeight = 1, recoilDistance = 4, recovery = .5f };
            input.movement = null;
            var state = new State(input) { Grounded = false }; state.BindMissile();
            var host = new MissileHost(state); Assert.That(state.Missile.Prepare(state.Step, ref host), Is.True);
            Assert.That(state.Velocity(), Is.EqualTo(12)); Assert.That(state.Moving(), Is.True);
            Assert.That(state.Freeze(), Is.True); Assert.That(state.Invulnerable(), Is.True);
            bool explosionObserved = false;
            state.Callback = operation =>
            {
                if (operation != "missile-explode") return;
                explosionObserved = true; Assert.That(state.Missile.Ended, Is.True);
                Assert.That(state.Freeze(), Is.False); Assert.That(state.Invulnerable(), Is.False);
            };
            Assert.That(state.Missile.TryManualDetonation(ref host), Is.True); Assert.That(explosionObserved, Is.True);
            Assert.That(state.HopVelocity, Is.EqualTo(10)); Assert.That(state.MotionEnd, Is.EqualTo(.75f));
            Assert.That(state.Moving(), Is.False); Assert.That(state.Freeze(), Is.False); Assert.That(state.Invulnerable(), Is.False);
            Assert.That(state.Velocity(), Is.EqualTo(-10));
            state.Cancelled = true; Assert.That(state.Velocity(), Is.EqualTo(state.RunSpeed));
        }

        [Test] public void PureBoundariesDoNotReferenceUnityAndTheHostIsInvokedByReference()
        {
            foreach (var assembly in new[] { typeof(ICastMotionHost).Assembly, typeof(CastMotionExecution).Assembly })
                foreach (var reference in assembly.GetReferencedAssemblies()) Assert.That(reference.Name, Does.Not.StartWith("Unity"));
            var state = new State(); var host = new Host(state);
            Assert.That(state.Motion.HorizontalVelocity(1, state.Definition, ref host), Is.EqualTo(-10));
            Assert.That(host.Reads, Is.GreaterThan(0));
        }

        [TestCase(false, 4)] [TestCase(true, 0)]
        public void MeleeDelegationWinsAndDirectSpecialKeepsItsOwnAirPolicy(bool direct, float expected)
        {
            var state = new State { DirectSpecial = direct, Flying = true, MissileRecoil = true, Approach = true, Basic = true };
            state.BindCharge(); state.BindMelee(); state.Events.Clear(); state.Grounded = direct ? false : true;
            Assert.That(state.Velocity(), Is.EqualTo(expected));
            Assert.That(state.Events, Is.EqualTo(new[] { "melee", "melee-velocity" }));
            Assert.That(state.Melee.State, Is.EqualTo(MeleeActionState.Active));
        }

        [TestCase(-1)] [TestCase(1)]
        public void ChargedOwnerKeepsHorizontalZeroUntilItsOwnCancellationOrCompletion(int axis)
        {
            var state = new State { Flying = true, MissileRecoil = true, Approach = true, Basic = true };
            state.BindCharge();
            Assert.That(state.Velocity(axis), Is.Zero); Assert.That(state.Moving(), Is.False);
            Assert.That(state.Freeze(), Is.False); Assert.That(state.Invulnerable(), Is.False);
            Assert.That(state.Events, Does.Not.Contain("flying"));
            state.Charged.Cancel(); state.Events.Clear();
            Assert.That(state.Velocity(axis), Is.EqualTo(axis * state.RunSpeed));

            state = new State(); state.BindCharge(); state.Charged.BeforeMovement(state.Step, false);
            state.Now = .75f; state.Charged.Step(state.Step); state.Events.Clear();
            Assert.That(state.Charged.MotionDone, Is.True); Assert.That(state.Charged.Cancelled, Is.False);
            Assert.That(state.Velocity(axis), Is.EqualTo(axis * state.RunSpeed));
        }

        [TestCase(false)] [TestCase(true)]
        public void GenericTerminalFallbackPrecedesEverySpecializedAndBasicMotion(bool done)
        {
            var state = new State { Cancelled = !done, Done = done, Flying = true, MissileRecoil = true, Approach = true, Basic = true };
            Assert.That(state.Velocity(-1), Is.EqualTo(-state.RunSpeed));
            Assert.That(state.Events, Does.Not.Contain("flying")); Assert.That(state.Events, Does.Not.Contain("elapsed"));
            Assert.That(state.Freeze(), Is.False); Assert.That(state.Invulnerable(), Is.False);
        }

        [TestCase("flight", 12)] [TestCase("missile-recoil", -6)] [TestCase("approach", 0)] [TestCase("ranged-recoil", -8)]
        public void FlightThenMissileRecoilThenApproachThenRangedRecoilPrecedeTheBasicPath(string winner, float expected)
        {
            var state = new State { Basic = true }; state.BeginRecoil();
            state.Flying = winner == "flight";
            state.MissileRecoil = winner == "flight" || winner == "missile-recoil";
            state.Approach = winner != "ranged-recoil";
            Assert.That(state.Velocity(), Is.EqualTo(expected));
            Assert.That(state.Events, Does.Not.Contain("basic")); Assert.That(state.Events, Does.Not.Contain("forced-velocity"));
            if (winner == "flight") Assert.That(state.Events, Does.Not.Contain("missile-recoil"));
            if (winner == "missile-recoil") Assert.That(state.Events, Does.Not.Contain("approaching"));
            if (winner == "approach") Assert.That(state.Events, Does.Not.Contain("recoiling"));
            Assert.That(state.Recoil.Read().Stopped, Is.False);
        }

        [TestCase(-1)] [TestCase(1)]
        public void BasicLungePrecedesForcedDashAndForcedDashPrecedesInput(int axis)
        {
            var state = new State { Basic = true };
            Assert.That(state.Velocity(axis), Is.EqualTo(axis * 8));
            Assert.That(state.Events, Does.Not.Contain("forced-velocity"));
            state.Basic = false; state.Events.Clear(); Assert.That(state.Velocity(axis), Is.EqualTo(-10));
            Assert.That(state.Events, Does.Contain("forced-velocity"));
            state.Activated = false; Assert.That(state.Velocity(axis), Is.EqualTo(axis * 3));
            state.Now = .75f; state.Activated = true; Assert.That(state.Velocity(axis), Is.EqualTo(axis * 3));
        }

        [Test] public void FirstPartialBasicIntervalIsIncludedExactlyOnce()
        {
            var input = Input(); input.basicLungeStart = 0; input.basicLungeDuration = .25f;
            var state = new State(input) { Basic = true, Now = 1, Started = 1.09375f };
            Assert.That(state.Velocity() * state.Step, Is.EqualTo(.5f));
            Assert.That(state.Velocity(), Is.Zero, "Repeated observation cannot donate the first partial interval again.");
            state.Now = 1.125f;
            Assert.That(state.Velocity() * state.Step, Is.EqualTo(2));
            state.Now = 1.25f;
            Assert.That(state.Velocity() * state.Step, Is.EqualTo(1.5f));
            state.Now = 1.375f; Assert.That(state.Velocity(), Is.Zero);
        }

        [TestCase(null)] [TestCase("bomb")]
        public void NeutralAndAirborneBasicIntervalsCannotBeRecoveredOnLandingOrNewInput(string kind)
        {
            var state = new State(Input(kind)) { Basic = true };
            Assert.That(state.Velocity(0), Is.Zero);
            state.Now = .375f; state.Grounded = false; Assert.That(state.Velocity(-1), Is.EqualTo(-7));
            state.Now = .5f; state.Grounded = true; Assert.That(state.Velocity() * state.Step, Is.EqualTo(1));
            state.Now = .625f; state.Step = .25f; Assert.That(state.Velocity() * state.Step, Is.EqualTo(1));
            state.Now = .875f; Assert.That(state.Velocity(), Is.Zero);
        }

        [Test] public void RangedBasicConsumesNeutralAndAirIntervalsBeforeChoosingInputOrRunSpeed()
        {
            var state = new State(Input("ranged")) { Basic = true };
            Assert.That(state.Velocity(0), Is.Zero);
            Assert.That(state.Events.IndexOf("elapsed"), Is.LessThan(state.Events.IndexOf("grounded")));
            state.Now = .375f; Assert.That(state.Velocity(-1), Is.EqualTo(-3));
            state.Now = .5f; state.Grounded = false; Assert.That(state.Velocity(), Is.EqualTo(7));
            Assert.That(state.Events, Does.Not.Contain("forced-velocity"));
            Assert.That(state.Events.FindAll(operation => operation == "elapsed").Count, Is.EqualTo(3));
        }

        [TestCase(0f)] [TestCase(-.25f)]
        public void BasicWithoutALungeWindowRemainsStationaryOnGround(float duration)
        {
            var input = Input(); input.basicLungeDuration = duration;
            var state = new State(input) { Basic = true }; Assert.That(state.Velocity(), Is.Zero);
            Assert.That(state.Events, Does.Not.Contain("forced-velocity"));
        }

        [Test] public void HigherPriorityMotionDoesNotAdvanceTheFallbackBasicCursor()
        {
            var state = new State { Basic = true, Flying = true };
            Assert.That(state.Velocity(), Is.EqualTo(12)); Assert.That(state.Events, Does.Not.Contain("elapsed"));
            state.Flying = false; state.Now = .375f;
            Assert.That(state.Velocity(), Is.EqualTo(16), "Only an entered basic fallback consumes its own existing interval.");
        }

        [TestCase(0f, .25f, false)] [TestCase(.125f, .25f, true)]
        [TestCase(.25f, .125f, true)] [TestCase(.75f, .125f, false)]
        public void MovingUsesTheExistingForcedDashOverlapBoundaries(float elapsed, float step, bool expected)
        {
            var state = new State { Now = elapsed, Step = step };
            Assert.That(state.Moving(), Is.EqualTo(expected));
            Assert.That(state.Dash.MovementDone, Is.False);
        }

        [Test] public void MovingShortCircuitsMissileAndApproachBeforeTheGenericLifecycleGate()
        {
            var state = new State { Flying = true, Approach = true, Cancelled = true, Done = true };
            Assert.That(state.Moving(), Is.True);
            Assert.That(state.Events, Is.EqualTo(new[] { "charged-definition", "flying" }));
            state.Flying = false; state.Events.Clear(); Assert.That(state.Moving(), Is.True);
            Assert.That(state.Events, Is.EqualTo(new[] { "charged-definition", "flying", "approaching" }));
            state.Approach = false; state.Events.Clear(); Assert.That(state.Moving(), Is.False);
            Assert.That(state.Events, Does.Not.Contain("done")); Assert.That(state.Events, Does.Not.Contain("forced-moving"));
            state.Cancelled = false; state.Events.Clear(); Assert.That(state.Moving(), Is.False);
            Assert.That(state.Events, Does.Contain("done")); Assert.That(state.Events, Does.Not.Contain("forced-moving"));
        }

        [Test] public void ForcedDashWallEndRemainsWithItsExistingOwner()
        {
            var state = new State(Input(vertical: "freeze_during_movement_then_fall_from_zero"));
            Assert.That(state.Moving(), Is.True); Assert.That(state.Freeze(), Is.True);
            state.Dash.AfterMovement(state.Activated, state.Elapsed, state.Step, true);
            Assert.That(state.Moving(), Is.False); Assert.That(state.Freeze(), Is.False); Assert.That(state.Velocity(), Is.EqualTo(3));
        }

        [TestCase(.05f, false)] [TestCase(.1f, true)] [TestCase(.5f, true)] [TestCase(.67f, false)] [TestCase(.75f, false)]
        public void AuthoredVerticalFreezeWindowIncludesStartAndExcludesEnd(float elapsed, bool expected)
        {
            var state = new State(Input(vertical: "freeze_0.10_to_0.67_then_fall_from_zero")) { Now = elapsed };
            Assert.That(state.Freeze(), Is.EqualTo(expected)); Assert.That(state.Invulnerable(), Is.False);
            Assert.That(state.Events, Does.Not.Contain("moving"));
        }

        [TestCase(.125f, false)] [TestCase(.25f, true)] [TestCase(.5f, false)]
        [TestCase(.625f, false)] [TestCase(.75f, true)] [TestCase(1f, false)]
        public void AuthoredInvulnerabilityWindowsAreHalfOpen(float elapsed, bool expected)
        {
            var input = Input(); input.invulStart = new[] { .25f, .75f }; input.invulEnd = new[] { .5f, 1f };
            var state = new State(input) { Now = elapsed }; Assert.That(state.Invulnerable(), Is.EqualTo(expected));
            Assert.That(state.Events, Does.Not.Contain("approaching"));
        }

        [TestCase("dash")] [TestCase("approach")] [TestCase("recoil")]
        public void OrdinaryForcedMotionApproachAndRecoilDoNotGrantExtraInvulnerability(string mode)
        {
            var state = new State(Input(vertical: "freeze_during_movement_then_fall_from_zero")) { Approach = mode == "approach" };
            if (mode == "recoil") { state.BeginRecoil(); state.Activated = false; }
            Assert.That(state.Invulnerable(), Is.False);
            Assert.That(state.Freeze(), Is.EqualTo(mode != "recoil"));
        }

        [Test] public void MissileFlightGrantsFreezeAndInvulnerabilityButRecoilDoesNot()
        {
            var state = new State { Flying = true, MissileRecoil = true };
            Assert.That(state.Freeze(), Is.True); Assert.That(state.Invulnerable(), Is.True);
            state.Flying = false; Assert.That(state.Freeze(), Is.False); Assert.That(state.Invulnerable(), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void DirectAndChargedMeleeDisableAuthoredFreezeAndInvulnerabilityBeforeAnyLiveMotionRead(bool charged)
        {
            var input = Input(vertical: "freeze_0.10_to_0.67_then_fall_from_zero"); input.invulStart = new[] { 0f }; input.invulEnd = new[] { 1f };
            var state = new State(input) { DirectSpecial = !charged, Flying = true, Approach = true };
            if (charged) state.BindCharge();
            Assert.That(state.Freeze(), Is.False); Assert.That(state.Invulnerable(), Is.False);
            Assert.That(state.Events, Does.Not.Contain("cancelled")); Assert.That(state.Events, Does.Not.Contain("flying"));
            Assert.That(state.Events, Does.Not.Contain("elapsed"));
        }

        [Test] public void FreezeRequeriesNestedMovingInsteadOfReusingEarlierMissileFacts()
        {
            var state = new State(Input(vertical: "freeze_during_movement_then_fall_from_zero")) { Activated = false };
            state.Callback = operation => { if (operation == "approaching") state.Flying = true; };
            Assert.That(state.Freeze(), Is.True);
            Assert.That(state.Events.FindAll(operation => operation == "flying").Count, Is.EqualTo(2));
            Assert.That(state.Events, Does.Contain("moving")); Assert.That(state.Events, Does.Not.Contain("forced-moving"));
        }

        [Test] public void NestedRecoilingRequeriesMissileStateAndUsesCurrentLandingFacts()
        {
            var state = new State { Basic = true }; state.BeginRecoil();
            state.Recoil.AfterMovement(state.Now, false, false, false);
            state.Callback = operation => { if (operation == "approaching") { state.MissileRecoil = true; state.Grounded = true; } };
            Assert.That(state.Velocity(), Is.Zero);
            Assert.That(state.Events.FindAll(operation => operation == "missile-recoil").Count, Is.EqualTo(2));
            Assert.That(state.Events, Does.Contain("ranged-velocity")); Assert.That(state.Events, Does.Not.Contain("missile-velocity"));
            Assert.That(state.Recoil.Read().Stopped, Is.True);
        }

        [Test] public void InvulnerabilityReadsElapsedAgainForEachComparisonAndAuthoredWindow()
        {
            var input = Input(); input.invulStart = new[] { .25f, .75f }; input.invulEnd = new[] { .375f, 1f };
            var state = new State(input); int reads = 0;
            state.Callback = operation =>
            {
                if (operation != "elapsed") return;
                reads++; if (reads == 2) state.Now = .375f; if (reads == 3) state.Now = .75f;
            };
            Assert.That(state.Invulnerable(), Is.True); Assert.That(reads, Is.EqualTo(4));
        }

        [Test] public void BasicCursorCommitsBeforeGroundedCallbackAndTheFinalStepRemainsLive()
        {
            var state = new State { Basic = true };
            state.Callback = operation => { if (operation == "grounded") state.Step = .25f; };
            Assert.That(state.Velocity(), Is.EqualTo(4));
            state.Callback = null; state.Now = .375f; state.Step = .125f;
            Assert.That(state.Velocity(), Is.EqualTo(8), "The cursor used the earlier interval end, not the later divisor step.");
        }

        [Test] public void ErrorAfterBasicCursorCommitDoesNotRollItBackOrDonateTheIntervalOnRetry()
        {
            var state = new State { Basic = true }; var failure = new InvalidOperationException("grounded callback");
            state.Callback = operation => { if (operation == "grounded") throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.Velocity()), Is.SameAs(failure));
            Assert.That(state.Events, Does.Not.Contain("forced-velocity"));
            state.Callback = null; Assert.That(state.Velocity(), Is.Zero);
            state.Now = .375f; Assert.That(state.Velocity(), Is.EqualTo(8));
        }

        [Test] public void SpecializedVelocityErrorsPropagateWithoutFallingThroughOrConsumingBasicMotion()
        {
            var state = new State { Basic = true, Flying = true }; var failure = new InvalidOperationException("flight callback");
            state.Callback = operation => { if (operation == "flight-velocity") throw failure; };
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.Velocity()), Is.SameAs(failure));
            Assert.That(state.Events, Does.Not.Contain("basic")); Assert.That(state.Events, Does.Not.Contain("elapsed"));
            state.Callback = null; state.Flying = false; state.Now = .375f;
            Assert.That(state.Velocity(), Is.EqualTo(16));
        }

        [Test] public void LandingStopsTheSameRangedRecoilOwnerBeforeNextBasicObservation()
        {
            var state = new State { Basic = true }; state.BeginRecoil();
            // Landing requires the recoil owner to observe departure first.
            state.Recoil.AfterMovement(state.Now, false, false, false);
            state.Grounded = true;
            Assert.That(state.Velocity(), Is.Zero); Assert.That(state.Recoil.Read().Stopped, Is.True);
            Assert.That(state.Events, Does.Not.Contain("basic"));
            state.Events.Clear(); Assert.That(state.Velocity(), Is.EqualTo(8));
            Assert.That(state.Events, Does.Contain("basic"));
        }
    }
}
