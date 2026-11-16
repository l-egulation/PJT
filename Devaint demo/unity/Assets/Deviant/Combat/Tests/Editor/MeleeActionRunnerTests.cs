using System;
using System.Collections.Generic;
using System.Globalization;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class MeleeActionRunnerTests
    {
        sealed class Host : IMeleeActionHost
        {
            public readonly List<string> Events = new List<string>();
            public Action OnActivate, OnSweep, OnComplete;
            public bool RejectActivation;
            public bool TryActivate() { Events.Add("activate"); OnActivate?.Invoke(); return !RejectActivation; }
            public void Sweep() { Events.Add("sweep"); OnSweep?.Invoke(); }
            public void Complete() { Events.Add("complete"); OnComplete?.Invoke(); }
        }
        static MeleeActionDefinition Definition(float speed = 1, float impact = .2f, float end = .4f)
        {
            Assert.That(BasicMeleeDefinition.TryCreate("base_sword_basic_0", new BasicData { damage = 10, impact = impact, active = .08f, duration = end, box = new BoxData { width = 2, height = 2, x = 1, y = 1 } }, out var source, out var error), Is.True, error);
            return new MeleeActionDefinition(source, speed, .65f, Math.Max(0, impact - .04f) / speed, .08f / speed);
        }
        static MeleeActionRunner Runner(Host host, MeleeActionDefinition definition = null, float start = 0) => new MeleeActionRunner(Guid.NewGuid(), definition ?? Definition(), start, host);
        static void Tick(MeleeActionRunner runner, float now, float step = .02f) { runner.Prepare(now, step); runner.Step(now, step); }
        [Test] public void DefinitionCopiesAndVersionsPreserveResolvedSpeedTimingAndGeometry()
        {
            var a = Definition(); var hit = a.CreateHit(); hit.time = 99; hit.box.width = 99;
            Assert.That(a.CreateHit().time, Is.EqualTo(.2f)); Assert.That(a.CreateHit().box.width, Is.EqualTo(2));
            var b = Definition(2); Assert.That(b.SourceVersion, Is.EqualTo(a.SourceVersion)); Assert.That(b.ContentHash, Is.Not.EqualTo(a.ContentHash));
            Assert.That(b.Impact, Is.EqualTo(.1f)); Assert.That(b.Active, Is.EqualTo(.04f)); Assert.That(b.End, Is.EqualTo(.2f));
            var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Assert.That(Definition().ContentHash, Is.EqualTo(a.ContentHash)); }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [Test] public void ActivationPrecedesMovementAndFinalSweepPrecedesSingleCompletion()
        {
            var host = new Host(); var runner = Runner(host); runner.Prepare(.18f, .02f);
            Assert.That(host.Events, Is.EqualTo(new[] { "activate" }));
            runner.HorizontalVelocity(.18f, .02f, 1, true, 5); runner.Step(.18f, .02f);
            Assert.That(host.Events, Is.EqualTo(new[] { "activate", "sweep" }));
            Tick(runner, .2f); Tick(runner, .3f); Tick(runner, .4f);
            Assert.That(runner.State, Is.EqualTo(MeleeActionState.Completed)); Assert.That(host.Events.FindAll(e => e == "complete").Count, Is.EqualTo(1));
            Tick(runner, .42f); Assert.That(host.Events.FindAll(e => e == "activate").Count, Is.EqualTo(1)); Assert.That(host.Events.FindAll(e => e == "complete").Count, Is.EqualTo(1));
        }
        [Test] public void RejectedActivationDoesNotSweepOrCompleteAndDoesNotThrowASimulationFault()
        {
            var host = new Host { RejectActivation = true }; var runner = Runner(host); runner.Prepare(.18f, .02f);
            Assert.That(runner.State, Is.EqualTo(MeleeActionState.Failed)); Assert.That(runner.Error, Does.Contain("activation"));
            Assert.DoesNotThrow(() => runner.Step(.18f, .02f)); Assert.That(host.Events, Is.EqualTo(new[] { "activate" }));
            Assert.That(runner.HorizontalVelocity(.18f, .02f, 0, true, 5), Is.Zero); Assert.That(runner.TryClaimTarget(1), Is.False);
        }
        [Test] public void SubTickHitAndWholeActionStillSweepBeforeCompletion()
        {
            var host = new Host(); var runner = Runner(host, Definition(500)); Tick(runner, 0);
            Assert.That(host.Events, Is.EqualTo(new[] { "activate", "sweep", "complete" })); Assert.That(runner.State, Is.EqualTo(MeleeActionState.Completed));
            host = new Host(); runner = Runner(host, Definition(1, 0, .08f)); Tick(runner, 0, .08f);
            Assert.That(host.Events, Is.EqualTo(new[] { "activate", "sweep", "complete" }), "Time-zero contact is not lost.");
        }
        [Test] public void GroundAdvanceConsumesNeutralAndAirIntervalsAndKeepsFractionalStart()
        {
            var host = new Host(); var runner = Runner(host); float distance = 0;
            for (int i = 0; i < 20; i++) { float now = i * .02f; runner.Prepare(now, .02f); distance += runner.HorizontalVelocity(now, .02f, 1, true, 5) * .02f; runner.Step(now, .02f); }
            Assert.That(distance, Is.EqualTo(.65f).Within(.000001f));
            runner = Runner(new Host()); runner.Prepare(.18f, .02f); Assert.That(runner.HorizontalVelocity(.18f, .02f, 0, true, 5), Is.Zero); runner.Step(.18f, .02f);
            runner.Prepare(.2f, .02f); Assert.That(runner.HorizontalVelocity(.2f, .02f, 1, false, 7), Is.EqualTo(7)); runner.Step(.2f, .02f);
            runner.Prepare(.22f, .02f); float remaining = runner.HorizontalVelocity(.22f, .02f, 1, true, 5) * .02f; runner.Step(.22f, .02f);
            Assert.That(remaining, Is.EqualTo(.1625f).Within(.000001f), "Landing/input cannot recover consumed advance intervals.");
            runner = Runner(new Host(), Definition(1, 0, .08f), .015f); runner.Prepare(0, .02f);
            Assert.That(runner.Age(0), Is.Zero); Assert.That(runner.HorizontalVelocity(0, .02f, 1, true, 5) * .02f, Is.EqualTo(.65f / .08f * .005f).Within(.000001f));
        }
        [Test] public void TargetClaimsBelongToOneExecutionAndStopAfterCancellation()
        {
            var host = new Host(); var runner = Runner(host); int claims = 0;
            host.OnSweep = () => { if (runner.TryClaimTarget(1)) claims++; if (runner.TryClaimTarget(1)) claims++; if (runner.TryClaimTarget(2)) claims++; };
            Tick(runner, .18f); Tick(runner, .2f); Assert.That(claims, Is.EqualTo(2));
            Assert.That(runner.Cancel(), Is.True); Assert.That(runner.Cancel(), Is.False); Assert.That(runner.TryClaimTarget(3), Is.False); Tick(runner, .3f);
            Assert.That(host.Events.Contains("complete"), Is.False);
            var anotherHost = new Host(); var another = Runner(anotherHost); anotherHost.OnSweep = () => Assert.That(another.TryClaimTarget(1), Is.True); Tick(another, .18f);
        }
        [Test] public void CancellationInsideSweepPreventsRemainingClaimsAndCompletion()
        {
            var host = new Host(); var runner = Runner(host, Definition(500));
            host.OnSweep = () => { Assert.That(runner.TryClaimTarget(1), Is.True); runner.Cancel(); Assert.That(runner.TryClaimTarget(2), Is.False); };
            Tick(runner, 0); Assert.That(runner.State, Is.EqualTo(MeleeActionState.Cancelled)); Assert.That(host.Events, Is.EqualTo(new[] { "activate", "sweep" }));
        }
        [Test] public void InvalidAndMismatchedTicksAreRejectedBeforeCallbacksOrProgress()
        {
            var host = new Host(); var runner = Runner(host);
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Prepare(0, float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => runner.Prepare(0, 0));
            Assert.Throws<InvalidOperationException>(() => runner.Step(0, .02f)); Assert.That(host.Events, Is.Empty);
            runner.Prepare(.1f, .02f); Assert.Throws<InvalidOperationException>(() => runner.Step(.1f, .03f)); runner.Step(.1f, .02f);
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Prepare(.08f, .02f)); Assert.That(runner.State, Is.EqualTo(MeleeActionState.Preparing));
        }
        [Test] public void CallbackFailuresCannotRetryAndAdvancementCannotReenter()
        {
            foreach (string phase in new[] { "activate", "sweep", "complete" }) {
                var host = new Host(); var runner = Runner(host, Definition(500)); Action fail = () => throw new InvalidOperationException(phase);
                if (phase == "activate") host.OnActivate = fail; else if (phase == "sweep") host.OnSweep = fail; else host.OnComplete = fail;
                Assert.Throws<InvalidOperationException>(() => Tick(runner, 0)); Assert.That(runner.State, Is.EqualTo(MeleeActionState.Failed)); Assert.That(runner.Error, Is.EqualTo(phase)); int count = host.Events.Count;
                Tick(runner, .02f); Assert.That(host.Events.Count, Is.EqualTo(count));
            }
            var reentryHost = new Host(); var reentry = Runner(reentryHost); reentryHost.OnActivate = () => reentry.Step(.18f, .02f);
            Assert.Throws<InvalidOperationException>(() => Tick(reentry, .18f)); Assert.That(reentry.State, Is.EqualTo(MeleeActionState.Failed));
        }
        [Test] public void CompletionKeepsTheLegacyFloatBoundaryInsteadOfTheRoundedSweepSample()
        {
            var host = new Host(); var runner = Runner(host, Definition(1, .12f, .36f)); float now = 0;
            for (int i = 0; i < 17; i++) { Tick(runner, now); now += .02f; }
            float sweepSample = now + .02f;
            Assert.That(sweepSample, Is.GreaterThanOrEqualTo(runner.Definition.End));
            Assert.That((double)now + .02f, Is.LessThan((double)runner.Definition.End));
            Tick(runner, now); Assert.That(runner.State, Is.EqualTo(MeleeActionState.Active)); Assert.That(host.Events.Contains("complete"), Is.False);
            now += .02f; Tick(runner, now); Assert.That(runner.State, Is.EqualTo(MeleeActionState.Completed)); Assert.That(host.Events.FindAll(e => e == "complete").Count, Is.EqualTo(1));
        }
        [Test] public void DefinitionsAndRunnerRemainOutsideUnityAssemblies()
        {
            Assert.That(typeof(MeleeActionRunner).Assembly.GetName().Name, Is.EqualTo("Deviant.Combat.Execution"));
            foreach (var assembly in new[] { typeof(MeleeActionDefinition).Assembly, typeof(MeleeActionRunner).Assembly, typeof(IMeleeActionHost).Assembly })
                foreach (var reference in assembly.GetReferencedAssemblies()) Assert.That(reference.Name.StartsWith("UnityEngine") || reference.Name.StartsWith("UnityEditor") || reference.Name == "Deviant.Combat", Is.False, reference.Name);
        }
    }
}
