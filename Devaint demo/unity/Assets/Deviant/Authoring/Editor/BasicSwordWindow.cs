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
    public sealed class BasicSwordWindow : EditorWindow
    {
        public const string LayoutPath = "Assets/Deviant/Authoring/Editor/BasicSwordWindow.uxml";
        static readonly string[] Fields = { "damage", "duration", "impact", "active", "width", "height", "offsetX", "offsetY" };
        [SerializeField] BasicSwordDraft edits;
        [SerializeField] BasicMeleeHitData saved;
        [SerializeField] string repository, source, fileHash, savedHash, python = "python";
        [SerializeField] CombatArena arena;
        [SerializeField] bool frameSourceMode;
        [SerializeField] AttackMotionDocument geometryMotionDocument;
        FrameAttackMotionSource geometryMotionSource;
        ActionGeometryPreview geometryPreview;
        string status;
        BasicMeleeDocument Draft => edits ? edits.document : null;

        [MenuItem("Tools/deviant/Experiments/Basic Sword First Hit")]
        public static void Open() => GetWindow<BasicSwordWindow>("Sword first hit");
        void OnEnable() { Undo.undoRedoPerformed += RefreshFields; }
        void OnDisable() { Undo.undoRedoPerformed -= RefreshFields; }
        void OnDestroy() { if (edits) DestroyImmediate(edits); }
        void OnInspectorUpdate() { RefreshState(); }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (!layout) { rootVisualElement.Add(new Label("BasicSwordWindow.uxml missing")); return; }
            layout.CloneTree(rootVisualElement);
            geometryPreview = new ActionGeometryPreview(); rootVisualElement.Q("geometryPreview").Add(geometryPreview);
            var target = rootVisualElement.Q<ObjectField>("runtime"); target.objectType = typeof(CombatArena); target.allowSceneObjects = true;
            target.RegisterValueChangedCallback(e => { arena = e.newValue as CombatArena; RefreshState(); });
            for (int i = 0; i < Fields.Length; i++)
            {
                int index = i;
                rootVisualElement.Q<FloatField>(Fields[i]).RegisterValueChangedCallback(e => {
                    if (Draft?.hit == null) return;
                    Undo.RecordObject(edits, "Edit sword first hit"); Set(Draft.hit, index, e.newValue); status = null; RefreshState();
                });
            }
            rootVisualElement.Q<TextField>("python").SetValueWithoutNotify(python);
            rootVisualElement.Q<TextField>("python").RegisterValueChangedCallback(e => python = e.newValue);
            rootVisualElement.Q<Button>("reload").clicked += Reload;
            rootVisualElement.Q<Button>("save").clicked += Save;
            rootVisualElement.Q<Button>("applyNext").clicked += () => Apply(false);
            rootVisualElement.Q<Button>("applyReset").clicked += () => Apply(true);
            if (Draft == null)
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "data/combat/actions/base_sword_basic_0.json"))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName);
                else status = "검 1타 원본을 찾지 못했습니다.";
            }
            RefreshFields();
        }

        public void LoadRepository(string path)
        { repository = path; source = Path.Combine(path, "data/combat/actions/base_sword_basic_0.json"); Reload(); }

        void Reload()
        {
            try
            {
                var document = BasicSwordSource.Load(source, out var hash);
                bool usesFrameTime = ReadFrameSourceMode();
                if (!edits) { edits = CreateInstance<BasicSwordDraft>(); edits.hideFlags = HideFlags.HideAndDontSave; }
                Undo.RecordObject(edits, "Reload sword source"); edits.document = document;
                var definition = BasicSwordSource.Validate(document);
                fileHash = hash; savedHash = definition.ContentHash; saved = BasicMeleeHitData.From(definition);
                frameSourceMode = usesFrameTime;
                status = "원본을 읽었습니다. 실행 중 공격은 변경하지 않았습니다.";
            }
            catch (Exception e) when (Expected(e)) { status = e.Message; }
            RefreshFields();
        }

        void Save()
        {
            bool wroteSource = false;
            try
            {
                frameSourceMode = ReadFrameSourceMode();
                if (frameSourceMode && saved != null && (Draft.hit.duration != saved.duration || Draft.hit.impact != saved.impact || Draft.hit.active != saved.active))
                    throw new ArgumentException("영구 프레임 모드의 타격·종료 시각은 Frame Timing Preview에서 편집하세요. 수치 원본을 재로드하면 피해·형상만 저장할 수 있습니다.");
                fileHash = BasicSwordSource.Save(repository, fileHash, Draft); wroteSource = true;
                var definition = BasicSwordSource.Validate(Draft);
                savedHash = definition.ContentHash; saved = BasicMeleeHitData.From(definition);
                BasicSwordSource.Regenerate(repository, python);
                string catalog = Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json");
                if (Path.GetFullPath(catalog).StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/MeleeCatalog.json");
                status = "원본 저장·문서/카탈로그 재생성 완료. 런타임 적용은 별도입니다.";
            }
            catch (Exception e) when (Expected(e))
            { status = (wroteSource ? "원본 저장 완료, 생성 실패: " : "저장 실패: ") + e.Message; }
            RefreshState();
        }

        void Apply(bool reset)
        {
            if (!EditorApplication.isPlaying || !arena || Draft == null) return;
            status = arena.TryApplyBasicSwordFirst(Draft.hit.ToData(), reset, out var error)
                ? (reset ? "리셋 적용 완료. 원본은 변경하지 않았습니다." : "다음 1타부터 적용합니다. 진행 중 공격과 원본은 유지합니다.") : error;
            RefreshState();
        }

        void RefreshFields()
        {
            if (Draft?.hit != null && rootVisualElement.Q<FloatField>(Fields[0]) != null)
                for (int i = 0; i < Fields.Length; i++) rootVisualElement.Q<FloatField>(Fields[i]).SetValueWithoutNotify(Get(Draft.hit, i));
            RefreshState();
        }
        bool ReadFrameSourceMode()
        {
            string path = Path.Combine(repository, AttackMotionSource.RelativePath);
            geometryMotionDocument = File.Exists(path) ? AttackMotionSource.Load(path, out _) : null;
            geometryMotionSource = geometryMotionDocument == null ? null : new FrameAttackMotionSource(geometryMotionDocument);
            return geometryMotionSource?.UsesFrameTime == true;
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
            BasicMeleeDefinition definition = null; string error = null;
            try { definition = BasicSwordSource.Validate(Draft); } catch (ArgumentException e) { error = e.Message; }
            bool valid = definition != null;
            if (!valid) geometryPreview?.SetInvalid(error);
            else
            {
                try
                {
                    if (geometryMotionSource == null && geometryMotionDocument != null) geometryMotionSource = new FrameAttackMotionSource(geometryMotionDocument);
                    geometryPreview?.SetFirstSwordCandidate(definition, geometryMotionSource, arena);
                }
                catch (ArgumentException exception) { geometryPreview?.SetInvalid(exception.Message); }
            }
            foreach (string field in new[] { "duration", "impact", "active" }) rootVisualElement.Q<FloatField>(field).SetEnabled(!frameSourceMode);
            rootVisualElement.Q<Button>("save").SetEnabled(valid && !string.IsNullOrEmpty(fileHash));
            foreach (string button in new[] { "applyNext", "applyReset" })
                rootVisualElement.Q<Button>(button).SetEnabled(valid && EditorApplication.isPlaying && arena && arena.Player && arena.FrameSwordTiming == null);
            rootVisualElement.Q<Label>("source").text = source ?? "원본 경로 없음";
            rootVisualElement.Q<Label>("state").text = valid
                ? (definition.ContentHash == savedHash ? "저장된 원본과 동일" : "미저장 변경") + " · 편집 " + definition.ContentHash.Substring(0, 12) +
                    (arena && arena.BasicSwordFirst != null ? " · 실행 " + arena.BasicSwordFirst.ContentHash.Substring(0, 12) : "")
                : "적용·저장 불가: " + error;
            rootVisualElement.Q<Label>("status").text = (frameSourceMode ? "영구 프레임 기준 원본: 타격·활성·종료는 Frame Timing Preview가 소유합니다. 이 창의 시간 값은 고정 모드 복귀용 기준값(읽기 전용)입니다.\n" : "") +
                (arena && arena.FrameSwordTiming != null ? "프레임 기준 실행 중: 별도 수치의 런타임 적용은 Frame Timing Preview에서 고정 실행으로 복귀한 뒤 사용하세요.\n" : "") + (status ?? "");
            var diff = new StringBuilder();
            if (saved != null && Draft?.hit != null)
                for (int i = 0; i < Fields.Length; i++)
                    if (!Get(saved, i).Equals(Get(Draft.hit, i)))
                        diff.AppendLine($"{rootVisualElement.Q<FloatField>(Fields[i]).label}: {Get(saved, i):R} → {Get(Draft.hit, i):R}");
            rootVisualElement.Q<Label>("diff").text = diff.Length == 0 ? "원본 대비 수치 변경 없음" : diff.ToString();
        }

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
        static bool Expected(Exception e) => e is IOException || e is ArgumentException || e is UnauthorizedAccessException || e is System.ComponentModel.Win32Exception;
    }
}
