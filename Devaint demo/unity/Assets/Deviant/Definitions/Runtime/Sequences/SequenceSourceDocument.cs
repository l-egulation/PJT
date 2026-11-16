using System;
using System.Linq;

namespace Deviant.Definitions
{
    [Serializable]
    public sealed class SequenceSourceDocument
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion;
        public string id;
        public string[] requiredAnchors;
        public SequenceSourceStepDocument[] steps;
        public bool hasSkip;
        public SequenceSourceSkipDocument skip;
    }
    [Serializable]
    public class SequenceSourceFieldsDocument
    {
        public string id, kind, bindingId;
        public double duration, timeout;
    }
    [Serializable]
    public sealed class SequenceSourceLeafDocument : SequenceSourceFieldsDocument
    {
        // A bounded rejection sentinel catches unsupported nested child arrays
        // before JsonUtility can silently discard their contents.
        public SequenceSourceNestedDocument[] children;
    }
    [Serializable]
    public sealed class SequenceSourceNestedDocument
    {
        public string kind;
    }
    [Serializable]
    public sealed class SequenceSourceStepDocument : SequenceSourceFieldsDocument
    {
        public SequenceSourceLeafDocument[] children;
    }
    [Serializable]
    public sealed class SequenceSourceSkipDocument
    {
        public string endpointId;
        public double timeout;
    }
    public sealed class CompiledSequenceSource
    {
        public SequenceDefinition Definition { get; }
        public string ContentHash => Definition.ContentHash;
        internal CompiledSequenceSource(SequenceDefinition definition) { Definition = definition; }
        public SequenceSourceDocument ToDocument() => new SequenceSourceDocument {
            schemaVersion = SequenceSourceDocument.CurrentSchemaVersion, id = Definition.Id, requiredAnchors = Definition.RequiredAnchors.ToArray(),
            steps = Definition.Steps.Select(ToDocument).ToArray(),
            hasSkip = Definition.Skip != null,
            skip = Definition.Skip == null ? null : new SequenceSourceSkipDocument { endpointId = Definition.Skip.EndpointId, timeout = Definition.Skip.Timeout }
        };
        static SequenceSourceStepDocument ToDocument(SequenceStep step) => new SequenceSourceStepDocument {
            id = step.Id, kind = step.Kind.ToString(), bindingId = step.BindingId, duration = step.Duration, timeout = step.Timeout ?? 0,
            children = step.Children.Select(ToLeafDocument).ToArray()
        };
        static SequenceSourceLeafDocument ToLeafDocument(SequenceStep step) => new SequenceSourceLeafDocument {
            id = step.Id, kind = step.Kind.ToString(), bindingId = step.BindingId, duration = step.Duration, timeout = step.Timeout ?? 0
        };
    }
    public static class SequenceSourceCompiler
    {
        public static CompiledSequenceSource Compile(SequenceSourceDocument document)
        {
            if (document == null || document.schemaVersion != SequenceSourceDocument.CurrentSchemaVersion)
                throw new ArgumentException("Unsupported or missing sequence source schema.", nameof(document));
            if (document.steps == null) throw new ArgumentException("An explicit sequence step array is required.", nameof(document));
            if (!document.hasSkip && document.skip != null && (!string.IsNullOrEmpty(document.skip.endpointId) || document.skip.timeout != 0))
                throw new ArgumentException("A skip contract must be explicitly enabled.", nameof(document));
            if (document.hasSkip && document.skip == null) throw new ArgumentException("An enabled skip contract requires its endpoint and timeout.", nameof(document));
            var skip = !document.hasSkip ? null : new SequenceSkipDefinition(document.skip.endpointId, document.skip.timeout);
            // These existing factories and constructor alone own step/graph/time/hash semantics.
            var definition = new SequenceDefinition(document.id, document.steps.Select(CompileStep).ToArray(), document.requiredAnchors, skip);
            return new CompiledSequenceSource(definition);
        }
        static SequenceStep CompileStep(SequenceSourceStepDocument step)
        {
            if (step == null) throw new ArgumentException("A sequence source step is required.");
            if (step.kind == nameof(SequenceStepKind.ParallelAll))
            {
                RequireUnused(step, true, true, true);
                return SequenceStep.ParallelAll(step.id, step.children == null ? null : step.children.Select(CompileLeaf).ToArray());
            }
            if (step.children != null && step.children.Length != 0) throw new ArgumentException("Only ParallelAll can have child steps.");
            return CompileLeaf(step);
        }
        static SequenceStep CompileLeaf(SequenceSourceFieldsDocument step)
        {
            if (step == null || !Enum.TryParse(step.kind, false, out SequenceStepKind kind) || kind.ToString() != step.kind)
                throw new ArgumentException("A sequence source leaf requires an exact supported kind name.");
            if (kind == SequenceStepKind.ParallelAll) throw new ArgumentException("Nested ParallelAll source groups are unsupported by the existing semantic definition.");
            if (step is SequenceSourceLeafDocument leaf && leaf.children != null && leaf.children.Length != 0)
                throw new ArgumentException("Only top-level ParallelAll steps may contain child steps.");
            if (kind == SequenceStepKind.Wait) { RequireUnused(step, true, false, true); return SequenceStep.Wait(step.id, step.duration); }
            if (kind == SequenceStepKind.Camera) { RequireUnused(step, false, false, true); return SequenceStep.Camera(step.id, step.bindingId, step.duration); }
            RequireUnused(step, false, true, false);
            switch (kind)
            {
                case SequenceStepKind.Dialogue: return SequenceStep.Dialogue(step.id, step.bindingId, step.timeout);
                case SequenceStepKind.Cue: return SequenceStep.Cue(step.id, step.bindingId, step.timeout);
                case SequenceStepKind.Fade: return SequenceStep.Fade(step.id, step.bindingId, step.timeout);
                case SequenceStepKind.StageMove: return SequenceStep.StageMove(step.id, step.bindingId, step.timeout);
                default: throw new ArgumentException("Unsupported sequence step kind.");
            }
        }
        static void RequireUnused(SequenceSourceFieldsDocument step, bool binding, bool duration, bool timeout)
        {
            if (binding && !string.IsNullOrEmpty(step.bindingId) || duration && step.duration != 0 || timeout && step.timeout != 0)
                throw new ArgumentException("Sequence source contains fields that do not belong to its step kind: " + step.id);
        }
    }
}
