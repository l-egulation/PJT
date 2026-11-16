using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    // The registry adapter supplies one resolved, start-independent media value.
    // Frame identities live in the registry; this snapshot never declares art.
    public sealed class MeleeFrameMedia
    {
        public string MotionId { get; }
        public IReadOnlyList<string> StepIds { get; }
        public AttackMotionTiming FixedTiming { get; }
        public bool CueEligible { get; }
        public string BindingVersion { get; }
        public MeleeFrameMedia(string motionId, IEnumerable<string> stepIds, AttackMotionTiming fixedTiming, bool cueEligible, string actionBindingIdentity)
        {
            if (string.IsNullOrWhiteSpace(motionId) || string.IsNullOrWhiteSpace(actionBindingIdentity)) throw new ArgumentException("An explicit motion and typed action binding identity are required.");
            FixedTiming = fixedTiming ?? throw new ArgumentNullException(nameof(fixedTiming));
            var ids = stepIds?.ToArray();
            if (ids == null || ids.Length != fixedTiming.Frames.Count || ids.Length == 0 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Resolved media requires the complete ordered, unique shared clip step IDs.", nameof(stepIds));
            MotionId = motionId; StepIds = Array.AsReadOnly(ids); CueEligible = cueEligible;
            // Default weights/contact/Fx values are deliberately excluded. A local
            // FrameTime override must not be retimed by another fixed-mode edit.
            BindingVersion = MeleeFrameValues.Hash(writer => {
                writer.Write("melee-frame-media-v1"); writer.Write(actionBindingIdentity); writer.Write(MotionId); writer.Write(CueEligible); writer.Write(ids.Length);
                for (int i = 0; i < ids.Length; i++) { writer.Write(ids[i]); writer.Write(fixedTiming.Frames[i]); }
            });
        }
    }

    // Exactly the two source values used by the existing CueElapsed mapping.
    // Effect frames/weights/offsets/Sprites remain a separate start capture.
    public sealed class CapturedCueMediaMapping
    {
        public float SourceImpact { get; }
        public float SourceDuration { get; }
        public string ContentHash { get; }
        public CapturedCueMediaMapping(float sourceImpact, float sourceDuration)
        {
            if (!MeleeFrameValues.Finite(sourceImpact) || !MeleeFrameValues.Finite(sourceDuration) || sourceDuration <= 0 || sourceImpact < 0 || sourceImpact > sourceDuration)
                throw new ArgumentException("Captured Cue mapping requires a finite contact within a positive source duration.");
            SourceImpact = sourceImpact; SourceDuration = sourceDuration;
            ContentHash = MeleeFrameValues.Hash(writer => { writer.Write("captured-cue-media-mapping-v1"); writer.Write(SourceImpact); writer.Write(SourceDuration); });
        }
        public CapturedCueMediaMappingDocument ToDocument() => new CapturedCueMediaMappingDocument { sourceImpact = SourceImpact, sourceDuration = SourceDuration };
    }

    public sealed class MeleeFrameDefinition
    {
        public const string Contact = "contact", HitEnd = "hit-end", ActionEnd = "action-end", AdvanceStart = "advance-start", AdvanceEnd = "advance-end", CueStart = "cue-start";
        public string Id => NumericAction.Id;
        public string FormId { get; }
        public MeleeFrameFamily Family { get; }
        public MeleeTimingMode Mode { get; }
        public BasicMeleeDefinition NumericAction { get; }
        public BasicMeleeDefinition Action { get; }
        public DirectMeleeSpecialDefinition NumericDirect { get; }
        public DirectMeleeSpecialDefinition Direct { get; }
        public MeleeFrameMedia Media { get; }
        public FrameTimeline Timeline { get; }
        public CapturedCueMediaMapping CueMapping { get; }
        public string PolicyVersion { get; }
        public string ContentHash { get; }
        public float AdvanceDistance { get; }
        public float AdvanceFrom { get; }
        public float AdvanceDuration { get; }
        public BasicMeleeDefinition SourceAction => NumericAction;
        readonly FixedAttackMotion delegatedMotion;
        public FixedAttackMotion SourceMotion => delegatedMotion ?? throw new InvalidOperationException("Only the delegated first-sword source has a FixedAttackMotion source view.");
        public bool CueEnabled => CueMapping != null;
        public float InputSpeed => inputSpeed;
        readonly float inputSpeed;
        internal MeleeFrameDefinition(string formId, MeleeFrameFamily family, MeleeTimingMode mode, BasicMeleeDefinition numeric, DirectMeleeSpecialDefinition direct,
            MeleeFrameMedia media, FrameTimeline timeline, CapturedCueMediaMapping cueMapping, string policyVersion, float inputSpeed, FixedAttackMotion delegatedMotion = null)
        {
            FormId = formId; Family = family; Mode = mode; NumericAction = numeric; NumericDirect = direct;
            Media = media; Timeline = timeline; CueMapping = cueMapping; PolicyVersion = policyVersion; this.inputSpeed = inputSpeed;
            this.delegatedMotion = delegatedMotion;
            if (mode == MeleeTimingMode.FrameTime)
            {
                var values = numeric.ToData(); values.impact = timeline.Times[Contact]; values.active = timeline.IntervalDuration(Contact, HitEnd); values.duration = timeline.Times[ActionEnd];
                if (!BasicMeleeDefinition.TryCreate(numeric.Id, values, out var action, out var error)) throw new ArgumentException(error);
                Action = action; Direct = direct?.Recompile(values);
                AdvanceDistance = family == MeleeFrameFamily.Basic && inputSpeed > 0 ? .65f : 0;
                AdvanceFrom = AdvanceDistance > 0 ? timeline.Times[AdvanceStart] : 0;
                AdvanceDuration = AdvanceDistance > 0 ? timeline.IntervalDuration(AdvanceStart, AdvanceEnd) : action.Duration;
            }
            else
            {
                Action = numeric; Direct = direct;
                AdvanceDistance = family == MeleeFrameFamily.Basic && inputSpeed > 0 ? .65f : 0;
                AdvanceFrom = family == MeleeFrameFamily.Basic ? Math.Max(0, numeric.Impact - .04f) : 0;
                AdvanceDuration = family == MeleeFrameFamily.Basic ? .08f : numeric.Duration;
            }
            ContentHash = MeleeFrameValues.Hash(writer => {
                writer.Write("melee-frame-definition-v1"); writer.Write(Id); writer.Write(FormId); writer.Write((int)Family); writer.Write((int)Mode);
                writer.Write(PolicyVersion); writer.Write(Media.BindingVersion); writer.Write(Action.ContentHash); writer.Write(Direct?.ContentHash ?? "");
                writer.Write(Timeline?.ContentHash ?? ""); writer.Write(CueMapping?.ContentHash ?? "");
                if (Mode == MeleeTimingMode.CombatTimeFixed) writer.Write(Media.FixedTiming.ContentHash);
            });
        }
        public MeleeActionDefinition CompileForStart(float basicSpeed)
        {
            if (Family == MeleeFrameFamily.Direct) return Mode == MeleeTimingMode.CombatTimeFixed ? Direct.Compile() : new MeleeActionDefinition(Action, 1, 0, 0, Action.Duration, Direct.ContentHash);
            if (Mode == MeleeTimingMode.CombatTimeFixed) return MeleeActionDefinition.ForBasic(Action, basicSpeed, inputSpeed);
            return new MeleeActionDefinition(Action, basicSpeed, AdvanceDistance, AdvanceFrom / basicSpeed, AdvanceDuration / basicSpeed);
        }
        public AttackMotionTiming CompileMotion(float basicSpeed)
        {
            float speed = Family == MeleeFrameFamily.Basic ? basicSpeed : 1;
            if (!MeleeFrameValues.Finite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(basicSpeed));
            if (Mode == MeleeTimingMode.CombatTimeFixed)
                return new AttackMotionTiming(Media.MotionId, Media.FixedTiming.Frames.ToArray(), Media.FixedTiming.Seconds.ToArray(), -1,
                    Media.FixedTiming.SourceImpact, Media.CueEligible ? Media.FixedTiming.FxStart : -1, Action.Impact / speed, Action.Duration / speed);
            return AttackMotionTiming.FrameTime(Media.MotionId, Media.FixedTiming.Frames.ToArray(), Timeline.Frames.Select(frame => frame.Seconds / speed).ToArray(),
                Action.Impact / speed, CueMapping == null ? -1 : Timeline.Times[CueStart] / speed, Action.Duration / speed, CueMapping);
        }
        public MeleeFrameSourceDocument ToDocument() => new MeleeFrameSourceDocument {
            actionId = Id, formId = FormId, family = Family, mode = Mode, motionId = Media.MotionId, policyVersion = PolicyVersion, mediaBindingVersion = Media.BindingVersion,
            timeline = Timeline == null ? null : FrameTimelineDocument.From(Timeline), cueMapping = CueMapping?.ToDocument()
        };
    }
    internal static class MeleeFrameValues
    {
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static string Hash(Action<BinaryWriter> body)
        {
            using (var bytes = new MemoryStream()) { using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) body(writer);
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant(); }
        }
    }
}
