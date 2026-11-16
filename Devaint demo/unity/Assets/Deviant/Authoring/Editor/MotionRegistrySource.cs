using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public static class MotionRegistrySource
    {
        public const string RelativePath = "data/combat/presentation/action_motion_registry.json";
        public const string CombatPath = "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json";
        public const int MaximumFileBytes = 1024 * 1024;
        public static Catalog ReadCombat(string repository) => JsonUtility.FromJson<Catalog>(SourceFiles.Read(Path.Combine(repository, CombatPath), out _, MaximumFileBytes));
        public static MotionSourceRegistry Load(string repository, out string fileHash)
            => new MotionSourceRegistry(JsonUtility.FromJson<MotionRegistryDocument>(SourceFiles.Read(Path.Combine(repository, RelativePath), out fileHash, MaximumFileBytes)), ReadCombat(repository));
        public static string Save(string repository, string expectedHash, MotionRegistryDocument document)
        {
            var original = Load(repository, out var currentHash);
            if (currentHash != expectedHash) throw new IOException("Motion registry changed outside this window. Reload before saving; no source was overwritten.");
            var candidate = new MotionSourceRegistry(document, ReadCombat(repository));
            if (original.Palettes.Count != candidate.Palettes.Count || original.Palettes.Any(palette => candidate.Palette(palette.Id) == null || !palette.Frames.SequenceEqual(candidate.Palette(palette.Id).Frames)))
                throw new ArgumentException("Supplied frame palettes are read-only. Edit body steps using only their existing known frame references.");
            return SourceFiles.Save(Path.Combine(repository, RelativePath), expectedHash, Serialize(candidate), MaximumFileBytes);
        }
        public static string Serialize(MotionSourceRegistry registry) => JsonUtility.ToJson((registry ?? throw new ArgumentNullException(nameof(registry))).ToDocument(), true) + "\n";
        public static MotionSourceRegistry ValidateGenerated(string repository, CharacterAnimationCatalog media, Catalog combat)
        {
            var canonical = Load(repository, out _);
            if (media?.motionRegistry == null) throw new ArgumentException("Generated animation catalog is missing the complete body motion registry.");
            var generated = new MotionSourceRegistry(media.motionRegistry, combat);
            if (canonical.ContentHash != generated.ContentHash) throw new ArgumentException("Generated body motion registry differs from its canonical source. Export attack motion before building.");
            var supplied = media.sets?.Where(set => set != null).SelectMany(set => (set.clips ?? Array.Empty<CharacterClip>())
                .Where(clip => clip?.name != null && clip.name.StartsWith("attack", StringComparison.Ordinal)).Select(clip => (Set: set, Clip: clip))).ToArray();
            if (supplied == null || supplied.Length != generated.Palettes.Count || supplied.Select(value => value.Set.form + "/" + value.Clip.name).Distinct(StringComparer.Ordinal).Count() != supplied.Length)
                throw new ArgumentException("Generated attack media inventory is incomplete or duplicated.");
            foreach (var value in supplied)
            {
                var palette = generated.Palette(value.Set.form + "/" + value.Clip.name);
                if (palette == null || value.Clip.frames == null || value.Clip.frames.Any(frame => !palette.Frames.Contains(frame)))
                    throw new ArgumentException("Generated body frame is outside the known media palette.");
                var motion = generated.Motion(palette.Id);
                if (!motion.IsDelegated && (value.Clip.stepIds == null || !value.Clip.stepIds.SequenceEqual(motion.StepIds) || !value.Clip.frames.SequenceEqual(motion.Frames) ||
                    value.Clip.seconds == null || !value.Clip.seconds.SequenceEqual(motion.Seconds) || value.Clip.sourceImpact != motion.SourceImpact || value.Clip.fxStart != motion.FxStart || value.Clip.contact != -1))
                    throw new ArgumentException("Generated body projection differs from the compiled registry. Export attack motion.");
            }
            return generated;
        }
    }
}
