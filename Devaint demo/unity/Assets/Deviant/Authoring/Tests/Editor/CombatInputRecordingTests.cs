using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using Deviant.CombatLab.Contracts;
using NUnit.Framework;

namespace Deviant.Authoring.Tests
{
    public class CombatInputRecordingTests
    {
        static CombatInput[] Inputs() => new[] { new CombatInput(1, CombatButtons.Basic | CombatButtons.Jump, true), new CombatInput(-1, CombatButtons.Drop, false, CombatButtons.Special) };
        [Test] public void RecordingContractHasNoEngineOrRuntimeImplementationReferences()
        {
            var assembly = typeof(CombatInputRecording).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo("Deviant.CombatLab.Contracts"));
            Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat"), Is.False);
        }
        [Test] public void RecordingCopiesTickIntentsAndExposesAnImmutableOrderedSnapshot()
        {
            var inputs = Inputs(); var recording = new CombatInputRecording("fixture/scenario", .02f, inputs);
            inputs[0] = default;
            Assert.That(recording.Inputs[0].Axis, Is.EqualTo(1)); Assert.That(recording.Inputs[0].SpecialHeld, Is.True);
            Assert.That(recording.Inputs[0].Pressed, Is.EqualTo(CombatButtons.Basic | CombatButtons.Jump));
            Assert.That(recording.Inputs[1].Axis, Is.EqualTo(-1));
            Assert.That(recording.Inputs[1].Released, Is.EqualTo(CombatButtons.Special));
            Assert.Throws<NotSupportedException>(() => ((IList<CombatInput>)recording.Inputs)[0] = default);
        }
        [Test] public void HashIncludesScenarioStepOrderAndEveryInputFieldAcrossCultures()
        {
            var inputs = Inputs(); var recording = new CombatInputRecording("fixture/scenario", .02f, inputs); var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                Assert.That(new CombatInputRecording("fixture/scenario", .02f, Inputs()).ContentHash, Is.EqualTo(recording.ContentHash));
                Assert.That(new CombatInputRecording("other/scenario", .02f, inputs).ContentHash, Is.Not.EqualTo(recording.ContentHash));
                Assert.That(new CombatInputRecording("fixture/scenario", .04f, inputs).ContentHash, Is.Not.EqualTo(recording.ContentHash));
                Assert.That(new CombatInputRecording("fixture/scenario", .02f, inputs.Reverse().ToArray()).ContentHash, Is.Not.EqualTo(recording.ContentHash));
                foreach (var changed in new[] {
                    new CombatInput(0, inputs[0].Pressed, inputs[0].SpecialHeld),
                    new CombatInput(inputs[0].Axis, CombatButtons.Special, inputs[0].SpecialHeld),
                    new CombatInput(inputs[0].Axis, inputs[0].Pressed, false),
                    new CombatInput(inputs[0].Axis, inputs[0].Pressed, inputs[0].SpecialHeld, CombatButtons.Special),
                    new CombatInput(inputs[0].Axis, inputs[0].Pressed, inputs[0].SpecialHeld, climbAxis: 1),
                    new CombatInput(inputs[0].Axis, inputs[0].Pressed, inputs[0].SpecialHeld, heldSpecials: CombatButtons.Weapon2Special) })
                    Assert.That(new CombatInputRecording("fixture/scenario", .02f, new[] { changed, inputs[1] }).ContentHash, Is.Not.EqualTo(recording.ContentHash));
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidStepIsRejectedBeforeRecordingStarts(float step)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatInputRecording("fixture", step, Inputs()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatInputRecorder("fixture", step, 2));
        }
        [TestCase(null)] [TestCase("")] [TestCase(" ")]
        public void MissingScenarioIdentityCannotBeRecorded(string id)
        {
            Assert.Throws<ArgumentException>(() => new CombatInputRecording(id, .02f, Inputs()));
            Assert.Throws<ArgumentException>(() => new CombatInputRecorder(id, .02f, 2));
        }
        [Test] public void EmptyInputsAndNonpositiveCapacityCannotClaimAnExperimentWasRecorded()
        {
            Assert.Throws<ArgumentException>(() => new CombatInputRecording("fixture", .02f, null));
            Assert.Throws<ArgumentException>(() => new CombatInputRecording("fixture", .02f, Array.Empty<CombatInput>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatInputRecorder("fixture", .02f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatInputRecorder("fixture", .02f, -1));
        }
        [Test] public void CompletionFreezesABoundedRecordingAndRepeatedCompletionReturnsTheSameSnapshot()
        {
            var recorder = new CombatInputRecorder("fixture", .02f, 3);
            recorder.Record(new CombatInput(1, CombatButtons.Basic)); recorder.Record(default); recorder.Record(new CombatInput(-1, CombatButtons.None, true));
            Assert.That(recorder.Count, Is.EqualTo(3)); Assert.That(recorder.CanRecord, Is.False); Assert.That(recorder.IsCompleted, Is.False);
            Assert.Throws<InvalidOperationException>(() => recorder.Record(default)); Assert.That(recorder.Count, Is.EqualTo(3));
            var snapshot = recorder.Complete();
            Assert.That(recorder.IsCompleted, Is.True); Assert.That(recorder.Complete(), Is.SameAs(snapshot));
            Assert.That(snapshot.Inputs.Count, Is.EqualTo(3)); Assert.That(snapshot.Inputs[2].SpecialHeld, Is.True);
            Assert.That(snapshot.Inputs[2].Pressed, Is.EqualTo(CombatButtons.None), "A held sample does not invent a pressed edge.");
            Assert.Throws<InvalidOperationException>(() => recorder.Record(default)); Assert.That(snapshot.Inputs.Count, Is.EqualTo(3));
        }
        [Test] public void AnEmptyCompletionAttemptDoesNotSealTheRecorder()
        {
            var recorder = new CombatInputRecorder("fixture", .02f, 1);
            Assert.Throws<InvalidOperationException>(() => recorder.Complete()); Assert.That(recorder.IsCompleted, Is.False); Assert.That(recorder.CanRecord, Is.True);
            recorder.Record(default); var recording = recorder.Complete();
            Assert.That(recording.ContentHash, Is.EqualTo(new CombatInputRecording("fixture", .02f, new[] { default(CombatInput) }).ContentHash));
        }
    }
}
