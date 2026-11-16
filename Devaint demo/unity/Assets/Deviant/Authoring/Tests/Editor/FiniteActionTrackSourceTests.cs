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

namespace Deviant.Authoring.Tests
{
    public class FiniteActionTrackSourceTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [TearDown] public void TearDown() { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        string Pathname => Path.Combine(repository, FiniteActionTrackSource.RelativePath);
        static XElement Parse(string json) { using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max)) return XElement.Load(reader); }
        static XElement[] Specials(XElement root) => root.Element("forms").Elements().SelectMany(form => form.Element("specials").Elements()).ToArray();
        internal static ActionTrackDefinition[] Edit(ActionTrackDefinition[] original, float delay = .03f)
        {
            return original.Select(source => {
                var document = source.ToDocument(); document.timeline.frames[0].seconds += delay;
                if (document.hits.Length != 0) document.hits[0].box.width += .25f;
                return source.Recompile(document);
            }).ToArray();
        }
        internal static ActionTrackDefinition NumericEdit(ActionTrackDefinition source)
        {
            var numeric = source.ToNumericData();
            for (int index = 0; index < numeric.hits.Length; index++) numeric.hits[index].damage += 7 + index;
            if (source.Id == ActionTrackDefinition.MovementId) { numeric.dashSpeed = 12; numeric.wallRecovery = .4f; }
            if (source.Id == ActionTrackDefinition.WaveId) { numeric.waveSpeed = 10; numeric.waveDistance = 6; numeric.waveSpawnX = 2; numeric.waveSpawnY = 1.25f; }
            if (source.Id == ActionTrackDefinition.GuardId) { numeric.guardHp = 45; numeric.guardReduction = .25f; }
            return source.Recompile(source.ToDocument(), numeric);
        }
        [Test] public void SourceContainsEveryMigratedActionWithoutInventingInvulnerabilityOrCueContent()
        {
            var original = FiniteActionTrackSource.Load(repository, out var hash);
            Assert.That(original.Select(value => value.Id), Is.EqualTo(ActionTrackDefinition.SupportedIds));
            Assert.That(hash.Length, Is.EqualTo(64));
            Assert.That(original[0].Execution.UsesCompiledTracks, Is.True); Assert.That(original[1].Hits.Count, Is.EqualTo(2));
            Assert.That(original.All(value => value.Execution.InvulStart.Count == 0 && value.Execution.InvulEnd.Count == 0), Is.True);
        }
        [Test] public void AtomicSaveRegenerateReloadAndBuildGateUseTheSameCompilerWithoutSecondPythonClock()
        {
            var original = FiniteActionTrackSource.Load(repository, out var hash); var candidate = Edit(original);
            string before = File.ReadAllText(Pathname); var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); hash = FiniteActionTrackSource.Save(repository, hash, candidate); }
            finally { CultureInfo.CurrentCulture = culture; }
            var reloaded = FiniteActionTrackSource.Load(repository, out var reloadedHash);
            Assert.That(reloadedHash, Is.EqualTo(hash)); Assert.That(reloaded.Select(value => value.ContentHash), Is.EqualTo(candidate.Select(value => value.ContentHash)));
            var oldRoot = Parse(before); var newRoot = Parse(File.ReadAllText(Pathname));
            var oldSpecials = Specials(oldRoot); var newSpecials = Specials(newRoot);
            for (int index = 0; index < newSpecials.Length; index++)
                if (newSpecials[index].Element("action_tracks") != null) newSpecials[index].Element("action_tracks").ReplaceWith(new XElement(oldSpecials[index].Element("action_tracks")));
            Assert.That(XNode.DeepEquals(oldRoot, newRoot), Is.True, "Save replaces only action_tracks; legacy provenance, metadata, other actions and unknown fields remain unchanged.");
            BasicSwordSource.Regenerate(repository); var generated = BasicMeleeSourceTests.Generated(repository);
            Assert.DoesNotThrow(() => FiniteActionTrackSource.ValidateGenerated(repository, generated));
            var compiled = ActionTrackDefinition.SupportedIds.Select(id => new ActionTrackDefinition(generated.forms.SelectMany(form => form.specials).Single(skill => skill.id == id))).ToArray();
            Assert.That(compiled.Select(value => value.ContentHash), Is.EqualTo(candidate.Select(value => value.ContentHash)));
            var changed = compiled[0].ToDocument(); changed.timeline.frames[0].seconds += .01f;
            generated.forms.SelectMany(form => form.specials).Single(skill => skill.id == compiled[0].Id).actionTracks = changed;
            Assert.Throws<ArgumentException>(() => FiniteActionTrackSource.ValidateGenerated(repository, generated));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(Pathname), "*.tmp"), Is.Empty);
        }
        [Test] public void SelectedNumericAndTrackSavePreservesAllInactiveProvenanceAndUnselectedActions()
        {
            var current = FiniteActionTrackSource.Load(repository, out var hash);
            foreach (string id in ActionTrackDefinition.SupportedIds) {
                var before = Parse(File.ReadAllText(Pathname)); var source = current.Single(value => value.Id == id); var numeric = NumericEdit(source); var document = numeric.ToDocument(); document.timeline.frames[0].seconds += .01f; var candidate = numeric.Recompile(document);
                var culture = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); hash = FiniteActionTrackSource.Save(repository, hash, new[] { candidate }); } finally { CultureInfo.CurrentCulture = culture; }
                var reloaded = FiniteActionTrackSource.Load(repository, out var reloadedHash); Assert.That(reloadedHash, Is.EqualTo(hash));
                foreach (var value in current) Assert.That(reloaded.Single(item => item.Id == value.Id).ContentHash, Is.EqualTo(value.Id == id ? candidate.ContentHash : value.ContentHash));
                var after = Parse(File.ReadAllText(Pathname)); var oldNode = Specials(before).Single(value => value.Element("id").Value == id); var newNode = Specials(after).Single(value => value.Element("id").Value == id);
                newNode.Element("action_tracks").ReplaceWith(new XElement(oldNode.Element("action_tracks")));
                var oldHits = oldNode.Element("hits").Elements().ToArray(); var newHits = newNode.Element("hits").Elements().ToArray();
                for (int index = 0; index < oldHits.Length; index++) newHits[index].Element("damage").ReplaceWith(new XElement(oldHits[index].Element("damage")));
                if (id == ActionTrackDefinition.MovementId) foreach (string name in new[] { "speed", "wall_recovery" }) newNode.Element("movement").Element(name).ReplaceWith(new XElement(oldNode.Element("movement").Element(name)));
                if (id == ActionTrackDefinition.WaveId) foreach (string name in new[] { "speed", "distance", "right_spawn", "left_spawn" }) newNode.Element("independent_effect").Element(name).ReplaceWith(new XElement(oldNode.Element("independent_effect").Element(name)));
                if (id == ActionTrackDefinition.GuardId) foreach (string name in new[] { "shield_hp", "damage_reduction" }) newNode.Element("independent_effect").Element(name).ReplaceWith(new XElement(oldNode.Element("independent_effect").Element(name)));
                Assert.That(XNode.DeepEquals(before, after), Is.True, "Only the selected consumed numeric nodes and tracks may change: " + id);
                current = reloaded;
            }
            var root = Parse(File.ReadAllText(Pathname)); var wave = Specials(root).Single(value => value.Element("id").Value == ActionTrackDefinition.WaveId).Element("independent_effect");
            Assert.That(wave.Element("right_spawn").Elements().Select(value => value.Value), Is.EqualTo(new[] { "2", "1.25" })); Assert.That(wave.Element("left_spawn").Elements().Select(value => value.Value), Is.EqualTo(new[] { "-2", "1.25" }));
            BasicSwordSource.Regenerate(repository); var generated = BasicMeleeSourceTests.Generated(repository); Assert.DoesNotThrow(() => FiniteActionTrackSource.ValidateGenerated(repository, generated));
            foreach (var value in current) Assert.That(new ActionTrackDefinition(generated.forms.SelectMany(form => form.specials).Single(item => item.id == value.Id)).ContentHash, Is.EqualTo(value.ContentHash));
            generated.forms.SelectMany(form => form.specials).Single(value => value.id == ActionTrackDefinition.GuardId).effect.hp += 1; Assert.Throws<ArgumentException>(() => FiniteActionTrackSource.ValidateGenerated(repository, generated));
        }
        [Test] public void InvalidCandidateOrExternalEditCannotOverwriteTheCanonicalFile()
        {
            var original = FiniteActionTrackSource.Load(repository, out var hash); byte[] before = File.ReadAllBytes(Pathname);
            Assert.Throws<ArgumentException>(() => FiniteActionTrackSource.Save(repository, hash, new[] { original[0], original[0] }));
            Assert.That(File.ReadAllBytes(Pathname), Is.EqualTo(before));
            File.AppendAllText(Pathname, "\n"); byte[] external = File.ReadAllBytes(Pathname);
            Assert.Throws<IOException>(() => FiniteActionTrackSource.Save(repository, hash, Edit(original)));
            Assert.That(File.ReadAllBytes(Pathname), Is.EqualTo(external));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(Pathname), "*.tmp"), Is.Empty);
        }
        [Test] public void SourceDeletionAndStaleMetadataRejectWithoutCreatingATemporaryReplacement()
        {
            var original = FiniteActionTrackSource.Load(repository, out var hash); var altered = original[0].ToData(); altered.cooldown += 1;
            Assert.Throws<ArgumentException>(() => FiniteActionTrackSource.Save(repository, hash, new[] { new ActionTrackDefinition(altered), original[1] }));
            File.Delete(Pathname); Assert.Throws<FileNotFoundException>(() => FiniteActionTrackSource.Save(repository, hash, original));
            Assert.That(File.Exists(Pathname), Is.False);
        }
    }
}
