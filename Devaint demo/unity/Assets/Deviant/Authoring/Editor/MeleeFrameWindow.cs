using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class MeleeFrameWindow : EditorWindow
    {
        [SerializeField] MeleeFrameDraft edits;
        [SerializeField] string repository, fileHash, firstFileHash, selected;
        [SerializeField] CombatArena arena;
        MeleeFrameCatalog saved;
        FixedAttackMotion firstBinding;
        Label state, status, comparison;
        DropdownField selector;
        VisualElement fields;
        string message;
        ActionGeometryPreview preview;
        MeleeFrameCatalogDocument Draft => edits ? edits.source : null;
        public MeleeFrameDefinition Candidate
        {
            get
            {
                var current = Compile().Actions.Single(value => value.Id == selected);
                if (current.Id != FixedAttackMotion.FirstSwordAction) return current;
                if (edits.firstMode == MeleeTimingMode.CombatTimeFixed)
                    return MeleeFrameCompiler.CompileBasic(current.NumericAction, current.FormId, current.InputSpeed, current.Media,
                        MeleeFrameCompiler.FixedBasic(current.NumericAction, current.FormId, current.InputSpeed, current.Media));
                return MeleeFrameCompiler.CompileFirstDocument(current.NumericAction, firstBinding, edits.firstTimeline, current.InputSpeed);
            }
        }
        [MenuItem("Tools/deviant/Experiments/Melee Frame Timing")]
        public static void Open() => GetWindow<MeleeFrameWindow>("Melee frame timing");
        void OnEnable() { Undo.undoRedoPerformed += Rebuild; }
        void OnDisable() { Undo.undoRedoPerformed -= Rebuild; }
        void OnDestroy() { if (edits) DestroyImmediate(edits); }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(Wrapped("17 melee basics + 3 direct specials · one action-local frame authority. Fixed actions inherit shared media weights. Explicit conversion captures local seconds; other actions sharing that clip remain unchanged."));
            selector = new DropdownField("Existing action") { name = "action" }; selector.RegisterValueChangedCallback(change => { selected = change.newValue; Rebuild(); }); scroll.Add(selector);
            var target = new ObjectField("Explicit runtime") { name = "runtime", objectType = typeof(CombatArena), allowSceneObjects = true, value = arena };
            target.RegisterValueChangedCallback(change => { arena = change.newValue as CombatArena; Refresh(); }); scroll.Add(target);
            var controls = new VisualElement(); controls.style.flexDirection = FlexDirection.Row; controls.style.flexWrap = Wrap.Wrap; scroll.Add(controls);
            controls.Add(Button("reload", "Reload", () => Run(Reload))); controls.Add(Button("convert", "Convert this action to FrameTime", () => Run(Convert)));
            controls.Add(Button("fixed", "Use inherited fixed timing", () => Run(ReturnFixed))); controls.Add(Button("apply", "Apply this action next start", () => Run(() => Apply(false))));
            controls.Add(Button("applyReset", "Apply and reset", () => Run(() => Apply(true)))); controls.Add(Button("save", "Save action source", () => Run(SaveSource)));
            state = Wrapped(""); state.name = "state"; scroll.Add(state); status = Wrapped(""); status.name = "status"; scroll.Add(status); comparison = Wrapped(""); comparison.name = "diff"; scroll.Add(comparison);
            fields = new VisualElement { name = "frameFields" }; scroll.Add(fields); preview = new ActionGeometryPreview(); scroll.Add(preview);
            if (Draft == null)
            {
                var directory = new DirectoryInfo(Application.dataPath); while (directory != null && !File.Exists(Path.Combine(directory.FullName, MeleeFrameSource.RelativePath))) directory = directory.Parent;
                if (directory != null) Run(() => LoadRepository(directory.FullName));
            }
            Rebuild();
        }
        static Label Wrapped(string text) { var label = new Label(text); label.style.whiteSpace = WhiteSpace.Normal; return label; }
        static Button Button(string name, string text, Action action) => new Button(action) { name = name, text = text };
        void Run(Action action) { try { action(); } catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException || error is InvalidOperationException) { message = error.Message; } Refresh(); }
        public void LoadRepository(string path) { repository = path; Reload(); }
        public void Reload()
        {
            saved = MeleeFrameSource.Load(repository, out fileHash); if (!edits) { edits = CreateInstance<MeleeFrameDraft>(); edits.hideFlags = HideFlags.HideAndDontSave; }
            Undo.RecordObject(edits, "Reload melee frame source"); edits.source = saved.ToDocument();
            var delegated = FrameMotionSource.Load(repository, out firstFileHash); firstBinding = delegated.FixedBinding;
            edits.firstMode = delegated.UsesFrameTime ? MeleeTimingMode.FrameTime : MeleeTimingMode.CombatTimeFixed;
            edits.firstTimeline = delegated.UsesFrameTime ? delegated.ToDocument().timeline : null;
            if (string.IsNullOrEmpty(selected) || !saved.Actions.Any(value => value.Id == selected)) selected = saved.Actions[0].Id;
            message = "Loaded sources. Runtime is unchanged."; Rebuild();
        }
        MeleeFrameCatalog Compile()
        {
            if (Draft == null || string.IsNullOrEmpty(repository)) throw new InvalidOperationException("Load the canonical numeric/media/frame sources first.");
            return MeleeFrameSource.Compile(repository, Draft);
        }
        public void Convert()
        {
            var current = Candidate;
            if (current.Id == FixedAttackMotion.FirstSwordAction)
            {
                Edit(() => { edits.firstMode = MeleeTimingMode.FrameTime; edits.firstTimeline = FrameAttackMotionSource.FromCandidate(MeleeFrameCompiler.CompileFirst(current.NumericAction, firstBinding,
                    MeleeFrameCompiler.ProjectFirst(current.NumericAction, firstBinding, current.InputSpeed), current.InputSpeed)).ToDocument().timeline; }, true);
                message = "Delegated first action converted through its one canonical source view; no duplicate family record."; return;
            }
            var next = current.Family == MeleeFrameFamily.Basic ? MeleeFrameCompiler.ProjectBasic(current.NumericAction, current.FormId, current.InputSpeed, current.Media) : MeleeFrameCompiler.ProjectDirect(current.NumericDirect, current.Media);
            Edit(() => Replace(next), true); message = "Explicit conversion captured action-local seconds/anchors; inherited numeric timing is now inactive provenance.";
        }
        public void ReturnFixed()
        {
            var current = Candidate;
            if (current.Id == FixedAttackMotion.FirstSwordAction) { Edit(() => { edits.firstMode = MeleeTimingMode.CombatTimeFixed; edits.firstTimeline = null; }, true); message = "First candidate returned fixed; apply/save remain explicit."; return; }
            var next = current.Family == MeleeFrameFamily.Basic ? MeleeFrameCompiler.FixedBasic(current.NumericAction, current.FormId, current.InputSpeed, current.Media) : MeleeFrameCompiler.FixedDirect(current.NumericDirect, current.Media);
            Edit(() => Replace(next), true); message = "This candidate inherits fixed numeric times again; save/apply remain separate.";
        }
        void Replace(MeleeFrameSourceDocument source) { int index = Array.FindIndex(Draft.actions, value => value.actionId == source.actionId); if (index < 0) throw new ArgumentException("Unsupported action frame source."); Draft.actions[index] = source; }
        public void Apply(bool reset)
        {
            if (!EditorApplication.isPlaying || !arena) throw new InvalidOperationException("Select an initialized runtime in Play mode.");
            var candidate = Candidate;
            if (candidate.Id == FixedAttackMotion.FirstSwordAction)
            {
                string firstError;
                bool applied = candidate.Mode == MeleeTimingMode.FrameTime ? arena.TryApplySwordFrameTiming(candidate, reset, out firstError) : arena.TryRestoreSwordFixedTiming(reset, out firstError);
                if (!applied) throw new InvalidOperationException(firstError);
            }
            else if (!arena.TryApplyMeleeFrames(new[] { candidate }, reset, out var error)) throw new InvalidOperationException(error);
            message = "Complete candidate applied; running cast/body/Cue retains its start snapshot. Source unchanged.";
        }
        public void SaveSource()
        {
            if (selected == FixedAttackMotion.FirstSwordAction)
                firstFileHash = edits.firstMode == MeleeTimingMode.FrameTime ? FrameMotionSource.Save(repository, firstFileHash, Candidate) : FrameMotionSource.ReturnFixedSource(repository, firstFileHash, firstBinding);
            else fileHash = MeleeFrameSource.Save(repository, fileHash, Draft);
            AttackMotionSource.Regenerate(repository); saved = MeleeFrameSource.Load(repository, out fileHash);
            message = "Selected canonical action source saved and generated. The other delegated/family source and runtime were not written.";
        }
        void Edit(Action change, bool rebuild = false)
        {
            Undo.IncrementCurrentGroup(); Undo.RecordObject(edits, "Edit melee frame source"); change(); EditorUtility.SetDirty(edits); message = null; if (rebuild) Rebuild(); else Refresh();
        }
        void Rebuild()
        {
            if (fields == null) return; fields.Clear(); if (saved == null || Draft == null) { Refresh(); return; }
            selector.choices = saved.Actions.Select(value => value.Id).ToList(); selector.SetValueWithoutNotify(selected);
            MeleeFrameDefinition current; try { current = Candidate; } catch (ArgumentException) { Refresh(); return; }
            if (current.Id == FixedAttackMotion.FirstSwordAction) fields.Add(Wrapped("Delegated first canonical source · save writes base_sword_attack1.json only, never an extra family record."));
            if (current.Mode == MeleeTimingMode.CombatTimeFixed) { fields.Add(Wrapped("Inherited CombatTimeFixed weights/timing. Convert explicitly to edit independent local frame seconds.")); Refresh(); return; }
            var timeline = current.Id == FixedAttackMotion.FirstSwordAction ? edits.firstTimeline : Draft.actions.Single(value => value.actionId == selected).timeline;
            for (int index = 0; index < timeline.frames.Length; index++)
            {
                var step = timeline.frames[index]; var field = new FloatField(step.id) { name = "frame-" + step.id, value = step.seconds };
                field.RegisterValueChangedCallback(change => Edit(() => step.seconds = change.newValue)); fields.Add(field);
            }
            foreach (var marker in timeline.markers)
            {
                var row = new Foldout { text = marker.id, value = true }; fields.Add(row);
                var anchor = new EnumField("Anchor", marker.anchor); anchor.RegisterValueChangedCallback(change => Edit(() => marker.anchor = (TimelineAnchor)change.newValue)); row.Add(anchor);
                var reference = new TextField("Existing step/marker ID") { value = marker.reference }; reference.RegisterValueChangedCallback(change => Edit(() => marker.reference = change.newValue)); row.Add(reference);
                var offset = new FloatField("Seconds offset") { value = marker.offset }; offset.RegisterValueChangedCallback(change => Edit(() => marker.offset = change.newValue)); row.Add(offset);
            }
            Refresh();
        }
        void Refresh()
        {
            if (state == null) return; MeleeFrameDefinition candidate = null; string error = null;
            try { candidate = Candidate; } catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException || failure is IOException) { error = failure.Message; }
            bool valid = candidate != null; status.text = message ?? "";
            state.text = valid ? candidate.Id + " · " + candidate.Mode + " · " + candidate.Media.MotionId + " · policy " + candidate.PolicyVersion.Substring(0, 12) + " · candidate " + candidate.ContentHash.Substring(0, 12) : "Invalid whole candidate: " + error;
            rootVisualElement.Q<Button>("convert")?.SetEnabled(valid && candidate.Mode == MeleeTimingMode.CombatTimeFixed);
            rootVisualElement.Q<Button>("fixed")?.SetEnabled(valid && candidate.Mode == MeleeTimingMode.FrameTime);
            rootVisualElement.Q<Button>("save")?.SetEnabled(valid && !string.IsNullOrEmpty(fileHash)); rootVisualElement.Q<Button>("apply")?.SetEnabled(valid && EditorApplication.isPlaying && arena);
            rootVisualElement.Q<Button>("applyReset")?.SetEnabled(valid && EditorApplication.isPlaying && arena);
            if (!valid) { comparison.text = ""; preview?.SetInvalid(error); return; }
            var baseline = saved.Actions.Single(value => value.Id == candidate.Id);
            comparison.text = $"Contact {baseline.Action.Impact:R} → {candidate.Action.Impact:R}s · active {baseline.Action.Active:R} → {candidate.Action.Active:R}s · end {baseline.Action.Duration:R} → {candidate.Action.Duration:R}s\nAdvance {baseline.AdvanceFrom:R}/{baseline.AdvanceDuration:R} → {candidate.AdvanceFrom:R}/{candidate.AdvanceDuration:R}s · inherited/override shown explicitly; Save and Apply are separate.";
            if (candidate.Family == MeleeFrameFamily.Direct) preview?.SetCandidate(candidate.Direct, arena, candidate.Mode == MeleeTimingMode.FrameTime ? candidate : null);
            else preview?.SetCandidate(candidate.Action, frame: candidate.Mode == MeleeTimingMode.FrameTime ? candidate : null, arena: arena);
        }
    }
}
