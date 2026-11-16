using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat;
using Deviant.CombatLab;
using Deviant.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class SwordDashComparisonWindow : EditorWindow
    {
        [SerializeField] CombatArena arena;
        [SerializeField] LocomotionData movementA, movementB;
        [SerializeField] BasicMeleeHitData swordA, swordB;
        [SerializeField] bool hasA, hasB, frameModeA, frameModeB;
        [SerializeField] FrameTimingDraft frameA, frameB;
        string results = "", status = "";
        SwordDashInputSet replayInputs, lastInputs;
        string inputsPath, inputsFileHash;
        CombatInputRecorder liveInputs;
        CombatPlayer livePlayer;
        string liveDescription = "라이브 입력 미기록 · 최대 256개 완료 경계를 보관합니다.";
        public IReadOnlyList<CombatInputBatch> LiveInputBatches => liveInputs == null ? (IReadOnlyList<CombatInputBatch>)Array.Empty<CombatInputBatch>() : liveInputs.LiveBatches;
        [MenuItem("Tools/deviant/Experiments/Sword and Dash A-B")]
        public static void Open() => GetWindow<SwordDashComparisonWindow>("Sword / Dash A-B");
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Deviant/Authoring/Editor/SwordDashComparisonWindow.uxml").CloneTree(rootVisualElement);
            var target = rootVisualElement.Q<ObjectField>("runtime"); target.objectType = typeof(CombatArena); target.allowSceneObjects = true;
            target.RegisterValueChangedCallback(e => { arena = e.newValue as CombatArena; Refresh(); });
            rootVisualElement.Q<Button>("captureA").clicked += () => Capture(true);
            rootVisualElement.Q<Button>("captureB").clicked += () => Capture(false);
            rootVisualElement.Q<Button>("compare").clicked += Compare;
            rootVisualElement.Q<Button>("importInputs").clicked += ImportDialog;
            rootVisualElement.Q<Button>("exportInputs").clicked += ExportDialog;
            rootVisualElement.Q<Button>("clearInputs").clicked += ClearInputs;
            rootVisualElement.Q<Button>("startLiveInputs").clicked += StartLiveInputs;
            rootVisualElement.Q<Button>("stopLiveInputs").clicked += StopLiveInputs;
            Refresh();
        }
        void OnInspectorUpdate() => PollLiveInputs();
        void OnDisable() => StopLiveInputs();
        void OnDestroy() { liveInputs?.Dispose(); if (frameA) DestroyImmediate(frameA); if (frameB) DestroyImmediate(frameB); }
        public void StartLiveInputs()
        {
            if (!EditorApplication.isPlaying || !arena || !arena.Player) { status = "Game 또는 Lab의 초기화된 전투를 선택하세요."; Refresh(); return; }
            var player = arena.Player;
            var recorder = new CombatInputRecorder(player, 256);
            liveInputs?.Dispose();
            livePlayer = player;
            liveInputs = recorder;
            liveDescription = "라이브 접수/소비 관측 중 · 행동 성공 기록이 아닙니다.";
            Refresh();
        }
        public void StopLiveInputs()
        {
            if (liveInputs != null) { liveInputs.Poll(); liveInputs.Dispose(); PollLiveInputs(); }
            livePlayer = null;
            Refresh();
        }
        public void PollLiveInputs()
        {
            if (liveInputs == null) { Refresh(); return; }
            liveInputs.Poll();
            if (liveInputs.IsCapturing && (!EditorApplication.isPlaying || !arena || livePlayer != arena.Player))
            { liveInputs.Dispose(); livePlayer = null; }
            var batches = liveInputs.LiveBatches;
            var text = new StringBuilder().Append(liveInputs.IsCapturing ? "라이브 관측 중" : "라이브 관측 중지")
                .Append(" · 완료 경계 ").Append(batches.Count).Append("/256 · 유실 ").Append(liveInputs.LostLiveBatches)
                .Append("\n순번은 접수 순서, 시각은 장치 비스케일/수동 시뮬레이션 표본입니다. 같은 틱 edge는 기존 우선순위로 합칩니다.");
            int start = Math.Max(0, batches.Count - 8);
            for (int i = start; i < batches.Count; i++)
            {
                var batch = batches[i];
                text.Append("\nactor ").Append(batch.Session.ActorId).Append(" epoch ").Append(batch.Session.Epoch)
                    .Append(" gen ").Append(batch.Session.Generation).Append(" tick ").Append(batch.TickCount)
                    .Append(" @").Append(batch.Now.ToString("R")).Append(' ').Append(batch.Kind).Append('/').Append(batch.TickOutcome)
                    .Append(" through ").Append(batch.ObservedThroughNow.ToString("R"))
                    .Append(" axis ").Append(batch.Input.Axis).Append(" held ").Append(batch.Input.SpecialHeld);
                foreach (var receipt in batch.Receipts)
                {
                    var sample = receipt.Sample;
                    text.Append("\n  #").Append(sample.Sequence).Append(' ').Append(sample.Source).Append('/').Append(sample.DeviceId)
                        .Append(' ').Append(sample.TimeDomain).Append('@').Append(sample.Timestamp.ToString("R"))
                        .Append(" +").Append(sample.Input.Pressed).Append(" −").Append(sample.Input.Released)
                        .Append(' ').Append(receipt.Disposition);
                    if (receipt.Disposition == CombatInputDisposition.Discarded) text.Append('/').Append(receipt.DiscardReason);
                }
            }
            liveDescription = text.ToString();
            Refresh();
        }
        void Capture(bool a)
        {
            if (!Ready()) return;
            var version = SwordDashVersion.Capture(arena);
            var capturedFrame = a ? frameA : frameB;
            if (version.FrameTiming != null)
            {
                if (!capturedFrame) { capturedFrame = CreateInstance<FrameTimingDraft>(); capturedFrame.hideFlags = HideFlags.HideAndDontSave; }
                capturedFrame.Load(version.FrameTiming.Timeline, version.FrameTiming.SourceAction, version.FrameTiming.SourceMotion,
                    FrameAttackMotionSource.FromCandidate(version.FrameTiming), version.FrameTiming.InputSpeed);
            }
            else { if (capturedFrame) DestroyImmediate(capturedFrame); capturedFrame = null; }
            if (a) { movementA = version.Movement.ToData(); swordA = version.FrameTiming == null ? BasicMeleeHitData.From(version.Sword) : null; frameA = capturedFrame; frameModeA = version.FrameTiming != null; hasA = true; }
            else { movementB = version.Movement.ToData(); swordB = version.FrameTiming == null ? BasicMeleeHitData.From(version.Sword) : null; frameB = capturedFrame; frameModeB = version.FrameTiming != null; hasB = true; }
            results = ""; lastInputs = null; status = (a ? "A" : "B") + " 정의를 기록했습니다."; Refresh();
        }
        SwordDashVersion Version(bool a)
        {
            var sword = a ? swordA : swordB; var movement = a ? movementA : movementB;
            if (!LocomotionProfile.TryCreate(movement, out var profile, out var error)) throw new ArgumentException(error);
            if (a ? frameModeA : frameModeB)
            {
                var captured = a ? frameA : frameB;
                if (!captured) throw new ArgumentException("Captured frame definition is unavailable; capture that version again.");
                var frame = captured.CompileCandidate(); return new SwordDashVersion(frame.Action, profile, frame);
            }
            if (!BasicMeleeDefinition.TryCreate("base_sword_basic_0", sword?.ToData(), out var basic, out error)) throw new ArgumentException(error);
            return new SwordDashVersion(basic, profile);
        }
        void Compare()
        {
            results = ""; lastInputs = null;
            try
            {
                var pair = SwordDashComparison.Run(arena, Version(true), Version(false), replayInputs);
                lastInputs = new SwordDashInputSet(pair.A.AttackInputs, pair.A.WallInputs);
                results = Describe("A", pair.A) + "\n" + Describe("B", pair.B) +
                    $"\nB−A: HP 감소 {pair.B.HealthLost - pair.A.HealthLost:0.###}, 이동 {pair.B.Travel - pair.A.Travel:0.###}, 벽 앞 이동 {pair.B.WallTravel - pair.A.WallTravel:0.###}" +
                    $"\n동일 입력 재생: 공격/대시 {pair.A.AttackInputs.TickCount}틱 {pair.A.AttackInputs.ContentHash.Substring(0, 12)} · 벽 {pair.A.WallInputs.TickCount}틱 {pair.A.WallInputs.ContentHash.Substring(0, 12)}";
                status = "비교 완료 · 기존 장비/정의/실행 모드로 전투 리셋 · 원본 저장 없음";
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { results = ""; lastInputs = null; status = e.Message; }
            Refresh();
        }
        public void LoadInputs(string path)
        {
            var loaded = SwordDashInputSource.Load(path, out var hash); // Validate both segments before changing selection.
            replayInputs = loaded; lastInputs = null; inputsPath = Path.GetFullPath(path); inputsFileHash = hash; results = "";
            status = "입력 기록을 불러왔습니다 · A/B 수치는 별도로 기록합니다."; Refresh();
        }
        public string SaveInputs(string path, string expectedFileHash)
        {
            var inputs = lastInputs ?? replayInputs ?? throw new InvalidOperationException("비교를 실행하거나 입력 기록을 먼저 불러오세요.");
            string hash = SwordDashInputSource.Save(path, expectedFileHash, inputs);
            inputsPath = Path.GetFullPath(path); inputsFileHash = hash; status = "입력 기록만 내보냈습니다 · 월드/장비 상태는 저장하지 않습니다."; Refresh(); return hash;
        }
        public void ClearInputs()
        {
            replayInputs = lastInputs = null; inputsPath = inputsFileHash = null; results = "";
            status = "기본 실험 입력으로 돌아왔습니다."; Refresh();
        }
        string InputDirectory => string.IsNullOrEmpty(inputsPath) ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs")) : Path.GetDirectoryName(inputsPath);
        void ImportDialog()
        {
            string path = EditorUtility.OpenFilePanel("실험 입력 불러오기", InputDirectory, "json");
            if (!string.IsNullOrEmpty(path)) InputAction(() => LoadInputs(path));
        }
        void ExportDialog()
        {
            string path = EditorUtility.SaveFilePanel("실험 입력 내보내기", InputDirectory, "lab-sword-dash-inputs.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            InputAction(() => {
                string expected = !string.IsNullOrEmpty(inputsPath) && string.Equals(Path.GetFullPath(path), inputsPath, StringComparison.OrdinalIgnoreCase) ? inputsFileHash : null;
                if (expected == null && File.Exists(path)) SwordDashInputSource.Load(path, out expected);
                SaveInputs(path, expected);
            });
        }
        void InputAction(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException) { status = e.Message; Refresh(); }
        }
        static string Describe(string name, SwordDashMetrics result)
        {
            string hit = result.FirstHitTick < 0 ? "적중 없음" : $"{result.FirstHitTick * SwordDashComparison.Step:0.00}–{(result.FirstHitTick + 1) * SwordDashComparison.Step:0.00}s";
            return $"{name}: HP 감소 {result.HealthLost:0.###} · 첫 적중 {hit} · 이동 {result.Travel:0.###} · 벽 앞 이동 {result.WallTravel:0.###}";
        }
        bool Ready() => EditorApplication.isPlaying && arena && arena.Player && !arena.ManualSimulationActive && arena.CompositionProfile == CombatCompositionProfile.Lab &&
            arena.gameObject.scene.path == "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity";
        void Refresh()
        {
            if (rootVisualElement.Q<Button>("compare") == null) return;
            if (!arena && EditorApplication.isPlaying)
            {
                var candidates = UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None);
                if (candidates.Length == 1) arena = candidates[0];
            }
            rootVisualElement.Q<ObjectField>("runtime").SetValueWithoutNotify(arena);
            rootVisualElement.Q<Button>("captureA").SetEnabled(Ready());
            rootVisualElement.Q<Button>("captureB").SetEnabled(Ready());
            rootVisualElement.Q<Button>("compare").SetEnabled(Ready() && hasA && hasB);
            rootVisualElement.Q<Button>("exportInputs").SetEnabled(lastInputs != null || replayInputs != null);
            rootVisualElement.Q<Button>("clearInputs").SetEnabled(replayInputs != null || lastInputs != null);
            rootVisualElement.Q<Label>("inputs").text = replayInputs == null ? "재생 입력: 기본 시나리오 · 비교 시 A 입력을 기록합니다." :
                $"재생 입력: 불러온 기록 · 공격 {replayInputs.Attack.ContentHash.Substring(0, 12)} · 벽 {replayInputs.Wall.ContentHash.Substring(0, 12)}";
            try
            {
                rootVisualElement.Q<Label>("versions").text = Identity("A", hasA, true) + "\n" + Identity("B", hasB, false);
            }
            catch (ArgumentException e) { rootVisualElement.Q<Label>("versions").text = e.Message; rootVisualElement.Q<Button>("compare").SetEnabled(false); }
            rootVisualElement.Q<Label>("results").text = results;
            rootVisualElement.Q<Label>("status").text = status;
            rootVisualElement.Q<Button>("startLiveInputs").SetEnabled(EditorApplication.isPlaying && arena && arena.Player && (liveInputs == null || !liveInputs.IsCapturing));
            rootVisualElement.Q<Button>("stopLiveInputs").SetEnabled(liveInputs != null && liveInputs.IsCapturing);
            rootVisualElement.Q<Label>("liveInputs").text = liveDescription;
        }
        string Identity(string name, bool exists, bool a)
        {
            if (!exists) return name + ": 미기록";
            var version = Version(a);
            return name + ": " + (version.FrameTiming == null ? "고정" : "프레임 " + version.FrameTiming.ContentHash.Substring(0, 12)) + " · 검 " + version.Sword.ContentHash.Substring(0, 12) + " · 이동 " + version.Movement.ContentHash.Substring(0, 12);
        }
    }
}
