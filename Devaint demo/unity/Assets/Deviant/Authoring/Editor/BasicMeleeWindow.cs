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
    public sealed class BasicMeleeWindow : EditorWindow
    {
        public const string LayoutPath = "Assets/Deviant/Authoring/Editor/BasicMeleeWindow.uxml";
        static readonly string[] Fields = { "damage", "duration", "impact", "active", "width", "height", "offsetX", "offsetY" };
        const string FirstSwordChoice = "검 · 1타 — 기존 전용 창";
        [SerializeField] BasicMeleeDraft edits;
        [SerializeField] BasicMeleeEdit[] saved;
        [SerializeField] string repository, source, fileHash, selectedId = "base_sword_basic_1", python = "python";
        [SerializeField] CombatArena arena;
        [SerializeField] BasicMeleeDocument firstSwordGeometryDocument;
        [SerializeField] AttackMotionDocument firstSwordMotionDocument;
        BasicMeleeDefinition firstSwordGeometry;
        FrameAttackMotionSource firstSwordGeometryMotion;
        ActionGeometryPreview geometryPreview;
        string status;
        BasicMeleeEdit[] Draft => edits ? edits.hits : null;
        BasicMeleeDefinition[] candidate;
        string candidateError;
        bool candidateDirty = true;
        BasicMeleeEdit Selected => Draft?.FirstOrDefault(hit => hit != null && hit.Id == selectedId);
        bool FirstSwordSelected => selectedId == BasicSwordSource.ActionId;

        [MenuItem("Tools/deviant/Experiments/Melee Basic Hits")]
        public static void Open() => GetWindow<BasicMeleeWindow>("Melee basics");
        void OnEnable() { Undo.undoRedoPerformed += RefreshFields; }
        void OnDisable() { Undo.undoRedoPerformed -= RefreshFields; }
        void OnDestroy() { if (edits) DestroyImmediate(edits); }
        void OnInspectorUpdate() { RefreshState(); }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (!layout) { rootVisualElement.Add(new Label("BasicMeleeWindow.uxml missing")); return; }
            layout.CloneTree(rootVisualElement);
            geometryPreview = new ActionGeometryPreview(); rootVisualElement.Q("geometryPreview").Add(geometryPreview);
            var target = rootVisualElement.Q<ObjectField>("runtime"); target.objectType = typeof(CombatArena); target.allowSceneObjects = true;
            target.RegisterValueChangedCallback(change => { arena = change.newValue as CombatArena; RefreshState(); });
            rootVisualElement.Q<DropdownField>("hit").RegisterValueChangedCallback(change => {
                var selector = rootVisualElement.Q<DropdownField>("hit");
                int index = selector.choices.IndexOf(change.newValue);
                if (index == 0) selectedId = BasicSwordSource.ActionId;
                else if (Draft != null && index > 0 && index <= Draft.Length) selectedId = Draft[index - 1].Id;
                status = null; RefreshFields();
            });
            rootVisualElement.Q<Button>("firstSword").clicked += BasicSwordWindow.Open;
            for (int i = 0; i < Fields.Length; i++)
            {
                int index = i;
                rootVisualElement.Q<FloatField>(Fields[i]).RegisterValueChangedCallback(change => {
                    var selected = Selected;
                    if (selected?.hit == null) return;
                    Undo.RecordObject(edits, "Edit melee basic hits");
                    if (index == 3)
                        foreach (var hit in Draft.Where(hit => hit.formId == selected.formId)) hit.hit.active = change.newValue;
                    else Set(selected.hit, index, change.newValue);
                    candidateDirty = true; status = null; RefreshState();
                });
            }
            rootVisualElement.Q<TextField>("python").SetValueWithoutNotify(python);
            rootVisualElement.Q<TextField>("python").RegisterValueChangedCallback(change => python = change.newValue);
            rootVisualElement.Q<Button>("reload").clicked += Reload;
            rootVisualElement.Q<Button>("save").clicked += Save;
            rootVisualElement.Q<Button>("applyNext").clicked += () => Apply(false);
            rootVisualElement.Q<Button>("applyReset").clicked += () => Apply(true);
            if (Draft == null)
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, BasicMeleeSource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName);
                else status = "근접 기본 타격 원본을 찾지 못했습니다.";
            }
            RefreshFields();
        }

        public void LoadRepository(string path)
        { repository = path; source = Path.Combine(path, BasicMeleeSource.RelativePath); Reload(); }
        void Reload()
        {
            try
            {
                var hits = BasicMeleeSource.Load(repository, out var hash);
                firstSwordGeometryDocument = BasicSwordSource.Load(Path.Combine(repository, FrameMotionSource.ActionPath), out _);
                firstSwordGeometry = BasicSwordSource.Validate(firstSwordGeometryDocument);
                string motionPath = Path.Combine(repository, AttackMotionSource.RelativePath);
                firstSwordMotionDocument = File.Exists(motionPath) ? AttackMotionSource.Load(motionPath, out _) : null;
                firstSwordGeometryMotion = firstSwordMotionDocument == null ? null : new FrameAttackMotionSource(firstSwordMotionDocument);
                if (!edits) { edits = CreateInstance<BasicMeleeDraft>(); edits.hideFlags = HideFlags.HideAndDontSave; }
                Undo.RecordObject(edits, "Reload melee basic source"); edits.hits = hits;
                saved = Copy(hits); fileHash = hash;
                if (!FirstSwordSelected && Selected == null) selectedId = hits[0].Id;
                status = "원본을 읽었습니다. 실행 중 공격과 다음 실행 정의는 변경하지 않았습니다.";
                candidateDirty = true;
            }
            catch (Exception error) when (Expected(error)) { status = error.Message; }
            RefreshFields();
        }
        void Save()
        {
            bool wroteSource = false;
            try
            {
                if (FirstSwordSelected) throw new ArgumentException("검 1타는 Basic Sword First Hit 창에서만 편집합니다.");
                fileHash = BasicMeleeSource.Save(repository, fileHash, Draft); wroteSource = true;
                saved = Copy(Draft);
                BasicSwordSource.Regenerate(repository, python);
                string catalog = Path.GetFullPath(Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json"));
                if (catalog.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/MeleeCatalog.json");
                status = "원본 저장·문서/카탈로그 재생성 완료. 현재 전투 적용은 별도입니다.";
            }
            catch (Exception error) when (Expected(error))
            { status = (wroteSource ? "원본 저장 완료, 생성 실패: " : "저장 실패: ") + error.Message; }
            RefreshState();
        }
        void Apply(bool reset)
        {
            try
            {
                if (!EditorApplication.isPlaying || !arena) throw new ArgumentException("Play Mode의 초기화된 전투를 선택하세요.");
                if (FirstSwordSelected) throw new ArgumentException("검 1타는 기존 전용 창과 프레임 시간 검사를 사용하세요.");
                var definitions = BasicMeleeSource.Compile(Draft);
                status = arena.TryApplyBasicMelee(definitions, reset, out var error)
                    ? (reset ? "전체 편집 후보를 리셋 후 적용했습니다. 원본은 변경하지 않았습니다." : "전체 편집 후보를 다음 시작부터 적용합니다. 진행 중 공격과 원본은 유지합니다.") : error;
            }
            catch (Exception error) when (Expected(error)) { status = error.Message; }
            RefreshState();
        }

        void RefreshFields()
        {
            candidateDirty = true;
            var selector = rootVisualElement.Q<DropdownField>("hit");
            if (selector == null) return;
            var choices = new List<string> { FirstSwordChoice };
            if (Draft != null) choices.AddRange(Draft.Select(Label));
            selector.choices = choices;
            int index = Selected == null ? 0 : Array.IndexOf(Draft, Selected) + 1;
            selector.SetValueWithoutNotify(choices[index]);
            var selected = Selected;
            if (selected?.hit != null)
                for (int i = 0; i < Fields.Length; i++) rootVisualElement.Q<FloatField>(Fields[i]).SetValueWithoutNotify(Get(selected.hit, i));
            RefreshState();
        }
        void RefreshState()
        {
            if (rootVisualElement.Q<Button>("save") == null) return;
            if (EditorApplication.isPlaying && !arena)
            {
                var candidates = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None);
                if (candidates.Length == 1) arena = candidates[0];
            }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena);
            if (candidateDirty)
            {
                candidate = null; candidateError = null;
                try { candidate = BasicMeleeSource.Compile(Draft); } catch (ArgumentException error) { candidateError = error.Message; }
                candidateDirty = false;
            }
            var selected = Selected;
            bool valid = candidate != null && selected != null && !FirstSwordSelected;
            foreach (string field in Fields) rootVisualElement.Q<FloatField>(field).SetEnabled(selected != null && (field != "offsetX" || !selected.rootSymmetric));
            bool frameTime = selected != null && (arena?.GetMeleeFrame(selected.Id, true)?.Mode == MeleeTimingMode.FrameTime ||
                !string.IsNullOrEmpty(repository) && MeleeFrameSource.HasFrameOverride(repository, selected.Id));
            foreach (string field in new[] { "impact", "active", "duration" }) rootVisualElement.Q<FloatField>(field).SetEnabled(selected != null && !frameTime);
            rootVisualElement.Q<Button>("firstSword").SetEnabled(FirstSwordSelected);
            rootVisualElement.Q<Button>("save").SetEnabled(valid && !string.IsNullOrEmpty(fileHash));
            foreach (string button in new[] { "applyNext", "applyReset" })
                rootVisualElement.Q<Button>(button).SetEnabled(valid && EditorApplication.isPlaying && arena && arena.Player);
            rootVisualElement.Q<Label>("source").text = source ?? "원본 경로 없음";
            rootVisualElement.Q<Label>("sharedActive").text = selected == null ? "검 1타는 기존 전용 창으로 이동하세요. 다른 타격 편집값은 유지합니다." :
                "active_seconds 공유 — 변경 시 함께 바뀌는 타격: " + string.Join(", ", Draft.Where(hit => hit.formId == selected.formId).Select(hit => hit.Id)) +
                (selected.rootSymmetric ? "\n기존 양옆 판정: X=0 유지, radius_from_root는 박스 너비의 절반입니다." : "") +
                (frameTime ? "\nFrameTime 활성: 시간 값은 action-local 마커가 소유합니다. 공유 active_seconds 변경도 명시 고정 모드 복귀 전 거부합니다." : "");
            var definition = candidate?.FirstOrDefault(hit => hit.Id == selectedId);
            if (candidate == null) geometryPreview?.SetInvalid(candidateError);
            else if (FirstSwordSelected)
            {
                try
                {
                    firstSwordGeometry ??= BasicSwordSource.Validate(firstSwordGeometryDocument);
                    if (firstSwordGeometryMotion == null && firstSwordMotionDocument != null) firstSwordGeometryMotion = new FrameAttackMotionSource(firstSwordMotionDocument);
                    geometryPreview?.SetFirstSwordCandidate(firstSwordGeometry, firstSwordGeometryMotion, arena);
                }
                catch (ArgumentException error) { geometryPreview?.SetInvalid(error.Message); }
            }
            else geometryPreview?.SetCandidate(definition, arena: arena);
            var runtime = selected != null && arena ? arena.GetBasicMelee(selected.formId, selected.hitIndex) : null;
            int changed = ChangedCount();
            rootVisualElement.Q<Label>("state").text = FirstSwordSelected ? "검 1타 편집·저장·적용 불가 — 기존 전용 창 사용" : definition == null ? "전체 후보 적용·저장 불가: " + candidateError :
                (changed == 0 ? "저장된 원본과 동일" : "미저장 변경 " + changed + "타") + " · 선택 후보 " + Short(definition.ContentHash) +
                (runtime != null ? " · 다음 실행 " + Short(runtime.ContentHash) : "") +
                (arena && arena.Player && arena.Player.currentCast ? " · 진행 중 " + Short(arena.Player.currentCast.DefinitionVersion) : "");
            rootVisualElement.Q<Label>("status").text = status ?? "";
            var diff = new StringBuilder();
            if (Draft != null && saved != null)
                foreach (var hit in Draft)
                {
                    var before = saved.FirstOrDefault(item => item.Id == hit.Id);
                    if (before == null || hit.hit == null) continue;
                    for (int i = 0; i < Fields.Length; i++)
                        if (!Get(before.hit, i).Equals(Get(hit.hit, i)))
                            diff.AppendLine($"{hit.Id} · {rootVisualElement.Q<FloatField>(Fields[i]).label}: {Get(before.hit, i):R} → {Get(hit.hit, i):R}");
                }
            rootVisualElement.Q<Label>("diff").text = diff.Length == 0 ? "전체 원본 대비 수치 변경 없음" : diff.ToString();
        }
        int ChangedCount()
        {
            if (Draft == null || saved == null) return 0;
            return Draft.Count(hit => { var before = saved.FirstOrDefault(item => item.Id == hit.Id); return before == null || Enumerable.Range(0, Fields.Length).Any(index => !Get(before.hit, index).Equals(Get(hit.hit, index))); });
        }
        static BasicMeleeEdit[] Copy(BasicMeleeEdit[] hits) => hits.Select(hit => new BasicMeleeEdit {
            formId = hit.formId, formName = hit.formName, hitIndex = hit.hitIndex, rootSymmetric = hit.rootSymmetric,
            hit = new BasicMeleeHitData { damage = hit.hit.damage, duration = hit.hit.duration, impact = hit.hit.impact, active = hit.hit.active,
                box = new BoxData { width = hit.hit.box.width, height = hit.hit.box.height, x = hit.hit.box.x, y = hit.hit.box.y } }
        }).ToArray();
        static string Label(BasicMeleeEdit hit) => hit.formName + " · " + (hit.hitIndex + 1) + "타 — " + hit.Id;
        static string Short(string hash) => string.IsNullOrEmpty(hash) ? "없음" : hash.Substring(0, Math.Min(12, hash.Length));
        static float Get(BasicMeleeHitData hit, int index) => index switch {
            0 => hit.damage, 1 => hit.duration, 2 => hit.impact, 3 => hit.active,
            4 => hit.box.width, 5 => hit.box.height, 6 => hit.box.x, _ => hit.box.y
        };
        static void Set(BasicMeleeHitData hit, int index, float value)
        {
            switch (index) {
                case 0: hit.damage = value; break; case 1: hit.duration = value; break;
                case 2: hit.impact = value; break; case 3: hit.active = value; break;
                case 4: hit.box.width = value; break; case 5: hit.box.height = value; break;
                case 6: hit.box.x = value; break; default: hit.box.y = value; break;
            }
        }
        static bool Expected(Exception error) => error is IOException || error is ArgumentException || error is UnauthorizedAccessException || error is System.ComponentModel.Win32Exception;
    }
}
