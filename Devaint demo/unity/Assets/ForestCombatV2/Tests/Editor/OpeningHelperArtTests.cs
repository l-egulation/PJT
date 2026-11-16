using System;
using System.Linq;
using Deviant.Narrative.Opening;
using NUnit.Framework;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class OpeningHelperArtTests
    {
        static string HelperRoot()
        {
            var script = JsonUtility.FromJson<OpeningScript>(Resources.Load<TextAsset>("HubOpening").text);
            return script.beats.SelectMany(beat => beat.actions)
                .Single(action => action.type == "actor" && action.spawn == "helper").sprite;
        }

        [Test] public void OpeningReferencesAll43ImportedNativeFramesWithTheOriginalFootPivot()
        {
            string root = HelperRoot();
            Assert.That(root, Is.EqualTo("Hub/opening/ink-helper"));
            var frames = OpeningHelperArt.Load(root);
            CollectionAssert.AreEqual(new[] { 28, 6, 9 }, frames.Select(row => row.Length));
            string[] clips = { "appear", "idle", "leap" };
            for (int row = 0; row < frames.Length; row++)
                for (int i = 0; i < frames[row].Length; i++)
                {
                    var sprite = frames[row][i];
                    Assert.That(sprite, Is.SameAs(Resources.Load<Sprite>(root + "/" + clips[row] + "/" + i.ToString("00"))));
                    Assert.That(sprite.texture.width, Is.EqualTo(320));
                    Assert.That(sprite.texture.height, Is.EqualTo(200));
                    Assert.That(sprite.texture.isReadable, Is.True, "speech-bubble height is measured from alpha");
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32));
                    Assert.That((sprite.pivot - new Vector2(160, 6)).sqrMagnitude, Is.LessThan(.001f));
                }
            foreach (var sprite in new[] { frames[0][21], frames[1][0], frames[2][4] })
                Assert.That(sprite.texture.GetPixels32().Any(pixel => pixel.a > 32), Is.True,
                    "representative appearance, idle and leap frames must contain visible pixels");
        }

        [Test] public void MissingFramesFailWithTheirExactResourcePath()
        {
            var error = Assert.Throws<InvalidOperationException>(() => OpeningHelperArt.Load("missing-opening-helper"));
            StringAssert.Contains("missing-opening-helper/appear/00", error.Message);
        }

        [Test] public void OversizedSheetIsOutsideUnityAndDeploymentUsesOnlyTheSplitFrames()
        {
            Assert.That(Resources.Load<Texture2D>("Hub/opening/ink-helper-pixel"), Is.Null,
                "the preserved 8960px source must not be imported into Unity again");
        }
    }
}
