using System;
using System.Linq;

namespace Deviant.Definitions
{
    public static class MeleeFrameCompiler
    {
        public static MeleeFrameDefinition CompileFirstDocument(BasicMeleeDefinition numeric, FixedAttackMotion motion, FrameTimelineDocument timeline, float inputSpeed)
            => CompileFirst(numeric, motion, (timeline ?? throw new ArgumentNullException(nameof(timeline))).Compile(), inputSpeed);
        public static MeleeFrameDefinition CompileFirst(BasicMeleeDefinition numeric, FixedAttackMotion motion, FrameTimeline timeline, float inputSpeed)
        {
            if (numeric == null || numeric.Id != FixedAttackMotion.FirstSwordAction || motion == null || timeline == null) throw new ArgumentException("The delegated first-sword source requires its exact action, media and timeline.");
            var media = new MeleeFrameMedia(FixedAttackMotion.FirstSwordId, motion.StepIds, motion.Compile(0, 1), motion.FxStart >= 0,
                new MotionActionKey(FixedAttackMotion.FirstSwordAction, MotionActionKind.Basic) + "|base_sword|attack1|" + FixedAttackMotion.FirstSwordId + "|False");
            var markers = timeline.Markers.ToList();
            if (inputSpeed > 0 && !markers.Any(marker => marker.Id == MeleeFrameDefinition.AdvanceStart))
            {
                if (!timeline.Times.TryGetValue(MeleeFrameDefinition.Contact, out float contact)) throw new ArgumentException("First frame source requires its contact marker.");
                markers.Add(FrameTimingProjection.AnchorAt(timeline.Frames, MeleeFrameDefinition.AdvanceStart, Math.Max(0, contact - .04f)));
                markers.Add(new TimelineMarker(MeleeFrameDefinition.AdvanceEnd, TimelineAnchor.Marker, MeleeFrameDefinition.AdvanceStart, .08f));
            }
            var source = FixedBasic(numeric, "base_sword", inputSpeed, media); source.mode = MeleeTimingMode.FrameTime;
            source.timeline = FrameTimelineDocument.From(new FrameTimeline(timeline.Frames, markers));
            source.cueMapping = motion.FxStart < 0 ? null : new CapturedCueMediaMapping(motion.SourceImpact, motion.SourceDuration).ToDocument();
            var validated = CompileBasic(numeric, "base_sword", inputSpeed, media, source);
            return new MeleeFrameDefinition("base_sword", MeleeFrameFamily.Basic, MeleeTimingMode.FrameTime, numeric, null, media,
                validated.Timeline, validated.CueMapping, validated.PolicyVersion, inputSpeed, motion);
        }
        public static FrameTimeline ProjectFirst(BasicMeleeDefinition action, FixedAttackMotion motion, float inputSpeed)
        {
            var projected = FrameTimingProjection.FromFixed(motion, action.Impact, action.Duration); var markers = projected.Markers.ToList();
            markers.Add(FrameTimingProjection.AnchorAt(projected.Frames, MeleeFrameDefinition.HitEnd, action.Impact + action.Active));
            if (motion.FxStart >= 0) markers.Add(FrameTimingProjection.AnchorAt(projected.Frames, MeleeFrameDefinition.CueStart, motion.ActionTime(motion.FxStart, action.Impact, action.Duration)));
            return CompileFirst(action, motion, new FrameTimeline(projected.Frames, markers), inputSpeed).Timeline;
        }
        public static MeleeFrameDefinition CompileBasic(BasicMeleeDefinition numeric, string formId, float inputSpeed, MeleeFrameMedia media, MeleeFrameSourceDocument source)
        {
            if (numeric == null) throw new ArgumentNullException(nameof(numeric));
            RequireBasicIdentity(numeric.Id, formId);
            if (!MeleeFrameValues.Finite(inputSpeed) || inputSpeed < 0) throw new ArgumentException("The existing basic input-movement policy must be finite and nonnegative.", nameof(inputSpeed));
            string policy = BasicPolicyVersion(numeric.Id, formId, inputSpeed);
            ValidateSource(source, numeric.Id, formId, MeleeFrameFamily.Basic, media, policy);
            var timeline = CompileTimeline(source, media, inputSpeed > 0, media.CueEligible);
            var cue = CompileCue(source, media.CueEligible);
            return new MeleeFrameDefinition(formId, MeleeFrameFamily.Basic, source.mode, numeric, null, media, timeline, cue, policy, inputSpeed);
        }
        public static MeleeFrameDefinition CompileDirect(DirectMeleeSpecialDefinition numeric, MeleeFrameMedia media, MeleeFrameSourceDocument source)
        {
            if (numeric == null || !DirectMeleeSpecialDefinition.SupportedIds.Contains(numeric.Id)) throw new ArgumentException("Only the three existing direct single-hit specials belong to this frame family.", nameof(numeric));
            ValidateSource(source, numeric.Id, numeric.FormId, MeleeFrameFamily.Direct, media, numeric.SourceVersion);
            var timeline = CompileTimeline(source, media, false, false); var cue = CompileCue(source, false);
            if (!BasicMeleeDefinition.TryCreate(numeric.Id, numeric.ToBasicData(), out var hit, out var error)) throw new ArgumentException(error);
            return new MeleeFrameDefinition(numeric.FormId, MeleeFrameFamily.Direct, source.mode, hit, numeric, media, timeline, cue, numeric.SourceVersion, 0);
        }
        public static string BasicPolicyVersion(string id, string formId, float inputSpeed)
        {
            RequireBasicIdentity(id, formId);
            if (!MeleeFrameValues.Finite(inputSpeed) || inputSpeed < 0) throw new ArgumentException("The existing basic input-movement policy must be finite and nonnegative.", nameof(inputSpeed));
            return MeleeFrameValues.Hash(writer => { writer.Write("melee-basic-frame-policy-v1"); writer.Write(id); writer.Write(formId); writer.Write(inputSpeed); writer.Write(.65f); writer.Write(.04f); writer.Write(.08f); });
        }
        public static MeleeFrameSourceDocument FixedBasic(BasicMeleeDefinition numeric, string formId, float inputSpeed, MeleeFrameMedia media)
        {
            if (numeric == null || media == null) throw new ArgumentNullException(nameof(numeric));
            return FixedSource(numeric.Id, formId, MeleeFrameFamily.Basic, media, BasicPolicyVersion(numeric.Id, formId, inputSpeed));
        }
        public static MeleeFrameSourceDocument FixedDirect(DirectMeleeSpecialDefinition numeric, MeleeFrameMedia media)
        {
            if (numeric == null || media == null) throw new ArgumentNullException(nameof(numeric));
            if (!DirectMeleeSpecialDefinition.SupportedIds.Contains(numeric.Id)) throw new ArgumentException("Only the three supported direct specials belong to this frame family.");
            return FixedSource(numeric.Id, numeric.FormId, MeleeFrameFamily.Direct, media, numeric.SourceVersion);
        }
        static MeleeFrameSourceDocument FixedSource(string id, string formId, MeleeFrameFamily family, MeleeFrameMedia media, string policy)
            => new MeleeFrameSourceDocument { actionId = id, formId = formId, family = family, mode = MeleeTimingMode.CombatTimeFixed, motionId = media.MotionId, policyVersion = policy, mediaBindingVersion = media.BindingVersion };
        public static MeleeFrameSourceDocument ProjectBasic(BasicMeleeDefinition numeric, string formId, float inputSpeed, MeleeFrameMedia media)
        {
            var source = FixedBasic(numeric, formId, inputSpeed, media);
            Project(source, numeric, media, inputSpeed > 0, media.CueEligible);
            CompileBasic(numeric, formId, inputSpeed, media, source); return source;
        }
        public static MeleeFrameSourceDocument ProjectDirect(DirectMeleeSpecialDefinition numeric, MeleeFrameMedia media)
        {
            var source = FixedDirect(numeric, media);
            if (!BasicMeleeDefinition.TryCreate(numeric.Id, numeric.ToBasicData(), out var hit, out var error)) throw new ArgumentException(error);
            Project(source, hit, media, false, false); CompileDirect(numeric, media, source); return source;
        }
        static void Project(MeleeFrameSourceDocument source, BasicMeleeDefinition numeric, MeleeFrameMedia media, bool advance, bool cueEligible)
        {
            var seconds = ProjectSeconds(media.FixedTiming, numeric.Impact, numeric.Duration);
            if (seconds.Any(value => value <= 0)) throw new ArgumentException("Collapsed fixed-mode frame spans cannot be converted by deleting IDs or inventing minimum durations.");
            var frames = media.StepIds.Select((id, index) => new TimelineFrame(id, seconds[index])).ToArray();
            var markers = new System.Collections.Generic.List<TimelineMarker> {
                FrameTimingProjection.AnchorAt(frames, MeleeFrameDefinition.Contact, numeric.Impact),
                new TimelineMarker(MeleeFrameDefinition.HitEnd, TimelineAnchor.Marker, MeleeFrameDefinition.Contact, numeric.Active),
                FrameTimingProjection.AnchorAt(frames, MeleeFrameDefinition.ActionEnd, numeric.Duration)
            };
            if (advance)
            {
                markers.Add(FrameTimingProjection.AnchorAt(frames, MeleeFrameDefinition.AdvanceStart, Math.Max(0, numeric.Impact - .04f)));
                markers.Add(new TimelineMarker(MeleeFrameDefinition.AdvanceEnd, TimelineAnchor.Marker, MeleeFrameDefinition.AdvanceStart, .08f));
            }
            if (cueEligible)
            {
                if (media.FixedTiming.FxStart < 0) throw new ArgumentException("The eligible Cue requires its existing enabled source origin.");
                markers.Add(FrameTimingProjection.AnchorAt(frames, MeleeFrameDefinition.CueStart, ActionTime(media.FixedTiming.FxStart, media.FixedTiming, numeric.Impact, numeric.Duration)));
                source.cueMapping = new CapturedCueMediaMapping(media.FixedTiming.SourceImpact, media.FixedTiming.SourceDuration).ToDocument();
            }
            source.mode = MeleeTimingMode.FrameTime; source.timeline = FrameTimelineDocument.From(new FrameTimeline(frames, markers));
        }
        static float[] ProjectSeconds(AttackMotionTiming media, float impact, float end)
            => new AttackMotionTiming(media.Id, media.Frames.ToArray(), media.Seconds.ToArray(), -1, media.SourceImpact, media.FxStart, impact, end).EffectiveSeconds().ToArray();
        static float ActionTime(float time, AttackMotionTiming media, float impact, float end)
            => new AttackMotionTiming(media.Id, media.Frames.ToArray(), media.Seconds.ToArray(), -1, media.SourceImpact, media.FxStart, impact, end).ActionTime(time);
        static void ValidateSource(MeleeFrameSourceDocument source, string id, string formId, MeleeFrameFamily family, MeleeFrameMedia media, string policy)
        {
            if (media == null) throw new ArgumentNullException(nameof(media));
            if (source == null || source.actionId != id || source.formId != formId || source.family != family || !Enum.IsDefined(typeof(MeleeTimingMode), source.mode) || source.motionId != media.MotionId ||
                (source.mode == MeleeTimingMode.FrameTime || !string.IsNullOrEmpty(source.policyVersion)) && source.policyVersion != policy ||
                (source.mode == MeleeTimingMode.FrameTime || !string.IsNullOrEmpty(source.mediaBindingVersion)) && source.mediaBindingVersion != media.BindingVersion)
                throw new ArgumentException("Melee frame source identity, family, policy or shared media binding changed. Reload or explicitly rebase; no source was applied.");
            if (source.mode == MeleeTimingMode.CombatTimeFixed && (source.timeline != null && !source.timeline.IsEmpty || source.cueMapping != null && (source.cueMapping.sourceImpact != 0 || source.cueMapping.sourceDuration != 0)))
                throw new ArgumentException("CombatTimeFixed cannot contain a hidden active frame timeline or Cue mapping.");
        }
        static FrameTimeline CompileTimeline(MeleeFrameSourceDocument source, MeleeFrameMedia media, bool advance, bool cueEligible)
        {
            if (source.mode == MeleeTimingMode.CombatTimeFixed) return null;
            if (source.timeline == null) throw new ArgumentException("FrameTime requires one complete action-local frame timeline.");
            var timeline = source.timeline.Compile();
            if (!timeline.Frames.Select(frame => frame.Id).SequenceEqual(media.StepIds)) throw new ArgumentException("Shared clip step insertion/removal/reordering requires an explicit frame-source rebase.");
            var allowed = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal) { MeleeFrameDefinition.Contact, MeleeFrameDefinition.HitEnd, MeleeFrameDefinition.ActionEnd };
            if (advance) { allowed.Add(MeleeFrameDefinition.AdvanceStart); allowed.Add(MeleeFrameDefinition.AdvanceEnd); }
            if (cueEligible) allowed.Add(MeleeFrameDefinition.CueStart);
            if (timeline.Markers.Any(marker => !allowed.Contains(marker.Id))) throw new ArgumentException("Only the supported melee hit/end/advance/Cue markers can own this action's timing.");
            if (!timeline.Times.TryGetValue(MeleeFrameDefinition.Contact, out var contact) || !timeline.Times.TryGetValue(MeleeFrameDefinition.HitEnd, out var hitEnd) || !timeline.Times.TryGetValue(MeleeFrameDefinition.ActionEnd, out var end) || contact >= hitEnd || hitEnd > end || end <= 0 || Math.Abs(end - timeline.Duration) > .000001f)
                throw new ArgumentException("Frame melee requires contact < hit-end <= action-end at the complete local frame duration.");
            timeline.IntervalDuration(MeleeFrameDefinition.Contact, MeleeFrameDefinition.HitEnd);
            if (advance)
            {
                if (!timeline.Times.ContainsKey(MeleeFrameDefinition.AdvanceStart) || !timeline.Times.ContainsKey(MeleeFrameDefinition.AdvanceEnd))
                    throw new ArgumentException("A moving basic requires its existing positive advance interval; the runner retains its original action-end truncation.");
                timeline.IntervalDuration(MeleeFrameDefinition.AdvanceStart, MeleeFrameDefinition.AdvanceEnd);
            }
            else if (timeline.Times.ContainsKey(MeleeFrameDefinition.AdvanceStart) || timeline.Times.ContainsKey(MeleeFrameDefinition.AdvanceEnd)) throw new ArgumentException("Stationary basics/direct specials cannot acquire a new advance policy.");
            if (cueEligible ? !timeline.Times.TryGetValue(MeleeFrameDefinition.CueStart, out var cue) || cue >= end : timeline.Times.ContainsKey(MeleeFrameDefinition.CueStart))
                throw new ArgumentException("Cue markers must match the existing eligible basic Cue policy.");
            return timeline;
        }
        static CapturedCueMediaMapping CompileCue(MeleeFrameSourceDocument source, bool cueEligible)
        {
            if (source.mode == MeleeTimingMode.CombatTimeFixed) return null;
            if (!cueEligible)
            {
                if (source.cueMapping != null && (source.cueMapping.sourceImpact != 0 || source.cueMapping.sourceDuration != 0)) throw new ArgumentException("This action cannot gain a Cue mapping.");
                return null;
            }
            if (source.cueMapping == null) throw new ArgumentException("The existing eligible Cue requires its captured media mapping.");
            return new CapturedCueMediaMapping(source.cueMapping.sourceImpact, source.cueMapping.sourceDuration);
        }
        static void RequireBasicIdentity(string id, string form)
        {
            int count;
            switch (form) { case "base_sword": case "whip": case "hammer": count = 2; break; case "rapier": case "greatsword": count = 3; break; case "dagger": count = 4; break; case "shield": count = 1; break; default: throw new ArgumentException("This frame compiler supports only the seven existing melee basic forms."); }
            int offset = form.Length + "_basic_".Length;
            if (id == null || !id.StartsWith(form + "_basic_", StringComparison.Ordinal) || !int.TryParse(id.Substring(offset), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index) || index < 0 || index >= count || id != form + "_basic_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new ArgumentException("A supported existing melee basic action identity is required.");
        }
    }
}
