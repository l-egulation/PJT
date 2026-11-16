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
    public sealed class ChargedMeleeWindow : EditorWindow
    {
        static readonly string[] Fields = { "minimum", "maximum", "minimumDamage", "maximumDamage", "active", "baseEnd", "width", "height", "offsetX", "offsetY" };
        [SerializeField] ChargedMeleeDraft draft;
        [SerializeField] CombatArena arena;
        [SerializeField] string repository, fileHash, savedVersion, python = "python";
        [SerializeField] int selected;
        [SerializeField] float previewRelease = .2f;
        ActionGeometryPreview preview;
        string status = "";
        [MenuItem("Tools/deviant/Experiments/Charged Melee Specials")]
        public static void Open() => GetWindow<ChargedMeleeWindow>("Charged melee specials");
        void OnEnable() => Undo.undoRedoPerformed += RefreshFields;
        void OnDisable() => Undo.undoRedoPerformed -= RefreshFields;
        void OnDestroy() { if (draft) DestroyImmediate(draft); }
        void OnInspectorUpdate() => RefreshState();
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var scroll = new ScrollView(); rootVisualElement.Add(scroll); scroll.Add(new Label("기존 차지 근접 2종 · ChargeExecution 정책 유지"));
            var target = new ObjectField("Game / Lab 전투") { name = "runtime", objectType = typeof(CombatArena), allowSceneObjects = true };
            target.RegisterValueChangedCallback(change => { arena = change.newValue as CombatArena; RefreshState(); }); scroll.Add(target);
            scroll.Add(new Button(Reload) { name = "reload", text = "현재 원본 다시 읽기 · 편집 폐기" });
            foreach (string name in new[] { "source", "state", "status", "policy" }) scroll.Add(Wrapped(name));
            var actions = new DropdownField("행동", ChargedMeleeDefinition.SupportedIds.ToList(), Mathf.Clamp(selected, 0, ChargedMeleeDefinition.SupportedIds.Count - 1)) { name = "action" };
            actions.RegisterValueChangedCallback(_ => { selected = actions.index; RefreshFields(); }); scroll.Add(actions);
            foreach (string name in Fields) {
                string captured = name; var field = new FloatField(name) { name = name };
                field.RegisterValueChangedCallback(change => { if (!draft || selected < 0 || selected >= draft.numerics.Length) return;
                    Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Edit charged melee values"); Set(draft.numerics[selected], captured, change.newValue); status = ""; RefreshState(); }); scroll.Add(field);
            }
            var release = new FloatField("미리보기 해제 표본 (읽기 전용 시뮬레이션 입력, 원본/전투 변경 없음)") { name = "previewRelease" }; release.SetValueWithoutNotify(previewRelease);
            release.RegisterValueChangedCallback(change => { previewRelease = change.newValue; RefreshState(); }); scroll.Add(release);
            scroll.Add(Wrapped("resolved")); scroll.Add(Wrapped("diff")); preview = new ActionGeometryPreview(); scroll.Add(preview);
            scroll.Add(new Button(() => Apply(false)) { name = "applyNext", text = "다음 시작부터 전체 차지 후보 임시 적용" });
            scroll.Add(new Button(() => Apply(true)) { name = "applyReset", text = "리셋 후 전체 차지 후보 임시 적용" });
            var executable = new TextField("Python 실행 파일") { name = "python", value = python }; executable.RegisterValueChangedCallback(change => python = change.newValue); scroll.Add(executable);
            scroll.Add(new Button(() => FileAction(() => SavePermanentSource(python))) { name = "saveSource", text = "원본 저장 · 문서/카탈로그 재생성" });
            scroll.Add(new Button(() => FileAction(() => { BasicSwordSource.Regenerate(repository, python); ImportCatalog(); status = "저장된 원본 재생성 완료."; })) { name = "regenerate", text = "저장된 원본 재생성 재시도" });
            if (!draft) { var directory = new DirectoryInfo(Application.dataPath); while (directory != null && !File.Exists(Path.Combine(directory.FullName, ChargedMeleeSource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName); else status = "Canonical melee source를 찾지 못했습니다."; }
            RefreshFields();
        }
        static Label Wrapped(string name) { var label = new Label { name = name }; label.style.whiteSpace = WhiteSpace.Normal; label.style.marginBottom = 6; return label; }
        public void LoadRepository(string path) { repository = path; Reload(); }
        public void SetRuntime(CombatArena value) { arena = value; RefreshState(); }
        void Reload()
        {
            FileAction(() => { var values = ChargedMeleeSource.Load(repository, out var hash);
                if (!draft) { draft = CreateInstance<ChargedMeleeDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; } else { Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Reload charged melee values"); }
                draft.Load(values); fileHash = hash; savedVersion = Version(values); status = "현재 원본을 읽었습니다. 적용·저장과 별도입니다."; RefreshFields(); });
        }
        static string Version(ChargedMeleeDefinition[] values) => string.Join("/", values.Select(value => value.ContentHash));
        public ChargedMeleeDefinition[] CompileCandidate() => draft ? draft.Compile() : throw new ArgumentException("원본을 먼저 읽으세요.");
        public bool ApplyCandidate(bool reset, out string error)
        {
            if (!EditorApplication.isPlaying || !arena) { error = "초기화된 Game/Lab 전투를 선택하세요."; return false; }
            try { return arena.TryApplyChargedMeleeDefinitions(CompileCandidate(), reset, out error); } catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        void Apply(bool reset) { status = ApplyCandidate(reset, out var error) ? "전체 차지 후보를 게시했습니다. 진행 중 차지/실행/매체·자원과 원본은 보존합니다." : error; RefreshState(); }
        public string SavePermanentSource(string executable = "python")
        {
            var values = CompileCandidate(); bool saved = false;
            try { fileHash = ChargedMeleeSource.Save(repository, fileHash, values); saved = true; savedVersion = Version(values); BasicSwordSource.Regenerate(repository, executable); ImportCatalog();
                status = "원본 저장·문서/카탈로그 재생성 완료. 현재 전투 적용은 별도입니다."; RefreshState(); return fileHash; }
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
            if (rootVisualElement.Q<DropdownField>("action") == null) return; if (draft?.sources == null || draft.sources.Length == 0) { RefreshState(); return; }
            selected = Mathf.Clamp(selected, 0, draft.sources.Length - 1); var selector = rootVisualElement.Q<DropdownField>("action"); selector.choices = draft.sources.Select(value => value.id).ToList(); selector.SetValueWithoutNotify(selector.choices[selected]);
            foreach (string name in Fields) rootVisualElement.Q<FloatField>(name).SetValueWithoutNotify(Get(draft.numerics[selected], name)); RefreshState();
        }
        void RefreshState()
        {
            var label = rootVisualElement.Q<Label>("state"); if (label == null) return;
            if (!arena && EditorApplication.isPlaying) { var values = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None); if (values.Length == 1) arena = values[0]; }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena); rootVisualElement.Q<Label>("source").text = string.IsNullOrEmpty(repository) ? "원본 없음" : Path.Combine(repository, ChargedMeleeSource.RelativePath); rootVisualElement.Q<Label>("status").text = status;
            foreach (string button in new[] { "applyNext", "applyReset", "saveSource" }) rootVisualElement.Q<Button>(button).SetEnabled(false);
            try {
                var values = CompileCandidate(); selected = Mathf.Clamp(selected, 0, values.Length - 1); var current = values[selected]; var numeric = current.ToNumericData(); var release = current.Policy.Resolve(previewRelease);
                preview?.SetCandidate(current, previewRelease, arena);
                label.text = (savedVersion == Version(values) ? "원본과 동일" : "차지 수치 편집 · 미저장") + "\n후보 " + current.ContentHash + (arena ? "\n다음 시작 " + arena.GetChargedMelee(current.Id)?.ContentHash : "") + (arena && arena.Player && arena.Player.currentCast ? "\n진행 중 " + arena.Player.currentCast.DefinitionVersion : "");
                rootVisualElement.Q<Label>("policy").text = $"{current.FormId} · 범위 전체 타격·고정 방향·수평0/정상 중력·기본 공속 미적용\n쿨타임 {current.Cooldown:R}/용량 {current.Capacity} 읽기 전용 · 대상 없이 발동·자원 재검사·자원 실패0.10초 정책 유지";
                rootVisualElement.Q<Label>("resolved").text = $"같은 ChargeDefinition.Resolve: release={release.ReleaseTime:R} shift={release.Shift:R} damage={release.Damage:R} end={release.MotionEnd:R}\n회복 주석={numeric.baseEnd - (numeric.minimum + numeric.active):R} · 최대 종료 주석={current.Policy.Resolve(current.Policy.Maximum).MotionEnd:R} (읽기 전용)";
                var before = draft.sources[selected]; var text = new StringBuilder();
                if (before.chargeMin != numeric.minimum || before.chargeMax != numeric.maximum) text.AppendLine($"유지 [{before.chargeMin:R},{before.chargeMax:R}] → [{numeric.minimum:R},{numeric.maximum:R}]");
                if (before.chargeMinDamage != numeric.minimumDamage || before.chargeMaxDamage != numeric.maximumDamage) text.AppendLine($"피해 [{before.chargeMinDamage:R},{before.chargeMaxDamage:R}] → [{numeric.minimumDamage:R},{numeric.maximumDamage:R}]");
                if (before.hits[0].active != numeric.active || before.end != numeric.baseEnd) text.AppendLine($"활성/기본 종료 {before.hits[0].active:R}/{before.end:R} → {numeric.active:R}/{numeric.baseEnd:R}");
                rootVisualElement.Q<Label>("diff").text = text.Length == 0 ? "정책/원본 대비 차지·시간 변경 없음 (형상은 미리보기/해시 참조)" : text.ToString();
                bool runtime = EditorApplication.isPlaying && arena && arena.Player; rootVisualElement.Q<Button>("applyNext").SetEnabled(runtime); rootVisualElement.Q<Button>("applyReset").SetEnabled(runtime); rootVisualElement.Q<Button>("saveSource").SetEnabled(!string.IsNullOrEmpty(fileHash));
            }
            catch (ArgumentException exception) { label.text = "전체 후보 무효 — 이전 실행 버전 보존: " + exception.Message; preview?.SetInvalid(exception.Message); rootVisualElement.Q<Label>("resolved").text = ""; }
        }
        static float Get(ChargedMeleeNumericData value, string name)
        {
            switch (name) { case "minimum": return value.minimum; case "maximum": return value.maximum; case "minimumDamage": return value.minimumDamage; case "maximumDamage": return value.maximumDamage; case "active": return value.active; case "baseEnd": return value.baseEnd; case "width": return value.box.width; case "height": return value.box.height; case "offsetX": return value.box.x; default: return value.box.y; }
        }
        static void Set(ChargedMeleeNumericData value, string name, float next)
        {
            switch (name) { case "minimum": value.minimum = next; break; case "maximum": value.maximum = next; break; case "minimumDamage": value.minimumDamage = next; break; case "maximumDamage": value.maximumDamage = next; break; case "active": value.active = next; break; case "baseEnd": value.baseEnd = next; break; case "width": value.box.width = next; break; case "height": value.box.height = next; break; case "offsetX": value.box.x = next; break; default: value.box.y = next; break; }
        }
    }
}
