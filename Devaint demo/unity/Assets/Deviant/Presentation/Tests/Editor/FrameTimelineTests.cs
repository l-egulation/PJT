using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class FrameTimelineTests
    {
        static TimelineFrame[] Frames() => new[] { new TimelineFrame("ready", .1f), new TimelineFrame("contact-pose", .1f), new TimelineFrame("recover", .2f) };
        static TimelineMarker[] Markers() => new[] {
            new TimelineMarker("contact", TimelineAnchor.StepStart, "contact-pose", 0),
            new TimelineMarker("end", TimelineAnchor.StepEnd, "recover", 0),
            new TimelineMarker("cue", TimelineAnchor.Marker, "contact", -.02f),
            new TimelineMarker("absolute", TimelineAnchor.Seconds, "", .15f)
        };
        static FixedAttackMotion Motion() => new FixedAttackMotion(new AttackMotionDocument {
            schemaVersion = 1, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FixedAttackMotion.Mode, sourceImpact = .155f, fxStart = .125f,
            steps = new[] {
                new AttackMotionStep { id = "ready", frame = "same", seconds = .06f },
                new AttackMotionStep { id = "windup", frame = "other", seconds = .095f },
                new AttackMotionStep { id = "contact-pose", frame = "same", seconds = .215f }
            }
        });
        [Test] public void AbsoluteStepAndMarkerOffsetsResolveFromOneDeclaredSource()
        {
            var definition = new FrameTimeline(Frames(), Markers());
            Assert.That(definition.Duration, Is.EqualTo(.4f)); Assert.That(definition.Times["contact"], Is.EqualTo(.1f)); Assert.That(definition.Times["end"], Is.EqualTo(.4f));
            Assert.That(definition.Times["cue"], Is.EqualTo(.08f).Within(.000001f)); Assert.That(definition.Times["absolute"], Is.EqualTo(.15f));
            var outside = new FrameTimeline(Frames(), new[] { new TimelineMarker("independent", TimelineAnchor.StepEnd, "recover", 1) });
            Assert.That(outside.Times["independent"], Is.EqualTo(1.4f).Within(.000001f), "A definition compiler must not clip independently owned future events to motion length.");
        }
        [Test] public void ThirtyMillisecondsAndEarlierInsertionMoveStableAnchorsButNotAbsoluteMarkers()
        {
            var frames = Frames(); frames[0] = new TimelineFrame("ready", .13f); var changed = new FrameTimeline(frames, Markers());
            Assert.That(changed.Times["contact"], Is.EqualTo(.13f)); Assert.That(changed.Times["end"], Is.EqualTo(.43f).Within(.000001f)); Assert.That(changed.Times["absolute"], Is.EqualTo(.15f));
            var inserted = new FrameTimeline(new[] { new TimelineFrame("inserted", .05f) }.Concat(frames), Markers());
            Assert.That(inserted.Times["contact"], Is.EqualTo(.18f).Within(.000001f)); Assert.That(inserted.Times["end"], Is.EqualTo(.48f).Within(.000001f));
        }
        [Test] public void SnapshotCopiesInputCollectionsAndReturnsReadOnlyTimes()
        {
            var frames = Frames(); var markers = Markers(); var result = new FrameTimeline(frames, markers);
            frames[0] = new TimelineFrame("changed", 9); markers[0] = new TimelineMarker("changed", TimelineAnchor.Seconds, "", 9);
            Assert.That(result.Frames[0].Id, Is.EqualTo("ready")); Assert.That(result.Times["contact"], Is.EqualTo(.1f));
            Assert.Throws<NotSupportedException>(() => ((IList<TimelineFrame>)result.Frames)[0] = frames[0]);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, float>)result.Times)["contact"] = 9);
        }
        [Test] public void HashPreservesMarkerOrderIndependenceAndAnchorIntentAcrossCultures()
        {
            var original = new FrameTimeline(Frames(), Markers()); var culture = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Assert.That(new FrameTimeline(Frames(), Markers().Reverse()).ContentHash, Is.EqualTo(original.ContentHash));
                var markers = Markers(); markers[0] = new TimelineMarker("contact", TimelineAnchor.StepEnd, "ready", 0);
                var sameTime = new FrameTimeline(Frames(), markers); Assert.That(sameTime.Times["contact"], Is.EqualTo(original.Times["contact"])); Assert.That(sameTime.ContentHash, Is.Not.EqualTo(original.ContentHash));
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [Test] public void MissingReferencesDuplicatesAndCyclesCannotCompile()
        {
            Assert.Catch<ArgumentException>(() => new FrameTimeline(null, Markers())); Assert.Catch<ArgumentException>(() => new FrameTimeline(Frames(), null));
            Assert.Catch<ArgumentException>(() => new FrameTimeline(new[] { new TimelineFrame("same", .1f), new TimelineFrame("same", .1f) }, Markers()));
            Assert.Catch<ArgumentException>(() => new FrameTimeline(Frames(), new[] { Markers()[0], Markers()[0] }));
            Assert.Catch<ArgumentException>(() => new FrameTimeline(Frames(), new[] { new TimelineMarker("a", TimelineAnchor.StepStart, "missing", 0) }));
            Assert.Catch<ArgumentException>(() => new FrameTimeline(Frames(), new[] { new TimelineMarker("a", TimelineAnchor.Marker, "missing", 0) }));
            Assert.Catch<ArgumentException>(() => new FrameTimeline(Frames(), new[] { new TimelineMarker("a", TimelineAnchor.Marker, "b", 0), new TimelineMarker("b", TimelineAnchor.Marker, "a", 0) }));
            Assert.Catch<ArgumentException>(() => new FrameTimeline(Frames(), new[] { new TimelineMarker("a", TimelineAnchor.Marker, "a", 0) }));
        }
        [Test] public void InvalidOrAmbiguousTimesAndFloatOverflowAreRejected()
        {
            foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) Assert.Catch<ArgumentException>(() => new TimelineFrame("bad", value));
            Assert.Catch<ArgumentException>(() => new TimelineMarker("bad", TimelineAnchor.Seconds, "ready", .1f));
            Assert.Catch<ArgumentException>(() => new TimelineMarker("bad", TimelineAnchor.StepStart, "", .1f));
            Assert.Catch<ArgumentException>(() => new TimelineMarker("bad", (TimelineAnchor)99, "", 0));
            Assert.Catch<ArgumentException>(() => new TimelineMarker("bad", TimelineAnchor.Marker, "other", float.NaN));
            Assert.Catch<ArgumentException>(() => new FrameTimeline(Frames(), new[] { new TimelineMarker("bad", TimelineAnchor.StepStart, "ready", -.1f) }));
            Assert.Catch<ArgumentException>(() => new FrameTimeline(new[] { new TimelineFrame("a", float.MaxValue), new TimelineFrame("b", 1) }, new[] { new TimelineMarker("x", TimelineAnchor.Seconds, "", 0) }));
        }
        [Test] public void ProjectionPreservesInterpretedContactAndEndThenMovesWithFrameEdits()
        {
            foreach (var times in new[] { (.2f, .4f), (.3f, .6f), (.02f, .04f) }) {
                var projection = FrameTimingProjection.FromFixed(Motion(), times.Item1, times.Item2);
                Assert.That(projection.Times[FrameTimingProjection.Contact], Is.EqualTo(times.Item1).Within(.000001f)); Assert.That(projection.Times[FrameTimingProjection.ActionEnd], Is.EqualTo(times.Item2).Within(.000001f));
                Assert.That(projection.Frames.Sum(f => f.Seconds), Is.EqualTo(times.Item2).Within(.000001f));
                var frames = projection.Frames.ToArray(); frames[0] = new TimelineFrame(frames[0].Id, frames[0].Seconds + .03f); var changed = new FrameTimeline(frames, projection.Markers);
                Assert.That(changed.Times[FrameTimingProjection.Contact], Is.EqualTo(times.Item1 + .03f).Within(.000001f)); Assert.That(changed.Times[FrameTimingProjection.ActionEnd], Is.EqualTo(times.Item2 + .03f).Within(.000001f));
            }
        }
        [Test] public void PreviewRejectsMissingOrImpossibleContactAndEndWithoutChangingGenericMarkerRules()
        {
            var impossible = new FrameTimeline(Frames(), new[] {
                new TimelineMarker(FrameTimingProjection.Contact, TimelineAnchor.Seconds, "", .5f),
                new TimelineMarker(FrameTimingProjection.ActionEnd, TimelineAnchor.Seconds, "", .4f)
            });
            Assert.Catch<ArgumentException>(() => FrameTimingProjection.ValidatePreview(impossible));
            Assert.Catch<ArgumentException>(() => FrameTimingProjection.ValidatePreview(new FrameTimeline(Frames(), Markers())));
            FrameTimingProjection.ValidatePreview(FrameTimingProjection.FromFixed(Motion(), .2f, .4f));
        }
        [Test] public void ProjectionRejectsCollapsedSpansWithoutInventingNewFrameIdsOrDurations()
        {
            Assert.Catch<ArgumentException>(() => FrameTimingProjection.FromFixed(Motion(), 0, .4f));
            Assert.Throws<ArgumentNullException>(() => FrameTimingProjection.FromFixed(null, .2f, .4f));
        }
    }
}
