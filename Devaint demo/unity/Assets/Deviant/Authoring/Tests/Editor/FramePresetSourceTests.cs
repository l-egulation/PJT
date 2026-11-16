using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class FramePresetSourceTests
    {
        [Serializable] sealed class Data
        {
            public int schemaVersion;
            public string kind, actionId, actionHash, motionId, motionHash, candidateHash;
            public float inputSpeed;
            public FramePreviewStep[] frames;
            public FramePreviewMarker[] markers;
        }
        string repository, path;
        public static string CopyRepository()
        {
            var source = new DirectoryInfo(Application.dataPath);
            while (source != null && !File.Exists(Path.Combine(source.FullName, AttackMotionSource.RelativePath))) source = source.Parent;
            Assert.That(source, Is.Not.Null);
            string result = Path.Combine(Path.GetTempPath(), "deviant-frame-preset-" + Guid.NewGuid().ToString("N"));
            foreach (string relative in new[] { AttackMotionSource.RelativePath, FrameMotionSource.ActionPath, MotionRegistrySource.CombatPath, MotionRegistrySource.RelativePath, MeleeFrameSource.RelativePath, SpecialFrameSource.RelativePath }) {
                string target = Path.Combine(result, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(Path.Combine(source.FullName, relative), target);
            }
            return result;
        }
        public static MeleeFrameDefinition Candidate(string repository, float delta = .03f)
        {
            var action = BasicSwordSource.Validate(BasicSwordSource.Load(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"), out _));
            var motion = new FixedAttackMotion(AttackMotionSource.Load(Path.Combine(repository, AttackMotionSource.RelativePath), out _));
            var original = MeleeFrameCompiler.ProjectFirst(action, motion, 2.5f); var frames = original.Frames.ToArray(); frames[0] = new TimelineFrame(frames[0].Id, frames[0].Seconds + delta);
            return MeleeFrameCompiler.CompileFirst(action, motion, new FrameTimeline(frames, original.Markers), 2.5f);
        }
        [SetUp] public void SetUp() { repository = CopyRepository(); path = Path.Combine(repository, "frame-experiment.json"); }
        [TearDown] public void TearDown() { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        void NoTemporaryFiles() => Assert.That(Directory.GetFiles(repository, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        [Test] public void SaveLoadRoundTripPreservesCandidateWithoutCopyingCombatNumbersOrArtBindings()
        {
            var candidate = Candidate(repository); string actionPath = Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"), motionPath = Path.Combine(repository, AttackMotionSource.RelativePath);
            byte[] action = File.ReadAllBytes(actionPath), motion = File.ReadAllBytes(motionPath);
            string hash = FramePresetSource.Save(path, null, repository, candidate); var loaded = FramePresetSource.Load(path, repository, out var loadedHash);
            Assert.That(loadedHash, Is.EqualTo(hash)); Assert.That(loaded.ContentHash, Is.EqualTo(candidate.ContentHash)); Assert.That(loaded.Action.Impact, Is.EqualTo(.23f).Within(.000001f)); Assert.That(loaded.Action.Duration, Is.EqualTo(.43f).Within(.000001f));
            Assert.That(File.ReadAllText(path), Is.EqualTo(FramePresetSource.Serialize(loaded))); Assert.That(File.ReadAllBytes(path).Take(3), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf }));
            Assert.That(File.ReadAllText(path), Does.Not.Contain("\"damage\"").And.Not.Contain("\"box\"").And.Not.Contain("CharacterConfirmed"));
            Assert.That(File.ReadAllBytes(actionPath), Is.EqualTo(action)); Assert.That(File.ReadAllBytes(motionPath), Is.EqualTo(motion)); NoTemporaryFiles();
        }
        [Test] public void ExpectedHashReplacesOnlyACompatibleExperimentAndNewCreationNeverOverwrites()
        {
            string first = FramePresetSource.Save(path, null, repository, Candidate(repository)); byte[] before = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => FramePresetSource.Save(path, null, repository, Candidate(repository, .06f))); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            string next = FramePresetSource.Save(path, first, repository, Candidate(repository, .06f)); Assert.That(next, Is.Not.EqualTo(first)); Assert.That(FramePresetSource.Load(path, repository, out _).Action.Impact, Is.EqualTo(.26f).Within(.000001f));
            string unrelated = Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"); string raw = SourceFiles.Read(unrelated, out var unrelatedHash);
            Assert.Catch<ArgumentException>(() => FramePresetSource.Save(unrelated, unrelatedHash, repository, Candidate(repository))); Assert.That(File.ReadAllText(unrelated), Is.EqualTo(raw)); NoTemporaryFiles();
        }
        [TestCase(false)] [TestCase(true)] public void ExternalEditOrDeletionCannotBeOverwritten(bool deletion)
        {
            string hash = FramePresetSource.Save(path, null, repository, Candidate(repository));
            if (deletion) File.Delete(path); else File.WriteAllText(path, "external edit");
            Assert.Catch<IOException>(() => FramePresetSource.Save(path, hash, repository, Candidate(repository, .06f)));
            if (deletion) Assert.That(File.Exists(path), Is.False); else Assert.That(File.ReadAllText(path), Is.EqualTo("external edit")); NoTemporaryFiles();
        }
        [TestCase(false)] [TestCase(true)] public void ChangedFixedSourceRejectsLoadAndSaveWithoutChangingThePreset(bool motionChanged)
        {
            var original = Candidate(repository); string hash = FramePresetSource.Save(path, null, repository, original); byte[] bytes = File.ReadAllBytes(path);
            if (motionChanged) {
                string source = Path.Combine(repository, AttackMotionSource.RelativePath); var motion = AttackMotionSource.Load(source, out var h); motion.steps[0].seconds += .01f; AttackMotionSource.Save(source, h, motion);
            }
            else {
                string source = Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"); var action = BasicSwordSource.Load(source, out var h); action.hit.damage = 20; BasicSwordSource.Save(repository, h, action);
            }
            Assert.Catch<ArgumentException>(() => FramePresetSource.Load(path, repository, out _)); Assert.Catch<ArgumentException>(() => FramePresetSource.Save(path, hash, repository, original));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes)); NoTemporaryFiles();
        }
        [Test] public void WhitespaceOnlySourceChangesKeepTheSameSemanticVersion()
        {
            FramePresetSource.Save(path, null, repository, Candidate(repository));
            File.AppendAllText(Path.Combine(repository, AttackMotionSource.RelativePath), "\n"); File.AppendAllText(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"), "\n");
            Assert.That(FramePresetSource.Load(path, repository, out _).Action.Impact, Is.EqualTo(.23f).Within(.000001f));
        }
        [Test] public void UnsupportedHeadersMissingGraphsAndInvalidReferencesOrTimesAreRejected()
        {
            var candidate = Candidate(repository); string json = FramePresetSource.Serialize(candidate);
            foreach (Action<Data> change in new Action<Data>[] {
                d => d.schemaVersion = 1, d => d.kind = "other", d => d.actionId = "other", d => d.motionId = "other", d => d.actionHash = "other", d => d.motionHash = "other",
                d => d.frames = null, d => d.markers = null, d => d.frames[0] = null, d => d.frames[0].seconds = 0,
                d => d.frames[0].id = d.frames[1].id, d => d.markers[0].anchor = (TimelineAnchor)99,
                d => d.markers[0].reference = "missing", d => { d.markers[0].anchor = TimelineAnchor.Marker; d.markers[0].reference = d.markers[0].id; }
            }) {
                var data = JsonUtility.FromJson<Data>(json); change(data); Assert.Catch<ArgumentException>(() => FramePresetSource.Deserialize(JsonUtility.ToJson(data), candidate.SourceAction, candidate.SourceMotion, 2.5f));
            }
            Assert.Catch<ArgumentException>(() => FramePresetSource.Deserialize("null", candidate.SourceAction, candidate.SourceMotion, 2.5f));
        }
        [Test] public void ValidEditedGraphWithoutUpdatedCandidateHashCannotBeAccepted()
        {
            var candidate = Candidate(repository); var data = JsonUtility.FromJson<Data>(FramePresetSource.Serialize(candidate)); data.frames[0].seconds += .01f;
            var error = Assert.Catch<ArgumentException>(() => FramePresetSource.Deserialize(JsonUtility.ToJson(data), candidate.SourceAction, candidate.SourceMotion, 2.5f)); Assert.That(error.Message, Does.Contain("hash"));
        }
        [Test] public void SerializationIsCanonicalAcrossMarkerDeclarationOrderAndCulture()
        {
            var candidate = Candidate(repository); var reversed = MeleeFrameCompiler.CompileFirst(candidate.SourceAction, candidate.SourceMotion, new FrameTimeline(candidate.Timeline.Frames, candidate.Timeline.Markers.Reverse()), 2.5f);
            var culture = CultureInfo.CurrentCulture; string json = FramePresetSource.Serialize(candidate);
            try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Assert.That(FramePresetSource.Serialize(reversed), Is.EqualTo(json)); }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [Test] public void OversizedAndInvalidPayloadsAreRejectedBeforeReadingOrReplacingFiles()
        {
            var candidate = Candidate(repository); string hash = FramePresetSource.Save(path, null, repository, candidate); byte[] before = File.ReadAllBytes(path);
            Assert.Throws<ArgumentNullException>(() => FramePresetSource.Save(path, hash, repository, null)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            string huge = Path.Combine(repository, "huge.json"); File.WriteAllBytes(huge, new byte[FramePresetSource.MaximumFileBytes + 1]); Assert.Throws<IOException>(() => FramePresetSource.Load(huge, repository, out _));
            Assert.Catch<ArgumentException>(() => FramePresetSource.Deserialize(new string('x', FramePresetSource.MaximumFileBytes + 1), candidate.SourceAction, candidate.SourceMotion, 2.5f)); NoTemporaryFiles();
        }
    }
}
