using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class SpecialFrameWindow : EditorWindow
    {
        [SerializeField] SpecialFrameDraft draft;
        [SerializeField] string repository, fileHash, selectedId, savedHash, python = "python";
        [SerializeField] CombatArena arena;
        ActionSpecialFrameCatalog candidate;
        ActionSpecialFrameDefinition selected;
        AttackMotionTiming timing;
        Sprite[] sprites = Array.Empty<Sprite>();
        string failure, status;
        bool dirty = true;
        Label state, policy, diff, sample, sourceLabel;
        VisualElement fields;
        DropdownField selector;
        Slider time;
        Toggle left;
        IMGUIContainer body;
        public SpecialFrameCatalogDocument Draft => draft ? draft.source : null;
        public ActionSpecialFrameDefinition Candidate { get { Compile(); return failure == null ? selected : null; } }
        [MenuItem("Tools/deviant/Experiments/Special Frame Timing")]
        public static void Open() => GetWindow<SpecialFrameWindow>("Special frame timing");
        void OnEnable() { Undo.undoRedoPerformed += UndoChanged; }
        void OnDisable() { Undo.undoRedoPerformed -= UndoChanged; }
        void OnDestroy() { if (draft) DestroyImmediate(draft); }
        void UndoChanged() { dirty = true; Rebuild(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(Wrapped("Special FrameTime · finite4 + authored19 · action-local timing"));
            scroll.Add(Wrapped("Existing execution owners retain damage/resource/physics priority and independent tails. Shared clip identities are referenced, never copied. Charge/ranged/missile policies are not converted by this surface."));
            sourceLabel = Wrapped(""); sourceLabel.name = "source"; scroll.Add(sourceLabel);
            var runtime = new ObjectField("Explicit runtime") { name = "runtime", objectType = typeof(CombatArena), allowSceneObjects = true };
            runtime.SetValueWithoutNotify(arena); runtime.RegisterValueChangedCallback(value => { arena = value.newValue as CombatArena; dirty = true; Rebuild(); }); scroll.Add(runtime);
            selector = new DropdownField("Existing special") { name = "action" }; selector.RegisterValueChangedCallback(value => { if (candidate == null || selector.index < 0) return; selectedId = candidate.Actions[selector.index].Id; dirty = true; Rebuild(); }); scroll.Add(selector);
            state = Wrapped(""); state.name = "state"; scroll.Add(state); policy = Wrapped(""); policy.name = "policy"; scroll.Add(policy);
            scroll.Add(new Button(() => FileAction(Convert)) { name = "convert", text = "Explicitly project inherited timing (reject collapsed spans)" });
            scroll.Add(new Button(() => FileAction(AuthorLocal)) { name = "authorLocal", text = "Author complete positive local timing explicitly" });
            scroll.Add(new Button(() => FileAction(ReturnFixed)) { name = "fixed", text = "Return this candidate to inherited fixed timing" });
            var local = new VisualElement(); local.name = "explicitLocal";
            local.Add(Wrapped("Intentional local authoring: supply every shared step ID/duration and every policy marker. This accepts a visible body timing diff; it does not claim lossless fixed conversion. No missing duration/marker is invented."));
            var json = new TextField("Complete FrameTimeline JSON") { name = "localTimeline", multiline = true }; json.style.minHeight = 120; local.Add(json); scroll.Add(local);
            fields = new VisualElement { name = "timingFields" }; scroll.Add(fields);
            time = new Slider("Read-only action time", 0, 1) { name = "time", showInputField = true }; time.RegisterValueChangedCallback(_ => RefreshSample()); scroll.Add(time);
            left = new Toggle("Reverse facing") { name = "left" }; left.RegisterValueChangedCallback(_ => body?.MarkDirtyRepaint()); scroll.Add(left);
            body = new IMGUIContainer(DrawBody) { name = "body" }; body.style.height = 260; scroll.Add(body);
            sample = Wrapped(""); sample.name = "sample"; scroll.Add(sample); diff = Wrapped(""); diff.name = "diff"; scroll.Add(diff);
            scroll.Add(new Button(() => FileAction(() => Apply(false))) { name = "applyNext", text = "Apply this whole candidate to next start" });
            scroll.Add(new Button(() => FileAction(() => Apply(true))) { name = "applyReset", text = "Apply whole candidate and reset" });
            scroll.Add(new Button(() => FileAction(Reload)) { name = "reload", text = "Reload canonical sources (discard draft only)" });
            var executable = new TextField("Python executable") { name = "python" }; executable.SetValueWithoutNotify(python); executable.RegisterValueChangedCallback(value => python = value.newValue); scroll.Add(executable);
            scroll.Add(new Button(() => FileAction(Save)) { name = "save", text = "Save frame source and transport generated catalog" });
            var statusLabel = Wrapped(""); statusLabel.name = "status"; scroll.Add(statusLabel);
            if (string.IsNullOrEmpty(repository))
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, SpecialFrameSource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName); else status = "The canonical complete special frame source is unavailable.";
            }
            else if (draft) { dirty = true; Rebuild(); } else FileAction(Reload);
            Refresh();
        }
        static Label Wrapped(string text) { var value = new Label(text); value.style.whiteSpace = WhiteSpace.Normal; value.style.marginBottom = 6; return value; }
        public void LoadRepository(string path) { repository = path; FileAction(Reload); }
        public void Reload()
        {
            var loaded = SpecialFrameSource.Load(repository, out var hash);
            if (!draft) { draft = CreateInstance<SpecialFrameDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; }
            Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(draft, "Reload special frame sources"); draft.source = loaded.ToDocument(); EditorUtility.SetDirty(draft);
            fileHash = hash; savedHash = loaded.ContentHash; candidate = loaded;
            if (string.IsNullOrEmpty(selectedId) || !loaded.Actions.Any(value => value.Id == selectedId)) selectedId = loaded.Actions[0].Id;
            status = "Loaded complete original numeric/media/mode sources. Current execution is unchanged."; dirty = true; Rebuild();
        }
        void Compile()
        {
            if (!dirty) return; dirty = false; failure = null; timing = null; sprites = Array.Empty<Sprite>();
            try
            {
                candidate = SpecialFrameSource.Compile(repository, Draft);
                selected = candidate.Actions.SingleOrDefault(value => value.Id == selectedId) ?? candidate.Actions[0]; selectedId = selected.Id;
                timing = selected.CompileMotion(); sprites = timing.Frames.Select(ResolveSprite).ToArray();
            }
            catch (Exception error) when (Expected(error)) { failure = error.Message; }
        }
        Sprite ResolveSprite(string frame)
        {
            if (!arena) return Resources.Load<Sprite>(frame);
            var media = arena.characterAnimations;
            if (media != null && media.TryMotionSprite(frame, out var sprite)) return sprite;
            if (media?.sets != null) foreach (var set in media.sets) foreach (var clip in set?.clips ?? Array.Empty<CharacterClip>())
            {
                int index = clip?.frames == null ? -1 : Array.IndexOf(clip.frames, frame);
                if (index >= 0 && clip.sprites != null && index < clip.sprites.Length) return clip.sprites[index];
            }
            return null;
        }
        void Replace(SpecialFrameSourceDocument record)
        {
            int index = Array.FindIndex(Draft.actions, value => value.actionId == record.actionId);
            if (index < 0) throw new ArgumentException("Unsupported special frame source identity."); Draft.actions[index] = record;
        }
        void Edit(Action change, bool rebuild = false)
        {
            if (!draft) return;
            Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(draft, "Edit complete special frame timing"); change(); EditorUtility.SetDirty(draft); dirty = true; status = "";
            if (rebuild) Rebuild(); else Refresh();
        }
        public void Convert()
        {
            var current = Candidate ?? throw new ArgumentException(failure);
            var next = current.Family == SpecialFrameFamily.FiniteTracks
                ? ActionSpecialFrameCompiler.ProjectFinite(current.NumericFinite, current.FormId, current.Media)
                : ActionSpecialFrameCompiler.ProjectAuthored(current.NumericAuthored, current.Media);
            Edit(() => Replace(next), true); status = "Explicit local projection compiled; numeric action timing is inactive for this candidate only."; Refresh();
        }
        public void AuthorLocal()
        {
            var current = Candidate ?? throw new ArgumentException(failure);
            string text = rootVisualElement.Q<TextField>("localTimeline").value;
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Supply the whole intentional local timeline. Missing frame durations or policy channels are not generated.");
            var document = JsonUtility.FromJson<FrameTimelineDocument>(text);
            if (document?.frames == null || document.markers == null || document.frames.Any(frame => frame == null) || document.markers.Any(marker => marker == null))
                throw new ArgumentException("Intentional local timing requires every positive frame and every existing policy marker; missing rows are not filled.");
            var timeline = new FrameTimeline(document?.frames?.Select(frame => new TimelineFrame(frame.id, frame.seconds)),
                document?.markers?.Select(marker => new TimelineMarker(marker.id, marker.anchor, marker.reference, marker.offset)));
            var next = current.Family == SpecialFrameFamily.FiniteTracks
                ? ActionSpecialFrameCompiler.AuthorFinite(current.NumericFinite, current.FormId, current.Media, timeline)
                : ActionSpecialFrameCompiler.AuthorAuthored(current.NumericAuthored, current.Media, timeline);
            Edit(() => Replace(next), true); status = "Intentional positive local FrameTime authored with a timing diff, not a lossless conversion claim."; Refresh();
        }
        public void ReturnFixed()
        {
            var current = Candidate ?? throw new ArgumentException(failure);
            var next = current.Family == SpecialFrameFamily.FiniteTracks
                ? ActionSpecialFrameCompiler.FixedFinite(current.NumericFinite, current.FormId, current.Media)
                : ActionSpecialFrameCompiler.FixedAuthored(current.NumericAuthored, current.Media);
            Edit(() => Replace(next), true); status = "This candidate inherits its original fixed source again. Save/apply remain separate."; Refresh();
        }
        public void Apply(bool reset)
        {
            if (!EditorApplication.isPlaying || !arena) throw new ArgumentException("Select an explicit initialized runtime in Play mode.");
            var current = Candidate ?? throw new ArgumentException(failure);
            if (!arena.TryApplySpecialFrames(new[] { current }, reset, out var error)) throw new ArgumentException(error);
            status = "Applied one complete typed source. Started bodies/independent tails retain their versions; files unchanged."; Refresh();
        }
        public void Save()
        {
            Compile(); if (failure != null) throw new ArgumentException(failure);
            fileHash = SpecialFrameSource.Save(repository, fileHash, Draft); savedHash = candidate.ContentHash;
            try { AttackMotionSource.Regenerate(repository, python); }
            catch (Exception error) when (Expected(error)) { status = "Frame source saved; generation failed and is recoverable: " + error.Message; Refresh(); throw; }
            status = "Frame source saved and generated. Runtime mode was not implicitly applied."; Refresh();
        }
        void Rebuild()
        {
            if (fields == null) return; Compile(); fields.Clear();
            if (candidate != null)
            {
                selector.choices = candidate.Actions.Select(value => value.Id).ToList(); selector.SetValueWithoutNotify(selectedId);
            }
            if (selected == null) { Refresh(); return; }
            var record = Draft.actions.Single(value => value.actionId == selectedId);
            fields.Add(Wrapped("Shared steps (identity/reference read-only): " + string.Join(", ", selected.Media.StepIds)));
            if (!selected.UsesFrameTime) fields.Add(Wrapped("Inherited fixed timing. Project explicitly, or intentionally author all local frame seconds and channels above when faithful projection is unavailable."));
            else
            {
                foreach (var frame in record.timeline.frames)
                {
                    var captured = frame; var field = new FloatField("Local seconds · " + frame.id) { name = "frame-" + frame.id }; field.SetValueWithoutNotify(frame.seconds);
                    field.RegisterValueChangedCallback(value => Edit(() => captured.seconds = value.newValue)); fields.Add(field);
                }
                foreach (var marker in record.timeline.markers)
                {
                    var captured = marker; fields.Add(Wrapped(marker.id)); var anchor = new EnumField("Anchor", marker.anchor) { name = "anchor-" + marker.id };
                    anchor.RegisterValueChangedCallback(value => Edit(() => { captured.anchor = (TimelineAnchor)value.newValue; captured.reference = ""; }, true)); fields.Add(anchor);
                    var references = new List<string> { "" };
                    if (marker.anchor == TimelineAnchor.Marker) references.AddRange(record.timeline.markers.Select(value => value.id));
                    else if (marker.anchor != TimelineAnchor.Seconds) references.AddRange(record.timeline.frames.Select(value => value.id));
                    var reference = new DropdownField("Reference", references, references.Contains(marker.reference) ? marker.reference : "") { name = "reference-" + marker.id };
                    reference.SetEnabled(marker.anchor != TimelineAnchor.Seconds); reference.RegisterValueChangedCallback(value => Edit(() => captured.reference = value.newValue)); fields.Add(reference);
                    var offset = new FloatField("Seconds / offset / authored interval") { name = "offset-" + marker.id }; offset.SetValueWithoutNotify(marker.offset);
                    offset.RegisterValueChangedCallback(value => Edit(() => captured.offset = value.newValue)); fields.Add(offset);
                }
            }
            Refresh();
        }
        void Refresh()
        {
            if (state == null) return; Compile(); bool valid = failure == null && selected != null;
            sourceLabel.text = (repository ?? "") + "/" + SpecialFrameSource.RelativePath + "\nfile " + fileHash;
            state.text = valid ? selected.Id + " · " + selected.Family + " · " + selected.Mode + "\nshared " + selected.Media.MotionId + " · policy " + selected.PolicyVersion + "\nsource " + selected.SourceHash + "\nstart " + selected.ContentHash + "\n" + (candidate.ContentHash == savedHash ? "Saved sources" : "Unsaved complete graph") : "Invalid whole candidate; previous runtime retained: " + failure;
            policy.text = valid ? "No basic speed/Cue. Body/hits/teleport/motion/invulnerability/freeze/parry/explicit effect intervals use this local graph; event-relative root/stun/push/wall recovery/recast/travel numeric policy is not scaled. Independent hits/effects may follow body-end.\nRequired channels: " + string.Join(", ", SpecialFrameChannels.Required(selected)) + "\nRoot/stun/push epoch: " + SpecialFrameChannels.Epoch("hit", "root") + "\nWall recovery epoch: " + SpecialFrameChannels.Epoch("movement", "wallRecovery") + "\nRecast epoch: " + SpecialFrameChannels.Epoch("movement", "recastWindow") + "\nOriginal numeric body " + (selected.NumericFinite?.BodyEnd ?? selected.NumericAuthored.Execution.End).ToString("R") + " → selected " + selected.Execution.End.ToString("R") : "";
            foreach (string name in new[] { "save", "applyNext", "applyReset", "convert", "fixed", "authorLocal" })
            {
                var button = rootVisualElement.Q<Button>(name);
                if (button != null) button.SetEnabled(valid && (name != "applyNext" && name != "applyReset" || EditorApplication.isPlaying && arena) &&
                    (name != "convert" || !selected.UsesFrameTime) && (name != "fixed" || selected.UsesFrameTime));
            }
            rootVisualElement.Q<Label>("status").text = status ?? "";
            diff.text = valid ? DescribeDiff(selected) : "No partial apply/save.";
            if (timing != null) { time.highValue = Math.Max(selected.Execution.End, selected.Execution.Hits.Count == 0 ? 0 : selected.Execution.Hits.Max(hit => hit.Time + hit.Active)); time.SetValueWithoutNotify(Mathf.Clamp(time.value, 0, time.highValue)); }
            RefreshSample();
        }
        string DescribeDiff(ActionSpecialFrameDefinition current)
        {
            var original = current.NumericFinite?.Execution ?? current.NumericAuthored.Execution; var effective = current.Execution; var text = new StringBuilder();
            text.AppendLine("Activation " + original.Activation.ToString("R") + " → " + effective.Activation.ToString("R") + " · body-end " + original.End.ToString("R") + " → " + effective.End.ToString("R"));
            for (int index = 0; index < original.Hits.Count; index++) text.AppendLine("hit-" + index + " [" + original.Hits[index].Time.ToString("R") + ",+" + original.Hits[index].Active.ToString("R") + "] → [" + effective.Hits[index].Time.ToString("R") + ",+" + effective.Hits[index].Active.ToString("R") + "] · " + effective.Hits[index].Anchor);
            if (current.Timeline != null) foreach (var marker in current.Timeline.Markers) text.AppendLine(marker.Id + " = " + current.Timeline.Times[marker.Id].ToString("R") + " (" + marker.Anchor + ":" + marker.Reference + "+" + marker.Offset.ToString("R") + ")");
            return text.ToString();
        }
        void RefreshSample()
        {
            if (sample == null) return;
            if (selected == null || timing == null) { sample.text = failure ?? "No valid whole source."; body?.MarkDirtyRepaint(); return; }
            float at = time.value; int index = selected.BodyFrameAt(at, timing); var text = new StringBuilder();
            text.AppendLine("Read-only body " + timing.Frames[index] + " · bound Sprite " + sprites.Count(sprite => sprite) + "/" + sprites.Length + " · no ticks/resources/hits/Cue executed");
            for (int hit = 0; hit < selected.Execution.Hits.Count; hit++) { var value = selected.Execution.Hits[hit]; text.AppendLine("hit-" + hit + " " + (at >= value.Time && at < value.Time + value.Active ? "active" : "inactive") + " · " + value.Anchor); }
            sample.text = text.ToString(); body?.MarkDirtyRepaint();
        }
        void DrawBody()
        {
            var bounds = GUILayoutUtility.GetRect(1, 260, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(bounds, new Color(.12f, .14f, .16f));
            if (timing == null || sprites.Length == 0 || selected == null) return; var sprite = sprites[selected.BodyFrameAt(time.value, timing)];
            if (!sprite) { GUI.Label(bounds, "Missing explicit media; no substitute image."); return; }
            var rect = sprite.rect; float zoom = Math.Min((bounds.width - 40) / rect.width, (bounds.height - 30) / rect.height);
            var target = new Rect(bounds.center.x - sprite.pivot.x * zoom, bounds.yMax - 15 - (rect.height - sprite.pivot.y) * zoom, rect.width * zoom, rect.height * zoom); var matrix = GUI.matrix;
            if (left.value) GUIUtility.ScaleAroundPivot(new Vector2(-1, 1), bounds.center);
            try { GUI.DrawTextureWithTexCoords(target, sprite.texture, new Rect(rect.x / sprite.texture.width, rect.y / sprite.texture.height, rect.width / sprite.texture.width, rect.height / sprite.texture.height)); }
            finally { GUI.matrix = matrix; }
        }
        void FileAction(Action action)
        {
            try { action(); } catch (Exception error) when (Expected(error)) { status = error.Message; Refresh(); }
        }
        static bool Expected(Exception error) => error is ArgumentException || error is InvalidOperationException || error is IOException || error is UnauthorizedAccessException || error is System.ComponentModel.Win32Exception;
    }
}
