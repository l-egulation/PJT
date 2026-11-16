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
    public sealed class MotionRegistryWindow : EditorWindow
    {
        public const string LayoutPath = "Assets/Deviant/Authoring/Editor/MotionRegistryWindow.uxml";
        [SerializeField] MotionRegistryDraft draft;
        [SerializeField] string repository, registryFileHash, firstFileHash, savedRegistryHash, savedFirstHash, selectedKey, python = "python";
        [SerializeField] MotionRegistryDocument savedRegistry;
        [SerializeField] AttackMotionDocument savedFirst;
        [SerializeField] CombatArena arena;
        Catalog combat;
        MotionSourceRegistry candidate;
        FrameAttackMotionSource first;
        MotionActionBinding selected;
        AttackMotionTiming timing;
        IReadOnlyList<float> effectiveSeconds;
        Sprite[] previewSprites = Array.Empty<Sprite>();
        string candidateBindingHash, runtimeBindingHash, runtimeAttackHash;
        bool observedPlayMode;
        IMGUIContainer canvas;
        string status, error;
        bool candidateDirty = true;
        public MotionRegistryDraft Draft => draft;
        bool Delegated => selected?.MotionId == FixedAttackMotion.FirstSwordId;
        bool Editable => selected != null && !selected.IsFallback && !(Delegated && (draft?.firstMotion?.mode == FrameAttackMotionSource.FrameMode));
        MotionClipDocument Clip => draft?.registry?.motions?.FirstOrDefault(value => value.id == selected?.MotionId);
        AttackMotionStep[] Steps => Delegated ? draft?.firstMotion?.steps : Clip?.steps;
        [MenuItem("Tools/deviant/Experiments/Attack Body Motions")]
        public static void Open() => GetWindow<MotionRegistryWindow>("Attack body motions");
        void OnEnable() { Undo.undoRedoPerformed += UndoChanged; }
        void OnDisable() { Undo.undoRedoPerformed -= UndoChanged; }
        void OnDestroy() { if (draft) DestroyImmediate(draft); }
        void UndoChanged() { candidateDirty = true; RefreshFields(); }
        void OnInspectorUpdate()
        {
            string applied = arena ? arena.MotionBindingHash : null, running = arena ? arena.Presentation.CurrentAttackMotionVersion : null;
            if (candidateDirty || runtimeBindingHash != applied || runtimeAttackHash != running || observedPlayMode != EditorApplication.isPlaying)
            { runtimeBindingHash = applied; runtimeAttackHash = running; observedPlayMode = EditorApplication.isPlaying; RefreshState(); }
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (!layout) { rootVisualElement.Add(new Label("MotionRegistryWindow.uxml missing")); return; }
            layout.CloneTree(rootVisualElement);
            var target = rootVisualElement.Q<ObjectField>("runtime"); target.objectType = typeof(CombatArena); target.allowSceneObjects = true;
            target.SetValueWithoutNotify(arena); target.RegisterValueChangedCallback(value => { arena = value.newValue as CombatArena; candidateDirty = true; RefreshFields(); });
            rootVisualElement.Q<DropdownField>("action").RegisterValueChangedCallback(value => {
                var field = rootVisualElement.Q<DropdownField>("action");
                if (candidate != null && field.index >= 0) { selectedKey = candidate.Actions[field.index].Key.ToString(); candidateDirty = true; RefreshFields(); }
            });
            rootVisualElement.Q<FloatField>("sourceImpact").RegisterValueChangedCallback(value => Change(() => { if (Delegated) draft.firstMotion.sourceImpact = value.newValue; else Clip.sourceImpact = value.newValue; }));
            rootVisualElement.Q<FloatField>("fxStart").RegisterValueChangedCallback(value => Change(() => { if (Delegated) draft.firstMotion.fxStart = value.newValue; else Clip.fxStart = value.newValue; }));
            rootVisualElement.Q<Button>("append").clicked += () => Change(() => {
                string id = rootVisualElement.Q<TextField>("newId").value, frame = rootVisualElement.Q<DropdownField>("newFrame").value;
                ReplaceSteps(MotionRegistryEdits.Insert(Steps, Steps.Length - 1, id, frame, .05f));
            }, true);
            rootVisualElement.Q<Button>("reload").clicked += Reload;
            rootVisualElement.Q<Button>("save").clicked += Save;
            rootVisualElement.Q<Button>("applyNext").clicked += () => Apply(false);
            rootVisualElement.Q<Button>("applyReset").clicked += () => Apply(true);
            rootVisualElement.Q<Button>("frameEditor").clicked += () => { var window = GetWindow<FrameTimingWindow>("Frame timing"); window.LoadRepository(repository); };
            rootVisualElement.Q<TextField>("python").SetValueWithoutNotify(python);
            rootVisualElement.Q<TextField>("python").RegisterValueChangedCallback(value => python = value.newValue);
            rootVisualElement.Q<Slider>("time").RegisterValueChangedCallback(_ => RefreshSample());
            rootVisualElement.Q<Toggle>("left").RegisterValueChangedCallback(_ => RefreshSample());
            canvas = new IMGUIContainer(DrawBody) { name = "motion-body-canvas" }; canvas.style.height = 280; rootVisualElement.Q("bodyPreview").Add(canvas);
            if (draft == null)
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, MotionRegistrySource.RelativePath))) directory = directory.Parent;
                if (directory != null) LoadRepository(directory.FullName); else status = "완전한 모션 원본을 찾지 못했습니다.";
            }
            else if (!string.IsNullOrEmpty(repository)) LoadDependencies();
            RefreshFields();
        }
        public void LoadRepository(string path) { repository = path; Reload(); }
        void LoadDependencies()
        {
            combat = MotionRegistrySource.ReadCombat(repository);
            _ = SourceFiles.Read(Path.Combine(repository, AttackMotionSource.CatalogPath), out _, MotionRegistrySource.MaximumFileBytes * 8);
        }
        void Reload()
        {
            try
            {
                var registry = MotionRegistrySource.Load(repository, out var registryHash);
                var firstDocument = AttackMotionSource.Load(Path.Combine(repository, AttackMotionSource.RelativePath), out var firstHash);
                var firstSource = new FrameAttackMotionSource(firstDocument);
                LoadDependencies();
                if (!draft) { draft = CreateInstance<MotionRegistryDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; }
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(draft, "Reload body motion sources");
                draft.registry = registry.ToDocument(); draft.firstMotion = firstDocument; EditorUtility.SetDirty(draft);
                registryFileHash = registryHash; firstFileHash = firstHash; savedRegistryHash = registry.ContentHash; savedFirstHash = firstSource.FixedBinding.ContentHash;
                savedRegistry = registry.ToDocument(); savedFirst = firstSource.FixedBinding.ToDocument();
                status = "모든 원본을 읽었습니다. 현재 전투와 시작 매체는 변경하지 않았습니다."; candidateDirty = true;
            }
            catch (Exception exception) when (Expected(exception)) { status = exception.Message; }
            RefreshFields();
        }
        void ReplaceSteps(AttackMotionStep[] values) { if (Delegated) draft.firstMotion.steps = values; else Clip.steps = values; }
        void Change(Action change, bool rebuild = false)
        {
            if (!Editable) return;
            try
            {
                // Stable step insertion/reordering and shared bindings are one
                // graph, not a collection of independently replayed array diffs.
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(draft, "Edit shared body motion");
                change(); EditorUtility.SetDirty(draft); status = null;
            }
            catch (ArgumentException exception) { status = exception.Message; }
            candidateDirty = true;
            if (rebuild) RefreshFields(); else RefreshState();
        }
        void CompileCandidate()
        {
            if (!candidateDirty) return;
            candidateDirty = false; error = null; timing = null; effectiveSeconds = null; previewSprites = Array.Empty<Sprite>();
            try
            {
                candidate = new MotionSourceRegistry(draft?.registry, combat);
                first = new FrameAttackMotionSource(draft.firstMotion);
                selected = candidate.Actions.FirstOrDefault(value => value.Key.ToString() == selectedKey) ?? candidate.Actions[0]; selectedKey = selected.Key.ToString();
                if (first.FixedBinding.Frames.Any(frame => !candidate.Palette(FixedAttackMotion.FirstSwordId).Frames.Contains(frame))) throw new ArgumentException("첫 검 모션은 공급된 기존 매체 프레임만 참조할 수 있습니다.");
                timing = MotionRegistryEdits.CompileBody(candidate, selected, first, combat, arena);
                effectiveSeconds = timing.EffectiveSeconds();
                candidateBindingHash = candidate.BindingHash(first.FixedBinding);
                previewSprites = timing.Frames.Select(ResolvePreviewSprite).ToArray();
            }
            catch (ArgumentException exception) { error = exception.Message; }
        }
        Sprite ResolvePreviewSprite(string frame)
        {
            // An explicitly selected runtime owns its actual media bindings.
            // Missing runtime media is shown as missing, never replaced by art
            // from a different load path; offline previews use project assets.
            if (!arena) return Resources.Load<Sprite>(frame);
            var media = arena.characterAnimations;
            if (media != null && media.TryMotionSprite(frame, out var sprite)) return sprite;
            if (media?.sets != null)
                foreach (var set in media.sets)
                    foreach (var clip in set?.clips ?? Array.Empty<CharacterClip>())
                    {
                        int index = clip?.frames == null ? -1 : Array.IndexOf(clip.frames, frame);
                        if (index >= 0 && clip.sprites != null && index < clip.sprites.Length) return clip.sprites[index];
                    }
            return null;
        }
        void RefreshFields()
        {
            if (rootVisualElement.Q<DropdownField>("action") == null) return;
            CompileCandidate();
            if (candidate == null || selected == null) { RefreshState(); return; }
            var selector = rootVisualElement.Q<DropdownField>("action"); selector.choices = candidate.Actions.Select(Label).ToList(); selector.SetValueWithoutNotify(Label(selected));
            var values = Steps ?? (Delegated ? first?.FixedBinding.ToDocument().steps : null);
            var firstBinding = draft?.firstMotion?.mode == FrameAttackMotionSource.FrameMode ? draft.firstMotion.fixedBinding?.ToDocument() : draft?.firstMotion;
            rootVisualElement.Q<FloatField>("sourceImpact").SetValueWithoutNotify(Delegated ? firstBinding?.sourceImpact ?? 0 : Clip?.sourceImpact ?? 0);
            rootVisualElement.Q<FloatField>("fxStart").SetValueWithoutNotify(Delegated ? firstBinding?.fxStart ?? -1 : Clip?.fxStart ?? -1);
            var frames = rootVisualElement.Q("frames"); frames.Clear();
            if (values != null)
                for (int index = 0; index < values.Length; index++)
                {
                    int position = index; var step = values[index]; var row = new VisualElement();
                    var id = new TextField("안정 스텝 ID") { name = "step-id-" + index }; id.SetValueWithoutNotify(step.id); id.RegisterValueChangedCallback(value => Change(() => Steps[position].id = value.newValue)); row.Add(id);
                    var frame = new DropdownField("기존 매체 프레임", candidate.Palette(selected.MotionId).Frames.ToList(), 0) { name = "step-frame-" + index }; frame.SetValueWithoutNotify(step.frame); frame.RegisterValueChangedCallback(value => Change(() => Steps[position].frame = value.newValue)); row.Add(frame);
                    var seconds = new FloatField("원본 유지 시간 (s)") { name = "seconds-" + index }; seconds.SetValueWithoutNotify(step.seconds); seconds.RegisterValueChangedCallback(value => Change(() => Steps[position].seconds = value.newValue)); row.Add(seconds);
                    var buttons = new VisualElement(); buttons.style.flexDirection = FlexDirection.Row;
                    var up = new Button(() => Change(() => ReplaceSteps(MotionRegistryEdits.Move(Steps, position, -1)), true)) { text = "위로", name = "up-" + index }; up.SetEnabled(index > 0); buttons.Add(up);
                    var down = new Button(() => Change(() => ReplaceSteps(MotionRegistryEdits.Move(Steps, position, 1)), true)) { text = "아래로", name = "down-" + index }; down.SetEnabled(index + 1 < values.Length); buttons.Add(down);
                    var remove = new Button(() => Change(() => ReplaceSteps(MotionRegistryEdits.Remove(Steps, position)), true)) { text = "제거", name = "remove-" + index }; remove.SetEnabled(values.Length > 1); buttons.Add(remove); row.Add(buttons); row.SetEnabled(Editable); frames.Add(row);
                }
            var newFrame = rootVisualElement.Q<DropdownField>("newFrame"); newFrame.choices = candidate.Palette(selected.MotionId).Frames.ToList();
            if (!newFrame.choices.Contains(newFrame.value)) newFrame.SetValueWithoutNotify(newFrame.choices[0]);
            RefreshState();
        }
        void Save()
        {
            bool wrote = false;
            try
            {
                CompileCandidate(); if (!Editable || error != null) throw new ArgumentException(error ?? "대체/읽기 전용 매체는 저장할 수 없습니다.");
                if (Delegated)
                {
                    firstFileHash = AttackMotionSource.Save(Path.Combine(repository, AttackMotionSource.RelativePath), firstFileHash, draft.firstMotion);
                    savedFirst = first.FixedBinding.ToDocument(); savedFirstHash = first.FixedBinding.ContentHash;
                }
                else
                {
                    registryFileHash = MotionRegistrySource.Save(repository, registryFileHash, draft.registry);
                    savedRegistry = candidate.ToDocument(); savedRegistryHash = candidate.ContentHash;
                }
                wrote = true; AttackMotionSource.Regenerate(repository, python);
                if (Path.GetFullPath(Path.Combine(repository, "unity")).Equals(Path.GetFullPath(Path.GetDirectoryName(Application.dataPath)), StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset("Assets/Deviant/Combat/Resources/CharacterV2/AnimationCatalog.json");
                status = "선택 원본 저장·전체 생성 완료. 현재 전투 적용은 별도입니다.";
            }
            catch (Exception exception) when (Expected(exception)) { status = (wrote ? "원본 저장 완료, 생성 실패: " : "저장 실패: ") + exception.Message; }
            RefreshState();
        }
        void Apply(bool reset)
        {
            CompileCandidate();
            if (!Editable || error != null || !EditorApplication.isPlaying || !arena) return;
            string failure;
            bool applied = Delegated ? arena.TryApplyBasicSwordMotion(draft.firstMotion, reset, out failure) : arena.TryApplyMotionRegistry(draft.registry, reset, out failure);
            status = applied ? "선택 공유 원본을 " + (reset ? "리셋 후" : "다음 시작부터") + " 적용했습니다. 파일과 이미 시작한 매체는 유지합니다." : failure;
            RefreshState();
        }
        void RefreshState()
        {
            if (rootVisualElement.Q<Label>("state") == null) return;
            CompileCandidate();
            bool valid = error == null && candidate != null && selected != null;
            foreach (string name in new[] { "sourceImpact", "fxStart", "newId", "newFrame", "append" }) rootVisualElement.Q(name).SetEnabled(Editable);
            rootVisualElement.Q<Button>("save").SetEnabled(valid && Editable && !string.IsNullOrEmpty(Delegated ? firstFileHash : registryFileHash));
            foreach (string name in new[] { "applyNext", "applyReset" }) rootVisualElement.Q<Button>(name).SetEnabled(valid && Editable && EditorApplication.isPlaying && arena && arena.Player && (!Delegated || arena.FrameSwordTiming == null));
            rootVisualElement.Q<Button>("frameEditor").SetEnabled(Delegated && !selected.IsFallback);
            rootVisualElement.Q<Label>("source").text = (repository ?? "원본 경로 없음") + "\nregistry " + registryFileHash + "\nfirst " + firstFileHash;
            rootVisualElement.Q<Label>("binding").text = selected == null ? "행동을 불러오세요." : selected.Key + " : " + selected.RequestedForm + "/" + selected.RequestedClip + " → " + selected.MotionId + (selected.IsFallback ? " · 기존 대체 표시만 읽기 전용 (선택 형태 아트 없음)" : " · 공급된 공유 매체");
            rootVisualElement.Q<Label>("affected").text = candidate == null || selected == null ? "" : "같은 원본에 영향받는 행동 " + candidate.AffectedActions(selected.MotionId).Count + ":\n" + string.Join("\n", candidate.AffectedActions(selected.MotionId).Select(Label));
            rootVisualElement.Q<Label>("framePolicy").text = Delegated && !selected.IsFallback ? (first?.UsesFrameTime == true ? "기존 첫 검 FrameTime 활성: 고정 매체는 읽기 전용입니다." : "첫 검만 기존 FrameTime 컴파일러에서 gameplay 프레임 저작을 지원합니다.") : "FrameTime gameplay 저작 미지원: 이 창은 CombatTimeFixed 본체 가중치만 편집합니다. 차지/재시전/미사일의 동적 표시 정책은 유지합니다.";
            string dirty = candidate != null && first != null && candidate.ContentHash == savedRegistryHash && first.FixedBinding.ContentHash == savedFirstHash ? "저장된 원본과 동일" : "미저장 변경";
            rootVisualElement.Q<Label>("state").text = valid ? dirty + "\n" + candidate.Palettes.Count + "개 공급 클립 · " + candidate.Actions.Count + "개 typed 행동 바인딩\n편집 전체 바인딩 " + candidateBindingHash + (arena ? "\n다음 시작 " + arena.MotionBindingHash + "\n진행 본체 " + arena.Presentation.CurrentAttackMotionVersion : "") : "저장/적용 불가: " + error;
            rootVisualElement.Q<Label>("status").text = status ?? "";
            rootVisualElement.Q<Label>("diff").text = Diff();
            var slider = rootVisualElement.Q<Slider>("time"); slider.highValue = timing?.End ?? 1; slider.SetValueWithoutNotify(Mathf.Clamp(slider.value, 0, slider.highValue)); slider.SetEnabled(timing != null);
            RefreshSample();
        }
        string Diff()
        {
            if (selected == null) return "";
            var before = Delegated ? savedFirst?.steps : savedRegistry?.motions?.FirstOrDefault(value => value.id == selected.MotionId)?.steps;
            var after = Steps; if (before == null || after == null) return "고정 매체/프레임 원본은 읽기 전용입니다.";
            var text = new StringBuilder();
            float oldImpact = Delegated ? savedFirst?.sourceImpact ?? 0 : savedRegistry?.motions?.FirstOrDefault(value => value.id == selected.MotionId)?.sourceImpact ?? 0;
            float oldCue = Delegated ? savedFirst?.fxStart ?? -1 : savedRegistry?.motions?.FirstOrDefault(value => value.id == selected.MotionId)?.fxStart ?? -1;
            float impact = Delegated ? draft.firstMotion.sourceImpact : Clip.sourceImpact, cue = Delegated ? draft.firstMotion.fxStart : Clip.fxStart;
            if (oldImpact != impact) text.AppendLine("원본 접촉 " + oldImpact.ToString("R") + " → " + impact.ToString("R") + " s");
            if (oldCue != cue) text.AppendLine("원본 Cue " + oldCue.ToString("R") + " → " + cue.ToString("R") + " s");
            foreach (var step in after)
            {
                var old = before.FirstOrDefault(value => value.id == step.id);
                if (old == null) text.AppendLine("추가 " + step.id + " · " + step.frame);
                else if (old.frame != step.frame || old.seconds != step.seconds || Array.IndexOf(before, old) != Array.IndexOf(after, step)) text.AppendLine(step.id + ": " + old.frame + " " + old.seconds.ToString("R") + " → " + step.frame + " " + step.seconds.ToString("R") + " s");
            }
            foreach (var step in before) if (!after.Any(value => value.id == step.id)) text.AppendLine("제거 " + step.id);
            return text.Length == 0 ? "선택 스텝 원본 대비 변경 없음 (접촉/Cue 변경은 위 값/내용 해시로 표시)" : text.ToString();
        }
        void RefreshSample()
        {
            if (rootVisualElement.Q<Label>("preview") == null) return;
            if (timing == null) rootVisualElement.Q<Label>("preview").text = error ?? "유효한 후보를 선택하세요.";
            else
            {
                int frame = timing.FrameAt(rootVisualElement.Q<Slider>("time").value);
                var text = new StringBuilder(); text.AppendLine("공속 M=1 시작 매핑 · 판정/본체 끝 " + timing.Impact.ToString("R") + " / " + timing.End.ToString("R") + " s · 현재 " + frame + " " + timing.Frames[frame]);
                for (int index = 0; index < timing.Seconds.Count; index++) text.AppendLine(index + " · 원본 " + timing.Seconds[index].ToString("R") + " s → 실제 유지 " + (effectiveSeconds[index] * 1000).ToString("F3") + " ms");
                int loaded = previewSprites.Count(sprite => sprite); text.AppendLine("실제 공급 Sprite " + loaded + "/" + previewSprites.Length + " · Cue/피해/실행 없음");
                rootVisualElement.Q<Label>("preview").text = text.ToString();
            }
            canvas?.MarkDirtyRepaint();
        }
        void DrawBody()
        {
            var bounds = GUILayoutUtility.GetRect(1, 280, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bounds, new Color(.12f, .14f, .16f));
            if (timing == null || previewSprites.Length == 0) return;
            var sprite = previewSprites[timing.FrameAt(rootVisualElement.Q<Slider>("time").value)];
            if (!sprite) { GUI.Label(bounds, "실제 매체 누락 · 대체 그림 없음"); return; }
            var rect = sprite.rect; float zoom = Math.Min((bounds.width - 40) / rect.width, (bounds.height - 30) / rect.height);
            var destination = new Rect(bounds.center.x - sprite.pivot.x * zoom, bounds.yMax - 15 - (rect.height - sprite.pivot.y) * zoom, rect.width * zoom, rect.height * zoom);
            var matrix = GUI.matrix;
            if (rootVisualElement.Q<Toggle>("left").value) GUIUtility.ScaleAroundPivot(new Vector2(-1, 1), new Vector2(bounds.center.x, bounds.center.y));
            try { GUI.DrawTextureWithTexCoords(destination, sprite.texture, new Rect(rect.x / sprite.texture.width, rect.y / sprite.texture.height, rect.width / sprite.texture.width, rect.height / sprite.texture.height)); }
            finally { GUI.matrix = matrix; }
        }
        static string Label(MotionActionBinding binding) => binding.Key + " · " + binding.RequestedForm + "/" + binding.RequestedClip + (binding.IsFallback ? " [대체 읽기 전용]" : "");
        static bool Expected(Exception exception) => exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException || exception is System.ComponentModel.Win32Exception;
    }
}
