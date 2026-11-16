using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class EnemyTickExecutionTests
    {
        sealed class Candidate
        {
            public readonly int Id;
            public readonly ActorVitalsState Vitals = new ActorVitalsState(100);
            public float X = 2, Y;
            public bool Decoy, Occluded, RejectReceive;
            public Action<State> OnReceive;
            public Candidate(int id) { Id = id; }
        }
        sealed class Motor
        {
            public float X, Y, Horizontal, StepDuration;
            public int Facing = 1, Steps;
            public bool Grounded, FreezeVertical;
            public void Step(float step, float horizontal, bool freezeVertical)
            {
                Steps++; StepDuration = step; Horizontal = horizontal; FreezeVertical = freezeVertical;
                X += horizontal * step;
            }
        }
        readonly struct Claim
        {
            public readonly int Hit, Target;
            public readonly bool Accepted;
            public Claim(int hit, int target, bool accepted) { Hit = hit; Target = target; Accepted = accepted; }
        }
        readonly struct Receipt
        {
            public readonly int Target, Facing;
            public readonly float Damage;
            public Receipt(int target, int facing, float damage) { Target = target; Facing = facing; Damage = damage; }
        }
        sealed class State : IEnemyAttackHost
        {
            public readonly ActorVitalsState Vitals = new ActorVitalsState(100);
            public readonly EnemyAttackExecution Attack = new EnemyAttackExecution();
            public readonly Motor Motor = new Motor();
            public readonly Candidate Player = new Candidate(1);
            public readonly List<Candidate> Candidates = new List<Candidate>();
            public readonly List<string> Events = new List<string>();
            public readonly Dictionary<string, Action<State>> Hooks = new Dictionary<string, Action<State>>();
            public readonly List<Claim> Claims = new List<Claim>();
            public readonly List<Receipt> Receipts = new List<Receipt>();
            public Candidate Target;
            public int Profile, Queries, CursorEnds, Flashes, Projectiles, FloorFacing, FacingRequest;
            public float Time = 1, StunUntil, StaggerUntil, FloorX, FloorY;
            public bool Aggressive = true, FloorExists = true, ShowBoxes;
            public State() { Target = Player; Candidates.Add(Player); }
            public void Event(string name) { Events.Add(name); if (Hooks.TryGetValue(name, out var callback)) callback(this); }
            int IEnemyAttackHost.Profile { get { Event("attack-profile"); return Profile; } }
            float IEnemyAttackHost.Now { get { Event("attack-now"); return Time; } }
            public void MeleeHit(int index)
            {
                var host = new MeleeHost(this); EnemyTickExecution.MeleeHit(index, ref host);
            }
            public void FireProjectile() { Event("projectile"); Projectiles++; }
        }
        struct TickHost : IEnemyTickHost, ICombatAICommandHost
        {
            readonly State state;
            Candidate target;
            public TickHost(State state) { this.state = state; target = null; }
            public bool Dead { get { state.Event("dead"); return state.Vitals.Read().Dead; } }
            public void CaptureTarget() { state.Event("target"); target = state.Target; }
            public float Now { get { state.Event("now"); return state.Time; } }
            public float StunUntil { get { state.Event("stun"); return state.StunUntil; } }
            public float StaggerUntil { get { state.Event("stagger"); return state.StaggerUntil; } }
            public EnemyAttackSnapshot ReadAttack() { state.Event("attack-read"); return state.Attack.Read(); }
            public int Profile { get { state.Event("profile"); return state.Profile; } }
            public bool Aggressive { get { state.Event("aggressive"); return state.Aggressive; } }
            public bool HasTarget { get { state.Event("has-target"); return target != null; } }
            public float TargetDeltaX { get { state.Event("delta-x"); return target.X - state.Motor.X; } }
            public float TargetDeltaY { get { state.Event("delta-y"); return target.Y - state.Motor.Y; } }
            public float ApplyIntent(EnemyIntent intent)
            {
                var command = new CombatAICommand(default, 1, intent, 0);
                var host = this; return CombatAICommandExecution.Apply(command, ref host).Horizontal;
            }
            public Guid BeginAttack(CombatCommandKey command) { state.Attack.Begin(Now); state.Event("begin"); return Guid.NewGuid(); }
            public void SyncFacing(int direction)
            {
                state.FacingRequest = direction; state.Motor.Facing = direction; state.Event("face");
            }
            public int Facing => state.Motor.Facing;
            public bool Grounded => state.Motor.Grounded;
            public bool HasChaseFloor()
            {
                state.FloorX = state.Motor.X; state.FloorY = state.Motor.Y; state.FloorFacing = state.Motor.Facing;
                state.Event("floor"); return state.FloorExists;
            }
            public void PhysicsStep(float step, float horizontal, bool freezeVertical)
            {
                state.Motor.Step(step, horizontal, freezeVertical); state.Event("physics");
            }
            public void StepAttack(float step, bool controlled)
            {
                state.Event("attack-step"); state.Attack.Step(step, controlled, state);
            }
        }
        struct MeleeHost : IEnemyMeleeHitHost
        {
            readonly State state;
            List<Candidate>.Enumerator targets;
            Candidate target;
            public MeleeHost(State state) { this.state = state; targets = default; target = null; }
            public void BeginTargets()
            {
                state.Queries++; state.Event("box"); targets = state.Candidates.GetEnumerator();
            }
            public bool NextTarget()
            {
                state.Event("next"); if (!targets.MoveNext()) return false;
                target = targets.Current; return true;
            }
            public bool TargetIsNotPlayer { get { state.Event("player:" + target.Id); return target != state.Player; } }
            public bool TargetIsDecoy { get { state.Event("decoy:" + target.Id); return target.Decoy; } }
            public bool TargetDead { get { state.Event("dead:" + target.Id); return target.Vitals.Read().Dead; } }
            public bool TargetOccluded { get { state.Event("occluded:" + target.Id); return target.Occluded; } }
            public bool FirstHit(int hitIndex)
            {
                bool accepted = state.Attack.FirstHit(hitIndex, target.Id);
                state.Claims.Add(new Claim(hitIndex, target.Id, accepted)); state.Event("claim:" + target.Id); return accepted;
            }
            public int Profile { get { state.Event("damage-profile"); return state.Profile; } }
            public void Receive(float damage)
            {
                state.Receipts.Add(new Receipt(target.Id, state.Motor.Facing, damage)); state.Event("receive:" + target.Id);
                if (!target.RejectReceive) target.Vitals.ApplyDamage(damage, state.Time, false);
                target.OnReceive?.Invoke(state);
            }
            public void EndTargets() { targets.Dispose(); state.CursorEnds++; state.Event("box-end"); }
            public bool ShowBoxes { get { state.Event("boxes"); return state.ShowBoxes; } }
            public void AddFlash() { state.Event("flash"); state.Flashes++; }
        }
        static void Step(State state, float step = .02f)
        {
            var host = new TickHost(state); EnemyTickExecution.Step(step, ref host);
        }
        static State Active(int profile = 0, float now = .35f)
        {
            var state = new State { Profile = profile, Time = now, Aggressive = false };
            state.Attack.Begin(0); return state;
        }

        [Test] public void ContractsAndStatelessExecutionHaveNoNativeSceneOrPresentationDependency()
        {
            foreach (var type in new[] { typeof(EnemyTickExecution), typeof(IEnemyTickHost), typeof(IEnemyMeleeHitHost) })
                foreach (var reference in type.Assembly.GetReferencedAssemblies())
                    Assert.That(reference.Name.StartsWith("UnityEngine") || reference.Name.StartsWith("UnityEditor") || reference.Name == "Deviant.Combat" ||
                        reference.Name.StartsWith("Deviant.World") || reference.Name.StartsWith("Deviant.UI") || reference.Name.StartsWith("Deviant.Presentation"), Is.False, reference.Name);
            Assert.That(typeof(EnemyTickExecution).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public), Is.Empty);
        }
        [Test] public void DeadEnemiesReturnBeforeTargetStatusMovementOrAnExistingAttack()
        {
            var state = Active(); state.Vitals.WriteHealth(0); Step(state, 1);
            Assert.That(state.Events, Is.EqualTo(new[] { "dead" })); Assert.That(state.Motor.Steps, Is.Zero);
            Assert.That(state.Attack.Read().Attacking, Is.True); Assert.That(state.Queries, Is.Zero);
        }
        [Test] public void ObservationReadsKeepTheirOriginalOrderAndBeginPrecedesFacingAndPhysics()
        {
            var state = new State();
            state.Hooks["face"] = s => {
                Assert.That(s.Attack.Read().Attacking, Is.True); Assert.That(s.Attack.Read().StartedAt, Is.EqualTo(1));
                Assert.That(s.Attack.Read().Sequence, Is.EqualTo(1)); Assert.That(s.Motor.Steps, Is.Zero);
            };
            Step(state);
            Assert.That(state.Events.GetRange(0, 20), Is.EqualTo(new[] { "dead", "target", "now", "stun", "now", "stagger", "attack-read", "profile",
                "aggressive", "has-target", "now", "has-target", "delta-x", "has-target", "delta-y", "now", "begin", "face", "physics", "attack-step" }));
            Assert.That(state.Motor.Horizontal, Is.Zero); Assert.That(state.Motor.FreezeVertical, Is.False);
            Assert.That(state.Motor.StepDuration, Is.EqualTo(.02f)); Assert.That(state.Attack.Read().Attacking, Is.True);
        }
        [Test] public void TargetIsCapturedBeforeStatusButItsCoordinatesRemainLiveUntilObservation()
        {
            var state = new State(); var original = state.Target; original.X = 9;
            state.Hooks["stun"] = s => s.Target = new Candidate(9) { X = 20 };
            state.Hooks["attack-read"] = s => original.X = -2;
            Step(state);
            Assert.That(state.FacingRequest, Is.EqualTo(-1)); Assert.That(state.Attack.Read().Sequence, Is.EqualTo(1));
            Assert.That(state.Motor.Horizontal, Is.Zero); Assert.That(state.Target.Id, Is.EqualTo(9));
        }
        [Test] public void MissingTargetSkipsBothCoordinateReadsButStillRunsTheMotorAndAttackOwner()
        {
            var state = new State { Target = null }; Step(state);
            Assert.That(state.Events, Does.Not.Contain("delta-x")); Assert.That(state.Events, Does.Not.Contain("delta-y"));
            Assert.That(state.Events, Does.Not.Contain("begin")); Assert.That(state.Events, Does.Not.Contain("face"));
            Assert.That(state.Events, Does.Contain("attack-step")); Assert.That(state.Motor.Steps, Is.EqualTo(1));
            Assert.That(state.Motor.Horizontal, Is.Zero); Assert.That(state.Attack.Read().Sequence, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void ControlIsCapturedBeforePhysicsAndNotRecomputedAfterPhysics(bool initiallyControlled)
        {
            var state = Active(); state.StunUntil = initiallyControlled ? 10 : 0;
            state.Hooks["physics"] = s => { s.StunUntil = initiallyControlled ? 0 : 10; s.StaggerUntil = s.StunUntil; };
            Step(state, .01f);
            Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(initiallyControlled ? 100 : 90));
            Assert.That(state.Queries, Is.EqualTo(initiallyControlled ? 0 : 1)); Assert.That(state.Attack.Read().Attacking, Is.True);
            Assert.That(state.Events.LastIndexOf("stun"), Is.LessThan(state.Events.IndexOf("physics")));
            if (initiallyControlled) Assert.That(state.Events, Does.Not.Contain("stagger"), "The first true control comparison short-circuits the second clock/status read.");
            else Assert.That(state.Events.IndexOf("stagger"), Is.LessThan(state.Events.IndexOf("physics")));
        }
        [Test] public void ATargetCallbackCanControlTheSameTickBeforeTheAttackSnapshotIsRead()
        {
            var state = Active(); state.Hooks["target"] = s => s.StaggerUntil = 2; Step(state, 1);
            Assert.That(state.Motor.Steps, Is.EqualTo(1)); Assert.That(state.Queries, Is.Zero);
            Assert.That(state.Attack.Read().Attacking, Is.True); Assert.That(state.Attack.Read().NextAttackAt, Is.Zero);
        }
        [TestCase(0, true, 2.5f)] [TestCase(0, false, 2.5f)]
        [TestCase(1, true, 2.5f)] [TestCase(1, false, 2.5f)]
        [TestCase(2, true, 1.5f)] [TestCase(2, false, 1.5f)]
        public void ChaseUsesThePostFacingMotorPositionAndDirectionBeforeTheGroundedFloorDecision(int profile, bool floor, float speed)
        {
            var state = new State { Profile = profile, FloorExists = floor }; state.Target.X = -9; state.Motor.Grounded = true;
            state.Hooks["face"] = s => { s.Motor.Facing = 1; s.Motor.X = 12; s.Motor.Y = 3; };
            Step(state, .1f);
            Assert.That(state.FacingRequest, Is.EqualTo(-1)); Assert.That(state.FloorFacing, Is.EqualTo(1));
            Assert.That(state.FloorX, Is.EqualTo(12)); Assert.That(state.FloorY, Is.EqualTo(3));
            Assert.That(state.Motor.Horizontal, Is.EqualTo(floor ? speed : 0));
            Assert.That(state.Motor.X, Is.EqualTo(floor ? 12 + speed * .1f : 12));
            Assert.That(state.Events.IndexOf("floor"), Is.GreaterThan(state.Events.IndexOf("face")));
            Assert.That(state.Events.IndexOf("physics"), Is.GreaterThan(state.Events.IndexOf("floor")));
            Assert.That(state.Motor.FreezeVertical, Is.False); Assert.That(state.Attack.Read().Sequence, Is.Zero);
        }
        [Test] public void AnAirborneChaseDoesNotProbeTheLedgeAndTheMotorStillReceivesIntentSpeed()
        {
            var state = new State { FloorExists = false }; state.Target.X = -9; Step(state, .1f);
            Assert.That(state.Events, Does.Not.Contain("floor")); Assert.That(state.Motor.Horizontal, Is.EqualTo(-2.5f));
            Assert.That(state.Motor.X, Is.EqualTo(-.25f)); Assert.That(state.Motor.FreezeVertical, Is.False);
        }
        [Test] public void StartingUsesThePreFacingClockButAttackProgressReadsTheClockAfterPhysics()
        {
            var state = new State(); state.Hooks["physics"] = s => s.Time = 2; Step(state);
            var attack = state.Attack.Read();
            Assert.That(attack.StartedAt, Is.EqualTo(1)); Assert.That(attack.Sequence, Is.EqualTo(1));
            Assert.That(attack.Attacking, Is.False); Assert.That(attack.NextAttackAt, Is.EqualTo(2.5f));
            Assert.That(state.Queries, Is.Zero, "The original active window was already over at the post-physics clock.");
        }
        [Test] public void DisablingAdmissionOrLosingTheTargetDoesNotCancelAnAlreadyStartedAttack()
        {
            var state = Active(1); state.Target = null; Step(state, .01f);
            Assert.That(state.Projectiles, Is.EqualTo(1)); Assert.That(state.Attack.Read().ProjectileHitIndex, Is.EqualTo(1));
            Assert.That(state.Attack.Read().Attacking, Is.True); Assert.That(state.Motor.Horizontal, Is.Zero);
            Step(state, .01f); Assert.That(state.Projectiles, Is.EqualTo(1)); Assert.That(state.Attack.Read().Sequence, Is.EqualTo(1));
        }
        [Test] public void LongTickStartsTheRealBossAndRunsItsThreeClaimsOnlyAfterPhysics()
        {
            var state = new State { Profile = 2 }; Step(state, 1);
            Assert.That(state.Motor.Steps, Is.EqualTo(1)); Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(40));
            Assert.That(state.Player.Vitals.Read().ReceivedHits, Is.EqualTo(3)); Assert.That(state.Claims.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++) { Assert.That(state.Claims[i].Hit, Is.EqualTo(i)); Assert.That(state.Claims[i].Accepted, Is.True); }
            Assert.That(state.Events.IndexOf("box"), Is.GreaterThan(state.Events.IndexOf("physics")));
            Assert.That(state.Attack.Read().Attacking, Is.True, "Completion uses current elapsed, not the long tick's end.");
            Assert.That(state.CursorEnds, Is.EqualTo(3));
        }
        [Test] public void RejectedReceivesStayClaimedWhileNewTargetsCanEnterTheSameActiveWindow()
        {
            var state = Active(); state.Player.RejectReceive = true; Step(state, .01f);
            state.Player.RejectReceive = false; var late = new Candidate(2) { Decoy = true }; state.Candidates.Add(late);
            state.Time = .4f; Step(state, .01f);
            Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(100)); Assert.That(state.Player.Vitals.Read().ReceivedHits, Is.Zero);
            Assert.That(late.Vitals.Read().Health, Is.EqualTo(90)); Assert.That(state.Receipts.Count, Is.EqualTo(2));
            Assert.That(state.Claims.Count, Is.EqualTo(3)); Assert.That(state.Claims[0].Accepted, Is.True);
            Assert.That(state.Claims[1].Accepted, Is.False); Assert.That(state.Claims[2].Accepted, Is.True);
            Assert.That(state.CursorEnds, Is.EqualTo(2)); state.Time = .44f; Step(state, 1);
            Assert.That(state.Queries, Is.EqualTo(2)); Assert.That(late.Vitals.Read().ReceivedHits, Is.EqualTo(1));
        }
        [Test] public void CandidateFilteringShortCircuitsBeforeDeadOcclusionOrClaimAsAppropriate()
        {
            var state = Active(); var ally = new Candidate(2); var dead = new Candidate(3) { Decoy = true };
            dead.Vitals.WriteHealth(0); var hidden = new Candidate(4) { Decoy = true, Occluded = true };
            state.Candidates.Clear(); state.Candidates.AddRange(new[] { ally, dead, hidden, state.Player });
            state.Hooks["dead:2"] = s => Assert.Fail("Nonplayer/nondecoy candidates never read Dead.");
            state.Hooks["occluded:3"] = s => Assert.Fail("Dead candidates never query occlusion.");
            state.Hooks["decoy:1"] = s => Assert.Fail("The current player comparison short-circuits isDecoy.");
            Step(state, .01f);
            Assert.That(state.Claims.Count, Is.EqualTo(1)); Assert.That(state.Claims[0].Target, Is.EqualTo(1));
            Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(90)); Assert.That(ally.Vitals.Read().Health, Is.EqualTo(100));
            Assert.That(hidden.Vitals.Read().Health, Is.EqualTo(100)); Assert.That(state.CursorEnds, Is.EqualTo(1));
        }
        [Test] public void LaterCandidatesReadCurrentBossDamageFacingAndDebugFactsAfterEarlierDamage()
        {
            var state = Active(2, .45f); var decoy = new Candidate(2) { Decoy = true }; state.Candidates.Add(decoy);
            state.Player.OnReceive = s => { s.Profile = 0; s.Motor.Facing = -1; s.ShowBoxes = true; };
            Step(state, .01f);
            Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(80)); Assert.That(decoy.Vitals.Read().Health, Is.EqualTo(90));
            Assert.That(state.Receipts[0].Damage, Is.EqualTo(20)); Assert.That(state.Receipts[0].Facing, Is.EqualTo(1));
            Assert.That(state.Receipts[1].Damage, Is.EqualTo(10)); Assert.That(state.Receipts[1].Facing, Is.EqualTo(-1));
            Assert.That(state.Flashes, Is.EqualTo(1)); Assert.That(state.Events.IndexOf("flash"), Is.GreaterThan(state.Events.IndexOf("box-end")));
            Assert.That(state.Events.IndexOf("damage-profile"), Is.GreaterThan(state.Events.IndexOf("claim:1")));
        }
        [TestCase("cancel")] [TestCase("reset")] [TestCase("begin")]
        public void DamageCallbacksKeepTheExistingAttackOwnersEnteredBossLoopAndLedgerRules(string callback)
        {
            var state = Active(2, 0); bool changed = false;
            state.Player.OnReceive = s => {
                if (changed) return; changed = true;
                if (callback == "cancel") s.Attack.Cancel(s.Time);
                else if (callback == "reset") s.Attack.Reset(s.Time);
                else s.Attack.Begin(10);
            };
            Step(state, 1);
            Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(40)); Assert.That(state.Queries, Is.EqualTo(3));
            Assert.That(state.Claims[0].Hit, Is.Zero); Assert.That(state.Claims[1].Hit, Is.EqualTo(1)); Assert.That(state.Claims[2].Hit, Is.EqualTo(2));
            var view = state.Attack.Read(); Assert.That(view.Sequence, Is.EqualTo(callback == "begin" ? 2 : 1));
            Assert.That(view.Attacking, Is.EqualTo(callback == "begin"));
            Assert.That(view.NextAttackAt, Is.EqualTo(callback == "cancel" ? .3f : callback == "reset" ? .5f : 0));
        }
        [Test] public void TargetSelectionFailurePropagatesWithoutReadingStatusOrTouchingMovementOrAttack()
        {
            var state = new State(); var error = new InvalidOperationException("target fixture");
            state.Hooks["target"] = s => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state)), Is.SameAs(error));
            Assert.That(state.Events, Is.EqualTo(new[] { "dead", "target" })); Assert.That(state.Motor.Steps, Is.Zero);
            Assert.That(state.Attack.Read().Sequence, Is.Zero);
        }
        [Test] public void FloorFailurePreservesFacingButDoesNotRunPhysicsOrAttack()
        {
            var state = new State(); state.Target.X = -9; state.Motor.Grounded = true;
            var error = new InvalidOperationException("floor fixture"); state.Hooks["floor"] = s => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state)), Is.SameAs(error));
            Assert.That(state.Motor.Facing, Is.EqualTo(-1)); Assert.That(state.Motor.Steps, Is.Zero);
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo("floor")); Assert.That(state.Events, Does.Not.Contain("attack-step"));
        }
        [Test] public void PhysicsFailureRetainsTheNewAttackAndFacingWithoutAdvancingIt()
        {
            var state = new State(); state.Target.X = -2; var error = new InvalidOperationException("motor fixture");
            state.Hooks["physics"] = s => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state, 1)), Is.SameAs(error));
            Assert.That(state.Attack.Read().Sequence, Is.EqualTo(1)); Assert.That(state.Attack.Read().StartedAt, Is.EqualTo(1));
            Assert.That(state.Attack.Read().Attacking, Is.True); Assert.That(state.Motor.Facing, Is.EqualTo(-1));
            Assert.That(state.Motor.Steps, Is.EqualTo(1)); Assert.That(state.Queries, Is.Zero); Assert.That(state.Events, Does.Not.Contain("attack-step"));
        }
        [Test] public void ABoxQueryFailureDoesNotInventACursorClaimDebugFlashOrAttackCompletion()
        {
            var state = Active(); state.ShowBoxes = true; var error = new InvalidOperationException("box fixture");
            state.Hooks["box"] = s => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state, 1)), Is.SameAs(error));
            Assert.That(state.Motor.Steps, Is.EqualTo(1)); Assert.That(state.CursorEnds, Is.Zero); Assert.That(state.Claims, Is.Empty);
            Assert.That(state.Flashes, Is.Zero); Assert.That(state.Attack.Read().Attacking, Is.True);
        }
        [Test] public void ReceiveFailureKeepsEarlierDamageAndItsOwnClaimThenDisposesBeforePropagating()
        {
            var state = Active(); state.ShowBoxes = true;
            var failing = new Candidate(2) { Decoy = true }; var later = new Candidate(3) { Decoy = true };
            state.Candidates.AddRange(new[] { failing, later }); var error = new InvalidOperationException("receive fixture");
            state.Hooks["receive:2"] = s => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state, .01f)), Is.SameAs(error));
            Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(90)); Assert.That(failing.Vitals.Read().Health, Is.EqualTo(100));
            Assert.That(later.Vitals.Read().Health, Is.EqualTo(100)); Assert.That(state.Claims.Count, Is.EqualTo(2));
            Assert.That(state.Claims[1].Accepted, Is.True); Assert.That(state.CursorEnds, Is.EqualTo(1)); Assert.That(state.Flashes, Is.Zero);
            Assert.That(state.Events.GetRange(state.Events.Count - 2, 2), Is.EqualTo(new[] { "receive:2", "box-end" }));
            Assert.That(state.Attack.Read().Attacking, Is.True); Assert.That(state.Attack.Read().NextAttackAt, Is.Zero);
            state.Hooks.Remove("receive:2"); Step(state, .01f);
            Assert.That(state.Player.Vitals.Read().ReceivedHits, Is.EqualTo(1)); Assert.That(failing.Vitals.Read().ReceivedHits, Is.Zero);
            Assert.That(later.Vitals.Read().Health, Is.EqualTo(90)); Assert.That(state.Flashes, Is.EqualTo(1));
        }
        [Test] public void EligibilityFailureDisposesTheCursorBeforeAnyClaimOrLaterCandidate()
        {
            var state = Active(); var error = new InvalidOperationException("occlusion fixture");
            state.Hooks["occluded:1"] = s => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state, .01f)), Is.SameAs(error));
            Assert.That(state.Claims, Is.Empty); Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(100));
            Assert.That(state.Events.GetRange(state.Events.Count - 2, 2), Is.EqualTo(new[] { "occluded:1", "box-end" }));
            Assert.That(state.CursorEnds, Is.EqualTo(1)); Assert.That(state.Flashes, Is.Zero);
        }
        [Test] public void NativeListInvalidationIsNotHiddenAndKeepsTheAlreadyAppliedDamage()
        {
            var state = Active(); state.ShowBoxes = true;
            state.Player.OnReceive = s => s.Candidates.Add(new Candidate(2) { Decoy = true });
            Assert.Throws<InvalidOperationException>(() => Step(state, .01f));
            Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(90)); Assert.That(state.Receipts.Count, Is.EqualTo(1));
            Assert.That(state.CursorEnds, Is.EqualTo(1)); Assert.That(state.Flashes, Is.Zero); Assert.That(state.Attack.Read().Attacking, Is.True);
        }
        [Test] public void DebugFailurePropagatesOnlyAfterDamageAndCursorDisposalWithoutCompletion()
        {
            var state = Active(); state.ShowBoxes = true; var error = new InvalidOperationException("flash fixture");
            state.Hooks["flash"] = s => throw error;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Step(state, .01f)), Is.SameAs(error));
            Assert.That(state.Player.Vitals.Read().Health, Is.EqualTo(90)); Assert.That(state.CursorEnds, Is.EqualTo(1));
            Assert.That(state.Events.GetRange(state.Events.Count - 3, 3), Is.EqualTo(new[] { "box-end", "boxes", "flash" }));
            Assert.That(state.Attack.Read().Attacking, Is.True); Assert.That(state.Flashes, Is.Zero);
        }
    }
}
