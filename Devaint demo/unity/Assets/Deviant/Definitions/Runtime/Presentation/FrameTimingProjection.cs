using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Definitions
{
    // Preserves currently interpreted fixed-mode times in a detached frame draft.
    // This does not switch the gameplay source mode or apply a new action.
    public static class FrameTimingProjection
    {
        public const string Contact = "contact";
        public const string ActionEnd = "action-end";
        public static void ValidatePreview(FrameTimeline timeline)
        {
            if (timeline == null || !timeline.Times.TryGetValue(Contact, out var contact) || !timeline.Times.TryGetValue(ActionEnd, out var end) || end <= 0 || contact >= end)
                throw new ArgumentException("Preview requires contact before a positive action end; active hit windows are not compiled here.");
        }
        public static TimelineMarker AnchorAt(IReadOnlyList<TimelineFrame> frames, string id, float time)
        {
            if (frames == null || frames.Count == 0 || !FrameTimeline.Finite(time) || time < 0) throw new ArgumentException("A finite marker time and nonempty frame list are required.");
            float start = 0; int selected = 0;
            while (selected < frames.Count - 1 && time >= start + frames[selected].Seconds) { start += frames[selected].Seconds; selected++; }
            return new TimelineMarker(id, TimelineAnchor.StepStart, frames[selected].Id, time - start);
        }
        public static FrameTimeline FromFixed(FixedAttackMotion motion, float impact, float end)
        {
            if (motion == null) throw new ArgumentNullException(nameof(motion));
            var seconds = motion.EffectiveSeconds(impact, end);
            if (seconds.Any(s => !FrameTimeline.Finite(s) || s <= 0))
                throw new ArgumentException("This frame preview requires positive interpreted frame spans; collapsed fixed-mode frames are not converted.");
            var frames = motion.StepIds.Select((id, i) => new TimelineFrame(id, seconds[i])).ToArray();
            float sum = 0; foreach (var frame in frames) sum += frame.Seconds;
            return new FrameTimeline(frames, new[] {
                AnchorAt(frames, Contact, impact),
                new TimelineMarker(ActionEnd, TimelineAnchor.StepEnd, frames[frames.Length - 1].Id, end - sum)
            });
        }
    }
}
