using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CastTickExecutionTests
    {
        sealed class State
        {
            public SkillExecutionDefinition Definition;
            public CastLifecycleExecution Lifecycle;
            public SkillHitTrackExecution Hits = new SkillHitTrackExecution(-.000001f);
            public SmokeEffectExecution Smoke;
            public ForcedDashExecution Dash;
            public SkillWaveExecution Wave;
            public CastHitResponseExecution Response;
            public readonly ActorStatusState Status = new ActorStatusState();
            public readonly ActorVitalsState Target = new ActorVitalsState(100);
            public readonly HitLedger Ledger = new HitLedger();
            public readonly List<string> Events = new List<string>();
            public readonly Dictionary<string, Action<State>> Hooks = new Dictionary<string, Action<State>>();
            public readonly List<int> Dispatches = new List<int>();
            public readonly List<float> Commits = new List<float>();
            public MeleeActionRunner Melee;
            public ChargedMeleeExecution Charged;
            public ChargeExecution Charge;
            public float Now, Started, StepDuration, SavedPose, CapturedPose;
            public float ActiveElapsed, RefreshElapsed, RefreshNow, RefreshStep;
            public bool Exists = true, Dead, Overlaps = true, Marked, TargetHit;
            public int DebugBoxes = 3, OwnerReads, DeadReads, AreaCaptures, OverlapReads, Completions, Disposals;
            public float Elapsed => Now - Started;
            public State(SkillData input, bool activated = true)
            {
                Definition = new SkillExecutionDefinition(input);
                Lifecycle = new CastLifecycleExecution(Definition.Activation, Definition.End);
                Smoke = new SmokeEffectExecution(Definition.Effect);
                Dash = new ForcedDashExecution(Definition.Movement);
                if (activated)
                {
                    Lifecycle.ClaimActivation();
                    if (Definition.Effect != null && Definition.Effect.Type == "smoke") Smoke.Activate(Definition.Activation);
                    if (Definition.Effect != null && Definition.Effect.Type == "projectile")
                    { var host = new WaveHost(this); Wave.Activate(0, 0, Definition.Effect, ref host); }
                }
            }
            public void Signal(string operation) { Events.Add(operation); if (Hooks.TryGetValue(operation, out var hook)) hook(this); }
            public void Tick(float step = .125f) { var host = new Host(this); CastTickExecution.Step(step, Definition, ref host); }
            public void Cancel() { var host = new LifecycleHost(this); Lifecycle.Cancel(ref host); }
            public void Dispose() { Disposals++; Charge?.Cancel(); Signal("dispose"); }
            public void ApplyHit(int index)
            {
                var host = new ResponseHost(this, index); Response.ApplyTarget(ref host);
            }
            public void FinishMovement(float step = .125f)
            {
                var result = Dash.AfterMovement(Lifecycle.Activated, Elapsed, step, false);
                Assert.That(result.Ended, Is.True);
            }
            public void BindMelee(float step)
            {
                Assert.That(BasicMeleeDefinition.TryCreate("base_sword_basic_0", new BasicData { damage = 8, impact = .125f,
                    active = .125f, duration = .25f, box = new BoxData { width = 2, height = 2, x = 1, y = 1 } }, out var source, out var error), Is.True, error);
                var definition = new MeleeActionDefinition(source, 1, .65f, .085f, .08f);
                Melee = new MeleeActionRunner(Guid.NewGuid(), definition, Started, new MeleeHost(this));
                Melee.Prepare(Now, step);
            }
            public void BindCharged(float step)
            {
                Charge = new ChargeExecution(Guid.NewGuid(), new ChargeDefinition("cast_tick_charge", .25f, 1, 25, 75, .5f), Started);
                Charged = new ChargedMeleeExecution(Charge, .125f, new ChargedHost(this));
                Charged.BeforeMovement(step, false);
            }
        }
        struct Host : ICastTickHost
        {
            readonly State state;
            public Host(State state) { this.state = state; }
            public void SetStepDuration(float step) { state.StepDuration = step; state.Signal("dt"); }
            public bool OwnerExists { get { state.OwnerReads++; return state.Exists; } }
            public bool OwnerDead { get { state.DeadReads++; return state.Dead; } }
            public void Dispose() => state.Dispose();
            public void ClearDebugBoxes() { state.DebugBoxes = 0; state.Signal("clear-debug"); }
            public bool HasChargedMelee => state.Charged != null;
            public void StepChargedMelee(float step) { state.Signal("charged"); state.Charged.Step(step); }
            public bool HasMeleeAction => state.Melee != null;
            public void StepMeleeAction(float step) { state.Signal("melee"); state.Melee.Step(state.Now, step); }
            public bool MeleeActionTerminal => state.Melee.IsTerminal;
            public bool Activated => state.Lifecycle.Activated;
            public void StepSkillWave(float step)
            {
                state.Signal("wave"); var host = new WaveHost(state); state.Wave.Step(step, state.Definition.Effect, ref host);
            }
            public void EvaluateHitTrack(float step)
            {
                state.Signal("hits"); var host = new HitHost(state); state.Hits.Evaluate(state.Definition.Hits, step, ref host);
            }
            public float Elapsed => state.Elapsed;
            public bool SmokeIsActive(float elapsed)
            {
                state.ActiveElapsed = elapsed; state.Signal("smoke-active"); return state.Smoke.IsActive(elapsed);
            }
            public void CaptureSmokeArea() { state.CapturedPose = state.SavedPose; state.AreaCaptures++; state.Signal("smoke-area"); }
            public void AddSmokeDebugBox() { state.DebugBoxes++; state.Signal("smoke-debug"); }
            public bool SmokeAreaIntersectsOwner { get { state.OverlapReads++; state.Signal("smoke-overlap"); return state.Overlaps; } }
            public float Now => state.Now;
            public bool TryRefreshSmoke(float elapsed, bool ownerOverlaps, float now, float step, out float until)
            {
                state.RefreshElapsed = elapsed; state.RefreshNow = now; state.RefreshStep = step;
                bool refreshed = state.Smoke.TryRefresh(elapsed, ownerOverlaps, now, step, out until);
                state.Signal("smoke-refresh"); return refreshed;
            }
            public float StartedAt => state.Started;
            public float SmokeEnd => state.Smoke.End;
            public void SetOwnerSmokeUntil(float until) { state.Status.WriteSmokeUntil(until); state.Signal("smoke-until"); }
            public bool MovementDone => state.Dash.MovementDone;
            public void EndPrimary()
            {
                state.Signal("end-primary"); var host = new ResponseHost(state); state.Response.EndPrimary(ref host);
            }
            public void CommitHitEvaluation(float elapsed)
            {
                state.Hits.CommitEvaluation(elapsed); state.Commits.Add(elapsed); state.Signal("commit");
            }
            public void AfterHits(float step)
            {
                state.Signal("lifecycle"); var host = new LifecycleHost(state); state.Lifecycle.AfterHits(step, state.Definition, null, ref host);
            }
        }
        readonly struct HitHost : ISkillHitTrackHost
        {
            readonly State state;
            public HitHost(State state) { this.state = state; }
            public bool Activated => state.Lifecycle.Activated;
            public bool Cancelled => state.Lifecycle.Cancelled;
            public bool MotionDone => state.Lifecycle.MotionDone;
            public float Elapsed => state.Elapsed;
            public float ChargeShift => 0;
            public bool DirectHitAllowed => state.Dash.DirectHitAllowed(state.Elapsed);
            public bool TargetHit => state.TargetHit;
            public bool TeleportClaimed => false;
            public bool MarkedTargetAlive => state.Marked && !state.Target.Read().Dead;
            public void ApplyMarkedTarget(int index) => ApplyBox(index, SkillHitBoxAnchor.Actor);
            public void ApplyTeleportTarget(int index) => throw new InvalidOperationException("The cast tick fixture has no teleport geometry.");
            public void ApplyBox(int index, SkillHitBoxAnchor anchor)
            {
                state.Dispatches.Add(index); state.Signal("box:" + index); state.ApplyHit(index);
            }
        }
        readonly struct ResponseHost : ICastHitResponseHost
        {
            readonly State state;
            readonly int index;
            public ResponseHost(State state, int index = 0) { this.state = state; this.index = index; }
            SkillHitDefinition Hit => state.Definition.Hits[index];
            public bool TryClaimTarget() => state.Ledger.First(index, 7);
            public float HitDamage => Hit.Damage;
            public float DamageMultiplier => 1;
            public bool ReceiveControl(float damage)
            {
                bool accepted = state.Target.ApplyDamage(damage, state.Now, false).Accepted; state.Signal("receive:" + index); return accepted;
            }
            public bool TrackPrimaryTarget => Hit.Targets == "nearest";
            public void MarkTarget() { state.Marked = state.TargetHit = true; state.Signal("mark"); }
            public void MarkApproachAccepted() => state.Signal("approach-mark");
            public bool HasResponseMovement => state.Definition.Movement != null;
            public bool PrimaryPush => state.Definition.Movement.PrimaryPush;
            public bool MarkedTargetExists => state.Marked;
            public bool TargetIsBoss => false;
            public bool TargetIsMarked => state.Marked;
            public int TargetActorId => 7;
            public bool PushWithCaster => Hit.PushWithCaster;
            public float EndForcedDashAtBoss() => state.Dash.EndAtPrimaryBoss(state.Elapsed, state.StepDuration);
            public void SetMotionEnd(float end) => state.Lifecycle.SetMotionEnd(end);
            public bool Cancelled => state.Lifecycle.Cancelled;
            public bool MovementExists => state.Definition.Movement != null;
            public bool MarkedTargetAlive => state.Marked && !state.Target.Read().Dead;
            public float EndDamage => state.Definition.Movement.EndDamage;
            public void ReceiveMarked(float damage) { state.Target.ApplyDamage(damage, state.Now, false); state.Signal("end-receive"); }
        }
        readonly struct LifecycleHost : ICastLifecycleHost
        {
            readonly State state;
            public LifecycleHost(State state) { this.state = state; }
            public float Now => state.Now;
            public float Elapsed => state.Elapsed;
            public bool ChargeHeld => false;
            public bool Approaching => false;
            public void AdvanceApproach(float step) => throw new InvalidOperationException("No approach in the cast tick fixture.");
            public void Activate() => throw new InvalidOperationException("Cast tick does not activate actions.");
            public void PrepareMissile(float step) => throw new InvalidOperationException("Cast tick does not prepare missiles.");
            public void ReleaseDueBombs() => throw new InvalidOperationException("Cast tick does not release bombs.");
            public void PrepareTeleport(float step) => throw new InvalidOperationException("Cast tick does not prepare teleport movement.");
            public void ReleaseResources() { state.Charge?.Cancel(); state.Signal("release"); }
            public bool CancelMeleeAction() => state.Melee != null && state.Melee.Cancel();
            public void Failed() => throw new InvalidOperationException("Cast tick does not fail activation.");
            public bool MissileEnded => false;
            public void Complete() { state.Completions++; state.Signal("complete"); }
            public float SmokeEnd => state.Smoke.End;
            public float RetainSkillWaveUntil(float last, float elapsed, float step) => state.Wave.RetainUntil(last, elapsed, step);
            public float RecastRemaining => 0;
            public float RecastDeadline => 0;
            public void Dispose() => state.Dispose();
        }
        readonly struct WaveHost : ISkillWaveExecutionHost
        {
            readonly State state;
            public WaveHost(State state) { this.state = state; }
            public int Direction => 1;
            public bool Blocked(float x, float y) => false;
            public bool Occluded(float x, float y) => false;
            public void CreateVisual(float x, float y) => state.Signal("wave-create");
            public float Travel(float x, float y, int direction, float requested) => requested;
            public void ApplyBox(float previousX, float previousY, float x, float y) { state.Signal("wave-box"); state.ApplyHit(0); }
            public void UpdateVisual(float x, float y) => state.Signal("wave-visual");
            public void DestroyVisual() => state.Signal("wave-destroy");
        }
        sealed class MeleeHost : IMeleeActionHost
        {
            readonly State state;
            public MeleeHost(State state) { this.state = state; }
            public bool TryActivate() { state.Signal("melee-activate"); return true; }
            public void Sweep()
            {
                if (state.Melee.TryClaimTarget(7)) state.Target.ApplyDamage(8, state.Now, false);
                state.Signal("melee-sweep");
            }
            public void Complete() { state.Completions++; state.Signal("melee-complete"); }
        }
        sealed class ChargedHost : IChargedMeleeExecutionHost
        {
            readonly State state;
            public ChargedHost(State state) { this.state = state; }
            public float Now => state.Now;
            public bool SelectTarget() => true;
            public bool CommitResources() => true;
            public void Activated() => state.Signal("charged-activate");
            public void ReleaseResources() => state.Signal("charged-release");
            public void Failed() => state.Signal("charged-fail");
            public void Sweep()
            {
                if (state.Charged.TryClaimTarget(7)) state.Target.ApplyDamage(state.Charge.Release.Value.Damage, state.Now, false);
                state.Signal("charged-sweep");
            }
            public void Complete() { state.Completions++; state.Signal("charged-complete"); }
            public void Dispose() => state.Dispose();
        }
        static HitData Hit(float time = 0, float active = .125f, float damage = 8, string anchor = "actor")
            => new HitData { time = time, active = active, damage = damage, anchor = anchor, targets = "all" };
        static SkillData Input(string effect = null, float end = 2, float duration = 1, params HitData[] hits)
            => new SkillData { id = "cast_tick_fixture", activation = 0, end = end, vertical = "normal", hits = hits,
                effect = effect == null ? null : new EffectData { type = effect, duration = duration, speed = 4, distance = 10 } };
        static State Primary(string effect = null, float end = .125f, params HitData[] hits)
        {
            var input = Input(effect, end, 1, hits);
            input.movement = new MoveData { type = "dash", start = 0, duration = .125f, primaryPush = true, endDamage = 20 };
            var state = new State(input); state.FinishMovement(); return state;
        }

        [Test] public void ContractAndFlowKeepNativeSceneAndPresentationDependenciesOutsidePureAssemblies()
        {
            foreach (var type in new[] { typeof(ICastTickHost), typeof(CastTickExecution) })
                foreach (var reference in type.Assembly.GetReferencedAssemblies())
                    Assert.That(reference.Name.StartsWith("UnityEngine") || reference.Name.StartsWith("UnityEditor") || reference.Name == "Deviant.Combat" ||
                        reference.Name.StartsWith("Deviant.World") || reference.Name.StartsWith("Deviant.Presentation"), Is.False, reference.Name);
        }
        [TestCase(false, false)] [TestCase(true, true)]
        public void MissingOrDeadOwnerSetsDtAndDisposesWithoutClearingExistingDebugOrAdvancingOwners(bool exists, bool dead)
        {
            var state = new State(Input("smoke", hits: new[] { Hit() })) { Exists = exists, Dead = dead };
            state.Status.WriteSmokeUntil(3); state.Tick(.03125f);
            Assert.That(state.StepDuration, Is.EqualTo(.03125f)); Assert.That(state.Disposals, Is.EqualTo(1));
            Assert.That(state.Events, Is.EqualTo(new[] { "dt", "dispose" })); Assert.That(state.DebugBoxes, Is.EqualTo(3));
            Assert.That(state.OwnerReads, Is.EqualTo(1)); Assert.That(state.DeadReads, Is.EqualTo(exists ? 1 : 0));
            Assert.That(state.Target.Read().ReceivedHits, Is.Zero); Assert.That(state.Status.Read().SmokeUntil, Is.EqualTo(3));
            Assert.That(state.Commits, Is.Empty); Assert.That(state.Lifecycle.MotionDone, Is.False);
        }
        [Test] public void ChargedOwnerWinsOverMeleeAndGenericFlowAndRetainsItsOwnHitCompletionAndDisposal()
        {
            var state = new State(Input("smoke", hits: new[] { Hit() })) { Now = .25f };
            state.BindMelee(.25f); state.BindCharged(.25f); state.Events.Clear(); state.Tick(.25f);
            Assert.That(state.Events, Is.EqualTo(new[] { "dt", "clear-debug", "charged", "charged-sweep", "charged-complete", "dispose" }));
            Assert.That(state.Target.Read().Health, Is.EqualTo(75)); Assert.That(state.Charged.MotionDone, Is.True);
            Assert.That(state.Melee.State, Is.EqualTo(MeleeActionState.Active)); Assert.That(state.Lifecycle.MotionDone, Is.False);
            Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.EqualTo(1));
            Assert.That(state.DebugBoxes, Is.Zero); Assert.That(state.AreaCaptures, Is.Zero); Assert.That(state.Commits, Is.Empty);
        }
        [TestCase(.125f, false)] [TestCase(.25f, true)]
        public void MeleeRunnerOwnsFinalSweepAndTerminalStatusAndAlwaysSkipsTheGenericTail(float step, bool terminal)
        {
            var state = new State(Input("smoke", hits: new[] { Hit() })); state.BindMelee(step); state.Events.Clear(); state.Tick(step);
            Assert.That(state.Target.Read().Health, Is.EqualTo(92)); Assert.That(state.Melee.IsTerminal, Is.EqualTo(terminal));
            Assert.That(state.Completions, Is.EqualTo(terminal ? 1 : 0)); Assert.That(state.Disposals, Is.EqualTo(terminal ? 1 : 0));
            Assert.That(state.Events.IndexOf("melee-sweep"), Is.GreaterThan(state.Events.IndexOf("clear-debug")));
            if (terminal) Assert.That(state.Events, Is.EqualTo(new[] { "dt", "clear-debug", "melee", "melee-sweep", "melee-complete", "dispose" }));
            Assert.That(state.Commits, Is.Empty); Assert.That(state.AreaCaptures, Is.Zero); Assert.That(state.Lifecycle.MotionDone, Is.False);
        }
        [Test] public void AnAlreadyCancelledMeleeRunnerIsDisposedWithoutPreparingOrSweepingAgain()
        {
            var state = new State(Input(hits: new[] { Hit() })); state.BindMelee(.125f); state.Melee.Cancel(); state.Events.Clear(); state.Tick();
            Assert.That(state.Events, Is.EqualTo(new[] { "dt", "clear-debug", "melee", "dispose" }));
            Assert.That(state.Target.Read().ReceivedHits, Is.Zero); Assert.That(state.Completions, Is.Zero); Assert.That(state.Commits, Is.Empty);
        }
        [Test] public void UnactivatedCastSkipsEffectsButStillEndsPrimaryCommitsAndCompletesThroughItsExistingLifecycle()
        {
            var input = Input("smoke", .125f, 1, Hit());
            input.movement = new MoveData { type = "dash", duration = .125f, primaryPush = true, endDamage = 20 };
            var state = new State(input, false) { Marked = true };
            state.Dash.AfterMovement(true, 0, .125f, false); state.Tick();
            Assert.That(state.Events, Is.EqualTo(new[] { "dt", "clear-debug", "end-primary", "end-receive", "commit", "lifecycle", "complete", "dispose" }));
            Assert.That(state.Target.Read().Health, Is.EqualTo(80)); Assert.That(state.Lifecycle.Activated, Is.False);
            Assert.That(state.Lifecycle.MotionDone, Is.True); Assert.That(state.Commits, Is.EqualTo(new[] { .125f })); Assert.That(state.AreaCaptures, Is.Zero);
        }
        [Test] public void ActualWaveMovesAndHitsBeforeTheDirectTrackAndAnEnteredPassIsNotReadmittedAfterItsCallback()
        {
            var state = new State(Input("projectile", hits: new[] { Hit(anchor: "projectile"), Hit(damage: 12, anchor: "saved_primary_world_pose") }));
            state.Events.Clear(); state.Hooks["wave-box"] = s => { s.Cancel(); s.Exists = false; s.Dead = true; };
            state.Tick();
            Assert.That(state.Wave.Read().X, Is.EqualTo(.5f)); Assert.That(state.Wave.Read().Travel, Is.EqualTo(.5f));
            Assert.That(state.Target.Read().Health, Is.EqualTo(80)); Assert.That(state.Target.Read().ReceivedHits, Is.EqualTo(2));
            Assert.That(state.Events.IndexOf("receive:0"), Is.LessThan(state.Events.IndexOf("hits")));
            Assert.That(state.Events.IndexOf("receive:1"), Is.GreaterThan(state.Events.IndexOf("wave-visual")));
            Assert.That(state.Dispatches, Is.EqualTo(new[] { 1 })); Assert.That(state.Lifecycle.Cancelled, Is.True);
            Assert.That(state.OwnerReads, Is.EqualTo(1)); Assert.That(state.DeadReads, Is.EqualTo(1)); Assert.That(state.Commits, Is.EqualTo(new[] { .125f }));
        }
        [Test] public void HitCallbacksUpdateTheCurrentSmokeTimePoseOverlapAndClampOnlyItsDeadline()
        {
            var state = new State(Input("smoke", duration: .75f, hits: new[] { Hit() })) { Started = .25f, Now = .5f, Overlaps = false, SavedPose = -2 };
            state.Status.WriteStealthUntil(9);
            state.Hooks["receive:0"] = s => { s.Now = .9375f; s.SavedPose = 7; s.Overlaps = true; };
            state.Tick();
            Assert.That(state.ActiveElapsed, Is.EqualTo(.6875f)); Assert.That(state.RefreshElapsed, Is.EqualTo(.6875f));
            Assert.That(state.RefreshNow, Is.EqualTo(.9375f)); Assert.That(state.RefreshStep, Is.EqualTo(.125f)); Assert.That(state.CapturedPose, Is.EqualTo(7));
            Assert.That(state.Status.Read().SmokeUntil, Is.EqualTo(1)); Assert.That(state.Status.Read().StealthUntil, Is.EqualTo(9));
            Assert.That(state.DebugBoxes, Is.EqualTo(1)); Assert.That(state.OverlapReads, Is.EqualTo(1));
            Assert.That(state.Events.IndexOf("smoke-active"), Is.GreaterThan(state.Events.IndexOf("receive:0")));
            Assert.That(state.Commits, Is.EqualTo(new[] { .8125f }));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExitOrExactExpiryClearsOnlySmokeAndExpiredSmokeStillCapturesAreaWithoutOverlapOrDebug(bool expired)
        {
            var state = new State(Input("smoke", duration: .5f, hits: new[] { Hit() }));
            state.Status.WriteSmokeUntil(9); state.Status.WriteStealthUntil(11);
            state.Hooks["receive:0"] = s => { if (expired) s.Now = .5f; else s.Overlaps = false; };
            state.Tick();
            Assert.That(state.Status.Read().SmokeUntil, Is.Zero); Assert.That(state.Status.Read().StealthUntil, Is.EqualTo(11));
            Assert.That(state.AreaCaptures, Is.EqualTo(1)); Assert.That(state.DebugBoxes, Is.EqualTo(expired ? 0 : 1));
            Assert.That(state.OverlapReads, Is.EqualTo(expired ? 0 : 1)); Assert.That(state.Events, Does.Contain("smoke-refresh"));
        }
        [Test] public void SpendingSmokeInAHitCallbackLeavesItsActiveDebugAreaButCannotRestoreConcealment()
        {
            var state = new State(Input("smoke", hits: new[] { Hit() })); state.Status.WriteSmokeUntil(9);
            state.Hooks["receive:0"] = s => s.Smoke.MarkSpent(); state.Tick();
            Assert.That(state.Smoke.Spent, Is.True); Assert.That(state.DebugBoxes, Is.EqualTo(1)); Assert.That(state.Status.Read().SmokeUntil, Is.Zero);
        }
        [Test] public void SmokeRefreshReadsElapsedAgainAfterAreaAndOverlapReadsNowBeforeInvokingTheEffect()
        {
            var state = new State(Input("smoke", duration: 2)) { Now = .125f };
            state.Hooks["smoke-area"] = s => s.Now = .25f;
            state.Hooks["smoke-overlap"] = s => s.Now = .5f;
            state.Tick();
            Assert.That(state.ActiveElapsed, Is.EqualTo(.125f)); Assert.That(state.RefreshElapsed, Is.EqualTo(.25f));
            Assert.That(state.RefreshNow, Is.EqualTo(.5f)); Assert.That(state.Status.Read().SmokeUntil, Is.EqualTo(.6875f));
            Assert.That(state.Commits, Is.EqualTo(new[] { .625f }));
        }
        [Test] public void CapturedImmutableEffectIsNotReplacedByTheDefinitionSeenAfterHitCallbacks()
        {
            var state = new State(Input("smoke", hits: new[] { Hit() })); var captured = state.Definition.Effect;
            state.Hooks["receive:0"] = s => s.Definition = new SkillExecutionDefinition(Input(hits: new[] { Hit() })); state.Tick();
            Assert.That(state.Definition.Effect, Is.Null); Assert.That(captured.Type, Is.EqualTo("smoke"));
            Assert.That(state.AreaCaptures, Is.EqualTo(1)); Assert.That(state.Status.Read().SmokeUntil, Is.EqualTo(.1875f));
        }
        [Test] public void TheFinalHitPrecedesPrimaryEndDamageAndItsCurrentClockIsCommittedBeforeCompletionReentry()
        {
            var state = Primary(hits: new[] { Hit(active: 0, anchor: "saved_primary_world_pose") });
            state.Hooks["end-receive"] = s => s.Now = .25f;
            state.Hooks["complete"] = s =>
            {
                Assert.That(s.Target.Read().Health, Is.EqualTo(72)); Assert.That(s.Lifecycle.MotionDone, Is.True);
                Assert.That(s.Commits, Is.EqualTo(new[] { .375f })); s.Hooks.Remove("complete"); s.Tick();
            };
            state.Tick();
            Assert.That(state.Events.IndexOf("receive:0"), Is.LessThan(state.Events.IndexOf("end-receive")));
            Assert.That(state.Events.IndexOf("commit"), Is.LessThan(state.Events.IndexOf("complete")));
            Assert.That(state.Target.Read().ReceivedHits, Is.EqualTo(2)); Assert.That(state.Dispatches, Is.EqualTo(new[] { 0 }));
            Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.EqualTo(2));
            Assert.That(state.Commits, Is.EqualTo(new[] { .375f, .375f }));
        }
        [Test] public void HitReentryUsesTheSameHitLedgerAndOwnersWithoutASecondGuardOrRestoringTheOuterDt()
        {
            var state = new State(Input("smoke", .125f, 1, Hit(), Hit(damage: 12)));
            state.Hooks["receive:0"] = s => { s.Hooks.Remove("receive:0"); s.Tick(.03125f); };
            state.Tick();
            Assert.That(state.Target.Read().Health, Is.EqualTo(80)); Assert.That(state.Target.Read().ReceivedHits, Is.EqualTo(2));
            Assert.That(state.Dispatches, Is.EqualTo(new[] { 0, 0, 1, 1 })); Assert.That(state.Commits, Is.EqualTo(new[] { .03125f, .125f }));
            Assert.That(state.StepDuration, Is.EqualTo(.03125f)); Assert.That(state.Status.Read().SmokeUntil, Is.EqualTo(.1875f));
            Assert.That(state.Completions, Is.EqualTo(1)); Assert.That(state.Disposals, Is.Zero);
        }
        [TestCase("receive:0")] [TestCase("smoke-area")] [TestCase("smoke-refresh")] [TestCase("end-receive")] [TestCase("complete")]
        public void CallbackExceptionsKeepTheirExactPrefixAndCommitOnlyAfterTheWholePrecompletionPass(string phase)
        {
            var state = Primary("smoke", hits: new[] { Hit(active: 0, anchor: "saved_primary_world_pose") });
            state.Status.WriteSmokeUntil(3); var failure = new InvalidOperationException(phase);
            state.Hooks[phase] = s => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.Tick()), Is.SameAs(failure));
            Assert.That(state.Events[state.Events.Count - 1], Is.EqualTo(phase)); Assert.That(state.Disposals, Is.Zero);
            Assert.That(state.Target.Read().Health, Is.EqualTo(phase == "end-receive" || phase == "complete" ? 72 : 92));
            Assert.That(state.Status.Read().SmokeUntil, Is.EqualTo(phase == "end-receive" || phase == "complete" ? .1875f : 3));
            Assert.That(state.Commits.Count, Is.EqualTo(phase == "complete" ? 1 : 0)); Assert.That(state.Lifecycle.MotionDone, Is.EqualTo(phase == "complete"));
            int dispatches = state.Dispatches.Count; state.Hooks.Remove(phase); state.Tick();
            Assert.That(state.Dispatches.Count, Is.EqualTo(dispatches + (phase == "complete" ? 0 : 1)), "Only a completed evaluation consumes the zero-width independent hit window.");
            Assert.That(state.Target.Read().Health, Is.EqualTo(phase == "receive:0" ? 92 : 72));
            Assert.That(state.Target.Read().ReceivedHits, Is.EqualTo(phase == "receive:0" ? 1 : 2));
            Assert.That(state.Marked, Is.EqualTo(phase != "receive:0"), "A throwing Receive spends its claim but does not reach primary selection.");
        }
        [Test] public void WaveReceiveFailureRetainsMovedWaveAndDamageWithoutRunningDirectHitsOrCommitting()
        {
            var state = new State(Input("projectile", hits: new[] { Hit(anchor: "projectile"), Hit(damage: 12) }));
            state.Events.Clear(); var failure = new InvalidOperationException("wave-receive"); state.Hooks["receive:0"] = s => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.Tick()), Is.SameAs(failure));
            Assert.That(state.Events, Is.EqualTo(new[] { "dt", "clear-debug", "wave", "wave-box", "receive:0" }));
            Assert.That(state.Wave.Read().Travel, Is.EqualTo(.5f)); Assert.That(state.Target.Read().Health, Is.EqualTo(92));
            Assert.That(state.Dispatches, Is.Empty); Assert.That(state.Commits, Is.Empty); Assert.That(state.Lifecycle.MotionDone, Is.False);
            state.Hooks.Clear(); state.Tick();
            Assert.That(state.Wave.Read().Travel, Is.EqualTo(1)); Assert.That(state.Target.Read().Health, Is.EqualTo(80));
        }
        [TestCase(false)] [TestCase(true)]
        public void SpecializedSweepExceptionsPropagateWithoutDisposalOrGenericEvaluation(bool charged)
        {
            var state = new State(Input("smoke", hits: new[] { Hit() })) { Now = charged ? .25f : 0 };
            if (charged) state.BindCharged(.25f); else state.BindMelee(.25f);
            string phase = charged ? "charged-sweep" : "melee-sweep"; var failure = new InvalidOperationException(phase);
            state.Events.Clear(); state.Hooks[phase] = s => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.Tick(.25f)), Is.SameAs(failure));
            Assert.That(state.DebugBoxes, Is.Zero); Assert.That(state.Target.Read().ReceivedHits, Is.EqualTo(1));
            Assert.That(state.Events, Is.EqualTo(new[] { "dt", "clear-debug", charged ? "charged" : "melee", phase }));
            Assert.That(state.Disposals, Is.Zero); Assert.That(state.Commits, Is.Empty); Assert.That(state.AreaCaptures, Is.Zero);
            if (charged) Assert.That(state.Charged.MotionDone, Is.False); else Assert.That(state.Melee.State, Is.EqualTo(MeleeActionState.Failed));
        }
    }
}
