using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class ExperimentScenarioTests
    {
        [Test] public void InputBlockCompactionPreservesClimbAndSlotHeldTransitions()
        {
            var inputs = new[] {
                new CombatInput(climbAxis: 1, heldSpecials: CombatButtons.Weapon1Special),
                new CombatInput(climbAxis: 1, heldSpecials: CombatButtons.Weapon1Special),
                new CombatInput(climbAxis: 1, heldSpecials: CombatButtons.Weapon2Special),
                new CombatInput(climbAxis: -1, heldSpecials: CombatButtons.Weapon2Special)
            };
            var type = typeof(ExperimentScenarioWindow);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var compact = type.GetMethod("CompactInputs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var blocks = (List<ExperimentScenarioWindow.InputBlock>)compact.Invoke(null, new object[] { inputs });
            Assert.That(blocks.Count, Is.EqualTo(3)); Assert.That(blocks[0].ticks, Is.EqualTo(2));
            var window = ScriptableObject.CreateInstance<ExperimentScenarioWindow>();
            try
            {
                type.GetField("blocks", flags).SetValue(window, blocks);
                var expanded = (CombatInput[])type.GetMethod("ExpandInputs", flags).Invoke(window, null);
                Assert.That(expanded, Is.EqualTo(inputs));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }
        string directory;
        static Catalog CatalogContext => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        static string CatalogHash(Catalog catalog)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(catalog)))).Replace("-", "").ToLowerInvariant();
        }
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "deviant-scenario-source-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        internal static ExperimentVersions Versions()
        {
            var catalog = CatalogContext;
            Assert.That(BasicMeleeCatalog.TryCreate(catalog, out var basics, out var error), Is.True, error);
            var movement = JsonUtility.FromJson<LocomotionData>(Resources.Load<TextAsset>("PlayerLocomotion").text);
            Assert.That(LocomotionProfile.TryCreate(movement, out var profile, out error), Is.True, error);
            var animation = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var first = animation.sets.Single(set => set.form == "base_sword").clips.Single(clip => clip.name == "attack1");
            var motion = new FixedAttackMotion(new AttackMotionDocument { schemaVersion = 1, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FixedAttackMotion.Mode,
                sourceImpact = first.sourceImpact, fxStart = first.fxStart,
                steps = first.frames.Select((frame, index) => new AttackMotionStep { id = first.stepIds[index], frame = frame, seconds = first.seconds[index] }).ToArray() });
            var tracks = catalog.forms.SelectMany(form => form.specials).Where(source => ActionTrackDefinition.Supports(source.id)).Select(source => new ActionTrackDefinition(source));
            var direct = catalog.forms.SelectMany(form => form.specials.Where(skill => DirectMeleeSpecialDefinition.SupportedIds.Contains(skill.id)).Select(skill => {
                if (!DirectMeleeSpecialDefinition.TryCreate(form.id, skill, out var definition, out var reason)) throw new ArgumentException(reason);
                return definition;
            })).ToArray();
            var charged = catalog.forms.SelectMany(form => form.specials.Where(skill => ChargedMeleeDefinition.SupportedIds.Contains(skill.id)).Select(skill => {
                if (!ChargedMeleeDefinition.TryCreate(form.id, skill, out var definition, out var reason)) throw new ArgumentException(reason);
                return definition;
            })).ToArray();
            var authored = catalog.forms.SelectMany(form => form.specials.Where(skill => AuthoredSkillDefinition.SupportedIds.Contains(skill.id)).Select(skill => new AuthoredSkillDefinition(form.id, skill))).ToArray();
            Assert.That(RangedActionCatalog.TryCreate(catalog, out var ranged, out error), Is.True, error);
            var registry = new MotionSourceRegistry(animation.motionRegistry, catalog);
            var frames = new MeleeFrameCatalog(animation.meleeFrames, catalog, registry, motion, null, basics.Actions, direct);
            var specialFrames = new ActionSpecialFrameCatalog(animation.specialFrames, catalog, registry, motion, tracks, authored);
            return new ExperimentVersions(CatalogHash(catalog), new string('b', 64), profile, basics.Actions, motion, null, tracks, direct, charged, authored, ranged, registry, frames, specialFrames);
        }
        internal static MeleeFrameCatalog RebuildFrames(ExperimentVersions source, System.Collections.Generic.IEnumerable<BasicMeleeDefinition> basics = null,
            System.Collections.Generic.IEnumerable<DirectMeleeSpecialDefinition> direct = null, MotionSourceRegistry registry = null, MeleeFrameDefinition first = null)
            => new MeleeFrameCatalog(source.MeleeFrames.ToSourceDocument(), CatalogContext, registry ?? source.MotionRegistry, source.Motion, first ?? source.Frame,
                basics ?? source.Basics, direct ?? source.DirectSpecials);
        internal static ActionSpecialFrameCatalog RebuildSpecialFrames(ExperimentVersions source, System.Collections.Generic.IEnumerable<ActionTrackDefinition> finite = null,
            System.Collections.Generic.IEnumerable<AuthoredSkillDefinition> authored = null, MotionSourceRegistry registry = null)
            => new ActionSpecialFrameCatalog(source.SpecialFrames.ToSourceDocument(), CatalogContext, registry ?? source.MotionRegistry, source.Motion,
                finite ?? source.ActionTracks, authored ?? source.AuthoredSkills);
        internal static ExperimentVersions WithSpecialFrames(ExperimentVersions source, ActionSpecialFrameCatalog frames)
            => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks,
                source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames, frames);
        internal static ActionSpecialFrameCatalog ShiftSpecialFrames(ExperimentVersions source, string id, float seconds = .03125f)
        {
            var original = source.SpecialFrames.Find(id);
            var changed = original.Family == SpecialFrameFamily.FiniteTracks
                ? ActionSpecialFrameCompiler.ProjectFinite(original.NumericFinite, original.FormId, original.Media)
                : ActionSpecialFrameCompiler.ProjectAuthored(original.NumericAuthored, original.Media);
            changed.timeline.frames[0].seconds += seconds;
            var document = source.SpecialFrames.ToDocument(); document.actions[Array.FindIndex(document.actions, value => value.actionId == id)] = changed;
            return new ActionSpecialFrameCatalog(document, CatalogContext, source.MotionRegistry, source.Motion, source.ActionTracks, source.AuthoredSkills);
        }
        internal static ExperimentInitialState Initial(ExperimentVersions versions = null, int? seed = null)
        {
            var actors = new ExperimentActorInitial[4];
            for (int i = 0; i < actors.Length; i++) actors[i] = new ExperimentActorInitial(i == 0 ? ExperimentInitialState.PlayerKey : ExperimentInitialState.EnemyKey(i - 1), i, i - 1,
                i * 3, i, i * 4, 0, i % 2 == 0 ? 1 : -1, 100, 75, 2, .5f, .25f, i == 1, i == 3, 1, 2, .1f, 1, i == 1 ? 7.5f : i == 2 ? -3.25f : 0);
            return new ExperimentInitialState("lab/source-fixture", new ExperimentRuntimeProvenance("fixture-unity", "fixture-editor", "fixture-cpu", 64,
                new[] { new ExperimentCodeModule("fixture-runtime", new Guid("18acf3ce-78bc-4b4f-bf03-344e19011d76")) }, new ExperimentQuerySettings(true, true, false, .01f)), new ExperimentGeometry(new[] {
                new ExperimentBox("surface/ground", 0, -.5f, 40, 1, false, false, 0), new ExperimentBox("pit/recovery", 0, -6, 70, 3, false, true, 0) }),
                actors, new[] { new ExperimentWeaponInitial("base_sword", "base_sword_default", new[] { 1, 2, 3, 4, 0 }), new ExperimentWeaponInitial("bomb", "bomb_default", new[] { 0, 0, 0, 0, 0 }) }, 1,
                versions ?? Versions(), seed, seed.HasValue ? "explicit test fixture seed; no RNG" : "");
        }
        static ExperimentScenario Scenario(int count = 3)
        {
            var initial = Initial(seed: 123); var samples = new CombatInput[count];
            samples[0] = new CombatInput(-1, CombatButtons.Basic | CombatButtons.Jump, true, CombatButtons.Special, 1, CombatButtons.Weapon2Special);
            return new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .017f, samples));
        }
        [Test] public void FreshAtomicSaveReloadPreservesCompleteInitialInputAndEveryHash()
        {
            var scenario = Scenario(); string path = Path.Combine(directory, "scenario.json");
            string hash = ExperimentScenarioSource.Save(path, null, scenario, CatalogContext); var loaded = ExperimentScenarioSource.Load(path, CatalogContext, out var actual);
            Assert.That(actual, Is.EqualTo(hash)); Assert.That(loaded.ContentHash, Is.EqualTo(scenario.ContentHash));
            Assert.That(loaded.Initial.ContentHash, Is.EqualTo(scenario.Initial.ContentHash)); Assert.That(loaded.Initial.Geometry.ContentHash, Is.EqualTo(scenario.Initial.Geometry.ContentHash));
            Assert.That(loaded.Initial.Versions.ContentHash, Is.EqualTo(scenario.Initial.Versions.ContentHash)); Assert.That(loaded.Recording.ContentHash, Is.EqualTo(scenario.Recording.ContentHash));
            Assert.That(loaded.Initial.Actors.Select(actor => (actor.Key, actor.Profile, actor.Order, actor.X, actor.Y, actor.Facing, actor.Health, actor.Shield, actor.SpawnX)),
                Is.EqualTo(scenario.Initial.Actors.Select(actor => (actor.Key, actor.Profile, actor.Order, actor.X, actor.Y, actor.Facing, actor.Health, actor.Shield, actor.SpawnX))));
            Assert.That(loaded.Initial.Actors.Select(actor => actor.InactiveKnockSpeed), Is.EqualTo(scenario.Initial.Actors.Select(actor => actor.InactiveKnockSpeed)));
            Assert.That(loaded.Initial.Weapons[0].StatSlots, Is.EqualTo(new[] { 1, 2, 3, 4, 0 })); Assert.That(loaded.Initial.Seed, Is.EqualTo(123));
            Assert.That(loaded.Recording.TickCount, Is.EqualTo(3)); Assert.That(loaded.Recording.StepSeconds, Is.EqualTo(.017f));
            Assert.That(loaded.Recording.Inputs[0].Released, Is.EqualTo(CombatButtons.Special));
            Assert.That(loaded.Recording.Inputs[0].ClimbAxis, Is.EqualTo(1));
            Assert.That(loaded.Recording.Inputs[0].HeldSpecials, Is.EqualTo(CombatButtons.Weapon2Special));
            Assert.That(File.ReadAllText(path), Is.EqualTo(ExperimentScenarioSource.Serialize(loaded)));
            Assert.That(File.ReadAllBytes(path).Take(3), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf })); Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void CanonicalHashAndJsonAreCultureIndependentAndSnapshotsDefendCollections()
        {
            var first = Scenario(); var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Assert.That(ExperimentScenarioSource.Serialize(Scenario()), Is.EqualTo(ExperimentScenarioSource.Serialize(first))); }
            finally { CultureInfo.CurrentCulture = culture; }
            Assert.Throws<NotSupportedException>(() => ((IList<ExperimentActorInitial>)first.Initial.Actors)[0] = first.Initial.Actors[1]);
            Assert.Throws<NotSupportedException>(() => ((IList<int>)first.Initial.Weapons[0].StatSlots)[0] = 4);
            Assert.That(typeof(ExperimentScenario).Assembly.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("UnityEngine") || reference.Name.StartsWith("UnityEditor") || reference.Name == "Deviant.Combat"), Is.False);
        }
        [TestCase(false)] [TestCase(true)] public void ExternalModificationOrDeletionNeverOverwritesSource(bool delete)
        {
            string path = Path.Combine(directory, "scenario.json"); string hash = ExperimentScenarioSource.Save(path, null, Scenario(), CatalogContext);
            if (delete) File.Delete(path); else File.WriteAllText(path, "external editor data");
            Assert.Catch<IOException>(() => ExperimentScenarioSource.Save(path, hash, Scenario(2), CatalogContext));
            if (delete) Assert.That(File.Exists(path), Is.False); else Assert.That(File.ReadAllText(path), Is.EqualTo("external editor data"));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void CreateDoesNotOverwriteAndExpectedHashReplacesAtomically()
        {
            string path = Path.Combine(directory, "scenario.json"); string hash = ExperimentScenarioSource.Save(path, null, Scenario(), CatalogContext);
            Assert.Throws<IOException>(() => ExperimentScenarioSource.Save(path, null, Scenario(2), CatalogContext));
            var next = Scenario(2); string replaced = ExperimentScenarioSource.Save(path, hash, next, CatalogContext);
            Assert.That(replaced, Is.Not.EqualTo(hash)); Assert.That(ExperimentScenarioSource.Load(path, CatalogContext, out _).ContentHash, Is.EqualTo(next.ContentHash));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [TestCase("\"axis\": -1", "\"axis\": 2")]
        [TestCase("\"pressed\": 9", "\"pressed\": 1024")]
        [TestCase("\"released\": 2", "\"released\": 1024")]
        [TestCase("\"health\": 75.0", "\"health\": 74.0")]
        public void InvalidOrTamperedWholeDocumentsRejectBeforePublication(string before, string after)
        {
            string json = ExperimentScenarioSource.Serialize(Scenario()); Assert.That(json.Contains(before), Is.True, before);
            Assert.Catch<ArgumentException>(() => ExperimentScenarioSource.Deserialize(ChangeFirst(json, before, after), CatalogContext));
        }
        [Test] public void UnsupportedFutureSchemaRejectsBeforePublication()
        {
            string json = ExperimentScenarioSource.Serialize(Scenario());
            string changed = ChangeFirst(json, "\"schemaVersion\": " + ExperimentScenarioSource.SchemaVersion, "\"schemaVersion\": 2147483647");
            Assert.That(changed, Is.Not.EqualTo(json));
            Assert.Throws<ArgumentException>(() => ExperimentScenarioSource.Deserialize(changed, CatalogContext));
        }
        [Test] public void ContentHashesIncludeTerrainGearPlacementVersionAndSeedProvenance()
        {
            var initial = Initial(); var changedBox = new ExperimentGeometry(initial.Geometry.Boxes.Select(box => new ExperimentBox(box.Key, box.X + 1, box.Y, box.Width, box.Height, box.OneWay, box.Pit, box.Layer)));
            var changed = new ExperimentInitialState(initial.Id, initial.Provenance, changedBox, initial.Actors, initial.Weapons, initial.SelectedWeapon, initial.Versions);
            Assert.That(changed.ContentHash, Is.Not.EqualTo(initial.ContentHash)); Assert.That(Initial(seed: 1).ContentHash, Is.Not.EqualTo(initial.ContentHash));
            Assert.Throws<ArgumentException>(() => new ExperimentInitialState(initial.Id, initial.Provenance, initial.Geometry, initial.Actors, initial.Weapons, 0, initial.Versions, 1, ""));
            Assert.Throws<ArgumentException>(() => new ExperimentInitialState(initial.Id, initial.Provenance, initial.Geometry, initial.Actors, initial.Weapons, 0, initial.Versions, null, "unbacked seed"));
        }
        [Test] public void ArbitraryBoundedRecordingsAcceptOneAnd4096TicksButRejectInvalidWholeLengthsAndSteps()
        {
            var initial = Initial();
            foreach (int length in new[] { 1, 7, 4096 }) Assert.That(new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .013f, new CombatInput[length])).Recording.TickCount, Is.EqualTo(length));
            Assert.Throws<ArgumentException>(() => new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .02f, new CombatInput[4097])));
            Assert.Throws<ArgumentException>(() => new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .00001f, new CombatInput[1])));
            Assert.Throws<ArgumentException>(() => new ExperimentScenario(initial, new CombatInputRecording("other/scenario", .02f, new CombatInput[1])));
        }
        [Test] public void BoundedReadAndDeserializeRejectOversizedFilesBeforeParsing()
        {
            string path = Path.Combine(directory, "oversized.json"); File.WriteAllBytes(path, new byte[ExperimentScenarioSource.MaximumFileBytes + 1]);
            Assert.Throws<IOException>(() => ExperimentScenarioSource.Load(path, CatalogContext, out _));
            Assert.Throws<ArgumentException>(() => ExperimentScenarioSource.Deserialize(new string('x', ExperimentScenarioSource.MaximumFileBytes + 1), CatalogContext));
        }
        [Test] public void CompleteSupportedGraphsAndProvenanceRejectOldSchemasWithoutMigration()
        {
            var scenario = Scenario(); string json = ExperimentScenarioSource.Serialize(scenario);
            var loaded = ExperimentScenarioSource.Deserialize(json, CatalogContext);
            Assert.That(loaded.Initial.Provenance.ContentHash, Is.EqualTo(scenario.Initial.Provenance.ContentHash));
            Assert.That(loaded.Initial.Versions.ActionTracks.Select(action => action.Id),
                Is.EqualTo(ActionTrackDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)));
            foreach (var action in scenario.Initial.Versions.ActionTracks)
            {
                var restored = loaded.Initial.Versions.ActionTracks.Single(candidate => candidate.Id == action.Id);
                Assert.That(restored.SourceVersion, Is.EqualTo(action.SourceVersion)); Assert.That(restored.ContentHash, Is.EqualTo(action.ContentHash));
                Assert.That(JsonUtility.ToJson(restored.ToData()), Is.EqualTo(JsonUtility.ToJson(action.ToData())));
            }
            foreach (int old in new[] { 1, 2, 3, 4, 5, 6, 7, 8 }) Assert.Throws<ArgumentException>(() => ExperimentScenarioSource.Deserialize(ChangeFirst(json, "\"schemaVersion\": " + ExperimentScenarioSource.SchemaVersion, "\"schemaVersion\": " + old), CatalogContext));
        }
        [Test] public void CompleteVersionsRejectMissingDuplicateAndObsoleteTwoGraphInventories()
        {
            var source = Versions(); var tracks = source.ActionTracks.ToArray();
            foreach (var invalid in new[] {
                tracks.Where(action => action.Id != ActionTrackDefinition.WaveId).ToArray(),
                tracks.Where(action => action.Id != ActionTrackDefinition.GuardId).ToArray(),
                tracks.Where(action => action.Id == ActionTrackDefinition.MovementId || action.Id == ActionTrackDefinition.FollowUpId).ToArray(),
                tracks.Skip(1).Concat(new[] { tracks[1] }).ToArray() })
                Assert.Throws<ArgumentException>(() => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, invalid, source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames));
        }
        [TestCase(ActionTrackDefinition.WaveId)] [TestCase(ActionTrackDefinition.GuardId)]
        public void FullVersionHashPinsWaveAndDefenseGraphEdits(string id)
        {
            var source = Versions(); var action = source.ActionTracks.Single(candidate => candidate.Id == id); var document = action.ToDocument();
            if (id == ActionTrackDefinition.WaveId) document.hits[0].box.height += .25f;
            else document.timeline.markers.Single(marker => marker.id == "guard-end").offset = .5f;
            var edited = action.Recompile(document);
            var changed = new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks.Select(candidate => candidate.Id == id ? edited : candidate), source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames,
                RebuildSpecialFrames(source, finite: source.ActionTracks.Select(candidate => candidate.Id == id ? edited : candidate)));
            Assert.That(edited.SourceVersion, Is.EqualTo(action.SourceVersion)); Assert.That(changed.ContentHash, Is.Not.EqualTo(source.ContentHash));
            var initial = Initial(changed); var scenario = new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .02f, new CombatInput[1]));
            var restored = ExperimentScenarioSource.Deserialize(ExperimentScenarioSource.Serialize(scenario), CatalogContext);
            Assert.That(restored.Initial.Versions.ActionTracks.Single(candidate => candidate.Id == id).ContentHash, Is.EqualTo(edited.ContentHash));
        }
        [Test] public void SchemaThreeRoundTripsAllDirectNumericSnapshotsAndRejectsMissingDuplicateInventories()
        {
            var scenario = Scenario(); var source = scenario.Initial.Versions;
            var loaded = ExperimentScenarioSource.Deserialize(ExperimentScenarioSource.Serialize(scenario), CatalogContext);
            Assert.That(loaded.Initial.Versions.DirectSpecials.Select(action => action.Id), Is.EqualTo(DirectMeleeSpecialDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)));
            foreach (var action in source.DirectSpecials)
            {
                var restored = loaded.Initial.Versions.DirectSpecials.Single(candidate => candidate.Id == action.Id);
                Assert.That(restored.FormId, Is.EqualTo(action.FormId)); Assert.That(restored.SourceVersion, Is.EqualTo(action.SourceVersion));
                Assert.That(restored.ContentHash, Is.EqualTo(action.ContentHash)); Assert.That(JsonUtility.ToJson(restored.ToData()), Is.EqualTo(JsonUtility.ToJson(action.ToData())));
                var incomplete = source.DirectSpecials.Where(candidate => candidate.Id != action.Id).ToArray();
                Assert.Throws<ArgumentException>(() => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks, incomplete, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames));
            }
            var duplicate = source.DirectSpecials.Skip(1).Concat(new[] { source.DirectSpecials[1] });
            Assert.Throws<ArgumentException>(() => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks, duplicate, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames));
        }
        [TestCase("base_sword_default")] [TestCase("rapier_range")] [TestCase("greatsword_range")]
        public void FullVersionHashPinsDirectNumericDefinitionWhilePreservingPolicy(string id)
        {
            var source = Versions(); var action = source.DirectSpecials.Single(candidate => candidate.Id == id); var values = action.ToBasicData(); values.damage += 7;
            var edited = action.Recompile(values);
            var direct = source.DirectSpecials.Select(candidate => candidate.Id == id ? edited : candidate).ToArray();
            var changed = new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks, direct, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, RebuildFrames(source, direct: direct), source.SpecialFrames);
            Assert.That(edited.SourceVersion, Is.EqualTo(action.SourceVersion)); Assert.That(changed.ContentHash, Is.Not.EqualTo(source.ContentHash));
            var initial = Initial(changed); var scenario = new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .02f, new CombatInput[1]));
            var restored = ExperimentScenarioSource.Deserialize(ExperimentScenarioSource.Serialize(scenario), CatalogContext);
            Assert.That(restored.Initial.Versions.DirectSpecials.Single(candidate => candidate.Id == id).ContentHash, Is.EqualTo(edited.ContentHash));
        }
        [Test] public void SchemaFourRoundTripsEveryChargedSourceAndRejectsMissingDuplicateInventory()
        {
            var scenario = Scenario(); var source = scenario.Initial.Versions;
            var loaded = ExperimentScenarioSource.Deserialize(ExperimentScenarioSource.Serialize(scenario), CatalogContext);
            Assert.That(loaded.Initial.Versions.ChargedSpecials.Select(action => action.Id), Is.EqualTo(ChargedMeleeDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)));
            foreach (var action in source.ChargedSpecials)
            {
                var restored = loaded.Initial.Versions.ChargedSpecials.Single(candidate => candidate.Id == action.Id);
                Assert.That(restored.FormId, Is.EqualTo(action.FormId)); Assert.That(restored.SourceVersion, Is.EqualTo(action.SourceVersion)); Assert.That(restored.ContentHash, Is.EqualTo(action.ContentHash));
                Assert.That(JsonUtility.ToJson(restored.ToData()), Is.EqualTo(JsonUtility.ToJson(action.ToData())));
                Assert.That(JsonUtility.ToJson(restored.ToNumericData()), Is.EqualTo(JsonUtility.ToJson(action.ToNumericData())));
                var incomplete = source.ChargedSpecials.Where(candidate => candidate.Id != action.Id);
                Assert.Throws<ArgumentException>(() => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks, source.DirectSpecials, incomplete, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames));
            }
            var duplicate = source.ChargedSpecials.Skip(1).Concat(new[] { source.ChargedSpecials[1] });
            Assert.Throws<ArgumentException>(() => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks, source.DirectSpecials, duplicate, source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames));
        }
        [TestCase("rapier_default")] [TestCase("greatsword_default")]
        public void FullVersionHashPinsChargedNumericDefinitionWithoutChangingItsPolicy(string id)
        {
            var source = Versions(); var action = source.ChargedSpecials.Single(candidate => candidate.Id == id); var values = action.ToNumericData();
            values.minimumDamage += 5; values.maximumDamage += 9; var edited = action.Recompile(values);
            var changed = new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame, source.ActionTracks, source.DirectSpecials, source.ChargedSpecials.Select(candidate => candidate.Id == id ? edited : candidate), source.AuthoredSkills, source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames);
            Assert.That(edited.SourceVersion, Is.EqualTo(action.SourceVersion)); Assert.That(changed.ContentHash, Is.Not.EqualTo(source.ContentHash));
            var initial = Initial(changed); var scenario = new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .02f, new CombatInput[1]));
            var restored = ExperimentScenarioSource.Deserialize(ExperimentScenarioSource.Serialize(scenario), CatalogContext);
            Assert.That(restored.Initial.Versions.ChargedSpecials.Single(candidate => candidate.Id == id).ContentHash, Is.EqualTo(edited.ContentHash));
        }
        [Test] public void SchemaFiveRoundTripsEveryAuthoredRangedAndMotionPayloadAndBindingHash()
        {
            var scenario = Scenario(); var source = scenario.Initial.Versions;
            var loaded = ExperimentScenarioSource.Deserialize(ExperimentScenarioSource.Serialize(scenario), CatalogContext).Initial.Versions;
            Assert.That(loaded.AuthoredSkills.Select(action => action.Id), Is.EqualTo(AuthoredSkillDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)));
            foreach (var action in source.AuthoredSkills)
            {
                var restored = loaded.AuthoredSkills.Single(candidate => candidate.Id == action.Id);
                Assert.That(restored.FormId, Is.EqualTo(action.FormId)); Assert.That(restored.SourceVersion, Is.EqualTo(action.SourceVersion)); Assert.That(restored.ContentHash, Is.EqualTo(action.ContentHash));
                Assert.That(JsonUtility.ToJson(restored.ToData()), Is.EqualTo(JsonUtility.ToJson(action.ToData())));
            }
            Assert.That(JsonUtility.ToJson(new RangedEnvelope { entries = loaded.RangedActions.ToSnapshot() }), Is.EqualTo(JsonUtility.ToJson(new RangedEnvelope { entries = source.RangedActions.ToSnapshot() })));
            Assert.That(loaded.RangedActions.Actions.Select(action => action.Id), Is.EqualTo(RangedActionCatalog.SupportedIds));
            Assert.That(JsonUtility.ToJson(loaded.MotionRegistry.ToDocument()), Is.EqualTo(JsonUtility.ToJson(source.MotionRegistry.ToDocument())));
            Assert.That(loaded.MotionRegistry.ContentHash, Is.EqualTo(source.MotionRegistry.ContentHash)); Assert.That(loaded.MotionBindingHash, Is.EqualTo(source.MotionBindingHash));
            Assert.That(loaded.MotionRegistry.Motion(FixedAttackMotion.FirstSwordId).IsDelegated, Is.True);
        }
        [Serializable] sealed class RangedEnvelope { public RangedActionSnapshotData[] entries; }
        [Test] public void CompleteVersionsRejectMissingAndDuplicateGenericInventories()
        {
            var source = Versions();
            foreach (string id in AuthoredSkillDefinition.SupportedIds)
                Assert.Throws<ArgumentException>(() => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame,
                    source.ActionTracks, source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills.Where(action => action.Id != id), source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames));
            var duplicate = source.AuthoredSkills.Skip(1).Concat(new[] { source.AuthoredSkills[1] });
            Assert.Throws<ArgumentException>(() => new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame,
                source.ActionTracks, source.DirectSpecials, source.ChargedSpecials, duplicate, source.RangedActions, source.MotionRegistry, source.MeleeFrames, source.SpecialFrames));
        }
        [Test] public void ExplicitCatalogMismatchRejectsBeforeMotionOrRangedCompilationAndSaveMutation()
        {
            var scenario = Scenario(); var catalog = CatalogContext; catalog.forms[0].inputSpeed += .1f;
            string path = Path.Combine(directory, "context.json"), json = ExperimentScenarioSource.Serialize(scenario);
            var malformedRegistry = ChangeFirst(json, "\"motionRegistry\": {", "\"motionRegistry\": null, \"ignoredRegistry\": {");
            var error = Assert.Throws<ArgumentException>(() => ExperimentScenarioSource.Deserialize(malformedRegistry, catalog)); Assert.That(error.Message, Does.Contain("canonical catalog context"));
            Assert.Throws<ArgumentException>(() => ExperimentScenarioSource.Save(path, null, scenario, catalog)); Assert.That(File.Exists(path), Is.False);
        }
        [Test] public void WholeVersionHashPinsGenericRangedAndRegistryChangesWithoutFirstSourceDuplication()
        {
            var source = Versions(); var generic = source.AuthoredSkills.Single(action => action.Id == "whip_default"); var numeric = generic.ToNumericData(); numeric.hits[0].damage += 7;
            var editedGeneric = generic.Recompile(numeric);
            var genericVersion = new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame,
                source.ActionTracks, source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills.Select(action => action.Id == generic.Id ? editedGeneric : action), source.RangedActions, source.MotionRegistry, source.MeleeFrames,
                RebuildSpecialFrames(source, authored: source.AuthoredSkills.Select(action => action.Id == generic.Id ? editedGeneric : action)));
            var ranged = source.RangedActions.Read("launcher_basic_0").ToNumericData(); ranged.damages[0] += 3;
            var rangedVersion = new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame,
                source.ActionTracks, source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions.Edit("launcher_basic_0", ranged), source.MotionRegistry, source.MeleeFrames, source.SpecialFrames);
            var document = source.MotionRegistry.ToDocument(); document.motions.First(motion => string.IsNullOrEmpty(motion.delegatedSource)).steps[0].seconds += .03f;
            var registry = new MotionSourceRegistry(document, CatalogContext);
            var registryVersion = new ExperimentVersions(source.CatalogHash, source.MediaHash, source.Movement, source.Basics, source.Motion, source.Frame,
                source.ActionTracks, source.DirectSpecials, source.ChargedSpecials, source.AuthoredSkills, source.RangedActions, registry, RebuildFrames(source, registry: registry), RebuildSpecialFrames(source, registry: registry));
            foreach (var changed in new[] { genericVersion, rangedVersion, registryVersion })
            {
                Assert.That(changed.ContentHash, Is.Not.EqualTo(source.ContentHash)); var initial = Initial(changed);
                var saved = new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .02f, new CombatInput[1]));
                Assert.That(ExperimentScenarioSource.Deserialize(ExperimentScenarioSource.Serialize(saved), CatalogContext).Initial.Versions.ContentHash, Is.EqualTo(changed.ContentHash));
            }
            Assert.That(registryVersion.MotionBindingHash, Is.Not.EqualTo(source.MotionBindingHash));
            var delegated = document.motions.Single(motion => motion.id == FixedAttackMotion.FirstSwordId); delegated.steps = source.Motion.ToDocument().steps;
            Assert.Throws<ArgumentException>(() => new MotionSourceRegistry(document, CatalogContext));
        }
        [TestCase(ActionTrackDefinition.FollowUpId)] [TestCase("whip_default")]
        public void CompleteSpecialLocalGraphsRoundTripWithTheirOriginalNumericAndStructuralMediaVersions(string id)
        {
            var original = Versions(); var changed = WithSpecialFrames(original, ShiftSpecialFrames(original, id));
            Assert.That(changed.ContentHash, Is.Not.EqualTo(original.ContentHash)); Assert.That(changed.MotionBindingHash, Is.Not.EqualTo(original.MotionBindingHash));
            Assert.That(changed.ActionTracks.Select(value => value.ContentHash), Is.EqualTo(original.ActionTracks.Select(value => value.ContentHash)));
            Assert.That(changed.AuthoredSkills.Select(value => value.ContentHash), Is.EqualTo(original.AuthoredSkills.Select(value => value.ContentHash)));
            var initial = Initial(changed); var scenario = new ExperimentScenario(initial, new CombatInputRecording(initial.Id, .02f, new CombatInput[2]));
            string path = Path.Combine(directory, id + "-local-frames.json"); ExperimentScenarioSource.Save(path, null, scenario, CatalogContext);
            var restored = ExperimentScenarioSource.Load(path, CatalogContext, out _).Initial.Versions;
            Assert.That(restored.SpecialFrames.ContentHash, Is.EqualTo(changed.SpecialFrames.ContentHash));
            Assert.That(restored.SpecialFrames.Find(id).Timeline.ContentHash, Is.EqualTo(changed.SpecialFrames.Find(id).Timeline.ContentHash));
            Assert.That(restored.SpecialFrames.Find(id).Media.BindingVersion, Is.EqualTo(changed.SpecialFrames.Find(id).Media.BindingVersion));
            Assert.That(restored.ContentHash, Is.EqualTo(changed.ContentHash));
            var document = changed.SpecialFrames.ToDocument(); document.actions = document.actions.Where(value => value.actionId != id).ToArray();
            Assert.Throws<ArgumentException>(() => new ActionSpecialFrameCatalog(document, CatalogContext, changed.MotionRegistry, changed.Motion, changed.ActionTracks, changed.AuthoredSkills));
            document = changed.SpecialFrames.ToDocument(); var active = document.actions.Single(value => value.actionId == id); active.mediaBindingVersion = new string('f', 64);
            Assert.Throws<ArgumentException>(() => new ActionSpecialFrameCatalog(document, CatalogContext, changed.MotionRegistry, changed.Motion, changed.ActionTracks, changed.AuthoredSkills));
        }
        static string ChangeFirst(string text, string before, string after)
        { int index = text.IndexOf(before, StringComparison.Ordinal); return text.Substring(0, index) + after + text.Substring(index + before.Length); }
    }
}
