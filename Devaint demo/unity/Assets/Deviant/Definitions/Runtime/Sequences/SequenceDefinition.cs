using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    public enum SequenceStepKind { Dialogue, Cue, Camera, Wait, ParallelAll, Fade, StageMove }

    public sealed class SequenceStep
    {
        public string Id { get; }
        public SequenceStepKind Kind { get; }
        public string BindingId { get; }
        public double Duration { get; }
        public double? Timeout { get; }
        public IReadOnlyList<SequenceStep> Children { get; }
        SequenceStep(string id, SequenceStepKind kind, string bindingId, double duration, double? timeout, SequenceStep[] children = null)
        {
            Id = SequenceValues.Id(id); Kind = kind; BindingId = bindingId;
            Duration = duration; Timeout = timeout;
            Children = Array.AsReadOnly(children == null ? Array.Empty<SequenceStep>() : (SequenceStep[])children.Clone());
        }
        public static SequenceStep Dialogue(string id, string bindingId, double timeout) => External(id, SequenceStepKind.Dialogue, bindingId, timeout);
        public static SequenceStep Cue(string id, string bindingId, double timeout) => External(id, SequenceStepKind.Cue, bindingId, timeout);
        public static SequenceStep Fade(string id, string bindingId, double timeout) => External(id, SequenceStepKind.Fade, bindingId, timeout);
        public static SequenceStep StageMove(string id, string bindingId, double timeout) => External(id, SequenceStepKind.StageMove, bindingId, timeout);
        static SequenceStep External(string id, SequenceStepKind kind, string bindingId, double timeout)
            => new SequenceStep(id, kind, SequenceValues.Id(bindingId), 0, SequenceValues.Positive(timeout));
        public static SequenceStep Camera(string id, string bindingId, double duration)
            => new SequenceStep(id, SequenceStepKind.Camera, SequenceValues.Id(bindingId), SequenceValues.Positive(duration), null);
        public static SequenceStep Wait(string id, double duration)
            => new SequenceStep(id, SequenceStepKind.Wait, null, SequenceValues.Positive(duration), null);
        public static SequenceStep ParallelAll(string id, params SequenceStep[] children)
        {
            SequenceValues.Id(id);
            if (children == null || children.Length == 0 || children.Any(c => c == null || c.Kind == SequenceStepKind.ParallelAll || c.Id == id) ||
                children.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != children.Length)
                throw new ArgumentException("A parallel group requires unique leaf steps; empty or nested groups are unsupported.", nameof(children));
            return new SequenceStep(id, SequenceStepKind.ParallelAll, null, 0, null, children);
        }
    }

    public sealed class SequenceSkipDefinition
    {
        public string EndpointId { get; }
        public double Timeout { get; }
        public SequenceSkipDefinition(string endpointId, double timeout)
        { EndpointId = SequenceValues.Id(endpointId); Timeout = SequenceValues.Positive(timeout); }
    }

    // Sequential steps with explicit flat wait-all groups. These keys refer to
    // bindings, never scene object names or executable code.
    public sealed class SequenceDefinition
    {
        public string Id { get; }
        public IReadOnlyList<SequenceStep> Steps { get; }
        public IReadOnlyList<SequenceStep> Leaves { get; }
        public IReadOnlyList<string> RequiredAnchors { get; }
        public SequenceSkipDefinition Skip { get; }
        public string ContentHash { get; }
        public SequenceDefinition(string id, SequenceStep[] steps, string[] requiredAnchors, SequenceSkipDefinition skip = null)
        {
            Id = SequenceValues.Id(id);
            if (steps == null || steps.Length == 0 || steps.Any(s => s == null) || steps.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != steps.Length)
                throw new ArgumentException("A sequence requires unique non-null steps.", nameof(steps));
            if (requiredAnchors == null || requiredAnchors.Any(string.IsNullOrWhiteSpace) || requiredAnchors.Distinct(StringComparer.Ordinal).Count() != requiredAnchors.Length)
                throw new ArgumentException("Required anchors must have unique stable IDs.", nameof(requiredAnchors));
            var allIds = steps.SelectMany(s => new[] { s.Id }.Concat(s.Children.Select(c => c.Id))).ToArray();
            if (allIds.Distinct(StringComparer.Ordinal).Count() != allIds.Length)
                throw new ArgumentException("Step and parallel child IDs must be unique across the sequence.", nameof(steps));
            Steps = Array.AsReadOnly((SequenceStep[])steps.Clone());
            Leaves = Array.AsReadOnly(steps.SelectMany(s => s.Kind == SequenceStepKind.ParallelAll ? s.Children : new[] { s }).ToArray());
            RequiredAnchors = Array.AsReadOnly(requiredAnchors.OrderBy(a => a, StringComparer.Ordinal).ToArray()); Skip = skip;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(Id); writer.Write(Steps.Count);
                    foreach (var step in Steps) WriteStep(writer, step);
                    writer.Write(RequiredAnchors.Count); foreach (var anchor in RequiredAnchors) writer.Write(anchor);
                    writer.Write(Skip != null); if (Skip != null) { writer.Write(Skip.EndpointId); writer.Write(Skip.Timeout); }
                }
                using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static void WriteStep(BinaryWriter writer, SequenceStep step)
        {
            writer.Write(step.Id); writer.Write((int)step.Kind); writer.Write(step.BindingId ?? "");
            writer.Write(step.Duration); writer.Write(step.Timeout.HasValue); if (step.Timeout.HasValue) writer.Write(step.Timeout.Value);
            // Ordinary step bytes remain unchanged, preserving existing hashes.
            if (step.Kind != SequenceStepKind.ParallelAll) return;
            writer.Write(step.Children.Count); foreach (var child in step.Children) WriteStep(writer, child);
        }
    }
    internal static class SequenceValues
    {
        public static string Id(string value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A stable sequence/step/binding/anchor key is required.");
        public static double Positive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
