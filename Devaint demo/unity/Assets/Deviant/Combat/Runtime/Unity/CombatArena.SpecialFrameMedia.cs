using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        sealed class SpecialMediaReplacement
        {
            internal readonly ActionSpecialFrameCatalog Frames;
            internal readonly Dictionary<string, ActionTrackDefinition> Finite;
            internal readonly Dictionary<string, AuthoredSkillDefinition> Authored;
            internal readonly SpecialFrameCatalogDocument Document;
            internal readonly string BindingHash;
            internal SpecialMediaReplacement(ActionSpecialFrameCatalog frames, MotionSourceRegistry registry, FixedAttackMotion first, MeleeFrameCatalog melee)
            {
                Frames = frames; Document = frames.ToDocument();
                Finite = frames.Actions.Where(value => value.Family == SpecialFrameFamily.FiniteTracks).ToDictionary(value => value.Id, value => value.Finite, StringComparer.Ordinal);
                Authored = frames.Actions.Where(value => value.Family == SpecialFrameFamily.AuthoredSkill).ToDictionary(value => value.Id, value => value.Authored, StringComparer.Ordinal);
                BindingHash = frames.BindingHash(registry, first, melee);
            }
        }
        SpecialMediaReplacement PrepareSpecialMediaReplacement(MotionSourceRegistry registry, FixedAttackMotion first, MeleeFrameCatalog melee)
        {
            if (specialFrames == null) return null; // Initialization precedes this family owner.
            return new SpecialMediaReplacement(new ActionSpecialFrameCatalog(specialFrames.ToSourceDocument(), catalog, registry, first,
                specialNumericFinite.Values, specialNumericAuthored.Values), registry, first, melee);
        }
        void PublishSpecialMediaReplacement(SpecialMediaReplacement next)
        {
            if (next == null) return;
            specialFrames = next.Frames; actionTracks = next.Finite; authoredSkills = next.Authored;
            characterAnimations.specialFrames = next.Document;
            motionBindingHash = next.BindingHash;
        }
        public void ValidateSpecialFrameSources(ActionSpecialFrameCatalog candidate, IEnumerable<ActionTrackDefinition> finite, IEnumerable<AuthoredSkillDefinition> authored,
            MotionSourceRegistry registry, FixedAttackMotion first)
        {
            if (candidate == null || registry == null || first == null) throw new ArgumentException("The complete captured special source and structural media values are required.");
            var compiled = new ActionSpecialFrameCatalog(candidate.ToDocument(), catalog, registry, first, finite, authored);
            if (compiled.ContentHash != candidate.ContentHash) throw new ArgumentException("Captured special frame modes differ from their original numeric sources.");
            // The caller's current palette is the Native media availability
            // authority. Its timing weights may differ from the captured graph.
            ValidateMotionRegistry(registry);
        }
    }
}
