using System;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using UnityEngine;

namespace Deviant.Integration
{
    public static class SequenceSourceJson
    {
        public const int MaximumFileBytes = 1024 * 1024;
        public static SequenceSourceDocument ReadDocument(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > MaximumFileBytes)
                throw new ArgumentException("Sequence JSON is empty or exceeds the allowed file size.");
            return JsonUtility.FromJson<SequenceSourceDocument>(json);
        }
        public static CompiledSequenceSource Compile(string json) => SequenceSourceCompiler.Compile(ReadDocument(json));
        public static CompiledSequenceSource Compile(TextAsset source)
        {
            if (!source) throw new ArgumentException("An explicit sequence source TextAsset is required.", nameof(source));
            return Compile(source.text);
        }
    }
    // Binding ports/policies are composition-owned, never executable source data.
    public sealed class SequenceSourceBinding
    {
        public CompiledSequenceSource Source { get; }
        public ISequenceBindings Bindings { get; }
        public IControlRequests Controls { get; }
        public SequenceControlPolicy Policy { get; }
        public ISequenceClock Clock { get; }
        public SequenceSourceBinding(CompiledSequenceSource source, ISequenceBindings bindings, IControlRequests controls, SequenceControlPolicy policy, ISequenceClock clock)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source)); Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            Controls = controls ?? throw new ArgumentNullException(nameof(controls)); Policy = policy ?? throw new ArgumentNullException(nameof(policy)); Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }
        internal SequenceSourceBinding WithSource(CompiledSequenceSource source) => new SequenceSourceBinding(source, Bindings, Controls, Policy, Clock);
    }
}
