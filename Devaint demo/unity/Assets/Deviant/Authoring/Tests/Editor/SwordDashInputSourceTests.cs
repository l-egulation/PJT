using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using Deviant.CombatLab.Contracts;
using NUnit.Framework;

namespace Deviant.Authoring.Tests
{
    public class SwordDashInputSourceTests
    {
        string directory;
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "deviant-input-source-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        public static SwordDashInputSet Inputs(bool idle = false)
        {
            var attack = new CombatInput[SwordDashInputSet.AttackTicks]; var wall = new CombatInput[SwordDashInputSet.WallTicks];
            if (!idle) { attack[0] = new CombatInput(0, CombatButtons.Basic, climbAxis: 1, heldSpecials: CombatButtons.Weapon1Special); attack[1] = new CombatInput(released: CombatButtons.Basic, climbAxis: -1, heldSpecials: CombatButtons.Weapon2Special); attack[30] = new CombatInput(0, CombatButtons.Dash); wall[0] = new CombatInput(0, CombatButtons.Dash); }
            return new SwordDashInputSet(new CombatInputRecording(SwordDashComparison.ScenarioId + "/attack", SwordDashComparison.Step, attack),
                new CombatInputRecording(SwordDashComparison.ScenarioId + "/wall", SwordDashComparison.Step, wall));
        }
        static string ChangeFirst(string text, string before, string after)
        {
            int index = text.IndexOf(before, StringComparison.Ordinal); Assert.That(index, Is.GreaterThanOrEqualTo(0), before);
            return text.Substring(0, index) + after + text.Substring(index + before.Length);
        }
        void AssertNoTemporaryFiles() => Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        [Test] public void SaveLoadRoundtripPreservesEveryInputAndProducesDeterministicBomFreeJson()
        {
            var inputs = Inputs(); string path = Path.Combine(directory, "inputs.json");
            string hash = SwordDashInputSource.Save(path, null, inputs); var loaded = SwordDashInputSource.Load(path, out var fileHash);
            Assert.That(fileHash, Is.EqualTo(hash)); Assert.That(loaded.Attack.ContentHash, Is.EqualTo(inputs.Attack.ContentHash)); Assert.That(loaded.Wall.ContentHash, Is.EqualTo(inputs.Wall.ContentHash));
            Assert.That(loaded.Attack.Inputs.Select(i => (i.Axis, i.Pressed, i.SpecialHeld, i.Released, i.ClimbAxis, i.HeldSpecials)), Is.EqualTo(inputs.Attack.Inputs.Select(i => (i.Axis, i.Pressed, i.SpecialHeld, i.Released, i.ClimbAxis, i.HeldSpecials))));
            Assert.That(File.ReadAllText(path), Is.EqualTo(SwordDashInputSource.Serialize(loaded)));
            Assert.That(File.ReadAllBytes(path).Take(3), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf })); AssertNoTemporaryFiles();
        }
        [Test] public void CreatingAnExistingPathCannotOverwriteItsContents()
        {
            string path = Path.Combine(directory, "inputs.json"); SwordDashInputSource.Save(path, null, Inputs()); string before = File.ReadAllText(path);
            Assert.Throws<IOException>(() => SwordDashInputSource.Save(path, null, Inputs(true))); Assert.That(File.ReadAllText(path), Is.EqualTo(before)); AssertNoTemporaryFiles();
        }
        [Test] public void ExpectedHashAllowsAtomicReplacementAndReloadsTheNewRecording()
        {
            string path = Path.Combine(directory, "inputs.json"); string first = SwordDashInputSource.Save(path, null, Inputs());
            var next = Inputs(true); string second = SwordDashInputSource.Save(path, first, next); Assert.That(second, Is.Not.EqualTo(first));
            var loaded = SwordDashInputSource.Load(path, out var hash); Assert.That(hash, Is.EqualTo(second)); Assert.That(loaded.Attack.ContentHash, Is.EqualTo(next.Attack.ContentHash)); AssertNoTemporaryFiles();
        }
        [TestCase(false)] [TestCase(true)] public void ExternalEditOrDeletionRejectsSaveWithoutReplacingTheExternalState(bool deletion)
        {
            string path = Path.Combine(directory, "inputs.json"); string hash = SwordDashInputSource.Save(path, null, Inputs());
            if (deletion) File.Delete(path); else File.WriteAllText(path, "external editor owns this content");
            Assert.Throws<IOException>(() => SwordDashInputSource.Save(path, hash, Inputs(true)));
            if (deletion) Assert.That(File.Exists(path), Is.False); else Assert.That(File.ReadAllText(path), Is.EqualTo("external editor owns this content"));
            AssertNoTemporaryFiles();
        }
        [Test] public void InvalidPayloadDoesNotTouchAnExistingDestination()
        {
            string path = Path.Combine(directory, "inputs.json"); string hash = SwordDashInputSource.Save(path, null, Inputs()); string before = File.ReadAllText(path);
            Assert.Throws<ArgumentNullException>(() => SwordDashInputSource.Save(path, hash, null)); Assert.That(File.ReadAllText(path), Is.EqualTo(before)); AssertNoTemporaryFiles();
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void UnsupportedSchemaCannotBeLoaded(int version)
        {
            string json = ChangeFirst(SwordDashInputSource.Serialize(Inputs()), "\"schemaVersion\": 3", "\"schemaVersion\": " + version);
            Assert.Throws<ArgumentException>(() => SwordDashInputSource.Deserialize(json));
        }
        [TestCase("\"axis\": 0", "\"axis\": 2")]
        [TestCase("\"pressed\": 1", "\"pressed\": 1024")]
        [TestCase("\"released\": 0", "\"released\": 1024")]
        public void InvalidInputValuesAreRejectedByTheSharedInputContract(string before, string after)
        {
            string json = ChangeFirst(SwordDashInputSource.Serialize(Inputs()), before, after);
            Assert.Throws<ArgumentOutOfRangeException>(() => SwordDashInputSource.Deserialize(json));
        }
        [Test] public void AValidInputChangeWithoutUpdatingItsHashCannotBeLoaded()
        {
            string json = ChangeFirst(SwordDashInputSource.Serialize(Inputs()), "\"specialHeld\": false", "\"specialHeld\": true");
            var error = Assert.Throws<ArgumentException>(() => SwordDashInputSource.Deserialize(json)); Assert.That(error.Message, Does.Contain("hash"));
        }
        [TestCase("attack")] [TestCase("wall")] public void BothScenarioSegmentsAreRequired(string missing)
        {
            string json = ChangeFirst(SwordDashInputSource.Serialize(Inputs()), "\"" + missing + "\": {", "\"" + missing + "\": null, \"ignored\": {");
            Assert.Throws<ArgumentException>(() => SwordDashInputSource.Deserialize(json));
        }
        [TestCase(false)] [TestCase(true)] public void ACorrectHashDoesNotPermitAnUnsupportedScenarioOrStep(bool step)
        {
            var inputs = Inputs(); var changed = new CombatInputRecording(step ? inputs.Attack.ScenarioId : "foreign/attack", step ? .04f : inputs.Attack.StepSeconds, inputs.Attack.Inputs.ToArray());
            string json = SwordDashInputSource.Serialize(inputs).Replace(inputs.Attack.ContentHash, changed.ContentHash);
            if (step)
            {
                var match = Regex.Match(json, "(\\\"stepSeconds\\\"\\s*:\\s*)[-+0-9.Ee]+"); Assert.That(match.Success, Is.True);
                json = json.Substring(0, match.Index) + match.Groups[1].Value + "0.04" + json.Substring(match.Index + match.Length);
            }
            else json = ChangeFirst(json, inputs.Attack.ScenarioId, changed.ScenarioId);
            var error = Assert.Throws<ArgumentException>(() => SwordDashInputSource.Deserialize(json)); Assert.That(error.Message, Does.Contain("fixed experiment segment"));
        }
        [Test] public void FixedSegmentCountsAreCheckedBeforeReplayCanStart()
        {
            var inputs = Inputs(); var shortAttack = new CombatInputRecording(inputs.Attack.ScenarioId, inputs.Attack.StepSeconds, new CombatInput[SwordDashInputSet.AttackTicks - 1]);
            Assert.Throws<ArgumentException>(() => new SwordDashInputSet(shortAttack, inputs.Wall)); Assert.Throws<ArgumentNullException>(() => new SwordDashInputSet(null, inputs.Wall));
        }
        [Test] public void ReadAndDeserializeEnforceTheFileBudgetAndBoundedReadAcceptsAnExactLimit()
        {
            string path = Path.Combine(directory, "oversized.json"); File.WriteAllBytes(path, new byte[SwordDashInputSource.MaximumFileBytes + 1]);
            Assert.Throws<IOException>(() => SwordDashInputSource.Load(path, out _));
            Assert.Throws<ArgumentException>(() => SwordDashInputSource.Deserialize(new string('x', SwordDashInputSource.MaximumFileBytes + 1)));
            string exact = Path.Combine(directory, "exact.txt"); File.WriteAllBytes(exact, Encoding.UTF8.GetBytes("abc"));
            Assert.That(SourceFiles.Read(exact, out var hash, 3), Is.EqualTo("abc")); Assert.Throws<IOException>(() => SourceFiles.Read(exact, out _, 2));
            Assert.Throws<IOException>(() => SourceFiles.Save(exact, hash, "a", 2)); Assert.Throws<IOException>(() => SourceFiles.Save(exact, hash, "abcd", 3));
            Assert.That(File.ReadAllText(exact), Is.EqualTo("abc"));
            Assert.Throws<ArgumentOutOfRangeException>(() => SourceFiles.Read(exact, out _, 0));
        }
    }
}
