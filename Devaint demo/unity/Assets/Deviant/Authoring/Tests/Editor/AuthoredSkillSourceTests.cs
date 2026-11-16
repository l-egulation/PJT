using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Authoring.Tests
{
    public class AuthoredSkillSourceTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [TearDown] public void TearDown() { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        [Test] public void AllNineteenSourcesCompileAndSelectedNumericSaveRegeneratesSameDefinitions()
        {
            var values = AuthoredSkillSource.Load(repository, out var hash); Assert.That(values.Select(value => value.Id), Is.EqualTo(AuthoredSkillDefinition.SupportedIds));
            var selected = values.Where(value => value.Id == "whip_default" || value.Id == "dagger_range" || value.Id == "shield_default" || value.Id == "shield_follow_up" || value.Id == "rapier_follow_up").Select(value => {
                var data = value.ToNumericData(); data.end += .1f; if (data.hits.Length > 0) data.hits[0].damage += 1;
                if (value.Id == "dagger_range") data.effect.box.width = 4;
                if (value.Id == "shield_default") { data.effect.parryWindow = .3f; data.effect.parryDamage = 45; }
                if (value.Id == "shield_follow_up") { data.effect.box.width = 1.2f; data.effect.radius = 2; }
                if (value.Id == "rapier_follow_up") data.verticalFreeze = new[] { .2f, .5f };
                return value.Recompile(data);
            }).ToArray();
            var culture = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); hash = AuthoredSkillSource.Save(repository, hash, selected); } finally { CultureInfo.CurrentCulture = culture; }
            var reloaded = AuthoredSkillSource.Load(repository, out var afterHash); Assert.That(afterHash, Is.EqualTo(hash));
            foreach (var value in values) Assert.That(reloaded.Single(item => item.Id == value.Id).ContentHash, Is.EqualTo(selected.SingleOrDefault(item => item.Id == value.Id)?.ContentHash ?? value.ContentHash));
            BasicSwordSource.Regenerate(repository); var catalog = BasicMeleeSourceTests.Generated(repository); Assert.DoesNotThrow(() => AuthoredSkillSource.ValidateGenerated(repository, catalog));
            catalog.forms.SelectMany(form => form.specials).Single(value => value.id == "shield_default").effect.parryDamage += 1;
            Assert.Throws<ArgumentException>(() => AuthoredSkillSource.ValidateGenerated(repository, catalog));
        }
        [Test] public void InvalidMetadataDuplicateAndExternalChangesNeverOverwriteSource()
        {
            var values = AuthoredSkillSource.Load(repository, out var hash); string path = Path.Combine(repository, AuthoredSkillSource.RelativePath); byte[] before = File.ReadAllBytes(path);
            Assert.Throws<ArgumentException>(() => AuthoredSkillSource.Save(repository, hash, new[] { values[0], values[0] }));
            var data = values[0].ToData(); data.cooldown += 1; var changed = new AuthoredSkillDefinition(values[0].FormId, data); Assert.Throws<ArgumentException>(() => AuthoredSkillSource.Save(repository, hash, new[] { changed })); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            File.AppendAllText(path, "\n"); byte[] external = File.ReadAllBytes(path); Assert.Throws<IOException>(() => AuthoredSkillSource.Save(repository, hash, values)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(external));
            Assert.That(Directory.GetFiles(Path.Combine(repository, "data/combat"), "*.tmp"), Is.Empty);
        }
    }
}
