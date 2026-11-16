using System;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;

namespace Deviant.Presentation.Unity
{
    // Finite composition routing, not a second runner. Composition retains
    // disposal ownership of the supplied adapters and their Unity outputs.
    public sealed class CueMediaFactory : ICueViewFactory
    {
        readonly SpriteCueFactory sprites;
        readonly AudioCueFactory audio;
        public CueMediaFactory(SpriteCueFactory sprites, AudioCueFactory audio)
        {
            this.sprites = sprites ?? throw new ArgumentNullException(nameof(sprites));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
        }
        public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
        {
            if (definition is PresentationCueDefinition) return sprites.Begin(definition, anchor, resolved);
            if (definition is AudioCueDefinition) return audio.Begin(definition, anchor, resolved);
            resolved(CueViewResult.Failure("Unsupported cue medium.")); return null;
        }
    }
}
