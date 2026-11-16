using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    public sealed class DialogueChoice
    {
        public string Id { get; }
        public string TextKey { get; }
        public string NextId { get; }
        public string ConditionId { get; }
        public string CommandId { get; }
        public DialogueChoice(string id, string textKey, string nextId, string conditionId = null, string commandId = null)
        {
            Id = DialogueValues.Id(id); TextKey = DialogueValues.Id(textKey); NextId = DialogueValues.Id(nextId);
            ConditionId = conditionId == null ? null : DialogueValues.Id(conditionId); CommandId = commandId == null ? null : DialogueValues.Id(commandId);
        }
    }
    public sealed class DialogueLine
    {
        public string Id { get; }
        public string SpeakerKey { get; }
        public string TextKey { get; }
        public string NextId { get; }
        public string ConditionId { get; }
        public IReadOnlyList<DialogueChoice> Choices { get; }
        public DialogueLine(string id, string speakerKey, string textKey, string nextId = null, string conditionId = null, DialogueChoice[] choices = null)
        {
            Id = DialogueValues.Id(id); SpeakerKey = DialogueValues.Id(speakerKey); TextKey = DialogueValues.Id(textKey);
            NextId = nextId == null ? null : DialogueValues.Id(nextId); ConditionId = conditionId == null ? null : DialogueValues.Id(conditionId);
            var copy = choices == null ? Array.Empty<DialogueChoice>() : (DialogueChoice[])choices.Clone();
            if (copy.Any(c => c == null) || copy.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length || NextId != null && copy.Length > 0)
                throw new ArgumentException("A line requires unique non-null choices or one advance destination, never both.", nameof(choices));
            Choices = Array.AsReadOnly(copy);
        }
    }
    public sealed class DialogueDefinition
    {
        public string Id { get; }
        public string EntryId { get; }
        public IReadOnlyList<DialogueLine> Lines { get; }
        public IReadOnlyList<string> CommandIds { get; }
        public string ContentHash { get; }
        public DialogueDefinition(string id, string entryId, DialogueLine[] lines)
        {
            Id = DialogueValues.Id(id); EntryId = DialogueValues.Id(entryId);
            if (lines == null || lines.Length == 0 || lines.Any(l => l == null) || lines.Select(l => l.Id).Distinct(StringComparer.Ordinal).Count() != lines.Length)
                throw new ArgumentException("A dialogue requires unique non-null lines.", nameof(lines));
            var byId = lines.ToDictionary(l => l.Id, StringComparer.Ordinal);
            var incoming = lines.ToDictionary(l => l.Id, l => 0, StringComparer.Ordinal);
            foreach (var source in lines) foreach (var destination in Destinations(source))
            {
                if (!byId.ContainsKey(destination)) throw new ArgumentException("Dialogue has a missing line reference.", nameof(lines));
                incoming[destination]++;
            }
            var visited = new HashSet<string>(StringComparer.Ordinal); var pending = new Stack<string>(); pending.Push(EntryId);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!byId.TryGetValue(current, out var line)) throw new ArgumentException("Dialogue entry is missing.", nameof(lines));
                if (visited.Add(current)) foreach (var destination in Destinations(line)) pending.Push(destination);
            }
            if (visited.Count != lines.Length) throw new ArgumentException("All dialogue lines must be reachable from the entry.", nameof(lines));
            var roots = new Queue<string>(incoming.Where(p => p.Value == 0).Select(p => p.Key)); int ordered = 0;
            while (roots.Count > 0)
            {
                var line = byId[roots.Dequeue()]; ordered++;
                foreach (var destination in Destinations(line)) if (--incoming[destination] == 0) roots.Enqueue(destination);
            }
            if (ordered != lines.Length) throw new ArgumentException("Dialogue cycles are not supported.", nameof(lines));
            Lines = Array.AsReadOnly(lines.OrderBy(l => l.Id, StringComparer.Ordinal).ToArray());
            bool branching = Lines.Any(l => l.Choices.Count > 0);
            CommandIds = Array.AsReadOnly(Lines.SelectMany(l => l.Choices).Select(c => c.CommandId).Where(c => c != null).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToArray());
            ContentHash = DialogueValues.Hash(w => {
                // Preserve the published hashes of command-free definitions.
                w.Write(CommandIds.Count > 0 ? 3 : branching ? 2 : 1); w.Write(Id); w.Write(EntryId); w.Write(Lines.Count);
                foreach (var line in Lines)
                {
                    w.Write(line.Id); w.Write(line.SpeakerKey); w.Write(line.TextKey); w.Write(line.NextId ?? ""); w.Write(line.ConditionId ?? "");
                    if (!branching) continue;
                    w.Write(line.Choices.Count);
                    foreach (var choice in line.Choices)
                    {
                        w.Write(choice.Id); w.Write(choice.TextKey); w.Write(choice.NextId); w.Write(choice.ConditionId ?? "");
                        if (CommandIds.Count > 0) w.Write(choice.CommandId ?? "");
                    }
                }
            });
        }
        static IEnumerable<string> Destinations(DialogueLine line) => line.Choices.Count > 0 ? line.Choices.Select(c => c.NextId) : line.NextId == null ? Array.Empty<string>() : new[] { line.NextId };
        public DialogueLine Find(string id) => Lines.FirstOrDefault(l => l.Id == id);
    }
    public sealed class NpcDefinition
    {
        public string Id { get; }
        public DialogueDefinition Dialogue { get; }
        public string ConditionId { get; }
        public string ContentHash { get; }
        public NpcDefinition(string id, DialogueDefinition dialogue, string conditionId = null)
        {
            Id = DialogueValues.Id(id); Dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            ConditionId = conditionId == null ? null : DialogueValues.Id(conditionId);
            ContentHash = DialogueValues.Hash(w => { w.Write(Id); w.Write(Dialogue.ContentHash); w.Write(ConditionId ?? ""); });
        }
    }
    internal static class DialogueValues
    {
        public static string Id(string value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A stable dialogue/NPC/text/condition key is required.");
        public static string Hash(Action<BinaryWriter> write)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) write(writer);
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
