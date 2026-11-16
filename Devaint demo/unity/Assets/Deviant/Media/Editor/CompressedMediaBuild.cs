using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Deviant.Media.Editor
{
    [Serializable] public sealed class ImageSelection { public string path; public int width, height; public bool opaque; }
    [Serializable] public sealed class CompressionSelection { public int schema; public ImageSelection[] images; public string[] audioSources; }

    public static class CompressedMediaBuild
    {
        static string SelectionPath => Path.Combine(Application.dataPath, "Deviant/Media/Editor/CompressionSelection.json");
        static string ModePath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/DeviantMediaTextureFormat.txt"));
        static CompressionSelection selection;
        static Dictionary<string, ImageSelection> images;
        public static string WebFormat => File.Exists(ModePath) ? File.ReadAllText(ModePath).Trim() : "dxt";
        public static CompressionSelection Selection => selection ??= JsonUtility.FromJson<CompressionSelection>(File.ReadAllText(SelectionPath));
        public static ImageSelection Find(string path)
        {
            if (!File.Exists(SelectionPath)) return null;
            images ??= Selection.images.ToDictionary(image => image.path, StringComparer.Ordinal);
            return images.TryGetValue(path, out var image) ? image : null;
        }
        public static TextureImporterFormat Format(ImageSelection image, bool bc7)
        {
            if (image.width < 4 || image.height < 4 || image.width % 4 != 0 || image.height % 4 != 0)
                return TextureImporterFormat.RGBA32;
            return image.opaque ? TextureImporterFormat.DXT1 : bc7 ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT5;
        }
        internal static void Configure(TextureImporter importer, ImageSelection image)
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
            foreach (string platform in new[] { "Standalone", "WebGL" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform; settings.overridden = true;
                settings.format = Format(image, platform == "Standalone" || WebFormat == "bc7");
                settings.textureCompression = TextureImporterCompression.Compressed;
                settings.maxTextureSize = importer.maxTextureSize;
                settings.crunchedCompression = false; settings.compressionQuality = 50;
                importer.SetPlatformTextureSettings(settings);
            }
        }
        public static void Prepare(string webFormat)
        {
            if (webFormat != "dxt" && webFormat != "bc7") throw new ArgumentException("Use dxt or bc7");
            Directory.CreateDirectory(Path.GetDirectoryName(ModePath)); File.WriteAllText(ModePath, webFormat);
            int changed = 0;
            // Queue all importer changes together; per-image refreshes otherwise scan the entire project thousands of times.
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var image in Selection.images.Where(image => image.path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                {
                    var importer = AssetImporter.GetAtPath(image.path) as TextureImporter;
                    if (!importer) throw new InvalidOperationException("Compressed image missing: " + image.path);
                    if (importer.GetPlatformTextureSettings("WebGL").overridden
                        && importer.GetPlatformTextureSettings("WebGL").format == Format(image, webFormat == "bc7")
                        && importer.GetPlatformTextureSettings("Standalone").overridden
                        && importer.GetPlatformTextureSettings("Standalone").format == Format(image, true)) continue;
                    Configure(importer, image); importer.SaveAndReimport(); changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("COMPRESSED_MEDIA_GPU mode=" + webFormat + " changed=" + changed + " images=" + Selection.images.Length);
        }
        public static IDisposable BeginPackaging() => new PackagingScope();

        // Source clips remain available to the authoring project, but are excluded from Resources in a Player build.
        sealed class PackagingScope : IDisposable
        {
            [Serializable] public sealed class Move { public string from, to; }
            [Serializable] public sealed class Backup { public string path, backup; }
            [Serializable] public sealed class Journal { public List<Move> moves = new List<Move>(); public List<Backup> backups = new List<Backup>(); }
            readonly string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/DeviantMediaPackaging"));
            readonly Journal journal = new Journal();
            string JournalPath => Path.Combine(root, "recovery.json");
            void Save() => File.WriteAllText(JournalPath, JsonUtility.ToJson(journal));
            public PackagingScope()
            {
                if (File.Exists(JournalPath)) throw new InvalidOperationException("Unrestored media packaging journal: " + JournalPath + ". Use Tools/deviant/Media/Restore Packaging Sources before building.");
                Directory.CreateDirectory(root); Save();
                try
                {
                    foreach (string path in new[] { "Assets/Deviant/Audio/Resources/SelectedAudioClips.asset", "Assets/ForestCombatV2/Resources/PresentationSfx.asset" })
                    {
                        BackupFile(path);
                        var asset = AssetDatabase.LoadMainAssetAtPath(path);
                        var serialized = new SerializedObject(asset); var entries = serialized.FindProperty("entries");
                        for (int i = 0; i < entries.arraySize; i++)
                            entries.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = null;
                        serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(asset);
                    }
                    foreach (var image in Selection.images.Where(image => image.path.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))) BakeGpuBytes(image);
                    foreach (string path in Selection.audioSources.Where(path => path.Contains("/Resources/")))
                    {
                        if (!File.Exists(path)) throw new InvalidOperationException("Audio source missing: " + path);
                        string to = "Assets/Deviant/Media/ExcludedAudioBuild/" + path.Substring("Assets/".Length).Replace("/Resources/", "/AuthoringSources/");
                        Directory.CreateDirectory(Path.GetDirectoryName(to)); AssetDatabase.Refresh();
                        journal.moves.Add(new Move { from = path, to = to }); Save();
                        string error = AssetDatabase.MoveAsset(path, to);
                        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                    }
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                catch { Dispose(); throw; }
            }
            void BackupFile(string path)
            {
                string backup = Path.Combine(root, journal.backups.Count + ".original"); File.Copy(path, backup, true);
                journal.backups.Add(new Backup { path = path, backup = backup }); Save();
            }
            void BakeGpuBytes(ImageSelection image)
            {
                var format = Format(image, WebFormat == "bc7");
                if (format == TextureImporterFormat.RGBA32) return;
                byte[] png = File.ReadAllBytes(image.path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!ImageConversion.LoadImage(texture, png)) throw new InvalidOperationException(image.path);
                    var gpu = format == TextureImporterFormat.BC7 ? TextureFormat.BC7 : format == TextureImporterFormat.DXT1 ? TextureFormat.DXT1 : TextureFormat.DXT5;
                    EditorUtility.CompressTexture(texture, gpu, TextureCompressionQuality.Normal);
                    byte[] blocks = texture.GetRawTextureData();
                    BackupFile(image.path);
                    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("DVGPU001")); writer.Write(texture.width); writer.Write(texture.height);
                    writer.Write((int)gpu); writer.Write(blocks.Length); writer.Write(png.Length); writer.Write(blocks); writer.Write(png);
                    File.WriteAllBytes(image.path, stream.ToArray()); AssetDatabase.ImportAsset(image.path);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            public void Dispose()
            {
                if (!File.Exists(JournalPath)) return;
                Restore(root, journal); File.Delete(JournalPath);
            }
            internal static void Restore(string root, Journal journal)
            {
                foreach (var move in Enumerable.Reverse(journal.moves))
                {
                    if (!File.Exists(move.to)) continue;
                    if (File.Exists(move.from)) throw new IOException("Preserve occupied audio source: " + move.from);
                    Directory.CreateDirectory(Path.GetDirectoryName(move.from)); AssetDatabase.Refresh();
                    string error = AssetDatabase.MoveAsset(move.to, move.from);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                }
                foreach (var backup in journal.backups) { File.Copy(backup.backup, backup.path, true); AssetDatabase.ImportAsset(backup.path); }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
        [MenuItem("Tools/deviant/Media/Restore Packaging Sources")]
        public static void RestorePackagingSources()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/DeviantMediaPackaging"));
            string path = Path.Combine(root, "recovery.json");
            if (!File.Exists(path)) return;
            PackagingScope.Restore(root, JsonUtility.FromJson<PackagingScope.Journal>(File.ReadAllText(path))); File.Delete(path);
        }
    }

    public sealed class CompressedTextureImporter : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 10000;
        public override uint GetVersion() => 1;
        void OnPreprocessTexture()
        {
            var selection = CompressedMediaBuild.Find(assetPath);
            if (selection != null) CompressedMediaBuild.Configure((TextureImporter)assetImporter, selection);
        }
    }
}
