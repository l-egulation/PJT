using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative.Contracts;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class DialogueSourceWindow : EditorWindow
    {
        [SerializeField] DialogueSourceDraft edits;
        [SerializeField] string sourcePath, referencePath, npcId;
        [SerializeField] NpcDialogueHost runtime;
        [SerializeField] string loadedSourcePath, loadedReferencePath, fileHash, referenceFileHash, savedSourceJson, savedReferenceJson;
        DialogueSourceSnapshot saved;
        DialogueSourcePreview preview;
        string status;
        TextField sourceField, catalogField;
        Label state, feedback, previewState;
        VisualElement nodes, conditionFields, previewActions;
        IMGUIContainer previewCanvas;
        public DialogueSourceDocument Document => edits ? edits.document : null;
        public DialogueSourceSnapshot Saved => saved;
        public DialogueSourcePreview Preview => preview;
        public string Status => status;
        [MenuItem("Tools/deviant/Experiments/Dialogue Source")]
        public static void Open() => GetWindow<DialogueSourceWindow>("Dialogue source");
        void OnEnable()
        {
            Undo.undoRedoPerformed += UndoChanged; EditorApplication.playModeStateChanged += PlayModeChanged;
            if (!string.IsNullOrEmpty(savedSourceJson) && !string.IsNullOrEmpty(savedReferenceJson))
            {
                try { saved = new DialogueSourceSnapshot(loadedSourcePath, fileHash, loadedReferencePath, referenceFileHash, DialogueSourceJson.Compile(savedSourceJson, savedReferenceJson)); }
                catch (ArgumentException error) { status = error.Message; }
            }
        }
        void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoChanged; EditorApplication.playModeStateChanged -= PlayModeChanged;
            preview?.Dispose(); preview = null;
        }
        void OnDestroy() { if (edits) DestroyImmediate(edits); }
        void PlayModeChanged(PlayModeStateChange change) { preview?.Dispose(); preview = null; RefreshState(); }
        void UndoChanged() { RebuildNodes(); RefreshState(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(new Label("Versioned dialogue source · explicit technical/content files only"));
            scroll.Add(Wrapped("No NPC registration, story facts, rewards or Run policy are created. Source Apply, preview, Save and generated export are separate actions."));
            sourceField = new TextField("Source JSON") { name = "sourcePath" }; sourceField.SetValueWithoutNotify(sourcePath); sourceField.RegisterValueChangedCallback(e => sourcePath = e.newValue); scroll.Add(sourceField);
            catalogField = new TextField("Reference/text catalog JSON") { name = "catalogPath" }; catalogField.SetValueWithoutNotify(referencePath); catalogField.RegisterValueChangedCallback(e => referencePath = e.newValue); scroll.Add(catalogField);
            var fileActions = Row(); scroll.Add(fileActions);
            fileActions.Add(ActionButton("browseSource", "Choose source", () => Choose(false))); fileActions.Add(ActionButton("browseCatalog", "Choose catalog", () => Choose(true)));
            fileActions.Add(ActionButton("reload", "Reload", () => Run(Reload))); fileActions.Add(ActionButton("save", "Save source atomically", () => Run(SaveSource)));
            fileActions.Add(ActionButton("export", "Export saved JSON TextAsset", () => Run(Export)));
            var target = new ObjectField("Explicit runtime NPC host") { name = "runtime", objectType = typeof(NpcDialogueHost), allowSceneObjects = true, value = runtime };
            target.RegisterValueChangedCallback(e => { runtime = e.newValue as NpcDialogueHost; RefreshState(); }); scroll.Add(target);
            var npc = new TextField("Configured source NPC ID") { name = "npcId" }; npc.SetValueWithoutNotify(npcId); npc.RegisterValueChangedCallback(e => { npcId = e.newValue; RefreshState(); }); scroll.Add(npc);
            scroll.Add(ActionButton("applyNext", "Apply candidate to next runtime session", () => Run(ApplyRuntime)));
            state = Wrapped(""); state.name = "state"; scroll.Add(state); feedback = Wrapped(status ?? ""); feedback.name = "status"; scroll.Add(feedback);
            nodes = new VisualElement { name = "nodes" }; scroll.Add(nodes);
            scroll.Add(new Label("Isolated real uGUI preview · fixture command bindings only"));
            var previewButtons = Row(); scroll.Add(previewButtons);
            previewButtons.Add(ActionButton("startPreview", "Start preview from candidate", () => Run(StartPreview)));
            previewButtons.Add(ActionButton("applyPreview", "Apply candidate to next preview session", () => Run(ApplyPreview)));
            previewButtons.Add(ActionButton("nextPreview", "Start next preview session", () => Run(StartNextPreview)));
            previewButtons.Add(ActionButton("closePreview", "Close preview", () => { preview?.Dispose(); preview = null; RefreshState(); }));
            previewState = Wrapped(""); previewState.name = "previewState"; scroll.Add(previewState);
            conditionFields = new VisualElement { name = "previewConditions" }; scroll.Add(conditionFields);
            previewCanvas = new IMGUIContainer(DrawPreview) { name = "previewCanvas" }; previewCanvas.style.height = 340; previewCanvas.style.flexShrink = 0; scroll.Add(previewCanvas);
            previewActions = Row(); previewActions.name = "previewActions"; scroll.Add(previewActions);
            RebuildNodes(); RefreshConditions(); RefreshState();
        }
        static Label Wrapped(string text) { var label = new Label(text); label.style.whiteSpace = WhiteSpace.Normal; return label; }
        static VisualElement Row() { var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        static Button ActionButton(string name, string text, Action action) => new Button(action) { name = name, text = text };
        void Run(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) { status = e.Message; }
            RefreshState();
        }
        void Choose(bool catalog)
        {
            string path = EditorUtility.OpenFilePanel(catalog ? "Dialogue reference catalog" : "Dialogue source", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            if (catalog) { referencePath = path; catalogField.SetValueWithoutNotify(path); }
            else { sourcePath = path; sourceField.SetValueWithoutNotify(path); }
        }
        public void LoadFiles(string source, string references) { sourcePath = source; referencePath = references; Reload(); }
        public void Reload()
        {
            var candidate = DialogueSource.Load(sourcePath, referencePath);
            if (!edits) { edits = CreateInstance<DialogueSourceDraft>(); edits.hideFlags = HideFlags.HideAndDontSave; }
            Undo.RecordObject(edits, "Reload dialogue source"); edits.document = candidate.Compiled.ToDocument(); RememberSaved(candidate);
            sourceField?.SetValueWithoutNotify(sourcePath); catalogField?.SetValueWithoutNotify(referencePath);
            status = "Loaded and compiled the whole source. Running sessions and preview are unchanged."; RebuildNodes(); RefreshState();
        }
        public void SaveSource()
        {
            RememberSaved(DialogueSource.Save(saved, Document)); status = "Source saved atomically. Runtime and running preview remain unchanged."; RefreshState();
        }
        void RememberSaved(DialogueSourceSnapshot snapshot)
        {
            saved = snapshot; loadedSourcePath = saved.Path; loadedReferencePath = saved.ReferencePath;
            fileHash = saved.FileHash; referenceFileHash = saved.ReferenceFileHash;
            savedSourceJson = JsonUtility.ToJson(saved.Compiled.ToDocument()); savedReferenceJson = JsonUtility.ToJson(saved.Compiled.References.ToDocument());
        }
        void Export()
        {
            if (saved == null) throw new InvalidOperationException("Load and save an explicit source first.");
            string path = EditorUtility.SaveFilePanel("Generated dialogue JSON TextAsset", Application.dataPath, Path.GetFileName(sourcePath), "json");
            if (string.IsNullOrEmpty(path)) return;
            DialogueSource.Export(saved, path);
            string assets = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(path).StartsWith(assets, StringComparison.OrdinalIgnoreCase)) AssetDatabase.ImportAsset("Assets/" + Path.GetFullPath(path).Substring(assets.Length).Replace('\\', '/'));
            status = "Exported the saved source DTO unchanged; runtime uses the same C# compiler. Bind the explicit reference catalog TextAsset alongside it.";
        }
        public CompiledDialogueSource CompileCandidate()
        {
            if (saved == null) throw new InvalidOperationException("Load a source and its explicit typed reference catalog first.");
            return DialogueSourceCompiler.Compile(Document, saved.Compiled.References);
        }
        public void BindRuntime(NpcDialogueHost host, string configuredNpcId)
        {
            runtime = host; npcId = configuredNpcId;
            rootVisualElement.Q<ObjectField>("runtime")?.SetValueWithoutNotify(runtime);
            rootVisualElement.Q<TextField>("npcId")?.SetValueWithoutNotify(npcId); RefreshState();
        }
        public void ApplyRuntime()
        {
            var candidate = CompileCandidate();
            if (!EditorApplication.isPlaying || !runtime || runtime.SourceBinding == null || runtime.SourceBinding.NpcId != npcId)
                throw new InvalidOperationException("Select an active, explicitly source-configured NPC host and its matching NPC ID in Play mode.");
            if (!runtime.TryApplySource(candidate, out var error)) throw new InvalidOperationException(error);
            status = "Candidate applied to future sessions. The active session retains its start definition and text catalog.";
        }
        public void StartPreview()
        {
            var candidate = CompileCandidate(); var next = new DialogueSourcePreview(candidate);
            try { if (!next.TryStart(out var error)) throw new InvalidOperationException(error); }
            catch { next.Dispose(); throw; }
            preview?.Dispose(); preview = next; status = "Preview started in its private scene; fixture commands only, no production services or Run mutation."; RefreshConditions(); RefreshState();
        }
        public void ApplyPreview()
        {
            if (preview == null) throw new InvalidOperationException("Start the preview first.");
            if (!preview.TryApply(CompileCandidate(), out var error)) throw new InvalidOperationException(error);
            status = "Preview candidate accepted for its next session; the active start version is pinned."; RefreshState();
        }
        public void StartNextPreview()
        {
            if (preview == null) throw new InvalidOperationException("Start the preview first.");
            if (!preview.TryStart(out var error)) throw new InvalidOperationException(error);
            status = "Started a new preview session from its accepted candidate."; RefreshState();
        }
        void Edit(Action<DialogueSourceDocument> change, bool rebuild = false)
        {
            if (!edits || Document == null) return;
            Undo.IncrementCurrentGroup(); Undo.RecordObject(edits, "Edit dialogue source"); change(Document); EditorUtility.SetDirty(edits); status = null;
            if (rebuild) RebuildNodes(); RefreshState();
        }
        TextField StringField(VisualElement parent, string name, string label, string value, Action<string> change, bool optional = false)
        {
            var field = new TextField(label) { name = name }; field.SetValueWithoutNotify(value ?? "");
            field.RegisterValueChangedCallback(e => Edit(_ => change(optional && e.newValue == "" ? null : e.newValue))); parent.Add(field); return field;
        }
        void ReferenceField(VisualElement parent, string name, string label, string value, IEnumerable<string> keys, Action<string> change, bool optional = false)
        {
            var options = keys.ToList(); if (optional) options.Insert(0, "");
            string current = value ?? ""; if (!options.Contains(current)) options.Insert(0, current);
            var field = new DropdownField(label, options, current) { name = name };
            field.RegisterValueChangedCallback(e => Edit(_ => change(optional && e.newValue == "" ? null : e.newValue))); parent.Add(field);
        }
        IEnumerable<string> TextKeys(DialogueTextKind kind) => saved.Compiled.References.Texts.Where(text => text.Kind == kind).Select(text => text.Key);
        void RebuildNodes()
        {
            if (nodes == null) return; nodes.Clear();
            if (Document == null || saved == null) { nodes.Add(Wrapped("Choose an existing source and typed text/reference catalog. There is no default product dialogue.")); return; }
            StringField(nodes, "dialogueId", "Stable dialogue ID", Document.id, value => Document.id = value);
            StringField(nodes, "entryId", "Entry node ID", Document.entryId, value => Document.entryId = value);
            var lines = Document.lines ?? Array.Empty<DialogueSourceLineDocument>();
            for (int i = 0; i < lines.Length; i++)
            {
                int index = i; var line = lines[i]; var group = new Foldout { text = "Node " + (line?.id ?? "<null>"), value = true, name = "node-" + index }; nodes.Add(group);
                if (line == null) { group.Add(Wrapped("Null node; reload a valid source.")); continue; }
                StringField(group, "lineId-" + i, "Stable node ID", line.id, value => line.id = value);
                ReferenceField(group, "speaker-" + i, "Speaker key", line.speakerKey, TextKeys(DialogueTextKind.Speaker), value => line.speakerKey = value);
                ReferenceField(group, "text-" + i, "Line text key", line.textKey, TextKeys(DialogueTextKind.Line), value => line.textKey = value);
                StringField(group, "next-" + i, "Advance destination (empty = end)", line.nextId, value => line.nextId = value, true);
                ReferenceField(group, "condition-" + i, "Line condition", line.conditionId, saved.Compiled.References.ConditionIds, value => line.conditionId = value, true);
                var choices = line.choices ?? Array.Empty<DialogueSourceChoiceDocument>();
                for (int c = 0; c < choices.Length; c++)
                {
                    int choiceIndex = c; var choice = choices[c]; var row = new Foldout { text = "Choice " + (choice?.id ?? "<null>"), value = true, name = "choice-" + i + "-" + c }; group.Add(row);
                    if (choice == null) { row.Add(Wrapped("Null choice; remove or reload it.")); continue; }
                    StringField(row, "choiceId-" + i + "-" + c, "Stable choice ID", choice.id, value => choice.id = value);
                    ReferenceField(row, "choiceText-" + i + "-" + c, "Choice text key", choice.textKey, TextKeys(DialogueTextKind.Choice), value => choice.textKey = value);
                    StringField(row, "choiceNext-" + i + "-" + c, "Destination node ID", choice.nextId, value => choice.nextId = value);
                    ReferenceField(row, "choiceCondition-" + i + "-" + c, "Choice condition", choice.conditionId, saved.Compiled.References.ConditionIds, value => choice.conditionId = value, true);
                    ReferenceField(row, "choiceCommand-" + i + "-" + c, "Command reference", choice.commandId, saved.Compiled.References.CommandIds, value => choice.commandId = value, true);
                    var actions = Row(); row.Add(actions);
                    actions.Add(ActionButton("removeChoice-" + i + "-" + c, "Remove choice", () => Edit(document => line.choices = line.choices.Where((choiceValue, n) => n != choiceIndex).ToArray(), true)));
                    if (c > 0) actions.Add(ActionButton("moveChoice-" + i + "-" + c, "Move up", () => Edit(_ => { var before = line.choices[choiceIndex - 1]; line.choices[choiceIndex - 1] = line.choices[choiceIndex]; line.choices[choiceIndex] = before; }, true)));
                }
                var lineActions = Row(); group.Add(lineActions);
                lineActions.Add(ActionButton("addChoice-" + i, "Add explicit choice", () => Edit(_ => {
                    line.choices = (line.choices ?? Array.Empty<DialogueSourceChoiceDocument>()).Concat(new[] { new DialogueSourceChoiceDocument { id = "", textKey = TextKeys(DialogueTextKind.Choice).FirstOrDefault(), nextId = "" } }).ToArray();
                }, true)));
                lineActions.Add(ActionButton("removeLine-" + i, "Remove node", () => Edit(d => d.lines = d.lines.Where((_, n) => n != index).ToArray(), true)));
            }
            nodes.Add(ActionButton("addLine", "Add explicit node", () => Edit(d => d.lines = (d.lines ?? Array.Empty<DialogueSourceLineDocument>()).Concat(new[] {
                new DialogueSourceLineDocument { id = "", speakerKey = TextKeys(DialogueTextKind.Speaker).FirstOrDefault(), textKey = TextKeys(DialogueTextKind.Line).FirstOrDefault(), choices = Array.Empty<DialogueSourceChoiceDocument>() }
            }).ToArray(), true)));
        }
        void RefreshConditions()
        {
            if (conditionFields == null) return; conditionFields.Clear();
            if (preview == null || saved == null) return;
            foreach (var id in saved.Compiled.References.ConditionIds)
            {
                var condition = id; var toggle = new Toggle(id) { name = "preview-condition-" + id, value = preview.GetCondition(id) };
                toggle.RegisterValueChangedCallback(e => { preview?.SetCondition(condition, e.newValue); RefreshState(); }); conditionFields.Add(toggle);
            }
        }
        void DrawPreview()
        {
            var area = new Rect(0, 0, previewCanvas.contentRect.width, previewCanvas.contentRect.height);
            if (preview == null || !preview.Texture) { GUI.Label(area, "Start an isolated uGUI preview from a valid candidate."); return; }
            float scale = Math.Min(area.width / preview.Texture.width, area.height / preview.Texture.height);
            var image = new Rect((area.width - preview.Texture.width * scale) * .5f, 0, preview.Texture.width * scale, preview.Texture.height * scale);
            GUI.DrawTexture(image, preview.Texture, ScaleMode.StretchToFill, false);
            var input = Event.current;
            if (input.type == EventType.MouseDown && input.button == 0 && image.Contains(input.mousePosition))
            {
                preview.Click(new Vector2((input.mousePosition.x - image.x) / image.width, 1 - (input.mousePosition.y - image.y) / image.height)); input.Use(); RefreshState();
            }
        }
        void RefreshState()
        {
            if (state == null) return;
            CompiledDialogueSource candidate = null; string error = null;
            try { candidate = CompileCandidate(); } catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { error = e.Message; }
            bool valid = candidate != null;
            state.text = valid ? (candidate.ContentHash == saved.Compiled.ContentHash ? "Saved source" : "Unsaved candidate") + " · graph " + candidate.Definition.ContentHash.Substring(0, 12) + " · catalog " + candidate.References.ContentHash.Substring(0, 12)
                : "Invalid whole candidate; Save/Apply/preview start disabled: " + error;
            feedback.text = status ?? "";
            rootVisualElement.Q<Button>("save")?.SetEnabled(valid && saved != null); rootVisualElement.Q<Button>("export")?.SetEnabled(saved != null);
            rootVisualElement.Q<Button>("applyNext")?.SetEnabled(valid && EditorApplication.isPlaying && runtime && runtime.SourceBinding != null && runtime.SourceBinding.NpcId == npcId);
            rootVisualElement.Q<Button>("startPreview")?.SetEnabled(valid); rootVisualElement.Q<Button>("applyPreview")?.SetEnabled(valid && preview != null);
            rootVisualElement.Q<Button>("nextPreview")?.SetEnabled(preview != null && preview.Current != null && preview.Current.Read().Status != DialogueStatus.Active);
            previewActions.Clear();
            if (preview == null) { previewState.text = "No preview session. Existing runtime/world state is untouched."; conditionFields.Clear(); }
            else
            {
                var snapshot = preview.Current?.Read(); var display = preview.View.Displayed;
                previewState.text = snapshot == null ? "No preview session." : snapshot.Status + " · pinned graph " + snapshot.DefinitionVersion.Substring(0, 12) + " · line " + snapshot.Token.LineId + " · revision " + snapshot.Token.Revision + "\nFixture command receipts: " + string.Join(", ", preview.CommandLog);
                if (display != null)
                {
                    if (display.CanAdvance) previewActions.Add(ActionButton("previewAdvance", "Advance", () => { preview.Advance(); RefreshState(); }));
                    for (int i = 0; i < display.Choices.Count; i++)
                    {
                        int slot = i; var button = ActionButton("previewChoice-" + i, display.Choices[i].Text, () => { preview.Select(slot); RefreshState(); }); button.SetEnabled(display.Choices[i].IsAvailable); previewActions.Add(button);
                    }
                    if (display.AllowCancel) previewActions.Add(ActionButton("previewCancel", "Cancel", () => { preview.Cancel(); RefreshState(); }));
                }
            }
            previewCanvas.MarkDirtyRepaint();
        }
    }
}
