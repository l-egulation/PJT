using System;
using System.Text;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class LabExecutionWindow : EditorWindow
    {
        [SerializeField] CombatArena arena;
        LabExecutionController controller;
        string status = "초기화된 Lab 전투를 명시적으로 선택하고 수동 실행을 연결하세요.";
        double nextRefresh;
        readonly StringBuilder metrics = new StringBuilder(512);
        public LabExecutionController Controller => controller;

        [MenuItem("Tools/deviant/Experiments/Lab Execution")]
        public static void Open() => GetWindow<LabExecutionWindow>("Lab Execution");

        void OnEnable() => EditorApplication.update += OnEditorUpdate;
        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            ReleaseRuntime();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Deviant/Authoring/Editor/LabExecutionWindow.uxml").CloneTree(rootVisualElement);
            var target = rootVisualElement.Q<ObjectField>("runtime");
            target.objectType = typeof(CombatArena); target.allowSceneObjects = true;
            target.RegisterValueChangedCallback(e =>
            {
                ReleaseRuntime();
                arena = e.newValue as CombatArena;
                status = "선택만으로 전투를 변경하지 않습니다. 수동 실행 연결을 눌러 시작하세요.";
                Refresh();
            });
            rootVisualElement.Q<Button>("bind").clicked += () => Execute(() => BindRuntime(arena));
            rootVisualElement.Q<Button>("release").clicked += ReleaseRuntime;
            rootVisualElement.Q<Button>("run").clicked += () => Execute(() =>
            {
                var input = Input(CombatButtons.None);
                controller.Run(); controller.SetContinuousInput(input.Axis, input.SpecialHeld);
                status = "로컬 실행 중 · 고정 dt는 변경하지 않습니다.";
            });
            rootVisualElement.Q<Button>("pause").clicked += () => Execute(() => { controller.Pause(); ClearInputDraft(); status = "이 창의 요청으로 일시정지했습니다."; });
            rootVisualElement.Q<Button>("step").clicked += () => Execute(() => SingleTick(default));
            rootVisualElement.Q<Button>("reset").clicked += () => Execute(() => { controller.Reset(); ClearInputDraft(); status = "실험을 새 epoch로 리셋했습니다. 외부 제어 요청은 유지됩니다."; });
            rootVisualElement.Q<Slider>("slowFactor").RegisterValueChangedCallback(e => Execute(() => controller.SlowFactor = e.newValue));
            rootVisualElement.Q<Toggle>("visualOutput").RegisterValueChangedCallback(e => Execute(() => controller.SetVisualOutput(e.newValue)));
            var direction = rootVisualElement.Q<DropdownField>("axis");
            direction.choices = new System.Collections.Generic.List<string> { "왼쪽", "없음", "오른쪽" };
            direction.SetValueWithoutNotify("없음");
            rootVisualElement.Q<DropdownField>("axis").RegisterValueChangedCallback(_ => UpdateContinuousInput());
            rootVisualElement.Q<Toggle>("specialHeld").RegisterValueChangedCallback(_ => UpdateContinuousInput());
            BindAction("basic", CombatButtons.Basic);
            BindAction("special", CombatButtons.Special);
            BindAction("dash", CombatButtons.Dash);
            BindAction("jump", CombatButtons.Jump);
            BindAction("drop", CombatButtons.Drop);
            BindAction("swap", CombatButtons.Swap);
            rootVisualElement.Q<Button>("releaseSpecial").clicked += () => Execute(() =>
            {
                rootVisualElement.Q<Toggle>("specialHeld").SetValueWithoutNotify(false);
                var input = Input(CombatButtons.None, CombatButtons.Special);
                if (controller.IsPaused) SingleTick(input);
                else
                {
                    controller.SetContinuousInput(input.Axis, false);
                    controller.QueueButtons(CombatButtons.None, CombatButtons.Special);
                    status = "특수 해제를 다음 실제 tick에 예약했습니다.";
                }
            });
            Refresh();
        }

        public void BindRuntime(CombatArena target)
        {
            if (controller != null && !controller.IsDisposed && controller.Arena == target) return;
            ReleaseRuntime();
            arena = target;
            controller = new LabExecutionController(target);
            status = "수동 실행 연결 · 일시정지. 다른 수동 실행 도구는 연결 해제 후 사용하세요.";
            Refresh();
        }

        public void ReleaseRuntime()
        {
            controller?.Dispose(); controller = null;
            ClearInputDraft();
            status = "수동 실행 연결 해제 · 이 창의 요청과 시각 출력 설정만 복구했습니다.";
            Refresh();
        }

        void BindAction(string name, CombatButtons button)
        {
            rootVisualElement.Q<Button>(name).clicked += () => Execute(() =>
            {
                var input = Input(button);
                if (controller.IsPaused) SingleTick(input);
                else
                {
                    controller.SetContinuousInput(input.Axis, input.SpecialHeld);
                    status = controller.QueueButtons(button)
                        ? "입력을 다음 실제 tick에 예약했습니다. 행동 성공을 의미하지는 않습니다."
                        : "외부 정지 또는 전투 입력 제한으로 입력을 폐기했습니다.";
                }
            });
        }

        CombatInput Input(CombatButtons buttons, CombatButtons released = CombatButtons.None)
        {
            int axis = rootVisualElement.Q<DropdownField>("axis").index - 1;
            bool held = rootVisualElement.Q<Toggle>("specialHeld").value;
            return new CombatInput(axis, buttons, held, released);
        }

        void SingleTick(CombatInput input)
        {
            if (input.Pressed == CombatButtons.None && input.Released == CombatButtons.None) input = Input(CombatButtons.None);
            bool advanced = controller.SingleTick(input);
            ClearInputDraft();
            status = advanced
                ? "실제 전투 1 tick 진행 · 이 창은 다시 일시정지 상태입니다."
                : "다른 소유자의 시뮬레이션 정지가 유지되어 tick을 진행하지 않았습니다.";
        }

        void UpdateContinuousInput() => Execute(() =>
        {
            var input = Input(CombatButtons.None);
            controller.SetContinuousInput(input.Axis, input.SpecialHeld);
        });

        void ClearInputDraft()
        {
            rootVisualElement.Q<DropdownField>("axis")?.SetValueWithoutNotify("없음");
            rootVisualElement.Q<Toggle>("specialHeld")?.SetValueWithoutNotify(false);
        }

        void Execute(Action operation)
        {
            try { operation(); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is ObjectDisposedException)
            { status = error.Message; }
            Refresh();
        }

        void OnEditorUpdate()
        {
            if (controller != null && controller.IsDisposed)
            {
                controller = null;
                ClearInputDraft();
                status = "Play Mode 종료 또는 Lab 비활성화로 수동 실행 연결을 정리했습니다.";
            }
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .1;
            Refresh();
        }

        public void Refresh()
        {
            var root = rootVisualElement;
            var target = root.Q<ObjectField>("runtime");
            if (target == null) return;
            target.SetValueWithoutNotify(arena);
            bool bound = controller != null && !controller.IsDisposed;
            bool paused = bound && controller.IsPaused;
            root.Q<Button>("bind").SetEnabled(!bound && Application.isPlaying && arena && arena.Player &&
                arena.isActiveAndEnabled && arena.CompositionProfile == CombatCompositionProfile.Lab && !arena.ManualSimulationActive);
            root.Q<Button>("release").SetEnabled(bound);
            root.Q<Button>("run").SetEnabled(paused);
            root.Q<Button>("pause").SetEnabled(bound && !paused);
            root.Q<Button>("step").SetEnabled(paused);
            root.Q<Button>("reset").SetEnabled(bound);
            root.Q<VisualElement>("localSettings").SetEnabled(bound);
            root.Q<VisualElement>("inputs").SetEnabled(bound);
            if (bound)
            {
                root.Q<Slider>("slowFactor").SetValueWithoutNotify(controller.SlowFactor);
                root.Q<Toggle>("visualOutput").SetValueWithoutNotify(controller.VisualOutputEnabled);
                if (!paused)
                {
                    root.Q<DropdownField>("axis").SetValueWithoutNotify(controller.Axis < 0 ? "왼쪽" : controller.Axis > 0 ? "오른쪽" : "없음");
                    root.Q<Toggle>("specialHeld").SetValueWithoutNotify(controller.SpecialHeld);
                }
            }
            root.Q<Label>("clock").text = bound
                ? $"{(paused ? "이 창 일시정지" : controller.IsExternallyPaused ? "외부 소유자 일시정지" : "실행 중")} · 로컬 속도 {controller.SlowFactor:0.00}x · 기존 고정 dt {controller.FixedStep:R}s\n보존 backlog {controller.BacklogSeconds:0.000000}s · 콜백당 최대 {LabExecutionController.MaximumTicksPerUpdate} tick · 최근 콜백 {controller.LastUpdateTicks} tick"
                : "수동 실행 미연결";
            metrics.Clear();
            if (bound && controller.TryReadPlayer(out var player))
            {
                metrics.Append("epoch ").Append(player.Handle.Session.Epoch).Append(" / generation ").Append(player.Handle.Session.Generation)
                    .Append(" · tick ").Append(player.TickCount).Append(" · clock ").Append(player.Now.ToString("R")).Append("s")
                    .Append("\nPlayer HP ").Append(player.Vitals.Health).Append('/').Append(player.Vitals.Maximum)
                    .Append(" · shield ").Append(player.Vitals.Shield).Append(" · received hits ").Append(player.Vitals.ReceivedHits)
                    .Append("\n위치 (").Append(player.Kinematics.X.ToString("0.000")).Append(", ").Append(player.Kinematics.Y.ToString("0.000"))
                    .Append(") · 연결/리셋 기준 변위 (").Append((player.Kinematics.X - controller.OriginX).ToString("0.000")).Append(", ")
                    .Append((player.Kinematics.Y - controller.OriginY).ToString("0.000")).Append(')')
                    .Append("\n행동 ").Append(player.Action.ContentId ?? "없음").Append(" / ").Append(player.Action.Phase)
                    .Append("\n행동 정의 버전 ").Append(player.Action.DefinitionVersion ?? "진행 중 행동 없음")
                    .Append("\n시간표 버전 ").Append(player.Action.TimelineVersion ?? "없음")
                    .Append(" · 프레임 버전 ").Append(player.Action.FrameTimingVersion ?? "없음");
                for (int i = 0; i < controller.TargetCount; i++)
                {
                    metrics.Append('\n').Append(controller.TargetName(i));
                    if (controller.TryReadTarget(i, out var enemy))
                        metrics.Append(" HP ").Append(enemy.Vitals.Health).Append('/').Append(enemy.Vitals.Maximum)
                            .Append(" · received hits ").Append(enemy.Vitals.ReceivedHits);
                    else metrics.Append(" · 현재 세션에서 읽을 수 없음");
                }
            }
            else metrics.Append("초기화/리셋 경계에 바인딩한 읽기 전용 전투 지표가 없습니다.");
            root.Q<Label>("metrics").text = metrics.ToString();
            root.Q<Label>("status").text = bound && controller.LastError != null ? controller.LastError : status;
            root.Q<Label>("outputState").text = !bound || controller.VisualOutputEnabled
                ? "시각 출력 켜짐 · 소리와 카메라 추적은 별도이며 이 창이 변경하지 않습니다."
                : controller.VisualOutputOverridden
                    ? "다른 소유자가 카메라 출력을 변경했습니다. 연결 해제 시 그 값을 덮어쓰지 않습니다."
                    : "선택 Lab 카메라의 시각 출력만 꺼짐 · 몸/새 Cue/투사체 표시를 숨겨도 표현 수명과 전투 판정은 계속 실행됩니다.";
        }
    }
}
