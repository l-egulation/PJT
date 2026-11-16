using System;
using System.IO;
using System.Text;
using Deviant.Definitions;
using Deviant.Presentation.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Authoring
{
    public sealed class CueSourceSnapshot
    {
        public string Path { get; }
        public string FileHash { get; }
        public CompiledCueSource Compiled { get; }
        internal CueSourceSnapshot(string path, string fileHash, CompiledCueSource compiled) { Path = path; FileHash = fileHash; Compiled = compiled; }
    }
    public static class CueSource
    {
        public static CueSourceSnapshot Load(string path)
        {
            path = System.IO.Path.GetFullPath(path);
            string json = SourceFiles.Read(path, out var hash, CueSourceJson.MaximumFileBytes);
            return new CueSourceSnapshot(path, hash, CueSourceJson.Compile(json));
        }
        public static string Serialize(CueSourceDocument document)
        {
            string json = JsonUtility.ToJson(CueSourceCompiler.Compile(document).ToDocument(), true) + "\n";
            if (Encoding.UTF8.GetByteCount(json) > CueSourceJson.MaximumFileBytes) throw new ArgumentException("Cue source exceeds the allowed file size.");
            return json;
        }
        public static CueSourceSnapshot Create(string path, CueSourceDocument document)
        {
            string json = Serialize(document); path = System.IO.Path.GetFullPath(path);
            string hash = SourceFiles.Create(path, json);
            return new CueSourceSnapshot(path, hash, CueSourceJson.Compile(json));
        }
        public static CueSourceSnapshot Save(CueSourceSnapshot original, CueSourceDocument candidate)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            string json = Serialize(candidate);
            string hash = SourceFiles.Save(original.Path, original.FileHash, json, CueSourceJson.MaximumFileBytes);
            return new CueSourceSnapshot(original.Path, hash, CueSourceJson.Compile(json));
        }
        public static void Export(CueSourceSnapshot saved, string destination)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            if (!string.Equals(System.IO.Path.GetExtension(destination), ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A generated Cue TextAsset requires a .json path.", nameof(destination));
            SourceFiles.Copy(saved.Path, saved.FileHash, destination);
        }
    }
    // Editor resolution is typed and exact. Missing GUIDs/subassets remain
    // unavailable; a matching display name or another subasset is never used.
    public sealed class CueSourceEditorAssets : ICueSourceAssets
    {
        public Sprite ReadSprite(CueSourceAssetReference reference) => Read<Sprite>(reference);
        public AudioClip ReadAudio(CueSourceAssetReference reference) => Read<AudioClip>(reference);
        public static CueSourceAssetDocument Reference(Object asset)
        {
            if (!asset) return new CueSourceAssetDocument { guid = "", localFileId = 0 };
            if (!(asset is Sprite) && !(asset is AudioClip)) throw new ArgumentException("Only explicit Sprite or AudioClip assets are supported.");
            if (!AssetDatabase.Contains(asset) || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                throw new ArgumentException("Choose an imported persistent Unity asset, not a scene or transient object.");
            return new CueSourceAssetReference(guid, localId).ToDocument();
        }
        static T Read<T>(CueSourceAssetReference reference) where T : Object
        {
            if (!reference.IsAssigned) throw new InvalidOperationException(typeof(T).Name + " asset is unassigned.");
            string path = AssetDatabase.GUIDToAssetPath(reference.Guid);
            if (!string.IsNullOrEmpty(path))
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is T typed && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId) &&
                        localId == reference.LocalFileId && string.Equals(guid, reference.Guid, StringComparison.OrdinalIgnoreCase)) return typed;
            throw new InvalidOperationException(typeof(T).Name + " asset is unavailable: " + reference);
        }
    }
}
