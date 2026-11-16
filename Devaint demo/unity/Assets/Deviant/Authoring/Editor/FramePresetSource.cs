using System;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    // Version-locked Editor experiment, not a second combat source or product save.
    // Only frame/marker overrides and source identities are persisted.
    public static class FramePresetSource
    {
        public const int SchemaVersion = 2;
        public const int MaximumFileBytes = 1024 * 1024;
        public const string Kind = "FirstSwordFrameExperiment";
        [Serializable] sealed class FileData
        {
            public int schemaVersion;
            public string kind, actionId, actionHash, motionId, motionHash, candidateHash;
            public float inputSpeed;
            public FramePreviewStep[] frames;
            public FramePreviewMarker[] markers;
        }
        public static MeleeFrameDefinition Load(string path, string repository, out string fileHash)
        {
            string json = SourceFiles.Read(path, out fileHash, MaximumFileBytes);
            ReadSources(repository, out var action, out var motion);
            return Deserialize(json, action, motion, FrameMotionSource.ReadInputSpeed(repository));
        }
        public static string Save(string path, string expectedHash, string repository, MeleeFrameDefinition candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            ReadSources(repository, out var action, out var motion);
            CheckSources(candidate.SourceAction.ContentHash, candidate.SourceMotion.ContentHash, action, motion);
            string json = Serialize(candidate);
            if (expectedHash == null) return SourceFiles.Create(path, json);
            string previous = SourceFiles.Read(path, out var actualHash, MaximumFileBytes);
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal)) throw new IOException("Frame experiment changed outside this window. Reload before saving.");
            Deserialize(previous, action, motion, FrameMotionSource.ReadInputSpeed(repository)); // An unrelated or incompatible existing file is never replaced.
            return SourceFiles.Save(path, expectedHash, json, MaximumFileBytes);
        }
        public static string Serialize(MeleeFrameDefinition candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            var data = new FileData {
                schemaVersion = SchemaVersion, kind = Kind, actionId = FixedAttackMotion.FirstSwordAction, actionHash = candidate.SourceAction.ContentHash,
                motionId = FixedAttackMotion.FirstSwordId, motionHash = candidate.SourceMotion.ContentHash, candidateHash = candidate.ContentHash,
                inputSpeed = candidate.InputSpeed,
                frames = candidate.Timeline.Frames.Select(f => new FramePreviewStep { id = f.Id, seconds = f.Seconds }).ToArray(),
                markers = candidate.Timeline.Markers.OrderBy(m => m.Id, StringComparer.Ordinal).Select(m => new FramePreviewMarker { id = m.Id, anchor = m.Anchor, reference = m.Reference, offset = m.Offset }).ToArray()
            };
            string json = JsonUtility.ToJson(data, true) + "\n";
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new ArgumentException("Frame experiment exceeds the allowed file size.");
            return json;
        }
        public static MeleeFrameDefinition Deserialize(string json, BasicMeleeDefinition action, FixedAttackMotion motion, float inputSpeed)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new ArgumentException("A bounded frame experiment document is required.");
            var data = JsonUtility.FromJson<FileData>(json);
            if (data == null || data.schemaVersion != SchemaVersion || data.kind != Kind || data.actionId != FixedAttackMotion.FirstSwordAction || data.motionId != FixedAttackMotion.FirstSwordId)
                throw new ArgumentException("Unsupported frame experiment kind, schema or content IDs.");
            CheckSources(data.actionHash, data.motionHash, action, motion);
            var timeline = new FrameTimeline(data.frames?.Select(f => f == null ? null : new TimelineFrame(f.id, f.seconds)),
                data.markers?.Select(m => m == null ? null : new TimelineMarker(m.id, m.anchor, m.reference, m.offset)));
            if (data.inputSpeed != inputSpeed) throw new ArgumentException("Frame experiment movement policy changed. Reload the matching source context.");
            var candidate = MeleeFrameCompiler.CompileFirst(action, motion, timeline, inputSpeed);
            if (!string.Equals(data.candidateHash, candidate.ContentHash, StringComparison.Ordinal)) throw new ArgumentException("Frame experiment candidate hash does not match its edits.");
            return candidate;
        }
        static void CheckSources(string actionHash, string motionHash, BasicMeleeDefinition action, FixedAttackMotion motion)
        {
            if (action == null || motion == null || !string.Equals(actionHash, action.ContentHash, StringComparison.Ordinal) || !string.Equals(motionHash, motion.ContentHash, StringComparison.Ordinal))
                throw new ArgumentException("Frame experiment requires the same fixed action and motion source versions. Re-read the matching sources.");
        }
        static void ReadSources(string repository, out BasicMeleeDefinition action, out FixedAttackMotion motion)
        {
            action = BasicSwordSource.Validate(BasicSwordSource.Load(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"), out _));
            motion = FrameMotionSource.Load(repository, out _).FixedBinding;
        }
    }
}
