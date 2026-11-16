using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1CurrentScenesTests
    {
        [Test] public void TraversalFramesKeepNativeScaleAndFootPivots()
        {
            // The original 290 PPU32 frames are separate from climb-v4's 82 frames with approved per-frame PPU.
            var paths = Directory.GetFiles("Assets/Deviant/Combat/Resources/ApprovedTraversal", "*.png", SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("/climb-v4/")).ToArray();
            Assert.That(paths.Length, Is.EqualTo(290));
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.filterMode, Is.EqualTo(UnityEngine.FilterMode.Point), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), path);
            }
            var jump = (TextureImporter)AssetImporter.GetAtPath("Assets/Deviant/Combat/Resources/ApprovedTraversal/jump/base_sword/F01.png");
            Assert.That(jump.spritePivot, Is.EqualTo(new UnityEngine.Vector2(.5f, .0546875f)));
        }

        // Weapon jump frames were drawn with the jump's vertical travel; their pivots cancel it (tools/combat/
        // pin_jump_feet.py) so the physical jump is the only travel, as with base_sword. Launcher frame pivots rise by
        // that baked travel: 0,0,-1,25,28,19,13,-1,0,0,0 px against frame 1.
        [Test] public void WeaponJumpFramesCancelTheTravelDrawnIntoThem()
        {
            float[] travel = { 0, 0, -1, 25, 28, 19, 13, -1, 0, 0, 0 };
            foreach (var form in new[] { "launcher", "launcher_laser", "dagger", "greatsword", "rapier" })
            {
                float first = 0;
                for (int i = 0; i < travel.Length; i++)
                {
                    var path = $"Assets/Deviant/Combat/Resources/ApprovedTraversal/jump/{form}/F{i + 1:00}.png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    var size = AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path).height;
                    float pivot = importer.spritePivot.y * size;
                    if (i == 0) first = pivot;
                    Assert.That(pivot - first, Is.EqualTo(travel[i]).Within(.01f), path);
                }
            }
        }

        [Test] public void OnlyCurrentTitleAndStageArePlayable()
        {
            var expected = new[] { Chapter1RunHost.TitleScene, Chapter1MapSelector.ProceduralScene };
            var actual = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).ToArray();
            Assert.That(actual, Is.EquivalentTo(expected.Concat(new[] { Chapter1MapSelector.BossTestScene })));
            Assert.That(EditorBuildSettings.scenes.Select(s => s.path), Is.EqualTo(expected));
            Assert.That(EditorBuildSettings.scenes.All(s => s.enabled), Is.True);
            Assert.That(Chapter1MapSelector.Supports(Chapter1MapSelector.ProceduralScene), Is.True);
            foreach(var path in new[] { Chapter1MapSelector.GameScene, Chapter1MapSelector.OriginalScene, Chapter1MapSelector.Ver2Scene })
                Assert.That(Chapter1MapSelector.Supports(path), Is.False, path);
        }
    }
}
