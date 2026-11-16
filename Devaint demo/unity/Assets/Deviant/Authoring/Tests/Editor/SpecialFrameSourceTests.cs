using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Deviant.Authoring.Tests
{
    public class SpecialFrameSourceTests
    {
        string repository;
        [SetUp] public void SetUp()
        {
            var original = new DirectoryInfo(Application.dataPath);
            while (original != null && !File.Exists(Path.Combine(original.FullName, SpecialFrameSource.RelativePath))) original = original.Parent;
            Assert.That(original, Is.Not.Null);
            repository = Path.Combine(Path.GetTempPath(), "deviant-special-frame-" + Guid.NewGuid().ToString("N"));
            foreach (string path in Directory.GetFiles(Path.Combine(original.FullName, "data/combat"), "*.json", SearchOption.AllDirectories))
                Copy(original.FullName, Path.GetRelativePath(original.FullName, path));
            foreach (string path in new[] { MotionRegistrySource.CombatPath, AttackMotionSource.CatalogPath, "unity/Assets/Deviant/Combat/Resources/CharacterV2/HeavyWeaponCatalog.json", "data/combat/presentation/basic_attack_effects.json", "tools/combat/export_attack_motion.py", "tools/combat/special_frame_transport.py" }) Copy(original.FullName, path);
        }
        void Copy(string original, string relative)
        {
            string destination = Path.Combine(repository, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(Path.Combine(original, relative), destination);
        }
        [TearDown] public void TearDown()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<SpecialFrameWindow>().Where(value => value.name == "SpecialFrameTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        [Test] public void CompleteSourceSaveReloadConflictsAndUnknownGraphsNeverPublishPartialState()
        {
            var original = SpecialFrameSource.Load(repository, out var hash); Assert.That(original.Actions.Count, Is.EqualTo(23));
            var candidate = original.ToDocument(); var action = original.Find("whip_default");
            candidate.actions[Array.FindIndex(candidate.actions, value => value.actionId == action.Id)] = ActionSpecialFrameCompiler.ProjectAuthored(action.NumericAuthored, action.Media);
            var compiled = SpecialFrameSource.Compile(repository, candidate);
            hash = SpecialFrameSource.Save(repository, hash, candidate); var reloaded = SpecialFrameSource.Load(repository, out var currentHash);
            Assert.That(currentHash, Is.EqualTo(hash)); Assert.That(reloaded.ContentHash, Is.EqualTo(compiled.ContentHash));
            Assert.That(reloaded.Find(action.Id).UsesFrameTime, Is.True); Assert.That(SpecialFrameSource.HasFrameOverride(repository, action.Id), Is.True);
            var canonical = JsonUtility.FromJson<SpecialFrameCatalogDocument>(File.ReadAllText(Path.Combine(repository, SpecialFrameSource.RelativePath)));
            Assert.That(canonical.actions.Where(value => value.mode == MeleeTimingMode.CombatTimeFixed).All(value => string.IsNullOrEmpty(value.policyVersion) && string.IsNullOrEmpty(value.mediaBindingVersion)), Is.True);
            Assert.That(canonical.actions.Single(value => value.actionId == action.Id).timeline.frames.Length, Is.EqualTo(compiled.Find(action.Id).Media.StepIds.Count));
            string path = Path.Combine(repository, SpecialFrameSource.RelativePath); byte[] bytes = File.ReadAllBytes(path);
            var invalid = reloaded.ToDocument(); invalid.actions = invalid.actions.Skip(1).ToArray(); Assert.Throws<ArgumentException>(() => SpecialFrameSource.Save(repository, hash, invalid));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            File.AppendAllText(path, "\n"); Assert.Throws<IOException>(() => SpecialFrameSource.Save(repository, hash, candidate));
            Assert.That(File.ReadAllText(path), Does.EndWith("\n\n"));
        }
        [Test] public void ActiveFrameModesGuardCanonicalNumericTimingAndPermitOtherPayloadEdits()
        {
            var current = SpecialFrameSource.Load(repository, out var hash); var candidate = current.ToDocument(); var action = current.Find("whip_default");
            candidate.actions[Array.FindIndex(candidate.actions, value => value.actionId == action.Id)] = ActionSpecialFrameCompiler.ProjectAuthored(action.NumericAuthored, action.Media);
            SpecialFrameSource.Save(repository, hash, candidate);
            var numeric = AuthoredSkillSource.Load(repository, out var numericHash).Single(value => value.Id == action.Id);
            var values = numeric.ToNumericData(); values.activation -= .03125f; var invalid = numeric.Recompile(values);
            byte[] original = File.ReadAllBytes(Path.Combine(repository, AuthoredSkillSource.RelativePath));
            Assert.Throws<ArgumentException>(() => AuthoredSkillSource.Save(repository, numericHash, new[] { invalid }));
            Assert.That(File.ReadAllBytes(Path.Combine(repository, AuthoredSkillSource.RelativePath)), Is.EqualTo(original));
            values = numeric.ToNumericData(); values.hits[0].damage += 5; var allowed = numeric.Recompile(values);
            Assert.DoesNotThrow(() => AuthoredSkillSource.Save(repository, numericHash, new[] { allowed }));
            Assert.That(SpecialFrameSource.Load(repository, out _).Find(action.Id).Execution.Hits[0].Damage, Is.EqualTo(allowed.Execution.Hits[0].Damage));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualWindowConvertsEditsUndoRedoAndRejectsImpossibleProjectionWithoutChangingSources()
        {
            var window = ScriptableObject.CreateInstance<SpecialFrameWindow>(); window.name = "SpecialFrameTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); yield return null;
            var selector = window.rootVisualElement.Q<DropdownField>("action"); Assert.That(selector.choices.Count, Is.EqualTo(23)); selector.value = "whip_default";
            var fixedHash = window.Candidate.ContentHash; var source = File.ReadAllBytes(Path.Combine(repository, SpecialFrameSource.RelativePath));
            window.Convert(); Assert.That(window.Candidate.UsesFrameTime, Is.True);
            var record = window.Draft.actions.Single(value => value.actionId == "whip_default"); string id = record.timeline.frames[0].id;
            var field = window.rootVisualElement.Q<FloatField>("frame-" + id); int originalBits = BitConverter.SingleToInt32Bits(field.value); int expectedBits = BitConverter.SingleToInt32Bits(field.value + .03125f);
            field.value = BitConverter.Int32BitsToSingle(expectedBits); string changedHash = window.Candidate.ContentHash;
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.That(BitConverter.SingleToInt32Bits(window.rootVisualElement.Q<FloatField>("frame-" + id).value), Is.EqualTo(originalBits));
            Undo.PerformRedo(); Assert.That(BitConverter.SingleToInt32Bits(window.rootVisualElement.Q<FloatField>("frame-" + id).value), Is.EqualTo(expectedBits)); Assert.That(window.Candidate.ContentHash, Is.EqualTo(changedHash));
            selector.value = "shield_default"; var zero = window.Candidate; Assert.Throws<ArgumentException>(() => window.Convert()); Assert.That(window.Candidate.ContentHash, Is.EqualTo(zero.ContentHash));
            selector.value = "greatsword_follow_up"; var tail = window.Candidate; Assert.Throws<ArgumentException>(() => window.Convert()); Assert.That(window.Candidate.ContentHash, Is.EqualTo(tail.ContentHash));
            window.rootVisualElement.Q<TextField>("localTimeline").value = "{\"frames\":[null],\"markers\":[]}";
            Assert.Throws<ArgumentException>(() => window.AuthorLocal()); Assert.That(window.Candidate.ContentHash, Is.EqualTo(tail.ContentHash));
            Assert.That(File.ReadAllBytes(Path.Combine(repository, SpecialFrameSource.RelativePath)), Is.EqualTo(source));
            selector.value = "whip_default"; window.ReturnFixed(); Assert.That(window.Candidate.ContentHash, Is.EqualTo(fixedHash));
        }
    }
}
