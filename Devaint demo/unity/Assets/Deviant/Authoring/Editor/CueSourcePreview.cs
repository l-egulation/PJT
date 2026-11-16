using System;
using Deviant.Definitions;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Deviant.Authoring
{
    // Real CuePlayer/factories in an isolated scene. Its explicitly advanced
    // presentation clock has no simulation, gameplay or global-time authority.
    public sealed class CueSourcePreview : IDisposable
    {
        readonly Scene scene;
        readonly GameObject spriteRoot, audioRoot;
        readonly Camera camera;
        readonly RenderTexture texture;
        readonly SpriteCueFactory sprites;
        readonly AudioCueFactory audio;
        readonly SequenceCueCatalog catalog;
        readonly CuePlayer player;
        readonly Clock clock = new Clock();
        readonly CueSourceBinding binding;
        ICueScope scope;
        ICueHandle current;
        SpriteRenderer renderer;
        AudioCueOutputBinding audioOutput;
        bool disposed;
        public RenderTexture Texture => texture;
        public Scene PreviewScene => scene;
        public CueSourceBinding Binding => binding;
        public ICueHandle Current => current;
        public ICueDefinition Definition { get; private set; }
        public string StartedSourceVersion { get; private set; }
        public Guid StartedCatalogVersion { get; private set; }
        public float Time => clock.Time;
        public int ActiveCount => player.ActiveCount;
        public int LiveSpriteCount => sprites.LiveViewCount;
        public int LiveAudioCount => audio.LiveOutputCount;
        public SpriteRenderer SpriteOutput => renderer;
        public AudioCueOutputBinding AudioOutput => audioOutput;
        public bool IsRunning => current != null && (current.Status == CueStatus.Pending || current.Status == CueStatus.Playing);
        public CueSourcePreview(CompiledCueSource candidate, ICueSourceAssets assets)
        {
            if (candidate == null || assets == null) throw new ArgumentNullException(nameof(candidate));
            scene = EditorSceneManager.NewPreviewScene();
            try
            {
                spriteRoot = Root("CueSourcePrivateSprites"); audioRoot = Root("CueSourcePrivateAudio");
                sprites = new SpriteCueFactory(outputRoot: spriteRoot.transform); audio = new AudioCueFactory(audioRoot.transform);
                catalog = new SequenceCueCatalog(sprites, audio); player = new CuePlayer(new CueMediaFactory(sprites, audio));
                binding = new CueSourceBinding(catalog, assets);
                if (!binding.TryPublish(candidate, out var error)) throw new InvalidOperationException(error);
                texture = new RenderTexture(760, 300, 24) { name = "CueSourcePrivatePreview", hideFlags = HideFlags.HideAndDontSave }; texture.Create();
                camera = Root("CueSourcePrivateCamera").AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 760f / 300;
                camera.transform.position = new Vector3(0, 0, -10); camera.targetTexture = texture;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .14f); camera.scene = scene;
            }
            catch { Dispose(); throw; }
        }
        GameObject Root(string name)
        {
            var root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, scene); return root;
        }
        public bool TryPublish(CompiledCueSource candidate, out string error) { Check(); return binding.TryPublish(candidate, out error); }
        public bool TryStart(string cueId, out string error)
        {
            Check(); error = null;
            if (IsRunning) { error = "Cancel or complete the current preview before starting another occurrence."; return false; }
            try
            {
                var captured = catalog.Capture(new[] { cueId }); var definition = captured.Read(cueId);
                if (definition is AudioCueDefinition && !Application.isPlaying)
                    throw new InvalidOperationException("Enter Play mode for actual AudioSource audition; metadata/save/export are available in Edit mode.");
                scope?.Dispose(); scope = player.OpenScope("private Cue source preview", captured); clock.Time = 0;
                Definition = definition; StartedSourceVersion = binding.Source.ContentHash; StartedCatalogVersion = captured.Version;
                current = scope.Play(definition, new CueOccurrence(Guid.NewGuid(), definition.Id), new CueAnchor(0, 0, 1), clock);
                if (current.Status == CueStatus.Failed) { error = current.Error; return false; }
                renderer = definition is PresentationCueDefinition ? spriteRoot.transform.GetChild(spriteRoot.transform.childCount - 1).GetComponent<SpriteRenderer>() : null;
                audioOutput = definition is AudioCueDefinition ? audioRoot.transform.GetChild(audioRoot.transform.childCount - 1).GetComponent<AudioCueOutputBinding>() : null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { error = exception.Message; return false; }
        }
        public void Seek(float time)
        {
            Check(); if (!IsRunning) throw new InvalidOperationException("Start or repeat a preview occurrence before scrubbing.");
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            clock.Time = time; current.Poll();
        }
        public void Advance(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || float.IsInfinity(clock.Time + seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            Seek(clock.Time + seconds);
        }
        public void Hold() { Check(); if (IsRunning) Seek(clock.Time); }
        public void Poll() { Check(); current?.Poll(); }
        public void Cancel() { Check(); current?.Cancel(); }
        public void Render()
        {
            Check();
            if (renderer && renderer.sprite)
            {
                var bounds = renderer.bounds; camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
                camera.orthographicSize = Mathf.Max(.5f, Mathf.Max(bounds.extents.y * 1.2f, bounds.extents.x / camera.aspect * 1.2f));
            }
            camera.Render();
        }
        void Check() { if (disposed) throw new ObjectDisposedException(nameof(CueSourcePreview)); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            player?.Dispose(); audio?.Dispose(); sprites?.Dispose();
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (texture) { texture.Release(); Object.DestroyImmediate(texture); }
        }
        sealed class Clock : ICueClock { public float Time; public float ReadElapsed() => Time; }
    }
}
