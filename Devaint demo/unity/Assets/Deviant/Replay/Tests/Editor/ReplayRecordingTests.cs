using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Deviant.Replay.Tests
{
    public class ReplayRecordingTests
    {
        static ReplayHeader Header() => new ReplayHeader { SessionId = Guid.NewGuid().ToString("N"), SegmentId = Guid.NewGuid().ToString("N"), BuildId = "test", ContentHash = "test" };
        static ReplayRecord Entity(long t, long x, long y = 0) => new ReplayRecord(ReplayKind.Entity, t, t, new long[] { 1, x, y, 1, ReplayNumbers.Float(100), ReplayNumbers.Float(100), 0, 0, 0, 0, 1, 0 });
        static List<ReplayRecord> Records() => new List<ReplayRecord> { new ReplayRecord(ReplayKind.SnapshotBegin, 0, -1), Entity(0, 0), new ReplayRecord(ReplayKind.SnapshotEnd, 0, -1), Entity(100, 128), Entity(200, 256) };
        [Test] public void RoundTripPreservesIdentityFloatBitsOrderAndFooter()
        {
            var h = Header(); var records = Records(); records.Add(new ReplayRecord(ReplayKind.String, 200, -1, new long[] { 1 }, "inventory.slot_1"));
            records.Add(new ReplayRecord(ReplayKind.UiActivate, 200, 20, new long[] { 1, 0 }));
            var bytes = ReplayCodec.Encode(h, records, out var size); var read = ReplayCodec.Decode(bytes);
            Assert.That(read.Header.Encode(), Is.EqualTo(h.Encode())); Assert.That(size, Is.GreaterThan(bytes.Length));
            Assert.That(read.Records.Count, Is.EqualTo(records.Count + 1));
            for (int i = 0; i < records.Count; i++)
            { Assert.That(read.Records[i].Kind, Is.EqualTo(records[i].Kind)); Assert.That(read.Records[i].Values, Is.EqualTo(records[i].Values)); Assert.That(read.Records[i].TimeMs, Is.EqualTo(records[i].TimeMs)); Assert.That(read.Records[i].Sequence, Is.EqualTo(i + 1)); }
            Assert.That(ReplayNumbers.Unfloat(read.Records[1].Values[4]), Is.EqualTo(100));
        }
        [Test] public void NoncanonicalHeaderUnsupportedSchemaAndInvalidChainAreRejected()
        {
            var h = Header(); string json = h.Encode();
            Assert.Throws<FormatException>(() => ReplayHeader.Decode(json.Replace("\"schema\":1", "\"schema\":2")));
            Assert.Throws<FormatException>(() => ReplayHeader.Decode(json.Replace("\"chunk\":0", "\"chunk\":0,\"chunk\":0")));
            h.ChunkIndex = 1; Assert.Throws<ArgumentException>(() => h.Encode());
        }
        [Test] public void TruncationCorruptionExpansionAndBadSnapshotNeverBecomeValidChunks()
        {
            var packed = ReplayCodec.Encode(Header(), Records(), out _);
            Assert.Catch(() => ReplayCodec.Decode(packed.Take(packed.Length / 2).ToArray()));
            packed[packed.Length / 2] ^= 127; Assert.Catch(() => ReplayCodec.Decode(packed));
            Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(new byte[ReplayCodec.MaxCompressed + 1]));
            var r = Records(); r.RemoveAt(2);
            Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(Header(), r, out _)));
        }
        [Test] public void SimplificationCollapsesLinearMotionButPreservesTurnsEventsAndTimedError()
        {
            var linear = new List<ReplayRecord>(); for (int i = 0; i < 100; i++) linear.Add(Entity(i * 100, i * 4));
            Assert.That(ReplaySimplifier.Simplify(linear).Count, Is.EqualTo(2));
            linear.Insert(50, new ReplayRecord(ReplayKind.UiActivate, 4900, 49, new long[] { 1, 0 }));
            var output = ReplaySimplifier.Simplify(linear);
            Assert.That(output.Any(r => r.Kind == ReplayKind.UiActivate), Is.True);
            Assert.That(output.Count(r => r.Kind == ReplayKind.Entity), Is.EqualTo(4));
            var turn = new List<ReplayRecord> { Entity(0, 0), Entity(100, 100, 50), Entity(1000, 200) };
            Assert.That(ReplaySimplifier.Simplify(turn).Count, Is.EqualTo(3));
        }
        [Test] public void SubthresholdMotionDoesNotAccumulateAndEventsGetContiguousFinalSequences()
        {
            var rec = new ReplayRecorder(0); rec.Add(ReplayKind.SnapshotEnd, 0, -1);
            for (int i = 0; i < 1000; i++) rec.Changed(ReplayKind.Entity, 1, i, i, Entity(i, i).Values);
            var compact = rec.Finish(); var read = ReplayCodec.Decode(ReplayCodec.Encode(Header(), compact, out _));
            Assert.That(read.Records.Last(r => r.Kind == ReplayKind.Entity).Values[1], Is.EqualTo(999));
            Assert.That(read.Records.Count, Is.LessThan(10));
            Assert.That(read.Records.Select(r => r.Sequence), Is.EqualTo(Enumerable.Range(1, read.Records.Count).Select(i => (long)i)));
        }
        [Test] public void DictionaryAndStationaryStateAreStoredOnlyOnce()
        {
            var rec = new ReplayRecorder(0); Assert.That(rec.Intern("action.attack", 0), Is.EqualTo(rec.Intern("action.attack", 0)));
            rec.Add(ReplayKind.SnapshotEnd, 0, -1); for (int i = 1; i <= 100; i++) rec.Changed(ReplayKind.Entity, 1, i, i, Entity(i, 0).Values);
            Assert.That(rec.Finish().Count(r => r.Kind == ReplayKind.String), Is.EqualTo(1));
            Assert.That(rec.Finish().Count(r => r.Kind == ReplayKind.Entity), Is.EqualTo(1));
        }
        [Test] public void LargePositionChangesCutInterpolationAndSealedCandidatesCannotChange()
        {
            var rec = new ReplayRecorder(0); rec.Add(ReplayKind.SnapshotEnd, 0, -1);
            rec.Changed(ReplayKind.Entity, 1, 0, 0, Entity(0, 0).Values);
            rec.Changed(ReplayKind.Entity, 1, 100, 1, Entity(100, 10000).Values);
            var records = rec.SealCandidates(); Assert.That(records.Any(r => r.Kind == ReplayKind.EntityExit && r.Values[1] == 2), Is.True);
            Assert.Throws<InvalidOperationException>(() => rec.Add(ReplayKind.Scene, 200, -1, 0, 0));
            Assert.That(ReplayCodec.Decode(ReplayCodec.Encode(Header(), ReplaySimplifier.Simplify(records), out _)).Records.Count, Is.GreaterThan(4));
        }
        [Test] public void UndefinedDictionaryReferencesAndNonfiniteActorValuesAreRejected()
        {
            var r = Records(); r.Add(new ReplayRecord(ReplayKind.UiActivate, 200, 0, new long[] { 999, 0 }));
            Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(Header(), r, out _)));
            r = Records(); var v = (long[])r[1].Values.Clone(); v[4] = 0x7fc00000;
            r[1] = new ReplayRecord(ReplayKind.Entity, 0, 0, v);
            Assert.Throws<FormatException>(() => ReplayCodec.Decode(ReplayCodec.Encode(Header(), r, out _)));
        }
        [Test] public void RecoveryRebuildsReceiptRejectsConflictAndOnlyDeletesOnMatchingCommit()
        {
            string root = Path.Combine(Path.GetTempPath(), "replay-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ReplayStore(root); var h = Header(); var file = store.Commit(h, Records());
                File.Delete(file.Path + ".json"); var found = store.Recover(); Assert.That(found.Count, Is.EqualTo(1)); Assert.That(File.Exists(file.Path + ".json"), Is.True);
                Assert.That(store.Commit(h, Records()).Sha256, Is.EqualTo(file.Sha256));
                var altered = Records(); altered.Add(Entity(300, 1024)); Assert.Throws<IOException>(() => store.Commit(h, altered));
                Assert.Throws<InvalidDataException>(() => store.Acknowledge(file.Path, "wrong")); Assert.That(File.Exists(file.Path), Is.True);
                store.Acknowledge(file.Path, file.Sha256); Assert.That(File.Exists(file.Path), Is.False);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        [Test] public void EvictionLeavesExplicitMissingChunkMarkerAndRemovesTemporaryFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "replay-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ReplayStore(root); var file = store.Commit(Header(), Records()); File.WriteAllText(Path.Combine(root, "crash.tmp"), "partial");
                store.Trim(0, TimeSpan.FromDays(7), DateTime.UtcNow); Assert.That(File.Exists(file.Path), Is.False);
                Assert.That(File.Exists(file.Path + ".evicted"), Is.True); Assert.That(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        [Test] public void OneHourSyntheticMovementAndCombatReportsActualCompressedSize()
        {
            long total = 0, candidates = 0, retained = 0; var h = Header(); long sequence = 1;
            for (int chunk = 0; chunk < 120; chunk++)
            {
                long begin = chunk * 30000L; var rec = new ReplayRecorder(begin); rec.Add(ReplayKind.SnapshotEnd, begin, -1);
                for (int i = 0; i < 300; i++)
                {
                    long t = begin + i * 100; rec.Add(ReplayKind.Entity, t, t / 20, Entity(t, i * 7, (long)(Math.Sin(i * .08) * 100)).Values);
                    if (i % 20 == 0) rec.Add(ReplayKind.Command, t, t / 20, 1, 1, 1, 0, i, i, 0, ReplayNumbers.Float(i), 0);
                }
                candidates += rec.Count; var records = rec.Finish(); retained += records.Count; h.ChunkIndex = chunk; h.FirstSequence = sequence; h.StartMs = begin;
                var bytes = ReplayCodec.Encode(h, records, out _); var decoded = ReplayCodec.Decode(bytes);
                Assert.That(decoded.Records.Last().TimeMs, Is.EqualTo(begin + 29900)); total += bytes.Length; sequence += records.Count + 1;
                h.PreviousSha256 = ReplayStore.Hash(bytes);
            }
            TestContext.Out.WriteLine("SYNTHETIC_HOUR compressed_bytes=" + total + " candidates=" + candidates + " retained=" + retained);
            Assert.That(total, Is.LessThan(5 * 1024 * 1024), "Synthetic guard only; this is not measured real gameplay.");
        }
    }
}
