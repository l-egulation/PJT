using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Narrative.Contracts;
using Deviant.UI.Contracts;
using UnityEngine;

namespace Deviant.Integration
{
    // Generated TextAssets carry the original DTO, not a second semantic format.
    public static class DialogueSourceJson
    {
        public const int MaximumFileBytes = 1024 * 1024;
        public static DialogueSourceDocument ReadDocument(string json)
        {
            RequireJson(json); return JsonUtility.FromJson<DialogueSourceDocument>(json);
        }
        public static DialogueReferenceCatalog ReadReferences(string json)
        {
            RequireJson(json); return new DialogueReferenceCatalog(JsonUtility.FromJson<DialogueReferenceCatalogDocument>(json));
        }
        public static CompiledDialogueSource Compile(string sourceJson, string referenceJson)
            => DialogueSourceCompiler.Compile(ReadDocument(sourceJson), ReadReferences(referenceJson));
        public static CompiledDialogueSource Compile(TextAsset source, TextAsset references)
        {
            if (!source || !references) throw new ArgumentException("Explicit dialogue source and reference catalog TextAssets are required.");
            return Compile(source.text, references.text);
        }
        static void RequireJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > MaximumFileBytes)
                throw new ArgumentException("Dialogue JSON is empty or exceeds the allowed file size.");
        }
    }

    public sealed class DialogueSourceText : IDialogueText
    {
        readonly DialogueReferenceCatalog references;
        public DialogueSourceText(DialogueReferenceCatalog references) { this.references = references ?? throw new ArgumentNullException(nameof(references)); }
        public bool TryRead(string key, out string text) => references.TryReadText(key, out text);
    }

    // Explicit composition only. It does not register an NPC or create a target.
    public sealed class DialogueSourceBinding
    {
        public string NpcId { get; }
        public CompiledDialogueSource Source { get; }
        public DialogueSourceBinding(string npcId, CompiledDialogueSource source)
        {
            if (string.IsNullOrWhiteSpace(npcId)) throw new ArgumentException("An explicit source NPC identity is required.", nameof(npcId));
            NpcId = npcId; Source = source ?? throw new ArgumentNullException(nameof(source));
        }
    }

    internal sealed class DialogueSourceTargets : IInteractionTargets
    {
        readonly IInteractionTargets targets;
        readonly Func<DialogueSourceBinding> readBinding;
        DialogueSourceBinding cachedBinding;
        readonly Dictionary<string, NpcDefinition> definitions = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
        public DialogueSourceTargets(IInteractionTargets targets, Func<DialogueSourceBinding> readBinding)
        { this.targets = targets ?? throw new ArgumentNullException(nameof(targets)); this.readBinding = readBinding; }
        public IReadOnlyList<NpcTarget> ReadTargets() => Array.AsReadOnly(targets.ReadTargets().Select(Apply).ToArray());
        public bool TryRead(Guid worldSession, string placementId, out NpcTarget target)
        {
            if (!targets.TryRead(worldSession, placementId, out target)) return false;
            target = Apply(target); return true;
        }
        NpcTarget Apply(NpcTarget target)
        {
            var binding = readBinding();
            if (binding == null || target.Definition.Id != binding.NpcId) return target;
            if (!ReferenceEquals(binding, cachedBinding)) { definitions.Clear(); cachedBinding = binding; }
            string condition = target.Definition.ConditionId ?? "";
            if (!definitions.TryGetValue(condition, out var definition))
            {
                definition = new NpcDefinition(target.Definition.Id, binding.Source.Definition, target.Definition.ConditionId);
                definitions.Add(condition, definition);
            }
            return new NpcTarget(target.WorldSessionId, target.PlacementId, definition, target.Distance, target.InCombat);
        }
    }
}
