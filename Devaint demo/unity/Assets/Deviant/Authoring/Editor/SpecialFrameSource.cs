using System;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public static class SpecialFrameSource
    {
        public const string RelativePath = ActionSpecialFrameCatalog.SourcePath;
        public const int MaximumFileBytes = 1024 * 1024;
        public static ActionSpecialFrameCatalog Load(string repository, out string fileHash)
        {
            var document = JsonUtility.FromJson<SpecialFrameCatalogDocument>(SourceFiles.Read(Path.Combine(repository, RelativePath), out fileHash, MaximumFileBytes));
            return Compile(repository, document);
        }
        public static ActionSpecialFrameCatalog Compile(string repository, SpecialFrameCatalogDocument document)
        {
            var combat = MotionRegistrySource.ReadCombat(repository); var registry = MotionRegistrySource.Load(repository, out _);
            var first = FrameMotionSource.Load(repository, out _).FixedBinding;
            // Original numeric source compilers do not resolve frame modes, so
            // compiling the mode file never recursively calls its own guards.
            var finite = FiniteActionTrackSource.Load(repository, out _); var authored = AuthoredSkillSource.Load(repository, out _);
            return new ActionSpecialFrameCatalog(document, combat, registry, first, finite, authored);
        }
        public static string Save(string repository, string expectedHash, SpecialFrameCatalogDocument document)
        {
            string path = Path.Combine(repository, RelativePath); Load(repository, out var currentHash);
            if (expectedHash != currentHash) throw new IOException("Special frame source changed outside this window. Reload before saving; no source was overwritten.");
            var compiled = Compile(repository, document); string json = JsonUtility.ToJson(compiled.ToSourceDocument(), true) + "\n";
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new ArgumentException("Special frame source exceeds the allowed file size.");
            return SourceFiles.Save(path, expectedHash, json, MaximumFileBytes);
        }
        public static bool HasFrameOverride(string repository, string id)
        {
            if (!ActionTrackDefinition.Supports(id) && !AuthoredSkillDefinition.Supports(id)) throw new ArgumentException("An existing finite/authored special ID is required.");
            var document = JsonUtility.FromJson<SpecialFrameCatalogDocument>(SourceFiles.Read(Path.Combine(repository, RelativePath), out _, MaximumFileBytes));
            if (document == null || document.schemaVersion != SpecialFrameCatalogDocument.SchemaVersion || document.actions == null || document.actions.Length != ActionTrackDefinition.SupportedIds.Count + AuthoredSkillDefinition.SupportedIds.Count)
                throw new ArgumentException("The complete canonical special source modes are required before timing edits.");
            var matches = document.actions.Where(value => value != null && value.actionId == id && value.kind == MotionActionKind.Special && value.variant == -1).ToArray();
            if (matches.Length != 1 || !Enum.IsDefined(typeof(MeleeTimingMode), matches[0].mode)) throw new ArgumentException("Missing, duplicate or invalid canonical special frame mode: " + id);
            return matches[0].mode == MeleeTimingMode.FrameTime;
        }
        public static void ValidateGenerated(string repository, CharacterAnimationCatalog generated, Catalog combat, SpecialFrameCatalogDocument generatedSource)
        {
            var canonical = Load(repository, out _);
            if (generated?.motionRegistry == null || generatedSource == null) throw new ArgumentException("The generated complete special frame source and shared media registry are required.");
            var registry = new MotionSourceRegistry(generated.motionRegistry, combat);
            var first = FrameMotionBuildValidation.Binding(generated);
            var finite = combat.forms.SelectMany(form => form.specials).Where(skill => ActionTrackDefinition.Supports(skill.id)).Select(skill => new ActionTrackDefinition(skill)).ToArray();
            var authored = combat.forms.SelectMany(form => form.specials.Select(skill => (form: form.id, skill)))
                .Where(value => AuthoredSkillDefinition.Supports(value.skill.id)).Select(value => new AuthoredSkillDefinition(value.form, value.skill)).ToArray();
            var runtime = new ActionSpecialFrameCatalog(generatedSource, combat, registry, first, finite, authored);
            if (canonical.ContentHash != runtime.ContentHash) throw new ArgumentException("Generated special frame policy differs from current canonical numeric/media/mode sources. Export before building.");
        }
    }
}
