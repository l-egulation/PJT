using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    // Compilation value only. Lifecycle, hits, movement and independent effects
    // are still executed by their existing owners from this captured payload.
    public sealed class ActionSpecialFrameDefinition
    {
        public const string BodyStart = "body-start", BodyEnd = "body-end", Activation = "activation";
        public MotionActionKey Key { get; }
        public string Id => Key.ActionId;
        public string FormId { get; }
        public SpecialFrameFamily Family { get; }
        public MeleeTimingMode Mode { get; }
        public MeleeFrameMedia Media { get; }
        public FrameTimeline Timeline { get; }
        public ActionTrackDefinition NumericFinite { get; }
        public AuthoredSkillDefinition NumericAuthored { get; }
        public ActionTrackDefinition Finite { get; }
        public AuthoredSkillDefinition Authored { get; }
        public SkillExecutionDefinition Execution => Finite?.Execution ?? Authored.Execution;
        public string PolicyVersion { get; }
        public string ContentHash { get; }
        public string SourceHash { get; }
        readonly AttackMotionTiming frameMotion;
        public bool UsesFrameTime => Mode == MeleeTimingMode.FrameTime;
        internal ActionSpecialFrameDefinition(string form, MeleeTimingMode mode, MeleeFrameMedia media, FrameTimeline timeline,
            ActionTrackDefinition numericFinite, AuthoredSkillDefinition numericAuthored, ActionTrackDefinition finite, AuthoredSkillDefinition authored, string policyVersion)
        {
            FormId = form; Mode = mode; Media = media; Timeline = timeline;
            NumericFinite = numericFinite; NumericAuthored = numericAuthored; Finite = finite; Authored = authored;
            Family = finite != null ? SpecialFrameFamily.FiniteTracks : SpecialFrameFamily.AuthoredSkill;
            Key = new MotionActionKey(finite?.Id ?? authored.Id, MotionActionKind.Special); PolicyVersion = policyVersion;
            SourceHash = SpecialFrameValues.Hash(writer => {
                writer.Write("special-frame-source-v1"); writer.Write(Id); writer.Write(FormId); writer.Write((int)Family); writer.Write((int)Mode);
                writer.Write(Media.MotionId); writer.Write(Media.BindingVersion); writer.Write(PolicyVersion); writer.Write(Timeline?.ContentHash ?? "");
            });
            // Fixed starts retain the exact original observed definition version.
            ContentHash = !UsesFrameTime ? finite?.ContentHash ?? authored.ContentHash : SpecialFrameValues.Hash(writer => {
                writer.Write("special-frame-start-v1"); writer.Write(SourceHash); writer.Write(finite?.ContentHash ?? authored.ContentHash);
            });
            if (UsesFrameTime)
            {
                float contact = Finite?.Activation ?? (Id == "greatsword_follow_up" || Execution.Hits.Count == 0 ? Execution.Activation : Execution.Hits[0].Time);
                frameMotion = AttackMotionTiming.FrameTime(Media.MotionId, Media.FixedTiming.Frames.ToArray(), Timeline.Frames.Select(frame => frame.Seconds).ToArray(),
                    Math.Min(contact, Execution.End), -1, Execution.End, null);
            }
        }
        public SpecialFrameSourceDocument ToDocument() => new SpecialFrameSourceDocument {
            actionId = Id, formId = FormId, kind = MotionActionKind.Special, variant = -1, family = Family, mode = Mode,
            motionId = Media.MotionId, policyVersion = PolicyVersion, mediaBindingVersion = Media.BindingVersion,
            timeline = Timeline == null ? null : FrameTimelineDocument.From(Timeline)
        };
        public AttackMotionTiming CompileMotion()
        {
            float impact = Finite?.Activation ?? (Id == "greatsword_follow_up" || Id == "dagger_range" || Execution.Hits.Count == 0 ? Execution.Activation : Execution.Hits[0].Time);
            float end = Execution.End;
            // This bound is display metadata only; effective gameplay hits may
            // outlive the body and are never clamped to it.
            if (UsesFrameTime) return frameMotion;
            return new AttackMotionTiming(Media.MotionId, Media.FixedTiming.Frames.ToArray(), Media.FixedTiming.Seconds.ToArray(), -1,
                Media.FixedTiming.SourceImpact, -1, Math.Min(impact, end), end);
        }
        public int BodyFrameAt(float age, AttackMotionTiming timing)
        {
            if (timing == null) throw new ArgumentNullException(nameof(timing));
            if (UsesFrameTime || Finite != null) return timing.FrameAt(age);
            float impact = Id == "greatsword_follow_up" || Id == "dagger_range" || Execution.Hits.Count == 0 ? Execution.Activation : Execution.Hits[0].Time;
            float sourceTime = AttackMotionTiming.MapSourceTime(age, impact, Execution.End, timing.SourceImpact, timing.SourceDuration);
            return AttackMotionTiming.SelectFrame(timing.Seconds, timing.SourceDuration, sourceTime, false);
        }
    }
    internal static class SpecialFrameValues
    {
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static string Hash(Action<BinaryWriter> body)
        {
            using (var bytes = new MemoryStream()) { using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) body(writer);
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant(); }
        }
    }
}
