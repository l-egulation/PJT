using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class ResourceDeploymentTests
    {
        [Serializable] sealed class Contract { public string format; public Item[] files; }
        [Serializable] sealed class Item { public string target, sha256; public int bytes; }
        [Serializable] sealed class MotionSource { public string sourceManifestSHA256; }
        [Serializable] sealed class EffectSource { public string sourceSHA256; }
        static string MainRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        [Test]
        public void EverySelectedImageAndNativeTextureHasExactBytesAndLoadsWithItsMainGuid()
        {
            var document = JsonUtility.FromJson<Contract>(File.ReadAllText(
                Path.Combine(MainRoot, "docs/assets/manifests/unity-deployment.json")));
            Assert.That(document.format, Is.EqualTo("deviant-asset-deployment-v1"));
            Assert.That(document.files.Length, Is.GreaterThan(2000));
            foreach (var item in document.files)
            {
                var data = File.ReadAllBytes(Path.Combine(MainRoot, item.target));
                Assert.That(data.Length, Is.EqualTo(item.bytes), item.target);
                Assert.That(Hash(data), Is.EqualTo(item.sha256), item.target);
                if (!item.target.StartsWith("unity/", StringComparison.Ordinal)) continue;
                string path = item.target.Substring("unity/".Length);
                Assert.That(File.Exists(Path.Combine(MainRoot, item.target + ".meta")), Is.True, path);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.Empty, path);
                if (path.EndsWith(".png", StringComparison.Ordinal))
                    Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(path), Is.Not.Null, path);
                else if (path.EndsWith(".bytes", StringComparison.Ordinal))
                    Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(path), Is.Not.Null, path);
                // The contract also restores approved audio and fonts from resources (audio/, fonts/).
                else if (path.EndsWith(".wav", StringComparison.Ordinal) || path.EndsWith(".mp3", StringComparison.Ordinal))
                    Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(path), Is.Not.Null, path);
                else if (path.EndsWith(".ttf", StringComparison.Ordinal))
                    Assert.That(AssetDatabase.LoadAssetAtPath<Font>(path), Is.Not.Null, path);
                else Assert.Fail("Media deployment must not include gameplay/catalog outputs: " + path);
            }
        }

        [Test]
        public void CurrentMonsterAndBossBindingsMatchTheResourceSourceWithoutOldRootCopies()
        {
            string module = Path.Combine(Application.dataPath, "Deviant/Stage1Monsters");
            string source = Path.Combine(MainRoot, "resources/authoring/workbench/source/MonsterReference");
            var motions = JsonUtility.FromJson<MotionSource>(File.ReadAllText(Path.Combine(module, "Data/import.json")));
            Assert.That(Hash(File.ReadAllBytes(Path.Combine(source, "approved/manifest.json"))),
                Is.EqualTo(motions.sourceManifestSHA256));
            var effects = JsonUtility.FromJson<EffectSource>(File.ReadAllText(Path.Combine(module, "Data/effects-import.json")));
            Assert.That(Hash(File.ReadAllBytes(Path.Combine(source, "approved-effects/manifest.json"))),
                Is.EqualTo(effects.sourceSHA256));
            string boss = Hash(File.ReadAllBytes(Path.Combine(MainRoot,
                "resources/monsters/stage-01/bosses/clock-executioner/source/battle-preview/asset-manifest.json")));
            foreach (var path in new[] {"Resources/ClockExecutionerPreview/preview.json", "Resources/ClockExecutionerTelegraph/telegraphs.json"})
                Assert.That(JsonUtility.FromJson<EffectSource>(File.ReadAllText(Path.Combine(module, path))).sourceSHA256,
                    Is.EqualTo(boss), path);
        }
    }
}
