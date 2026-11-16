using System;
using System.IO;
using System.Text;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class AttackMotionWindow : EditorWindow
    {
        public const string LayoutPath = "Assets/Deviant/Authoring/Editor/AttackMotionWindow.uxml";
        [SerializeField] AttackMotionDraft edits;
        [SerializeField] AttackMotionDocument saved;
        [SerializeField] BasicMeleeHitData previewAction;
        [SerializeField] string repository, source, fileHash, savedHash, python = "python";
        [SerializeField] CombatArena arena;
        [SerializeField] bool frameSourceActive;
        ActionGeometryPreview geometryPreview;
        string status;
        AttackMotionDocument Draft => edits ? edits.document : null;
        [MenuItem("Tools/deviant/Experiments/Basic Sword Motion")]
        public static void Open() => GetWindow<AttackMotionWindow>("Sword motion");
        void OnEnable() { Undo.undoRedoPerformed += RefreshFields; }
        void OnDisable() { Undo.undoRedoPerformed -= RefreshFields; }
        void OnDestroy() { if (edits) DestroyImmediate(edits); }
        void OnInspectorUpdate() { RefreshState(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (!layout) { rootVisualElement.Add(new Label("AttackMotionWindow.uxml missing")); return; }
            layout.CloneTree(rootVisualElement);
            geometryPreview = new ActionGeometryPreview(); rootVisualElement.Q("geometryPreview").Add(geometryPreview);
            var target = rootVisualElement.Q<ObjectField>("runtime"); target.objectType = typeof(CombatArena); target.allowSceneObjects = true;
            target.RegisterValueChangedCallback(e => { arena = e.newValue as CombatArena; RefreshState(); });
            rootVisualElement.Q<FloatField>("sourceImpact").RegisterValueChangedCallback(e => Edit(d => d.sourceImpact = e.newValue));
            rootVisualElement.Q<FloatField>("fxStart").RegisterValueChangedCallback(e => Edit(d => d.fxStart = e.newValue));
            rootVisualElement.Q<TextField>("python").SetValueWithoutNotify(python);
            rootVisualElement.Q<TextField>("python").RegisterValueChangedCallback(e => python = e.newValue);
            rootVisualElement.Q<Button>("reload").clicked += Reload;
            rootVisualElement.Q<Button>("save").clicked += Save;
            rootVisualElement.Q<Button>("applyNext").clicked += () => Apply(false);
            rootVisualElement.Q<Button>("applyReset").clicked += () => Apply(true);
            if (Draft == null)
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, AttackMotionSource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName); else status = "모션 원본을 찾지 못했습니다.";
            }
            RefreshFields();
        }
        public void LoadRepository(string path) { repository = path; source = Path.Combine(path, AttackMotionSource.RelativePath); Reload(); }
        void Reload()
        {
            try
            {
                var document = AttackMotionSource.Load(source, out var hash);
                frameSourceActive = document.mode == FrameAttackMotionSource.FrameMode;
                var motion = new FrameAttackMotionSource(document).FixedBinding;
                var action = BasicSwordSource.Validate(BasicSwordSource.Load(Path.Combine(repository, "data/combat/actions/base_sword_basic_0.json"), out _));
                if (!edits) { edits = CreateInstance<AttackMotionDraft>(); edits.hideFlags = HideFlags.HideAndDontSave; }
                Undo.RecordObject(edits, "Reload attack motion"); edits.document = document;
                fileHash = hash; savedHash = motion.ContentHash; saved = motion.ToDocument(); previewAction = BasicMeleeHitData.From(action); status = "원본을 읽었습니다. 실행 바인딩은 유지합니다.";
            }
            catch (Exception e) when (Expected(e)) { status = e.Message; }
            RefreshFields();
        }
        void Edit(Action<AttackMotionDocument> change)
        {
            if (Draft == null || frameSourceActive) return;
            Undo.IncrementCurrentGroup(); Undo.RecordObject(edits, "Edit attack motion"); change(Draft); status = null; RefreshState();
        }
        void Save()
        {
            bool wrote = false;
            try
            {
                frameSourceActive = AttackMotionSource.Load(source, out _).mode == FrameAttackMotionSource.FrameMode;
                if (frameSourceActive) throw new ArgumentException("FrameTime source is active. Its fixed media/art binding is read-only; edit and save in Frame Timing Preview.");
                fileHash = AttackMotionSource.Save(source, fileHash, Draft); wrote = true;
                var motion = new FixedAttackMotion(Draft); saved = motion.ToDocument(); savedHash = motion.ContentHash;
                AttackMotionSource.Regenerate(repository, python);
                string catalog = Path.Combine(repository, AttackMotionSource.CatalogPath);
                if (Path.GetFullPath(catalog).StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/CharacterV2/AnimationCatalog.json");
                status = "모션 원본 저장·카탈로그 재생성 완료. 런타임 적용은 별도입니다.";
            }
            catch (Exception e) when (Expected(e)) { status = (wrote ? "원본 저장 완료, 생성 실패: " : "저장 실패: ") + e.Message; }
            RefreshState();
        }
        void Apply(bool reset)
        {
            if (!EditorApplication.isPlaying || !arena || Draft == null) return;
            status = arena.TryApplyBasicSwordMotion(Draft, reset, out var error)
                ? (reset ? "리셋 적용 완료. 원본은 유지합니다." : "다음 1타부터 적용합니다. 진행 중 본체·Cue와 원본은 유지합니다.") : error;
            RefreshState();
        }
        void RefreshFields()
        {
            if (rootVisualElement.Q<FloatField>("sourceImpact") == null) return;
            AttackMotionDocument binding = frameSourceActive ? Draft?.fixedBinding?.ToDocument() : Draft;
            rootVisualElement.Q<FloatField>("sourceImpact").SetValueWithoutNotify(binding?.sourceImpact ?? 0);
            rootVisualElement.Q<FloatField>("fxStart").SetValueWithoutNotify(binding?.fxStart ?? -1);
            var list = rootVisualElement.Q("frames"); list.Clear();
            if (binding?.steps != null)
                for (int i = 0; i < binding.steps.Length; i++)
                {
                    int index = i; var step = binding.steps[i];
                    list.Add(new Label(step.id + " · " + step.frame));
                    var field = new FloatField("원본 유지 시간 (s)") { name = "seconds-" + i };
                    field.SetValueWithoutNotify(step.seconds); field.RegisterValueChangedCallback(e => Edit(d => d.steps[index].seconds = e.newValue)); list.Add(field);
                }
            RefreshState();
        }
        void RefreshState()
        {
            if (rootVisualElement.Q<Button>("save") == null) return;
            if (EditorApplication.isPlaying && !arena)
            { var candidates = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None); if (candidates.Length == 1) arena = candidates[0]; }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena);
            FixedAttackMotion motion = null; string error = null;
            try { motion = new FrameAttackMotionSource(Draft).FixedBinding; } catch (ArgumentException e) { error = e.Message; }
            bool valid = motion != null && !frameSourceActive;
            rootVisualElement.Q<FloatField>("sourceImpact").SetEnabled(!frameSourceActive);
            rootVisualElement.Q<FloatField>("fxStart").SetEnabled(!frameSourceActive);
            foreach (var field in rootVisualElement.Q("frames").Query<FloatField>().ToList()) field.SetEnabled(!frameSourceActive);
            rootVisualElement.Q<Button>("save").SetEnabled(valid && !string.IsNullOrEmpty(fileHash));
            foreach (string name in new[] { "applyNext", "applyReset" }) rootVisualElement.Q<Button>(name).SetEnabled(valid && EditorApplication.isPlaying && arena && arena.Player && arena.characterAnimations != null && arena.FrameSwordTiming == null);
            rootVisualElement.Q<Label>("source").text = source ?? "원본 경로 없음";
            string runtime = arena && arena.TryGetBasicSwordMotion(out var running, out _) ? " · 실행 " + running.ContentHash.Substring(0, 12) : "";
            rootVisualElement.Q<Label>("state").text = frameSourceActive ? "FrameTime 원본 활성: 고정 매체/아트 바인딩은 읽기 전용입니다. Frame Timing Preview에서 편집·저장하세요." : valid ? (motion.ContentHash == savedHash ? "저장된 원본과 동일" : "미저장 변경") + " · 편집 " + motion.ContentHash.Substring(0, 12) + runtime : "적용·저장 불가: " + error;
            rootVisualElement.Q<Label>("status").text = (arena && arena.FrameSwordTiming != null ? "임시 프레임 기준 실행 중: Frame Timing Preview에서 고정 실행으로 복귀한 뒤 별도 모션을 적용하세요.\n" : "") + (status ?? "");
            RefreshPreview(motion, error);
        }
        void RefreshPreview(FixedAttackMotion motion, string error)
        {
            var diff = new StringBuilder(); var preview = new StringBuilder();
            BasicMeleeDefinition action = !frameSourceActive && arena && arena.BasicSwordFirst != null ? arena.BasicSwordFirst : null;
            if (action == null && !BasicMeleeDefinition.TryCreate(BasicSwordSource.ActionId, previewAction?.ToData(), out action, out var actionError))
            { geometryPreview?.SetInvalid(actionError); rootVisualElement.Q<Label>("preview").text = "환산 시간에 필요한 전투 정의를 재로드하세요."; rootVisualElement.Q<Label>("diff").text = ""; return; }
            if (motion == null) geometryPreview?.SetInvalid(error);
            else if (frameSourceActive)
            {
                try { var candidate = new FrameAttackMotionSource(Draft).Compile(action, FrameMotionSource.ReadInputSpeed(repository)); geometryPreview?.SetCandidate(candidate.Action, frame: candidate, arena: arena); }
                catch (ArgumentException exception) { geometryPreview?.SetInvalid(exception.Message); }
            }
            else geometryPreview?.SetCandidate(action, motion, arena: arena);
            if (frameSourceActive) { rootVisualElement.Q<Label>("preview").text = "FrameTime 원본의 같은 컴파일 후보를 위 장면에 표시합니다. 타격/종료·프레임 편집은 Frame Timing Preview를 사용하세요."; rootVisualElement.Q<Label>("diff").text = "고정 매체 바인딩 편집 불가"; return; }
            if (saved != null && Draft?.steps != null)
            {
                if (saved.sourceImpact != Draft.sourceImpact) diff.AppendLine($"원본 접촉: {saved.sourceImpact:R} → {Draft.sourceImpact:R} s");
                if (saved.fxStart != Draft.fxStart) diff.AppendLine($"Cue 시작: {saved.fxStart:R} → {Draft.fxStart:R} s");
                for (int i = 0; i < Math.Min(saved.steps.Length, Draft.steps.Length); i++)
                    if (saved.steps[i].seconds != Draft.steps[i].seconds) diff.AppendLine($"{Draft.steps[i].id}: {saved.steps[i].seconds:R} → {Draft.steps[i].seconds:R} s");
            }
            float impact = action.Impact, end = action.Duration;
            preview.AppendLine($"현재 기준 공속 전 타격 {impact * 1000:F2} ms · 종료 {end * 1000:F2} ms (편집 후에도 고정)");
            if (motion != null)
            {
                var actual = motion.EffectiveSeconds(impact, end);
                var before = saved == null ? null : new FixedAttackMotion(saved).EffectiveSeconds(impact, end);
                for (int i = 0; i < actual.Count; i++) preview.AppendLine($"{motion.StepIds[i]} 실제 유지: {(before != null && i < before.Count ? (before[i] * 1000).ToString("F2") : "?")} → {actual[i] * 1000:F2} ms");
            }
            rootVisualElement.Q<Label>("preview").text = preview.ToString(); rootVisualElement.Q<Label>("diff").text = diff.Length == 0 ? "원본 대비 수치 변경 없음" : diff.ToString();
        }
        static bool Expected(Exception e) => e is IOException || e is ArgumentException || e is UnauthorizedAccessException || e is System.ComponentModel.Win32Exception;
    }
}
