using System;
using System.Linq;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Playtest
{
    public sealed partial class GameSandbox
    {
        int tab, weaponSlot, paletteSlot, colorFilter;
        string search = "", hpText = "100", basicText = "1", skillText = "1", speedText = "1", cooldownText = "-1";
        Vector2 inkScroll, roomScroll;
        Font font;
        GUIStyle label, heading, button, selected, field, small;
        GameObject pointerBlocker;
        static readonly string[] ColorIds = { "", "red", "yellow", "green", "blue", "purple" };
        static readonly string[] ColorNames = { "전체", "빨강", "노랑", "초록", "파랑", "보라" };
        static readonly Color Paper = new Color32(27, 35, 40, 255), Panel = new Color32(40, 52, 58, 255), Accent = new Color32(145, 225, 204, 255);

        void SetPointerBlocker(bool on)
        {
            if (on && !pointerBlocker)
            {
                pointerBlocker = new GameObject("Sandbox pointer blocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                pointerBlocker.transform.SetParent(transform, false);
                var canvas = pointerBlocker.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
                var image = new GameObject("Block game UI", typeof(RectTransform), typeof(Image)); image.transform.SetParent(pointerBlocker.transform, false);
                var rect = image.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                image.GetComponent<Image>().color = Color.clear; image.GetComponent<Image>().raycastTarget = true;
            }
            if (pointerBlocker) pointerBlocker.SetActive(on);
        }

        void RefreshTuningText()
        {
            var setup = Weapons[weaponSlot];
            basicText = setup.Basic; skillText = setup.Skill; speedText = setup.Speed; cooldownText = setup.Cooldown;
        }
        void Styles()
        {
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 16);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true, normal = { textColor = Color.white } };
            small = new GUIStyle(label) { fontSize = 13, normal = { textColor = new Color(.7f, .78f, .8f) } };
            heading = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15, wordWrap = true, padding = new RectOffset(8, 8, 4, 4) };
            selected = new GUIStyle(button) { normal = { textColor = Accent }, fontStyle = FontStyle.Bold };
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 17, padding = new RectOffset(8, 8, 6, 4) };
        }
        static void Fill(Rect rect, Color color)
        { var previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous; }
        bool Button(float x, float y, float w, float h, string text, bool active = false)
            => GUI.Button(new Rect(x, y, w, h), text, active ? selected : button);
        void Label(float x, float y, float w, float h, string text, bool muted = false)
            => GUI.Label(new Rect(x, y, w, h), text, muted ? small : label);

        void OnGUI()
        {
            var matrix = GUI.matrix; int depth = GUI.depth;
            try
            {
                Styles(); GUI.depth = -1000;
                float scale = Mathf.Min(Screen.width / 1200f, Screen.height / 760f);
                GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1200 * scale) / 2, (Screen.height - 760 * scale) / 2), Quaternion.identity, Vector3.one * scale);
                if (Loading) { Fill(new Rect(300, 330, 600, 90), Paper); Label(330, 352, 540, 50, "테스트 방을 준비하고 있습니다…"); return; }
                if (!Arena) return;
                if (!IsOpen)
                {
                    Fill(new Rect(384, 8, 432, 40), Paper);
                    if (Button(390, 12, 240, 32, "테스트 설정 열기  [F3]")) SetOpen(true);
                    if (Button(638, 12, 172, 32, "현재 방 다시 시작")) RestartRoom();
                    return;
                }
                Fill(new Rect(-4000, -4000, 10000, 10000), new Color(0, 0, 0, .85f));
                Fill(new Rect(0, 0, 1200, 760), Paper);
                GUI.Label(new Rect(26, 20, 750, 32), "게임 테스트 환경", heading);
                Label(26, 55, 900, 24, "전투 일시정지 / 일반 진행 저장과 분리 / 설정은 이번 실행 동안만 유지", true);
                if (Button(958, 20, 216, 44, "설정 닫고 전투  [F3]")) SetOpen(false);
                string[] tabs = { "장비 / 런 잉크", "전투 수치", "방 바로 이동" };
                for (int i = 0; i < tabs.Length; i++) if (Button(26 + i * 384, 93, 372, 42, tabs[i], tab == i)) tab = i;
                Fill(new Rect(20, 148, 1160, 548), Panel);
                if (tab < 2)
                {
                    for (int i = 0; i < 2; i++)
                        if (Button(36 + i * 250, 160, 238, 36, $"무기 {i + 1} / {Arena.catalog.forms[Arena.Player.weapons[i].FormIndex].name}", weaponSlot == i))
                        { weaponSlot = i; paletteSlot = 0; RefreshTuningText(); }
                    if (tab == 0) DrawEquipment(); else DrawStats();
                }
                else DrawRooms();
                Label(28, 710, 1140, 38, Message);
            }
            finally { GUI.matrix = matrix; GUI.depth = depth; }
        }

        void DrawEquipment()
        {
            var setup = Weapons[weaponSlot];
            string kind = Chapter1Rewards.BlueprintKindOf(setup.Form);
            Label(36, 212, 300, 24, "도면 종류");
            string[] kinds = { "sword", "blunt", "ranged" }, names = { "검", "둔기", "원거리" }, bases = { "base_sword", "hammer", "launcher" };
            for (int i = 0; i < kinds.Length; i++) if (Button(36 + i * 122, 242, 114, 34, names[i], kind == kinds[i])) ConfigureWeapon(weaponSlot, bases[i], 0);
            Label(36, 288, 360, 24, "무기 형태");
            var forms = Arena.catalog.forms.Where(f => Chapter1Rewards.BlueprintKindOf(f.id) == kind).ToArray();
            for (int i = 0; i < forms.Length; i++) if (Button(36 + i % 2 * 184, 318 + i / 2 * 40, 176, 34, forms[i].name, setup.Form == forms[i].id)) ConfigureWeapon(weaponSlot, forms[i].id, 0);
            Label(36, 410, 360, 24, "스킬");
            var form = Arena.catalog.forms.First(f => f.id == setup.Form);
            for (int i = 0; i < form.specials.Length; i++)
                if (Button(36 + i % 2 * 184, 440 + i / 2 * 44, 176, 38, (i == 0 ? "기본 / " : "") + form.specials[i].name, setup.Special == i)) ConfigureWeapon(weaponSlot, form.id, i);
            Label(36, 542, 362, 38, "팔레트 1~6칸 / 칸별 색상 선택\n잉크와 같은 색이면 강화 효과", true);
            for (int n = 1; n <= 6; n++)
                if (Button(36 + (n - 1) % 3 * 122, 588 + (n - 1) / 3 * 42, 114, 36, n + "칸", setup.Inks.Length == n))
                {
                    var ids = new string[n]; var colors = Enumerable.Repeat("neutral", n).ToArray();
                    Array.Copy(setup.Inks, ids, Math.Min(n, setup.Inks.Length)); Array.Copy(setup.Colors, colors, Math.Min(n, setup.Colors.Length));
                    ConfigurePalette(weaponSlot, ids, colors); paletteSlot = Math.Min(paletteSlot, n - 1);
                }

            if (Equipment.Equipped(weaponSlot) == null) { Label(420, 210, 736, 50, "무기를 먼저 선택하세요."); return; }
            paletteSlot = Mathf.Clamp(paletteSlot, 0, setup.Inks.Length - 1);
            float width = 742f / setup.Inks.Length;
            for (int i = 0; i < setup.Inks.Length; i++)
            {
                var ink = InkCatalog.Find(setup.Inks[i]);
                if (Button(420 + i * width, 210, width - 6, 66, $"{i + 1}번 / {PaletteColorName(setup.Colors[i])}\n{ink?.name ?? "빈 칸"}", paletteSlot == i)) paletteSlot = i;
            }
            for (int i = 0; i < ColorNames.Length; i++)
            {
                string color = i == 0 ? "neutral" : ColorIds[i];
                if (Button(420 + i * 124, 282, 116, 30, i == 0 ? "칸 색 / 무색" : "칸 색 / " + ColorNames[i], setup.Colors[paletteSlot] == color))
                { var colors = (string[])setup.Colors.Clone(); colors[paletteSlot] = color; ConfigurePalette(weaponSlot, setup.Inks, colors); }
            }
            search = GUI.TextField(new Rect(420, 324, 556, 32), search, field);
            if (Button(984, 324, 170, 32, "선택 칸 비우기")) SetInk(null);
            for (int i = 0; i < ColorNames.Length; i++) if (Button(420 + i * 124, 364, 116, 30, ColorNames[i], colorFilter == i)) { colorFilter = i; inkScroll = Vector2.zero; }
            var candidates = InkCatalog.inks.Where(p => (colorFilter == 0 || p.color == ColorIds[colorFilter]) && (string.IsNullOrWhiteSpace(search) || p.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            inkScroll = GUI.BeginScrollView(new Rect(420, 402, 744, 148), inkScroll, new Rect(0, 0, 718, Mathf.Ceil(candidates.Length / 3f) * 52));
            for (int i = 0; i < candidates.Length; i++)
                if (Button(i % 3 * 238, i / 3 * 52, 230, 46, candidates[i].name + " / " + PaletteColorName(candidates[i].color), setup.Inks[paletteSlot] == candidates[i].id)) SetInk(candidates[i].id);
            GUI.EndScrollView();
            var current = InkCatalog.Find(setup.Inks[paletteSlot]);
            Label(420, 562, 736, 78, current == null ? $"현재 본게임 런 잉크 {InkCatalog.inks.Length}종을 선택할 수 있습니다." : current.name + " / " + current.description);
            Label(420, 646, 736, 38, current == null ? "칸 색과 잉크 색이 같으면 강화 효과, 다르면 기본 효과입니다." :
                (setup.Colors[paletteSlot] == current.color ? "색상 강화 효과 적용" : "기본 효과 적용") + $" / 재발동 대기 {current.cooldown:0.##}초", true);
        }
        static string PaletteColorName(string color)
        { int index = Array.IndexOf(ColorIds, color); return color == "neutral" ? "무색" : index >= 0 ? ColorNames[index] : color; }
        void SetInk(string id)
        {
            var setup = Weapons[weaponSlot]; var ids = (string[])setup.Inks.Clone(); ids[paletteSlot] = id;
            ConfigurePalette(weaponSlot, ids, setup.Colors);
        }

        void DrawStats()
        {
            Label(36, 214, 478, 80, "잉크는 본게임의 기본·색상 강화 효과를 사용합니다.\n장비 / 런 잉크 탭에서 칸 색상을 변경하세요.");
            int yGlobal = 340;
            Label(36, yGlobal, 300, 38, "공통 공격 강화  +" + Growth.AttackLevel);
            if (Button(350, yGlobal, 42, 34, "−")) SetAttackLevel(Math.Max(0, Growth.AttackLevel - 1));
            if (Button(466, yGlobal, 42, 34, "+")) SetAttackLevel(Math.Min(Growth.Data.attackMaxLevel, Growth.AttackLevel + 1));
            Label(36, 390, 478, 54, $"공격 강화 범위: +0~+{Growth.Data.attackMaxLevel}\n현재 공통 공격 배율: {Number(Growth.AttackMultiplier)}배", true);

            Label(558, 214, 580, 30, "선택 무기 수치 직접 입력");
            basicText = Numeric(558, 258, "기본 피해 배율 (0~1000)", basicText);
            skillText = Numeric(558, 308, "스킬 피해 배율 (0~1000)", skillText);
            speedText = Numeric(558, 358, "추가 공속 배율 (0.05~20)", speedText);
            cooldownText = Numeric(558, 408, "스킬 쿨타임 초 (-1 = 원본)", cooldownText);
            if (Button(558, 464, 282, 38, "선택 무기에 적용")) ApplyTuning(weaponSlot, basicText, skillText, speedText, cooldownText);
            if (Button(850, 464, 300, 38, "배율, 쿨타임 기본값")) { ApplyTuning(weaponSlot, "1", "1", "1", "-1"); RefreshTuningText(); }
            Label(558, 510, 590, 42, "피해 배율은 잉크, 공격 강화 효과에 추가로 곱합니다.\n쿨타임 0~3600초 지정 가능. 발사기의 스킬 예외는 유지합니다.", true);
            hpText = Numeric(558, 568, "최대 HP (1~1,000,000)", hpText);
            if (Button(558, 620, 282, 38, "HP 적용 / 전부 회복")) SetMaxHealth(hpText);
            if (Button(850, 620, 300, 38, "쿨타임 전부 회복"))
                foreach (var weapon in Arena.Player.weapons)
                { var skill = Arena.catalog.forms[weapon.FormIndex].specials[weapon.SpecialIndex]; weapon.Cooldown.Reset(skill.capacity, weapon.CooldownPeriod(skill.cooldown)); }
        }
        string Numeric(float x, float y, string title, string value)
        { Label(x, y + 5, 390, 32, title); return GUI.TextField(new Rect(x + 400, y, 192, 36), value, field); }

        void DrawRooms()
        {
            Label(36, 166, 1100, 30, "장비 설정 유지 / 이동할 때 해당 방의 전투를 새로 시작합니다.");
            string[] kinds = { "start", "elite", "maintenance", "boss" }, names = { "시작방", "엘리트 방", "정비소", "보스방" };
            for (int chapter = 1; chapter <= 2; chapter++)
            {
                float y = 214 + (chapter - 1) * 62;
                Label(36, y + 7, 120, 34, chapter + "챕터");
                for (int i = 0; i < kinds.Length; i++)
                    if (Button(166 + i * 246, y, 234, 44, names[i])) { openAfterLoad = false; Travel(chapter, kinds[i]); return; }
            }
            Label(36, 344, 180, 32, "2챕터 보스 페이즈");
            for (int phase = 1; phase <= 3; phase++)
                if (Button(226 + (phase - 1) * 312, 338, 300, 42, phase + "페이즈 바로 시작", BossPhase == phase))
                { TravelToBossPhase(phase); return; }
            Label(36, 396, 1100, 32, $"현재 {Chapter}챕터 전체 방 / {Rooms.Length}개 / 방 번호를 눌러 이동");
            var rooms = Rooms;
            roomScroll = GUI.BeginScrollView(new Rect(36, 438, 1128, 230), roomScroll, new Rect(0, 0, 1100, Mathf.Ceil(rooms.Length / 3f) * 72));
            for (int i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                if (Button(i % 3 * 366, i / 3 * 72, 354, 62, $"{room.Id} / {KindName(room.Kind)}\n{room.Name}", RoomId == room.Id))
                { openAfterLoad = false; Travel(Chapter, room.Id); break; }
            }
            GUI.EndScrollView();
        }
        static string KindName(string kind) => kind switch { "start" => "시작", "combat" or "normal" => "일반 전투", "elite" => "엘리트", "maintenance" or "facility" or "shop" => "정비소", "boss" => "보스", "treasure" => "보물", _ => kind };
    }
}
