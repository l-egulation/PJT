using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CombatTickSnapshotTests
    {
        sealed class Entry
        {
            public readonly string Name;
            public Entry(string name) { Name = name; }
        }

        // The real orchestrator enters every phase. Only the entity callback is
        // supplied here; captures and cleanup use the same production owner.
        sealed class World : ICombatTickWorld
        {
            public readonly CombatTickSnapshot Snapshot = new CombatTickSnapshot();
            public readonly List<Entry>[] Sources = {
                new List<Entry>(), new List<Entry>(), new List<Entry>(),
                new List<Entry>(), new List<Entry>(), new List<Entry>()
            };
            public readonly List<string> Visits = new List<string>();
            public readonly CombatSimulation Simulation;
            public Action<CombatTickPhase, Entry> OnVisit;
            public bool Record = true;
            public World() { Simulation = new CombatSimulation(this); }
            public void ApplyInput(CombatInput input) { }
            public void ResetState(SimulationResetMode mode) { }
            public void Execute(CombatTickPhase phase, float dt)
            {
                List<Entry> source;
                switch (phase)
                {
                    case CombatTickPhase.Actions:
                    case CombatTickPhase.ActionPush: source = Sources[0]; break;
                    case CombatTickPhase.EnemyProjectiles: source = Sources[1]; break;
                    case CombatTickPhase.Bombs: source = Sources[2]; break;
                    case CombatTickPhase.PlayerProjectiles: source = Sources[3]; break;
                    case CombatTickPhase.Lasers: source = Sources[4]; break;
                    case CombatTickPhase.Bursts: source = Sources[5]; break;
                    default: return;
                }
                try
                {
                    Snapshot.Capture(source);
                    for (int i = 0; i < Snapshot.Count; i++)
                    {
                        var entry = (Entry)Snapshot[i];
                        if (entry == null) continue;
                        if (Record) Visits.Add(phase + ":" + entry.Name);
                        OnVisit?.Invoke(phase, entry);
                    }
                }
                finally { Snapshot.Clear(); }
            }
        }

        [Test] public void SimulationKeepsEntryOrderWhileCallbacksRemoveReplaceAndAppend()
        {
            var world = new World(); var a = new Entry("a"); var b = new Entry("b"); var c = new Entry("c");
            var replacement = new Entry("replacement"); var added = new Entry("added"); var casts = world.Sources[0];
            casts.AddRange(new[] { a, b, c });
            for (int i = 1; i < world.Sources.Length; i++) world.Sources[i].Add(new Entry("effect" + i));
            world.OnVisit = (phase, entry) => {
                if (phase == CombatTickPhase.Actions && entry == a)
                { casts.Remove(b); casts[1] = replacement; casts.Add(added); }
                if (phase == CombatTickPhase.ActionPush && entry == replacement)
                { casts.Clear(); casts.Add(c); }
            };
            world.Simulation.Step(.125f);
            Assert.That(world.Visits, Is.EqualTo(new[] {
                "Actions:a", "Actions:b", "Actions:c",
                "ActionPush:a", "ActionPush:replacement", "ActionPush:added",
                "EnemyProjectiles:effect1", "Bombs:effect2", "PlayerProjectiles:effect3", "Lasers:effect4", "Bursts:effect5"
            }));
            Assert.That(world.Snapshot.Count, Is.Zero); Assert.That(casts, Is.EqualTo(new[] { c }));
        }

        [Test] public void EmptyAndDuplicateEntriesAreNotCompactedOrDeduplicated()
        {
            var world = new World(); var entry = new Entry("same");
            world.Sources[0].AddRange(new[] { entry, null, entry });
            world.OnVisit = (phase, current) => {
                Assert.That(world.Snapshot.Count, Is.EqualTo(3));
                Assert.That(world.Snapshot[0], Is.SameAs(entry)); Assert.That(world.Snapshot[1], Is.Null);
                Assert.That(world.Snapshot[2], Is.SameAs(entry));
            };
            world.Simulation.Step(.125f);
            Assert.That(world.Visits, Is.EqualTo(new[] { "Actions:same", "Actions:same", "ActionPush:same", "ActionPush:same" }));
            world.Sources[0].Clear(); world.Visits.Clear(); world.Simulation.Step(.125f);
            Assert.That(world.Visits, Is.Empty); Assert.That(world.Snapshot.Count, Is.Zero);
        }

        [TestCase(CombatTickPhase.Actions)] [TestCase(CombatTickPhase.ActionPush)]
        [TestCase(CombatTickPhase.EnemyProjectiles)] [TestCase(CombatTickPhase.Bombs)]
        [TestCase(CombatTickPhase.PlayerProjectiles)] [TestCase(CombatTickPhase.Lasers)] [TestCase(CombatTickPhase.Bursts)]
        public void CallbackFailurePreservesPartialWorkClearsTheSnapshotAndUsesExistingFaultPolicy(CombatTickPhase failedPhase)
        {
            var world = new World(); var first = new Entry("first"); var failure = new InvalidOperationException("entered callback");
            foreach (var source in world.Sources) source.AddRange(new[] { first, new Entry("never") });
            int callbacks = 0;
            world.OnVisit = (phase, entry) => {
                if (phase != failedPhase) return;
                callbacks++;
                foreach (var source in world.Sources) source.Clear();
                throw failure;
            };
            Assert.That(Assert.Throws<InvalidOperationException>(() => world.Simulation.Step(.125f)), Is.SameAs(failure));
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(world.Visits[world.Visits.Count - 1], Is.EqualTo(failedPhase + ":first"));
            Assert.That(world.Snapshot.Count, Is.Zero); Assert.That(world.Simulation.IsFaulted, Is.True);
            Assert.That(world.Simulation.Clock.Now, Is.Zero); Assert.That(world.Simulation.Clock.TickCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => world.Simulation.Step(.125f));
            world.OnVisit = null; world.Simulation.Reset(SimulationResetMode.KeepTime); world.Simulation.Step(.125f);
            Assert.That(world.Simulation.IsFaulted, Is.False); Assert.That(world.Snapshot.Count, Is.Zero);
            Assert.That(world.Simulation.Clock.TickCount, Is.EqualTo(1));
        }

        [Test] public void SeparateSimulationOwnersCannotOverwriteEachOthersActiveSnapshot()
        {
            var outer = new World(); var inner = new World();
            outer.Sources[0].AddRange(new[] { new Entry("outer-a"), new Entry("outer-b") });
            inner.Sources[0].Add(new Entry("inner")); bool entered = false;
            outer.OnVisit = (phase, entry) => {
                if (entered) return;
                entered = true; inner.Simulation.Step(.125f);
            };
            outer.Simulation.Step(.125f);
            Assert.That(outer.Visits, Is.EqualTo(new[] { "Actions:outer-a", "Actions:outer-b", "ActionPush:outer-a", "ActionPush:outer-b" }));
            Assert.That(inner.Visits, Is.EqualTo(new[] { "Actions:inner", "ActionPush:inner" }));
        }

        [TestCase(1)] [TestCase(65)]
        public void AllSevenPhaseCapturesAllocateNothingAtWarmedCapacity(int count)
        {
            var world = new World { Record = false }; var entry = new Entry("warm");
            foreach (var source in world.Sources) for (int i = 0; i < 128; i++) source.Add(entry);
            for (int i = 0; i < 32; i++) world.Simulation.Step(.02f);
            foreach (var source in world.Sources) source.RemoveRange(count, source.Count - count);
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2048; i++) world.Simulation.Step(.02f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero, "Only new maximum capacity may allocate; actor callbacks are outside this measurement.");
            Assert.That(world.Snapshot.Count, Is.Zero);
        }
    }
}
