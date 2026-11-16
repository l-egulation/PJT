using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Definitions
{
    [Serializable]
    public sealed class DialogueSourceDocument
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion;
        public string id, entryId;
        public DialogueSourceLineDocument[] lines;
    }

    [Serializable]
    public sealed class DialogueSourceLineDocument
    {
        public string id, speakerKey, textKey, nextId, conditionId;
        public DialogueSourceChoiceDocument[] choices;
    }

    [Serializable]
    public sealed class DialogueSourceChoiceDocument
    {
        public string id, textKey, nextId, conditionId, commandId;
    }

    public enum DialogueTextKind { Speaker, Line, Choice }

    [Serializable]
    public sealed class DialogueReferenceCatalogDocument
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion;
        public DialogueTextDocument[] texts;
        public string[] conditionIds, commandIds;
    }

    [Serializable]
    public sealed class DialogueTextDocument
    {
        public string key, kind, text;
    }

    public sealed class DialogueTextEntry
    {
        public string Key { get; }
        public DialogueTextKind Kind { get; }
        public string Text { get; }
        public DialogueTextEntry(string key, DialogueTextKind kind, string text)
        {
            Key = DialogueValues.Id(key);
            if (!Enum.IsDefined(typeof(DialogueTextKind), kind)) throw new ArgumentException("Unknown dialogue text kind.", nameof(kind));
            Kind = kind; Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }

    // An explicit, immutable snapshot of the text and domain reference catalog.
    // Merely listing a command here never registers or executes a domain service.
    public sealed class DialogueReferenceCatalog
    {
        readonly Dictionary<string, DialogueTextEntry> byKey;
        readonly HashSet<string> conditions, commands;
        public IReadOnlyList<DialogueTextEntry> Texts { get; }
        public IReadOnlyList<string> ConditionIds { get; }
        public IReadOnlyList<string> CommandIds { get; }
        public string ContentHash { get; }
        public DialogueReferenceCatalog(DialogueReferenceCatalogDocument document)
        {
            if (document == null || document.schemaVersion != DialogueReferenceCatalogDocument.CurrentSchemaVersion)
                throw new ArgumentException("Unsupported or missing dialogue reference catalog schema.", nameof(document));
            if (document.texts == null) throw new ArgumentException("A dialogue reference catalog requires an explicit text array.", nameof(document));
            byKey = new Dictionary<string, DialogueTextEntry>(StringComparer.Ordinal);
            foreach (var source in document.texts)
            {
                if (source == null || !Enum.TryParse(source.kind, false, out DialogueTextKind kind) || kind.ToString() != source.kind)
                    throw new ArgumentException("Text references require the exact kind Speaker, Line or Choice.", nameof(document));
                var entry = new DialogueTextEntry(source.key, kind, source.text);
                if (byKey.ContainsKey(entry.Key)) throw new ArgumentException("Dialogue text keys must be unique.", nameof(document));
                byKey.Add(entry.Key, entry);
            }
            conditions = ReadIds(document.conditionIds, "condition"); commands = ReadIds(document.commandIds, "command");
            Texts = Array.AsReadOnly(byKey.Values.OrderBy(t => t.Key, StringComparer.Ordinal).ToArray());
            ConditionIds = Array.AsReadOnly(conditions.OrderBy(id => id, StringComparer.Ordinal).ToArray());
            CommandIds = Array.AsReadOnly(commands.OrderBy(id => id, StringComparer.Ordinal).ToArray());
            ContentHash = DialogueValues.Hash(writer => {
                writer.Write(DialogueReferenceCatalogDocument.CurrentSchemaVersion); writer.Write(Texts.Count);
                foreach (var text in Texts) { writer.Write(text.Key); writer.Write((int)text.Kind); writer.Write(text.Text); }
                writer.Write(ConditionIds.Count); foreach (var id in ConditionIds) writer.Write(id);
                writer.Write(CommandIds.Count); foreach (var id in CommandIds) writer.Write(id);
            });
        }
        static HashSet<string> ReadIds(string[] ids, string kind)
        {
            if (ids == null) throw new ArgumentException("The reference catalog requires an explicit " + kind + " ID array.");
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids) if (!result.Add(DialogueValues.Id(id))) throw new ArgumentException("Duplicate dialogue " + kind + " reference: " + id);
            return result;
        }
        public bool HasText(string key, DialogueTextKind kind) => key != null && byKey.TryGetValue(key, out var entry) && entry.Kind == kind;
        public bool TryReadText(string key, out string text)
        {
            if (key != null && byKey.TryGetValue(key, out var entry)) { text = entry.Text; return true; }
            text = null; return false;
        }
        public bool HasCondition(string id) => id != null && conditions.Contains(id);
        public bool HasCommand(string id) => id != null && commands.Contains(id);
        public DialogueReferenceCatalogDocument ToDocument() => new DialogueReferenceCatalogDocument {
            schemaVersion = DialogueReferenceCatalogDocument.CurrentSchemaVersion,
            texts = Texts.Select(t => new DialogueTextDocument { key = t.Key, kind = t.Kind.ToString(), text = t.Text }).ToArray(),
            conditionIds = ConditionIds.ToArray(), commandIds = CommandIds.ToArray()
        };
    }

    public sealed class CompiledDialogueSource
    {
        public DialogueDefinition Definition { get; }
        public DialogueReferenceCatalog References { get; }
        public string ContentHash { get; }
        internal CompiledDialogueSource(DialogueDefinition definition, DialogueReferenceCatalog references)
        {
            Definition = definition; References = references;
            ContentHash = DialogueValues.Hash(writer => { writer.Write(definition.ContentHash); writer.Write(references.ContentHash); });
        }
        public DialogueSourceDocument ToDocument() => new DialogueSourceDocument {
            schemaVersion = DialogueSourceDocument.CurrentSchemaVersion, id = Definition.Id, entryId = Definition.EntryId,
            lines = Definition.Lines.Select(line => new DialogueSourceLineDocument {
                id = line.Id, speakerKey = line.SpeakerKey, textKey = line.TextKey, nextId = line.NextId, conditionId = line.ConditionId,
                choices = line.Choices.Select(choice => new DialogueSourceChoiceDocument {
                    id = choice.Id, textKey = choice.TextKey, nextId = choice.NextId, conditionId = choice.ConditionId, commandId = choice.CommandId
                }).ToArray()
            }).ToArray()
        };
    }

    public static class DialogueSourceCompiler
    {
        public static CompiledDialogueSource Compile(DialogueSourceDocument document, DialogueReferenceCatalog references)
        {
            if (document == null || document.schemaVersion != DialogueSourceDocument.CurrentSchemaVersion)
                throw new ArgumentException("Unsupported or missing dialogue source schema.", nameof(document));
            if (references == null) throw new ArgumentNullException(nameof(references));
            if (document.lines == null) throw new ArgumentException("Dialogue source lines are required.", nameof(document));
            // Existing semantic constructors remain the sole graph/identity/hash authority.
            var definition = new DialogueDefinition(document.id, document.entryId, document.lines.Select(line => {
                if (line == null) throw new ArgumentException("Dialogue source contains a null line.", nameof(document));
                var choices = line.choices == null ? null : line.choices.Select(choice => {
                    if (choice == null) throw new ArgumentException("Dialogue source contains a null choice.", nameof(document));
                    return new DialogueChoice(choice.id, choice.textKey, choice.nextId, Optional(choice.conditionId), Optional(choice.commandId));
                }).ToArray();
                return new DialogueLine(line.id, line.speakerKey, line.textKey, Optional(line.nextId), Optional(line.conditionId), choices);
            }).ToArray());
            foreach (var line in definition.Lines)
            {
                RequireText(references, line.SpeakerKey, DialogueTextKind.Speaker); RequireText(references, line.TextKey, DialogueTextKind.Line);
                RequireCondition(references, line.ConditionId);
                foreach (var choice in line.Choices)
                {
                    RequireText(references, choice.TextKey, DialogueTextKind.Choice); RequireCondition(references, choice.ConditionId);
                    if (choice.CommandId != null && !references.HasCommand(choice.CommandId)) throw new ArgumentException("Unknown dialogue command reference: " + choice.CommandId);
                }
            }
            return new CompiledDialogueSource(definition, references);
        }
        static void RequireText(DialogueReferenceCatalog references, string key, DialogueTextKind kind)
        {
            if (!references.HasText(key, kind)) throw new ArgumentException("Missing or incorrectly typed " + kind + " text reference: " + key);
        }
        static void RequireCondition(DialogueReferenceCatalog references, string id)
        {
            if (id != null && !references.HasCondition(id)) throw new ArgumentException("Unknown dialogue condition reference: " + id);
        }
        static string Optional(string value) => value == "" ? null : value;
    }
}
