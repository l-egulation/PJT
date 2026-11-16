using System;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        public ActionSpecialFrameDefinition SpecialFrame { get; private set; }
        static void ValidateSpecialFrameStart(CombatArena arena, SkillData source, bool basic, string version,
            MeleeActionDefinition melee, MeleeFrameDefinition meleeFrame, DirectMeleeSpecialDefinition direct, ChargedMeleeDefinition charged,
            ActionTrackDefinition finite, AuthoredSkillDefinition authored, RangedActionDefinition ranged, ActionSpecialFrameDefinition frame)
        {
            if (frame == null)
            {
                if (finite?.SourceTimeMode == ActionTrackSourceTimeMode.FrameTime)
                    throw new ArgumentException("A derived finite FrameTime payload requires its same captured special frame/media source.");
                return;
            }
            if (arena == null || source == null || basic || melee != null || meleeFrame != null || direct != null || charged != null || ranged != null || frame.Id != source.id || frame.ContentHash != version ||
                (frame.Family == SpecialFrameFamily.FiniteTracks ? authored != null || finite == null || !ReferenceEquals(frame.Finite, finite) : finite != null || authored == null || !ReferenceEquals(frame.Authored, authored)))
                throw new ArgumentException("A special cast requires its one complete matching frame/numeric/media start snapshot without mixed policy families.");
            var current = arena.GetSpecialFrame(frame.Id);
            if (!ReferenceEquals(current, frame)) throw new ArgumentException("The special frame source is stale or belongs to another application binding.");
            if (frame.UsesFrameTime) frame.CompileMotion();
            // Resolve no mutable media or current weapon here. The application
            // captured this exact wrapper; presentation adopts it at the Native
            // start boundary after the existing resource/command setup.
        }
    }
}
