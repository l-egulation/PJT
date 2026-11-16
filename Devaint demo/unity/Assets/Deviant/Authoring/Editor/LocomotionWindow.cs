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
    public sealed class LocomotionWindow : EditorWindow
    {
        public const string LayoutPath = "Assets/Deviant/Authoring/Editor/LocomotionWindow.uxml";
        [SerializeField] LocomotionDraft edits;
        LocomotionData draft => edits ? edits.data : null;
        [SerializeField] LocomotionData savedData;
        [SerializeField] string sourcePath, generatedPath, sourceFileHash, savedContentHash;
        [SerializeField] CombatArena arena;
        string status;

        [MenuItem("Tools/deviant/Experiments/Locomotion")]
        public static void Open() => GetWindow<LocomotionWindow>("Movement experiment");

        void OnEnable() { Undo.undoRedoPerformed += RefreshFields; }
        void OnDisable() { Undo.undoRedoPerformed -= RefreshFields; }
        void OnDestroy() { if (edits) DestroyImmediate(edits); }
        void OnInspectorUpdate() { RefreshState(); }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (!layout) { rootVisualElement.Add(new Label("LocomotionWindow.uxml is missing.")); return; }
            layout.CloneTree(rootVisualElement);
            var target = rootVisualElement.Q<ObjectField>("runtime");
            target.objectType = typeof(CombatArena); target.allowSceneObjects = true;
            target.RegisterValueChangedCallback(e => { arena = e.newValue as CombatArena; RefreshState(); });
            Bind("runSpeed", (d, v) => d.runSpeed = v);
            Bind("dashSpeed", (d, v) => d.dashSpeed = v);
            Bind("dashDuration", (d, v) => d.dashDuration = v);
            Bind("dashHoldFraction", (d, v) => Curve(d).holdFraction = v);
            Bind("dashEaseOutPower", (d, v) => Curve(d).easeOutPower = v);
            Bind("dashRecovery", (d, v) => d.dashRecovery = v);
            Bind("dashInvulnerability", (d, v) => d.dashInvulnerability = v);
            Bind("dashJumpCarrySpeed", (d, v) => d.dashJumpCarrySpeed = v);
            Bind("dashJumpCarryDuration", (d, v) => d.dashJumpCarryDuration = v);
            Bind("firstJumpVelocity", (d, v) => d.firstJumpVelocity = v);
            Bind("secondJumpVelocity", (d, v) => d.secondJumpVelocity = v);
            Bind("riseGravity", (d, v) => d.riseGravity = v);
            Bind("fallGravity", (d, v) => d.fallGravity = v);
            rootVisualElement.Q<Button>("reload").clicked += Reload;
            rootVisualElement.Q<Button>("save").clicked += Save;
            rootVisualElement.Q<Button>("apply").clicked += Apply;
            if (draft == null)
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "data/combat/locomotion/player.json"))) directory = directory.Parent;
                if (directory != null)
                    LoadSource(Path.Combine(directory.FullName, "data/combat/locomotion/player.json"),
                        Path.Combine(Application.dataPath, "Deviant/Combat/Resources/PlayerLocomotion.json"));
                else status = "locomotion/player.json 원본을 찾지 못했습니다.";
            }
            RefreshFields();
        }

        // Explicit paths also let tests exercise the real window without touching repository data.
        public void LoadSource(string source, string generated)
        { sourcePath = source; generatedPath = generated; Reload(); }

        // An absent block means constant speed, so editing starts from that curve.
        static DashCurveData Curve(LocomotionData d) => d.dashCurve ?? (d.dashCurve = new DashCurveData { easeOutPower = 1 });

        void Bind(string name, Action<LocomotionData, float> set)
        {
            rootVisualElement.Q<FloatField>(name).RegisterValueChangedCallback(e => {
                if (draft == null) return;
                Undo.RecordObject(edits, "Edit locomotion");
                set(draft, e.newValue);
                status = null;
                RefreshState();
            });
        }

        void Reload()
        {
            try
            {
                var loaded = LocomotionSource.Load(sourcePath, out var hash);
                if (!edits) { edits = CreateInstance<LocomotionDraft>(); edits.hideFlags = HideFlags.HideAndDontSave; }
                Undo.RecordObject(edits, "Reload locomotion source");
                edits.data = loaded; sourceFileHash = hash;
                var profile = LocomotionSource.Validate(loaded);
                savedContentHash = profile.ContentHash; savedData = profile.ToData();
                status = "원본을 읽었습니다. 런타임 적용은 별도입니다.";
            }
            catch (Exception e) when (IsExpected(e)) { status = e.Message; }
            RefreshFields();
        }

        void Save()
        {
            bool sourceSaved = false;
            try
            {
                sourceFileHash = LocomotionSource.Save(sourcePath, sourceFileHash, draft);
                var profile = LocomotionSource.Validate(draft);
                savedContentHash = profile.ContentHash; savedData = profile.ToData();
                sourceSaved = true;
                LocomotionSource.Export(sourcePath, sourceFileHash, generatedPath);
                if (Path.GetFullPath(generatedPath).StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset("Assets/" + Path.GetRelativePath(Application.dataPath, generatedPath).Replace('\\', '/'));
                status = "원본 저장·생성물 갱신 완료. 런타임 적용은 별도입니다.";
            }
            catch (Exception e) when (IsExpected(e))
            { status = (sourceSaved ? "원본은 저장됐지만 생성물 갱신 실패: " : "저장 실패: ") + e.Message; }
            RefreshState();
        }

        void Apply()
        {
            if (!EditorApplication.isPlaying || !arena) return;
            status = arena.TryApplyMovementAndReset(draft, out var error)
                ? "리셋 적용 완료. 원본 파일은 변경하지 않았습니다." : error;
            RefreshState();
        }

        void RefreshFields()
        {
            if (draft == null || rootVisualElement.Q<FloatField>("runSpeed") == null) { RefreshState(); return; }
            rootVisualElement.Q<FloatField>("runSpeed").SetValueWithoutNotify(draft.runSpeed);
            rootVisualElement.Q<FloatField>("dashSpeed").SetValueWithoutNotify(draft.dashSpeed);
            rootVisualElement.Q<FloatField>("dashDuration").SetValueWithoutNotify(draft.dashDuration);
            rootVisualElement.Q<FloatField>("dashHoldFraction").SetValueWithoutNotify(draft.dashCurve?.holdFraction ?? 0);
            rootVisualElement.Q<FloatField>("dashEaseOutPower").SetValueWithoutNotify(draft.dashCurve?.easeOutPower ?? 1);
            rootVisualElement.Q<FloatField>("dashRecovery").SetValueWithoutNotify(draft.dashRecovery);
            rootVisualElement.Q<FloatField>("dashInvulnerability").SetValueWithoutNotify(draft.dashInvulnerability);
            rootVisualElement.Q<FloatField>("dashJumpCarrySpeed").SetValueWithoutNotify(draft.dashJumpCarrySpeed);
            rootVisualElement.Q<FloatField>("dashJumpCarryDuration").SetValueWithoutNotify(draft.dashJumpCarryDuration);
            rootVisualElement.Q<FloatField>("firstJumpVelocity").SetValueWithoutNotify(draft.firstJumpVelocity);
            rootVisualElement.Q<FloatField>("secondJumpVelocity").SetValueWithoutNotify(draft.secondJumpVelocity);
            rootVisualElement.Q<FloatField>("riseGravity").SetValueWithoutNotify(draft.riseGravity);
            rootVisualElement.Q<FloatField>("fallGravity").SetValueWithoutNotify(draft.fallGravity);
            RefreshState();
        }

        void RefreshState()
        {
            if (rootVisualElement.Q<Button>("apply") == null) return;
            if (EditorApplication.isPlaying && !arena)
            {
                var candidates = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None);
                if (candidates.Length == 1) arena = candidates[0];
            }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena);
            bool valid = LocomotionProfile.TryCreate(draft, out var compiled, out var error);
            rootVisualElement.Q<Button>("apply").SetEnabled(valid && EditorApplication.isPlaying && arena && arena.Player);
            rootVisualElement.Q<Button>("save").SetEnabled(valid && !string.IsNullOrEmpty(sourceFileHash));
            rootVisualElement.Q<Label>("source").text = sourcePath ?? "원본 경로 없음";
            rootVisualElement.Q<Label>("state").text = valid
                ? (compiled.ContentHash == savedContentHash ? "저장된 원본과 동일" : "미저장 변경") +
                    $" · 예상 대시 거리 {compiled.ExpectedDashDistance:0.###} unit · 출발 속도 {compiled.DashPeakSpeed:0.#} unit/s\n편집 버전 {compiled.ContentHash.Substring(0, 12)}" +
                    (arena && arena.Movement != null ? $" · 실행 버전 {arena.Movement.ContentHash.Substring(0, 12)}" : " · 실행 중인 전투 없음")
                : "적용·저장 불가: " + error;
            rootVisualElement.Q<Label>("status").text = status ?? "";
            var diff = new StringBuilder();
            if (draft != null && savedData != null)
            {
                Compare(diff, "일반 이동", savedData.runSpeed, draft.runSpeed);
                Compare(diff, "대시 속도", savedData.dashSpeed, draft.dashSpeed);
                Compare(diff, "대시 시간", savedData.dashDuration, draft.dashDuration);
                Compare(diff, "대시 최고속 유지", savedData.dashCurve?.holdFraction ?? 0, draft.dashCurve?.holdFraction ?? 0);
                Compare(diff, "대시 감속 곡선", savedData.dashCurve?.easeOutPower ?? 1, draft.dashCurve?.easeOutPower ?? 1);
                Compare(diff, "대시 회복", savedData.dashRecovery, draft.dashRecovery);
                Compare(diff, "대시 무적", savedData.dashInvulnerability, draft.dashInvulnerability);
                Compare(diff, "점프 취소 수평 속도", savedData.dashJumpCarrySpeed, draft.dashJumpCarrySpeed);
                Compare(diff, "점프 취소 유지", savedData.dashJumpCarryDuration, draft.dashJumpCarryDuration);
                Compare(diff, "첫 점프 수직속도", savedData.firstJumpVelocity, draft.firstJumpVelocity);
                Compare(diff, "두 번째 점프 수직속도", savedData.secondJumpVelocity, draft.secondJumpVelocity);
                Compare(diff, "상승 중력", savedData.riseGravity, draft.riseGravity);
                Compare(diff, "하강 중력", savedData.fallGravity, draft.fallGravity);
            }
            rootVisualElement.Q<Label>("diff").text = diff.Length == 0 ? "원본 대비 수치 변경 없음" : diff.ToString();
        }

        static void Compare(StringBuilder diff, string label, float before, float after)
        {
            if (!before.Equals(after)) diff.AppendLine($"{label}: {before:R} → {after:R}");
        }

        static bool IsExpected(Exception e) => e is IOException || e is UnauthorizedAccessException || e is ArgumentException;
    }
}
