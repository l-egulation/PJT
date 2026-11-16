using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        // Definition-apply boundary only. Candidate compilation/media checks occur
        // before any live reference changes; Reset callbacks see one coherent set.
        public bool TryApplyMeleeFrameState(IReadOnlyList<BasicMeleeDefinition> basics, IReadOnlyList<DirectMeleeSpecialDefinition> direct,
            FixedAttackMotion firstMotion, MotionSourceRegistry registry, MeleeFrameDefinition firstFrame, MeleeFrameCatalogDocument frameSource,
            bool resetScenario, out string error)
        {
            error = null;
            if (!Player || basicMeleeCatalog == null || directMeleeSpecials == null || registry == null || firstMotion == null)
            { error = "Initialized actual melee/media state and complete candidate context are required."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                if (basics == null || basics.Count != 17 || basics.Any(value => value == null) || basics.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != 17 ||
                    direct == null || direct.Count != DirectMeleeSpecialDefinition.SupportedIds.Count) throw new ArgumentException("The complete unique non-null 17 basic/three direct state is required.");
                var byId = basics.ToDictionary(value => value.Id, StringComparer.Ordinal);
                var first = byId[FixedAttackMotion.FirstSwordAction];
                if (!basicMeleeCatalog.TryReplace(basics, out var nextBasics, out error)) return false;
                var nextDirect = new Dictionary<string, DirectMeleeSpecialDefinition>(StringComparer.Ordinal);
                foreach (var value in direct)
                {
                    if (value == null || !directMeleeSpecials.TryGetValue(value.Id, out var current) || value.SourceVersion != current.SourceVersion || nextDirect.ContainsKey(value.Id))
                        throw new ArgumentException("A complete matching direct policy inventory is required before frame state publication.");
                    nextDirect.Add(value.Id, value);
                }
                ValidateMotionRegistry(registry);
                var sets = MotionRegistryMedia.Project(characterAnimations, registry);
                sets = PrepareFirstMotion(sets, firstMotion, registry);
                if (firstFrame != null && (firstFrame.NumericAction.ContentHash != first.ContentHash || firstFrame.SourceMotion.ContentHash != firstMotion.ContentHash)) throw new ArgumentException("Delegated first frame source differs from its captured numeric/media state.");
                var frames = new MeleeFrameCatalog(frameSource, catalog, registry, firstMotion, firstFrame, basics, direct);
                foreach (var frame in frames.Actions) { frame.CompileForStart(Player.Weapon.Speed); frame.CompileMotion(Player.Weapon.Speed); }
                var special = PrepareSpecialMediaReplacement(registry, firstMotion, frames);
                var meleeDocument = frames.ToSourceDocument(); var mediaDocument = registry.ToDocument();
                string bindingHash = special != null ? special.BindingHash : frames.BindingHash(registry, firstMotion);
                // There is no user callback or engine tick between these writes.
                if (resetScenario) simulation.ValidateResetAdmission();
                basicMeleeCatalog = nextBasics; basicSwordFirst = first; directMeleeSpecials = nextDirect;
                motionRegistry = registry; appliedFirstMotion = firstMotion; frameSwordTiming = firstFrame;
                meleeFrames = frames; meleeFrameSource = meleeDocument;
                characterAnimations.sets = sets; characterAnimations.motionRegistry = mediaDocument; characterAnimations.meleeFrames = meleeDocument;
                PublishSpecialMediaReplacement(special);
                motionBindingHash = bindingHash;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is KeyNotFoundException)
            { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest(); return true;
        }
        CharacterAnimationSet[] PrepareFirstMotion(CharacterAnimationSet[] sets, FixedAttackMotion motion, MotionSourceRegistry registry)
        {
            var old = sets.Single(set => set != null && set.form == "base_sword"); var clip = old.clips.Single(value => value != null && value.name == "attack1");
            var palette = registry.Palette(FixedAttackMotion.FirstSwordId);
            if (palette == null || motion.Frames.Any(frame => !palette.Frames.Contains(frame)) || clip.sprites != null && clip.sprites.Length != clip.frames.Length)
                throw new ArgumentException("Delegated first frames must remain in the current known media palette.");
            var sprites = clip.sprites == null ? null : new Sprite[motion.Frames.Count]; bool same = clip.frames.SequenceEqual(motion.Frames);
            for (int index = 0; index < motion.Frames.Count; index++)
            {
                int source = same ? index : Array.IndexOf(clip.frames, motion.Frames[index]);
                if (source >= 0) { if (sprites != null) sprites[index] = clip.sprites[source]; }
                else if (characterAnimations.TryMotionSprite(motion.Frames[index], out var sprite)) { if (sprites != null) sprites[index] = sprite; }
                else throw new ArgumentException("A delegated first frame has no current media binding.");
            }
            var replacement = new CharacterClip { name = clip.name, frames = motion.Frames.ToArray(), seconds = motion.Seconds.ToArray(), stepIds = motion.StepIds.ToArray(), sourceImpact = motion.SourceImpact, fxStart = motion.FxStart, contact = -1, sprites = sprites };
            var clips = (CharacterClip[])old.clips.Clone(); clips[Array.IndexOf(clips, clip)] = replacement;
            var result = (CharacterAnimationSet[])sets.Clone(); result[Array.IndexOf(result, old)] = new CharacterAnimationSet { form = old.form, clips = clips, effects = old.effects,
                iconUvRect = old.iconUvRect == null ? null : (float[])old.iconUvRect.Clone() }; return result;
        }
    }
}
