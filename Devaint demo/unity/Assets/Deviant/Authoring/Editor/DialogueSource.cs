using System;
using System.IO;
using System.Text;
using Deviant.Definitions;
using Deviant.Integration;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class DialogueSourceSnapshot
    {
        public string Path { get; }
        public string FileHash { get; }
        public string ReferencePath { get; }
        public string ReferenceFileHash { get; }
        public CompiledDialogueSource Compiled { get; }
        internal DialogueSourceSnapshot(string path, string fileHash, string referencePath, string referenceFileHash, CompiledDialogueSource compiled)
        { Path = path; FileHash = fileHash; ReferencePath = referencePath; ReferenceFileHash = referenceFileHash; Compiled = compiled; }
    }

    public static class DialogueSource
    {
        public static DialogueSourceSnapshot Load(string sourcePath, string referencePath)
        {
            sourcePath = Path.GetFullPath(sourcePath); referencePath = Path.GetFullPath(referencePath);
            if (string.Equals(sourcePath, referencePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Dialogue source and reference catalog paths must differ.");
            string source = SourceFiles.Read(sourcePath, out var hash, DialogueSourceJson.MaximumFileBytes);
            string catalog = SourceFiles.Read(referencePath, out var referenceHash, DialogueSourceJson.MaximumFileBytes);
            return new DialogueSourceSnapshot(sourcePath, hash, referencePath, referenceHash, DialogueSourceJson.Compile(source, catalog));
        }
        public static string Serialize(DialogueSourceDocument document, DialogueReferenceCatalog references)
        {
            var compiled = DialogueSourceCompiler.Compile(document, references);
            string json = JsonUtility.ToJson(compiled.ToDocument(), true) + "\n";
            if (Encoding.UTF8.GetByteCount(json) > DialogueSourceJson.MaximumFileBytes) throw new ArgumentException("Dialogue source exceeds the allowed file size.");
            return json;
        }
        public static DialogueSourceSnapshot Save(DialogueSourceSnapshot original, DialogueSourceDocument candidate)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            SourceFiles.Read(original.ReferencePath, out var referenceHash, DialogueSourceJson.MaximumFileBytes);
            if (referenceHash != original.ReferenceFileHash) throw new IOException("Dialogue reference catalog changed outside this window. Reload before saving; no source was overwritten.");
            string json = Serialize(candidate, original.Compiled.References);
            string hash = SourceFiles.Save(original.Path, original.FileHash, json, DialogueSourceJson.MaximumFileBytes);
            return new DialogueSourceSnapshot(original.Path, hash, original.ReferencePath, referenceHash,
                DialogueSourceCompiler.Compile(DialogueSourceJson.ReadDocument(json), original.Compiled.References));
        }
        public static void Export(DialogueSourceSnapshot saved, string destination)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            var fresh = Load(saved.Path, saved.ReferencePath);
            if (fresh.FileHash != saved.FileHash || fresh.ReferenceFileHash != saved.ReferenceFileHash) throw new IOException("Dialogue source or reference catalog changed. Reload before exporting.");
            if (!string.Equals(System.IO.Path.GetExtension(destination), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A generated dialogue TextAsset must use a .json path.", nameof(destination));
            if (string.Equals(Path.GetFullPath(destination), Path.GetFullPath(saved.ReferencePath), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Generated dialogue output cannot overwrite its reference catalog.", nameof(destination));
            SourceFiles.Copy(saved.Path, saved.FileHash, destination);
        }
    }
}
