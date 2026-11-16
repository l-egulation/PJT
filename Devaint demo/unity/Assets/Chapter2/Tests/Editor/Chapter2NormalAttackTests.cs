using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Chapter2.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2NormalAttackTests
    {
        Chapter2World testWorld;Chapter2NormalEnemy subject;
        void SuppressOtherAttacks(float dt){foreach(var e in testWorld.Actors.Values)if(e&&e!=subject)e.aggressive=false;}
        [UnityTearDown] public IEnumerator LeavePlayModeAfterEachTest(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [SetUp] public void PrepareExistingChapterTwoScene()
        {
            if(Application.isPlaying)return;
            EditorSettings.enterPlayModeOptionsEnabled=false;
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=null;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Chapter2Tools.ScenePath);
            var world=Object.FindAnyObjectByType<Chapter2World>();world.seed=17;world.family="current";
        }


        [Test] public void SelectedEffectClipsKeepNativeImportAndDurations()
        {
            var data=JsonUtility.FromJson<C2FxData>(Resources.Load<TextAsset>("Chapter2AttackFx").text);
            Assert.That(data.clips.Length,Is.EqualTo(10));
            foreach(var c in data.clips)
            {
                Assert.That(c.frames.Length,Is.EqualTo(c.milliseconds.Length),c.id);
                foreach(var path in c.frames)
                {
                    var s=Resources.Load<Sprite>(path);Assert.That(s,Is.Not.Null,path);
                    Assert.That(s.pixelsPerUnit,Is.EqualTo(32));Assert.That(s.texture.filterMode,Is.EqualTo(FilterMode.Point));
                    var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s));
                    Assert.That(importer.mipmapEnabled,Is.False);Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Compressed));
                }
            }
        }
        // Runtime coverage moved to Chapter2ApprovedNormalTests: approved entry delay,
        // three M3 windows and independent post-release projectile ownership.
    }
}
