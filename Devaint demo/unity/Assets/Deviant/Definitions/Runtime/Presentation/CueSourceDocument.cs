using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Deviant.Definitions
{
    [Serializable]
    public sealed class CueSourceDocument
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion;
        public string id;
        public CueSourceEntryDocument[] cues;
    }
    [Serializable]
    public sealed class CueSourceEntryDocument
    {
        public string id, kind;
        public bool loop, flipX;
        public CueSourceFrameDocument[] frames;
        public string clipId;
        public int sampleCount, frequency;
        public CueSourceAssetDocument audioAsset;
    }
    [Serializable]
    public sealed class CueSourceFrameDocument
    {
        public string id;
        public float seconds, x, y;
        public CueSourceAssetDocument asset;
    }
    [Serializable]
    public sealed class CueSourceAssetDocument
    {
        public string guid;
        public long localFileId;
    }
    public enum CueSourceKind { Sprite, Audio }

    // Unity identity only; paths/names are not fallback lookup keys. An empty
    // pair records an explicitly unassigned asset and can be saved, not played.
    public readonly struct CueSourceAssetReference : IEquatable<CueSourceAssetReference>
    {
        public string Guid { get; }
        public long LocalFileId { get; }
        public bool IsAssigned => !string.IsNullOrEmpty(Guid);
        public CueSourceAssetReference(string guid, long localFileId)
        {
            if (string.IsNullOrEmpty(guid) && localFileId == 0) { Guid = ""; LocalFileId = 0; return; }
            if (guid == null || guid.Length != 32 || guid.Any(c => !Uri.IsHexDigit(c)) || localFileId == 0)
                throw new ArgumentException("An asset reference requires a 32-digit GUID and nonzero local file ID, or an empty pair.");
            Guid = guid.ToLowerInvariant(); LocalFileId = localFileId;
        }
        public CueSourceAssetDocument ToDocument() => new CueSourceAssetDocument { guid = Guid ?? "", localFileId = LocalFileId };
        public bool Equals(CueSourceAssetReference other) => string.Equals(Guid ?? "", other.Guid ?? "", StringComparison.Ordinal) && LocalFileId == other.LocalFileId;
        public override bool Equals(object value) => value is CueSourceAssetReference other && Equals(other);
        public override int GetHashCode() => ((Guid ?? "").GetHashCode() * 397) ^ LocalFileId.GetHashCode();
        public override string ToString() => IsAssigned ? Guid + ":" + LocalFileId : "<unassigned>";
    }
    public sealed class CompiledCueSourceEntry
    {
        public ICueDefinition Definition { get; }
        public CueSourceKind Kind { get; }
        public IReadOnlyList<CueSourceAssetReference> SpriteAssets { get; }
        public CueSourceAssetReference AudioAsset { get; }
        internal CompiledCueSourceEntry(ICueDefinition definition, CueSourceAssetReference[] sprites, CueSourceAssetReference audio)
        {
            Definition = definition; Kind = definition is PresentationCueDefinition ? CueSourceKind.Sprite : CueSourceKind.Audio;
            SpriteAssets = Array.AsReadOnly(sprites); AudioAsset = audio;
        }
        internal CueSourceEntryDocument ToDocument()
        {
            if (Definition is AudioCueDefinition audio)
                return new CueSourceEntryDocument { id = audio.Id, kind = nameof(CueSourceKind.Audio), loop = audio.Loop,
                    clipId = audio.ClipId, sampleCount = audio.SampleCount, frequency = audio.Frequency, audioAsset = AudioAsset.ToDocument() };
            var sprite = (PresentationCueDefinition)Definition;
            return new CueSourceEntryDocument { id = sprite.Id, kind = nameof(CueSourceKind.Sprite), loop = sprite.Loop, flipX = sprite.FlipX,
                frames = Enumerable.Range(0, sprite.Frames.Count).Select(i => new CueSourceFrameDocument {
                    id = sprite.Frames[i], seconds = sprite.Seconds[i], x = sprite.X[i], y = sprite.Y[i], asset = SpriteAssets[i].ToDocument()
                }).ToArray() };
        }
    }
    public sealed class CompiledCueSource
    {
        public string Id { get; }
        public IReadOnlyList<CompiledCueSourceEntry> Cues { get; }
        public string DefinitionHash { get; }
        public string BindingHash { get; }
        public string ContentHash { get; }
        internal CompiledCueSource(string id, CompiledCueSourceEntry[] cues)
        {
            Id = id; Cues = Array.AsReadOnly(cues);
            DefinitionHash = Hash(writer => { writer.Write(1); writer.Write(Id); writer.Write(cues.Length);
                foreach (var cue in cues) { writer.Write((int)cue.Kind); writer.Write(cue.Definition.ContentHash); } });
            BindingHash = Hash(writer => { writer.Write(1); writer.Write(cues.Length);
                foreach (var cue in cues)
                {
                    writer.Write(cue.Definition.Id); writer.Write(cue.SpriteAssets.Count);
                    foreach (var asset in cue.SpriteAssets) Write(writer, asset);
                    Write(writer, cue.AudioAsset);
                }
            });
            ContentHash = Hash(writer => { writer.Write(CueSourceDocument.CurrentSchemaVersion); writer.Write(DefinitionHash); writer.Write(BindingHash); });
        }
        static void Write(BinaryWriter writer, CueSourceAssetReference asset) { writer.Write(asset.Guid ?? ""); writer.Write(asset.LocalFileId); }
        static string Hash(Action<BinaryWriter> write)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) write(writer);
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public CompiledCueSourceEntry Read(string cueId)
        {
            foreach (var cue in Cues) if (cue.Definition.Id == cueId) return cue;
            throw new InvalidOperationException("Cue is not in this source: " + cueId);
        }
        public CueSourceDocument ToDocument() => new CueSourceDocument { schemaVersion = CueSourceDocument.CurrentSchemaVersion, id = Id, cues = Cues.Select(cue => cue.ToDocument()).ToArray() };
    }
    public static class CueSourceCompiler
    {
        public static CompiledCueSource Compile(CueSourceDocument document)
        {
            if (document == null || document.schemaVersion != CueSourceDocument.CurrentSchemaVersion)
                throw new ArgumentException("Unsupported or missing Cue source schema.", nameof(document));
            if (string.IsNullOrWhiteSpace(document.id) || document.cues == null || document.cues.Any(cue => cue == null) ||
                document.cues.Select(cue => cue.id).Distinct(StringComparer.Ordinal).Count() != document.cues.Length)
                throw new ArgumentException("A stable catalog ID and explicit array of unique non-null Cue entries are required.");
            return new CompiledCueSource(document.id, document.cues.Select(CompileEntry).ToArray());
        }
        static CompiledCueSourceEntry CompileEntry(CueSourceEntryDocument cue)
        {
            if (cue.kind == nameof(CueSourceKind.Sprite))
            {
                if (!string.IsNullOrEmpty(cue.clipId) || cue.sampleCount != 0 || cue.frequency != 0 || Read(cue.audioAsset).IsAssigned)
                    throw new ArgumentException("Sprite Cue contains Audio fields: " + cue.id);
                if (cue.frames == null || cue.frames.Any(frame => frame == null)) throw new ArgumentException("Explicit non-null Sprite frames are required: " + cue.id);
                // Existing definitions alone own frame timing, offsets and hashes.
                var definition = new PresentationCueDefinition(cue.id, cue.frames.Select(frame => frame.id).ToArray(), cue.frames.Select(frame => frame.seconds).ToArray(),
                    cue.frames.Select(frame => frame.x).ToArray(), cue.frames.Select(frame => frame.y).ToArray(), cue.flipX, cue.loop);
                return new CompiledCueSourceEntry(definition, cue.frames.Select(frame => Read(frame.asset)).ToArray(), default);
            }
            if (cue.kind == nameof(CueSourceKind.Audio))
            {
                if (cue.flipX || cue.frames != null && cue.frames.Length != 0) throw new ArgumentException("Audio Cue contains Sprite fields: " + cue.id);
                var definition = new AudioCueDefinition(cue.id, cue.clipId, cue.sampleCount, cue.frequency, cue.loop);
                return new CompiledCueSourceEntry(definition, Array.Empty<CueSourceAssetReference>(), Read(cue.audioAsset));
            }
            throw new ArgumentException("Cue kind must be exactly Sprite or Audio: " + cue.id);
        }
        static CueSourceAssetReference Read(CueSourceAssetDocument asset) => asset == null ? default : new CueSourceAssetReference(asset.guid, asset.localFileId);
    }
}
