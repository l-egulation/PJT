using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Deviant.Definitions
{
    // Bounded compile-time adapters for existing policies. This class neither
    // executes a marker nor owns a clock; existing payload validators still
    // decide which complete hit/motion/effect combinations are legal.
    public static class ActionSpecialFrameCompiler
    {
        const string Activation = ActionSpecialFrameDefinition.Activation;
        const string BodyStart = ActionSpecialFrameDefinition.BodyStart;
        const string BodyEnd = ActionSpecialFrameDefinition.BodyEnd;
        const string MotionStart = "motion-start", MotionEnd = "motion-end", Teleport = "teleport-start";
        const string EffectStart = "effect-start", EffectEnd = "effect-end", ParryStart = "parry-start", ParryEnd = "parry-end";
        const string FreezeStart = "freeze-start", FreezeEnd = "freeze-end";
        public static SpecialFrameSourceDocument FixedFinite(ActionTrackDefinition numeric, string formId, MeleeFrameMedia media)
        {
            RequireFinite(numeric, formId); return Fixed(numeric.Id, formId, SpecialFrameFamily.FiniteTracks, media, FinitePolicyVersion(numeric, formId));
        }
        public static SpecialFrameSourceDocument FixedAuthored(AuthoredSkillDefinition numeric, MeleeFrameMedia media)
        {
            RequireAuthored(numeric); return Fixed(numeric.Id, numeric.FormId, SpecialFrameFamily.AuthoredSkill, media, AuthoredPolicyVersion(numeric));
        }
        static SpecialFrameSourceDocument Fixed(string id, string form, SpecialFrameFamily family, MeleeFrameMedia media, string policy)
        {
            if (media == null || media.CueEligible) throw new ArgumentException("An existing special body binding without a basic Cue is required.");
            return new SpecialFrameSourceDocument { actionId = id, formId = form, kind = MotionActionKind.Special, variant = -1, family = family,
                mode = MeleeTimingMode.CombatTimeFixed, motionId = media.MotionId, policyVersion = policy, mediaBindingVersion = media.BindingVersion };
        }
        public static ActionSpecialFrameDefinition CompileFinite(ActionTrackDefinition numeric, string formId, MeleeFrameMedia media, SpecialFrameSourceDocument source)
        {
            RequireFinite(numeric, formId); string policy = FinitePolicyVersion(numeric, formId);
            ValidateSource(source, numeric.Id, formId, SpecialFrameFamily.FiniteTracks, media, policy);
            if (source.mode == MeleeTimingMode.CombatTimeFixed)
                return new ActionSpecialFrameDefinition(formId, source.mode, media, null, numeric, null, numeric, null, policy);
            var template = numeric.ToDocument();
            var expected = new HashSet<string>(template.timeline.markers.Select(marker => marker.id), StringComparer.Ordinal);
            var timeline = CompileTimeline(source, media, expected, template.control.start, template.bodyEnd);
            template.timeline = FrameTimelineDocument.From(timeline); template.sourceTimeMode = ActionTrackSourceTimeMode.FrameTime;
            var effective = numeric.Recompile(template, numeric.ToNumericData());
            return new ActionSpecialFrameDefinition(formId, source.mode, media, timeline, numeric, null, effective, null, policy);
        }
        public static ActionSpecialFrameDefinition CompileAuthored(AuthoredSkillDefinition numeric, MeleeFrameMedia media, SpecialFrameSourceDocument source)
        {
            RequireAuthored(numeric); string policy = AuthoredPolicyVersion(numeric);
            ValidateSource(source, numeric.Id, numeric.FormId, SpecialFrameFamily.AuthoredSkill, media, policy);
            if (source.mode == MeleeTimingMode.CombatTimeFixed)
                return new ActionSpecialFrameDefinition(numeric.FormId, source.mode, media, null, null, numeric, null, numeric, policy);
            var values = numeric.ToNumericData(); var expected = AuthoredMarkerIds(numeric);
            var timeline = CompileTimeline(source, media, expected, BodyStart, BodyEnd);
            values.activation = Point(timeline, Activation); values.end = Point(timeline, BodyEnd);
            for (int index = 0; index < values.hits.Length; index++)
            {
                values.hits[index].time = Point(timeline, HitStart(index));
                values.hits[index].active = timeline.IntervalDuration(HitStart(index), HitEnd(index));
            }
            var move = numeric.Execution.Movement;
            if (move?.Type == "dash")
            {
                values.movement.start = Point(timeline, MotionStart); values.movement.duration = timeline.IntervalDuration(MotionStart, MotionEnd);
            }
            else if (move?.Type == "target_teleport_strike") values.movement.start = Point(timeline, Teleport);
            // Recast approach motion begins from accepted D/target/geometry at
            // runtime. Its mark window/speed/distance remain event-relative data.
            for (int index = 0; index < values.invulStart.Length; index++)
            {
                values.invulStart[index] = Point(timeline, InvulStart(index)); values.invulEnd[index] = Point(timeline, InvulEnd(index));
                timeline.IntervalDuration(InvulStart(index), InvulEnd(index));
            }
            if (values.verticalFreeze.Length != 0)
            {
                values.verticalFreeze[0] = Point(timeline, FreezeStart); values.verticalFreeze[1] = Point(timeline, FreezeEnd);
                timeline.IntervalDuration(FreezeStart, FreezeEnd);
            }
            if (OwnsEffectDuration(numeric))
            {
                if (Point(timeline, EffectStart) != values.activation) throw new ArgumentException("The existing independent effect starts only at this action's accepted activation.");
                values.effect.duration = timeline.IntervalDuration(EffectStart, EffectEnd);
            }
            if (numeric.Execution.Effect?.Type == "parry")
            {
                if (values.activation != 0 || Point(timeline, ParryStart) != 0) throw new ArgumentException("The existing parry starts at body time zero; conversion cannot move its activation.");
                values.effect.parryWindow = timeline.IntervalDuration(ParryStart, ParryEnd);
            }
            var effective = numeric.Recompile(values);
            return new ActionSpecialFrameDefinition(numeric.FormId, source.mode, media, timeline, null, numeric, null, effective, policy);
        }
        public static SpecialFrameSourceDocument ProjectFinite(ActionTrackDefinition numeric, string formId, MeleeFrameMedia media)
        {
            var source = FixedFinite(numeric, formId, media); var document = numeric.ToDocument();
            var frames = ProjectBody(media, numeric.Activation, numeric.BodyEnd);
            var markers = document.timeline.markers.Select(marker => marker.id == document.control.start ? new TimelineMarker(marker.id, TimelineAnchor.Seconds, "", 0)
                : marker.id == document.bodyEnd ? BodyEndMarker(frames, marker.id, numeric.BodyEnd) : ProjectMarker(marker.id, numeric.Timeline, frames)).ToArray();
            source.mode = MeleeTimingMode.FrameTime; source.timeline = FrameTimelineDocument.From(new FrameTimeline(frames, markers));
            var compiled = CompileFinite(numeric, formId, media, source);
            RequireFiniteProjection(numeric, compiled.Finite);
            return source;
        }
        public static SpecialFrameSourceDocument ProjectAuthored(AuthoredSkillDefinition numeric, MeleeFrameMedia media)
        {
            var source = FixedAuthored(numeric, media); var execution = numeric.Execution;
            float contact = execution.Hits.Count > 0 ? execution.Hits[0].Time : execution.Activation;
            // Dynamic fixed bodies can skip leading/trailing frames. A positive
            // local timeline must not pretend such a projection was lossless.
            if (contact <= 0 || contact >= execution.End)
                throw new ArgumentException("The inherited body has a zero/beyond-body contact and collapsed frame spans. Author a valid positive local FrameTime timeline explicitly; automatic lossless conversion is unavailable.");
            var frames = ProjectBody(media, contact, execution.End);
            var values = numeric.ToNumericData(); var markers = new List<TimelineMarker> {
                new TimelineMarker(BodyStart, TimelineAnchor.Seconds, "", 0),
                FrameTimingProjection.AnchorAt(frames, Activation, values.activation),
                BodyEndMarker(frames, BodyEnd, values.end)
            };
            for (int index = 0; index < values.hits.Length; index++)
            {
                markers.Add(FrameTimingProjection.AnchorAt(frames, HitStart(index), values.hits[index].time));
                markers.Add(new TimelineMarker(HitEnd(index), TimelineAnchor.Marker, HitStart(index), values.hits[index].active));
            }
            var move = execution.Movement;
            if (move?.Type == "dash")
            {
                markers.Add(FrameTimingProjection.AnchorAt(frames, MotionStart, values.movement.start));
                markers.Add(new TimelineMarker(MotionEnd, TimelineAnchor.Marker, MotionStart, values.movement.duration));
            }
            else if (move?.Type == "target_teleport_strike") markers.Add(FrameTimingProjection.AnchorAt(frames, Teleport, values.movement.start));
            for (int index = 0; index < values.invulStart.Length; index++)
            {
                markers.Add(FrameTimingProjection.AnchorAt(frames, InvulStart(index), values.invulStart[index]));
                markers.Add(FrameTimingProjection.AnchorAt(frames, InvulEnd(index), values.invulEnd[index]));
            }
            if (values.verticalFreeze.Length != 0)
            {
                markers.Add(FrameTimingProjection.AnchorAt(frames, FreezeStart, values.verticalFreeze[0]));
                markers.Add(FrameTimingProjection.AnchorAt(frames, FreezeEnd, values.verticalFreeze[1]));
            }
            if (OwnsEffectDuration(numeric))
            {
                markers.Add(new TimelineMarker(EffectStart, TimelineAnchor.Marker, Activation, 0));
                markers.Add(new TimelineMarker(EffectEnd, TimelineAnchor.Marker, EffectStart, values.effect.duration));
            }
            if (execution.Effect?.Type == "parry")
            {
                markers.Add(new TimelineMarker(ParryStart, TimelineAnchor.Marker, BodyStart, 0));
                markers.Add(new TimelineMarker(ParryEnd, TimelineAnchor.Marker, ParryStart, values.effect.parryWindow));
            }
            source.mode = MeleeTimingMode.FrameTime; source.timeline = FrameTimelineDocument.From(new FrameTimeline(frames, markers));
            var compiled = CompileAuthored(numeric, media, source);
            if (!SameAuthoredTimes(numeric.ToNumericData(), compiled.Authored.ToNumericData()))
                throw new ArgumentException("The inherited fixed body cannot project every current gameplay marker without changing a committed float. No FrameTime mode was published.");
            return source;
        }
        // Intentional new local timing for policies whose inherited body cannot
        // project losslessly. The caller authors the complete graph; no padding,
        // invented default seconds or silently removed shared IDs are supplied.
        public static SpecialFrameSourceDocument AuthorFinite(ActionTrackDefinition numeric, string formId, MeleeFrameMedia media, FrameTimeline timeline)
        {
            var source = FixedFinite(numeric, formId, media); source.mode = MeleeTimingMode.FrameTime;
            source.timeline = FrameTimelineDocument.From(timeline ?? throw new ArgumentNullException(nameof(timeline)));
            CompileFinite(numeric, formId, media, source); return source;
        }
        public static SpecialFrameSourceDocument AuthorAuthored(AuthoredSkillDefinition numeric, MeleeFrameMedia media, FrameTimeline timeline)
        {
            var source = FixedAuthored(numeric, media); source.mode = MeleeTimingMode.FrameTime;
            source.timeline = FrameTimelineDocument.From(timeline ?? throw new ArgumentNullException(nameof(timeline)));
            CompileAuthored(numeric, media, source); return source;
        }
        static TimelineFrame[] ProjectBody(MeleeFrameMedia media, float impact, float end)
        {
            if (media.FixedTiming.SourceImpact <= 0 || media.FixedTiming.SourceImpact >= media.FixedTiming.SourceDuration)
                throw new ArgumentException("A boundary source contact cannot be projected to positive local spans losslessly. Explicit local authoring is required.");
            var timing = new AttackMotionTiming(media.MotionId, media.FixedTiming.Frames.ToArray(), media.FixedTiming.Seconds.ToArray(), -1,
                media.FixedTiming.SourceImpact, -1, impact, end);
            var seconds = timing.EffectiveSeconds();
            if (seconds.Any(value => !SpecialFrameValues.Finite(value) || value <= 0))
                throw new ArgumentException("Collapsed inherited body spans cannot be converted by removing IDs or inventing minimum durations.");
            return media.StepIds.Select((id, index) => new TimelineFrame(id, seconds[index])).ToArray();
        }
        static TimelineMarker ProjectMarker(string id, FrameTimeline source, IReadOnlyList<TimelineFrame> frames)
        {
            var marker = source.Markers.Single(value => value.Id == id);
            // Retain authored positive interval/addition edges, including tails.
            if (marker.Anchor == TimelineAnchor.Marker) return new TimelineMarker(id, marker.Anchor, marker.Reference, marker.Offset);
            return FrameTimingProjection.AnchorAt(frames, id, source.Times[id]);
        }
        static TimelineMarker BodyEndMarker(IReadOnlyList<TimelineFrame> frames, string id, float end)
        {
            float duration = 0; foreach (var frame in frames) duration += frame.Seconds;
            return new TimelineMarker(id, TimelineAnchor.StepEnd, frames[frames.Count - 1].Id, end - duration);
        }
        static FrameTimeline CompileTimeline(SpecialFrameSourceDocument source, MeleeFrameMedia media, HashSet<string> expected, string bodyStart, string bodyEnd)
        {
            if (source.timeline == null) throw new ArgumentException("FrameTime requires one complete action-local timeline.");
            var timeline = source.timeline.Compile();
            if (!timeline.Frames.Select(frame => frame.Id).SequenceEqual(media.StepIds)) throw new ArgumentException("Shared step insertion/removal/order changes require explicit special-frame rebase.");
            if (timeline.Markers.Count != expected.Count || timeline.Markers.Any(marker => !expected.Contains(marker.Id)))
                throw new ArgumentException("Every existing policy timing channel is required exactly once; unsupported channels cannot be added.");
            if (Point(timeline, bodyStart) != 0 || Point(timeline, bodyEnd) <= 0 || Math.Abs(Point(timeline, bodyEnd) - timeline.Duration) > .000001f)
                throw new ArgumentException("Special body-start must be zero and body-end must match the complete positive local frame duration.");
            return timeline;
        }
        static void ValidateSource(SpecialFrameSourceDocument source, string id, string form, SpecialFrameFamily family, MeleeFrameMedia media, string policy)
        {
            if (media == null || media.CueEligible || source == null || source.actionId != id || source.formId != form || source.kind != MotionActionKind.Special || source.variant != -1 || source.family != family ||
                !Enum.IsDefined(typeof(MeleeTimingMode), source.mode) || source.motionId != media.MotionId ||
                (source.mode == MeleeTimingMode.FrameTime || !string.IsNullOrEmpty(source.policyVersion)) && source.policyVersion != policy ||
                (source.mode == MeleeTimingMode.FrameTime || !string.IsNullOrEmpty(source.mediaBindingVersion)) && source.mediaBindingVersion != media.BindingVersion)
                throw new ArgumentException("Special source identity, family, policy or structural media binding changed; reload or explicitly rebase.");
            if (source.mode == MeleeTimingMode.CombatTimeFixed && source.timeline != null && !source.timeline.IsEmpty)
                throw new ArgumentException("CombatTimeFixed cannot contain a hidden active special FrameTime graph.");
        }
        public static string FinitePolicyVersion(ActionTrackDefinition numeric, string formId)
        {
            RequireFinite(numeric, formId); var graph = numeric.ToDocument();
            return SpecialFrameValues.Hash(writer => {
                writer.Write("special-finite-frame-policy-v1"); writer.Write(numeric.SourceVersion); writer.Write(formId);
                writer.Write(graph.activation); writer.Write(graph.bodyEnd); writer.Write(graph.control.start); writer.Write(graph.control.end);
                writer.Write((int)graph.control.horizontal); writer.Write((int)graph.control.vertical);
                foreach (var marker in graph.timeline.markers.OrderBy(value => value.id, StringComparer.Ordinal)) writer.Write(marker.id);
                foreach (var hit in graph.hits) { writer.Write(hit.id); writer.Write(hit.start); writer.Write(hit.end); writer.Write((int)hit.anchor); }
                WriteSpan(writer, graph.motion?.id, graph.motion?.start, graph.motion?.end);
                WriteSpan(writer, graph.effect?.id, graph.effect?.start, graph.effect?.end);
                WriteSpan(writer, graph.defense?.id, graph.defense?.start, graph.defense?.end);
            });
        }
        static void WriteSpan(BinaryWriter writer, string id, string start, string end)
        { writer.Write(id ?? ""); writer.Write(start ?? ""); writer.Write(end ?? ""); }
        public static string AuthoredPolicyVersion(AuthoredSkillDefinition numeric)
        {
            RequireAuthored(numeric);
            return SpecialFrameValues.Hash(writer => {
                writer.Write("special-authored-frame-policy-v1"); writer.Write(numeric.SourceVersion);
                foreach (string id in AuthoredMarkerIds(numeric).OrderBy(value => value, StringComparer.Ordinal)) writer.Write(id);
                writer.Write(numeric.Id == "shield_default" ? "parry-zero-epoch" : "normal-activation");
                writer.Write("effect-origin=accepted-activation;relative-status-wall-recast-travel=numeric");
            });
        }
        public static IReadOnlyList<string> MarkerIds(AuthoredSkillDefinition numeric) => Array.AsReadOnly(AuthoredMarkerIds(numeric).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        static HashSet<string> AuthoredMarkerIds(AuthoredSkillDefinition numeric)
        {
            var values = numeric.ToNumericData(); var execution = numeric.Execution;
            var ids = new HashSet<string>(StringComparer.Ordinal) { BodyStart, Activation, BodyEnd };
            for (int index = 0; index < values.hits.Length; index++) { ids.Add(HitStart(index)); ids.Add(HitEnd(index)); }
            if (execution.Movement?.Type == "dash") { ids.Add(MotionStart); ids.Add(MotionEnd); }
            else if (execution.Movement?.Type == "target_teleport_strike") ids.Add(Teleport);
            for (int index = 0; index < values.invulStart.Length; index++) { ids.Add(InvulStart(index)); ids.Add(InvulEnd(index)); }
            if (values.verticalFreeze.Length != 0) { ids.Add(FreezeStart); ids.Add(FreezeEnd); }
            if (OwnsEffectDuration(numeric)) { ids.Add(EffectStart); ids.Add(EffectEnd); }
            if (execution.Effect?.Type == "parry") { ids.Add(ParryStart); ids.Add(ParryEnd); }
            return ids;
        }
        public static bool OwnsEffectDuration(AuthoredSkillDefinition numeric)
            => numeric.Execution.Effect?.Type == "stealth" || numeric.Execution.Effect?.Type == "smoke" || numeric.Execution.Effect?.Type == "decoy";
        static string HitStart(int index) => "hit-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "-start";
        static string HitEnd(int index) => "hit-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "-end";
        static string InvulStart(int index) => "invul-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "-start";
        static string InvulEnd(int index) => "invul-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "-end";
        static float Point(FrameTimeline timeline, string id) => timeline.Times.TryGetValue(id, out float value) ? value : throw new ArgumentException("Missing special frame marker: " + id);
        static void RequireFinite(ActionTrackDefinition value, string form)
        {
            if (value == null || !ActionTrackDefinition.Supports(value.Id) || form != (value.Id == ActionTrackDefinition.GuardId ? "shield" : "base_sword"))
                throw new ArgumentException("Only the four existing finite actions and their exact original form belong to this frame family.");
            if (value.SourceTimeMode != ActionTrackSourceTimeMode.CombatTimeFixed) throw new ArgumentException("Recompilation requires the original inactive fixed numeric graph, not a previously derived FrameTime payload.");
        }
        static void RequireAuthored(AuthoredSkillDefinition value)
        {
            if (value == null || !AuthoredSkillDefinition.Supports(value.Id)) throw new ArgumentException("Only the nineteen supported authored special policies belong to this frame family.");
        }
        static bool Same(float left, float right) => BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right);
        static void RequireFiniteProjection(ActionTrackDefinition before, ActionTrackDefinition after)
        {
            bool same = Same(before.Activation, after.Activation) && Same(before.BodyEnd, after.BodyEnd) && Same(before.ControlStart, after.ControlStart) && Same(before.ControlEnd, after.ControlEnd);
            for (int index = 0; index < before.Hits.Count; index++) same &= Same(before.Hits[index].Start, after.Hits[index].Start) && Same(before.Hits[index].Duration, after.Hits[index].Duration);
            if (before.Motion != null) same &= Same(before.Motion.Start, after.Motion.Start) && Same(before.Motion.Duration, after.Motion.Duration);
            if (before.Effect != null) same &= Same(before.Effect.Start, after.Effect.Start) && Same(before.Effect.Duration, after.Effect.Duration);
            if (before.Defense != null) same &= Same(before.Defense.Start, after.Defense.Start) && Same(before.Defense.Duration, after.Defense.Duration);
            if (!same) throw new ArgumentException("Finite projection changed a committed gameplay marker or authored interval; no mode was published.");
        }
        static bool SameAuthoredTimes(SkillNumericDocument before, SkillNumericDocument after)
        {
            bool same = Same(before.activation, after.activation) && Same(before.end, after.end);
            for (int index = 0; index < before.hits.Length; index++) same &= Same(before.hits[index].time, after.hits[index].time) && Same(before.hits[index].active, after.hits[index].active);
            if (before.movement != null) same &= Same(before.movement.start, after.movement.start) && Same(before.movement.duration, after.movement.duration);
            if (before.effect != null) same &= Same(before.effect.duration, after.effect.duration) && Same(before.effect.parryWindow, after.effect.parryWindow);
            for (int index = 0; index < before.invulStart.Length; index++) same &= Same(before.invulStart[index], after.invulStart[index]) && Same(before.invulEnd[index], after.invulEnd[index]);
            for (int index = 0; index < before.verticalFreeze.Length; index++) same &= Same(before.verticalFreeze[index], after.verticalFreeze[index]);
            return same;
        }
    }
}
