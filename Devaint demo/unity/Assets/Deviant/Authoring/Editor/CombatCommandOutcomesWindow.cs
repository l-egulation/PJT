using System;
using System.Text;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    // Polls detached receipts only. This window neither submits commands nor
    // acquires input/time control, and never discovers/rebinds another arena.
    public sealed class CombatCommandOutcomesWindow : EditorWindow
    {
        public const string LayoutPath = "Assets/Deviant/Authoring/Editor/CombatCommandOutcomesWindow.uxml";
        const int Capacity = 128;
        readonly CombatCommandOutcome[] page = new CombatCommandOutcome[Capacity];
        readonly CombatCommandOutcome[] retained = new CombatCommandOutcome[Capacity];
        ICombatCommands commands;
        CombatOutcomeCursor cursor;
        int start, count;
        long lost;
        [SerializeField] CombatArena arena;
        string status = "초기화된 Game 또는 Lab 전투를 지정하고 명시적으로 연결하세요.";
        public int OutcomeCount => count;
        public long LostOutcomes => lost;
        public CombatCommandOutcome OutcomeAt(int index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return retained[(start + index) % Capacity];
        }
        [MenuItem("Tools/deviant/Experiments/Combat Command Outcomes")]
        public static void Open() => GetWindow<CombatCommandOutcomesWindow>("Command Outcomes");
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath).CloneTree(rootVisualElement);
            var runtime = rootVisualElement.Q<ObjectField>("runtime"); runtime.objectType = typeof(CombatArena); runtime.allowSceneObjects = true;
            runtime.SetValueWithoutNotify(arena);
            runtime.RegisterValueChangedCallback(change => { Bind(null); arena = change.newValue as CombatArena; Refresh(); });
            rootVisualElement.Q<Button>("bind").clicked += () => Bind(arena);
            rootVisualElement.Q<Button>("stop").clicked += () => Bind(null);
            rootVisualElement.Q<Button>("poll").clicked += Poll;
            Refresh();
        }
        public void Bind(CombatArena runtime)
        {
            commands = null; start = count = 0; lost = 0; cursor = default;
            if (runtime && runtime.ReadModel.TryGetSession(out var session))
            {
                arena = runtime; commands = runtime.Commands; cursor = new CombatOutcomeCursor(session);
                status = "명령 결과 관측 중 · 입력 접수/소비와 별개 · reset 후 다시 연결하세요.";
            }
            else status = "관측 중지 · 새 전투나 세대에 자동 재연결하지 않습니다.";
            Refresh();
        }
        void OnInspectorUpdate() => Poll();
        void OnDisable() { commands = null; }
        public void Poll()
        {
            if (commands == null) return;
            if (!commands.TryReadOutcomes(cursor, page, out var result))
            {
                commands = null; status = "세션이 끝났습니다. 결과를 보존하며 명시적 재연결을 기다립니다."; Refresh(); return;
            }
            cursor = result.Next; lost += result.DroppedBefore;
            for (int i = 0; i < result.Count; i++)
            {
                if (count == Capacity) { start = (start + 1) % Capacity; count--; lost++; }
                retained[(start + count++) % Capacity] = page[i];
            }
            Refresh();
        }
        void Refresh()
        {
            var rows = rootVisualElement.Q<Label>("outcomes"); if (rows == null) return;
            rootVisualElement.Q<Label>("status").text = status;
            rootVisualElement.Q<Button>("bind").SetEnabled(arena && arena.ReadModel.TryGetSession(out _));
            rootVisualElement.Q<Button>("stop").SetEnabled(commands != null);
            rootVisualElement.Q<Button>("poll").SetEnabled(commands != null);
            var text = new StringBuilder().Append("결과 ").Append(count).Append("/128 · 유실 ").Append(lost)
                .Append("\nAdmitted=입력 접수, Consumed=중재 관측이며 이 결과 목록의 Started와 다릅니다.")
                .Append("\nStarted는 실제 시작, Completed는 해당 동작 경계 완료입니다. 적중/이동 거리/독립 효과 소멸 보장이 아닙니다.");
            for (int i = 0; i < count; i++)
            {
                var outcome = OutcomeAt(i); var key = outcome.Command;
                text.Append("\n#").Append(outcome.EventSequence).Append(" tick ").Append(outcome.TickCount).Append(" @").Append(outcome.Now.ToString("R"))
                    .Append(" actor ").Append(key.Actor.ActorId).Append(" gen ").Append(key.Actor.Session.Generation).Append(" epoch ").Append(key.Actor.Session.Epoch)
                    .Append(" command ").Append(key.Sequence).Append('/').Append(key.Kind).Append(' ').Append(outcome.State).Append('/').Append(outcome.Reason)
                    .Append(" execution ").Append(outcome.ExecutionId);
                if (outcome.Snapshot.Handle.ActorId > 0) text.Append(" hp ").Append(outcome.Snapshot.Vitals.Health.ToString("R"))
                    .Append(" xy ").Append(outcome.Snapshot.Kinematics.X.ToString("R")).Append(',').Append(outcome.Snapshot.Kinematics.Y.ToString("R"));
                if (outcome.Detail != null) text.Append(" · ").Append(outcome.Detail);
            }
            rows.text = text.ToString();
        }
    }
}
