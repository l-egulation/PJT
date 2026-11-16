using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Deviant.CombatLab.Contracts
{
    public sealed class ExperimentCodeModule
    {
        public string Name { get; }
        public Guid ModuleVersionId { get; }
        public ExperimentCodeModule(string name, Guid moduleVersionId)
        {
            Name = ExperimentHash.Key(name);
            if (moduleVersionId == Guid.Empty) throw new ArgumentException("A real loaded Editor module MVID is required.", nameof(moduleVersionId));
            ModuleVersionId = moduleVersionId;
        }
        internal void Write(BinaryWriter writer) { writer.Write(Name); writer.Write(ModuleVersionId.ToByteArray()); }
    }

    public sealed class ExperimentQuerySettings
    {
        public bool QueriesHitTriggers { get; }
        public bool QueriesStartInColliders { get; }
        public bool AutoSyncTransforms { get; }
        public float DefaultContactOffset { get; }
        public string ContentHash { get; }
        public ExperimentQuerySettings(bool queriesHitTriggers, bool queriesStartInColliders, bool autoSyncTransforms, float defaultContactOffset)
        {
            QueriesHitTriggers = queriesHitTriggers; QueriesStartInColliders = queriesStartInColliders; AutoSyncTransforms = autoSyncTransforms;
            DefaultContactOffset = ExperimentHash.Positive(defaultContactOffset);
            ContentHash = ExperimentHash.Compute(writer => {
                writer.Write("lab-query-settings-v1"); writer.Write(QueriesHitTriggers); writer.Write(QueriesStartInColliders);
                writer.Write(AutoSyncTransforms); writer.Write(DefaultContactOffset);
            });
        }
    }

    // Initial scope is loaded managed Editor code plus the actual query inputs.
    // This is provenance matching, never a cross-platform determinism claim or
    // a Player/IL2CPP build identity fabricated from missing metadata.
    public sealed class ExperimentRuntimeProvenance
    {
        public const string Kind = "lab-editor-code-and-queries-v1";
        public string UnityVersion { get; }
        public string Platform { get; }
        public string ProcessorType { get; }
        public int PointerBits { get; }
        public IReadOnlyList<ExperimentCodeModule> Modules { get; }
        public ExperimentQuerySettings Queries { get; }
        public string CodeHash { get; }
        public string ContentHash { get; }
        public ExperimentRuntimeProvenance(string unityVersion, string platform, string processorType, int pointerBits,
            IEnumerable<ExperimentCodeModule> modules, ExperimentQuerySettings queries)
        {
            UnityVersion = ExperimentHash.Key(unityVersion); Platform = ExperimentHash.Key(platform); ProcessorType = ExperimentHash.Key(processorType);
            if (pointerBits != 32 && pointerBits != 64) throw new ArgumentOutOfRangeException(nameof(pointerBits));
            PointerBits = pointerBits; Queries = queries ?? throw new ArgumentNullException(nameof(queries));
            var copy = modules?.ToArray();
            if (copy == null || copy.Length == 0 || copy.Any(module => module == null) || copy.Select(module => module.Name).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("A nonempty uniquely named loaded module vector is required.", nameof(modules));
            Array.Sort(copy, (first, second) => StringComparer.Ordinal.Compare(first.Name, second.Name));
            Modules = Array.AsReadOnly(copy);
            CodeHash = ExperimentHash.Compute(writer => {
                writer.Write("lab-editor-code-v1"); writer.Write(copy.Length); foreach (var module in copy) module.Write(writer);
            });
            ContentHash = ExperimentHash.Compute(writer => {
                writer.Write(Kind); writer.Write(UnityVersion); writer.Write(Platform); writer.Write(ProcessorType); writer.Write(PointerBits);
                writer.Write(CodeHash); writer.Write(Queries.ContentHash);
            });
        }
        public bool Matches(ExperimentRuntimeProvenance current) => current != null && ContentHash == current.ContentHash;
    }
}
