using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring
{
    public sealed class CueSourceWindow : EditorWindow
    {
        [SerializeField] CueSourceDraft edits;
        [SerializeField] string sourcePath, loadedPath, fileHash, savedJson, selectedCue;
        CueSourceSnapshot saved;
        CueSourcePreview preview;
        CueSourceBinding runtime;
        readonly CueSourceEditorAssets assets = new CueSourceEditorAssets();
        VisualElement entries;
        Label state, feedback, previewState, mediaState, runtimeState;
        TextField pathField;
        DropdownField selection;
        FloatField advanceBy;
        Slider scrub;
        IMGUIContainer canvas;
        string status;
        bool playing;
        double lastUpdate;
        public CueSourceDocument Document => edits ? edits.document : null;
        public CueSourceSnapshot Saved => saved;
        public CueSourcePreview Preview => preview;
        public string SelectedCueId => selectedCue;
        [MenuItem("Tools/deviant/Experiments/Cue Source")]
        public static void Open() => GetWindow<CueSourceWindow>("Cue source");
        void OnEnable()
        {
            Undo.undoRedoPerformed += UndoChanged; EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayModeChanged;
            if (!string.IsNullOrEmpty(savedJson))
                try { saved = new CueSourceSnapshot(loadedPath, fileHash, CueSourceJson.Compile(savedJson)); } catch (ArgumentException error) { status = error.Message; }
        }
        void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoChanged; EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayModeChanged;
            ClosePreview(); runtime = null;
            if (state != null) { rootVisualElement.Clear(); state = feedback = previewState = mediaState = runtimeState = null; entries = null; }
        }
        void OnDestroy() { if (edits) DestroyImmediate(edits); }
        void PlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.ExitingPlayMode) { ClosePreview(); runtime = null; }
            RefreshState();
        }
        void UndoChanged() { RebuildEntries(); RefreshState(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(Wrapped("Versioned Sprite/Audio Cue source · existing semantic definitions and real CuePlayer only. No product sound/art is created or registered; asset references are explicit GUID + local file ID."));
            pathField = new TextField("Canonical source JSON") { name = "sourcePath", value = sourcePath };
            pathField.RegisterValueChangedCallback(e => sourcePath = e.newValue); scroll.Add(pathField);
            var files = Row(); scroll.Add(files);
            files.Add(Button("browse", "Choose source", () => { string path = EditorUtility.OpenFilePanel("Cue source", "", "json"); if (!string.IsNullOrEmpty(path)) { sourcePath = path; pathField.SetValueWithoutNotify(path); } }));
            files.Add(Button("reload", "Reload", () => Run(Reload))); files.Add(Button("newSource", "New empty source", () => Run(NewSource)));
            files.Add(Button("save", "Save source atomically", () => Run(SaveSource))); files.Add(Button("export", "Export saved JSON TextAsset", () => Run(Export)));
            state = Wrapped(""); state.name = "state"; scroll.Add(state); feedback = Wrapped(""); feedback.name = "status"; scroll.Add(feedback);
            runtimeState = Wrapped(""); runtimeState.name = "runtimeState"; scroll.Add(runtimeState);
            scroll.Add(Button("publishRuntime", "Publish candidate to explicitly bound catalog", () => Run(PublishRuntime)));
            entries = new VisualElement { name = "entries" }; scroll.Add(entries);
            mediaState = Wrapped(""); mediaState.name = "mediaState"; scroll.Add(mediaState);
            scroll.Add(Wrapped("Private preview · Sprite renders in its own scene. Audio uses an actual private AudioSource in Play mode. Scrub changes only this preview clock; equal/backward samples hold/seek, positive advancement resumes. No mixer or listening-quality policy is implied."));
            selection = new DropdownField("Cue to preview") { name = "previewCue" };
            selection.RegisterValueChangedCallback(e => { selectedCue = e.newValue; RefreshState(); }); scroll.Add(selection);
            var previewActions = Row(); scroll.Add(previewActions);
            previewActions.Add(Button("startPreview", "Start private preview", () => Run(StartPreview)));
            previewActions.Add(Button("publishPreview", "Publish candidate to next preview", () => Run(PublishPreview)));
            previewActions.Add(Button("bindPreviewCatalog", "Bind private catalog as explicit publish target", () => Run(() => { if (preview == null) throw new InvalidOperationException("Start a preview first."); BindRuntime(preview.Binding); })));
            previewActions.Add(Button("repeatPreview", "Repeat selected Cue", () => Run(RepeatPreview)));
            previewActions.Add(Button("cancelPreview", "Cancel occurrence", () => Run(() => { playing = false; preview.Cancel(); })));
            previewActions.Add(Button("closePreview", "Close private outputs", () => { ClosePreview(); RefreshState(); }));
            var timeActions = Row(); scroll.Add(timeActions);
            timeActions.Add(Button("playPreview", "Play", () => Run(() => { if (!preview.IsRunning) throw new InvalidOperationException("Repeat an occurrence before playing."); playing = true; lastUpdate = EditorApplication.timeSinceStartup; })));
            timeActions.Add(Button("holdPreview", "Hold", () => Run(() => { playing = false; preview.Hold(); })));
            advanceBy = new FloatField("Advance presentation seconds") { name = "advanceBy", value = .1f }; scroll.Add(advanceBy);
            scroll.Add(Button("advancePreview", "Advance once", () => Run(() => { playing = false; preview.Advance(advanceBy.value); })));
            scrub = new Slider("Absolute preview seconds", 0, 1) { name = "scrub", showInputField = true };
            scrub.RegisterValueChangedCallback(e => Run(() => { playing = false; preview.Seek(e.newValue); })); scroll.Add(scrub);
            previewState = Wrapped(""); previewState.name = "previewState"; scroll.Add(previewState);
            canvas = new IMGUIContainer(DrawPreview) { name = "previewCanvas" }; canvas.style.height = 300; canvas.style.flexShrink = 0; scroll.Add(canvas);
            RebuildEntries(); RefreshState();
        }
        static Label Wrapped(string text) { var label = new Label(text); label.style.whiteSpace = WhiteSpace.Normal; return label; }
        static VisualElement Row() { var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        static Button Button(string name, string text, Action action) => new Button(action) { name = name, text = text };
        void Run(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is InvalidOperationException || error is UnauthorizedAccessException) { playing = false; status = error.Message; }
            RefreshState();
        }
        public void LoadFile(string path) { sourcePath = path; Reload(); }
        void SetDocument(CueSourceDocument document)
        {
            if (!edits) { edits = CreateInstance<CueSourceDraft>(); edits.hideFlags = HideFlags.HideAndDontSave; }
            Undo.RecordObject(edits, "Load Cue source"); edits.document = document; EditorUtility.SetDirty(edits);
        }
        public void Reload()
        {
            var next = CueSource.Load(sourcePath); SetDocument(next.Compiled.ToDocument()); Remember(next);
            pathField?.SetValueWithoutNotify(sourcePath); status = "Loaded source. Running previews/catalogs remain unchanged."; RebuildEntries(); RefreshState();
        }
        void NewSource()
        {
            SetDocument(new CueSourceDocument { schemaVersion = CueSourceDocument.CurrentSchemaVersion, id = "", cues = Array.Empty<CueSourceEntryDocument>() });
            saved = null; savedJson = loadedPath = fileHash = null; status = "Enter an explicit stable catalog ID, then save to a new canonical path. No content is registered.";
            RebuildEntries(); RefreshState();
        }
        void Remember(CueSourceSnapshot next) { saved = next; loadedPath = next.Path; fileHash = next.FileHash; savedJson = JsonUtility.ToJson(next.Compiled.ToDocument()); }
        public void SaveSource()
        {
            if (saved == null)
            {
                if (string.IsNullOrWhiteSpace(sourcePath)) throw new InvalidOperationException("Choose a new canonical .json path first.");
                if (!string.Equals(Path.GetExtension(sourcePath), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A canonical Cue source requires a .json path.");
                Remember(CueSource.Create(sourcePath, Document));
            }
            else Remember(CueSource.Save(saved, Document));
            status = "Source saved atomically. Preview and runtime publication are separate actions."; RefreshState();
        }
        void Export()
        {
            if (saved == null) throw new InvalidOperationException("Save an explicit source first.");
            string path = EditorUtility.SaveFilePanel("Generated Cue JSON", Application.dataPath, Path.GetFileName(saved.Path), "json"); if (string.IsNullOrEmpty(path)) return;
            CueSource.Export(saved, path); string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(path).StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                AssetDatabase.ImportAsset("Assets/" + Path.GetFullPath(path).Substring(assetsRoot.Length).Replace('\\', '/'));
            status = "Exported the saved DTO unchanged; runtime TextAsset compilation uses the same C# compiler.";
        }
        public CompiledCueSource CompileCandidate() => CueSourceCompiler.Compile(Document);
        public void BindRuntime(CueSourceBinding binding) { runtime = binding; RefreshState(); }
        public void PublishRuntime()
        {
            if (!EditorApplication.isPlaying || runtime == null) throw new InvalidOperationException("Play mode and a caller-supplied CueSourceBinding are required; the window never searches for a catalog.");
            if (!runtime.TryPublish(CompileCandidate(), out var error)) throw new InvalidOperationException(error);
            status = "Published the whole candidate to the explicit runtime catalog. Existing captured executions retain their version.";
        }
        public void StartPreview()
        {
            var candidate = CompileCandidate(); candidate.Read(selectedCue);
            var next = new CueSourcePreview(candidate, assets);
            try { if (!next.TryStart(selectedCue, out var error)) throw new InvalidOperationException(error); next.Render(); }
            catch { next.Dispose(); throw; }
            ClosePreview(); preview = next; status = "Private actual CuePlayer occurrence started; saved files and runtime catalogs are unchanged.";
        }
        public void PublishPreview()
        {
            if (preview == null) throw new InvalidOperationException("Start a private preview first.");
            if (!preview.TryPublish(CompileCandidate(), out var error)) throw new InvalidOperationException(error);
            status = "Preview catalog candidate accepted. Current occurrence remains pinned; Repeat adopts the new version.";
        }
        public void RepeatPreview()
        {
            if (preview == null) throw new InvalidOperationException("Start a private preview first.");
            playing = false; preview.Cancel();
            if (!preview.TryStart(selectedCue, out var error)) throw new InvalidOperationException(error);
            status = "Started a new occurrence from the accepted preview catalog.";
        }
        void ClosePreview()
        {
            playing = false;
            if (preview != null && ReferenceEquals(runtime, preview.Binding)) runtime = null;
            preview?.Dispose(); preview = null;
        }
        void Edit(Action change, bool rebuild = false)
        {
            if (!edits) return; Undo.IncrementCurrentGroup(); Undo.RecordObject(edits, "Edit Cue source"); change(); EditorUtility.SetDirty(edits); status = null;
            if (rebuild) RebuildEntries(); RefreshState();
        }
        void Text(VisualElement parent, string name, string label, string value, Action<string> change)
        {
            var field = new TextField(label) { name = name, value = value ?? "" }; field.RegisterValueChangedCallback(e => Edit(() => change(e.newValue))); parent.Add(field);
        }
        void Number(VisualElement parent, string name, string label, float value, Action<float> change)
        {
            var field = new FloatField(label) { name = name, value = value }; field.RegisterValueChangedCallback(e => Edit(() => change(e.newValue))); parent.Add(field);
        }
        void Integer(VisualElement parent, string name, string label, int value, Action<int> change)
        {
            var field = new IntegerField(label) { name = name, value = value }; field.RegisterValueChangedCallback(e => Edit(() => change(e.newValue))); parent.Add(field);
        }
        void Toggle(VisualElement parent, string name, string label, bool value, Action<bool> change)
        {
            var field = new Toggle(label) { name = name, value = value }; field.RegisterValueChangedCallback(e => Edit(() => change(e.newValue))); parent.Add(field);
        }
        void Asset(VisualElement parent, string name, string label, CueSourceAssetDocument reference, Type type, Action<CueSourceAssetDocument> change)
        {
            reference = reference ?? new CueSourceAssetDocument();
            Object current = null;
            try
            {
                var key = reference == null ? default : new CueSourceAssetReference(reference.guid, reference.localFileId);
                current = type == typeof(Sprite) ? (Object)assets.ReadSprite(key) : assets.ReadAudio(key);
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException) { parent.Add(Wrapped(error.Message)); }
            var field = new ObjectField(label) { name = name, objectType = type, allowSceneObjects = false, value = current };
            field.RegisterValueChangedCallback(e => Run(() => { var next = CueSourceEditorAssets.Reference(e.newValue); Edit(() => change(next), true); })); parent.Add(field);
            Text(parent, name + "-guid", "Asset GUID", reference.guid, value => { reference.guid = value; change(reference); });
            var local = new LongField("Asset local file ID") { name = name + "-localId", value = reference?.localFileId ?? 0 };
            local.RegisterValueChangedCallback(e => Edit(() => { reference.localFileId = e.newValue; change(reference); })); parent.Add(local);
        }
        void RebuildEntries()
        {
            if (entries == null) return; entries.Clear();
            if (Document == null) { entries.Add(Wrapped("Choose an existing source or create an empty document. No product media is synthesized.")); return; }
            Text(entries, "catalogId", "Stable catalog ID", Document.id, value => Document.id = value);
            var cues = Document.cues ?? Array.Empty<CueSourceEntryDocument>();
            for (int i = 0; i < cues.Length; i++)
            {
                int index = i; var cue = cues[i]; string prefix = "cue-" + i;
                var group = new Foldout { name = prefix, text = cue?.id ?? "<new Cue>", value = true }; entries.Add(group);
                if (cue != null)
                {
                    Text(group, prefix + "-id", "Stable Cue ID", cue.id, value => cue.id = value);
                    var kind = new DropdownField("Kind", new[] { "", "Sprite", "Audio" }.ToList(), cue.kind ?? "") { name = prefix + "-kind" };
                    kind.RegisterValueChangedCallback(e => Edit(() => { cue.kind = e.newValue; cue.frames = null; cue.audioAsset = null; cue.clipId = null; cue.frequency = cue.sampleCount = 0; cue.flipX = false; }, true)); group.Add(kind);
                    Toggle(group, prefix + "-loop", "Loop (Sequence steps require finite one-shots)", cue.loop, value => cue.loop = value);
                    if (cue.kind == "Sprite")
                    {
                        Toggle(group, prefix + "-flipX", "Flip Sprite", cue.flipX, value => cue.flipX = value);
                        var frames = cue.frames ?? Array.Empty<CueSourceFrameDocument>();
                        for (int f = 0; f < frames.Length; f++)
                        {
                            int frameIndex = f; var frame = frames[f]; string framePrefix = prefix + "-frame-" + f;
                            var frameGroup = new Foldout { name = framePrefix, text = frame?.id ?? "<new frame>", value = true }; group.Add(frameGroup);
                            if (frame != null)
                            {
                                Text(frameGroup, framePrefix + "-id", "Stable frame key", frame.id, value => frame.id = value);
                                Number(frameGroup, framePrefix + "-seconds", "Frame seconds", frame.seconds, value => frame.seconds = value);
                                Number(frameGroup, framePrefix + "-x", "X offset", frame.x, value => frame.x = value); Number(frameGroup, framePrefix + "-y", "Y offset", frame.y, value => frame.y = value);
                                Asset(frameGroup, framePrefix + "-asset", "Explicit Sprite asset", frame.asset, typeof(Sprite), value => frame.asset = value);
                            }
                            var frameActions = Row(); frameGroup.Add(frameActions);
                            frameActions.Add(Button(framePrefix + "-remove", "Remove frame", () => Edit(() => cue.frames = cue.frames.Where((item, n) => n != frameIndex).ToArray(), true)));
                            if (f > 0) frameActions.Add(Button(framePrefix + "-up", "Move frame up", () => Edit(() => { var before = cue.frames[frameIndex - 1]; cue.frames[frameIndex - 1] = cue.frames[frameIndex]; cue.frames[frameIndex] = before; }, true)));
                        }
                        group.Add(Button(prefix + "-addFrame", "Add explicit Sprite frame", () => Edit(() => cue.frames = (cue.frames ?? Array.Empty<CueSourceFrameDocument>()).Concat(new[] { new CueSourceFrameDocument() }).ToArray(), true)));
                    }
                    else if (cue.kind == "Audio")
                    {
                        Text(group, prefix + "-clipId", "Stable clip key", cue.clipId, value => cue.clipId = value);
                        Integer(group, prefix + "-samples", "Declared samples per channel", cue.sampleCount, value => cue.sampleCount = value);
                        Integer(group, prefix + "-frequency", "Declared frequency (Hz)", cue.frequency, value => cue.frequency = value);
                        Asset(group, prefix + "-asset", "Explicit AudioClip asset", cue.audioAsset, typeof(AudioClip), value => cue.audioAsset = value);
                    }
                }
                var actions = Row(); group.Add(actions);
                actions.Add(Button(prefix + "-remove", "Remove Cue", () => Edit(() => Document.cues = Document.cues.Where((item, n) => n != index).ToArray(), true)));
                if (i > 0) actions.Add(Button(prefix + "-up", "Move Cue up", () => Edit(() => { var before = Document.cues[index - 1]; Document.cues[index - 1] = Document.cues[index]; Document.cues[index] = before; }, true)));
            }
            entries.Add(Button("addCue", "Add explicit Cue", () => Edit(() => Document.cues = (Document.cues ?? Array.Empty<CueSourceEntryDocument>()).Concat(new[] { new CueSourceEntryDocument() }).ToArray(), true)));
        }
        void Tick()
        {
            if (preview == null) return;
            try
            {
                if (playing)
                {
                    double now = EditorApplication.timeSinceStartup; float elapsed = (float)(now - lastUpdate); lastUpdate = now;
                    preview.Advance(elapsed); if (!preview.IsRunning) playing = false;
                }
                else if (preview.IsRunning) preview.Poll();
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { playing = false; status = error.Message; if (feedback != null) feedback.text = status; }
            RefreshPreviewState(); Repaint();
        }
        void DrawPreview()
        {
            var rect = new Rect(0, 0, canvas.contentRect.width, canvas.contentRect.height);
            if (preview == null) { GUI.Label(rect, "Start a private preview with explicit available media."); return; }
            preview.Render(); GUI.DrawTexture(rect, preview.Texture, ScaleMode.ScaleToFit, false);
        }
        void RefreshState()
        {
            if (state == null) return;
            CompiledCueSource candidate = null; string error = null;
            try { candidate = CompileCandidate(); } catch (ArgumentException exception) { error = exception.Message; }
            bool valid = candidate != null;
            state.text = valid ? (saved != null && candidate.ContentHash == saved.Compiled.ContentHash ? "Saved source" : "Unsaved candidate") + " · source " + candidate.ContentHash + "\nSemantic " + candidate.DefinitionHash + "\nBindings " + candidate.BindingHash : "Invalid whole candidate: " + error;
            feedback.text = status ?? ""; runtimeState.text = runtime == null ? "No runtime catalog bound. Composition must call BindRuntime explicitly; no automatic lookup." : "Explicit runtime catalog · " + runtime.CatalogVersion + " · source " + (runtime.Source?.ContentHash ?? "<none>");
            string[] ids = Document?.cues?.Where(cue => cue != null && !string.IsNullOrWhiteSpace(cue.id)).Select(cue => cue.id).Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
            if (!ids.Contains(selectedCue)) selectedCue = ids.FirstOrDefault(); selection.choices = ids.ToList(); selection.SetValueWithoutNotify(selectedCue);
            bool mediaReady = valid;
            if (valid)
            {
                try { CueSourceBinding.Prepare(candidate, assets); mediaState.text = "All explicitly referenced media are available and AudioClip metadata matches."; }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { mediaReady = false; mediaState.text = "Media unavailable (save/export remain allowed): " + exception.Message; }
            }
            else mediaState.text = "Media resolution waits for a valid whole candidate.";
            rootVisualElement.Q<Button>("save")?.SetEnabled(valid); rootVisualElement.Q<Button>("export")?.SetEnabled(saved != null);
            rootVisualElement.Q<Button>("publishRuntime")?.SetEnabled(valid && EditorApplication.isPlaying && runtime != null);
            rootVisualElement.Q<Button>("startPreview")?.SetEnabled(mediaReady && !string.IsNullOrEmpty(selectedCue));
            rootVisualElement.Q<Button>("publishPreview")?.SetEnabled(mediaReady && preview != null);
            foreach (string name in new[] { "repeatPreview", "cancelPreview", "closePreview" }) rootVisualElement.Q<Button>(name)?.SetEnabled(preview != null);
            RefreshPreviewState();
        }
        void RefreshPreviewState()
        {
            if (previewState == null) return;
            foreach (string name in new[] { "playPreview", "holdPreview", "advancePreview" }) rootVisualElement.Q<Button>(name)?.SetEnabled(preview != null && preview.IsRunning);
            scrub.SetEnabled(preview != null && preview.IsRunning);
            if (preview?.Definition != null)
            {
                scrub.highValue = preview.Definition.Duration; scrub.SetValueWithoutNotify(preview.Time);
                var definition = preview.Definition; string sample = definition is AudioCueDefinition audio ? "sample " + audio.SampleAt(preview.Time) + " / " + audio.SampleCount + " @ " + audio.Frequency + "Hz" : "frame " + ((PresentationCueDefinition)definition).FrameAt(preview.Time);
                var actualAudio = preview.AudioOutput;
                string actual = actualAudio && !actualAudio.IsDisposed && actualAudio.Source ? "\nActual AudioSource sample=" + actualAudio.Source.timeSamples + " playing=" + actualAudio.Source.isPlaying : "";
                previewState.text = preview.Current.Status + " · " + definition.Id + " · " + (definition.Loop ? "loop" : "finite") + " · t=" + preview.Time.ToString("0.000", CultureInfo.InvariantCulture) + " / " + definition.Duration + "s · " + sample + actual +
                    "\nPinned definition " + preview.Current.DefinitionVersion + "\nPinned source " + preview.StartedSourceVersion + " · catalog " + preview.StartedCatalogVersion +
                    "\nAccepted next source " + preview.Binding.Source.ContentHash + " · outputs " + preview.LiveSpriteCount + "/" + preview.LiveAudioCount + "\n" + preview.Current.Error;
            }
            else previewState.text = "No occurrence. Scrubbing never starts a cancelled or completed Cue; Repeat starts a new occurrence.";
            canvas.MarkDirtyRepaint();
        }
    }
}
