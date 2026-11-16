using System;
using System.IO;
using System.Globalization;
using Deviant.Game;
using NUnit.Framework;

namespace ForestCombatV2.Tests
{
    public sealed class WebBuildStorageResetTests
    {
        const string Marker = ".deviant-web-reset-utc";
        static readonly DateTimeOffset Cutoff = WebBuildStorageReset.ResetBeforeUtc;
        string root;
        int preferenceClears;
        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "deviant-web-reset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            preferenceClears = 0;
        }
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        bool Reset(DateTimeOffset cutoff) => WebBuildStorageReset.ResetIfOlder(root, cutoff, () => preferenceClears++);
        void Mark(DateTimeOffset cutoff) => File.WriteAllText(Path.Combine(root, Marker), cutoff.ToString("O", CultureInfo.InvariantCulture));
        void Seed()
        {
            Directory.CreateDirectory(Path.Combine(root, "profile"));
            File.WriteAllText(Path.Combine(root, "profile/profile.json"), "old profile");
            File.WriteAllText(Path.Combine(root, "profile/profile.json.bak"), "old backup");
            Directory.CreateDirectory(Path.Combine(root, "ReplayQueue/nested"));
            File.WriteAllText(Path.Combine(root, "ReplayQueue/nested/replay"), "old replay");
            File.WriteAllText(Path.Combine(root, "settings"), "old settings");
        }
        [Test] public void LegacyStorageIsClearedBeforeWritingCutoffMarker()
        {
            Seed();
            File.WriteAllText(Path.Combine(root, ".deviant-web-build"), Guid.NewGuid().ToString());
            Assert.That(Reset(Cutoff), Is.True);
            Assert.That(preferenceClears, Is.EqualTo(1));
            Assert.That(Directory.GetDirectories(root), Is.Empty);
            Assert.That(Directory.GetFiles(root), Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(root, Marker)), Is.EqualTo(Cutoff.ToString("O", CultureInfo.InvariantCulture)));
        }
        [TestCase(0)] [TestCase(1)]
        public void CompletedCutoffAtOrAfterThresholdPreservesAllStorage(int secondsAfter)
        {
            Seed(); Mark(Cutoff.AddSeconds(secondsAfter));
            Assert.That(Reset(Cutoff), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "profile/profile.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "ReplayQueue/nested/replay")), Is.True);
            Assert.That(preferenceClears, Is.Zero);
        }
        [Test] public void OneSecondBeforeThresholdResetsOnlyOnce()
        {
            Seed(); Mark(Cutoff.AddSeconds(-1));
            Assert.That(Reset(Cutoff), Is.True);
            Assert.That(Directory.GetDirectories(root), Is.Empty);
            Seed();
            Assert.That(Reset(Cutoff), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "profile/profile.json")), Is.True);
            Assert.That(preferenceClears, Is.EqualTo(1));
        }
        [Test] public void RepeatedLaunchesAndOlderCutoffPreserveNewProgress()
        {
            Reset(Cutoff); Seed();
            for (int i = 0; i < 3; i++) Assert.That(Reset(Cutoff), Is.False);
            Assert.That(Reset(Cutoff.AddSeconds(-1)), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "profile/profile.json")), Is.True);
            Assert.That(preferenceClears, Is.EqualTo(1));
        }
        [Test] public void EquivalentKoreanTimestampDoesNotReset()
        {
            Seed(); Mark(Cutoff.ToOffset(TimeSpan.FromHours(9)));
            Assert.That(Reset(Cutoff), Is.False);
            Assert.That(preferenceClears, Is.Zero);
        }
        [Test] public void InvalidMarkerResetsOnlyOnce()
        {
            Seed(); File.WriteAllText(Path.Combine(root, Marker), "invalid timestamp");
            Assert.That(Reset(Cutoff), Is.True);
            Assert.That(Reset(Cutoff), Is.False);
            Assert.That(preferenceClears, Is.EqualTo(1));
        }
        [Test] public void FailedPreferenceResetDoesNotMarkCutoffComplete()
        {
            Seed(); Mark(Cutoff.AddSeconds(-1));
            Assert.Throws<IOException>(() => WebBuildStorageReset.ResetIfOlder(root, Cutoff, () => throw new IOException("test failure")));
            Assert.That(File.Exists(Path.Combine(root, Marker)), Is.False);
            Assert.That(Reset(Cutoff), Is.True);
        }
    }
}
