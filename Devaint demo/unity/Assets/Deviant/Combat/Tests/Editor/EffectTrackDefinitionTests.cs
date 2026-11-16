using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class EffectTrackDefinitionTests
    {
        static SkillData Source(string id) => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text).forms.SelectMany(form => form.specials).Single(skill => skill.id == id);
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [TestCase(ActionTrackDefinition.WaveId)] [TestCase(ActionTrackDefinition.GuardId)]
        public void GeneratedTypedSpansPreserveNativeTimingMetadataAndRoundtrip(string id)
        {
            var source = Source(id); var tracks = new ActionTrackDefinition(source); var execution = tracks.Execution;
            Assert.That(execution.UsesCompiledTracks, Is.True); Assert.That(Bits(execution.Activation), Is.EqualTo(Bits(source.activation))); Assert.That(Bits(execution.End), Is.EqualTo(Bits(source.end)));
            Assert.That(execution.Effect.Type, Is.EqualTo(source.effect.type)); Assert.That(Bits(execution.Effect.Duration), Is.EqualTo(Bits(source.effect.duration)));
            Assert.That(execution.Effect.Speed, Is.EqualTo(source.effect.speed)); Assert.That(execution.Effect.Distance, Is.EqualTo(source.effect.distance));
            Assert.That(execution.Effect.Hp, Is.EqualTo(source.effect.hp)); Assert.That(execution.Effect.Reduction, Is.EqualTo(source.effect.reduction));
            Assert.That(new ActionTrackDefinition(tracks.ToData()).ContentHash, Is.EqualTo(tracks.ContentHash));
            if (id == ActionTrackDefinition.WaveId) {
                Assert.That(execution.HasProjectileWave, Is.True); Assert.That(tracks.Hits.Single().Anchor, Is.EqualTo(ActionHitAnchor.ProjectileWave));
                Assert.That(tracks.Effect.Duration, Is.EqualTo(.5f)); Assert.That(tracks.TailEnd, Is.EqualTo(.2f + .5f));
            }
            else { Assert.That(execution.HasOwnerGuard, Is.True); Assert.That(tracks.Hits, Is.Empty); Assert.That(tracks.Defense.Duration, Is.EqualTo(3)); }
        }
        [Test] public void EffectAndDefenseMarkerEditsPublishOneVersionWithoutUsingLegacyDuration()
        {
            foreach (string id in new[] { ActionTrackDefinition.WaveId, ActionTrackDefinition.GuardId }) {
                var source = Source(id); var original = new ActionTrackDefinition(source); var document = original.ToDocument();
                document.timeline.frames[0].seconds += .03f;
                document.timeline.markers.Single(marker => marker.id == (id == ActionTrackDefinition.WaveId ? "wave-end" : "guard-end")).offset = .75f;
                var candidate = original.Recompile(document);
                Assert.That(candidate.SourceVersion, Is.EqualTo(original.SourceVersion)); Assert.That(candidate.ContentHash, Is.Not.EqualTo(original.ContentHash));
                Assert.That(candidate.Execution.Effect.Duration, Is.EqualTo(.75f)); Assert.That(candidate.Activation, Is.EqualTo(original.Activation + .03f));
                source.effect.duration = 900; Assert.That(new ActionTrackDefinition(source).ContentHash, Is.EqualTo(original.ContentHash), "Legacy duration is provenance.");
                var timing = candidate.CompileBody("fixture/attack1", new[] { "a", "b" }, new[] { .2f, .3f }, 1, .2f);
                Assert.That(timing.Impact, Is.EqualTo(candidate.Activation)); Assert.That(timing.End, Is.EqualTo(candidate.BodyEnd));
            }
        }
        [Test] public void WaveAndGuardNumericPayloadsFeedExistingCompiledConsumersOnly()
        {
            var wave = new ActionTrackDefinition(Source(ActionTrackDefinition.WaveId)); var numeric = wave.ToNumericData();
            numeric.hits[0].damage = 53; numeric.waveSpeed = 8; numeric.waveDistance = 4; numeric.waveSpawnX = 2; numeric.waveSpawnY = 1.25f;
            var candidate = wave.Recompile(wave.ToDocument(), numeric); var effect = candidate.Execution.Effect;
            Assert.That(effect.Speed, Is.EqualTo(8)); Assert.That(effect.Distance, Is.EqualTo(4)); Assert.That(effect.X, Is.EqualTo(2)); Assert.That(effect.Y, Is.EqualTo(1.25f)); Assert.That(candidate.Execution.Hits[0].Damage, Is.EqualTo(53));
            Assert.That(candidate.Effect.Duration, Is.EqualTo(wave.Effect.Duration)); Assert.That(candidate.ToData().effect.duration, Is.EqualTo(wave.ToData().effect.duration));
            foreach (Action<ActionTrackNumericDocument> change in new Action<ActionTrackNumericDocument>[] { value => value.waveSpeed = 0, value => value.waveDistance = -1, value => value.waveSpawnY = float.PositiveInfinity }) {
                numeric = wave.ToNumericData(); change(numeric); Assert.Throws<ArgumentException>(() => wave.Recompile(wave.ToDocument(), numeric));
            }
            var guard = new ActionTrackDefinition(Source(ActionTrackDefinition.GuardId)); numeric = guard.ToNumericData(); numeric.guardHp = 50; numeric.guardReduction = .25f;
            candidate = guard.Recompile(guard.ToDocument(), numeric); Assert.That(candidate.Execution.Effect.Hp, Is.EqualTo(50)); Assert.That(candidate.Execution.Effect.Reduction, Is.EqualTo(.25f)); Assert.That(candidate.Execution.Effect.Duration, Is.EqualTo(3)); Assert.That(candidate.Hits, Is.Empty);
        }
        [Test] public void InvalidTypedSpansAndGeometryRejectBeforePublication()
        {
            var wave = new ActionTrackDefinition(Source(ActionTrackDefinition.WaveId));
            foreach (Action<ActionTrackDocument> change in new Action<ActionTrackDocument>[] {
                document => document.effect.id = "unknown",
                document => document.effect.kind = (ActionEffectTrackKind)1,
                document => document.effect.end = "body-start",
                document => document.hits[0].anchor = ActionHitAnchor.Actor,
                document => document.hits[0].box.x = 1,
                document => document.defense = new ActionDefenseTrackDocument { id = "guard", start = "contact", end = "wave-end" }
            }) { var document = wave.ToDocument(); change(document); Assert.Throws<ArgumentException>(() => wave.Recompile(document)); }
            var guard = new ActionTrackDefinition(Source(ActionTrackDefinition.GuardId)); var invalid = guard.ToDocument(); invalid.defense.start = "body-start";
            Assert.Throws<ArgumentException>(() => guard.Recompile(invalid));
        }
        [Test] public void ProjectileTypedHitIsNotDoubleDispatchedByTheDirectTrackOwner()
        {
            var tracks = new ActionTrackDefinition(Source(ActionTrackDefinition.WaveId)); var host = new HitHost(); var hit = new SkillHitTrackExecution(-.000001f);
            hit.Evaluate(tracks.Execution.Hits, 10, ref host); Assert.That(host.Reads, Is.Zero);
        }
        struct HitHost : ISkillHitTrackHost
        {
            public int Reads;
            public bool Activated { get { Reads++; return true; } }
            public bool Cancelled { get { Reads++; return false; } }
            public bool MotionDone { get { Reads++; return false; } }
            public float Elapsed { get { Reads++; return 0; } }
            public float ChargeShift { get { Reads++; return 0; } }
            public bool DirectHitAllowed { get { Reads++; return true; } }
            public bool TargetHit => false;
            public bool TeleportClaimed => false;
            public bool MarkedTargetAlive => false;
            public void ApplyMarkedTarget(int index) => Assert.Fail("unexpected direct dispatch");
            public void ApplyTeleportTarget(int index) => Assert.Fail("unexpected direct dispatch");
            public void ApplyBox(int index, SkillHitBoxAnchor anchor) => Assert.Fail("wave belongs to its dedicated sweep owner");
        }
        sealed class WaveState
        {
            public readonly List<float> Travel = new List<float>();
            public int Hits, Removed;
        }
        struct WaveHost : ISkillWaveExecutionHost
        {
            public WaveState State;
            public int Direction => 1;
            public bool Blocked(float x, float y) => false;
            public bool Occluded(float x, float y) => false;
            public void CreateVisual(float x, float y) { }
            public float Travel(float x, float y, int direction, float requested) { State.Travel.Add(requested); return requested; }
            public void ApplyBox(float previousX, float previousY, float x, float y) => State.Hits++;
            public void UpdateVisual(float x, float y) { }
            public void DestroyVisual() => State.Removed++;
        }
        [Test] public void AuthoredWaveSpanNeverIntroducesAFixedTimerOrDamageCutoff()
        {
            var original = new ActionTrackDefinition(Source(ActionTrackDefinition.WaveId)); var document = original.ToDocument();
            document.timeline.markers.Single(marker => marker.id == "wave-end").offset = .01f;
            var tracks = original.Recompile(document); var state = new WaveState(); var host = new WaveHost { State = state }; var wave = new SkillWaveExecution();
            wave.Activate(0, 0, tracks.Execution.Effect, ref host);
            wave.Step(.25f, tracks.Execution.Effect, ref host); Assert.That(wave.Read().Dead, Is.False); Assert.That(state.Hits, Is.EqualTo(1));
            wave.Step(.25f, tracks.Execution.Effect, ref host); Assert.That(wave.Read().Dead, Is.True); Assert.That(state.Travel.Sum(), Is.EqualTo(8));
        }
    }
}
