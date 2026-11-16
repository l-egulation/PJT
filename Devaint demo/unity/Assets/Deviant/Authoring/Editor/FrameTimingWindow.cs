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
    public sealed class FrameTimingWindow : EditorWindow
    {
        public const string LayoutPath = "Assets/Deviant/Authoring/Editor/FrameTimingWindow.uxml";
        [SerializeField] FrameTimingDraft draft;
        [SerializeField] string repository, motionVersion, actionVersion, baselineVersion;
        [SerializeField] string presetPath, presetFileHash, savedPresetCandidateHash;
        [SerializeField] string motionFileHash, savedSourceCandidateHash, python = "python";
        [SerializeField] AttackMotionDocument savedSource;
        [SerializeField] string savedSourceActionHash, savedSourceMotionHash;
        [SerializeField] float baselineContact, baselineEnd;
        [SerializeField] CombatArena arena;
        ActionGeometryPreview geometryPreview;
        string status;
        [MenuItem("Tools/deviant/Experiments/Frame Timing Preview")]
        public static void Open() => GetWindow<FrameTimingWindow>("Frame timing");
        void OnEnable() { Undo.undoRedoPerformed += RefreshFields; }
        void OnDisable() { Undo.undoRedoPerformed -= RefreshFields; }
        void OnDestroy() { if (draft) DestroyImmediate(draft); }
        void OnInspectorUpdate() { RefreshState(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (!layout) { rootVisualElement.Add(new Label("FrameTimingWindow.uxml missing")); return; }
            layout.CloneTree(rootVisualElement); rootVisualElement.Q<Button>("reload").clicked += Reload;
            geometryPreview = new ActionGeometryPreview(); rootVisualElement.Q("geometryPreview").Add(geometryPreview);
            var target = rootVisualElement.Q<ObjectField>("runtime"); target.objectType = typeof(CombatArena); target.allowSceneObjects = true;
            target.RegisterValueChangedCallback(e => { arena = e.newValue as CombatArena; RefreshState(); });
            rootVisualElement.Q<Button>("applyNext").clicked += () => Apply(false);
            rootVisualElement.Q<Button>("applyReset").clicked += () => Apply(true);
            rootVisualElement.Q<Button>("restoreFixed").clicked += RestoreFixed;
            rootVisualElement.Q<Button>("importPreset").clicked += ImportDialog;
            rootVisualElement.Q<Button>("exportPreset").clicked += ExportDialog;
            rootVisualElement.Q<Button>("saveSource").clicked += () => FileAction(() => SavePermanentSource(python));
            rootVisualElement.Q<TextField>("python").SetValueWithoutNotify(python);
            rootVisualElement.Q<TextField>("python").RegisterValueChangedCallback(e => python = e.newValue);
            if (!draft)
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, AttackMotionSource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName); else status = "모션 원본을 찾지 못했습니다.";
            }
            RefreshFields();
        }
        public void LoadRepository(string path) { repository = path; Reload(); }
        void Reload()
        {
            try
            {
                var source = FrameMotionSource.Load(repository, out var hash); var motion = source.FixedBinding;
                var action = FrameMotionSource.ReadAction(repository);
                float inputSpeed = FrameMotionSource.ReadInputSpeed(repository);
                var candidate = source.UsesFrameTime ? source.Compile(action, inputSpeed) : MeleeFrameCompiler.CompileFirst(action, motion, MeleeFrameCompiler.ProjectFirst(action, motion, inputSpeed), inputSpeed);
                var timeline = candidate.Timeline;
                if (!draft) { draft = CreateInstance<FrameTimingDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; }
                else { Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Reload frame timing preview"); }
                draft.Load(timeline, action, motion, source, inputSpeed);
                baselineContact = candidate.Action.Impact; baselineEnd = candidate.Action.Duration; motionVersion = motion.ContentHash; actionVersion = action.ContentHash; baselineVersion = timeline.ContentHash;
                motionFileHash = hash; savedSourceCandidateHash = source.UsesFrameTime ? candidate.ContentHash : null;
                savedSource = source.ToDocument();
                savedSourceActionHash = action.ContentHash; savedSourceMotionHash = motion.ContentHash;
                presetPath = presetFileHash = savedPresetCandidateHash = null;
                status = source.UsesFrameTime ? "FrameTime 원본을 다시 읽었습니다. 전투 적용/리셋은 별도입니다." : "고정 모드의 실제 시간을 보존해 변환했습니다. diff 확인 후 명시적으로 FrameTime 원본에 저장할 수 있습니다.";
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException) { status = exception.Message; }
            RefreshFields();
        }
        public void LoadPreset(string path)
        {
            var candidate = FramePresetSource.Load(path, repository, out var hash);
            var source = FrameMotionSource.Load(repository, out var sourceHash);
            float inputSpeed = FrameMotionSource.ReadInputSpeed(repository);
            var baseline = source.UsesFrameTime ? source.Compile(candidate.SourceAction, inputSpeed) : MeleeFrameCompiler.CompileFirst(candidate.SourceAction, candidate.SourceMotion, MeleeFrameCompiler.ProjectFirst(candidate.SourceAction, candidate.SourceMotion, inputSpeed), inputSpeed);
            if (!draft) { draft = CreateInstance<FrameTimingDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; }
            else { Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Load frame experiment"); }
            draft.Load(candidate.Timeline, candidate.SourceAction, candidate.SourceMotion, source, inputSpeed);
            baselineContact = baseline.Action.Impact; baselineEnd = baseline.Action.Duration;
            motionVersion = candidate.SourceMotion.ContentHash; actionVersion = candidate.SourceAction.ContentHash; baselineVersion = baseline.Timeline.ContentHash;
            motionFileHash = sourceHash; savedSourceCandidateHash = source.UsesFrameTime ? baseline.ContentHash : null;
            savedSource = source.ToDocument();
            savedSourceActionHash = candidate.SourceAction.ContentHash; savedSourceMotionHash = candidate.SourceMotion.ContentHash;
            presetPath = Path.GetFullPath(path); presetFileHash = hash; savedPresetCandidateHash = candidate.ContentHash;
            status = "프레임 실험 파일을 불러왔습니다. 전투 적용/리셋은 별도입니다."; RefreshFields();
        }
        public string SavePreset(string path, string expectedHash)
        {
            if (!draft) throw new InvalidOperationException("프레임 후보를 먼저 가져오세요.");
            var candidate = draft.CompileCandidate();
            string hash = FramePresetSource.Save(path, expectedHash, repository, candidate);
            presetPath = Path.GetFullPath(path); presetFileHash = hash; savedPresetCandidateHash = candidate.ContentHash;
            status = "프레임 실험 파일만 저장했습니다. 전투/수치·모션 원본은 유지합니다."; RefreshState(); return hash;
        }
        public string SavePermanentSource(string pythonExecutable = "python")
        {
            if (!draft) throw new InvalidOperationException("프레임 후보를 먼저 가져오세요.");
            var candidate = draft.CompileCandidate(); bool wrote = false;
            try
            {
                motionFileHash = FrameMotionSource.Save(repository, motionFileHash, candidate); wrote = true;
                savedSourceCandidateHash = candidate.ContentHash;
                savedSource = FrameAttackMotionSource.FromCandidate(candidate).ToDocument();
                savedSourceActionHash = candidate.SourceAction.ContentHash; savedSourceMotionHash = candidate.SourceMotion.ContentHash;
                AttackMotionSource.Regenerate(repository, pythonExecutable);
                string catalog = Path.GetFullPath(Path.Combine(repository, AttackMotionSource.CatalogPath));
                if (catalog.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/CharacterV2/AnimationCatalog.json");
                status = "FrameTime 원본 저장·카탈로그 재생성 완료. 새 Play 세션부터 자동 사용하며 현재 전투 적용은 별도입니다.";
                RefreshState(); return motionFileHash;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException || exception is System.ComponentModel.Win32Exception)
            {
                status = (wrote ? "FrameTime 원본 저장 완료, 생성 실패: " : "원본 저장 실패: ") + exception.Message;
                RefreshState(); throw;
            }
        }
        string PresetDirectory => !string.IsNullOrEmpty(presetPath) ? Path.GetDirectoryName(presetPath) : Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs"));
        void ImportDialog()
        {
            string path = EditorUtility.OpenFilePanel("프레임 실험 불러오기", PresetDirectory, "json");
            if (!string.IsNullOrEmpty(path)) FileAction(() => LoadPreset(path));
        }
        void ExportDialog()
        {
            string path = EditorUtility.SaveFilePanel("프레임 실험 저장", PresetDirectory, "first-sword-frame-experiment.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            FileAction(() => {
                string expected = !string.IsNullOrEmpty(presetPath) && string.Equals(Path.GetFullPath(path), presetPath, StringComparison.OrdinalIgnoreCase) ? presetFileHash : null;
                if (expected == null && File.Exists(path)) FramePresetSource.Load(path, repository, out expected);
                SavePreset(path, expected);
            });
        }
        void FileAction(Action action)
        {
            status = null;
            try { action(); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is IOException || exception is UnauthorizedAccessException || exception is System.ComponentModel.Win32Exception) { status ??= exception.Message; RefreshState(); }
        }
        void Apply(bool reset)
        {
            if (!EditorApplication.isPlaying || !arena || !draft) return;
            try { status = arena.TryApplySwordFrameTiming(draft.CompileCandidate(), reset, out var error) ? (reset ? "프레임 후보를 리셋 적용했습니다. 파일은 유지합니다." : "다음 1타부터 프레임 후보를 적용합니다. 진행 중 실행과 파일은 유지합니다.") : error; }
            catch (ArgumentException exception) { status = exception.Message; }
            RefreshState();
        }
        void RestoreFixed()
        {
            if (!EditorApplication.isPlaying || !arena) return;
            status = arena.TryRestoreSwordFixedTiming(false, out var error) ? "다음 1타부터 기존 고정 실행으로 복귀합니다. 진행 중 실행과 파일은 유지합니다." : error;
            RefreshState();
        }
        void Edit(Action change, bool rebuild = false)
        {
            if (!draft) return;
            Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Edit frame timing preview"); change(); status = null;
            if (rebuild) RefreshFields(); else RefreshState();
        }
        void RefreshFields()
        {
            if (rootVisualElement.Q("frames") == null) return;
            var frames = rootVisualElement.Q("frames"); var markers = rootVisualElement.Q("markers"); frames.Clear(); markers.Clear();
            if (draft)
            {
                foreach (var step in draft.steps)
                {
                    var captured = step; var field = new FloatField(step.id + " 실제 유지 (s)") { name = "frame-" + step.id };
                    field.SetValueWithoutNotify(step.seconds); field.RegisterValueChangedCallback(e => Edit(() => captured.seconds = e.newValue)); frames.Add(field);
                }
                foreach (var marker in draft.markers)
                {
                    var captured = marker; markers.Add(new Label(marker.id));
                    var anchor = new EnumField("시간 기준", marker.anchor) { name = "anchor-" + marker.id };
                    anchor.RegisterValueChangedCallback(e => Edit(() => { captured.anchor = (TimelineAnchor)e.newValue; captured.reference = ""; }, true)); markers.Add(anchor);
                    var options = new List<string> { "" };
                    if (marker.anchor == TimelineAnchor.Marker) options.AddRange(draft.markers.Select(m => m.id));
                    else if (marker.anchor != TimelineAnchor.Seconds) options.AddRange(draft.steps.Select(s => s.id));
                    var reference = new DropdownField("기준 ID (선택 필요)", options, options.Contains(marker.reference) ? marker.reference : "") { name = "reference-" + marker.id };
                    reference.SetEnabled(marker.anchor != TimelineAnchor.Seconds); reference.RegisterValueChangedCallback(e => Edit(() => captured.reference = e.newValue)); markers.Add(reference);
                    var offset = new FloatField(marker.anchor == TimelineAnchor.Seconds ? "절대 시각 (s)" : "오프셋 (s)") { name = "offset-" + marker.id };
                    offset.SetValueWithoutNotify(marker.offset); offset.RegisterValueChangedCallback(e => Edit(() => captured.offset = e.newValue)); markers.Add(offset);
                }
            }
            RefreshState();
        }
        void RefreshState()
        {
            if (rootVisualElement.Q<Label>("state") == null) return;
            if (EditorApplication.isPlaying && !arena) { var candidates = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None); if (candidates.Length == 1) arena = candidates[0]; }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena);
            foreach (string name in new[] { "applyNext", "applyReset", "exportPreset", "saveSource" }) rootVisualElement.Q<Button>(name).SetEnabled(false);
            rootVisualElement.Q<Button>("restoreFixed").SetEnabled(EditorApplication.isPlaying && arena && arena.FrameSwordTiming != null);
            rootVisualElement.Q<Label>("source").text = repository == null ? "원본 경로 없음" : Path.Combine(repository, AttackMotionSource.RelativePath) + "\n모션 " + motionVersion + "\n행동 " + actionVersion;
            rootVisualElement.Q<Label>("status").text = status ?? "";
            rootVisualElement.Q<Label>("preset").text = string.IsNullOrEmpty(presetPath) ? "프레임 실험 파일 선택 없음" : "실험 파일: " + presetPath;
            try
            {
                if (!draft) throw new ArgumentException("원본을 다시 가져오세요.");
                // Undo restores source snapshots with the draft. Derive display
                // metadata from those snapshots instead of the last file load.
                var sourceAction = BasicSwordSource.Validate(draft.actionSource);
                var sourceMotion = new FixedAttackMotion(draft.motionSource);
                var baselineSource = savedSourceCandidateHash != null && savedSource != null && savedSourceActionHash == sourceAction.ContentHash && savedSourceMotionHash == sourceMotion.ContentHash ? savedSource : draft.canonicalSource;
                var canonical = baselineSource == null ? new FrameAttackMotionSource(sourceMotion.ToDocument()) : new FrameAttackMotionSource(baselineSource);
                float inputSpeed = FrameMotionSource.ReadInputSpeed(repository);
                var baseline = canonical.UsesFrameTime ? canonical.Compile(sourceAction, inputSpeed) : MeleeFrameCompiler.CompileFirst(sourceAction, sourceMotion, MeleeFrameCompiler.ProjectFirst(sourceAction, sourceMotion, inputSpeed), inputSpeed);
                baselineContact = baseline.Action.Impact; baselineEnd = baseline.Action.Duration;
                actionVersion = sourceAction.ContentHash; motionVersion = sourceMotion.ContentHash;
                baselineVersion = baseline.Timeline.ContentHash;
                rootVisualElement.Q<Label>("source").text = Path.Combine(repository, AttackMotionSource.RelativePath) + "\n원본 모드 " + (canonical.UsesFrameTime ? FrameAttackMotionSource.FrameMode : FixedAttackMotion.Mode) + " · 모션 바인딩 " + motionVersion + "\n행동 " + actionVersion;
                var candidate = draft.CompileCandidate(); var timeline = candidate.Timeline; var text = new StringBuilder();
                text.AppendLine($"접촉 {baselineContact * 1000:F2} → {timeline.Times[FrameTimingProjection.Contact] * 1000:F2} ms");
                text.AppendLine($"행동 종료 {baselineEnd * 1000:F2} → {timeline.Times[FrameTimingProjection.ActionEnd] * 1000:F2} ms");
                geometryPreview?.SetCandidate(candidate.Action, frame: candidate, arena: arena);
                rootVisualElement.Q<Button>("exportPreset").SetEnabled(!string.IsNullOrEmpty(repository));
                rootVisualElement.Q<Button>("saveSource").SetEnabled(!string.IsNullOrEmpty(repository) && !string.IsNullOrEmpty(motionFileHash));
                text.AppendLine($"타격 활성 {baseline.Action.Active * 1000:F2} → {candidate.Action.Active * 1000:F2} ms · Cue 시작 {(baseline.CueEnabled ? (baseline.Timeline.Times[MeleeFrameDefinition.CueStart] * 1000).ToString("F2") + " ms" : "끔")} → {(candidate.CueEnabled ? (timeline.Times[MeleeFrameDefinition.CueStart] * 1000).ToString("F2") + " ms" : "끔")}");
                foreach (string name in new[] { "applyNext", "applyReset" }) rootVisualElement.Q<Button>(name).SetEnabled(EditorApplication.isPlaying && arena && arena.Player);
                float begin = 0;
                foreach (var frame in timeline.Frames) { text.AppendLine($"{frame.Id}: {begin * 1000:F2} .. {(begin + frame.Seconds) * 1000:F2} ms"); begin += frame.Seconds; }
                rootVisualElement.Q<Label>("result").text = text.ToString();
                rootVisualElement.Q<Label>("state").text = (baselineVersion == timeline.ContentHash ? "가져온 시간과 동일" : "미리보기 편집") + " · " + timeline.ContentHash.Substring(0, 12) + (savedSourceCandidateHash == candidate.ContentHash ? " · FrameTime 원본과 동일" : " · FrameTime 원본 미저장") + (savedPresetCandidateHash == candidate.ContentHash ? " · 실험 파일과 동일" : " · 실험 파일 미저장") + (arena && arena.FrameSwordTiming != null ? " · 다음 실행 프레임 " + arena.FrameSwordTiming.ContentHash.Substring(0, 12) : " · 다음 실행 고정");
            }
            catch (ArgumentException exception) { geometryPreview?.SetInvalid(exception.Message); rootVisualElement.Q<Label>("result").text = ""; rootVisualElement.Q<Label>("state").text = "미리보기 불가: " + exception.Message; }
        }
    }
}
