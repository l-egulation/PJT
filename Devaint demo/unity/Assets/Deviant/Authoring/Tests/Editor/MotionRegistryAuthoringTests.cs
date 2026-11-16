using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class MotionRegistryAuthoringTests
    {
        string repository;
        [SetUp] public void SetUp()
        {
            var original = new DirectoryInfo(Application.dataPath);
            while (original != null && !File.Exists(Path.Combine(original.FullName, MotionRegistrySource.RelativePath))) original = original.Parent;
            Assert.That(original, Is.Not.Null);
            repository = Path.Combine(Path.GetTempPath(), "deviant-motion-registry-" + Guid.NewGuid().ToString("N"));
            foreach (string path in new[] { MotionRegistrySource.RelativePath, MotionRegistrySource.CombatPath, AttackMotionSource.RelativePath,
                AttackMotionSource.CatalogPath, "unity/Assets/Deviant/Combat/Resources/CharacterV2/HeavyWeaponCatalog.json", MeleeFrameSource.RelativePath, SpecialFrameSource.RelativePath, "data/combat/presentation/basic_attack_effects.json", "tools/combat/export_attack_motion.py", "tools/combat/special_frame_transport.py" })
            {
                string destination = Path.Combine(repository, path); Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(Path.Combine(original.FullName, path), destination);
            }
        }
        [TearDown] public void TearDown()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<MotionRegistryWindow>().Where(value => value.name == "MotionRegistryTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        CharacterAnimationCatalog Media() => JsonUtility.FromJson<CharacterAnimationCatalog>(File.ReadAllText(Path.Combine(repository, AttackMotionSource.CatalogPath)));
        [Test] public void SaveGenerateReloadUsesOneCompilerAndPreservesOtherBodyAndEffectSources()
        {
            var baseline = Media(); var registry = MotionRegistrySource.Load(repository, out var fileHash); var document = registry.ToDocument();
            var clip = document.motions.Single(value => value.id == "rapier/attack1"); clip.steps[0].seconds += .03f;
            clip.steps = MotionRegistryEdits.Insert(clip.steps, 0, "rapier-added-known", document.palettes.Single(value => value.id == clip.palette).frames.Last(), .04f);
            var expected = new MotionSourceRegistry(document, MotionRegistrySource.ReadCombat(repository));
            fileHash = MotionRegistrySource.Save(repository, fileHash, document); var reloaded = MotionRegistrySource.Load(repository, out var afterHash);
            Assert.That(afterHash, Is.EqualTo(fileHash)); Assert.That(reloaded.ContentHash, Is.EqualTo(expected.ContentHash));
            AttackMotionSource.Regenerate(repository); var generated = Media();
            Assert.That(MotionRegistrySource.ValidateGenerated(repository, generated, MotionRegistrySource.ReadCombat(repository)).ContentHash, Is.EqualTo(expected.ContentHash));
            var target = generated.sets.Single(value => value.form == "rapier").Find("attack1");
            Assert.That(target.frames, Is.EqualTo(clip.steps.Select(value => value.frame))); Assert.That(target.stepIds, Is.EqualTo(clip.steps.Select(value => value.id)));
            Assert.That(generated.sets.Single(value => value.form == "rapier").effects.Select(value => JsonUtility.ToJson(value)), Is.EqualTo(baseline.sets.Single(value => value.form == "rapier").effects.Select(value => JsonUtility.ToJson(value))));
            Assert.That(JsonUtility.ToJson(generated.sets.Single(value => value.form == "whip")), Is.EqualTo(JsonUtility.ToJson(baseline.sets.Single(value => value.form == "whip"))));
            byte[] once = File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.CatalogPath)); AttackMotionSource.Regenerate(repository);
            Assert.That(File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.CatalogPath)), Is.EqualTo(once));
        }
        [Test] public void SourceConflictInvalidGraphAndStaleBuildProjectionDoNotOverwrite()
        {
            var registry = MotionRegistrySource.Load(repository, out var hash); string path = Path.Combine(repository, MotionRegistrySource.RelativePath); byte[] original = File.ReadAllBytes(path);
            var invalid = registry.ToDocument(); invalid.actions = invalid.actions.Skip(1).ToArray();
            Assert.Throws<ArgumentException>(() => MotionRegistrySource.Save(repository, hash, invalid)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            File.AppendAllText(path, "\n"); Assert.Throws<IOException>(() => MotionRegistrySource.Save(repository, hash, registry.ToDocument()));
            Assert.That(File.ReadAllText(path), Does.EndWith("\n\n"));
            var generated = Media(); generated.motionRegistry.motions.First(value => string.IsNullOrEmpty(value.delegatedSource)).steps[0].seconds += .03f;
            Assert.Throws<ArgumentException>(() => MotionRegistrySource.ValidateGenerated(repository, generated, MotionRegistrySource.ReadCombat(repository)));
            generated = Media(); generated.sets.Single(value => value.form == "rapier").Find("attack1").seconds[0] += .03f;
            Assert.Throws<ArgumentException>(() => MotionRegistrySource.ValidateGenerated(repository, generated, MotionRegistrySource.ReadCombat(repository)));
            Assert.That(Directory.GetFiles(repository, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }
        [Test] public void GeneralEditorSupportsAllSixtyBindingsButFallbackCannotAuthorMissingArt()
        {
            var registry = MotionRegistrySource.Load(repository, out _); var first = new FrameAttackMotionSource(AttackMotionSource.Load(Path.Combine(repository, AttackMotionSource.RelativePath), out _));
            var combat = MotionRegistrySource.ReadCombat(repository);
            foreach (var binding in registry.Actions)
            {
                var timing = MotionRegistryEdits.CompileBody(registry, binding, first, combat);
                Assert.That(timing.Frames.Count, Is.GreaterThan(0), binding.Key.ToString()); Assert.That(timing.End, Is.GreaterThan(0), binding.Key.ToString());
                Assert.That(timing.EffectiveSeconds().Sum(), Is.EqualTo(timing.End).Within(.00001f));
            }
            var document = registry.ToDocument(); var motion = document.motions.Single(value => value.id == "whip/attack1");
            motion.steps = MotionRegistryEdits.Insert(motion.steps, 0, "known-added", document.palettes.Single(value => value.id == motion.palette).frames[0], .04f);
            motion.steps = MotionRegistryEdits.Move(motion.steps, 1, 1); motion.steps = MotionRegistryEdits.Remove(motion.steps, 0);
            Assert.DoesNotThrow(() => new MotionSourceRegistry(document, combat));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowSelectsSharedActionsEditsWithUndoAndKeepsFallbackReadOnly()
        {
            var window = ScriptableObject.CreateInstance<MotionRegistryWindow>(); window.name = "MotionRegistryTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); yield return null;
            var selector = window.rootVisualElement.Q<DropdownField>("action"); Assert.That(selector.choices.Count, Is.EqualTo(60));
            selector.value = selector.choices.Single(value => value.StartsWith("rapier_default/", StringComparison.Ordinal)); yield return null;
            Assert.That(window.rootVisualElement.Q<Label>("affected").text, Does.Contain("rapier_basic_0"));
            var source = File.ReadAllText(Path.Combine(repository, MotionRegistrySource.RelativePath));
            byte[] firstBytes = File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.RelativePath)), generatedBytes = File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.CatalogPath));
            float before = window.rootVisualElement.Q<FloatField>("seconds-0").value;
            var combat = MotionRegistrySource.ReadCombat(repository);
            string originalGraph = new MotionSourceRegistry(window.Draft.registry, combat).ContentHash;
            window.rootVisualElement.Q<FloatField>("seconds-0").value = before + .03f; yield return null;
            Assert.That(window.rootVisualElement.Q<Label>("state").text, Does.Contain("미저장")); Assert.That(window.rootVisualElement.Q<Label>("preview").text, Does.Contain("실제 유지"));
            string editedGraph = new MotionSourceRegistry(window.Draft.registry, combat).ContentHash;
            Assert.That(editedGraph, Is.Not.EqualTo(originalGraph));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Assert.That(window.rootVisualElement.Q<FloatField>("seconds-0").value, Is.EqualTo(before));
            Assert.That(new MotionSourceRegistry(window.Draft.registry, combat).ContentHash, Is.EqualTo(originalGraph));
            Undo.PerformRedo();
            Assert.That(window.rootVisualElement.Q<FloatField>("seconds-0").value, Is.EqualTo(before + .03f));
            Assert.That(window.Draft.registry.motions.Single(value => value.id == "rapier/attack1").steps[0].seconds, Is.EqualTo(before + .03f));
            Assert.That(new MotionSourceRegistry(window.Draft.registry, combat).ContentHash, Is.EqualTo(editedGraph));
            Assert.That(window.rootVisualElement.Q<Label>("state").text, Does.Contain("미저장"));
            Assert.That(window.rootVisualElement.Q<Button>("save").enabledSelf, Is.True);
            window.rootVisualElement.Q<TextField>("newId").value = "redo-known-step";
            var append = window.rootVisualElement.Q<Button>("append");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = append; append.SendEvent(submit); }
            int insertedCount = window.Draft.registry.motions.Single(value => value.id == "rapier/attack1").steps.Length;
            string insertedGraph = new MotionSourceRegistry(window.Draft.registry, combat).ContentHash;
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(window.Draft.registry.motions.Single(value => value.id == "rapier/attack1").steps.Length, Is.EqualTo(insertedCount - 1));
            Undo.PerformRedo();
            Assert.That(window.Draft.registry.motions.Single(value => value.id == "rapier/attack1").steps.Last().id, Is.EqualTo("redo-known-step"));
            Assert.That(new MotionSourceRegistry(window.Draft.registry, combat).ContentHash, Is.EqualTo(insertedGraph));
            Assert.That(File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.RelativePath)), Is.EqualTo(firstBytes));
            Assert.That(File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.CatalogPath)), Is.EqualTo(generatedBytes));
            selector.value = selector.choices.Single(value => value.StartsWith("launcher_basic_0/Conversion/2", StringComparison.Ordinal)); yield return null;
            Assert.That(window.rootVisualElement.Q<Label>("binding").text, Does.Contain("대체")); Assert.That(window.rootVisualElement.Q<Button>("save").enabledSelf, Is.False);
            Assert.That(window.rootVisualElement.Q<Label>("framePolicy").text, Does.Contain("FrameTime gameplay 저작 미지원"));
            Assert.That(File.ReadAllText(Path.Combine(repository, MotionRegistrySource.RelativePath)), Is.EqualTo(source));
        }
    }
}
