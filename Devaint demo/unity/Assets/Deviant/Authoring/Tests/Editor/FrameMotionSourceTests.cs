using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class FrameMotionSourceTests
    {
        string repository;
        public static string CopyRepository()
        {
            string result = FramePresetSourceTests.CopyRepository();
            var original = new DirectoryInfo(Application.dataPath);
            while (original != null && !File.Exists(Path.Combine(original.FullName, AttackMotionSource.RelativePath))) original = original.Parent;
            Assert.That(original, Is.Not.Null);
            foreach (string relative in new[] {
                AttackMotionSource.CatalogPath, "unity/Assets/Deviant/Combat/Resources/CharacterV2/HeavyWeaponCatalog.json", "data/combat/presentation/basic_attack_effects.json", "tools/combat/export_attack_motion.py", "tools/combat/special_frame_transport.py", "tools/combat/build_melee_design.py",
                "tools/combat/export_unity_catalog.py", "tools/combat/melee_source.py", "data/combat/melee_weapon_design.json",
                "data/combat/ranged_basic_design.json", "data/combat/launcher_skills.json", "data/combat/locomotion/player.json"
            })
            {
                string target = Path.Combine(result, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(Path.Combine(original.FullName, relative), target);
            }
            foreach (string file in Directory.GetFiles(Path.Combine(original.FullName, "data/combat"), "*.json", SearchOption.AllDirectories))
            {
                string target = Path.Combine(result, Path.GetRelativePath(original.FullName, file));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target);
            }
            Directory.CreateDirectory(Path.Combine(result, "docs/design/equipment/weapons/forms"));
            return result;
        }
        public static MeleeFrameDefinition Candidate(string repository, float extra = .03f)
        {
            var baseline = FrameMotionSource.ReadCandidate(repository, out _); var frames = baseline.Timeline.Frames.ToArray();
            frames[0] = new TimelineFrame(frames[0].Id, frames[0].Seconds + extra);
            return MeleeFrameCompiler.CompileFirst(baseline.SourceAction, baseline.SourceMotion, new FrameTimeline(frames, baseline.Timeline.Markers), 2.5f);
        }
        public static CharacterAnimationCatalog Generated(string repository)
            => JsonUtility.FromJson<CharacterAnimationCatalog>(File.ReadAllText(Path.Combine(repository, AttackMotionSource.CatalogPath)));
        [SetUp] public void SetUp() { repository = CopyRepository(); }
        [TearDown] public void TearDown() { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        void NoTemporaryFiles() => Assert.That(Directory.GetFiles(repository, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        [Test] public void FixedDefaultAndExplicitProjectionPreserveEveryResolvedTimeAndCueMediaCurve()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.RelativePath));
            var source = FrameMotionSource.Load(repository, out _); Assert.That(source.UsesFrameTime, Is.False);
            Assert.That(source.ToDocument().schemaVersion, Is.EqualTo(1)); Assert.That(source.ToDocument().mode, Is.EqualTo(FixedAttackMotion.Mode));
            var projected = FrameMotionSource.ReadCandidate(repository, out _); var action = projected.SourceAction;
            var promoted = FrameAttackMotionSource.FromCandidate(projected); var compiled = promoted.Compile(action, 2.5f);
            Assert.That(compiled.ContentHash, Is.EqualTo(projected.ContentHash)); Assert.That(compiled.Action.Impact, Is.EqualTo(action.Impact).Within(.000001f));
            Assert.That(compiled.Action.Active, Is.EqualTo(action.Active).Within(.000001f)); Assert.That(compiled.Action.Duration, Is.EqualTo(action.Duration).Within(.000001f));
            Assert.That(compiled.Timeline.Frames.Select(frame => frame.Seconds), Is.EqualTo(source.FixedBinding.EffectiveSeconds(action.Impact, action.Duration)));
            foreach (float speed in new[] { 1f, 1.1f, 3f })
            {
                var fixedTiming = source.FixedBinding.Compile(action.Impact / speed, action.Duration / speed); var frameTiming = compiled.CompileMotion(speed);
                Assert.That(frameTiming.FxStart, Is.EqualTo(source.FixedBinding.ActionTime(source.FixedBinding.FxStart, action.Impact, action.Duration) / speed).Within(.000001f));
                foreach (float age in new[] { .01f, .1f, .19f, .25f, .39f }.Select(time => time / speed))
                {
                    Assert.That(frameTiming.CueElapsed(age), Is.EqualTo(fixedTiming.CueElapsed(age)).Within(.000001f));
                    Assert.That(frameTiming.FrameAt(age), Is.EqualTo(fixedTiming.FrameAt(age)));
                }
            }
            Assert.That(File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.RelativePath)), Is.EqualTo(bytes));
        }
        [Test] public void SaveReloadAndGenerationUseTheSameCompilerAndKeepOnlyOneGameplayTimeline()
        {
            string path = Path.Combine(repository, AttackMotionSource.RelativePath), actionPath = Path.Combine(repository, FrameMotionSource.ActionPath);
            byte[] actionBytes = File.ReadAllBytes(actionPath); var candidate = Candidate(repository);
            FrameMotionSource.Load(repository, out var hash); hash = FrameMotionSource.Save(repository, hash, candidate);
            byte[] saved = File.ReadAllBytes(path); Assert.That(saved.Take(3), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf }));
            var source = FrameMotionSource.Load(repository, out var loadedHash); Assert.That(source.UsesFrameTime, Is.True); Assert.That(loadedHash, Is.EqualTo(hash));
            Assert.That(source.Compile(candidate.SourceAction, 2.5f).ContentHash, Is.EqualTo(candidate.ContentHash));
            Assert.That(FrameMotionSource.Save(repository, hash, candidate), Is.EqualTo(hash));
            var before = Generated(repository); AttackMotionSource.Regenerate(repository); var generated = Generated(repository);
            Assert.That(generated.motionSources, Has.Length.EqualTo(1));
            var runtime = new FrameAttackMotionSource(generated.motionSources[0]).Compile(candidate.SourceAction, 2.5f);
            Assert.That(runtime.ContentHash, Is.EqualTo(candidate.ContentHash)); Assert.That(runtime.CompileMotion(1.1f).ContentHash, Is.EqualTo(candidate.CompileMotion(1.1f).ContentHash));
            generated.motionSources = before.motionSources; Assert.That(JsonUtility.ToJson(generated), Is.EqualTo(JsonUtility.ToJson(before)), "Generation leaves all existing clips, effects and art references unchanged.");
            byte[] once = File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.CatalogPath)); AttackMotionSource.Regenerate(repository);
            Assert.That(File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.CatalogPath)), Is.EqualTo(once));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(saved)); Assert.That(File.ReadAllBytes(actionPath), Is.EqualTo(actionBytes)); NoTemporaryFiles();
        }
        [Test] public void FrameMarkersAreTheOnlyActiveTimesAndCurrentNumericSourceStillSuppliesDamageAndGeometry()
        {
            var candidate = Candidate(repository); var source = FrameAttackMotionSource.FromCandidate(candidate); var changed = candidate.SourceAction.ToData();
            changed.impact = .1f; changed.duration = .7f; changed.active = .01f; changed.damage = 17; changed.box.width += 1;
            Assert.That(BasicMeleeDefinition.TryCreate(candidate.SourceAction.Id, changed, out var action, out var error), Is.True, error);
            var compiled = source.Compile(action, 2.5f);
            Assert.That(compiled.Action.Impact, Is.EqualTo(.23f).Within(.000001f)); Assert.That(compiled.Action.Active, Is.EqualTo(.08f).Within(.000001f)); Assert.That(compiled.Action.Duration, Is.EqualTo(.43f).Within(.000001f));
            Assert.That(compiled.Action.Damage, Is.EqualTo(17)); Assert.That(compiled.Action.ToData().box.width, Is.EqualTo(changed.box.width));
        }
        [Test] public void MarkerOrderAndCultureDoNotChangeCanonicalSerializationOrCompilation()
        {
            var candidate = Candidate(repository); string expected = FrameMotionSource.Serialize(FrameAttackMotionSource.FromCandidate(candidate));
            var reversed = MeleeFrameCompiler.CompileFirst(candidate.SourceAction, candidate.SourceMotion, new FrameTimeline(candidate.Timeline.Frames, candidate.Timeline.Markers.Reverse()), 2.5f);
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                Assert.That(FrameMotionSource.Serialize(FrameAttackMotionSource.FromCandidate(reversed)), Is.EqualTo(expected));
                Assert.That(reversed.ContentHash, Is.EqualTo(candidate.ContentHash));
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [TestCase(false)] [TestCase(true)] public void ExternalEditOrDeletionIsNeverOverwritten(bool deletion)
        {
            var candidate = Candidate(repository); FrameMotionSource.Load(repository, out var hash);
            string path = Path.Combine(repository, AttackMotionSource.RelativePath);
            if (deletion) File.Delete(path); else File.AppendAllText(path, "\n");
            byte[] external = deletion ? null : File.ReadAllBytes(path);
            Assert.Catch<IOException>(() => FrameMotionSource.Save(repository, hash, candidate));
            if (deletion) Assert.That(File.Exists(path), Is.False); else Assert.That(File.ReadAllBytes(path), Is.EqualTo(external)); NoTemporaryFiles();
        }
        [Test] public void ChangedActionOrReadOnlyBindingRejectsStaleCandidatesAndFixedEditorCannotDemoteFrameSource()
        {
            var candidate = Candidate(repository); FrameMotionSource.Load(repository, out var hash);
            string path = Path.Combine(repository, AttackMotionSource.RelativePath), actionPath = Path.Combine(repository, FrameMotionSource.ActionPath);
            var action = BasicSwordSource.Load(actionPath, out var actionHash); action.hit.damage += 1; BasicSwordSource.Save(repository, actionHash, action);
            byte[] before = File.ReadAllBytes(path); Assert.Catch<ArgumentException>(() => FrameMotionSource.Save(repository, hash, candidate)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            candidate = Candidate(repository); hash = FrameMotionSource.Save(repository, hash, candidate); before = File.ReadAllBytes(path);
            Assert.Catch<ArgumentException>(() => AttackMotionSource.Save(path, hash, candidate.SourceMotion.ToDocument())); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            var changed = candidate.SourceMotion.ToDocument(); changed.steps[0].seconds += .01f; var binding = new FixedAttackMotion(changed);
            var stale = MeleeFrameCompiler.CompileFirst(candidate.SourceAction, binding, candidate.Timeline, 2.5f);
            Assert.Catch<ArgumentException>(() => FrameMotionSource.Save(repository, hash, stale)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before)); NoTemporaryFiles();
        }
        [Test] public void InvalidSourceAndCompilerFailuresPreserveExistingFiles()
        {
            var candidate = Candidate(repository); var source = FrameAttackMotionSource.FromCandidate(candidate); var action = candidate.SourceAction;
            foreach (Action<AttackMotionDocument> mutation in new Action<AttackMotionDocument>[] {
                d => d.schemaVersion = 3, d => d.id = "other", d => d.actionId = "other", d => d.mode = "other",
                d => d.steps = d.fixedBinding.steps, d => d.sourceImpact = .2f, d => d.fxStart = -1,
                d => d.fixedBinding = null, d => d.timeline = null, d => d.timeline.frames[0].seconds = 0,
                d => d.timeline.frames[0].id = d.timeline.frames[1].id, d => d.timeline.markers[0].anchor = (TimelineAnchor)99,
                d => { var m = d.timeline.markers.Single(marker => marker.id == FrameTimingProjection.Contact); m.anchor = TimelineAnchor.Marker; m.reference = m.id; },
                d => d.timeline.markers = d.timeline.markers.Where(marker => marker.id != MeleeFrameDefinition.HitEnd).ToArray(),
                d => { var m = d.timeline.markers.Single(marker => marker.id == FrameTimingProjection.ActionEnd); m.offset += .01f; }
            })
            {
                var document = source.ToDocument(); mutation(document);
                Assert.Catch<ArgumentException>(() => new FrameAttackMotionSource(document).Compile(action, 2.5f));
            }
            string path = Path.Combine(repository, AttackMotionSource.RelativePath); FrameMotionSource.Load(repository, out var hash); byte[] before = File.ReadAllBytes(path);
            Assert.Throws<ArgumentNullException>(() => FrameMotionSource.Save(repository, hash, null)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            File.WriteAllBytes(path, new byte[FrameMotionSource.MaximumFileBytes + 1]); Assert.Throws<IOException>(() => FrameMotionSource.Load(repository, out _)); NoTemporaryFiles();
        }
        [Test] public void GenerationFailureLeavesTheSavedFrameSourceRecoverableForRetry()
        {
            var candidate = Candidate(repository); FrameMotionSource.Load(repository, out var hash); hash = FrameMotionSource.Save(repository, hash, candidate);
            string path = Path.Combine(repository, AttackMotionSource.CatalogPath); byte[] catalog = File.ReadAllBytes(path); File.Delete(path);
            Assert.Throws<IOException>(() => AttackMotionSource.Regenerate(repository));
            Assert.That(FrameMotionSource.Load(repository, out var actual).Compile(candidate.SourceAction, 2.5f).ContentHash, Is.EqualTo(candidate.ContentHash)); Assert.That(actual, Is.EqualTo(hash));
            File.WriteAllBytes(path, catalog); AttackMotionSource.Regenerate(repository);
            Assert.That(new FrameAttackMotionSource(Generated(repository).motionSources.Single()).Compile(candidate.SourceAction, 2.5f).ContentHash, Is.EqualTo(candidate.ContentHash)); NoTemporaryFiles();
        }
        [Test] public void ExistingExperimentFilesRemainDetachedAndUsableAfterPermanentPromotion()
        {
            var candidate = Candidate(repository); string path = Path.Combine(repository, "experiment.json");
            string presetHash = FramePresetSource.Save(path, null, repository, candidate); FrameMotionSource.Load(repository, out var sourceHash); FrameMotionSource.Save(repository, sourceHash, candidate);
            Assert.That(FramePresetSource.Load(path, repository, out var loadedHash).ContentHash, Is.EqualTo(candidate.ContentHash)); Assert.That(loadedHash, Is.EqualTo(presetHash));
            var changed = Candidate(repository); FramePresetSource.Save(path, presetHash, repository, changed);
            Assert.That(FrameMotionSource.ReadCandidate(repository, out _).ContentHash, Is.EqualTo(candidate.ContentHash), "Saving an experiment never rewrites the permanent source.");
        }
        [Test] public void PlayerBuildGateAcceptsFixedAndMatchingFrameDataButRejectsInvalidGraphsBindingsAndDuplicates()
        {
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var generated = Generated(repository); var candidate = Candidate(repository);
            MeleeFrameDefinition ValidateParsed() => FrameMotionBuildValidation.Validate(JsonUtility.FromJson<CharacterAnimationCatalog>(JsonUtility.ToJson(generated)), combat);
            Assert.That(ValidateParsed(), Is.Null, "Existing fixed catalogs do not need frame metadata.");
            generated.motionSources = new[] { candidate.SourceMotion.ToDocument() };
            Assert.That(ValidateParsed(), Is.Null, "A valid explicit fixed source is not frame activation.");
            generated.motionSources = new[] { FrameAttackMotionSource.FromCandidate(candidate).ToDocument() };
            Assert.That(ValidateParsed().ContentHash, Is.EqualTo(candidate.ContentHash));
            var contact = generated.motionSources[0].timeline.markers.Single(marker => marker.id == FrameTimingProjection.Contact);
            contact.anchor = TimelineAnchor.Marker; contact.reference = contact.id;
            Assert.Catch<ArgumentException>(() => ValidateParsed());
            generated.motionSources = new[] { FrameAttackMotionSource.FromCandidate(candidate).ToDocument() };
            generated.sets.Single(set => set.form == "base_sword").Find("attack1").seconds[0] += .01f;
            Assert.Catch<ArgumentException>(() => ValidateParsed());
            generated = Generated(repository); generated.motionSources = new[] { candidate.SourceMotion.ToDocument(), candidate.SourceMotion.ToDocument() };
            Assert.Catch<ArgumentException>(() => ValidateParsed());
        }
    }
}
