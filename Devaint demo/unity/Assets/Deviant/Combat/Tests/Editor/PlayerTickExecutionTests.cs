using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using Deviant.Equipment.Contracts;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class PlayerTickExecutionTests
    {
        sealed class Cast
        {
            public string Id, Vertical = "normal";
            public float Horizontal = 3;
            public bool Basic, Freeze, Direct, Charge, Recoil, Approach, Missile;
            public Cast(string id) { Id = id; }
        }
        sealed class State : IPlayerActionHost
        {
            public PlayerTickExecution Tick;
            public readonly CombatInputBuffer Input = new CombatInputBuffer();
            public readonly PlayerDashExecution Dash = new PlayerDashExecution();
            public readonly PlayerJumpExecution Jumps = new PlayerJumpExecution();
            public readonly IWeaponCharges[] Charges = {
                Deviant.Equipment.WeaponResources.CreateCharges(1, 1), Deviant.Equipment.WeaponResources.CreateCharges(1, 2)
            };
            public readonly PlayerActionArbiter Arbiter;
            public readonly LocomotionProfile Movement;
            public readonly List<string> Events = new List<string>();
            public readonly Dictionary<string, Action<State>> Hooks = new Dictionary<string, Action<State>>();
            public readonly List<float> CooldownTimes = new List<float>();
            public readonly List<bool> Pits = new List<bool>();
            public readonly List<float[]> Records = new List<float[]>();
            public Cast Current;
            public float Now = 1, RunSpeed = 5, VelocityX, VelocityY = -4, StepDuration, X = 2, SafeY = 10;
            public int Facing = 1, Selected, Landings, Falls, VerticalClears;
            public bool Blocked, Dead, Grounded, Wall, Freeze, SafeFloor = true, RecoverySafe = true;
            public State(LocomotionData movementData = null)
            {
                Assert.That(LocomotionProfile.TryCreate(new LocomotionData { schemaVersion = 2, id = "tick-test", runSpeed = 5,
                    dashSpeed = 15, dashDuration = .2f, dashRecovery = 1, dashInvulnerability = .15f,
                    dashJumpCarrySpeed = 5, dashJumpCarryDuration = .1f, firstJumpVelocity = 20, secondJumpVelocity = 20,
                    riseGravity = 66.6666667f, fallGravity = 104.1666667f }, out var movement, out var error), Is.True, error);
                if (movementData != null) Assert.That(LocomotionProfile.TryCreate(movementData, out movement, out error), Is.True, error);
                Movement = movement; Arbiter = new PlayerActionArbiter(this);
            }
            public void Event(string name) { Events.Add(name); if (Hooks.TryGetValue(name, out var callback)) callback(this); }
            public bool HasAction => Current != null;
            public bool IsBasicAction => Current != null && Current.Basic;
            public bool Dashing => Dash.IsDashing(Now);
            public bool MissileFlying => Current != null && Current.Missile;
            public bool CanRequestSpecial => HasSpecialCharge;
            public bool HasSpecialCharge => Charges[Selected].Read().Available > 0;
            public CombatCommandReason SpecialUnavailableReason => CombatCommandReason.Cooldown;
            public CombatActionAttempt DropThrough(CombatCommandKey command) { Event("drop"); return CombatActionAttempt.Start(Guid.NewGuid(), immediate: true); }
            public CombatActionAttempt TryDash(CombatCommandKey command)
            {
                Event("dash-start"); if (!Dash.CanStart(Now)) return CombatActionAttempt.Reject(CombatCommandReason.Cooldown);
                Current = null; Dash.Begin(Now, Input.Current.Axis == 0 ? Facing : Input.Current.Axis, Movement); return CombatActionAttempt.Start(Guid.NewGuid());
            }
            public CombatActionAttempt TryJump(CombatCommandKey command)
            {
                Event("jump-start"); if (!Jumps.Press(Now, Movement, out float velocity)) return CombatActionAttempt.Buffer();
                Current = null; Dash.Cancel(Now); VelocityY = velocity; Grounded = false; Tick.HadGround = false; return CombatActionAttempt.Start(Guid.NewGuid(), immediate: true);
            }
            public CombatActionAttempt TryApproachRecast(CombatCommandKey command) { Event("recast"); return CombatActionAttempt.Reject(CombatCommandReason.Unavailable); }
            public CombatActionAttempt RequestMissileDetonation(CombatCommandKey command) { Event("detonate"); return CombatActionAttempt.Start(Guid.NewGuid(), immediate: true); }
            public CombatActionAttempt StartBasic(CombatCommandKey command) { Event("basic-start"); Current = new Cast("basic") { Basic = true }; return CombatActionAttempt.Start(Guid.NewGuid()); }
            public CombatActionAttempt StartNextBasic(CombatCommandKey command, float completedAt) { Event("basic-next"); Current = new Cast("next") { Basic = true }; return CombatActionAttempt.Start(Guid.NewGuid()); }
            public CombatActionAttempt StartSpecial(CombatCommandKey command, float? startAt) { Event("special-start"); Current = new Cast("special"); return CombatActionAttempt.Start(Guid.NewGuid()); }
            public void OpenComboWindow(float completedAt) => Event("combo-window");
            public CombatActionAttempt SwitchWeapon(CombatCommandKey command) { Event("swap"); Selected = 1 - Selected; return CombatActionAttempt.Start(Guid.NewGuid(), immediate: true); }
            public void RecordOutcome(CombatCommandKey command, CombatCommandState state, CombatCommandReason reason, Guid executionId = default, string detail = null) { }
        }
        struct Host : IPlayerTickHost
        {
            public readonly State State;
            List<bool>.Enumerator pits;
            int pitIndex;
            public int PitEnds;
            public Host(State state) { State = state; pits = default; pitIndex = -1; PitEnds = 0; }
            public bool InputBlocked => State.Blocked;
            public void ClearInputState() { State.Event("clear-state"); State.Input.Clear(); }
            public int WeaponCount => State.Charges.Length;
            public void TickWeaponCooldown(int index)
            {
                State.CooldownTimes.Add(State.Now); State.Charges[index].Tick(State.Now); State.Event("cooldown:" + index);
            }
            public bool Dead => State.Dead;
            public void ResolveShurikenReturns() => State.Event("returns");
            public CombatInput CurrentInput => State.Input.Current;
            public void EvaluateActions(CombatInput input) { State.Event("evaluate"); State.Arbiter.Evaluate(input); }
            public void ApplyJumpRelease(CombatInput input) { }
            public void ClearPressed() { State.Input.ClearPressed(); State.Event("clear-pressed"); }
            public bool Dashing => State.Dashing;
            public int Axis => State.Input.Current.Axis;
            public float RunSpeed => State.RunSpeed;
            public float VelocityX => State.VelocityX;
            public float GroundAccelerationSeconds => State.Movement.GroundAccelerationSeconds;
            public float GroundDecelerationSeconds => State.Movement.GroundDecelerationSeconds;
            public float AirAccelerationSeconds => State.Movement.AirAccelerationSeconds;
            public float AirDecelerationSeconds => State.Movement.AirDecelerationSeconds;
            public bool HasCurrentCast => State.Current != null;
            public bool CastIsBasic => State.Current.Basic;
            public void SyncFacing(int direction) { State.Facing = direction; State.Event("face:" + direction); }
            public void FollowBasicFacing() { var cast = State.Current; State.Event(cast.Id + ":follow"); }
            public void BeforeCastMovement(float step) { var cast = State.Current; State.Event(cast.Id + ":before"); }
            public float CastHorizontalVelocity(int axis) { var cast = State.Current; State.Event(cast.Id + ":velocity"); return cast.Horizontal; }
            public bool CastFreezeVertical { get { var cast = State.Current; State.Event(cast.Id + ":freeze"); return cast.Freeze; } }
            public bool CastHasDirectMeleeSpecial => State.Current.Direct;
            public bool CastHasChargePolicy => State.Current.Charge;
            public bool Grounded => State.Grounded;
            public string CastVerticalPolicy => State.Current.Vertical;
            public bool CastRecoiling => State.Current.Recoil;
            public bool CastApproaching => State.Current.Approach;
            public bool CastMissileFlying => State.Current.Missile;
            public void ApplyDashMotion(float step, ref float horizontal, ref bool freezeVertical)
            {
                State.Event("dash-motion"); State.Dash.ApplyMotion(State.Now, State.Movement, ref horizontal, ref freezeVertical, step);
            }
            public void PhysicsStep(float step, float horizontal, bool freezeVertical)
            {
                State.StepDuration = step; State.VelocityX = horizontal; State.Freeze = freezeVertical;
                State.Event("physics");
            }
            public bool EndDashAtWall() { State.Event("wall"); return State.Dash.EndAtWall(State.Now, State.Wall); }
            public void ClearVerticalVelocity() { State.VelocityY = 0; State.VerticalClears++; State.Event("clear-y"); }
            public void AfterCastMovement() { var cast = State.Current; State.Event(cast.Id + ":after"); }
            public void Landed()
            {
                State.Landings++;
                if (State.Jumps.Land(State.Now, State.Movement, out float velocity)) { State.VelocityY = velocity; State.Grounded = false; }
                State.Event("land");
            }
            public bool ProbeSafeFloor(out float safeY) { State.Event("safe-probe"); safeY = State.SafeY; return State.SafeFloor; }
            public bool RecoveryRecordingSafe() { State.Event("recovery-safe"); return State.RecoverySafe; }
            public void RecordSafeFloor(float safeY)
            {
                State.Records.Add(new[] { State.Now, State.X, safeY, State.Facing }); State.Event("record");
            }
            public void BeginPits() { pits = State.Pits.GetEnumerator(); State.Event("pits-begin"); }
            public bool NextPit() { State.Event("pit-next"); if (!pits.MoveNext()) return false; pitIndex++; return true; }
            public bool CurrentPitIntersects() { State.Event("pit:" + pitIndex); return pits.Current; }
            public void EndPits() { pits.Dispose(); PitEnds++; State.Event("pits-end"); }
            public void Fall() { State.Falls++; State.Tick.HadGround = false; State.Event("fall"); }
            public void AfterMovementActions() { State.Event("arbiter-after"); State.Arbiter.AfterMovement(); }
        }
        static Host Step(State state, float step = .02f)
        {
            var host = new Host(state); state.Tick.Step(step, ref host); return host;
        }

        [Test] public void ExecutionAndContractHaveNoNativeSceneOrPresentationDependency()
        {
            foreach (var type in new[] { typeof(PlayerTickExecution), typeof(IPlayerTickHost) })
                foreach (var reference in type.Assembly.GetReferencedAssemblies())
                    Assert.That(reference.Name.StartsWith("UnityEngine") || reference.Name.StartsWith("UnityEditor") || reference.Name == "Deviant.Combat" ||
                        reference.Name.StartsWith("Deviant.World") || reference.Name.StartsWith("Deviant.UI") || reference.Name.StartsWith("Deviant.Presentation"), Is.False, reference.Name);
        }
        [Test] public void TickOrdersExistingOwnersAndDisposesTheNativePitCursorByReference()
        {
            var state = new State { Current = new Cast("basic") { Basic = true }, Grounded = true };
            state.Input.Submit(new CombatInput(1, CombatButtons.None)); state.Pits.AddRange(new[] { false, false });
            var host = Step(state);
            Assert.That(state.Events, Is.EqualTo(new[] { "cooldown:0", "cooldown:1", "returns", "evaluate", "clear-pressed", "face:1", "basic:follow",
                "basic:before", "basic:velocity", "basic:freeze", "dash-motion", "physics", "wall", "basic:after", "land", "safe-probe", "recovery-safe", "record",
                "pits-begin", "pit-next", "pit:0", "pit-next", "pit:1", "pit-next", "pits-end", "arbiter-after" }));
            Assert.That(host.PitEnds, Is.EqualTo(1)); Assert.That(state.Tick.HadGround, Is.True); Assert.That(state.Landings, Is.EqualTo(1));
            Assert.That(state.VelocityX, Is.EqualTo(3)); Assert.That(state.StepDuration, Is.EqualTo(.02f));
        }
        [Test] public void BlockedInputClearsBeforeEachCooldownReadsTheCurrentCallerTime()
        {
            var state = new State { Blocked = true };
            state.Input.Submit(new CombatInput(-1, CombatButtons.Basic | CombatButtons.Dash, true));
            Assert.That(state.Charges[0].Spend(0) && state.Charges[1].Spend(0), Is.True);
            state.Hooks["cooldown:0"] = s => { Assert.That(s.Input.Current, Is.EqualTo(default(CombatInput))); s.Now = 2; };
            Step(state);
            Assert.That(state.Events.GetRange(0, 3), Is.EqualTo(new[] { "clear-state", "cooldown:0", "cooldown:1" }));
            Assert.That(state.CooldownTimes, Is.EqualTo(new[] { 1f, 2f }));
            Assert.That(state.Charges[0].Read().Available, Is.EqualTo(1)); Assert.That(state.Charges[1].Read().Available, Is.EqualTo(1));
            Assert.That(state.Current, Is.Null); Assert.That(state.Dashing, Is.False); Assert.That(state.VelocityX, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void DeadPlayersTickCooldownsThenClearOnlyTheAppropriateInputAndReturn(bool blocked)
        {
            var state = new State { Dead = true, Blocked = blocked };
            state.Input.Submit(new CombatInput(-1, CombatButtons.Basic, true)); Step(state);
            Assert.That(state.Events, Is.EqualTo(blocked ? new[] { "clear-state", "cooldown:0", "cooldown:1", "clear-pressed" } : new[] { "cooldown:0", "cooldown:1", "clear-pressed" }));
            Assert.That(state.Input.Current.Pressed, Is.EqualTo(CombatButtons.None)); Assert.That(state.Input.Current.Axis, Is.EqualTo(blocked ? 0 : -1));
            Assert.That(state.Input.Current.SpecialHeld, Is.EqualTo(!blocked)); Assert.That(state.Tick.HadGround, Is.False);
        }
        [Test] public void AWeaponCooldownFailureKeepsEarlierResourceChangesAndDoesNotClearOrEvaluateInput()
        {
            var state = new State(); state.Input.Submit(new CombatInput(1, CombatButtons.Basic));
            state.Charges[0].Spend(0); state.Charges[1].Spend(0);
            var failure = new InvalidOperationException("cooldown"); state.Hooks["cooldown:0"] = s => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state)), Is.SameAs(failure));
            Assert.That(state.Charges[0].Read().Available, Is.EqualTo(1)); Assert.That(state.Charges[1].Read().Available, Is.Zero);
            Assert.That(state.Input.Current.Pressed, Is.EqualTo(CombatButtons.Basic)); Assert.That(state.Events, Is.EqualTo(new[] { "cooldown:0" }));
        }
        [Test] public void ReturnsCanClearPressedInputBeforeTheRealArbiterSeesIt()
        {
            var state = new State(); state.Input.Submit(new CombatInput(1, CombatButtons.Dash | CombatButtons.Basic));
            state.Hooks["returns"] = s => s.Input.ClearPressed(); Step(state);
            Assert.That(state.Events, Does.Not.Contain("dash-start")); Assert.That(state.Events, Does.Not.Contain("basic-start"));
            Assert.That(state.VelocityX, Is.EqualTo(5)); Assert.That(state.Input.Current.Axis, Is.EqualTo(1));
        }
        [Test] public void ACurrentInputReadAfterReturnsCanStartTheRealArbiterOnAnOtherwiseEmptyTick()
        {
            var state = new State();
            state.Hooks["returns"] = s => s.Input.Submit(new CombatInput(-1, CombatButtons.Basic, true)); Step(state);
            Assert.That(state.Current, Is.Not.Null); Assert.That(state.Current.Basic, Is.True); Assert.That(state.Facing, Is.EqualTo(-1));
            Assert.That(state.Input.Current.Pressed, Is.EqualTo(CombatButtons.None)); Assert.That(state.Input.Current.SpecialHeld, Is.True);
            Assert.That(state.Events.IndexOf("basic-start"), Is.GreaterThan(state.Events.IndexOf("returns")));
        }
        [Test] public void RealArbiterAndDashKeepPriorityAndDoNotReplayConsumedEdges()
        {
            var state = new State(); state.Input.Submit(new CombatInput(-1, CombatButtons.Dash | CombatButtons.Jump | CombatButtons.Swap | CombatButtons.Special | CombatButtons.Basic));
            Step(state); Assert.That(state.Events, Does.Contain("dash-start")); Assert.That(state.Events, Does.Not.Contain("jump-start"));
            Assert.That(state.Current, Is.Null); Assert.That(state.Selected, Is.Zero); Assert.That(state.VelocityX, Is.EqualTo(-15)); Assert.That(state.Freeze, Is.True);
            state.Events.Clear(); state.Now += .02f; Step(state);
            Assert.That(state.Events, Does.Not.Contain("dash-start")); Assert.That(state.Jumps.Used, Is.Zero); Assert.That(state.VelocityX, Is.EqualTo(-15));
        }
        [Test] public void ANoInputTickStillRunsMovementAndCommitsAnExistingSwapOnlyAfterItEndsTheDash()
        {
            var state = new State(); state.Dash.Begin(state.Now, 1, state.Movement);
            state.Arbiter.Evaluate(new CombatInput(0, CombatButtons.Swap)); Assert.That(state.Arbiter.Reserved, Is.EqualTo(ReservedPlayerAction.Swap));
            state.Hooks["physics"] = s => s.Wall = true; Step(state);
            Assert.That(state.Selected, Is.EqualTo(1)); Assert.That(state.Arbiter.Reserved, Is.EqualTo(ReservedPlayerAction.None));
            Assert.That(state.Events.IndexOf("swap"), Is.GreaterThan(state.Events.IndexOf("pits-end")));
        }
        [Test] public void EveryEnteredCastCallbackUsesTheCurrentReferenceRatherThanAnEarlierCastSnapshot()
        {
            var state = new State { Current = new Cast("initial") { Basic = true } }; state.Input.SetContinuous(1, false);
            state.Hooks["face:1"] = s => s.Current = new Cast("follow");
            state.Hooks["follow:follow"] = s => { s.Current = new Cast("before"); s.Input.SetContinuous(-1, false); };
            state.Hooks["before:before"] = s => s.Current = new Cast("velocity") { Horizontal = 23 };
            state.Hooks["velocity:velocity"] = s => s.Current = new Cast("freeze") { Freeze = true };
            state.Hooks["freeze:freeze"] = s => { s.Current = new Cast("fallback"); s.RunSpeed = 7; };
            state.Hooks["physics"] = s => s.Current = new Cast("after");
            state.Hooks["after:after"] = s => s.Grounded = true;
            Step(state);
            Assert.That(state.Events.GetRange(5, 10), Is.EqualTo(new[] { "face:1", "follow:follow", "before:before", "velocity:velocity", "freeze:freeze", "dash-motion", "physics", "wall", "after:after", "land" }));
            Assert.That(state.VelocityX, Is.EqualTo(-7)); Assert.That(state.Freeze, Is.True); Assert.That(state.Tick.HadGround, Is.True);
        }
        [Test] public void ClearingTheCastDuringBeforeMovementKeepsTheAlreadyComputedRunVelocityButUsesLiveIdleFacing()
        {
            var state = new State { Current = new Cast("cast") }; state.Input.SetContinuous(1, false);
            state.Hooks["cast:before"] = s => { s.Current = null; s.RunSpeed = 7; s.Input.SetContinuous(-1, false); };
            Step(state);
            Assert.That(state.VelocityX, Is.EqualTo(5)); Assert.That(state.Freeze, Is.False); Assert.That(state.Facing, Is.EqualTo(-1));
            Assert.That(state.Events, Does.Not.Contain("cast:velocity")); Assert.That(state.Events, Does.Not.Contain("cast:freeze")); Assert.That(state.Events, Does.Not.Contain("cast:after"));
        }
        [Test] public void AHorizontalVelocityCallbackThatClearsTheCastDoesNotReadFreezeOrAfterFromTheOldReference()
        {
            var state = new State { Current = new Cast("cast") { Horizontal = 9, Freeze = true } }; state.Input.SetContinuous(-1, false);
            state.Hooks["cast:velocity"] = s => s.Current = null; Step(state);
            Assert.That(state.VelocityX, Is.EqualTo(9)); Assert.That(state.Freeze, Is.False); Assert.That(state.Facing, Is.EqualTo(-1));
            Assert.That(state.Events, Does.Not.Contain("cast:freeze")); Assert.That(state.Events, Does.Not.Contain("cast:after"));
        }
        [TestCase("normal", -5)] [TestCase("direct", 3)] [TestCase("charge", 3)] [TestCase("ground", 3)]
        [TestCase("vertical", 3)] [TestCase("recoil", 3)] [TestCase("approach", 3)] [TestCase("missile", 3)]
        public void AirNormalRunFallbackRetainsEachExistingCastException(string exception, float expected)
        {
            var cast = new Cast("cast") { Direct = exception == "direct", Charge = exception == "charge", Recoil = exception == "recoil",
                Approach = exception == "approach", Missile = exception == "missile", Vertical = exception == "vertical" ? "hover" : "normal" };
            var state = new State { Current = cast, Grounded = exception == "ground" }; state.Input.SetContinuous(-1, false); Step(state);
            Assert.That(state.VelocityX, Is.EqualTo(expected));
        }
        [TestCase("wall", 2)] [TestCase("deadline", 1)] [TestCase("cancel", 1)]
        public void WallAndNaturalDashEndClearYAfterTheFullMovementAndBeforeTheLiveCastAfterMovement(string ending, int clears)
        {
            var state = new State { Current = new Cast("cast") }; state.Dash.Begin(state.Now, 1, state.Movement);
            float ready = state.Dash.Read().ReadyAt;
            state.Hooks["physics"] = s => { if (ending == "wall") s.Wall = true; else if (ending == "deadline") s.Now = s.Dash.Read().EndsAt; else s.Dash.Cancel(s.Now); };
            state.Hooks["cast:after"] = s => Assert.That(s.VelocityY, Is.Zero); Step(state, .25f);
            Assert.That(state.VelocityX, Is.EqualTo(15)); Assert.That(state.Freeze, Is.True); Assert.That(state.StepDuration, Is.EqualTo(.25f));
            Assert.That(state.VerticalClears, Is.EqualTo(clears)); Assert.That(state.Dash.Read().ReadyAt, Is.EqualTo(ready));
        }
        [Test] public void BufferedLandingReadsGroundAgainAfterTheActualJumpOwnerAndStableGroundDoesNotLandTwice()
        {
            var state = new State { Grounded = true };
            state.Jumps.Press(state.Now, state.Movement, out _); state.Jumps.Press(state.Now, state.Movement, out _); state.Jumps.Press(state.Now, state.Movement, out _);
            Step(state); Assert.That(state.Landings, Is.EqualTo(1)); Assert.That(state.Jumps.Used, Is.EqualTo(1));
            Assert.That(state.Grounded || state.Tick.HadGround, Is.False); Assert.That(state.VelocityY, Is.EqualTo(20));
            state.Grounded = true; Step(state); Assert.That(state.Landings, Is.EqualTo(2)); Assert.That(state.Jumps.Used, Is.Zero);
            state.Jumps.Press(state.Now, state.Movement, out _); Step(state);
            Assert.That(state.Landings, Is.EqualTo(2)); Assert.That(state.Jumps.Used, Is.EqualTo(1)); Assert.That(state.Tick.HadGround, Is.True);
        }
        [Test] public void SynchronousReturnAndMovementCallbacksMutateTheSameEmbeddedGroundOwner()
        {
            var state = new State { Grounded = true };
            state.Hooks["returns"] = s => s.Tick.HadGround = true; Step(state); Assert.That(state.Landings, Is.Zero);
            state.Hooks.Clear(); state.Hooks["physics"] = s => s.Tick.HadGround = false; Step(state);
            Assert.That(state.Landings, Is.EqualTo(1)); Assert.That(state.Tick.HadGround, Is.True);
        }
        [TestCase("probe")] [TestCase("hazard")] [TestCase("dash")]
        public void SafeHistoryRecordingPreservesItsExistingShortCircuitOrder(string blockedBy)
        {
            var state = new State { SafeFloor = blockedBy != "probe", RecoverySafe = blockedBy != "hazard" };
            if (blockedBy == "dash") state.Dash.Begin(state.Now, 1, state.Movement); Step(state);
            Assert.That(state.Records, Is.Empty);
            if (blockedBy == "dash") Assert.That(state.Events, Does.Not.Contain("safe-probe"));
            if (blockedBy != "hazard") Assert.That(state.Events, Does.Not.Contain("recovery-safe"));
            Assert.That(state.Events, Does.Contain("pits-begin"));
        }
        [Test] public void RecordingReadsCurrentFactsAndOnlyTheFirstIntersectingPitFallsBeforeTheFinalArbiter()
        {
            var state = new State { Grounded = true }; state.Pits.AddRange(new[] { false, true, true });
            state.Hooks["safe-probe"] = s => { s.X = 9; s.Facing = -1; s.SafeY = 12; };
            state.Hooks["recovery-safe"] = s => s.Now = 3;
            state.Hooks["fall"] = s => s.Dead = true;
            Step(state);
            Assert.That(state.Records[0], Is.EqualTo(new[] { 3f, 9f, 12f, -1f })); Assert.That(state.Falls, Is.EqualTo(1));
            Assert.That(state.Events, Does.Not.Contain("pit:2")); Assert.That(state.Tick.HadGround, Is.False);
            Assert.That(state.Events.GetRange(state.Events.Count - 3, 3), Is.EqualTo(new[] { "fall", "pits-end", "arbiter-after" }));
        }
        [TestCase("returns", false)] [TestCase("cast:before", false)] [TestCase("physics", false)]
        [TestCase("cast:after", false)] [TestCase("land", false)] [TestCase("safe-probe", true)] [TestCase("record", true)] [TestCase("fall", false)]
        public void CallbackFailuresKeepTheirAppliedPrefixWithoutInventingLaterMovementOrGroundWrites(string callback, bool hadGround)
        {
            var state = new State { Current = new Cast("cast"), Grounded = true }; state.Pits.Add(true);
            state.Input.SetContinuous(1, true); state.Input.Submit(new CombatInput(1, CombatButtons.Basic, true));
            var failure = new InvalidOperationException(callback); state.Hooks[callback] = s => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state)), Is.SameAs(failure));
            Assert.That(state.Tick.HadGround, Is.EqualTo(hadGround));
            Assert.That(state.Input.Current.Pressed, Is.EqualTo(callback == "returns" ? CombatButtons.Basic : CombatButtons.None));
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo(callback == "fall" ? "pits-end" : callback));
            Assert.That(state.Events, Does.Not.Contain("arbiter-after"));
            Assert.That(state.Events.Contains("physics"), Is.EqualTo(callback != "returns" && callback != "cast:before"));
        }
        [Test] public void AThrowingPitQueryDisposesItsEnteredCursorAndDoesNotFallOrReachTheFinalArbiter()
        {
            var state = new State { Grounded = true }; state.Pits.Add(true);
            var failure = new InvalidOperationException("pit bounds"); state.Hooks["pit:0"] = s => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state)), Is.SameAs(failure));
            Assert.That(state.Tick.HadGround, Is.True); Assert.That(state.Falls, Is.Zero); Assert.That(state.Records.Count, Is.EqualTo(1));
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo("pits-end")); Assert.That(state.Events, Does.Not.Contain("arbiter-after"));
        }
        [Test] public void OrdinaryMovementAcceleratesFromTheCommittedVelocityWithGroundAndAirRates()
        {
            var data = new State().Movement.ToData();
            data.horizontalControl = new HorizontalControlData { groundAccelerationSeconds = .05f, groundDecelerationSeconds = .03f,
                airAccelerationSeconds = .1f, airDecelerationSeconds = .08f };
            var state = new State(data) { Grounded = true };
            state.Input.Submit(new CombatInput(1)); Step(state);
            Assert.That(state.VelocityX, Is.EqualTo(2).Within(.0001f), "ground accelerates 5u/s over 0.05s");
            state.Grounded = false; state.Input.SetContinuous(-1, false); Step(state);
            Assert.That(state.VelocityX, Is.EqualTo(.75f).Within(.0001f), "air reversal decelerates at 5u/s over 0.08s");
            state.VelocityX = 9; state.Input.SetContinuous(1, false); Step(state);
            Assert.That(state.VelocityX, Is.EqualTo(5), "faster special motion is clamped before ordinary control");
        }
        [Test] public void ClearingTheCastDuringFacingDoesNotAddANewGuardAroundTheAlreadyEnteredFollowCallback()
        {
            var state = new State { Current = new Cast("basic") { Basic = true } }; state.Input.SetContinuous(1, false);
            state.Hooks["face:1"] = s => s.Current = null;
            Assert.Throws<NullReferenceException>(() => Step(state));
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo("face:1")); Assert.That(state.Events, Does.Not.Contain("dash-motion"));
        }
    }
}
