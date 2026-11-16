using System;
using System.Linq;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        public bool TryGetBasicSwordMotion(out FixedAttackMotion motion, out string error)
        {
            motion = null;
            try
            {
                var clip = FirstSwordMotionClip(out _);
                if (clip.sprites != null && (clip.frames == null || clip.sprites.Length != clip.frames.Length))
                    throw new ArgumentException("Attack sprite count does not match its motion frames.");
                motion = new FixedAttackMotion(MotionDocument(clip)); error = null; return true;
            }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        public bool TryApplyBasicSwordMotion(AttackMotionDocument candidate, bool resetScenario, out string error)
        {
            if (!Player) { error = "Combat runtime is not initialized."; return false; }
            if (frameSwordTiming != null) { error = "Restore fixed sword timing before applying a separate motion definition."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var motion = new FixedAttackMotion(candidate);
                var clip = FirstSwordMotionClip(out var set);
                var palette = motionRegistry?.Palette(FixedAttackMotion.FirstSwordId);
                if (palette == null ? clip.frames == null || !clip.frames.SequenceEqual(motion.Frames) : motion.Frames.Any(frame => !palette.Frames.Contains(frame)))
                    throw new ArgumentException("Motion frames must reference the known supplied first-sword media palette.");
                if (clip.sprites != null && clip.sprites.Length != clip.frames.Length)
                    throw new ArgumentException("Attack sprite count does not match its motion frames.");
                var captured = clip.sprites == null ? null : new UnityEngine.Sprite[motion.Frames.Count];
                bool unchangedFrames = clip.frames.SequenceEqual(motion.Frames);
                for (int index = 0; index < motion.Frames.Count; index++)
                {
                    int original = unchangedFrames ? index : Array.IndexOf(clip.frames, motion.Frames[index]);
                    if (original >= 0) { if (captured != null) captured[index] = clip.sprites[original]; }
                    else if (characterAnimations.TryMotionSprite(motion.Frames[index], out var sprite)) { if (captured != null) captured[index] = sprite; }
                    else throw new ArgumentException("Known first-sword frame is not bound to the current media palette.");
                }
                var replacement = new CharacterClip {
                    name = clip.name, frames = motion.Frames.ToArray(), seconds = motion.Seconds.ToArray(), stepIds = motion.StepIds.ToArray(),
                    contact = -1, sourceImpact = motion.SourceImpact, fxStart = motion.FxStart, sprites = captured
                };
                var frames = meleeFrames == null ? null : CompileMeleeFrames(meleeFrameSource, motionRegistry, motion, null);
                var special = PrepareSpecialMediaReplacement(motionRegistry, motion, frames);
                var clips = (CharacterClip[])set.clips.Clone(); clips[Array.IndexOf(clips, clip)] = replacement;
                string bindingHash = special != null ? special.BindingHash : frames == null ? motionRegistry?.BindingHash(motion) : frames.BindingHash(motionRegistry, motion);
                if (resetScenario) simulation.ValidateResetAdmission();
                set.clips = clips; // Publish one complete binding. Running captures remain owned by their execution.
                if (frames != null) meleeFrames = frames;
                PublishSpecialMediaReplacement(special);
                appliedFirstMotion = motion; motionBindingHash = bindingHash;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest();
            error = null; return true;
        }
        CharacterClip FirstSwordMotionClip(out CharacterAnimationSet set)
        {
            var sets = characterAnimations?.sets?.Where(s => s != null && s.form == "base_sword").ToArray();
            if (sets == null || sets.Length != 1) throw new ArgumentException("One basic sword art binding is required.");
            set = sets[0]; var clips = set.clips?.Where(c => c != null && c.name == "attack1").ToArray();
            if (clips == null || clips.Length != 1) throw new ArgumentException("One first sword motion binding is required.");
            return clips[0];
        }
        internal static AttackMotionDocument MotionDocument(CharacterClip clip)
        {
            if (clip.frames == null || clip.seconds == null || clip.stepIds == null || clip.frames.Length != clip.seconds.Length || clip.frames.Length != clip.stepIds.Length)
                throw new ArgumentException("Migrated motion requires matching frame, second and stable step ID lists.");
            return new AttackMotionDocument {
                schemaVersion = 1, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FixedAttackMotion.Mode,
                sourceImpact = clip.sourceImpact, fxStart = clip.fxStart,
                steps = Enumerable.Range(0, clip.frames.Length).Select(i => new AttackMotionStep { id = clip.stepIds[i], frame = clip.frames[i], seconds = clip.seconds[i] }).ToArray()
            };
        }
    }
}
