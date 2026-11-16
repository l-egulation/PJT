using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class AttackMotionAuthoringTests
    {
        string repository, source;
        [SetUp] public void SetUp()
        {
            var original = new DirectoryInfo(Application.dataPath);
            while (original != null && !File.Exists(Path.Combine(original.FullName, AttackMotionSource.RelativePath))) original = original.Parent;
            Assert.That(original, Is.Not.Null);
            repository = Path.Combine(Path.GetTempPath(), "deviant-motion-" + Guid.NewGuid().ToString("N"));
            foreach (string relative in new[] { AttackMotionSource.RelativePath, AttackMotionSource.CatalogPath, "unity/Assets/Deviant/Combat/Resources/CharacterV2/HeavyWeaponCatalog.json", MotionRegistrySource.RelativePath, MeleeFrameSource.RelativePath, SpecialFrameSource.RelativePath, MotionRegistrySource.CombatPath, "data/combat/actions/base_sword_basic_0.json", "data/combat/presentation/basic_attack_effects.json", "tools/combat/export_attack_motion.py", "tools/combat/special_frame_transport.py" }) {
                string target = Path.Combine(repository, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(Path.Combine(original.FullName, relative), target);
            }
            source = Path.Combine(repository, AttackMotionSource.RelativePath);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var window in Resources.FindObjectsOfTypeAll<AttackMotionWindow>().Where(w => w.name == "AttackMotionTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        CharacterAnimationCatalog Generated() => JsonUtility.FromJson<CharacterAnimationCatalog>(File.ReadAllText(Path.Combine(repository, AttackMotionSource.CatalogPath)));
        [Test] public void SaveReloadAndImageFreeGenerationChangeOnlyFirstMotionTiming()
        {
            string catalogPath = Path.Combine(repository, AttackMotionSource.CatalogPath); var baseline = Generated();
            var document = AttackMotionSource.Load(source, out var hash); document.steps[0].seconds = .12f; document.sourceImpact = .17f; document.fxStart = .13f;
            var expected = new FixedAttackMotion(document); hash = AttackMotionSource.Save(source, hash, document); byte[] savedBytes = File.ReadAllBytes(source);
            Assert.That(savedBytes.Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 239, 187, 191 }));
            var loaded = AttackMotionSource.Load(source, out var reloadedHash); Assert.That(reloadedHash, Is.EqualTo(hash)); Assert.That(new FixedAttackMotion(loaded).ContentHash, Is.EqualTo(expected.ContentHash));
            Assert.That(AttackMotionSource.Save(source, hash, loaded), Is.EqualTo(hash), "Repeated save is deterministic.");
            AttackMotionSource.Regenerate(repository); var generated = Generated();
            var clip = generated.sets.Single(s => s.form == "base_sword").Find("attack1");
            Assert.That(clip.seconds[0], Is.EqualTo(.12f)); Assert.That(clip.sourceImpact, Is.EqualTo(.17f)); Assert.That(clip.fxStart, Is.EqualTo(.13f));
            Assert.That(clip.stepIds, Is.EqualTo(loaded.steps.Select(s => s.id))); Assert.That(clip.frames, Is.EqualTo(loaded.steps.Select(s => s.frame)));
            var set = generated.sets.Single(s => s.form == "base_sword"); set.clips[Array.IndexOf(set.clips, clip)] = baseline.sets.Single(s => s.form == "base_sword").Find("attack1");
            Assert.That(JsonUtility.ToJson(generated), Is.EqualTo(JsonUtility.ToJson(baseline)), "Other body/effect/asset bindings are preserved.");
            byte[] once = File.ReadAllBytes(catalogPath); AttackMotionSource.Regenerate(repository); Assert.That(File.ReadAllBytes(catalogPath), Is.EqualTo(once));
        }
        [Test] public void InvalidSaveExternalEditAndGenerationFailurePreserveRecoverableSource()
        {
            var document = AttackMotionSource.Load(source, out var hash); byte[] original = File.ReadAllBytes(source);
            document.steps[0].seconds = float.NaN; Assert.Catch<ArgumentException>(() => AttackMotionSource.Save(source, hash, document)); Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
            document = AttackMotionSource.Load(source, out hash); File.AppendAllText(source, "\n"); string external = File.ReadAllText(source);
            Assert.Throws<IOException>(() => AttackMotionSource.Save(source, hash, document)); Assert.That(File.ReadAllText(source), Is.EqualTo(external));
            document = AttackMotionSource.Load(source, out hash); document.steps[0].seconds = .12f; hash = AttackMotionSource.Save(source, hash, document);
            string catalogPath = Path.Combine(repository, AttackMotionSource.CatalogPath); byte[] catalog = File.ReadAllBytes(catalogPath); File.Delete(catalogPath);
            Assert.Throws<IOException>(() => AttackMotionSource.Regenerate(repository)); Assert.That(AttackMotionSource.Load(source, out var after).steps[0].seconds, Is.EqualTo(.12f)); Assert.That(after, Is.EqualTo(hash));
            File.WriteAllBytes(catalogPath, catalog); AttackMotionSource.Regenerate(repository); Assert.That(Generated().sets.Single(s => s.form == "base_sword").Find("attack1").seconds[0], Is.EqualTo(.12f));
        }
        [UnityTest] public IEnumerator GameKeepsRunningMotionAndUsesSavedTimingForNextAttack() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabKeepsRunningMotionAndUsesSavedTimingForNextAttack() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var catalog = Generated(); var clip = catalog.sets.Single(s => s.form == "base_sword").Find("attack1");
            var texture = new Texture2D(5, 1); var sprites = Enumerable.Range(0, 5).Select(i => Sprite.Create(texture, new Rect(i, 0, 1, 1), Vector2.zero, 32)).ToArray();
            clip.sprites = sprites; arena.characterAnimations = catalog;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                    player.selected = 0; Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var setupError), Is.True, setupError); player.Position = Vector2.zero; player.SyncFacing(1); arena.enemies[0].Position = new Vector2(3, 0); Physics2D.SyncTransforms();
                    Assert.That(arena.TryGetBasicSwordMotion(out var baseline, out var error), Is.True, error);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var cast = player.currentCast; string oldVersion = arena.Presentation.CurrentAttackMotionVersion;
                    var draft = AttackMotionSource.Load(source, out var hash); draft.steps[0].seconds = .12f;
                    hash = AttackMotionSource.Save(source, hash, draft); AttackMotionSource.Regenerate(repository); draft = AttackMotionSource.Load(source, out var reloadedHash); Assert.That(reloadedHash, Is.EqualTo(hash));
                    Assert.That(arena.TryApplyBasicSwordMotion(draft, false, out error), Is.True, error);
                    Assert.That(player.currentCast, Is.SameAs(cast)); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(oldVersion));
                    draft.steps[0].seconds = 99;
                    Assert.That(arena.TryGetBasicSwordMotion(out var active, out error), Is.True, error); Assert.That(active.Seconds[0], Is.EqualTo(.12f), "Applied timing does not retain a mutable draft.");
                    var invalid = active.ToDocument(); invalid.steps[0].frame = "missing"; float now = arena.Now;
                    Assert.That(arena.TryApplyBasicSwordMotion(invalid, true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.currentCast, Is.SameAs(cast));
                    for (int i = 0; i < 4; i++) driver.Step(.02f); arena.Presentation.Draw(0);
                    Assert.That(cast.Age, Is.EqualTo(.1f).Within(.000001f)); Assert.That(player.visual.sprite, Is.SameAs(sprites[1]), "Running attack keeps its starting frame weights.");
                    for (int i = 0; i < 35; i++) { driver.Step(.02f); arena.Presentation.Draw(0); }
                    Assert.That(arena.enemies[0].health, Is.EqualTo(90)); Assert.That(cast.ImpactTime, Is.EqualTo(.2f)); Assert.That(cast.Definition.End, Is.EqualTo(.4f));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); for (int i = 0; i < 4; i++) driver.Step(.02f); arena.Presentation.Draw(0);
                    Assert.That(player.visual.sprite, Is.SameAs(sprites[0])); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.EqualTo(oldVersion));
                    Assert.That(player.currentCast.ImpactTime, Is.EqualTo(.2f)); Assert.That(player.currentCast.Definition.End, Is.EqualTo(.4f));
                    Assert.That(arena.TryApplyBasicSwordMotion(baseline.ToDocument(), true, out error), Is.True, error); Assert.That(player.currentCast, Is.Null); Assert.That(arena.Presentation.ActiveEffects, Is.Zero);
                }
            }
            finally { arena.Presentation.Reset(); foreach (var sprite in sprites) Object.Destroy(sprite); Object.Destroy(texture); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowUndoSaveReloadAndApplyAreSeparateAcrossPlayMode()
        {
            var window = ScriptableObject.CreateInstance<AttackMotionWindow>(); window.name = "AttackMotionTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); yield return null;
            var root = window.rootVisualElement; Assert.That(root.Query<FloatField>().ToList().Count(field => field.GetFirstAncestorOfType<ActionGeometryPreview>() == null), Is.EqualTo(7));
            string original = File.ReadAllText(source); string beforePreview = root.Q<Label>("preview").text;
            root.Q<FloatField>("seconds-0").value = .12f; yield return null;
            Assert.That(root.Q<Label>("preview").text, Is.Not.EqualTo(beforePreview)); Assert.That(root.Q<Label>("state").text, Does.Contain("미저장")); Assert.That(root.Q<Label>("diff").text, Does.Contain("sword-first-ready"));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null; Assert.That(root.Q<FloatField>("seconds-0").value, Is.EqualTo(.06f));
            root.Q<FloatField>("seconds-0").value = .12f; root.Q<FloatField>("fxStart").value = -1; yield return null;
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            window = Resources.FindObjectsOfTypeAll<AttackMotionWindow>().Single(w => w.name == "AttackMotionTestWindow"); root = window.rootVisualElement;
            Assert.That(root.Q<FloatField>("seconds-0").value, Is.EqualTo(.12f)); Assert.That(root.Q<FloatField>("fxStart").value, Is.EqualTo(-1));
            var arena = Object.FindAnyObjectByType<CombatArena>(); arena.characterAnimations = Generated();
            window.CreateGUI(); root = window.rootVisualElement; yield return null;
            Click(window, "applyNext"); Assert.That(arena.TryGetBasicSwordMotion(out var motion, out var error), Is.True, error); Assert.That(motion.Seconds[0], Is.EqualTo(.12f)); Assert.That(File.ReadAllText(source), Is.EqualTo(original));
            Click(window, "save"); Assert.That(AttackMotionSource.Load(source, out _).steps[0].seconds, Is.EqualTo(.12f)); Assert.That(root.Q<Label>("state").text, Does.Contain("저장된 원본과 동일"));
            root.Q<FloatField>("seconds-0").value = 0; Assert.That(root.Q<Button>("save").enabledSelf, Is.False); Assert.That(root.Q<Button>("applyNext").enabledSelf, Is.False);
            Click(window, "reload"); Assert.That(root.Q<FloatField>("seconds-0").value, Is.EqualTo(.12f)); Click(window, "applyReset"); Assert.That(arena.Player.currentCast, Is.Null);
            string saved = File.ReadAllText(source); yield return new ExitPlayMode(); yield return null; Assert.That(File.ReadAllText(source), Is.EqualTo(saved));
        }
        static void Click(AttackMotionWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
