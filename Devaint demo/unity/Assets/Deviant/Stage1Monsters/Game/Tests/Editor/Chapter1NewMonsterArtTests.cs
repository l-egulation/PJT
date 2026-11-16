using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game.Tests
{
    public sealed class Chapter1NewMonsterArtTests
    {
        [System.Serializable] sealed class Deployment { public DeploymentItem[] files; }
        [System.Serializable] sealed class DeploymentItem { public string target, sha256; public string[] replaceHashes; }
        [Test] public void ProgressiveShakeHasAnExactFinalHold()
        {
            float early=0;bool late=false;
            for(float t=0;t<1.8f;t+=.01f)
            {
                float offset=Chapter1NewMonsterPresentation.ShakePixels(t,2,.2f);
                if(t<.4f)early=Mathf.Max(early,Mathf.Abs(offset));
                if(t>1.3f)late|=Mathf.Abs(offset)>=2;
                Assert.That(Mathf.Abs(offset),Is.LessThanOrEqualTo(3));
            }
            Assert.That(early,Is.LessThanOrEqualTo(1));Assert.That(late,Is.True);
            foreach(float t in new[]{1.8f,1.84f,1.92f,1.99f,2f})
                Assert.That(Chapter1NewMonsterPresentation.ShakePixels(t,2,.2f),Is.Zero);
        }
        [Test] public void NativeWarningAndBodyClipsHaveExpectedDimensionsAndDurations()
        {
            var art=Chapter1NewMonsterArt.Load();
            Assert.That(art.Find("barrel-attack").DurationMs,Is.EqualTo(2400));
            Assert.That(art.Find("millstone-roll").DurationMs,Is.EqualTo(320));
            Assert.That(art.Find("barrel-launch").DurationMs,Is.EqualTo(320));
            var warning=art.Find("barrel-warning").SpriteAt(0);
            Assert.That(warning.rect.size,Is.EqualTo(new Vector2(64,64)));
            Assert.That(warning.bounds.size.x,Is.EqualTo(2).Within(.001));
            Assert.That(warning.bounds.size.y,Is.EqualTo(2).Within(.001));
            Assert.That(warning.texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
        [Test] public void EveryApprovedBodyAndEffectKeepsNativeGeometryAndSelectedDerivative()
        {
            var art = Chapter1NewMonsterArt.Load();
            string repo = System.IO.Directory.GetParent(Application.dataPath).Parent.FullName;
            var deployed = JsonUtility.FromJson<Deployment>(System.IO.File.ReadAllText(System.IO.Path.Combine(repo,
                "docs/assets/manifests/unity-deployment.json"))).files.ToDictionary(item => item.target);
            foreach (var clip in art.clips)
                foreach (var frame in clip.frames)
                {
                    string path = "Assets/Deviant/Stage1Monsters/Resources/" + frame.resource + ".png";
                    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(clip.ppu), path + " native body/FX size");
                    Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(clip.width, clip.height)), path);
                    Assert.That(sprite.pivot, Is.EqualTo(new Vector2(clip.width * clip.pivot[0], clip.height * clip.pivot[1])), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
                    Assert.That(importer.spriteImportMode, Is.EqualTo(UnityEditor.SpriteImportMode.Single), path);
                    Assert.That(importer.textureCompression, Is.EqualTo(UnityEditor.TextureImporterCompression.Compressed), path);
                    Assert.That(importer.mipmapEnabled, Is.False, path);
                    var selected = deployed["unity/" + path];
                    Assert.That(selected.replaceHashes, Does.Contain(frame.sha256), path + " approved source provenance");
                    using (var hash = System.Security.Cryptography.SHA256.Create())
                        Assert.That(System.BitConverter.ToString(hash.ComputeHash(System.IO.File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(),
                            Is.EqualTo(selected.sha256), path + " selected compressed derivative bytes");
                }
        }
    }
}
