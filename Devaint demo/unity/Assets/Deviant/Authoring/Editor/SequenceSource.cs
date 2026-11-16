using System;
using System.IO;
using System.Text;
using Deviant.Definitions;
using Deviant.Integration;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class SequenceSourceSnapshot
    {
        public string Path { get; }
        public string FileHash { get; }
        public CompiledSequenceSource Compiled { get; }
        internal SequenceSourceSnapshot(string path, string fileHash, CompiledSequenceSource compiled) { Path = path; FileHash = fileHash; Compiled = compiled; }
    }
    public static class SequenceSource
    {
        public static SequenceSourceSnapshot Load(string path)
        {
            path = System.IO.Path.GetFullPath(path);
            string json = SourceFiles.Read(path, out var hash, SequenceSourceJson.MaximumFileBytes);
            return new SequenceSourceSnapshot(path, hash, SequenceSourceJson.Compile(json));
        }
        public static string Serialize(SequenceSourceDocument document)
        {
            string json = JsonUtility.ToJson(SequenceSourceCompiler.Compile(document).ToDocument(), true) + "\n";
            if (Encoding.UTF8.GetByteCount(json) > SequenceSourceJson.MaximumFileBytes) throw new ArgumentException("Sequence source exceeds the allowed file size.");
            return json;
        }
        public static SequenceSourceSnapshot Save(SequenceSourceSnapshot original, SequenceSourceDocument candidate)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            string json = Serialize(candidate);
            string hash = SourceFiles.Save(original.Path, original.FileHash, json, SequenceSourceJson.MaximumFileBytes);
            return new SequenceSourceSnapshot(original.Path, hash, SequenceSourceJson.Compile(json));
        }
        public static void Export(SequenceSourceSnapshot saved, string destination)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            if (!string.Equals(System.IO.Path.GetExtension(destination), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A generated sequence TextAsset requires a .json path.", nameof(destination));
            SourceFiles.Copy(saved.Path, saved.FileHash, destination);
        }
    }
}
