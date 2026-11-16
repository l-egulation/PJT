using System;
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
    public sealed class DirectMeleeSpecialWindow : EditorWindow
    {
        static readonly string[] Fields = { "damage", "impact", "active", "duration", "width", "height", "offsetX", "offsetY" };
        [SerializeField] DirectMeleeSpecialDraft draft;
        [SerializeField] CombatArena arena;
        [SerializeField] string repository, fileHash, savedVersion, python = "python";
        [SerializeField] int selected;
        ActionGeometryPreview preview;
        string status = "";
        [MenuItem("Tools/deviant/Experiments/Direct Melee Specials")]
        public static void Open() => GetWindow<DirectMeleeSpecialWindow>("Direct melee specials");
        void OnEnable() => Undo.undoRedoPerformed += RefreshFields;
        void OnDisable() => Undo.undoRedoPerformed -= RefreshFields;
        void OnDestroy() { if (draft) DestroyImmediate(draft); }
        void OnInspectorUpdate() => RefreshState();
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(new Label("기존 고정 direct 특수 3종 · 수치 편집"));
            var target = new ObjectField("Game / Lab 전투") { name = "runtime", objectType = typeof(CombatArena), allowSceneObjects = true };
            target.RegisterValueChangedCallback(change => { arena = change.newValue as CombatArena; RefreshState(); }); scroll.Add(target);
            scroll.Add(new Button(Reload) { name = "reload", text = "현재 원본 다시 읽기 · 편집 폐기" });
            scroll.Add(Wrapped("source")); scroll.Add(Wrapped("state")); scroll.Add(Wrapped("status"));
            var selector = new DropdownField("행동", DirectMeleeSpecialDefinition.SupportedIds.ToList(), Mathf.Clamp(selected, 0, DirectMeleeSpecialDefinition.SupportedIds.Count - 1)) { name = "action" };
            selector.RegisterValueChangedCallback(_ => { selected = selector.index; RefreshFields(); }); scroll.Add(selector);
            scroll.Add(Wrapped("policy"));
            foreach (string name in Fields)
            {
                string captured = name; var field = new FloatField(name) { name = name };
                field.RegisterValueChangedCallback(change => { if (!draft || selected < 0 || selected >= draft.numerics.Length) return;
                    Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Edit direct melee numeric values"); Set(draft.numerics[selected], captured, change.newValue); status = ""; RefreshState(); }); scroll.Add(field);
            }
            scroll.Add(Wrapped("diff")); preview = new ActionGeometryPreview(); scroll.Add(preview);
            scroll.Add(new Button(() => Apply(false)) { name = "applyNext", text = "다음 시작부터 전체 편집 후보 임시 적용" });
            scroll.Add(new Button(() => Apply(true)) { name = "applyReset", text = "리셋 후 전체 편집 후보 임시 적용" });
            var executable = new TextField("Python 실행 파일") { name = "python", value = python }; executable.RegisterValueChangedCallback(change => python = change.newValue); scroll.Add(executable);
            scroll.Add(new Button(() => FileAction(() => SavePermanentSource(python))) { name = "saveSource", text = "원본 저장 · 문서/카탈로그 재생성" });
            scroll.Add(new Button(() => FileAction(() => { BasicSwordSource.Regenerate(repository, python); ImportCatalog(); status = "저장된 원본 재생성 완료."; })) { name = "regenerate", text = "저장된 원본 재생성 재시도" });
            if (!draft) {
                var directory = new DirectoryInfo(Application.dataPath); while (directory != null && !File.Exists(Path.Combine(directory.FullName, DirectMeleeSpecialSource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName); else status = "Canonical melee source를 찾지 못했습니다.";
            }
            RefreshFields();
        }
        static Label Wrapped(string name) { var label = new Label { name = name }; label.style.whiteSpace = WhiteSpace.Normal; label.style.marginBottom = 6; return label; }
        public void LoadRepository(string path) { repository = path; Reload(); }
        public void SetRuntime(CombatArena value) { arena = value; RefreshState(); }
        void Reload()
        {
            FileAction(() => {
                var values = DirectMeleeSpecialSource.Load(repository, out var hash);
                if (!draft) { draft = CreateInstance<DirectMeleeSpecialDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; }
                else { Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Reload direct melee numeric source"); }
                draft.Load(values); fileHash = hash; savedVersion = Version(values); status = "현재 원본을 읽었습니다. 전투 적용·저장과 별도입니다."; RefreshFields();
            });
        }
        static string Version(DirectMeleeSpecialDefinition[] values) => string.Join("/", values.Select(value => value.ContentHash));
        public DirectMeleeSpecialDefinition[] CompileCandidate() => draft ? draft.Compile() : throw new ArgumentException("원본을 먼저 읽으세요.");
        public bool ApplyCandidate(bool reset, out string error)
        {
            if (!EditorApplication.isPlaying || !arena) { error = "초기화된 Game/Lab 전투를 선택하세요."; return false; }
            try { return arena.TryApplyDirectMeleeSpecials(CompileCandidate(), reset, out error); }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        void Apply(bool reset) { status = ApplyCandidate(reset, out var error) ? "수치 후보를 게시했습니다. 진행 중 캐스트/본체 매체·자원과 원본 파일은 보존합니다." : error; RefreshState(); }
        public string SavePermanentSource(string executable = "python")
        {
            var candidate = CompileCandidate(); bool saved = false;
            try {
                fileHash = DirectMeleeSpecialSource.Save(repository, fileHash, candidate); saved = true; savedVersion = Version(candidate);
                BasicSwordSource.Regenerate(repository, executable); ImportCatalog(); status = "원본 저장·문서/카탈로그 재생성 완료. 새 초기화/빌드에서 사용하며 현재 전투 적용은 별도입니다."; RefreshState(); return fileHash;
            }
            catch (Exception exception) when (Expected(exception)) { status = (saved ? "원본 저장 완료, 생성 실패: " : "원본 저장 실패: ") + exception.Message; RefreshState(); throw; }
        }
        void ImportCatalog()
        {
            string path = Path.GetFullPath(Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json"));
            if (path.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/MeleeCatalog.json");
        }
        static bool Expected(Exception exception) => exception is ArgumentException || exception is InvalidOperationException || exception is IOException || exception is UnauthorizedAccessException || exception is System.ComponentModel.Win32Exception;
        void FileAction(Action action) { status = ""; try { action(); } catch (Exception exception) when (Expected(exception)) { if (string.IsNullOrEmpty(status)) status = exception.Message; } RefreshState(); }
        void RefreshFields()
        {
            if (rootVisualElement.Q<DropdownField>("action") == null) return;
            if (draft?.sources == null || draft.sources.Length == 0) { RefreshState(); return; }
            selected = Mathf.Clamp(selected, 0, draft.sources.Length - 1); var selector = rootVisualElement.Q<DropdownField>("action"); selector.choices = draft.sources.Select(value => value.id).ToList(); selector.SetValueWithoutNotify(selector.choices[selected]);
            foreach (string name in Fields) rootVisualElement.Q<FloatField>(name).SetValueWithoutNotify(Get(draft.numerics[selected], name));
            RefreshState();
        }
        void RefreshState()
        {
            var label = rootVisualElement.Q<Label>("state"); if (label == null) return;
            if (!arena && EditorApplication.isPlaying) { var values = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None); if (values.Length == 1) arena = values[0]; }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena); rootVisualElement.Q<Label>("source").text = string.IsNullOrEmpty(repository) ? "원본 없음" : Path.Combine(repository, DirectMeleeSpecialSource.RelativePath);
            rootVisualElement.Q<Label>("status").text = status;
            foreach (string name in new[] { "applyNext", "applyReset", "saveSource" }) rootVisualElement.Q<Button>(name).SetEnabled(false);
            try {
                var values = CompileCandidate(); selected = Mathf.Clamp(selected, 0, values.Length - 1); var current = values[selected];
                preview?.SetCandidate(current, arena);
                label.text = (savedVersion == Version(values) ? "원본과 동일" : "수치 편집 · 미저장") + "\n후보 " + current.ContentHash +
                    (arena ? "\n다음 시작 " + arena.GetDirectMeleeSpecial(current.Id)?.ContentHash : "") + (arena && arena.Player && arena.Player.currentCast ? "\n진행 중 " + arena.Player.currentCast.DefinitionVersion : "");
                rootVisualElement.Q<Label>("policy").text = $"{current.FormId} · 기존 actor/all/방향 잠금·정상 중력·수평 입력0·기본 공속 미적용\n쿨타임 {current.Cooldown:R} · 용량 {current.Capacity} (Equipment 소유/읽기 전용)" + (current.Id == "greatsword_range" ? "\n기존 양옆 판정: offsetX=0 고정" : "");
                rootVisualElement.Q<FloatField>("offsetX").SetEnabled(current.Id != "greatsword_range");
                bool frameTime = arena?.GetMeleeFrame(current.Id, false)?.Mode == MeleeTimingMode.FrameTime ||
                    !string.IsNullOrEmpty(repository) && MeleeFrameSource.HasFrameOverride(repository, current.Id);
                foreach (string field in new[] { "impact", "active", "duration" }) rootVisualElement.Q<FloatField>(field).SetEnabled(!frameTime);
                if (frameTime) rootVisualElement.Q<Label>("policy").text += "\nFrameTime 활성: 접촉/활성/끝은 프레임 마커 전용입니다. 피해/형상은 기존 원본입니다.";
                var baseline = draft.sources[selected].hits[0]; var data = current.ToBasicData(); var diff = new StringBuilder();
                if (baseline.damage != data.damage) diff.AppendLine($"피해 {baseline.damage:R} → {data.damage:R}");
                if (baseline.time != data.impact || baseline.active != data.active || draft.sources[selected].end != data.duration) diff.AppendLine($"시간 contact/active/end {baseline.time:R}/{baseline.active:R}/{draft.sources[selected].end:R} → {data.impact:R}/{data.active:R}/{data.duration:R}");
                if (baseline.box.width != data.box.width || baseline.box.height != data.box.height || baseline.box.x != data.box.x || baseline.box.y != data.box.y) diff.AppendLine($"박스 ({baseline.box.width:R},{baseline.box.height:R})@({baseline.box.x:R},{baseline.box.y:R}) → ({data.box.width:R},{data.box.height:R})@({data.box.x:R},{data.box.y:R})");
                rootVisualElement.Q<Label>("diff").text = diff.Length == 0 ? "선택 행동 원본 대비 변경 없음" : diff.ToString();
                bool runtime = EditorApplication.isPlaying && arena && arena.Player; rootVisualElement.Q<Button>("applyNext").SetEnabled(runtime); rootVisualElement.Q<Button>("applyReset").SetEnabled(runtime); rootVisualElement.Q<Button>("saveSource").SetEnabled(!string.IsNullOrEmpty(fileHash));
            }
            catch (ArgumentException exception) { label.text = "전체 후보 무효 — 이전 실행 버전 보존: " + exception.Message; preview?.SetInvalid(exception.Message); rootVisualElement.Q<Label>("diff").text = ""; }
        }
        static float Get(BasicMeleeHitData hit, string name)
        {
            switch (name) { case "damage": return hit.damage; case "impact": return hit.impact; case "active": return hit.active; case "duration": return hit.duration; case "width": return hit.box.width; case "height": return hit.box.height; case "offsetX": return hit.box.x; default: return hit.box.y; }
        }
        static void Set(BasicMeleeHitData hit, string name, float value)
        {
            switch (name) { case "damage": hit.damage = value; break; case "impact": hit.impact = value; break; case "active": hit.active = value; break; case "duration": hit.duration = value; break; case "width": hit.box.width = value; break; case "height": hit.box.height = value; break; case "offsetX": hit.box.x = value; break; default: hit.box.y = value; break; }
        }
    }
}
