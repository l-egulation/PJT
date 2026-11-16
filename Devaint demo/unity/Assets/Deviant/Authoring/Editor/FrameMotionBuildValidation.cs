using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deviant.Authoring
{
    // Transport generation is image-free and does not interpret gameplay graphs.
    // Every Player build must pass the same C# compiler as runtime initialization.
    public sealed class FrameMotionBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            var animations = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Deviant/Combat/Resources/CharacterV2/AnimationCatalog.json");
            var combat = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Deviant/Combat/Resources/MeleeCatalog.json");
            if (!animations || !combat) throw new BuildFailedException("Generated combat and animation catalogs are required before a Player build.");
            try
            {
                var repository = new DirectoryInfo(Application.dataPath);
                while (repository != null && !File.Exists(Path.Combine(repository.FullName, FrameMotionSource.ActionPath))) repository = repository.Parent;
                if (repository == null) throw new IOException("Canonical combat sources are required before a Player build.");
                ValidateCurrentSources(repository.FullName, JsonUtility.FromJson<CharacterAnimationCatalog>(animations.text), JsonUtility.FromJson<Catalog>(combat.text));
                FiniteActionTrackSource.ValidateGenerated(repository.FullName, JsonUtility.FromJson<Catalog>(combat.text));
                DirectMeleeSpecialSource.ValidateGenerated(repository.FullName, JsonUtility.FromJson<Catalog>(combat.text));
                ChargedMeleeSource.ValidateGenerated(repository.FullName, JsonUtility.FromJson<Catalog>(combat.text));
                AuthoredSkillSource.ValidateGenerated(repository.FullName, JsonUtility.FromJson<Catalog>(combat.text));
                RangedActionSource.ValidateGenerated(repository.FullName, JsonUtility.FromJson<Catalog>(combat.text));
                MotionRegistrySource.ValidateGenerated(repository.FullName, JsonUtility.FromJson<CharacterAnimationCatalog>(animations.text), JsonUtility.FromJson<Catalog>(combat.text));
                MeleeFrameSource.ValidateGenerated(repository.FullName, JsonUtility.FromJson<CharacterAnimationCatalog>(animations.text), JsonUtility.FromJson<Catalog>(combat.text));
                var generatedAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(animations.text);
                SpecialFrameSource.ValidateGenerated(repository.FullName, generatedAnimations, JsonUtility.FromJson<Catalog>(combat.text), generatedAnimations.specialFrames);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
            { throw new BuildFailedException("Invalid or stale generated sword content: " + exception.Message); }
        }
        public static MeleeFrameDefinition ValidateCurrentSources(string repository, CharacterAnimationCatalog generated, Catalog combat)
        {
            if (generated == null) throw new ArgumentException("Generated animation catalog is required.");
            if (!BasicMeleeCatalog.TryCreate(combat, out var actions, out var error)) throw new ArgumentException(error);
            var action = actions.Find("base_sword", 0);
            if (action == null) throw new ArgumentException("Generated first sword action is required.");
            var sourceAction = FrameMotionSource.ReadAction(repository);
            if (sourceAction.ContentHash != action.ContentHash)
                throw new ArgumentException("Generated first sword action differs from its canonical source. Export the combat catalog before building.");
            var source = FrameMotionSource.Load(repository, out _);
            var fixedGenerated = generated.motionSources == null || generated.motionSources.Length == 0 ? null : new FrameAttackMotionSource(generated.motionSources[0]);
            var binding = Binding(generated);
            float inputSpeed = combat.forms.Single(form => form.id == "base_sword").inputSpeed;
            var compiled = FrameAttackMotionSource.CompileGenerated(generated.motionSources, action, binding, inputSpeed);
            if (source.FixedBinding.ContentHash != binding.ContentHash || source.UsesFrameTime != (fixedGenerated?.UsesFrameTime ?? false) ||
                source.UsesFrameTime && source.Compile(sourceAction, inputSpeed).ContentHash != compiled?.ContentHash)
                throw new ArgumentException("Generated first sword motion differs from its canonical source. Export attack motion before building.");
            return compiled;
        }
        public static MeleeFrameDefinition Validate(CharacterAnimationCatalog generated, Catalog combat)
        {
            if (generated == null) throw new ArgumentException("Generated animation catalog is required.");
            if (!BasicMeleeCatalog.TryCreate(combat, out var actions, out var error)) throw new ArgumentException(error);
            var action = actions.Find("base_sword", 0);
            if (action == null) throw new ArgumentException("Generated first sword action is required.");
            if (generated.motionSources == null || generated.motionSources.Length == 0) return null;
            return FrameAttackMotionSource.CompileGenerated(generated.motionSources, action, Binding(generated), combat.forms.Single(form => form.id == "base_sword").inputSpeed);
        }
        internal static FixedAttackMotion Binding(CharacterAnimationCatalog generated)
        {
            var sets = generated.sets?.Where(set => set != null && set.form == "base_sword").ToArray();
            if (sets == null || sets.Length != 1) throw new ArgumentException("One basic sword art binding is required.");
            var clips = sets[0].clips?.Where(clip => clip != null && clip.name == "attack1").ToArray();
            if (clips == null || clips.Length != 1) throw new ArgumentException("One first sword motion binding is required.");
            var clip = clips[0];
            if (clip.frames == null || clip.seconds == null || clip.stepIds == null || clip.frames.Length != clip.seconds.Length || clip.frames.Length != clip.stepIds.Length)
                throw new ArgumentException("Migrated motion requires matching frame, second and stable step ID lists.");
            return new FixedAttackMotion(new AttackMotionDocument {
                schemaVersion = 1, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FixedAttackMotion.Mode,
                sourceImpact = clip.sourceImpact, fxStart = clip.fxStart,
                steps = Enumerable.Range(0, clip.frames.Length).Select(index => new AttackMotionStep { id = clip.stepIds[index], frame = clip.frames[index], seconds = clip.seconds[index] }).ToArray()
            });
        }
    }
}
