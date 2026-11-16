using System;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Combat;
using Deviant.CombatLab;
using Deviant.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class FiniteActionTrackWindow : EditorWindow
    {
        [SerializeField] FiniteActionTrackDraft draft, captureA, captureB, effectCaptureA, effectCaptureB;
        [SerializeField] LocomotionData movementA, movementB, effectMovementA, effectMovementB;
        [SerializeField] CombatArena arena;
        [SerializeField] string repository, sourceFileHash, savedVersion, python = "python";
        [SerializeField] int selectedAction, selectedHit;
        ActionGeometryPreview preview;
        string status = "", results = "";
        [MenuItem("Tools/deviant/Experiments/Finite Sword Action Tracks")]
        public static void Open() => GetWindow<FiniteActionTrackWindow>("Finite action tracks");
        void OnEnable() => Undo.undoRedoPerformed += RefreshFields;
        void OnDisable() => Undo.undoRedoPerformed -= RefreshFields;
        void OnDestroy() { foreach (var value in new[] { draft, captureA, captureB, effectCaptureA, effectCaptureB }) if (value) DestroyImmediate(value); }
        void OnInspectorUpdate() => RefreshState();
        public void CreateGUI()
        {
            rootVisualElement.Clear(); var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(new Label("기존 검 돌진/잔상/검기·방패 수호막 · 유한 행동 로컬 시간"));
            scroll.Add(new Label("CombatTimeFixed: 트랙이 시간/형상을 소유하고 실제 피해·돌진/검기/수호막 수치만 편집합니다. 기존 시간/형상·돌진 distance는 읽기 전용 이행 출처이며 자원/종류/제어 정책은 바꾸지 않습니다. Sprite 삽입/새 아트/Cue/무적은 만들지 않습니다."));
            var target = new ObjectField("Game / Lab 전투") { name = "runtime", objectType = typeof(CombatArena), allowSceneObjects = true };
            target.SetValueWithoutNotify(arena); target.RegisterValueChangedCallback(change => { arena = change.newValue as CombatArena; RefreshState(); }); scroll.Add(target);
            scroll.Add(Button("reload", "현재 원본에서 다시 가져오기", Reload));
            scroll.Add(Label("source")); scroll.Add(Label("state")); scroll.Add(Label("status"));
            var actions = new DropdownField("행동", ActionTrackDefinition.SupportedIds.ToList(), Mathf.Clamp(selectedAction, 0, ActionTrackDefinition.SupportedIds.Count - 1)) { name = "action" };
            actions.RegisterValueChangedCallback(_ => { selectedAction = actions.index; selectedHit = 0; RefreshFields(); }); scroll.Add(actions);
            var hits = new DropdownField("미리볼 StableHitId") { name = "hit" }; hits.RegisterValueChangedCallback(_ => { selectedHit = hits.index; RefreshState(); }); scroll.Add(hits);
            scroll.Add(new VisualElement { name = "frames" }); scroll.Add(new VisualElement { name = "markers" }); scroll.Add(new VisualElement { name = "geometry" });
            scroll.Add(Label("effective")); preview = new ActionGeometryPreview(); scroll.Add(preview);
            scroll.Add(Button("applyNext", "다음 시작부터 전체 후보 임시 적용", () => Apply(false)));
            scroll.Add(Button("applyReset", "전투 리셋 후 전체 후보 임시 적용", () => Apply(true)));
            var executable = new TextField("Python 실행 파일") { name = "python", value = python };
            executable.RegisterValueChangedCallback(change => python = change.newValue); scroll.Add(executable);
            scroll.Add(Button("saveSource", "원본 저장 · 문서/카탈로그 재생성", () => FileAction(() => SavePermanentSource(python))));
            scroll.Add(Button("regenerate", "저장된 원본 재생성 재시도", () => FileAction(Regenerate)));
            scroll.Add(Button("captureA", "현재 적용된 돌진/잔상·이동을 A로 기록", () => Capture(true)));
            scroll.Add(Button("captureB", "현재 적용된 돌진/잔상·이동을 B로 기록", () => Capture(false)));
            scroll.Add(Button("compare", "같은 입력으로 돌진/잔상 A/B · 전투 리셋", Compare));
            scroll.Add(Button("captureEffectsA", "현재 적용된 검기/수호막·이동을 A로 기록", () => CaptureEffects(true)));
            scroll.Add(Button("captureEffectsB", "현재 적용된 검기/수호막·이동을 B로 기록", () => CaptureEffects(false)));
            scroll.Add(Button("compareEffects", "같은 입력으로 검기/수호막 A/B · 전투 리셋", CompareEffects));
            scroll.Add(Label("comparison"));
            if (!draft)
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, FiniteActionTrackSource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName); else status = "Canonical melee source를 찾지 못했습니다.";
            }
            RefreshFields();
        }
        static Label Label(string name) { var value = new Label { name = name }; value.style.whiteSpace = WhiteSpace.Normal; value.style.marginBottom = 6; return value; }
        static Button Button(string name, string text, Action action) => new Button(action) { name = name, text = text };
        public void LoadRepository(string path) { repository = path; Reload(); }
        public void SetRuntime(CombatArena value) { arena = value; RefreshState(); }
        void Reload()
        {
            FileAction(() => {
                var definitions = FiniteActionTrackSource.Load(repository, out var hash);
                if (!draft) draft = NewDraft(); else { Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Reload finite sword tracks"); }
                draft.Load(definitions); sourceFileHash = hash; savedVersion = Version(definitions); status = "원본을 읽었습니다. 적용/리셋/저장은 각각 명시적 동작입니다.";
                RefreshFields();
            });
        }
        static FiniteActionTrackDraft NewDraft() { var value = CreateInstance<FiniteActionTrackDraft>(); value.hideFlags = HideFlags.HideAndDontSave; return value; }
        static string Version(ActionTrackDefinition[] values) => string.Join("/", values.Select(value => value.ContentHash));
        void Edit(Action change)
        {
            Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Edit finite sword tracks"); change(); status = ""; RefreshState();
        }
        void Numeric(VisualElement parent, string name, float value, Action<float> change)
        {
            var field = new FloatField(name) { name = name }; field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(next => Edit(() => change(next.newValue))); parent.Add(field);
        }
        void RefreshFields()
        {
            var frames = rootVisualElement.Q("frames"); if (frames == null) return;
            frames.Clear(); var markers = rootVisualElement.Q("markers"); markers.Clear(); var geometry = rootVisualElement.Q("geometry"); geometry.Clear();
            if (draft?.tracks == null || draft.numerics == null || draft.numerics.Length != draft.tracks.Length || selectedAction < 0 || selectedAction >= draft.tracks.Length) { RefreshState(); return; }
            var actionSelector = rootVisualElement.Q<DropdownField>("action"); actionSelector.choices = draft.tracks.Select(value => value.id).ToList();
            actionSelector.SetValueWithoutNotify(actionSelector.choices[selectedAction]);
            var track = draft.tracks[selectedAction]; var numeric = draft.numerics[selectedAction]; var hitSelector = rootVisualElement.Q<DropdownField>("hit");
            hitSelector.choices = track.hits.Length == 0 ? new System.Collections.Generic.List<string> { track.defense.id + " (공격 박스 없음)" } : track.hits.Select(hit => hit.id).ToList();
            selectedHit = Mathf.Clamp(selectedHit, 0, hitSelector.choices.Count - 1); hitSelector.SetValueWithoutNotify(hitSelector.choices[selectedHit]); hitSelector.SetEnabled(track.hits.Length > 0);
            foreach (var hit in numeric.hits) { var captured = hit; Numeric(geometry, "damage-" + hit.id, hit.damage, value => captured.damage = value); }
            if (track.id == ActionTrackDefinition.MovementId) {
                Numeric(geometry, "dash-speed", numeric.dashSpeed, value => numeric.dashSpeed = value); Numeric(geometry, "wall-recovery", numeric.wallRecovery, value => numeric.wallRecovery = value);
            }
            if (track.id == ActionTrackDefinition.WaveId) {
                Numeric(geometry, "wave-speed", numeric.waveSpeed, value => numeric.waveSpeed = value); Numeric(geometry, "wave-distance", numeric.waveDistance, value => numeric.waveDistance = value);
                Numeric(geometry, "wave-spawn-x", numeric.waveSpawnX, value => numeric.waveSpawnX = value); Numeric(geometry, "wave-spawn-y", numeric.waveSpawnY, value => numeric.waveSpawnY = value);
            }
            if (track.id == ActionTrackDefinition.GuardId) {
                Numeric(geometry, "guard-hp", numeric.guardHp, value => numeric.guardHp = value); Numeric(geometry, "guard-reduction", numeric.guardReduction, value => numeric.guardReduction = value);
            }
            bool frameTime = !string.IsNullOrEmpty(repository) && SpecialFrameSource.HasFrameOverride(repository, track.id) || arena && arena.GetSpecialFrame(track.id)?.UsesFrameTime == true;
            frames.SetEnabled(!frameTime); markers.SetEnabled(!frameTime);
            if (frameTime) geometry.Add(new Label("FrameTime 활성: 시간은 Special Frame Timing에서 편집하며 이 창은 비시간 수치/형상만 게시합니다."));
            foreach (var frame in track.timeline.frames)
            {
                var captured = frame; var field = new FloatField(frame.id + " 구간 (s)") { name = "frame-" + frame.id };
                field.SetValueWithoutNotify(frame.seconds); field.RegisterValueChangedCallback(change => Edit(() => captured.seconds = change.newValue)); frames.Add(field);
            }
            foreach (var marker in track.timeline.markers)
            {
                var captured = marker; markers.Add(new Label(marker.id));
                var anchor = new EnumField("기준", marker.anchor) { name = "anchor-" + marker.id };
                anchor.RegisterValueChangedCallback(change => { Edit(() => { captured.anchor = (TimelineAnchor)change.newValue; captured.reference = ""; }); RefreshFields(); }); markers.Add(anchor);
                var choices = new System.Collections.Generic.List<string> { "" };
                if (marker.anchor == TimelineAnchor.Marker) choices.AddRange(track.timeline.markers.Select(value => value.id));
                else if (marker.anchor != TimelineAnchor.Seconds) choices.AddRange(track.timeline.frames.Select(value => value.id));
                var reference = new DropdownField("기준 ID", choices, choices.Contains(marker.reference) ? marker.reference : "") { name = "reference-" + marker.id };
                reference.SetEnabled(marker.anchor != TimelineAnchor.Seconds); reference.RegisterValueChangedCallback(change => Edit(() => captured.reference = change.newValue)); markers.Add(reference);
                var offset = new FloatField(marker.anchor == TimelineAnchor.Seconds ? "절대 시각 (s)" : "오프셋/구간 (s)") { name = "offset-" + marker.id };
                offset.SetValueWithoutNotify(marker.offset); offset.RegisterValueChangedCallback(change => Edit(() => captured.offset = change.newValue)); markers.Add(offset);
            }
            foreach (var hit in track.hits)
            {
                var captured = hit; geometry.Add(new Label(hit.id + " · " + hit.anchor + " (정책/ID 읽기 전용)"));
                foreach (string coordinate in new[] { "width", "height", "x", "y" })
                {
                    string component = coordinate; var field = new FloatField(hit.id + " box." + coordinate) { name = "box-" + hit.id + "-" + coordinate };
                    field.SetValueWithoutNotify(coordinate == "width" ? hit.box.width : coordinate == "height" ? hit.box.height : coordinate == "x" ? hit.box.x : hit.box.y);
                    field.RegisterValueChangedCallback(change => Edit(() => {
                        if (component == "width") captured.box.width = change.newValue; else if (component == "height") captured.box.height = change.newValue;
                        else if (component == "x") captured.box.x = change.newValue; else captured.box.y = change.newValue;
                    })); geometry.Add(field);
                }
            }
            if (track.effect != null && !string.IsNullOrEmpty(track.effect.id)) geometry.Add(new Label("검기 span은 최소 독립 보관 시간입니다. 실제 이동/판정 수명은 기존 속도·거리·고체 충돌을 따릅니다."));
            if (track.defense != null && !string.IsNullOrEmpty(track.defense.id)) geometry.Add(new Label("수호막 공격 박스 없음. guard-end − contact의 구간을 실제 자원 발동 시각부터 유지합니다. HP/감소율만 편집하며 교체·최대 지속 갱신 정책은 유지합니다."));
            RefreshState();
        }
        public ActionTrackDefinition[] CompileCandidate()
        {
            var values = draft ? draft.Compile() : throw new ArgumentException("원본을 먼저 읽으세요.");
            SpecialFrameNumericEdits.ValidateFiniteCandidates(repository, draft.sources.Select(source => new ActionTrackDefinition(source)).ToArray(), values);
            return values;
        }
        public bool ApplyCandidate(bool reset, out string error)
        {
            if (!EditorApplication.isPlaying || !arena) { error = "초기화된 Game/Lab 전투를 선택하세요."; return false; }
            try { return arena.TryApplyActionTracks(CompileCandidate(), reset, out error); }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        void Apply(bool reset) { status = ApplyCandidate(reset, out var error) ? "전체 후보를 게시했습니다. 진행 중 본체/독립 타격의 시작 버전과 원본 파일은 유지합니다." : error; RefreshState(); }
        public string SavePermanentSource(string pythonExecutable = "python")
        {
            var candidate = CompileCandidate(); bool saved = false;
            try
            {
                sourceFileHash = FiniteActionTrackSource.Save(repository, sourceFileHash, candidate); saved = true; savedVersion = Version(candidate);
                Regenerate(pythonExecutable); status = "원본 저장·문서/카탈로그 재생성 완료. 현재 전투는 명시적 적용 전까지 유지합니다.";
                RefreshState(); return sourceFileHash;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException || exception is System.ComponentModel.Win32Exception)
            { status = (saved ? "원본 저장 완료, 생성 실패: " : "원본 저장 실패: ") + exception.Message; RefreshState(); throw; }
        }
        void Regenerate() { Regenerate(python); status = "저장된 원본 문서/카탈로그 재생성 완료."; }
        void Regenerate(string executable)
        {
            BasicSwordSource.Regenerate(repository, executable);
            string generated = Path.GetFullPath(Path.Combine(repository, FiniteActionTrackSource.CatalogPath));
            if (generated.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/MeleeCatalog.json");
        }
        void Capture(bool a)
        {
            if (!EditorApplication.isPlaying || !arena || !arena.Player) return;
            var capture = a ? captureA : captureB; if (!capture) capture = NewDraft();
            var version = FiniteActionTrackVersion.Capture(arena); capture.Load(version.Actions.ToArray());
            if (a) { captureA = capture; movementA = version.Movement.ToData(); } else { captureB = capture; movementB = version.Movement.ToData(); }
            status = (a ? "A" : "B") + " 실제 적용 버전을 기록했습니다. 미적용 Draft는 자동 채택하지 않습니다."; RefreshState();
        }
        FiniteActionTrackVersion CaptureVersion(FiniteActionTrackDraft capture, LocomotionData movement)
        {
            if (!capture) throw new ArgumentException("A/B 버전을 먼저 기록하세요.");
            if (!LocomotionProfile.TryCreate(movement, out var profile, out var error)) throw new ArgumentException(error);
            return new FiniteActionTrackVersion(capture.Compile(), profile);
        }
        void Compare()
        {
            FileAction(() => {
                var pair = FiniteActionTrackComparison.Run(arena, CaptureVersion(captureA, movementA), CaptureVersion(captureB, movementB));
                var text = new StringBuilder("고정 입력: 0초 특수 · 취소 구간0.26초 대시 · 교체 구간0.30초 교체 예약 · 각1.20초. 기존 장비/정의 복구 후 리셋; 진행 중 행동/HP 복원 아님.\n");
                for (int index = 0; index < pair.A.Count; index++)
                {
                    var a = pair.A[index]; var b = pair.B[index];
                    text.AppendLine($"{a.Segment}: A HP−{a.HealthLost:F3}/hit{a.ReceivedHits}/first{a.FirstHitTick}/x{a.Travel:F3} · B HP−{b.HealthLost:F3}/hit{b.ReceivedHits}/first{b.FirstHitTick}/x{b.Travel:F3}");
                    text.AppendLine("동일 입력 " + a.Inputs.ContentHash.Substring(0, 12) + " / " + b.Inputs.ContentHash.Substring(0, 12));
                }
                results = text.ToString(); status = "두 특수 실제 공통 런타임 A/B 완료. 원본 저장 없음.";
            });
        }
        void CaptureEffects(bool a)
        {
            if (!EditorApplication.isPlaying || !arena || !arena.Player) return;
            var capture = a ? effectCaptureA : effectCaptureB; if (!capture) capture = NewDraft();
            var version = EffectTrackVersion.Capture(arena); capture.Load(version.Actions.ToArray());
            if (a) { effectCaptureA = capture; effectMovementA = version.Movement.ToData(); } else { effectCaptureB = capture; effectMovementB = version.Movement.ToData(); }
            status = (a ? "A" : "B") + " 검기/수호막 실제 적용 버전을 기록했습니다."; RefreshState();
        }
        EffectTrackVersion EffectVersion(FiniteActionTrackDraft capture, LocomotionData movement)
        {
            if (!capture || !LocomotionProfile.TryCreate(movement, out var profile, out _)) throw new ArgumentException("검기/수호막 A/B를 먼저 기록하세요.");
            return new EffectTrackVersion(capture.Compile(), profile);
        }
        void CompareEffects()
        {
            FileAction(() => {
                var pair = EffectTrackComparison.Run(arena, EffectVersion(effectCaptureA, effectMovementA), EffectVersion(effectCaptureB, effectMovementB));
                var text = new StringBuilder("검기: 특수 후0.26초 대시 취소·0.30초 교체. 수호막: 같은 입력 후 고정시각 일반 피해100/낙사 우회 피해10. 기존 Native Receive만 호출합니다.\n");
                for (int index = 0; index < pair.A.Count; index++) {
                    var a = pair.A[index]; var b = pair.B[index];
                    text.AppendLine($"{a.Segment}: A target−{a.TargetHealthLost:F3}/hit{a.TargetHits}/player−{a.PlayerHealthLost:F3}/shield{a.Shield:F3}/guardUntil{a.GuardUntil:F3} · B target−{b.TargetHealthLost:F3}/hit{b.TargetHits}/player−{b.PlayerHealthLost:F3}/shield{b.Shield:F3}/guardUntil{b.GuardUntil:F3}");
                    text.AppendLine("동일 입력 " + a.Inputs.ContentHash.Substring(0, 12) + " / " + b.Inputs.ContentHash.Substring(0, 12));
                }
                results = text.ToString(); status = "검기/수호막 실제 공통 런타임 A/B 완료. 기존 장비/정의를 복구하고 리셋했습니다.";
            });
        }
        void FileAction(Action action)
        {
            status = "";
            try { action(); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is IOException || exception is UnauthorizedAccessException || exception is System.ComponentModel.Win32Exception) { if (string.IsNullOrEmpty(status)) status = exception.Message; }
            RefreshState();
        }
        void RefreshState()
        {
            var state = rootVisualElement.Q<Label>("state"); if (state == null) return;
            if (!arena && EditorApplication.isPlaying) { var values = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None); if (values.Length == 1) arena = values[0]; }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena);
            rootVisualElement.Q<Label>("source").text = string.IsNullOrEmpty(repository) ? "원본 없음" : Path.Combine(repository, FiniteActionTrackSource.RelativePath);
            rootVisualElement.Q<Label>("status").text = status;
            rootVisualElement.Q<Label>("comparison").text = results;
            bool runtime = EditorApplication.isPlaying && arena && arena.Player;
            rootVisualElement.Q<Button>("captureA").SetEnabled(runtime); rootVisualElement.Q<Button>("captureB").SetEnabled(runtime);
            rootVisualElement.Q<Button>("compare").SetEnabled(runtime && captureA && captureB && !arena.ManualSimulationActive && arena.CompositionProfile == CombatCompositionProfile.Lab);
            rootVisualElement.Q<Button>("captureEffectsA").SetEnabled(runtime); rootVisualElement.Q<Button>("captureEffectsB").SetEnabled(runtime);
            rootVisualElement.Q<Button>("compareEffects").SetEnabled(runtime && effectCaptureA && effectCaptureB && !arena.ManualSimulationActive && arena.CompositionProfile == CombatCompositionProfile.Lab);
            foreach (string button in new[] { "applyNext", "applyReset", "saveSource" }) rootVisualElement.Q<Button>(button).SetEnabled(false);
            try
            {
                var values = CompileCandidate(); var current = values[Mathf.Clamp(selectedAction, 0, values.Length - 1)];
                selectedHit = Mathf.Clamp(selectedHit, 0, Math.Max(0, current.Hits.Count - 1)); preview?.SetCandidate(current, selectedHit, arena);
                state.text = (savedVersion == Version(values) ? "원본과 동일" : "트랙/수치 편집 · 미저장") + "\n정책 " + current.SourceVersion + "\n후보 " + current.ContentHash +
                    (runtime ? "\n다음 시작 " + arena.GetActionTracks(current.Id)?.ContentHash : "") + (runtime && arena.Player.currentCast ? "\n진행 중 " + arena.Player.currentCast.DefinitionVersion : "");
                var text = new StringBuilder($"activation={current.Activation:R}s · bodyEnd={current.BodyEnd:R}s · tailEnd={current.TailEnd:R}s\n");
                for (int index = 0; index < current.Hits.Count; index++) { var hit = current.Hits[index]; text.AppendLine($"{hit.Id}: [{hit.Start:R}, {hit.End:R}) duration={hit.Duration:R}s · {hit.Anchor} · damage={current.Execution.Hits[index].Damage:R}"); }
                if (current.Motion != null) text.AppendLine($"motion {current.Motion.Id}: [{current.Motion.Start:R}, {current.Motion.End:R}) speed={current.Motion.Speed:R} · wallRecovery={current.Execution.Movement.WallRecovery:R} · 평지 이동={current.Motion.Speed * current.Motion.Duration:R} (읽기 전용 파생 표본)");
                if (current.Effect != null) { var effect = current.Execution.Effect; text.AppendLine($"effect {current.Effect.Id}: [{current.Effect.Start:R}, {current.Effect.End:R}) minimum retention; speed={effect.Speed:R} distance={effect.Distance:R} spawn=({effect.X:R},{effect.Y:R}); live distance/speed/wall owns damage lifetime"); }
                if (current.Defense != null) text.AppendLine($"defense {current.Defense.Id}: [{current.Defense.Start:R}, {current.Defense.End:R}) duration={current.Defense.Duration:R}s from actual activation · hp={current.Execution.Effect.Hp:R} reduction={current.Execution.Effect.Reduction:R}");
                rootVisualElement.Q<Label>("effective").text = text.ToString();
                rootVisualElement.Q<Button>("applyNext").SetEnabled(runtime); rootVisualElement.Q<Button>("applyReset").SetEnabled(runtime);
                rootVisualElement.Q<Button>("saveSource").SetEnabled(!string.IsNullOrEmpty(sourceFileHash));
            }
            catch (ArgumentException exception)
            { state.text = "전체 후보 무효 — 이전 실행 버전 보존: " + exception.Message; rootVisualElement.Q<Label>("effective").text = ""; preview?.SetInvalid(exception.Message); }
        }
    }
}
