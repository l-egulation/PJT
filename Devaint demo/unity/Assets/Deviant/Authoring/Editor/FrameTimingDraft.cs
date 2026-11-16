using System;
using System.Linq;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    [Serializable] public sealed class FramePreviewStep
    {
        public string id;
        public float seconds;
    }
    [Serializable] public sealed class FramePreviewMarker
    {
        public string id, reference;
        public TimelineAnchor anchor;
        public float offset;
    }
    public sealed class FrameTimingDraft : ScriptableObject
    {
        public FramePreviewStep[] steps;
        public FramePreviewMarker[] markers;
        public BasicMeleeDocument actionSource;
        public AttackMotionDocument motionSource;
        public AttackMotionDocument canonicalSource;
        public float inputSpeed;
        public MeleeFrameDefinition CompileCandidate()
        {
            if (actionSource?.hit == null || actionSource.schemaVersion != 1 || !BasicMeleeDefinition.TryCreate(actionSource.id, actionSource.hit.ToData(), out var action, out var error))
                throw new ArgumentException("Reload the fixed action and motion before applying a frame candidate.");
            return MeleeFrameCompiler.CompileFirst(action, new FixedAttackMotion(motionSource), Compile(), inputSpeed);
        }
        public FrameTimeline Compile() => new FrameTimeline(
            steps?.Select(s => s == null ? null : new TimelineFrame(s.id, s.seconds)),
            markers?.Select(m => m == null ? null : new TimelineMarker(m.id, m.anchor, m.reference, m.offset)));
        public void Load(FrameTimeline definition, BasicMeleeDefinition action, FixedAttackMotion motion, FrameAttackMotionSource source, float inputSpeed)
        {
            this.inputSpeed = inputSpeed;
            actionSource = action == null ? null : new BasicMeleeDocument { schemaVersion = 1, id = action.Id, hit = BasicMeleeHitData.From(action) };
            motionSource = motion?.ToDocument();
            canonicalSource = source?.ToDocument();
            steps = definition.Frames.Select(s => new FramePreviewStep { id = s.Id, seconds = s.Seconds }).ToArray();
            markers = definition.Markers.Select(m => new FramePreviewMarker { id = m.Id, anchor = m.Anchor, reference = m.Reference, offset = m.Offset }).ToArray();
        }
    }
}
