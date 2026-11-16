using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    [Serializable] public sealed class AttackMotionDocument
    {
        public int schemaVersion;
        public string id, actionId, mode;
        public AttackMotionStep[] steps;
        public float sourceImpact, fxStart;
        public FixedAttackMotionBindingDocument fixedBinding;
        public FrameTimelineDocument timeline;
    }
    [Serializable] public sealed class AttackMotionStep
    {
        public string id, frame;
        public float seconds;
    }

    // CombatTimeFixed owns source weights. FrameTime retains this immutable
    // binding only for art identity and the existing Cue media curve.
    public sealed class FixedAttackMotion
    {
        public const string FirstSwordId = "base_sword/attack1";
        public const string FirstSwordAction = "base_sword_basic_0";
        public const string Mode = "CombatTimeFixed";
        public IReadOnlyList<string> StepIds { get; }
        public IReadOnlyList<string> Frames { get; }
        public IReadOnlyList<float> Seconds { get; }
        public float SourceImpact { get; }
        public float FxStart { get; }
        public float SourceDuration { get; }
        public string ContentHash { get; }
        public FixedAttackMotion(AttackMotionDocument document)
        {
            if (document != null && document.mode == FrameAttackMotionSource.FrameMode)
                throw new ArgumentException("FrameTime source is edited in Frame Timing Preview. Its fixed media binding is read-only; reload that source there.");
            if (document == null || document.schemaVersion != 1 || document.id != FirstSwordId || document.actionId != FirstSwordAction || document.mode != Mode ||
                document.fixedBinding != null && !document.fixedBinding.IsEmpty || document.timeline != null && !document.timeline.IsEmpty)
                throw new ArgumentException("Expected schemaVersion 1, first sword motion/action IDs and CombatTimeFixed mode without a frame timeline.");
            if (document.steps == null || document.steps.Length == 0 || document.steps.Length > 256 ||
                document.steps.Any(s => s == null || string.IsNullOrWhiteSpace(s.id) || string.IsNullOrWhiteSpace(s.frame)) ||
                document.steps.Select(s => s.id).Distinct(StringComparer.Ordinal).Count() != document.steps.Length)
                throw new ArgumentException("Motion requires 1..256 frames with unique stable step IDs and frame references.");
            var frames = document.steps.Select(s => s.frame).ToArray();
            var seconds = document.steps.Select(s => s.seconds).ToArray();
            // Validate source metadata here; action markers are supplied only to Compile.
            var timing = new AttackMotionTiming(FirstSwordId, frames, seconds, -1, document.sourceImpact, document.fxStart, 0, 1);
            if (document.sourceImpact <= 0 || document.sourceImpact >= timing.SourceDuration || document.fxStart < -1 || document.fxStart > timing.SourceDuration || document.fxStart < 0 && document.fxStart != -1)
                throw new ArgumentException("Fixed motion needs an interior source contact and Cue start in [0,duration] or -1 (disabled).");
            StepIds = Array.AsReadOnly(document.steps.Select(s => s.id).ToArray());
            Frames = timing.Frames; Seconds = timing.Seconds;
            SourceDuration = timing.SourceDuration; SourceImpact = timing.SourceImpact; FxStart = timing.FxStart;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(FirstSwordId); writer.Write(FirstSwordAction); writer.Write(Mode); writer.Write(StepIds.Count);
                    for (int i = 0; i < StepIds.Count; i++) { writer.Write(StepIds[i]); writer.Write(Frames[i]); writer.Write(Seconds[i]); }
                    writer.Write(SourceImpact); writer.Write(FxStart);
                }
                using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public AttackMotionTiming Compile(float impact, float end)
            => new AttackMotionTiming(FirstSwordId, Frames.ToArray(), Seconds.ToArray(), -1, SourceImpact, FxStart, impact, end);
        public AttackMotionDocument ToDocument() => new AttackMotionDocument {
            schemaVersion = 1, id = FirstSwordId, actionId = FirstSwordAction, mode = Mode, sourceImpact = SourceImpact, fxStart = FxStart,
            steps = Enumerable.Range(0, StepIds.Count).Select(i => new AttackMotionStep { id = StepIds[i], frame = Frames[i], seconds = Seconds[i] }).ToArray()
        };
        public float ActionTime(float sourceTime, float impact, float end)
        {
            Compile(impact, end);
            if (!FrameTimeline.Finite(sourceTime) || sourceTime < 0) throw new ArgumentOutOfRangeException(nameof(sourceTime));
            return ActionBoundary(sourceTime, impact, end);
        }
        float ActionBoundary(float sourceTime, float impact, float end)
            => sourceTime <= SourceImpact ? sourceTime / SourceImpact * impact : impact + (sourceTime - SourceImpact) / (SourceDuration - SourceImpact) * Math.Max(.001f, end - impact);
        public IReadOnlyList<float> EffectiveSeconds(float impact, float end)
        {
            Compile(impact, end); // Same runtime validation and minimum recovery span.
            var result = new float[Seconds.Count]; float source = 0, previous = 0;
            for (int i = 0; i < result.Length; i++)
            {
                source += Seconds[i];
                float boundary = ActionBoundary(source, impact, end);
                boundary = Math.Min(end, Math.Max(previous, boundary));
                result[i] = boundary - previous; previous = boundary;
            }
            return Array.AsReadOnly(result);
        }
    }
}
