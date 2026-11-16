using System;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        MeleeFrameDefinition frameSwordTiming;
        public MeleeFrameDefinition FrameSwordTiming => frameSwordTiming;
        bool InitializeSwordFrameSource(out string error)
        {
            try
            {
                var sources = characterAnimations?.motionSources;
                if (sources == null || sources.Length == 0) { error = null; return true; }
                var binding = new FixedAttackMotion(MotionDocument(FirstSwordMotionClip(out _)));
                frameSwordTiming = FrameAttackMotionSource.CompileGenerated(sources, basicSwordFirst, binding,
                    System.Linq.Enumerable.Single(catalog.forms, form => form.id == "base_sword").inputSpeed);
                error = null; return true;
            }
            catch (ArgumentException exception) { error = "Invalid generated sword motion source: " + exception.Message; return false; }
        }
        public bool TryApplySwordFrameTiming(MeleeFrameDefinition candidate, bool resetScenario, out string error)
        {
            if (!Player) { error = "Combat runtime is not initialized."; return false; }
            if (candidate == null) { error = "A complete frame action and motion candidate is required."; return false; }
            if (candidate.SourceAction.ContentHash != basicSwordFirst.ContentHash)
            { error = "The fixed action changed since this frame candidate was read. Reload its source before applying."; return false; }
            MeleeFrameCatalog frames = null;
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var clip = FirstSwordMotionClip(out _);
                var motion = new FixedAttackMotion(MotionDocument(clip));
                if (motion.ContentHash != candidate.SourceMotion.ContentHash) throw new ArgumentException("The fixed motion binding changed since this frame candidate was read.");
                if (clip.sprites != null && clip.sprites.Length != candidate.SourceMotion.Frames.Count) throw new ArgumentException("Attack sprite count does not match its motion frames.");
                candidate.CompileMotion(Player.Weapon.Speed); // Reject unsupported scaled spans before replacing/resetting anything.
                if (meleeFrames != null) frames = CompileMeleeFrames(meleeFrameSource, motionRegistry, appliedFirstMotion, candidate);
                if (resetScenario) simulation.ValidateResetAdmission();
                frameSwordTiming = candidate; // One owner publishes gameplay and presentation together.
                if (frames != null) { meleeFrames = frames; RefreshMotionBindingHash(); }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest();
            error = null; return true;
        }
        public bool TryRestoreSwordFixedTiming(bool resetScenario, out string error)
        {
            if (!Player) { error = "Combat runtime is not initialized."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var frames = meleeFrames == null ? null : CompileMeleeFrames(meleeFrameSource, motionRegistry, appliedFirstMotion, null);
                if (resetScenario) simulation.ValidateResetAdmission();
                frameSwordTiming = null;
                if (frames != null) { meleeFrames = frames; RefreshMotionBindingHash(); }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest();
            error = null; return true;
        }
    }
}
