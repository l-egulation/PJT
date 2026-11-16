using System;
using System.IO;
using System.Linq;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Sequences.Contracts;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class SequenceSourceWindow : EditorWindow
    {
        [SerializeField] SequenceSourceDraft edits;
        [SerializeField] string sourcePath, loadedPath, fileHash, savedJson;
        [SerializeField] SequenceHost runtime;
        SequenceSourceSnapshot saved;
        SequenceSourcePreview preview;
        string status;
        VisualElement steps;
        Label state, feedback, previewState;
        TextField pathField;
        IMGUIContainer canvas;
        DoubleField previewStep;
        public SequenceSourceDocument Document => edits ? edits.document : null;
        public SequenceSourceSnapshot Saved => saved;
        public SequenceSourcePreview Preview => preview;
        [MenuItem("Tools/deviant/Experiments/Sequence Source")]
        public static void Open() => GetWindow<SequenceSourceWindow>("Sequence source");
        void OnEnable()
        {
            Undo.undoRedoPerformed += UndoChanged;
            if (!string.IsNullOrEmpty(savedJson))
                try { saved = new SequenceSourceSnapshot(loadedPath, fileHash, SequenceSourceJson.Compile(savedJson)); } catch (ArgumentException error) { status = error.Message; }
        }
        void OnDisable() { Undo.undoRedoPerformed -= UndoChanged; preview?.Dispose(); preview = null; }
        void OnDestroy() { if (edits) DestroyImmediate(edits); }
        void UndoChanged() { RebuildSteps(); RefreshState(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(Wrapped("Versioned sequence source · supported existing steps and flat ParallelAll only. Binding IDs refer to explicit runtime services; source data never contains code, Run rules or content registration."));
            pathField = new TextField("Source JSON") { name = "sourcePath", value = sourcePath }; pathField.RegisterValueChangedCallback(e => sourcePath = e.newValue); scroll.Add(pathField);
            var actions = Row(); scroll.Add(actions);
            actions.Add(Button("browse", "Choose source", () => { string path = EditorUtility.OpenFilePanel("Sequence source", "", "json"); if (!string.IsNullOrEmpty(path)) { sourcePath = path; pathField.SetValueWithoutNotify(path); } }));
            actions.Add(Button("reload", "Reload", () => Run(Reload))); actions.Add(Button("save", "Save source atomically", () => Run(SaveSource))); actions.Add(Button("export", "Export saved JSON TextAsset", () => Run(Export)));
            var host = new ObjectField("Explicit runtime sequence host") { name = "runtime", objectType = typeof(SequenceHost), allowSceneObjects = true, value = runtime };
            host.RegisterValueChangedCallback(e => { runtime = e.newValue as SequenceHost; RefreshState(); }); scroll.Add(host);
            scroll.Add(Button("applyNext", "Apply candidate to next runtime execution", () => Run(ApplyRuntime)));
            state = Wrapped(""); state.name = "state"; scroll.Add(state); feedback = Wrapped(""); feedback.name = "status"; scroll.Add(feedback);
            steps = new VisualElement { name = "steps" }; scroll.Add(steps);
            scroll.Add(Wrapped("Private real-binding preview · explicit fixture bindings only; manual presentation clock, no simulation or Run commands."));
            var previewActions = Row(); scroll.Add(previewActions);
            previewActions.Add(Button("startPreview", "Start technical preview", () => Run(StartPreview))); previewActions.Add(Button("applyPreview", "Apply candidate to next preview", () => Run(ApplyPreview)));
            previewActions.Add(Button("nextPreview", "Start next preview", () => Run(() => {
                if (preview == null) throw new InvalidOperationException("No preview.");
                if (!preview.TryStart(out var error)) throw new InvalidOperationException(error);
            })));
            previewActions.Add(Button("closePreview", "Close preview", () => { preview?.Dispose(); preview = null; RefreshState(); }));
            previewStep = new DoubleField("Advance presentation seconds") { name = "previewStep", value = .1 }; scroll.Add(previewStep);
            var clockActions = Row(); scroll.Add(clockActions);
            clockActions.Add(Button("stepPreview", "Advance preview clock", () => Run(() => preview.Advance(previewStep.value))));
            clockActions.Add(Button("choosePreview", "Choose technical dialogue option", () => Run(() => preview.SelectDialogue(0))));
            clockActions.Add(Button("advanceDialogue", "Advance dialogue", () => Run(() => preview.AdvanceDialogue())));
            clockActions.Add(Button("cancelPreview", "Cancel", () => Run(() => preview.Cancel()))); clockActions.Add(Button("skipPreview", "Declared skip", () => Run(() => preview.Skip())));
            previewState = Wrapped(""); previewState.name = "previewState"; scroll.Add(previewState);
            canvas = new IMGUIContainer(DrawPreview) { name = "previewCanvas" }; canvas.style.height = 360; canvas.style.flexShrink = 0; scroll.Add(canvas);
            RebuildSteps(); RefreshState();
        }
        static VisualElement Row() { var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        static Label Wrapped(string text) { var label = new Label(text); label.style.whiteSpace = WhiteSpace.Normal; return label; }
        static Button Button(string name, string text, Action action) => new Button(action) { name = name, text = text };
        void Run(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is InvalidOperationException || error is UnauthorizedAccessException) { status = error.Message; }
            RefreshState();
        }
        public void LoadFile(string path) { sourcePath = path; Reload(); }
        public void Reload()
        {
            var next = SequenceSource.Load(sourcePath);
            if (!edits) { edits = CreateInstance<SequenceSourceDraft>(); edits.hideFlags = HideFlags.HideAndDontSave; }
            Undo.RecordObject(edits, "Reload sequence source"); edits.document = next.Compiled.ToDocument(); Remember(next);
            pathField?.SetValueWithoutNotify(sourcePath); status = "Loaded the complete source. Runtime and preview are unchanged."; RebuildSteps(); RefreshState();
        }
        void Remember(SequenceSourceSnapshot next) { saved = next; loadedPath = next.Path; fileHash = next.FileHash; savedJson = JsonUtility.ToJson(next.Compiled.ToDocument()); }
        public void SaveSource() { Remember(SequenceSource.Save(saved, Document)); status = "Source saved atomically. Runtime and preview are unchanged."; RefreshState(); }
        void Export()
        {
            if (saved == null) throw new InvalidOperationException("Load and save an explicit source first.");
            string path = EditorUtility.SaveFilePanel("Generated sequence JSON", Application.dataPath, Path.GetFileName(saved.Path), "json"); if (string.IsNullOrEmpty(path)) return;
            SequenceSource.Export(saved, path); string assets = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(path).StartsWith(assets, StringComparison.OrdinalIgnoreCase)) AssetDatabase.ImportAsset("Assets/" + Path.GetFullPath(path).Substring(assets.Length).Replace('\\', '/'));
            status = "Exported saved DTO unchanged; runtime uses the same C# compiler.";
        }
        public CompiledSequenceSource CompileCandidate() => SequenceSourceCompiler.Compile(Document);
        public void BindRuntime(SequenceHost host) { runtime = host; rootVisualElement.Q<ObjectField>("runtime")?.SetValueWithoutNotify(runtime); RefreshState(); }
        public void ApplyRuntime()
        {
            var candidate = CompileCandidate();
            if (!EditorApplication.isPlaying || !runtime || runtime.SourceBinding == null) throw new InvalidOperationException("Select an explicitly source-configured runtime host in Play mode.");
            if (!runtime.TryApplySource(candidate, out var error)) throw new InvalidOperationException(error);
            status = "Next execution candidate applied. The running execution remains pinned.";
        }
        public void StartPreview()
        {
            var next = new SequenceSourcePreview(CompileCandidate());
            try { if (!next.TryStart(out var error)) throw new InvalidOperationException(error); } catch { next.Dispose(); throw; }
            preview?.Dispose(); preview = next; status = "Preview uses real chapter/presentation/dialogue adapters in its private scene; receipt-only fixture domain services.";
        }
        public void ApplyPreview()
        {
            if (preview == null) throw new InvalidOperationException("Start the preview first.");
            if (!preview.Host.TryApplySource(CompileCandidate(), out var error)) throw new InvalidOperationException(error);
            status = "Candidate accepted for the next preview execution; current start version retained.";
        }
        void Edit(Action change, bool rebuild = false)
        {
            if (!edits) return; Undo.IncrementCurrentGroup(); Undo.RecordObject(edits, "Edit sequence source"); change(); EditorUtility.SetDirty(edits); status = null;
            if (rebuild) RebuildSteps(); RefreshState();
        }
        void String(VisualElement parent, string name, string label, string value, Action<string> change)
        {
            var field = new TextField(label) { name = name, value = value ?? "" }; field.RegisterValueChangedCallback(e => Edit(() => change(e.newValue))); parent.Add(field);
        }
        void Number(VisualElement parent, string name, string label, double value, Action<double> change)
        {
            var field = new DoubleField(label) { name = name, value = value }; field.RegisterValueChangedCallback(e => Edit(() => change(e.newValue))); parent.Add(field);
        }
        void RebuildSteps()
        {
            if (steps == null) return; steps.Clear(); if (Document == null) { steps.Add(Wrapped("Choose an existing sequence source. No product sequence is registered by this tool.")); return; }
            String(steps, "sequenceId", "Stable sequence ID", Document.id, value => Document.id = value);
            String(steps, "anchors", "Required anchor IDs (one per line)", string.Join("\n", Document.requiredAnchors ?? Array.Empty<string>()), value => Document.requiredAnchors = value.Length == 0 ? Array.Empty<string>() : value.Split('\n'));
            steps.Q<TextField>("anchors").multiline = true;
            var skip = new Toggle("Explicit skip contract") { name = "hasSkip", value = Document.hasSkip };
            skip.RegisterValueChangedCallback(e => Edit(() => { Document.hasSkip = e.newValue; Document.skip = e.newValue ? new SequenceSourceSkipDocument() : null; }, true)); steps.Add(skip);
            if (Document.hasSkip && Document.skip != null)
            { String(steps, "skipEndpoint", "Skip endpoint ID", Document.skip.endpointId, value => Document.skip.endpointId = value); Number(steps, "skipTimeout", "Skip timeout seconds", Document.skip.timeout, value => Document.skip.timeout = value); }
            var sourceSteps = Document.steps ?? Array.Empty<SequenceSourceStepDocument>();
            for (int i = 0; i < sourceSteps.Length; i++)
            {
                int index = i; AddStep(steps, sourceSteps[i], "step-" + i, () => Edit(() => Document.steps = Document.steps.Where((item, n) => n != index).ToArray(), true),
                    i == 0 ? null : () => Edit(() => { var before = Document.steps[index - 1]; Document.steps[index - 1] = Document.steps[index]; Document.steps[index] = before; }, true));
            }
            steps.Add(Button("addStep", "Add explicit step", () => Edit(() => Document.steps = (Document.steps ?? Array.Empty<SequenceSourceStepDocument>()).Concat(new[] { new SequenceSourceStepDocument() }).ToArray(), true)));
        }
        void AddStep(VisualElement parent, SequenceSourceFieldsDocument step, string path, Action remove, Action move, bool child = false)
        {
            var group = new Foldout { name = path, text = step?.id ?? "<new step>", value = true }; parent.Add(group);
            if (step == null) { group.Add(Button(path + "-remove", "Remove null step", remove)); return; }
            var topLevel = step as SequenceSourceStepDocument;
            String(group, path + "-id", "Stable step ID", step.id, value => step.id = value);
            var kinds = Enum.GetNames(typeof(SequenceStepKind)).Where(kind => !child || kind != "ParallelAll").ToList(); if (!kinds.Contains(step.kind ?? "")) kinds.Insert(0, step.kind ?? "");
            var type = new DropdownField("Kind", kinds, step.kind ?? "") { name = path + "-kind" };
            type.RegisterValueChangedCallback(e => Edit(() => { step.kind = e.newValue; step.bindingId = null; step.duration = step.timeout = 0; if (topLevel != null) topLevel.children = null; }, true)); group.Add(type);
            bool parallel = step.kind == "ParallelAll", wait = step.kind == "Wait", camera = step.kind == "Camera";
            if (!parallel && !wait) String(group, path + "-binding", "Explicit binding ID", step.bindingId, value => step.bindingId = value);
            if (wait || camera) Number(group, path + "-duration", "Duration seconds", step.duration, value => step.duration = value);
            else if (!parallel) Number(group, path + "-timeout", "Timeout seconds", step.timeout, value => step.timeout = value);
            if (parallel && topLevel != null)
            {
                var children = topLevel.children ?? Array.Empty<SequenceSourceLeafDocument>();
                for (int i = 0; i < children.Length; i++)
                {
                    int index = i; AddStep(group, children[i], path + "-child-" + i, () => Edit(() => topLevel.children = topLevel.children.Where((item, n) => n != index).ToArray(), true),
                        i == 0 ? null : () => Edit(() => { var before = topLevel.children[index - 1]; topLevel.children[index - 1] = topLevel.children[index]; topLevel.children[index] = before; }, true), true);
                }
                group.Add(Button(path + "-addChild", "Add explicit leaf", () => Edit(() => topLevel.children = (topLevel.children ?? Array.Empty<SequenceSourceLeafDocument>()).Concat(new[] { new SequenceSourceLeafDocument() }).ToArray(), true)));
            }
            var actions = Row(); group.Add(actions); actions.Add(Button(path + "-remove", "Remove step", remove)); if (move != null) actions.Add(Button(path + "-up", "Move up", move));
        }
        void DrawPreview()
        {
            var rect = new Rect(0, 0, canvas.contentRect.width, canvas.contentRect.height);
            if (preview == null) { GUI.Label(rect, "Start a private preview with explicitly supported technical bindings."); return; }
            var stageRect = new Rect(0, 0, rect.width, rect.height * .4f);
            var dialogueRect = new Rect(0, stageRect.height, rect.width, rect.height - stageRect.height);
            GUI.DrawTexture(stageRect, preview.StageTexture, ScaleMode.ScaleToFit, false);
            GUI.DrawTexture(dialogueRect, preview.Texture, ScaleMode.ScaleToFit, false);
        }
        void RefreshState()
        {
            if (state == null) return; CompiledSequenceSource candidate = null; string error = null;
            try { candidate = CompileCandidate(); } catch (ArgumentException exception) { error = exception.Message; }
            bool valid = candidate != null; state.text = valid ? (saved != null && candidate.ContentHash == saved.Compiled.ContentHash ? "Saved source" : "Unsaved candidate") + " · " + candidate.ContentHash.Substring(0, 12) : "Invalid whole candidate: " + error;
            feedback.text = status ?? "";
            rootVisualElement.Q<Button>("save")?.SetEnabled(valid && saved != null); rootVisualElement.Q<Button>("export")?.SetEnabled(saved != null);
            rootVisualElement.Q<Button>("applyNext")?.SetEnabled(valid && EditorApplication.isPlaying && runtime && runtime.SourceBinding != null);
            rootVisualElement.Q<Button>("startPreview")?.SetEnabled(valid); rootVisualElement.Q<Button>("applyPreview")?.SetEnabled(valid && preview != null);
            foreach (string action in new[] { "stepPreview", "choosePreview", "advanceDialogue", "cancelPreview", "skipPreview" }) rootVisualElement.Q<Button>(action)?.SetEnabled(preview != null);
            rootVisualElement.Q<Button>("nextPreview")?.SetEnabled(preview != null && preview.Host.Current != null && preview.Host.Current.Read().Status != SequenceStatus.Running);
            var snapshot = preview?.Host.Current?.Read(); previewState.text = snapshot == null ? "No preview execution." : snapshot.Status + " · pinned " + snapshot.DefinitionVersion.Substring(0, 12) + " · step " + snapshot.StepId + " · t=" + preview.Time + "\n" + snapshot.Error + "\nFixture commands " + preview.CommandCount + " · skip endpoints " + preview.SkipCount;
            canvas.MarkDirtyRepaint();
        }
    }
}
