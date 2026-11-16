using System;
using System.IO;
using System.Text;
using Deviant.Replay.Unity;
using NUnit.Framework;

namespace Deviant.Replay.Tests
{
    public class ReplayCollectionTests
    {
        [Test] public void IdentityPersistsAcrossLaunchesAndReplacesInvalidPreference()
        {
            string saved = "bad"; int writes = 0;
            string first = ReplayPlayerIdentity.GetOrCreate(() => saved, value => { saved = value; writes++; });
            Assert.That(Guid.ParseExact(first, "N"), Is.Not.EqualTo(Guid.Empty));
            Assert.That(ReplayPlayerIdentity.GetOrCreate(() => saved, value => writes++), Is.EqualTo(first));
            Assert.That(writes, Is.EqualTo(1));
            saved = Guid.Empty.ToString("N");
            Assert.That(ReplayPlayerIdentity.GetOrCreate(() => saved, value => saved = value), Is.Not.EqualTo(first));
        }
        [TestCase("https://collector.example/", "https://collector.example")]
        [TestCase("http://127.0.0.1:8080/", "http://127.0.0.1:8080")]
        [TestCase("", "")]
        public void EndpointNormalizesSupportedOrigins(string value, string expected) => Assert.That(ReplayUploadBinding.NormalizeEndpoint(value), Is.EqualTo(expected));
        [TestCase("http://collector.example")]
        [TestCase("https://secret@collector.example")]
        [TestCase("https://collector.example?token=secret")]
        public void EndpointRejectsCleartextAndEmbeddedCredentials(string value) => Assert.Throws<ArgumentException>(() => ReplayUploadBinding.NormalizeEndpoint(value));
        [Test] public void LocalFilesRemainIneligibleAndRevocationDoesNotRelabelQueuedPlayers()
        {
            string root = Path.Combine(Path.GetTempPath(), "collection-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ReplayStore(root);
                var h = new ReplayHeader { SessionId = Guid.NewGuid().ToString("N"), SegmentId = Guid.NewGuid().ToString("N"), BuildId = "test", ContentHash = "test" };
                var rec = new ReplayRecorder(0); rec.Add(ReplayKind.SnapshotEnd, 0, -1);
                var local = store.Commit(h, rec.Finish());
                Assert.That(File.Exists(local.Path + ".collection.json"), Is.False);
                h.SegmentId = Guid.NewGuid().ToString("N"); h.FirstSequence = 4;
                var binding = new ReplayUploadBinding { endpoint = "https://collector.example", session = new ReplayCollectionSession
                { player_id = Guid.NewGuid().ToString("N"), session_id = h.SessionId, build_id = "test", platform = "web", started_at = DateTimeOffset.UtcNow.ToString("O") } };
                var remote = store.Commit(h, rec.Finish(), binding);
                Assert.That(File.ReadAllText(remote.Path + ".collection.json"), Does.Contain(binding.session.player_id));
                binding.session.player_id = Guid.NewGuid().ToString("N");
                Assert.Throws<InvalidDataException>(() => store.Commit(h, rec.Finish(), binding));
                store.RevokePendingUploads();
                Assert.That(File.Exists(remote.Path), Is.True);
                Assert.That(Directory.GetFiles(root, "*collection.json", SearchOption.AllDirectories), Is.Empty);
                store.Acknowledge(remote.Path, remote.Sha256);
                Assert.That(File.Exists(local.Path), Is.True);
                Assert.That(File.Exists(remote.Path), Is.False);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        [Test] public void OnlyExactCommittedHashConfirmsAnUpload()
        {
            string expected = new string('a', 64);
            Func<string, bool> confirms = json => ReplayUploadClient.Confirms(Encoding.UTF8.GetBytes(json), expected);
            Assert.That(confirms("{\"state\":\"committed\",\"sha256\":\"" + expected + "\"}"), Is.True);
            Assert.That(confirms("{\"state\":\"uploaded\",\"sha256\":\"" + expected + "\"}"), Is.False);
            Assert.That(confirms("{\"state\":\"committed\",\"sha256\":\"wrong\"}"), Is.False);
            Assert.That(confirms("not json"), Is.False);
        }
    }
}
