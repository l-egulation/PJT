using System;
using System.IO;
using System.Text;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public static class FrameMotionSource
    {
        public const int MaximumFileBytes = 1024 * 1024;
        public const string ActionPath = "data/combat/actions/base_sword_basic_0.json";
        [Serializable] sealed class FrameDocument
        {
            public int schemaVersion;
            public string id, actionId, mode;
            public FixedAttackMotionBindingDocument fixedBinding;
            public FrameTimelineDocument timeline;
        }
        public static FrameAttackMotionSource Load(string repository, out string fileHash)
        {
            var document = JsonUtility.FromJson<AttackMotionDocument>(SourceFiles.Read(Path.Combine(repository, AttackMotionSource.RelativePath), out fileHash, MaximumFileBytes));
            var source = new FrameAttackMotionSource(document);
            if (source.UsesFrameTime) source.Compile(ReadAction(repository), ReadInputSpeed(repository));
            return source;
        }
        public static BasicMeleeDefinition ReadAction(string repository)
            => BasicSwordSource.Validate(BasicSwordSource.Load(Path.Combine(repository, ActionPath), out _));
        public static float ReadInputSpeed(string repository)
        {
            var combat = MotionRegistrySource.ReadCombat(repository);
            return System.Linq.Enumerable.Single(combat.forms, form => form.id == "base_sword").inputSpeed;
        }
        public static MeleeFrameDefinition ReadCandidate(string repository, out string fileHash)
        {
            var source = Load(repository, out fileHash); var action = ReadAction(repository);
            float inputSpeed = ReadInputSpeed(repository);
            return source.UsesFrameTime ? source.Compile(action, inputSpeed) : MeleeFrameCompiler.CompileFirst(action, source.FixedBinding, MeleeFrameCompiler.ProjectFirst(action, source.FixedBinding, inputSpeed), inputSpeed);
        }
        public static string Save(string repository, string expectedHash, MeleeFrameDefinition candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            var current = Load(repository, out var actualHash);
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                throw new IOException("Motion source changed outside this window. Reload before saving; no file was overwritten.");
            var action = ReadAction(repository);
            if (candidate.SourceAction.ContentHash != action.ContentHash || candidate.SourceMotion.ContentHash != current.FixedBinding.ContentHash)
                throw new ArgumentException("Action or read-only media/art binding changed. Reload the current source before saving frame timing.");
            var promoted = FrameAttackMotionSource.FromCandidate(candidate);
            promoted.Compile(action, ReadInputSpeed(repository)); // The same compiler used by fresh runtime initialization.
            return SourceFiles.Save(Path.Combine(repository, AttackMotionSource.RelativePath), expectedHash, Serialize(promoted), MaximumFileBytes);
        }
        public static string ReturnFixedSource(string repository, string expectedHash, FixedAttackMotion capturedBinding)
        {
            var current = Load(repository, out var actualHash);
            if (actualHash != expectedHash) throw new IOException("First motion source changed outside this window. Reload before returning fixed mode; no source was overwritten.");
            if (capturedBinding == null || capturedBinding.ContentHash != current.FixedBinding.ContentHash) throw new ArgumentException("The captured delegated first media binding changed. Reload before returning fixed mode.");
            var action = ReadAction(repository);
            MeleeFrameCompiler.CompileFirst(action, capturedBinding, MeleeFrameCompiler.ProjectFirst(action, capturedBinding, ReadInputSpeed(repository)), ReadInputSpeed(repository));
            return SourceFiles.Save(Path.Combine(repository, AttackMotionSource.RelativePath), expectedHash, Serialize(new FrameAttackMotionSource(capturedBinding.ToDocument())), MaximumFileBytes);
        }
        public static string Serialize(FrameAttackMotionSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string json;
            if (source.UsesFrameTime)
            {
                var document = source.ToDocument();
                json = JsonUtility.ToJson(new FrameDocument {
                    schemaVersion = document.schemaVersion, id = document.id, actionId = document.actionId, mode = document.mode,
                    fixedBinding = document.fixedBinding, timeline = document.timeline
                }, true) + "\n";
            }
            else json = JsonUtility.ToJson(FixedAttackMotionBindingDocument.From(source.FixedBinding), true) + "\n";
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new ArgumentException("Motion source exceeds the allowed file size.");
            return json;
        }
    }
}
