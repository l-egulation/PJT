using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CombatSimulationTests
    {
        sealed class World : ICombatTickWorld
        {
            public CombatSimulation simulation;
            public readonly List<CombatTickPhase> phases = new List<CombatTickPhase>();
            public readonly List<float> times = new List<float>();
            public readonly List<CombatInput> inputs = new List<CombatInput>();
            public Action<CombatTickPhase> onPhase;
            public Action onReset;
            public int resets;
            public SimulationResetMode resetMode;
            public void ApplyInput(CombatInput input) { inputs.Add(input); }
            public void Execute(CombatTickPhase phase, float dt)
            { phases.Add(phase); times.Add(simulation.Clock.Now); onPhase?.Invoke(phase); }
            public void ResetState(SimulationResetMode mode) { resetMode = mode; resets++; onReset?.Invoke(); }
        }
        static World Create()
        { var world = new World(); world.simulation = new CombatSimulation(world); return world; }

        [Test] public void ExecutionAssemblyHasNoUnityDependencyAndPreservesLegacyPhaseAndClockOrder()
        {
            Assert.That(typeof(CombatSimulation).Assembly.GetName().Name, Is.EqualTo("Deviant.Combat.Execution"));
            Assert.That(typeof(CombatSimulation).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name == "Deviant.Combat"), Is.False);
            var world = Create(); var simulation = world.simulation;
            simulation.Step(.02f, new CombatInput(1, CombatButtons.Basic));
            Assert.That(world.phases, Is.EqualTo(new[] {
                CombatTickPhase.Player, CombatTickPhase.Actions, CombatTickPhase.ActionPush,
                CombatTickPhase.Enemies, CombatTickPhase.EnemyProjectiles, CombatTickPhase.Bombs,
                CombatTickPhase.PlayerProjectiles, CombatTickPhase.Lasers, CombatTickPhase.Bursts,
                CombatTickPhase.Decoys, CombatTickPhase.ExpiredFeedback
            }));
            Assert.That(world.times.Take(10), Is.All.EqualTo(0));
            Assert.That(world.times[10], Is.EqualTo(.02f));
            Assert.That(world.inputs.Count, Is.EqualTo(1));
            Assert.That(world.inputs[0].Has(CombatButtons.Basic), Is.True);
            Assert.That(simulation.Clock.TickCount, Is.EqualTo(1));
            simulation.Step(.02f);
            Assert.That(world.inputs.Count, Is.EqualTo(1), "Automatic ticks retain externally sampled input.");
            float legacy = .04f;
            for (int i = 0; i < 1000; i++) { simulation.Step(.02f); legacy += .02f; }
            Assert.That(simulation.Clock.Now, Is.EqualTo(legacy), "Do not silently replace legacy float accumulation.");
        }

        [TestCase(0f), TestCase(-1f), TestCase(float.NaN), TestCase(float.PositiveInfinity)]
        public void InvalidTimeDoesNotSubmitInputOrTouchWorld(float step)
        {
            var world = Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => world.simulation.Step(step, new CombatInput(1, CombatButtons.Dash)));
            Assert.That(world.inputs, Is.Empty); Assert.That(world.phases, Is.Empty);
            Assert.That(world.simulation.Clock.Now, Is.Zero); Assert.That(world.simulation.Clock.TickCount, Is.Zero);
            Assert.That(world.simulation.IsFaulted, Is.False);
        }

        [Test] public void OverflowAndNonAdvancingTimeAreRejectedBeforeAnyPhase()
        {
            var world = Create(); world.simulation.Step(float.MaxValue);
            int count = world.phases.Count;
            Assert.Throws<ArgumentOutOfRangeException>(() => world.simulation.Step(float.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.simulation.Step(1));
            Assert.That(world.phases.Count, Is.EqualTo(count));
        }

        [Test] public void ResetModesPreserveOrRestartTheSameClockAndResetWorldOnce()
        {
            var world = Create(); var simulation = world.simulation; var clock = simulation.Clock;
            simulation.Step(.02f);
            world.onReset = () => Assert.That(clock.Now, Is.EqualTo(.02f));
            simulation.Reset(SimulationResetMode.KeepTime);
            Assert.That(clock.TickCount, Is.EqualTo(1)); Assert.That(world.inputs, Is.Empty);
            Assert.That(world.resetMode, Is.EqualTo(SimulationResetMode.KeepTime));
            world.onReset = () => Assert.That(clock.Now, Is.Zero);
            simulation.Reset(SimulationResetMode.NewEpoch);
            Assert.That(clock.TickCount, Is.Zero); Assert.That(simulation.Clock, Is.SameAs(clock));
            Assert.That(world.inputs, Is.Empty, "Reset is a lifecycle boundary, not an invented input sample.");
            Assert.That(world.resetMode, Is.EqualTo(SimulationResetMode.NewEpoch));
            Assert.That(world.resets, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Reset((SimulationResetMode)123));
            Assert.That(world.resets, Is.EqualTo(2));
        }

        [Test] public void CallbacksCannotReenterStepOrReset()
        {
            var world = Create(); var simulation = world.simulation;
            Action check = () => {
                Assert.Throws<InvalidOperationException>(() => simulation.Step(.02f));
                Assert.Throws<InvalidOperationException>(() => simulation.Reset(SimulationResetMode.NewEpoch));
            };
            world.onPhase = _ => check(); world.onReset = check;
            simulation.Step(.02f); simulation.Reset(SimulationResetMode.KeepTime);
            Assert.That(world.phases.Count, Is.EqualTo(11)); Assert.That(world.resets, Is.EqualTo(1));
            Assert.That(simulation.IsFaulted, Is.False); Assert.That(simulation.Clock.TickCount, Is.EqualTo(1));
        }

        [TestCase(CombatTickPhase.Actions, 0f), TestCase(CombatTickPhase.ExpiredFeedback, .02f)]
        public void FailedPhaseCannotReplayPartialWorkAndRequiresSuccessfulReset(CombatTickPhase phase, float time)
        {
            var world = Create(); var simulation = world.simulation;
            world.onPhase = current => { if (current == phase) throw new ApplicationException("phase failure"); };
            Assert.Throws<ApplicationException>(() => simulation.Step(.02f));
            Assert.That(simulation.IsFaulted, Is.True); Assert.That(simulation.Clock.Now, Is.EqualTo(time));
            int count = world.phases.Count;
            Assert.Throws<InvalidOperationException>(() => simulation.Step(.02f, new CombatInput(1)));
            Assert.That(world.phases.Count, Is.EqualTo(count)); Assert.That(world.inputs, Is.Empty);
            world.onReset = () => throw new ApplicationException("reset failure");
            Assert.Throws<ApplicationException>(() => simulation.Reset(SimulationResetMode.NewEpoch));
            Assert.That(simulation.IsFaulted, Is.True);
            world.onReset = null; world.onPhase = null;
            simulation.Reset(SimulationResetMode.NewEpoch); simulation.Step(.02f);
            Assert.That(simulation.IsFaulted, Is.False); Assert.That(simulation.Clock.Now, Is.EqualTo(.02f));
            Assert.That(simulation.Clock.TickCount, Is.EqualTo(1));
        }
        [TestCase(CombatTickPhase.Actions, 0f)] [TestCase(CombatTickPhase.ExpiredFeedback, .02f)]
        public void BoundIngressPublishesFaultWithoutReplacingTheOriginalPhaseException(CombatTickPhase failAt, float endedNow)
        {
            var world = Create(); var simulation = world.simulation; var ingress = new CombatInputIngress(12);
            simulation.BindInputIngress(ingress);
            using (var capture = ingress.BeginCapture(4))
            {
                var sample = new CombatInputSample(ingress.Session, ingress.NextSequence, new CombatInput(0, CombatButtons.Basic),
                    CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, 0);
                ingress.Submit(sample, 0, 0);
                var failure = new ApplicationException("phase failure identity");
                world.onPhase = phase =>
                {
                    if (phase == CombatTickPhase.Player) { ingress.ReadForArbitration(); ingress.Consume(0, 0); Assert.That(capture.TryRead(out _), Is.False); }
                    if (phase == failAt) throw failure;
                };
                Assert.That(Assert.Throws<ApplicationException>(() => simulation.Step(.02f)), Is.SameAs(failure));
                Assert.That(capture.TryRead(out var batch), Is.True);
                Assert.That(batch.TickOutcome, Is.EqualTo(CombatInputTickOutcome.Faulted)); Assert.That(batch.ObservedThroughNow, Is.EqualTo(endedNow));
                Assert.That(batch.Receipts[0].Disposition, Is.EqualTo(CombatInputDisposition.Consumed));
                Assert.Throws<InvalidOperationException>(() => simulation.Step(.02f)); Assert.That(capture.TryRead(out _), Is.False);
            }
        }
        [Test] public void InputIngressBindingIsSingleOwnerAndCannotChangeDuringWorldExecution()
        {
            var world = Create(); var first = new CombatInputIngress(12);
            world.simulation.BindInputIngress(first);
            Assert.Throws<InvalidOperationException>(() => world.simulation.BindInputIngress(new CombatInputIngress(13)));
            world.onPhase = _ => Assert.Throws<InvalidOperationException>(() => world.simulation.BindInputIngress(first));
            world.simulation.Step(.02f);
        }
    }
}
