using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Deviant.Authoring.Tests
{
    public class BasicSwordAuthoringTests
    {
        string repository, source;
        [SetUp] public void SetUp()
        {
            var original = new DirectoryInfo(Application.dataPath);
            while (original != null && !File.Exists(Path.Combine(original.FullName, "tools/combat/melee_source.py"))) original = original.Parent;
            Assert.That(original, Is.Not.Null);
            repository = Path.Combine(Path.GetTempPath(), "deviant-sword-" + Guid.NewGuid().ToString("N"));
            foreach (string file in Directory.GetFiles(Path.Combine(original.FullName, "data/combat"), "*.json", SearchOption.AllDirectories))
            {
                string target = Path.Combine(repository, Path.GetRelativePath(original.FullName, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target);
            }
            string combatPath = Path.Combine(repository, MotionRegistrySource.CombatPath);
            Directory.CreateDirectory(Path.GetDirectoryName(combatPath)); File.Copy(Path.Combine(original.FullName, MotionRegistrySource.CombatPath), combatPath);
            Directory.CreateDirectory(Path.Combine(repository, "tools/combat"));
            Directory.CreateDirectory(Path.Combine(repository, "docs/design/equipment/weapons/forms"));
            foreach (string name in new[] { "build_melee_design.py", "export_unity_catalog.py", "melee_source.py" })
                File.Copy(Path.Combine(original.FullName, "tools/combat", name), Path.Combine(repository, "tools/combat", name));
            source = Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var window in Resources.FindObjectsOfTypeAll<BasicSwordWindow>().Where(w => w.name == "BasicSwordTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }

        [Test] public void SaveRegenerateAndReloadChangeOnlyFirstHit()
        {
            var document = BasicSwordSource.Load(source, out var hash);
            document.hit.damage = 24; document.hit.duration = .6f; document.hit.impact = .35f; document.hit.active = .1f;
            document.hit.box.width = 8; document.hit.box.x = 4.1875f;
            var definition = BasicSwordSource.Validate(document);
            hash = BasicSwordSource.Save(repository, hash, document);
            BasicSwordSource.Regenerate(repository);
            var restored = BasicSwordSource.Load(source, out var reloadedHash);
            Assert.That(reloadedHash, Is.EqualTo(hash));
            Assert.That(BasicSwordSource.Validate(restored).ContentHash, Is.EqualTo(definition.ContentHash));
            var catalog = Generated(); var first = catalog.forms[0].basics[0];
            Assert.That(first.damage, Is.EqualTo(24)); Assert.That(first.duration, Is.EqualTo(.6f));
            Assert.That(first.impact, Is.EqualTo(.35f)); Assert.That(first.active, Is.EqualTo(.1f));
            Assert.That(first.box.width, Is.EqualTo(8));
            var baseline = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            catalog.forms[0].basics[0] = baseline.forms[0].basics[0];
            Assert.That(JsonUtility.ToJson(catalog), Is.EqualTo(JsonUtility.ToJson(baseline)), "All other content must be preserved.");
        }

        [Test] public void ConflictAndInvalidDocumentLeaveSourceUntouched()
        {
            var document = BasicSwordSource.Load(source, out var hash);
            File.AppendAllText(source, "\n"); string external = File.ReadAllText(source);
            Assert.Throws<IOException>(() => BasicSwordSource.Save(repository, hash, document));
            Assert.That(File.ReadAllText(source), Is.EqualTo(external));
            document = BasicSwordSource.Load(source, out hash); document.schemaVersion = 2;
            Assert.Throws<ArgumentException>(() => BasicSwordSource.Save(repository, hash, document));
            Assert.That(File.ReadAllText(source), Is.EqualTo(external));
        }

        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowEditsSavesReloadsAndAppliesWithoutImplicitSaving()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            UnityEngine.Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var window = ScriptableObject.CreateInstance<BasicSwordWindow>(); window.name = "BasicSwordTestWindow";
            window.Show(); window.CreateGUI(); window.LoadRepository(repository); yield return null;
            var root = window.rootVisualElement;
            Assert.That(root.Query<FloatField>().ToList().Count(field => field.GetFirstAncestorOfType<ActionGeometryPreview>() == null), Is.EqualTo(8));
            root.Q<FloatField>("duration").value = .6f;
            root.Q<FloatField>("impact").value = .35f;
            root.Q<FloatField>("width").value = 8;
            string before = File.ReadAllText(source);
            Click(window, "applyNext"); yield return null;
            var arena = UnityEngine.Object.FindAnyObjectByType<CombatArena>();
            Assert.That(arena.BasicSwordFirst.Impact, Is.EqualTo(.35f));
            Assert.That(arena.BasicSwordFirst.Width, Is.EqualTo(8));
            Assert.That(File.ReadAllText(source), Is.EqualTo(before));
            Click(window, "save"); yield return null;
            Assert.That(BasicSwordSource.Load(source, out _).hit.impact, Is.EqualTo(.35f));
            Assert.That(Generated().forms[0].basics[0].box.width, Is.EqualTo(8));
            root.Q<FloatField>("impact").value = 4;
            Assert.That(root.Q<Button>("save").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("applyReset").enabledSelf, Is.False);
            Click(window, "reload"); yield return null;
            Assert.That(root.Q<FloatField>("impact").value, Is.EqualTo(.35f));
            Click(window, "applyReset"); yield return null;
            Assert.That(arena.Player.currentCast, Is.Null);
        }

        Catalog Generated() => JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json")));
        static void Click(BasicSwordWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
