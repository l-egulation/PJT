using System;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public static class MeleeFrameSource
    {
        public const string RelativePath = MeleeFrameCatalog.SourcePath;
        public const int MaximumFileBytes = 1024 * 1024;
        public static MeleeFrameCatalog Load(string repository, out string fileHash)
        {
            var document = JsonUtility.FromJson<MeleeFrameCatalogDocument>(SourceFiles.Read(Path.Combine(repository, RelativePath), out fileHash, MaximumFileBytes));
            return Compile(repository, document);
        }
        public static MeleeFrameCatalog Compile(string repository, MeleeFrameCatalogDocument document)
        {
            var combat = MotionRegistrySource.ReadCombat(repository); var registry = MotionRegistrySource.Load(repository, out _);
            if (!BasicMeleeCatalog.TryCreate(combat, out var basics, out var error)) throw new ArgumentException(error);
            var firstSource = FrameMotionSource.Load(repository, out _); var first = FrameMotionSource.ReadAction(repository);
            var numeric = basics.Actions.Select(value => value.Id == FixedAttackMotion.FirstSwordAction ? first : value);
            var direct = DirectMeleeSpecialSource.Load(repository, out _);
            return new MeleeFrameCatalog(document, combat, registry, firstSource.FixedBinding,
                firstSource.UsesFrameTime ? firstSource.Compile(first, FrameMotionSource.ReadInputSpeed(repository)) : null, numeric, direct);
        }
        public static string Save(string repository, string expectedHash, MeleeFrameCatalogDocument candidate)
        {
            string path = Path.Combine(repository, RelativePath); var current = Load(repository, out var actualHash);
            if (actualHash != expectedHash) throw new IOException("Melee frame source changed outside this window. Reload before saving; no file was overwritten.");
            var compiled = Compile(repository, candidate);
            string json = JsonUtility.ToJson(compiled.ToSourceDocument(), true) + "\n";
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new ArgumentException("Melee frame source exceeds the allowed file size.");
            return SourceFiles.Save(path, expectedHash, json, MaximumFileBytes);
        }
        public static void ValidateGenerated(string repository, CharacterAnimationCatalog generated, Catalog combat)
        {
            var canonical = Load(repository, out _);
            if (generated?.meleeFrames == null || generated.motionRegistry == null) throw new ArgumentException("Generated complete melee frame sources are required.");
            if (!BasicMeleeCatalog.TryCreate(combat, out var basics, out var error)) throw new ArgumentException(error);
            var direct = combat.forms.SelectMany(form => form.specials.Select(skill => (form: form.id, skill)))
                .Where(item => DirectMeleeSpecialDefinition.SupportedIds.Contains(item.skill.id)).Select(item => {
                    if (!DirectMeleeSpecialDefinition.TryCreate(item.form, item.skill, out var value, out var failure)) throw new ArgumentException(failure); return value;
                }).ToArray();
            var binding = FrameMotionBuildValidation.Binding(generated);
            var registry = new MotionSourceRegistry(generated.motionRegistry, combat); var first = basics.Find(FixedAttackMotion.FirstSwordAction);
            var firstFrame = FrameAttackMotionSource.CompileGenerated(generated.motionSources, first, binding, combat.forms.Single(form => form.id == "base_sword").inputSpeed);
            var runtime = new MeleeFrameCatalog(generated.meleeFrames, combat, registry, binding, firstFrame, basics.Actions, direct);
            if (runtime.ContentHash != canonical.ContentHash) throw new ArgumentException("Generated melee frame sources differ from current canonical action/media sources. Export before building.");
        }
        public static bool HasFrameOverride(string repository, string actionId)
        {
            if (actionId == FixedAttackMotion.FirstSwordAction) return FrameMotionSource.Load(repository, out _).UsesFrameTime;
            var document = JsonUtility.FromJson<MeleeFrameCatalogDocument>(SourceFiles.Read(Path.Combine(repository, RelativePath), out _, MaximumFileBytes));
            if (document == null || document.schemaVersion != MeleeFrameCatalogDocument.SchemaVersion || document.actions == null) throw new ArgumentException("Complete canonical frame source modes are required for numeric timing edits.");
            var matches = document.actions.Where(action => action != null && action.actionId == actionId).ToArray();
            if (matches.Length != 1) throw new ArgumentException("Missing or duplicate canonical melee frame source: " + actionId);
            return matches[0].mode == MeleeTimingMode.FrameTime;
        }
    }
}
