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
    public class FiniteActionTrackDefinitionTests
    {
        internal static SkillData Source(string id)
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            return catalog.forms.SelectMany(form => form.specials).Single(skill => skill.id == id);
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [TestCase(ActionTrackDefinition.MovementId)] [TestCase(ActionTrackDefinition.FollowUpId)]
        public void MigratedGeneratedTimingPreservesNativeFloatInputsAndStableHitOrder(string id)
        {
            var source = Source(id); var tracks = new ActionTrackDefinition(source); var compiled = tracks.Execution;
            Assert.That(compiled.UsesCompiledTracks, Is.True); Assert.That(compiled.Tracks, Is.SameAs(tracks));
            Assert.That(Bits(compiled.Activation), Is.EqualTo(Bits(source.activation)));
            if (id == ActionTrackDefinition.MovementId) Assert.That(compiled.End, Is.EqualTo(.5333333333f).Within(.000001f));
            else Assert.That(Bits(compiled.End), Is.EqualTo(Bits(source.end)));
            Assert.That(compiled.Hits.Select(hit => hit.Id), Is.EqualTo(id == ActionTrackDefinition.MovementId ? new[] { "thrust" } : new[] { "primary", "echo" }));
            for (int index = 0; index < source.hits.Length; index++)
            {
                Assert.That(Bits(compiled.Hits[index].Time), Is.EqualTo(Bits(source.hits[index].time)));
                if (id == ActionTrackDefinition.MovementId) Assert.That(compiled.Hits[index].Active, Is.EqualTo(.1333333333f).Within(.000001f));
                else Assert.That(Bits(compiled.Hits[index].Active), Is.EqualTo(Bits(source.hits[index].active)));
                Assert.That(compiled.Hits[index].Damage, Is.EqualTo(source.hits[index].damage));
                Assert.That(compiled.Hits[index].Box.Width, Is.EqualTo(source.actionTracks.hits[index].box.width));
            }
            Assert.That(new ActionTrackDefinition(tracks.ToData()).ContentHash, Is.EqualTo(tracks.ContentHash));
            Assert.That(new SkillExecutionDefinition(tracks.ToData()).UsesCompiledTracks, Is.True);
            if (tracks.Motion != null) {
                Assert.That(Bits(compiled.Movement.Start), Is.EqualTo(Bits(source.movement.start)));
                Assert.That(compiled.Movement.Duration, Is.EqualTo(.1333333333f).Within(.000001f), "The active track owns duration; the legacy .2-second value is provenance.");
                Assert.That(compiled.Movement.UsesCompiledTrack, Is.True);
            }
        }
        [Test] public void CompleteNumericCandidatesVersionValuesWithoutChangingFixedPoliciesOrTimingAuthority()
        {
            foreach (string id in ActionTrackDefinition.SupportedIds) {
                var original = new ActionTrackDefinition(Source(id)); var numeric = original.ToNumericData(); var document = original.ToDocument();
                Assert.That(original.Recompile(document, numeric).ContentHash, Is.EqualTo(original.ContentHash));
                for (int index = 0; index < numeric.hits.Length; index++) numeric.hits[index].damage += index + 3;
                if (id == ActionTrackDefinition.MovementId) { numeric.dashSpeed = 12; numeric.wallRecovery = .4f; }
                if (id == ActionTrackDefinition.WaveId) { numeric.waveSpeed = 10; numeric.waveDistance = 6; numeric.waveSpawnX = 2; numeric.waveSpawnY = 1.5f; }
                if (id == ActionTrackDefinition.GuardId) { numeric.guardHp = 45; numeric.guardReduction = .25f; }
                var candidate = original.Recompile(document, numeric);
                Assert.That(candidate.SourceVersion, Is.EqualTo(original.SourceVersion), id); Assert.That(candidate.ContentHash, Is.Not.EqualTo(original.ContentHash), id);
                Assert.That(new ActionTrackDefinition(candidate.ToData()).ContentHash, Is.EqualTo(candidate.ContentHash));
                Assert.That(candidate.Recompile(candidate.ToDocument()).ContentHash, Is.EqualTo(candidate.ContentHash));
                Assert.That(candidate.Activation, Is.EqualTo(original.Activation)); Assert.That(candidate.BodyEnd, Is.EqualTo(original.BodyEnd));
                for (int index = 0; index < numeric.hits.Length; index++) Assert.That(candidate.Execution.Hits[index].Damage, Is.EqualTo(numeric.hits[index].damage));
                if (candidate.Motion != null) { Assert.That(candidate.Execution.Movement.Speed, Is.EqualTo(12)); Assert.That(candidate.Execution.Movement.WallRecovery, Is.EqualTo(.4f)); Assert.That(candidate.ToData().movement.distance, Is.EqualTo(original.ToData().movement.distance), "Old distance remains inactive provenance, not a second travel control."); }
                numeric.guardHp = 999; Assert.That(candidate.ToNumericData().guardHp, Is.Not.EqualTo(999), "The numeric document is a defensive copy.");
                var policy = original.ToData(); policy.cooldown += 1; Assert.That(new ActionTrackDefinition(policy).SourceVersion, Is.Not.EqualTo(original.SourceVersion));
            }
        }
        [Test] public void NumericCandidatesRejectWrongGroupsStableIdsNonfiniteAndOutOfRangeValues()
        {
            foreach (string id in ActionTrackDefinition.SupportedIds) {
                var original = new ActionTrackDefinition(Source(id)); var invalid = original.ToNumericData(); invalid.hits = invalid.hits.Concat(new[] { new ActionHitNumericDocument { id = "extra", damage = 1 } }).ToArray();
                Assert.Throws<ArgumentException>(() => original.Recompile(original.ToDocument(), invalid));
                invalid = original.ToNumericData(); if (id == ActionTrackDefinition.GuardId) invalid.waveSpeed = 1; else invalid.guardHp = 1;
                Assert.Throws<ArgumentException>(() => original.Recompile(original.ToDocument(), invalid));
                invalid = original.ToNumericData(); if (invalid.hits.Length == 0) invalid.guardReduction = float.NaN; else invalid.hits[0].damage = float.NaN;
                Assert.Throws<ArgumentException>(() => original.Recompile(original.ToDocument(), invalid));
                if (original.Hits.Count > 0) { invalid = original.ToNumericData(); invalid.hits[0].id = "other"; Assert.Throws<ArgumentException>(() => original.Recompile(original.ToDocument(), invalid)); }
            }
            var dash = new ActionTrackDefinition(Source(ActionTrackDefinition.MovementId)); var bad = dash.ToNumericData(); bad.dashSpeed = 0; Assert.Throws<ArgumentException>(() => dash.Recompile(dash.ToDocument(), bad));
            bad = dash.ToNumericData(); bad.wallRecovery = -1; Assert.Throws<ArgumentException>(() => dash.Recompile(dash.ToDocument(), bad));
            var guard = new ActionTrackDefinition(Source(ActionTrackDefinition.GuardId)); bad = guard.ToNumericData(); bad.guardReduction = 1.01f; Assert.Throws<ArgumentException>(() => guard.Recompile(guard.ToDocument(), bad));
            var echo = new ActionTrackDefinition(Source(ActionTrackDefinition.FollowUpId)); bad = echo.ToNumericData(); bad.hits[0].damage = 9; bad.hits[1].damage = 17;
            var independent = echo.Recompile(echo.ToDocument(), bad); Assert.That(independent.Execution.Hits[0].Damage, Is.EqualTo(9)); Assert.That(independent.Execution.Hits[1].Damage, Is.EqualTo(17));
        }
        [Test] public void FrameAndMarkerEditsMoveMotionHitsAndBodyTogetherWithoutChangingProvenanceOrMetadata()
        {
            var original = new ActionTrackDefinition(Source(ActionTrackDefinition.MovementId)); var document = original.ToDocument();
            document.timeline.frames[0].seconds += .03f; document.hits[0].box.width += .5f;
            var edited = original.Recompile(document);
            Assert.That(edited.SourceVersion, Is.EqualTo(original.SourceVersion)); Assert.That(edited.ContentHash, Is.Not.EqualTo(original.ContentHash));
            Assert.That(edited.Activation, Is.EqualTo(.18f).Within(.000001f)); Assert.That(edited.BodyEnd, Is.EqualTo(.5633333333f).Within(.000001f));
            Assert.That(edited.Motion.Start, Is.EqualTo(edited.Hits[0].Start)); Assert.That(edited.Motion.Duration, Is.EqualTo(.1333333333f).Within(.000001f));
            Assert.That(edited.Execution.Hits[0].Box.Width, Is.EqualTo(2.6875f)); Assert.That(original.Hits[0].Box.Width, Is.EqualTo(2.1875f));
            Assert.That(edited.ToData().activation, Is.EqualTo(.15f), "Old numeric time is noneditable provenance, not active timing.");
            var timing = edited.CompileBody("base_sword/attack1", new[] { "ready", "hit" }, new[] { .2f, .3f }, 1, .2f);
            Assert.That(timing.Impact, Is.EqualTo(edited.Activation)); Assert.That(timing.End, Is.EqualTo(edited.BodyEnd)); Assert.That(timing.UsesFrameTime, Is.False); Assert.That(timing.FxStart, Is.EqualTo(-1));
        }
        [Test] public void IndependentTailCanFollowBodyEndAndKeepsItsOwnMarkerInterval()
        {
            var source = new ActionTrackDefinition(Source(ActionTrackDefinition.FollowUpId)); var document = source.ToDocument();
            document.timeline.markers.Single(marker => marker.id == "echo-start").offset = .6f;
            var edited = source.Recompile(document);
            Assert.That(edited.BodyEnd, Is.EqualTo(.5f)); Assert.That(edited.Hits[1].Start, Is.EqualTo(.8f)); Assert.That(edited.Hits[1].Duration, Is.EqualTo(.08f));
            Assert.That(edited.TailEnd, Is.EqualTo(.8f + .08f)); Assert.That(edited.Execution.Hits[1].TrackAnchor, Is.EqualTo(ActionHitAnchor.SavedPrimaryWorldPose));
        }
        [Test] public void InvalidWholeGraphsIdentitiesGeometryAndControlPoliciesCannotCompile()
        {
            var original = new ActionTrackDefinition(Source(ActionTrackDefinition.FollowUpId));
            foreach (Action<ActionTrackDocument> change in new Action<ActionTrackDocument>[] {
                document => document.timeline.frames[0].seconds = 0,
                document => document.timeline.markers.Single(marker => marker.id == "contact").reference = "missing",
                document => { var marker = document.timeline.markers.Single(value => value.id == "contact"); marker.anchor = TimelineAnchor.Marker; marker.reference = "echo-start"; },
                document => document.hits[1].id = "primary",
                document => document.hits[1].anchor = ActionHitAnchor.Actor,
                document => document.hits[1].box.height = float.NaN,
                document => document.control.vertical = ActionVerticalControl.FreezeDuringMotionThenFallFromZero,
                document => document.hits[0].end = "body-start"
            }) {
                var document = original.ToDocument(); change(document); Assert.Catch<ArgumentException>(() => original.Recompile(document));
            }
            Assert.That(original.Execution.Hits[1].Time, Is.EqualTo(.5f));
        }
        [Test] public void LegacyTimingAndGeometryAreProvenanceButDamageAndResourcePolicyRemainVersioned()
        {
            var source = Source(ActionTrackDefinition.FollowUpId); var original = new ActionTrackDefinition(source);
            source.activation = 50; source.end = 90; source.hits[0].time = 60; source.hits[0].active = 4; source.hits[0].box.width = 80;
            Assert.That(new ActionTrackDefinition(source).ContentHash, Is.EqualTo(original.ContentHash));
            source.hits[0].damage += 1; var changed = new ActionTrackDefinition(source);
            Assert.That(changed.SourceVersion, Is.EqualTo(original.SourceVersion)); Assert.That(changed.ContentHash, Is.Not.EqualTo(original.ContentHash));
            Assert.That(changed.Execution.Hits[0].Damage, Is.EqualTo(original.Execution.Hits[0].Damage + 1));
            source.cooldown += 1; Assert.That(new ActionTrackDefinition(source).SourceVersion, Is.Not.EqualTo(original.SourceVersion));
        }
        sealed class State
        {
            public IReadOnlyList<SkillHitDefinition> Hits;
            public SkillHitTrackExecution Track = new SkillHitTrackExecution(-.000001f);
            public readonly HitLedger Ledger = new HitLedger();
            public readonly List<string> Events = new List<string>();
            public float Elapsed;
            public bool Cancelled, Throw;
        }
        struct Host : ISkillHitTrackHost
        {
            public State State;
            public bool Activated => true;
            public bool Cancelled => State.Cancelled;
            public bool MotionDone => State.Cancelled;
            public float Elapsed => State.Elapsed;
            public float ChargeShift => 0;
            public bool DirectHitAllowed => true;
            public bool TargetHit => false;
            public bool TeleportClaimed => false;
            public bool MarkedTargetAlive => true;
            public void ApplyMarkedTarget(int index) => Assert.Fail("Unexpected mark policy.");
            public void ApplyTeleportTarget(int index) => Assert.Fail("Unexpected teleport policy.");
            public void ApplyBox(int index, SkillHitBoxAnchor anchor)
            {
                foreach (int target in new[] { 7, 8 }) {
                    if (!State.Ledger.First(index, target)) continue;
                    State.Events.Add(index + "/" + target + "/" + anchor);
                    if (State.Throw) throw new InvalidOperationException("native receive");
                    if (index == 0 && target == 7) State.Cancelled = true;
                }
            }
        }
        static void Evaluate(State state, float step) { var host = new Host { State = state }; state.Track.Evaluate(state.Hits, step, ref host); }
        [Test] public void CompiledDirectAndIndependentHitsKeepNativeClaimExceptionAndCancellationOrder()
        {
            var tracks = new ActionTrackDefinition(Source(ActionTrackDefinition.FollowUpId));
            var state = new State { Hits = tracks.Execution.Hits, Throw = true };
            Assert.Throws<InvalidOperationException>(() => Evaluate(state, 1)); Assert.That(state.Events, Is.EqualTo(new[] { "0/7/Actor" }));
            state.Throw = false; Evaluate(state, 1);
            Assert.That(state.Events, Is.EqualTo(new[] { "0/7/Actor", "0/8/Actor", "1/7/SavedPrimaryWorldPose", "1/8/SavedPrimaryWorldPose" }));
            state.Track.CommitEvaluation(1); state.Elapsed = 1; Evaluate(state, 1); Assert.That(state.Events.Count, Is.EqualTo(4));
        }
        [Test] public void CompiledSubTickHitIntervalsAreCrossedOnceAtPlainRuntimeBoundaries()
        {
            var original = new ActionTrackDefinition(Source(ActionTrackDefinition.FollowUpId)); var document = original.ToDocument();
            document.timeline.markers.Single(marker => marker.id == "primary-end").offset = .000001f;
            document.timeline.markers.Single(marker => marker.id == "echo-start").offset = .00001f;
            document.timeline.markers.Single(marker => marker.id == "echo-end").offset = .000001f;
            var state = new State { Hits = original.Recompile(document).Execution.Hits, Elapsed = .18f };
            Evaluate(state, .01f); Assert.That(state.Events, Is.Empty); state.Track.CommitEvaluation(.19f);
            state.Elapsed = .19f; Evaluate(state, .02f); Assert.That(state.Events.Count, Is.EqualTo(4));
            state.Track.CommitEvaluation(.21f); state.Elapsed = .21f; Evaluate(state, .02f); Assert.That(state.Events.Count, Is.EqualTo(4));
        }
    }
}
