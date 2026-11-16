using System.Linq;
using System.IO;
using UnityEditor;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class BasicOutlineMotionTests
    {
        [System.Serializable] sealed class Selection { public SelectionFile[] files; }
        [System.Serializable] sealed class SelectionFile { public string target, sha256; public SelectionSource source; }
        [System.Serializable] sealed class SelectionSource { public string asset; }

        [Test]
        public void SelectedHammerAndRapierBytesMatchTheFixedDeploymentContract()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var selection = JsonUtility.FromJson<Selection>(File.ReadAllText(Path.Combine(root, "docs/assets/manifests/unity-deployment.json")));
            var files = selection.files.Where(f => f.source.asset == "characters.ink.weapons.basic-outline").ToArray();
            Assert.That(files.Count(f => f.target.Contains("/hammer-a/")), Is.EqualTo(21));
            Assert.That(files.Count(f => f.target.Contains("/rapier/")), Is.EqualTo(24));
            using (var hash = System.Security.Cryptography.SHA256.Create())
                foreach (var file in files)
                    Assert.That(System.BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(root, file.target)))).Replace("-", "").ToLowerInvariant(),
                        Is.EqualTo(file.sha256), file.target);
        }

        [TestCase("hammer", "attack1", 9, 7, .80f, .96f)]
        [TestCase("hammer", "attack2", 12, 6, .29f, .59f)]
        [TestCase("rapier", "attack1", 9, 3, .10f, .22f)]
        [TestCase("rapier", "attack2", 7, 2, .10f, .22f)]
        [TestCase("rapier", "attack3", 8, 1, .12f, .36f)]
        public void OriginalFramesLoadAndContactPictureMatchesExistingCombatClock(
            string form, string clip, int count, int contact, float impact, float end)
        {
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var registry = new MotionSourceRegistry(media.motionRegistry, combat);
            var body = media.sets.Single(s => s.form == form).Find(clip);
            Assert.That(body.frames.Length, Is.EqualTo(count));
            var numeric = combat.forms.Single(s => s.id == form).basics[int.Parse(clip.Substring(6)) - 1];
            Assert.That(numeric.impact, Is.EqualTo(impact).Within(.00001f));
            Assert.That(numeric.duration, Is.EqualTo(end).Within(.00001f));
            var timing = registry.Motion(form + "/" + clip).Compile(impact, end);
            Assert.That(timing.End, Is.EqualTo(end).Within(.00001f));
            Assert.That(timing.Impact, Is.EqualTo(impact).Within(.00001f));
            Assert.That(timing.FrameAt(impact + .00001f), Is.EqualTo(contact));
            foreach (var frame in body.frames)
            {
                Assert.That(frame, Does.StartWith("CharacterBasicOutline/"));
                var sprite = Resources.Load<Sprite>(frame);
                Assert.That(sprite, Is.Not.Null, frame);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(256, 144)), frame);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(form == "hammer" ? 32 : 35), frame);
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(form == "hammer" ? 104 : 72, 16)), frame);
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), frame);
            }
        }

        [Test]
        public void RapierReadyPoseHasTheSameWorldHeightAsIdle()
        {
            var idle = Resources.Load<Sprite>("CharacterApproved11/rapier/F030");
            var ready = Resources.Load<Sprite>("CharacterBasicOutline/rapier/00");
            Assert.That(AlphaHeight(ready) / ready.pixelsPerUnit,
                Is.EqualTo(AlphaHeight(idle) / idle.pixelsPerUnit).Within(.01f));
        }

        static float AlphaHeight(Sprite sprite)
        {
            var copy = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(copy.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))), Is.True);
                var pixels = copy.GetPixels32();
                int bottom = copy.height, top = -1;
                for (int y = 0; y < copy.height; y++)
                    for (int x = 0; x < copy.width; x++)
                        if (pixels[y * copy.width + x].a >= 128)
                        { bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                Assert.That(top, Is.GreaterThanOrEqualTo(bottom));
                return top - bottom + 1;
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
