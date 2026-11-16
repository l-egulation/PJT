using System;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    public sealed class SequenceCueMedia
    {
        public ICueDefinition Definition { get; }
        readonly Sprite[] sprites;
        readonly AudioClip clip;
        SequenceCueMedia(ICueDefinition definition, Sprite[] sprites, AudioClip clip)
        { Definition = definition; this.sprites = sprites; this.clip = clip; }
        public static SequenceCueMedia Sprite(PresentationCueDefinition definition, Sprite[] frames)
        {
            if (definition == null || frames == null) throw new ArgumentNullException(nameof(definition));
            return new SequenceCueMedia(definition, (Sprite[])frames.Clone(), null);
        }
        public static SequenceCueMedia Audio(AudioCueDefinition definition, AudioClip clip)
        {
            if (definition == null || !clip) throw new ArgumentException("An audio definition and explicit clip are required.");
            return new SequenceCueMedia(definition, null, clip);
        }
        internal Entry Bind(SpriteCueFactory spriteFactory, AudioCueFactory audioFactory)
        {
            if (Definition is PresentationCueDefinition sprite)
            {
                if (spriteFactory == null) throw new InvalidOperationException("The sequence catalog has no sprite output factory.");
                var captured = spriteFactory.Capture(sprite, sprites);
                return new Entry(Definition, captured, captured.Validate);
            }
            if (audioFactory == null) throw new InvalidOperationException("The sequence catalog has no audio output factory.");
            var audio = audioFactory.Capture((AudioCueDefinition)Definition, clip);
            return new Entry(Definition, audio, audio.Validate);
        }
        internal sealed class Entry
        {
            internal readonly ICueDefinition Definition;
            internal readonly ICueViewFactory Factory;
            internal readonly Action Validate;
            internal Entry(ICueDefinition definition, ICueViewFactory factory, Action validate)
            { Definition = definition; Factory = factory; Validate = validate; }
        }
    }

    // Explicit Unity references, not product sound content or a second player.
    // Publishing replaces the whole catalog only after every binding validates.
    // Captures retain the old definitions and references without creating views;
    // their sources/renderers remain owned by the existing CuePlayer scope.
    public sealed class SequenceCueCatalog : ICueCatalog
    {
        readonly SpriteCueFactory sprites;
        readonly AudioCueFactory audio;
        Dictionary<string, SequenceCueMedia.Entry> entries = new Dictionary<string, SequenceCueMedia.Entry>(StringComparer.Ordinal);
        public Guid Version { get; private set; } = Guid.NewGuid();
        public SequenceCueCatalog(SpriteCueFactory sprites = null, AudioCueFactory audio = null)
        { this.sprites = sprites; this.audio = audio; }
        public void Publish(params SequenceCueMedia[] media)
        {
            if (media == null) throw new ArgumentNullException(nameof(media));
            var next = new Dictionary<string, SequenceCueMedia.Entry>(StringComparer.Ordinal);
            foreach (var item in media)
            {
                if (item == null) throw new ArgumentException("Sequence cue media entries must be non-null.", nameof(media));
                next.Add(item.Definition.Id, item.Bind(sprites, audio));
            }
            entries = next; Version = Guid.NewGuid();
        }
        public ICueCatalogSnapshot Capture(IReadOnlyList<string> cueIds)
        {
            if (cueIds == null) throw new ArgumentNullException(nameof(cueIds));
            var selected = new Dictionary<string, SequenceCueMedia.Entry>(StringComparer.Ordinal);
            foreach (var id in cueIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !entries.TryGetValue(id, out var entry))
                    throw new InvalidOperationException("Missing required sequence cue: " + id);
                if (selected.ContainsKey(id)) continue;
                entry.Validate(); selected.Add(id, entry);
            }
            return new Snapshot(Version, selected);
        }
        sealed class Snapshot : ICueCatalogSnapshot
        {
            readonly Dictionary<string, SequenceCueMedia.Entry> entries;
            public Guid Version { get; }
            internal Snapshot(Guid version, Dictionary<string, SequenceCueMedia.Entry> entries) { Version = version; this.entries = entries; }
            public ICueDefinition Read(string cueId)
            {
                if (string.IsNullOrWhiteSpace(cueId) || !entries.TryGetValue(cueId, out var entry))
                    throw new InvalidOperationException("Cue was not captured by this sequence catalog version: " + cueId);
                return entry.Definition;
            }
            public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
            {
                if (definition == null || !entries.TryGetValue(definition.Id, out var entry) || entry.Definition.ContentHash != definition.ContentHash)
                { resolved(CueViewResult.Failure("Cue does not belong to the captured sequence catalog version.")); return null; }
                return entry.Factory.Begin(definition, anchor, resolved);
            }
        }
    }
}
