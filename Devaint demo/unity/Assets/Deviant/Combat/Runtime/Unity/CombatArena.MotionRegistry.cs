using System;
using System.Linq;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        MotionSourceRegistry motionRegistry;
        FixedAttackMotion appliedFirstMotion;
        string motionBindingHash;
        public MotionSourceRegistry MotionRegistry => motionRegistry;
        public string MotionBindingHash => motionBindingHash;
        bool InitializeMotionRegistry(out string error)
        {
            try
            {
                // Explicit injected media fixtures have no generated registry.
                // Product loader carries the complete generated source graph.
                if (characterAnimations?.motionRegistry == null || characterAnimations.motionRegistry.IsEmpty) { error = null; return true; }
                motionRegistry = new MotionSourceRegistry(characterAnimations.motionRegistry, catalog);
                ValidateMotionRegistry(motionRegistry);
                characterAnimations.sets = MotionRegistryMedia.Project(characterAnimations, motionRegistry);
                if (!TryGetBasicSwordMotion(out appliedFirstMotion, out var firstError)) throw new ArgumentException(firstError);
                motionBindingHash = motionRegistry.BindingHash(appliedFirstMotion);
                error = null; return true;
            }
            catch (ArgumentException exception) { error = "Invalid generated motion registry: " + exception.Message; return false; }
        }
        internal MotionSourceRegistry RegistryFor(CharacterAnimationCatalog media)
            => media?.motionRegistry == null || media.motionRegistry.IsEmpty ? null : motionRegistry;
        public void ValidateMotionRegistry(MotionSourceRegistry candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (motionRegistry != null && (candidate.Palettes.Count != motionRegistry.Palettes.Count || candidate.Palettes.Any(palette =>
                motionRegistry.Palette(palette.Id) == null || !palette.Frames.SequenceEqual(motionRegistry.Palette(palette.Id).Frames))))
                throw new ArgumentException("Known media palettes differ from the applied supplied media inventory.");
            var supplied = characterAnimations?.sets?.Where(set => set != null).SelectMany(set => (set.clips ?? Array.Empty<CharacterClip>())
                .Where(clip => clip != null && clip.name != null && clip.name.StartsWith("attack", StringComparison.Ordinal)).Select(clip => set.form + "/" + clip.name)).ToArray();
            if (supplied == null || supplied.Length != candidate.Palettes.Count || supplied.Distinct(StringComparer.Ordinal).Count() != supplied.Length || supplied.Any(id => candidate.Palette(id) == null))
                throw new ArgumentException("The complete supplied attack media inventory must match the motion registry.");
            foreach (var palette in candidate.Palettes)
            {
                var set = characterAnimations.sets.Single(value => value != null && value.form == palette.Form);
                var clip = set.clips.Single(value => value != null && value.name == palette.Clip);
                if (clip.frames == null || clip.frames.Any(frame => !palette.Frames.Contains(frame))) throw new ArgumentException("Unknown body frame in supplied media: " + palette.Id);
                if (!clip.Deferred && clip.sprites != null && clip.sprites.Length != clip.frames.Length) throw new ArgumentException("Body sprite bindings do not match supplied frames: " + palette.Id);
                foreach (var frame in palette.Frames)
                    if (!clip.frames.Contains(frame) && (characterAnimations.motionSprites == null || !characterAnimations.motionSprites.ContainsKey(frame)))
                        throw new ArgumentException("Known body media palette is absent or changed: " + palette.Id);
            }
        }
        public bool TryApplyMotionRegistry(MotionRegistryDocument source, bool resetScenario, out string error)
        {
            error = null;
            if (!Player || motionRegistry == null) { error = "The actual generated motion registry must be initialized before applying."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                var candidate = new MotionSourceRegistry(source, catalog);
                if (candidate.Palettes.Count != motionRegistry.Palettes.Count) throw new ArgumentException("Supplied media inventory changed; reimport it separately.");
                foreach (var palette in candidate.Palettes)
                {
                    var previous = motionRegistry.Palette(palette.Id);
                    if (previous == null || !previous.Frames.SequenceEqual(palette.Frames)) throw new ArgumentException("Known frame palette changed; body timing cannot author new art.");
                }
                ValidateMotionRegistry(candidate);
                var sets = MotionRegistryMedia.Project(characterAnimations, candidate);
                var frames = meleeFrames == null ? null : CompileMeleeFrames(meleeFrameSource, candidate, appliedFirstMotion, frameSwordTiming);
                var special = PrepareSpecialMediaReplacement(candidate, appliedFirstMotion, frames);
                var document = candidate.ToDocument();
                string bindingHash = special != null ? special.BindingHash : frames == null ? candidate.BindingHash(appliedFirstMotion) : frames.BindingHash(candidate, appliedFirstMotion);
                if (resetScenario) simulation.ValidateResetAdmission();
                characterAnimations.sets = sets; characterAnimations.motionRegistry = document; motionRegistry = candidate;
                meleeFrames = frames; PublishSpecialMediaReplacement(special); motionBindingHash = bindingHash;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest();
            return true;
        }
    }
    public partial class CombatCast
    {
        MotionActionKey motionActionKey;
        bool motionKeyCaptured;
        internal string MotionRequestedForm { get; private set; }
        internal MotionActionKey CaptureMotionActionKey()
        {
            if (!motionKeyCaptured)
            {
                var form = arena.catalog.forms[weapon.FormIndex];
                MotionRequestedForm = form.id;
                // Launcher art follows the equipped ink, so its basic picks the body clip of that gun.
                bool inkLook = isBasic && MotionSourceRegistry.HasInkAppearance(form.id);
                motionActionKey = new MotionActionKey(Definition.Id, inkLook ? MotionActionKind.Conversion : isBasic ? MotionActionKind.Basic : MotionActionKind.Special,
                    inkLook ? weapon.SpecialIndex : -1);
                motionKeyCaptured = true;
            }
            return motionActionKey;
        }
    }
}
