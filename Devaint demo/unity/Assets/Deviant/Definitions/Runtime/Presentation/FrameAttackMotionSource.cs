using System;
using System.Linq;

namespace Deviant.Definitions
{
    [Serializable] public sealed class FixedAttackMotionBindingDocument
    {
        public int schemaVersion;
        public string id, actionId, mode;
        public AttackMotionStep[] steps;
        public float sourceImpact, fxStart;
        internal bool IsEmpty => schemaVersion == 0 && string.IsNullOrEmpty(id) && string.IsNullOrEmpty(actionId) && string.IsNullOrEmpty(mode) &&
            (steps == null || steps.Length == 0) && sourceImpact == 0 && fxStart == 0;
        public AttackMotionDocument ToDocument() => new AttackMotionDocument {
            schemaVersion = schemaVersion, id = id, actionId = actionId, mode = mode,
            steps = steps, sourceImpact = sourceImpact, fxStart = fxStart
        };
        public static FixedAttackMotionBindingDocument From(FixedAttackMotion binding)
        {
            var document = binding.ToDocument();
            return new FixedAttackMotionBindingDocument {
                schemaVersion = document.schemaVersion, id = document.id, actionId = document.actionId, mode = document.mode,
                steps = document.steps, sourceImpact = document.sourceImpact, fxStart = document.fxStart
            };
        }
    }
    [Serializable] public sealed class FrameTimelineStepDocument
    {
        public string id;
        public float seconds;
    }
    [Serializable] public sealed class FrameTimelineMarkerDocument
    {
        public string id, reference;
        public TimelineAnchor anchor;
        public float offset;
    }
    [Serializable] public sealed class FrameTimelineDocument
    {
        public FrameTimelineStepDocument[] frames;
        internal bool IsEmpty => (frames == null || frames.Length == 0) && (markers == null || markers.Length == 0);
        public FrameTimelineMarkerDocument[] markers;
        internal FrameTimeline Compile() => new FrameTimeline(
            frames?.Select(frame => frame == null ? null : new TimelineFrame(frame.id, frame.seconds)),
            markers?.Select(marker => marker == null ? null : new TimelineMarker(marker.id, marker.anchor, marker.reference, marker.offset)));
        internal static FrameTimelineDocument From(FrameTimeline timeline) => new FrameTimelineDocument {
            frames = timeline.Frames.Select(frame => new FrameTimelineStepDocument { id = frame.Id, seconds = frame.Seconds }).ToArray(),
            markers = timeline.Markers.OrderBy(marker => marker.Id, StringComparer.Ordinal).Select(marker => new FrameTimelineMarkerDocument {
                id = marker.Id, anchor = marker.Anchor, reference = marker.Reference, offset = marker.Offset
            }).ToArray()
        };
    }

    // A tagged first-hit source. Only Timeline owns gameplay times in FrameTime;
    // FixedBinding is read-only art identity and Cue media-curve provenance.
    public sealed class FrameAttackMotionSource
    {
        public const int FrameSchemaVersion = 2;
        public const string FrameMode = "FrameTime";
        public bool UsesFrameTime { get; }
        public FixedAttackMotion FixedBinding { get; }
        public FrameTimeline Timeline { get; }
        public FrameAttackMotionSource(AttackMotionDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.schemaVersion == 1 && document.mode == FixedAttackMotion.Mode)
            {
                FixedBinding = new FixedAttackMotion(document);
                return;
            }
            if (document.schemaVersion != FrameSchemaVersion || document.id != FixedAttackMotion.FirstSwordId ||
                document.actionId != FixedAttackMotion.FirstSwordAction || document.mode != FrameMode)
                throw new ArgumentException("Expected the first sword motion's schemaVersion 1 CombatTimeFixed or schemaVersion 2 FrameTime source.");
            if (document.steps != null && document.steps.Length != 0 || document.sourceImpact != 0 || document.fxStart != 0)
                throw new ArgumentException("FrameTime gameplay times belong only to timeline; root fixed timing fields are not allowed.");
            if (document.fixedBinding == null || document.timeline == null)
                throw new ArgumentException("FrameTime requires its unchanged fixed media/art binding and one frame timeline.");
            FixedBinding = new FixedAttackMotion(document.fixedBinding.ToDocument());
            Timeline = document.timeline.Compile();
            if (!Timeline.Frames.Select(frame => frame.Id).SequenceEqual(FixedBinding.StepIds))
                throw new ArgumentException("FrameTime requires the first sword hit's unchanged art step bindings.");
            UsesFrameTime = true;
        }
        public MeleeFrameDefinition Compile(BasicMeleeDefinition fixedAction, float inputSpeed)
        {
            if (!UsesFrameTime) throw new ArgumentException("CombatTimeFixed source must be explicitly converted in Frame Timing Preview before permanent frame compilation.");
            return MeleeFrameCompiler.CompileFirst(fixedAction, FixedBinding, Timeline, inputSpeed);
        }
        public static MeleeFrameDefinition CompileGenerated(AttackMotionDocument[] sources, BasicMeleeDefinition fixedAction, FixedAttackMotion binding, float inputSpeed)
        {
            if (sources == null || sources.Length == 0) return null;
            if (sources.Length != 1) throw new ArgumentException("Only one generated first-sword motion source is supported.");
            var source = new FrameAttackMotionSource(sources[0]);
            if (binding == null || source.FixedBinding.ContentHash != binding.ContentHash)
                throw new ArgumentException("Generated first-sword source differs from its read-only fixed media/art binding.");
            if (!source.UsesFrameTime) return null;
            var compiled = source.Compile(fixedAction, inputSpeed);
            compiled.CompileMotion(1f);
            return compiled;
        }
        public AttackMotionDocument ToDocument() => UsesFrameTime ? new AttackMotionDocument {
            schemaVersion = FrameSchemaVersion, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FrameMode,
            fixedBinding = FixedAttackMotionBindingDocument.From(FixedBinding), timeline = FrameTimelineDocument.From(Timeline)
        } : FixedBinding.ToDocument();
        public static FrameAttackMotionSource FromCandidate(MeleeFrameDefinition candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            return new FrameAttackMotionSource(new AttackMotionDocument {
                schemaVersion = FrameSchemaVersion, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FrameMode,
                fixedBinding = FixedAttackMotionBindingDocument.From(candidate.SourceMotion), timeline = FrameTimelineDocument.From(candidate.Timeline)
            });
        }
    }
}
