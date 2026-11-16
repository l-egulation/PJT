using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class BasicMeleeSourceTests
    {
        string repository;
        public static string CopyRepository()
        {
            var original = new DirectoryInfo(Application.dataPath);
            while (original != null && !File.Exists(Path.Combine(original.FullName, "tools/combat/melee_source.py"))) original = original.Parent;
            Assert.That(original, Is.Not.Null);
            string result = Path.Combine(Path.GetTempPath(), "deviant-melee-basic-" + Guid.NewGuid().ToString("N"));
            foreach (string file in Directory.GetFiles(Path.Combine(original.FullName, "data/combat"), "*.json", SearchOption.AllDirectories))
            {
                string target = Path.Combine(result, Path.GetRelativePath(original.FullName, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target);
            }
            string combatPath = Path.Combine(result, MotionRegistrySource.CombatPath);
            Directory.CreateDirectory(Path.GetDirectoryName(combatPath)); File.Copy(Path.Combine(original.FullName, MotionRegistrySource.CombatPath), combatPath);
            Directory.CreateDirectory(Path.Combine(result, "tools/combat"));
            Directory.CreateDirectory(Path.Combine(result, "docs/design/equipment/weapons/forms"));
            foreach (string name in new[] { "build_melee_design.py", "export_unity_catalog.py", "melee_source.py" })
                File.Copy(Path.Combine(original.FullName, "tools/combat", name), Path.Combine(result, "tools/combat", name));
            return result;
        }
        public static Catalog Generated(string repository) => JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json")));
        [SetUp] public void SetUp() { repository = CopyRepository(); }
        [TearDown] public void TearDown() { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        string SourcePath => Path.Combine(repository, BasicMeleeSource.RelativePath);

        [Test] public void SourceSelectsExactlyRemainingSixteenAndUsesCanonicalFirstSwordMapping()
        {
            var hits = BasicMeleeSource.Load(repository, out _);
            Assert.That(hits.Length, Is.EqualTo(16)); Assert.That(hits.Select(hit => hit.formId).Distinct().Count(), Is.EqualTo(7));
            Assert.That(hits.Any(hit => hit.Id == BasicSwordSource.ActionId), Is.False);
            var expected = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            Assert.That(BasicMeleeCatalog.TryCreate(expected, out var catalog, out var error), Is.True, error);
            foreach (var definition in BasicMeleeSource.Compile(hits))
                Assert.That(definition.ContentHash, Is.EqualTo(catalog.Actions.Single(hit => hit.Id == definition.Id).ContentHash));
            var root = Parse(File.ReadAllText(SourcePath));
            Form(root, "base_sword").Element("basic").Element("damages").Elements().First().Value = "25";
            Form(root, "base_sword").Element("basic").Element("damages").Elements().First().SetAttributeValue("type", "number");
            Store(SourcePath, root);
            Assert.Throws<ArgumentException>(() => BasicMeleeSource.Load(repository, out _), "Mapped source slots must remain null.");
        }

        [Test] public void SaveRegenerateReloadPreservesUnknownFieldsAndAllUneditedConsumers()
        {
            var root = Parse(File.ReadAllText(SourcePath));
            root.Add(new XElement("unknown_metadata", new XAttribute("type", "object"),
                new XElement("caption", new XAttribute("type", "string"), "유지 & <원본>"),
                new XElement("whitespace", new XAttribute("type", "string"), "  \t "),
                new XElement("large_number", new XAttribute("type", "number"), "12345678901234567890.123456789"),
                new XElement("empty", new XAttribute("type", "null")),
                new XElement("empty_object", new XAttribute("type", "object")),
                new XElement("empty_array", new XAttribute("type", "array")),
                new XElement("values", new XAttribute("type", "array"),
                    new XElement("item", new XAttribute("type", "null")),
                    new XElement("item", new XAttribute("type", "object")),
                    new XElement("item", new XAttribute("type", "array")),
                    new XElement("item", new XAttribute("type", "string"), " \t "))));
            Form(root, "base_sword").Element("basic").Add(new XElement("unknown_basic", new XAttribute("type", "string"), "기존 기본 메모"));
            Form(root, "dagger").Element("basic").Element("shapes").Elements().Last().Add(new XElement("unknown_shape", new XAttribute("type", "boolean"), "true"));
            Store(SourcePath, root, true);
            root = Parse(File.ReadAllText(SourcePath));
            byte[] action = File.ReadAllBytes(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"));
            byte[] references = File.ReadAllBytes(Path.Combine(repository, "data/combat/basic_sword_attack.json"));
            var baseline = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var hits = BasicMeleeSource.Load(repository, out var hash);
            var sword = hits.Single(hit => hit.formId == "base_sword"); sword.hit.damage = 21; sword.hit.duration = .72f; sword.hit.impact = .31f;
            var dagger = hits.Single(hit => hit.formId == "dagger" && hit.hitIndex == 3); dagger.hit.damage = 19; dagger.hit.box.width += .25f;
            var greatsword = hits.Single(hit => hit.formId == "greatsword" && hit.hitIndex == 0); greatsword.hit.box.width += .5f;
            foreach (var hit in hits.Where(hit => hit.formId == "rapier")) hit.hit.active = .07f;
            var definitions = BasicMeleeSource.Compile(hits);
            var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); hash = BasicMeleeSource.Save(repository, hash, hits); }
            finally { CultureInfo.CurrentCulture = culture; }
            Assert.That(File.ReadAllBytes(SourcePath).Take(3), Is.Not.EqualTo(new byte[] { 239, 187, 191 }), "Writes use UTF-8 without a BOM.");
            var reloaded = BasicMeleeSource.Load(repository, out var reloadedHash);
            Assert.That(reloadedHash, Is.EqualTo(hash));
            Assert.That(BasicMeleeSource.Compile(reloaded).Select(hit => hit.ContentHash), Is.EqualTo(definitions.Select(hit => hit.ContentHash)));
            var after = Parse(File.ReadAllText(SourcePath));
            Assert.That(XNode.DeepEquals(Form(after, "base_sword").Element("basic").Element("unknown_basic"), Form(root, "base_sword").Element("basic").Element("unknown_basic")), Is.True);
            Assert.That(XNode.DeepEquals(Form(after, "dagger").Element("basic").Element("shapes").Elements().Last().Element("unknown_shape"), Form(root, "dagger").Element("basic").Element("shapes").Elements().Last().Element("unknown_shape")), Is.True);
            foreach (string id in new[] { "base_sword", "dagger", "greatsword", "rapier" })
                Form(after, id).Element("basic").ReplaceWith(new XElement(Form(root, id).Element("basic")));
            Assert.That(XNode.DeepEquals(after, root), Is.True, "All non-basic fields, unknown metadata, null mappings and number tokens are preserved.");
            BasicSwordSource.Regenerate(repository);
            var generated = Generated(repository);
            foreach (var form in generated.forms)
                for (int index = 0; index < form.basics.Length; index++)
                {
                    var candidate = definitions.SingleOrDefault(hit => hit.Id == form.id + "_basic_" + index);
                    if (candidate != null)
                    {
                        Assert.That(BasicMeleeDefinition.TryCreate(candidate.Id, form.basics[index], out var actual, out var error), Is.True, error);
                        Assert.That(actual.ContentHash, Is.EqualTo(candidate.ContentHash));
                        form.basics[index] = baseline.forms.Single(item => item.id == form.id).basics[index];
                    }
                }
            Assert.That(JsonUtility.ToJson(generated), Is.EqualTo(JsonUtility.ToJson(baseline)), "First sword, specials, ranged and every other consumer are unchanged.");
            Assert.That(File.ReadAllBytes(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json")), Is.EqualTo(action));
            Assert.That(File.ReadAllBytes(Path.Combine(repository, "data/combat/basic_sword_attack.json")), Is.EqualTo(references));
            Assert.That(Directory.GetFiles(repository, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test] public void ConflictAndInvalidWholeCandidateLeaveSourceAndHashUntouched()
        {
            var hits = BasicMeleeSource.Load(repository, out var hash);
            File.AppendAllText(SourcePath, "\n"); byte[] external = File.ReadAllBytes(SourcePath);
            Assert.Throws<IOException>(() => BasicMeleeSource.Save(repository, hash, hits));
            Assert.That(File.ReadAllBytes(SourcePath), Is.EqualTo(external));
            hits = BasicMeleeSource.Load(repository, out hash);
            hits[0].hit.damage += 1; hits[hits.Length - 1].hit.box.width = 0;
            Assert.Throws<ArgumentException>(() => BasicMeleeSource.Save(repository, hash, hits));
            Assert.That(File.ReadAllBytes(SourcePath), Is.EqualTo(external), "An invalid later hit cannot publish earlier edits.");
            hits = BasicMeleeSource.Load(repository, out hash);
            hits.Single(hit => hit.formId == "rapier" && hit.hitIndex == 1).hit.active += .01f;
            Assert.Throws<ArgumentException>(() => BasicMeleeSource.Save(repository, hash, hits), "Shared active_seconds cannot become divergent per-hit values.");
            hits = BasicMeleeSource.Load(repository, out hash);
            hits.First(hit => hit.rootSymmetric).hit.box.x = 1;
            Assert.Throws<ArgumentException>(() => BasicMeleeSource.Save(repository, hash, hits));
            Assert.That(File.ReadAllBytes(SourcePath), Is.EqualTo(external));
            hits = BasicMeleeSource.Load(repository, out hash);
            File.Delete(SourcePath); Assert.Catch<IOException>(() => BasicMeleeSource.Save(repository, hash, hits));
            Assert.That(File.Exists(SourcePath), Is.False);
        }

        [Test] public void ImmutableCatalogPublishesReferencesOnlyAfterEveryReplacementIsValid()
        {
            var source = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            Assert.That(BasicMeleeCatalog.TryCreate(source, out var catalog, out var error), Is.True, error);
            var old = catalog.Find("dagger", 0); var data = old.ToData(); data.damage = 17;
            Assert.That(BasicMeleeDefinition.TryCreate(old.Id, data, out var edited, out error), Is.True, error);
            Assert.That(catalog.TryReplace(new[] { edited, null }, out var rejected, out error), Is.False); Assert.That(rejected, Is.Null);
            Assert.That(catalog.TryReplace(new[] { edited, edited }, out rejected, out error), Is.False); Assert.That(rejected, Is.Null);
            Assert.That(catalog.TryReplace(new[] { edited }, out var next, out error), Is.True, error);
            data.damage = 500;
            Assert.That(catalog.Find("dagger", 0), Is.SameAs(old)); Assert.That(next.Find("dagger", 0), Is.SameAs(edited)); Assert.That(edited.Damage, Is.EqualTo(17));
            foreach (var unchanged in catalog.Actions.Where(hit => hit.Id != edited.Id)) Assert.That(next.Actions.Single(hit => hit.Id == unchanged.Id), Is.SameAs(unchanged));
        }

        static XElement Form(XElement root, string id) => root.Element("forms").Elements().Single(form => form.Element("id").Value == id);
        static XElement Parse(string json)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json.TrimStart('\uFEFF')), XmlDictionaryReaderQuotas.Max)) return XElement.Load(reader, LoadOptions.PreserveWhitespace);
        }
        static void Store(string path, XElement root, bool bom = false)
        {
            var document = new XElement(root);
            foreach (var node in document.DescendantsAndSelf())
                if ((string)node.Attribute("type") == "null" || (string)node.Attribute("type") == "object" || (string)node.Attribute("type") == "array")
                {
                    if (!node.HasElements) node.RemoveNodes();
                    else foreach (var text in node.Nodes().OfType<XText>().ToArray()) text.Remove();
                }
            using (var stream = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), false, true, "  ")) document.WriteTo(writer);
                File.WriteAllText(path, Encoding.UTF8.GetString(stream.ToArray()) + "\n", new UTF8Encoding(bom));
            }
        }
    }
}
