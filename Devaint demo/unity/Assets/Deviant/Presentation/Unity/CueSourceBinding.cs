using System;
using System.Collections.Generic;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Presentation.Unity
{
    public interface ICueSourceAssets
    {
        Sprite ReadSprite(CueSourceAssetReference reference);
        AudioClip ReadAudio(CueSourceAssetReference reference);
    }
    // Runtime composition supplies its own Unity references. GUIDs are identity,
    // never a request to search scenes, Resources or the Editor asset database.
    public sealed class CueSourceAssets : ICueSourceAssets
    {
        readonly Dictionary<CueSourceAssetReference, Sprite> sprites;
        readonly Dictionary<CueSourceAssetReference, AudioClip> audio;
        public CueSourceAssets(IDictionary<CueSourceAssetReference, Sprite> sprites, IDictionary<CueSourceAssetReference, AudioClip> audio)
        {
            this.sprites = new Dictionary<CueSourceAssetReference, Sprite>(sprites ?? throw new ArgumentNullException(nameof(sprites)));
            this.audio = new Dictionary<CueSourceAssetReference, AudioClip>(audio ?? throw new ArgumentNullException(nameof(audio)));
        }
        public Sprite ReadSprite(CueSourceAssetReference reference)
        {
            if (!reference.IsAssigned || !sprites.TryGetValue(reference, out var sprite) || !sprite)
                throw new InvalidOperationException("Explicit Sprite asset is unassigned or unavailable: " + reference);
            return sprite;
        }
        public AudioClip ReadAudio(CueSourceAssetReference reference)
        {
            if (!reference.IsAssigned || !audio.TryGetValue(reference, out var clip) || !clip)
                throw new InvalidOperationException("Explicit AudioClip asset is unassigned or unavailable: " + reference);
            return clip;
        }
    }
    // The only publication operation is explicit and whole-candidate. Existing
    // SequenceCueCatalog snapshots retain the old media for running executions.
    public sealed class CueSourceBinding
    {
        readonly SequenceCueCatalog catalog;
        readonly ICueSourceAssets assets;
        bool publishing;
        public CompiledCueSource Source { get; private set; }
        public Guid CatalogVersion => catalog.Version;
        public CueSourceBinding(SequenceCueCatalog catalog, ICueSourceAssets assets)
        { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); this.assets = assets ?? throw new ArgumentNullException(nameof(assets)); }
        public bool TryPublish(TextAsset candidate, out string error)
        {
            try { return TryPublish(CueSourceJson.Compile(candidate), out error); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { error = exception.Message; return false; }
        }
        public bool TryPublish(CompiledCueSource candidate, out string error)
        {
            error = null;
            if (publishing) { error = "Cue source publication cannot reenter media resolution."; return false; }
            publishing = true;
            try
            {
                catalog.Publish(Prepare(candidate, assets)); Source = candidate; return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { error = exception.Message; return false; }
            finally { publishing = false; }
        }
        public static SequenceCueMedia[] Prepare(CompiledCueSource candidate, ICueSourceAssets assets)
        {
            if (candidate == null || assets == null) throw new ArgumentNullException(nameof(candidate));
            var media = new SequenceCueMedia[candidate.Cues.Count];
            for (int i = 0; i < media.Length; i++)
            {
                var cue = candidate.Cues[i];
                try
                {
                    if (cue.Definition is PresentationCueDefinition sprite)
                    {
                        var frames = new Sprite[cue.SpriteAssets.Count];
                        for (int frame = 0; frame < frames.Length; frame++)
                        {
                            frames[frame] = assets.ReadSprite(cue.SpriteAssets[frame]);
                            if (!frames[frame]) throw new InvalidOperationException("Required Sprite frame is unavailable: " + sprite.Frames[frame]);
                        }
                        media[i] = SequenceCueMedia.Sprite(sprite, frames);
                    }
                    else
                    {
                        var audio = (AudioCueDefinition)cue.Definition; var clip = assets.ReadAudio(cue.AudioAsset);
                        if (!clip) throw new InvalidOperationException("Required AudioClip is unavailable: " + cue.AudioAsset);
                        if (clip.samples != audio.SampleCount || clip.frequency != audio.Frequency || clip.loadState != AudioDataLoadState.Loaded)
                            throw new InvalidOperationException("AudioClip must be loaded and match the declared sample count/frequency. Expected " +
                                audio.SampleCount + " samples @ " + audio.Frequency + "Hz; actual " + clip.samples + " samples @ " + clip.frequency + "Hz, state=" + clip.loadState + ".");
                        media[i] = SequenceCueMedia.Audio(audio, clip);
                    }
                }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                { throw new InvalidOperationException("Cue '" + cue.Definition.Id + "': " + exception.Message, exception); }
            }
            return media;
        }
    }
}
