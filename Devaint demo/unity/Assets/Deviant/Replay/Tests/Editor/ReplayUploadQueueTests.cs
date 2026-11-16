using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Deviant.Replay.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Replay.Tests
{
    public sealed class ReplayUploadQueueTests
    {
        const string Endpoint = "https://collector.example";
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        string root;
        [SetUp] public void SetUp() { root = Path.Combine(Path.GetTempPath(), "replay-queue-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        static void Finish(IEnumerator wait)
        {
            var deadline = Stopwatch.StartNew();
            while (wait.MoveNext())
            {
                if (wait.Current is IEnumerator nested) Finish(nested);
                else Thread.Sleep(1);
                Assert.That(deadline.Elapsed.TotalSeconds, Is.LessThan(8), "queue read completes");
            }
        }
        ReplayFile FileFor(string endpoint = Endpoint)
        {
            var header = new ReplayHeader { SessionId = ReplayUuid7.NewId(), SegmentId = ReplayUuid7.NewId(), BuildId = "queue-test", ContentHash = "queue-test" };
            var binding = new ReplayUploadBinding { endpoint = endpoint, session = new ReplayCollectionSession {
                player_id = Guid.NewGuid().ToString("N"), session_id = header.SessionId, build_id = "queue-test", platform = "editor", started_at = DateTimeOffset.UtcNow.ToString("O")
            }};
            var recorder = new ReplayRecorder(0); recorder.Add(ReplayKind.SnapshotEnd, 0, -1);
            var file = new ReplayStore(root).Commit(header, recorder.Finish(), binding);
            new ReplayStore(root).MarkSession(header.SessionId, 100, 1, true, false);
            return file;
        }
        ReplayUploadQueue.Result Read(string endpoint = Endpoint)
        {
            var read = new ReplayUploadQueue.Read(root, endpoint); Finish(read.Wait());
            Assert.That(read.Result.Error, Is.Null); return read.Result;
        }
        [Test] public void SlowStorageRunsOffTheGameThreadAndStopDoesNotJoinIt()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var disposed = new ManualResetEventSlim())
            {
                int owner = Thread.CurrentThread.ManagedThreadId, worker = owner;
                IEnumerator Slow(ReplayUploadQueue.Result result)
                {
                    try { worker = Thread.CurrentThread.ManagedThreadId; entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); yield return null; }
                    finally { disposed.Set(); }
                }
                var read = new ReplayUploadQueue.Read(Slow);
                var go = new GameObject("Queue cancellation test"); var client = go.AddComponent<ReplayUploadClient>();
                try
                {
                    Assert.That(entered.Wait(1000), Is.True);
                    Assert.That(worker, Is.Not.EqualTo(owner), "storage must not execute inside the game coroutine");
                    typeof(ReplayUploadClient).GetField("queueRead", Hidden).SetValue(client, read);
                    var waiting = read.Wait(); var timer = Stopwatch.StartNew();
                    Assert.That(waiting.MoveNext(), Is.True); client.StopUploads(); timer.Stop();
                    Assert.That(timer.Elapsed.TotalMilliseconds, Is.LessThan(100), "a blocked storage worker must not block a frame or stop request");
                    Assert.That(read.Cancelled, Is.True); Assert.That(disposed.IsSet, Is.False, "stop did not wait for storage");
                    release.Set(); Finish(waiting); Assert.That(disposed.IsSet, Is.True);
                }
                finally { release.Set(); read.Cancel(); Finish(read.Wait()); UnityEngine.Object.DestroyImmediate(go); }
            }
        }
        [Test] public void ReconfigurationReapsOldScanWithoutPublishingItsResult()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                IEnumerator Slow(ReplayUploadQueue.Result result) { result.Error = "stale-result"; entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); yield return null; }
                var old = new ReplayUploadQueue.Read(Slow);
                var go = new GameObject("Queue reconfiguration test"); var client = go.AddComponent<ReplayUploadClient>();
                try
                {
                    Assert.That(entered.Wait(1000), Is.True);
                    typeof(ReplayUploadClient).GetField("queueRead", Hidden).SetValue(client, old);
                    client.StopUploads();
                    typeof(ReplayUploadClient).GetField("root", Hidden).SetValue(client, root);
                    typeof(ReplayUploadClient).GetField("endpoint", Hidden).SetValue(client, Endpoint);
                    var read = (IEnumerator)typeof(ReplayUploadClient).GetMethod("ReadQueue", Hidden).Invoke(client, null);
                    Assert.That(read.MoveNext(), Is.True);
                    Assert.That(((IEnumerator)read.Current).MoveNext(), Is.True, "the previous worker is awaited without joining it");
                    Assert.That(typeof(ReplayUploadClient).GetField("queueRead", Hidden).GetValue(client), Is.SameAs(old), "no overlapping reader starts");
                    release.Set(); Finish((IEnumerator)read.Current); Finish(read);
                    Assert.That(client.LastError, Is.Null, "cancelled results cannot update the newly configured uploader");
                }
                finally { release.Set(); old.Cancel(); Finish(old.Wait()); UnityEngine.Object.DestroyImmediate(go); }
            }
        }
        [Test] public void QueueReadRetainsBytesHashAndStatusWithoutAcknowledgingAnything()
        {
            var file = FileFor(); var scan = Read();
            Assert.That(scan.File.Path, Is.EqualTo(file.Path)); CollectionAssert.AreEqual(File.ReadAllBytes(file.Path), scan.File.Bytes);
            Assert.That(scan.File.Receipt.sha256, Is.EqualTo(file.Sha256));
            Assert.That(scan.Session, Is.Not.Null); Assert.That(System.Text.Encoding.UTF8.GetString(scan.Session.Bytes), Does.Contain("\"ended\":true").And.Not.Contain("\"session\""));
            Assert.That(File.Exists(file.Path), Is.True, "reading cannot acknowledge or delete a queued chunk");
            Assert.That(File.Exists(scan.Session.Path + ".uploaded"), Is.False);
        }
        [Test] public void IntegrityRejectionIsReadOnlyUntilTheUploaderAppliesIt()
        {
            var file = FileFor(); File.WriteAllBytes(file.Path, new byte[] {1,2,3});
            var scan = Read(); Assert.That(scan.File, Is.Null);
            Assert.That(scan.Rejections.Single().Reason, Is.EqualTo("local_integrity"));
            Assert.That(File.Exists(file.Path), Is.True); Assert.That(File.Exists(file.Path + ".upload-error"), Is.False);
        }
        [Test] public void UploadedStatusIsSkippedButAChangedSessionIsPickedUpAgain()
        {
            var file = FileFor(); var first = Read().Session;
            File.WriteAllText(first.Path + ".uploaded", first.Hash);
            Assert.That(Read().Session, Is.Null);
            new ReplayStore(root).MarkSession(file.Chunk.Header.SessionId, 200, 2, true, false);
            var changed = Read().Session;
            Assert.That(changed, Is.Not.Null); Assert.That(changed.Hash, Is.Not.EqualTo(first.Hash));
        }
        [Test] public void AnotherCollectorQueueRemainsUntouched()
        {
            var file = FileFor(); var scan = Read("https://another.example");
            Assert.That(scan.File, Is.Null); Assert.That(scan.Session, Is.Null); Assert.That(scan.Rejections, Is.Empty);
            Assert.That(File.Exists(file.Path), Is.True);
        }
        [Test] public void CooperativeReaderYieldsAndDisposesOnCancellation()
        {
            int steps = 0; bool disposed = false;
            IEnumerator Work(ReplayUploadQueue.Result result)
            {
                try { for (int i = 0; i < 1000; i++) { steps++; Thread.Sleep(1); yield return null; } }
                finally { disposed = true; }
            }
            var read = new ReplayUploadQueue.Read(Work, false); var waiting = read.Wait();
            Assert.That(waiting.MoveNext(), Is.True); Assert.That(steps, Is.InRange(1, 10), "WebGL work is split between render frames");
            read.Cancel(); Finish(waiting); Assert.That(disposed, Is.True);
        }
    }
}
