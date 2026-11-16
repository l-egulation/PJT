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
    public sealed class AuthoredSkillWindow : EditorWindow
    {
        [SerializeField] AuthoredSkillDraft draft;
        [SerializeField] CombatArena arena;
        [SerializeField] string repository, fileHash, savedVersion, python = "python";
        [SerializeField] int selected;
        [SerializeField] float previewTime;
        string status = "";
        ActionGeometryPreview geometry;
        [SerializeField] int selectedHit;
        [MenuItem("Tools/deviant/Experiments/Generic Melee Specials")]
        public static void Open() => GetWindow<AuthoredSkillWindow>("Generic melee specials");
        void OnEnable() => Undo.undoRedoPerformed += RefreshFields;
        void OnDisable() => Undo.undoRedoPerformed -= RefreshFields;
        void OnDestroy() { if (draft) DestroyImmediate(draft); }
        void OnInspectorUpdate() => RefreshState();
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var scroll = new ScrollView(); rootVisualElement.Add(scroll); scroll.Add(new Label("기존 근접 특수 19종 · 고정 정책/전체 수치 후보"));
            var target = new ObjectField("Game / Lab 전투") { name = "runtime", objectType = typeof(CombatArena), allowSceneObjects = true }; target.RegisterValueChangedCallback(change => { arena = change.newValue as CombatArena; RefreshState(); }); scroll.Add(target);
            scroll.Add(new Button(Reload) { name = "reload", text = "현재 원본 다시 읽기 · 편집 폐기" });
            foreach (string name in new[] { "source", "state", "status", "policy" }) scroll.Add(Label(name));
            var actions = new DropdownField("행동", AuthoredSkillDefinition.SupportedIds.ToList(), Mathf.Clamp(selected, 0, AuthoredSkillDefinition.SupportedIds.Count - 1)) { name = "action" };
            actions.RegisterValueChangedCallback(_ => { selected = actions.index; RefreshFields(); }); scroll.Add(actions); scroll.Add(new VisualElement { name = "fields" });
            var time = new FloatField("읽기 전용 시간 표본") { name = "previewTime", value = previewTime }; time.RegisterValueChangedCallback(change => { previewTime = change.newValue; RefreshState(); }); scroll.Add(time);
            var hits = new DropdownField("공간 미리보기 타격") { name = "hit" }; hits.RegisterValueChangedCallback(_ => { selectedHit = hits.index; RefreshState(); }); scroll.Add(hits);
            geometry = new ActionGeometryPreview(); scroll.Add(geometry);
            scroll.Add(Label("preview")); scroll.Add(new Button(() => Apply(false)) { name = "applyNext", text = "다음 시작부터 전체 편집 후보 임시 적용" });
            scroll.Add(new Button(() => Apply(true)) { name = "applyReset", text = "리셋 후 전체 편집 후보 임시 적용" });
            var executable = new TextField("Python 실행 파일") { name = "python", value = python }; executable.RegisterValueChangedCallback(change => python = change.newValue); scroll.Add(executable);
            scroll.Add(new Button(() => FileAction(() => SavePermanentSource(python))) { name = "saveSource", text = "원본 저장 · 문서/카탈로그 재생성" });
            scroll.Add(new Button(() => FileAction(() => { BasicSwordSource.Regenerate(repository, python); status = "저장된 원본 재생성 완료."; })) { name = "regenerate", text = "저장된 원본 재생성 재시도" });
            if (!draft) { var directory = new DirectoryInfo(Application.dataPath); while (directory != null && !File.Exists(Path.Combine(directory.FullName, AuthoredSkillSource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName); else status = "Canonical melee source를 찾지 못했습니다."; }
            RefreshFields();
        }
        static Label Label(string name) { var label = new Label { name = name }; label.style.whiteSpace = WhiteSpace.Normal; label.style.marginBottom = 6; return label; }
        public void LoadRepository(string path) { repository = path; Reload(); }
        public void SetRuntime(CombatArena value) { arena = value; RefreshState(); }
        void Reload()
        {
            FileAction(() => { var values = AuthoredSkillSource.Load(repository, out var hash); if (!draft) { draft = CreateInstance<AuthoredSkillDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; }
                else { Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Reload generic melee source"); }
                draft.Load(values); fileHash = hash; savedVersion = Version(values); status = "현재 원본을 읽었습니다. 적용/저장과 별도입니다."; RefreshFields(); });
        }
        static string Version(AuthoredSkillDefinition[] values) => string.Join("/", values.Select(value => value.ContentHash));
        void Field(VisualElement parent, string name, float value, Action<float> change, bool editable = true)
        {
            var field = new FloatField(name) { name = name }; field.SetValueWithoutNotify(value); field.SetEnabled(editable);
            field.RegisterValueChangedCallback(next => { Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Edit generic melee numeric values"); change(next.newValue); status = ""; RefreshState(); }); parent.Add(field);
        }
        void Geometry(VisualElement parent, string prefix, BoxData box, bool editable, bool symmetric = false)
        {
            if (box == null) { parent.Add(new Label(prefix + " geometry 없음 (정책 고정)")); return; }
            Field(parent, prefix + "-width", box.width, value => box.width = value, editable); Field(parent, prefix + "-height", box.height, value => box.height = value, editable);
            Field(parent, prefix + "-x", box.x, value => box.x = value, editable && !symmetric); Field(parent, prefix + "-y", box.y, value => box.y = value, editable);
        }
        void RefreshFields()
        {
            var fields = rootVisualElement.Q("fields"); if (fields == null) return; fields.Clear();
            if (draft?.numerics == null || draft.numerics.Length == 0) { RefreshState(); return; }
            selected = Mathf.Clamp(selected, 0, draft.numerics.Length - 1); var selector = rootVisualElement.Q<DropdownField>("action"); selector.choices = draft.sources.Select(value => value.id).ToList(); selector.SetValueWithoutNotify(selector.choices[selected]);
            var policy = draft.Source(selected); var numeric = draft.numerics[selected];
            bool frameTime = !string.IsNullOrEmpty(repository) && SpecialFrameSource.HasFrameOverride(repository, policy.Id) || arena && arena.GetSpecialFrame(policy.Id)?.UsesFrameTime == true;
            var hitSelector = rootVisualElement.Q<DropdownField>("hit"); hitSelector.choices = numeric.hits.Length == 0 ? new System.Collections.Generic.List<string> { "effect" } : Enumerable.Range(0, numeric.hits.Length).Select(index => "hit-" + index).ToList();
            selectedHit = Mathf.Clamp(selectedHit, 0, hitSelector.choices.Count - 1); hitSelector.SetValueWithoutNotify(hitSelector.choices[selectedHit]);
            Field(fields, "activation", numeric.activation, value => numeric.activation = value, !frameTime && policy.Id != "shield_default"); Field(fields, "end", numeric.end, value => numeric.end = value, !frameTime);
            for (int index = 0; index < numeric.hits.Length; index++) {
                int hitIndex = index; var hit = numeric.hits[index]; fields.Add(new Label("hit[" + index + "] " + policy.Execution.Hits[index].Anchor + "/" + policy.Execution.Hits[index].Targets));
                Field(fields, "hit-" + index + "-time", hit.time, value => hit.time = value, !frameTime); Field(fields, "hit-" + index + "-active", hit.active, value => hit.active = value, !frameTime); Field(fields, "hit-" + index + "-damage", hit.damage, value => hit.damage = value);
                Field(fields, "hit-" + index + "-root", hit.root, value => hit.root = value, policy.CanEdit("hit", "root", hitIndex)); Field(fields, "hit-" + index + "-stun", hit.stun, value => hit.stun = value, policy.CanEdit("hit", "stun", hitIndex));
                Field(fields, "hit-" + index + "-push", hit.push, value => hit.push = value, policy.CanEdit("hit", "push", hitIndex)); Field(fields, "hit-" + index + "-pushSeconds", hit.pushSeconds, value => hit.pushSeconds = value, policy.CanEdit("hit", "pushSeconds", hitIndex));
                Geometry(fields, "hit-" + index + "-box", hit.box, policy.CanEdit("hit", "box", hitIndex), policy.Id == "hammer_range" || policy.Id == "hammer_follow_up" || policy.Id == "greatsword_movement");
            }
            if (numeric.hasMovement) {
                var move = numeric.movement; fields.Add(new Label("movement " + policy.Execution.Movement.Type + "/" + policy.Execution.Movement.WallPolicy));
                Field(fields, "move-start", move.start, value => move.start = value, policy.CanEdit("movement", "start") && !frameTime); Field(fields, "move-duration", move.duration, value => move.duration = value, policy.CanEdit("movement", "duration") && !frameTime);
                Field(fields, "move-distance", move.distance, value => move.distance = value, policy.CanEdit("movement", "distance")); Field(fields, "move-speed", move.speed, value => move.speed = value, policy.CanEdit("movement", "speed"));
                Field(fields, "move-wallRecovery", move.wallRecovery, value => move.wallRecovery = value, policy.CanEdit("movement", "wallRecovery")); Field(fields, "move-gap", move.gap, value => move.gap = value, policy.CanEdit("movement", "gap"));
                Field(fields, "move-recastWindow", move.recastWindow, value => move.recastWindow = value, policy.CanEdit("movement", "recastWindow")); Field(fields, "move-targetRadius", move.targetRadius, value => move.targetRadius = value, policy.CanEdit("movement", "targetRadius")); Field(fields, "move-endDamage", move.endDamage, value => move.endDamage = value, policy.CanEdit("movement", "endDamage"));
            }
            if (numeric.hasEffect) {
                var effect = numeric.effect; fields.Add(new Label("effect " + policy.Execution.Effect.Type));
                Field(fields, "effect-duration", effect.duration, value => effect.duration = value, policy.CanEdit("effect", "duration") && !frameTime); Field(fields, "effect-hp", effect.hp, value => effect.hp = value, policy.CanEdit("effect", "hp")); Field(fields, "effect-radius", effect.radius, value => effect.radius = value, policy.CanEdit("effect", "radius"));
                Field(fields, "effect-x", effect.x, value => effect.x = value, policy.CanEdit("effect", "x")); Field(fields, "effect-y", effect.y, value => effect.y = value, policy.CanEdit("effect", "y"));
                Field(fields, "effect-parryWindow", effect.parryWindow, value => effect.parryWindow = value, policy.CanEdit("effect", "parryWindow") && !frameTime); Field(fields, "effect-parryDamage", effect.parryDamage, value => effect.parryDamage = value, policy.CanEdit("effect", "parryDamage")); Geometry(fields, "effect-box", effect.box, policy.CanEdit("effect", "box"));
            }
            for (int index = 0; index < numeric.invulStart.Length; index++) { int span = index; Field(fields, "invul-" + span + "-start", numeric.invulStart[span], value => numeric.invulStart[span] = value, !frameTime); Field(fields, "invul-" + span + "-end", numeric.invulEnd[span], value => numeric.invulEnd[span] = value, !frameTime); }
            for (int index = 0; index < numeric.verticalFreeze.Length; index++) { int endpoint = index; Field(fields, "freeze-" + endpoint, numeric.verticalFreeze[endpoint], value => numeric.verticalFreeze[endpoint] = value, !frameTime); }
            RefreshState();
        }
        public AuthoredSkillDefinition[] CompileCandidate()
        {
            var values = draft ? draft.Compile() : throw new ArgumentException("원본을 먼저 읽으세요.");
            var original = draft.sources.Select(source => new AuthoredSkillDefinition(source.id.Substring(0, source.id.IndexOf('_')), source)).ToArray();
            SpecialFrameNumericEdits.ValidateAuthoredCandidates(repository, original, values); return values;
        }
        public bool ApplyCandidate(bool reset, out string error) { if (!EditorApplication.isPlaying || !arena) { error = "초기화된 Game/Lab 전투를 선택하세요."; return false; } try { return arena.TryApplyAuthoredSkills(CompileCandidate(), reset, out error); } catch (ArgumentException exception) { error = exception.Message; return false; } }
        void Apply(bool reset) { status = ApplyCandidate(reset, out var error) ? "전체 수치 후보 게시 완료. 진행 실행/독립 효과는 시작 버전을 보존합니다." : error; RefreshState(); }
        public string SavePermanentSource(string executable = "python")
        {
            var values = CompileCandidate(); bool saved = false; try { fileHash = AuthoredSkillSource.Save(repository, fileHash, values); saved = true; savedVersion = Version(values); BasicSwordSource.Regenerate(repository, executable); ImportCatalog(); status = "원본 저장·문서/카탈로그 재생성 완료. 현재 전투 적용과 별도입니다."; RefreshState(); return fileHash; }
            catch (Exception exception) when (Expected(exception)) { status = (saved ? "원본 저장 완료, 생성 실패: " : "원본 저장 실패: ") + exception.Message; RefreshState(); throw; }
        }
        void ImportCatalog()
        {
            string path = Path.GetFullPath(Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json"));
            if (path.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/MeleeCatalog.json");
        }
        static bool Expected(Exception value) => value is ArgumentException || value is InvalidOperationException || value is IOException || value is UnauthorizedAccessException || value is System.ComponentModel.Win32Exception;
        void FileAction(Action action) { status = ""; try { action(); } catch (Exception exception) when (Expected(exception)) { if (string.IsNullOrEmpty(status)) status = exception.Message; } RefreshState(); }
        void RefreshState()
        {
            var state = rootVisualElement.Q<Label>("state"); if (state == null) return; if (!arena && EditorApplication.isPlaying) { var values = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None); if (values.Length == 1) arena = values[0]; }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena); rootVisualElement.Q<Label>("source").text = string.IsNullOrEmpty(repository) ? "원본 없음" : Path.Combine(repository, AuthoredSkillSource.RelativePath); rootVisualElement.Q<Label>("status").text = status;
            foreach (string button in new[] { "applyNext", "applyReset", "saveSource" }) rootVisualElement.Q<Button>(button).SetEnabled(false);
            try { var values = CompileCandidate(); selected = Mathf.Clamp(selected, 0, values.Length - 1); var current = values[selected]; var data = current.ToNumericData();
                if (float.IsNaN(previewTime) || float.IsInfinity(previewTime)) throw new ArgumentException("유한 미리보기 시간 표본이 필요합니다.");
                selectedHit = Mathf.Clamp(selectedHit, 0, Math.Max(1, current.Execution.Hits.Count) - 1); geometry?.SetCandidate(current, selectedHit, arena);
                state.text = (savedVersion == Version(values) ? "원본과 동일" : "수치 편집 · 미저장") + "\n후보 " + current.ContentHash + (arena ? "\n다음 시작 " + arena.GetAuthoredSkill(current.Id)?.ContentHash : "");
                rootVisualElement.Q<Label>("policy").text = $"{current.FormId}/{current.Id} · {current.Execution.Vertical}/{current.Execution.Facing} · 쿨타임 {current.Execution.Cooldown:R}/용량 {current.Execution.Capacity}·종류/개수/앵커/자원 정책 읽기 전용";
                var text = new System.Text.StringBuilder("읽기 전용 t=" + previewTime.ToString("R") + " · 행동/물리/자원/피해/Cue 실행 없음\n");
                for (int index = 0; index < data.hits.Length; index++) { var hit = data.hits[index]; text.AppendLine($"hit[{index}] {(previewTime >= hit.time && previewTime < hit.time + hit.active ? "활성" : "비활성")} [{hit.time:R},{hit.time + hit.active:R}) · {current.Execution.Hits[index].Anchor}" + (hit.box == null ? " geometry 없음" : $" box({hit.box.width:R},{hit.box.height:R})@({hit.box.x:R},{hit.box.y:R})")); }
                if (data.movement != null && current.Execution.Movement.Type == "dash") text.AppendLine("평지 이동 표본=" + (data.movement.speed * Mathf.Clamp(previewTime - data.movement.start, 0, data.movement.duration)).ToString("R") + " (충돌 예측 아님)");
                if (data.effect?.box != null) text.AppendLine($"효과 형상 ({data.effect.box.width:R},{data.effect.box.height:R})@({data.effect.box.x:R},{data.effect.box.y:R}) · 실제생성없음");
                rootVisualElement.Q<Label>("preview").text = text.ToString(); bool runtime = EditorApplication.isPlaying && arena && arena.Player; rootVisualElement.Q<Button>("applyNext").SetEnabled(runtime); rootVisualElement.Q<Button>("applyReset").SetEnabled(runtime); rootVisualElement.Q<Button>("saveSource").SetEnabled(!string.IsNullOrEmpty(fileHash));
            }
            catch (ArgumentException exception) { state.text = "전체 후보 무효 — 이전 버전 보존: " + exception.Message; rootVisualElement.Q<Label>("preview").text = ""; geometry?.SetInvalid(exception.Message); }
        }
    }
}
