using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;

namespace Deviant.Authoring.Tests
{
    public class ChargedMeleeSourceTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [TearDown] public void TearDown() { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        [Test] public void CanonicalReleasePoliciesMatchGeneratedContentAndSurviveNumericSaves()
        {
            var values = ChargedMeleeSource.Load(repository, out var hash);
            var sword = values.Single(value => value.Id == "greatsword_default");
            var rapier = values.Single(value => value.Id == "rapier_default");
            Assert.That(sword.Policy.HoldUntilRelease, Is.True);
            Assert.That(sword.Policy.ReleaseWindup, Is.EqualTo(.18f));
            Assert.That(rapier.Policy.HoldUntilRelease, Is.False);
            Assert.That(rapier.Policy.ReleaseWindup, Is.Zero);
            Assert.DoesNotThrow(() => ChargedMeleeSource.ValidateGenerated(repository, BasicMeleeSourceTests.Generated(repository)));
            var numeric = sword.ToNumericData(); numeric.minimumDamage += 1;
            var candidate = sword.Recompile(numeric);
            ChargedMeleeSource.Save(repository, hash, new[] { candidate });
            var saved = ChargedMeleeSource.Load(repository, out _).Single(value => value.Id == sword.Id);
            Assert.That(saved.Policy.HoldUntilRelease, Is.True);
            Assert.That(saved.Policy.ReleaseWindup, Is.EqualTo(.18f));
            Assert.That(saved.ContentHash, Is.EqualTo(candidate.ContentHash));
            BasicSwordSource.Regenerate(repository);
            Assert.DoesNotThrow(() => ChargedMeleeSource.ValidateGenerated(repository, BasicMeleeSourceTests.Generated(repository)));
        }
        [Test] public void SelectedChargeSaveUpdatesEveryCanonicalReferenceAndSameGeneratedCompiler()
        {
            var values = ChargedMeleeSource.Load(repository, out var hash); Assert.That(values.Select(value => value.Id), Is.EqualTo(ChargedMeleeDefinition.SupportedIds));
            var numeric = values[0].ToNumericData(); numeric.minimum += .03f; numeric.maximum += .06f; numeric.minimumDamage += 7; numeric.maximumDamage += 11; numeric.baseEnd += .1f; numeric.active += .01f; numeric.box.height += .25f;
            var candidate = values[0].Recompile(numeric); var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); hash = ChargedMeleeSource.Save(repository, hash, new[] { candidate }); } finally { CultureInfo.CurrentCulture = culture; }
            var after = ChargedMeleeSource.Load(repository, out var reloadedHash); Assert.That(reloadedHash, Is.EqualTo(hash)); Assert.That(after[0].ContentHash, Is.EqualTo(candidate.ContentHash)); Assert.That(after[1].ContentHash, Is.EqualTo(values[1].ContentHash));
            BasicSwordSource.Regenerate(repository); var generated = BasicMeleeSourceTests.Generated(repository); Assert.DoesNotThrow(() => ChargedMeleeSource.ValidateGenerated(repository, generated));
            var payload = generated.forms.SelectMany(form => form.specials).Single(value => value.id == candidate.Id);
            Assert.That(payload.activation, Is.EqualTo(numeric.minimum)); Assert.That(payload.hits[0].time, Is.EqualTo(numeric.minimum)); Assert.That(payload.hits[0].damage, Is.EqualTo(numeric.minimumDamage));
            Assert.That(payload.chargeMin, Is.EqualTo(numeric.minimum)); Assert.That(payload.chargeMax, Is.EqualTo(numeric.maximum)); Assert.That(payload.hits[0].active, Is.EqualTo(numeric.active));
            payload.chargeMaxDamage += 1; Assert.Throws<ArgumentException>(() => ChargedMeleeSource.ValidateGenerated(repository, generated));
            Assert.That(Directory.GetFiles(Path.Combine(repository, "data/combat"), "*.tmp"), Is.Empty);
        }
        [Test] public void InvalidMetadataOrExternalEditCannotReplaceCanonicalSource()
        {
            var values = ChargedMeleeSource.Load(repository, out var hash); string path = Path.Combine(repository, ChargedMeleeSource.RelativePath); byte[] before = File.ReadAllBytes(path);
            Assert.Throws<ArgumentException>(() => ChargedMeleeSource.Save(repository, hash, new[] { values[0], values[0] }));
            var payload = values[0].ToData(); payload.cooldown += 1; Assert.That(ChargedMeleeDefinition.TryCreate(values[0].FormId, payload, out var changed, out var error), Is.True, error);
            Assert.Throws<ArgumentException>(() => ChargedMeleeSource.Save(repository, hash, new[] { changed })); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            File.AppendAllText(path, "\n"); var external = File.ReadAllBytes(path); Assert.Throws<IOException>(() => ChargedMeleeSource.Save(repository, hash, values)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(external));
        }
        [Test] public void UnchangedSavePreservesInitialAnnotationFloatTokens()
        {
            var values = ChargedMeleeSource.Load(repository, out var hash); string path = Path.Combine(repository, ChargedMeleeSource.RelativePath);
            ChargedMeleeSource.Save(repository, hash, values); string after = File.ReadAllText(path);
            Assert.That(after, Does.Contain("\"recovery\": 0.25").And.Contain("\"max_motion\": 1.13"));
        }
    }
}
