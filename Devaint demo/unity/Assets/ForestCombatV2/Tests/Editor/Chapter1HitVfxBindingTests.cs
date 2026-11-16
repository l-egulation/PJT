using System.Collections;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1HitVfxBindingTests
    {
        [Test] public void GeneratedParticleMaterialsKeepTheirAuthoredTextures()
        {
            var bindings = Resources.Load<Deviant.Presentation.Unity.HitFeedbackBindings>("HitFeedback/Generated/Bindings");
            Assert.That(bindings, Is.Not.Null, "Build the local hit feedback bindings first");
            Assert.That(bindings.prefabs.Length, Is.EqualTo(44));
            foreach (var row in bindings.prefabs)
            {
                Assert.That(row.prefab, Is.Not.Null, row.key);
                foreach (var renderer in row.prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, row.key + "/" + renderer.name);
                        Assert.That(material.mainTexture, Is.Not.Null, row.key + "/" + material.name + " must not render as an untextured rectangle");
                        Assert.That(AssetDatabase.GetAssetPath(material.mainTexture), Does.StartWith("Assets/ThirdParty/Wallcoeur/"));
                    }
            }
        }
        // Requires the locally generated bindings (Tools > deviant > Presentation > Build Hit Feedback Bindings,
        // built automatically by interactive editors and by GameBuilder.BuildWindows).
        [UnityTest] public IEnumerator ChapterSceneAttachesReadyHitFeedbackWithGeneratedBindings()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            Assert.That(Resources.Load<Object>("HitFeedback/Generated/Bindings"), Is.Not.Null, "generated hit feedback bindings must exist");
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            HitFeedbackBridge bridge = null;
            for (int frame = 0; frame < 120 && !(bridge && bridge.Ready); frame++) { yield return null; bridge = world.arena.GetComponent<HitFeedbackBridge>(); }
            Assert.That(bridge, Is.Not.Null, "chapter 1 combat attaches the hit feedback bridge");
            Assert.That(bridge.Ready, Is.True, "the bridge loads its bindings, so hit VFX play in chapter 1");
            yield return new ExitPlayMode();
        }
    }
}
