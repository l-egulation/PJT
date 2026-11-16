using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Authoring.Tests
{
    public class DirectMeleeSpecialSourceTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [TearDown] public void TearDown() { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        [Test] public void CurrentCanonicalSourceCompilesExactlyExistingThreeAndMatchesStartupCatalog()
        {
            var values = DirectMeleeSpecialSource.Load(repository, out var hash);
            Assert.That(values.Select(value => value.Id), Is.EqualTo(DirectMeleeSpecialDefinition.SupportedIds)); Assert.That(hash.Length, Is.EqualTo(64));
            foreach (var value in values) Assert.That(value.Recompile(value.ToBasicData()).ContentHash, Is.EqualTo(value.ContentHash));
        }
        [Test] public void SingleCanonicalNumericSaveRegeneratesWithoutInitialReferencePinAndPreservesOtherActions()
        {
            var values = DirectMeleeSpecialSource.Load(repository, out var hash); var source = values[0]; var data = source.ToBasicData();
            data.damage = 42; data.impact = .35f; data.active = .12f; data.duration = .85f; data.box.width += .5f; data.box.x += .25f;
            var candidate = source.Recompile(data); string initialPath = Path.Combine(repository, "data/combat/basic_sword_special.json"); byte[] initial = File.ReadAllBytes(initialPath);
            var before = DirectMeleeSpecialSource.Load(repository, out _); var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); hash = DirectMeleeSpecialSource.Save(repository, hash, new[] { candidate }); }
            finally { CultureInfo.CurrentCulture = culture; }
            var after = DirectMeleeSpecialSource.Load(repository, out var reloadedHash);
            Assert.That(reloadedHash, Is.EqualTo(hash)); Assert.That(after[0].ContentHash, Is.EqualTo(candidate.ContentHash));
            Assert.That(after.Skip(1).Select(value => value.ContentHash), Is.EqualTo(before.Skip(1).Select(value => value.ContentHash)));
            Assert.That(File.ReadAllBytes(initialPath), Is.EqualTo(initial), "Initial approved regression provenance is not a second active numeric source.");
            BasicSwordSource.Regenerate(repository); var generated = BasicMeleeSourceTests.Generated(repository);
            Assert.DoesNotThrow(() => DirectMeleeSpecialSource.ValidateGenerated(repository, generated));
            var changed = generated.forms.SelectMany(form => form.specials).Single(value => value.id == source.Id); changed.hits[0].damage += 1;
            Assert.Throws<ArgumentException>(() => DirectMeleeSpecialSource.ValidateGenerated(repository, generated));
            Assert.That(Directory.GetFiles(Path.Combine(repository, "data/combat"), "*.tmp"), Is.Empty);
        }
        [Test] public void InvalidWholeNumericMetadataAndExternalEditPreserveExistingFile()
        {
            var values = DirectMeleeSpecialSource.Load(repository, out var hash); string path = Path.Combine(repository, DirectMeleeSpecialSource.RelativePath); byte[] before = File.ReadAllBytes(path);
            Assert.Throws<ArgumentException>(() => DirectMeleeSpecialSource.Save(repository, hash, new[] { values[0], values[0] }));
            var data = values[2].ToBasicData(); data.box.x = 1; Assert.Throws<ArgumentException>(() => values[2].Recompile(data));
            var changed = values[0].ToData(); changed.cooldown += 1; Assert.That(DirectMeleeSpecialDefinition.TryCreate(values[0].FormId, changed, out var otherPolicy, out var error), Is.True, error);
            Assert.Throws<ArgumentException>(() => DirectMeleeSpecialSource.Save(repository, hash, new[] { otherPolicy })); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            File.AppendAllText(path, "\n"); byte[] external = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => DirectMeleeSpecialSource.Save(repository, hash, values)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(external));
        }
        [Test] public void InitialReferenceMustDeclareTheOneActiveNumericSource()
        {
            string reference = Path.Combine(repository, "data/combat/basic_sword_special.json");
            File.WriteAllText(reference, File.ReadAllText(reference).Replace("melee_weapon_design.json#base_sword_default", "actions/not-this-source.json"));
            Assert.Throws<ArgumentException>(() => DirectMeleeSpecialSource.Load(repository, out _));
        }
    }
}
