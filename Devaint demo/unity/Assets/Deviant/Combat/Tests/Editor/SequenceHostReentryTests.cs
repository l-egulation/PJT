using System;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SequenceHostReentryTests
    {
        sealed class Clock : ISequenceClock { public double ReadTime() => 0; }
        sealed class Bindings : ISequenceBindings
        {
            readonly SequenceHost host;
            readonly bool duringPoll;
            public bool ScopeDisposed, OperationDisposed;
            public Bindings(SequenceHost host, bool duringPoll) { this.host = host; this.duringPoll = duringPoll; }
            public bool Validate(SequenceDefinition definition, out string error) { error = null; return true; }
            public ISequenceBindingScope OpenScope(Guid executionId, SequenceDefinition definition) => new Scope(this);
            sealed class Scope : ISequenceBindingScope
            {
                readonly Bindings owner;
                public Scope(Bindings owner) { this.owner = owner; }
                public bool IsActive => !owner.ScopeDisposed;
                public ISequenceOperation Begin(SequenceStep step) => throw new InvalidOperationException("Fixture only uses Wait.");
                public ISequenceOperation BeginSkip(SequenceSkipDefinition skip)
                {
                    if (!owner.duringPoll) owner.host.Close();
                    return new Operation(owner);
                }
                public void Dispose() { owner.ScopeDisposed = true; }
            }
            sealed class Operation : ISequenceOperation
            {
                readonly Bindings owner;
                public Operation(Bindings owner) { this.owner = owner; }
                public SequenceOperationStatus Status => SequenceOperationStatus.Running;
                public string Error => null;
                public void Poll(double elapsed) { if (owner.duringPoll) owner.host.Close(); }
                public void Dispose() { owner.OperationDisposed = true; }
            }
        }
        // Current exposes the command contract. A lifecycle close raised by its
        // binding callback must be deferred just as it is during TryStart/Refresh.
        [TestCase(false)] [TestCase(true)]
        public void ClosingHostFromDirectSkipCommandDefersUntilTheCommandReturns(bool duringPoll)
        {
            var root = new GameObject("SequenceHostReentryFixture"); var host = root.AddComponent<SequenceHost>();
            try
            {
                using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture modal"))
                {
                    var block = external.BlockInput(InputChannels.Navigation); var pause = external.PauseSimulation();
                    var definition = new SequenceDefinition("fixture-close-during-skip", new[] { SequenceStep.Wait("wait", 1) }, Array.Empty<string>(), new SequenceSkipDefinition("fixture.end", 1));
                    var bindings = new Bindings(host, duringPoll);
                    Assert.That(host.TryStart(definition, bindings, controls, new SequenceControlPolicy(InputChannels.Combat, false), new Clock(), out var error), Is.True, error);
                    var commands = host.Current;
                    commands.TrySkip(commands.Read().ExecutionId, out _);
                    Assert.That(commands.Read().Status, Is.EqualTo(SequenceStatus.Cancelled), commands.Read().Error);
                    Assert.That(commands.Read().EndReason, Is.EqualTo(SequenceEndReason.OwnerDisposed));
                    Assert.That(bindings.ScopeDisposed && bindings.OperationDisposed, Is.True);
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(block.IsActive && pause.IsActive, Is.True);
                }
            }
            finally { host.Close(); Object.DestroyImmediate(root); }
        }
    }
}
