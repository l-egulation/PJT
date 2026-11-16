using System;
using System.Linq;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // 잉크 파츠 이펙트 시험용 개발 단축키(에디터·개발 빌드 전용): F9 를 누를 때마다 발동 조건별 파츠 세트를 두 무기에 바로 끼운다.
    // 잉크를 얻고 정비소에서 끼우는 과정을 건너뛰고 Chapter1InkPartFx 의 이펙트를 확인하려는 것이다. 세트마다 새 5칸 팔레트를 묶는다.
    public sealed class Chapter1InkFxTrialKeys : MonoBehaviour
    {
        static readonly (string name, string[] parts)[] Sets =
        {
            ("적중 시", new[] { "ink3_red_01", "ink3_red_02", "ink3_blue_04", "ink3_purple_01", "ink3_purple_05" }),
            ("무기 교체 시", new[] { "ink3_red_03", "ink3_yellow_04", "ink3_green_02", "ink3_blue_02", "ink3_green_01" }),
            ("표식을 터트릴 시", new[] { "ink3_red_04", "ink3_yellow_05", "ink3_blue_05", "ink3_purple_06", "ink3_purple_05" }),
            ("몬스터 처치 시", new[] { "ink3_red_05", "ink3_red_06", "ink3_yellow_06", "ink3_green_04", "ink3_blue_06" }),
            ("이동, 상시", new[] { "ink3_yellow_01", "ink3_yellow_02", "ink3_yellow_03", "ink3_blue_01" }),
            ("속박, 기절, 피격", new[] { "ink3_purple_01", "ink3_purple_02", "ink3_purple_03", "ink3_purple_04", "ink3_green_03" }),
            ("체력 상태", new[] { "ink3_green_05", "ink3_green_06", "ink3_green_01", "ink3_green_02", "ink3_red_01" }),
            ("무지개 표식 색", new[] { "ink3_red_07", "ink3_yellow_08", "ink3_green_07", "ink3_blue_07", "ink3_purple_07" }),
            ("표식 연쇄, 독", new[] { "ink3_yellow_09", "ink3_purple_08", "ink3_purple_09", "ink3_purple_05", "ink3_red_04" }),
            ("추가 파츠", new[] { "ink3_red_08", "ink3_yellow_10", "ink3_green_08", "ink3_blue_08", "ink3_green_01" }),
            ("새 11종 1", new[] { "ink3_red_09", "ink3_red_10", "ink3_yellow_11", "ink3_yellow_12", "ink3_green_09" }),
            ("새 11종 2", new[] { "ink3_green_10", "ink3_blue_09", "ink3_blue_10", "ink3_blue_11", "ink3_purple_10" }),
            ("새 11종 3", new[] { "ink3_purple_11", "ink3_purple_01", "ink3_purple_03", "ink3_red_10", "ink3_yellow_03" }),
        };
        Chapter1RunBinding binding;
        public static string[] SetNames => Sets.Select(s => s.name).ToArray();
        string notice;
        float noticeUntil;

        public void Initialize(Chapter1RunBinding owner) => binding = owner;

        public void Apply(int index)
        {
            if (!DevFeatures.Enabled || !binding || index < 0 || index >= Sets.Length) return;
            var growth = binding.Run?.Growth; var player = binding.World && binding.World.arena ? binding.World.arena.Player : null;
            if (growth == null || !player) { Show("잉크 시험 세트: 이 런에는 잉크 성장이 없습니다"); return; }
            var set = Sets[index];
            try
            {
                // 두 무기 모두에 같은 세트: 교체·격발은 어느 쪽에서 일어나도 보이게
                foreach (var weapon in player.weapons.Take(2))
                {
                    var palette = growth.GrantPalette(set.parts.Select(id => growth.Data.parts.First(p => p.id == id).color).ToArray());
                    growth.Bind(palette.Id, weapon.InstanceId);
                    for (int i = 0; i < set.parts.Length; i++) growth.DebugSetPart(weapon.InstanceId, i, set.parts[i]);
                }
                Show("잉크 시험 세트: " + set.name + " (" + string.Join(", ", set.parts.Select(id => growth.Data.parts.First(p => p.id == id).name)) + ")");
            }
            catch (Exception e) { Show("잉크 시험 세트 실패: " + e.Message); Debug.LogException(e); }
        }

        void Show(string text) { notice = text; noticeUntil = Time.unscaledTime + 3; Debug.Log(text); }

        void OnGUI()
        {
            if (Time.unscaledTime > noticeUntil || string.IsNullOrEmpty(notice)) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width / 2f - 360, 40, 720, 40), notice, style);
        }
    }
}
