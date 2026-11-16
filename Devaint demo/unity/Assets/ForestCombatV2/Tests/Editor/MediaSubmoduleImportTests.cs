using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class MediaSubmoduleImportTests
    {
        [Test]
        public void AllSelectedAudioClipsResolveAtTheirRuntimeIds()
        {
            var catalog = Resources.Load<SelectedAudioClips>("SelectedAudioClips");
            Assert.That(catalog, Is.Not.Null);
            var entries = new SerializedObject(catalog).FindProperty("entries");
            Assert.That(entries.arraySize, Is.EqualTo(59));
            var ids = new HashSet<string>();
            int music = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("id").stringValue;
                Assert.That(ids.Add(id), Is.True, id);
                var clip = SelectedAudioClips.Find(id);
                Assert.That(clip, Is.Not.Null, id);
                Assert.That(clip.samples, Is.Positive, id);
                bool bgm = id.StartsWith("bgm_", StringComparison.Ordinal);
                if (bgm) music++;
                Assert.That(clip.channels, Is.EqualTo(bgm ? 2 : 1), id);
                Assert.That(clip.loadType, Is.EqualTo(bgm ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad), id);
            }
            Assert.That(music, Is.EqualTo(4));
        }

        [Serializable]
        sealed class Mapping
        {
            public Record[] records;
        }

        [Serializable]
        sealed class Record
        {
            public string originalProjectPath;
        }

        [Test]
        public void MigratedAudioFontsAndVfxImportAtTheirOriginalUnityGuids()
        {
            var repository = Directory.GetParent(Application.dataPath).Parent.FullName;
            var mappingPath = Path.Combine(repository, "resources/imports/main-media/mapping.json");
            Assert.That(File.Exists(mappingPath), Is.True, "Restore the pinned resource submodule first.");
            var mapping = JsonUtility.FromJson<Mapping>(File.ReadAllText(mappingPath));
            int audio = 0, fonts = 0, textures = 0;
            foreach (var record in mapping.records)
            {
                Assert.That(record.originalProjectPath.StartsWith("unity/Assets/", StringComparison.Ordinal), Is.True);
                var path = record.originalProjectPath.Substring("unity/".Length);
                var metadata = File.ReadAllText(Path.Combine(repository, record.originalProjectPath + ".meta"));
                var guid = Regex.Match(metadata, @"(?m)^guid: ([0-9a-f]{32})\r?$").Groups[1].Value;
                Assert.That(guid.Length, Is.EqualTo(32), path);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid), path);
                switch (Path.GetExtension(path).ToLowerInvariant())
                {
                    case ".wav":
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        Assert.That(clip, Is.Not.Null, path);
                        Assert.That(clip.samples, Is.GreaterThan(0), path);
                        // Unity 6000.6's Web AudioImporter previews originals as 44.1 kHz AAC.
                        // Player audio is separately decoded from Opus at 48 kHz (Deviant.Media.Tests).
                        int importedFrequency = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL ? 44100 : 48000;
                        Assert.That(clip.frequency, Is.EqualTo(importedFrequency), path);
                        audio++;
                        break;
                    case ".ttf":
                        Assert.That(AssetDatabase.LoadAssetAtPath<Font>(path), Is.Not.Null, path);
                        fonts++;
                        break;
                    case ".png":
                        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        Assert.That(texture, Is.Not.Null, path);
                        Assert.That(texture.width, Is.GreaterThan(0), path);
                        textures++;
                        break;
                    default:
                        Assert.Fail("Unexpected migrated media: " + path);
                        break;
                }
            }
            Assert.That(audio, Is.EqualTo(170));
            Assert.That(fonts, Is.EqualTo(6));
            Assert.That(textures, Is.EqualTo(43));
        }

        [Test]
        public void MigratedStationaryBossTextAssetKeepsItsGuidAndDecodesAsPng()
        {
            const string resource = "ClockExecutioner/StationaryBytes";
            const string path = "Assets/ForestCombatV2/Resources/ClockExecutioner/StationaryBytes.bytes";
            var project = Directory.GetParent(Application.dataPath).FullName;
            var metadata = File.ReadAllText(Path.Combine(project, path + ".meta"));
            var guid = Regex.Match(metadata, @"(?m)^guid: ([0-9a-f]{32})\r?$").Groups[1].Value;
            Assert.That(guid.Length, Is.EqualTo(32));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            var bytes = Resources.Load<TextAsset>(resource);
            Assert.That(bytes, Is.Not.Null, "Restore the pinned resource contract before opening Unity.");
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(project, path)), bytes.bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, bytes.bytes), Is.True);
                Assert.That(texture.width, Is.GreaterThan(0));
                Assert.That(texture.height, Is.GreaterThan(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
