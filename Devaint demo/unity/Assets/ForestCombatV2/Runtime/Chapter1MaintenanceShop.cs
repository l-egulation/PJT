using Deviant.Control.Contracts;
using Deviant.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // Maintenance facility screen: ink stall, stat/slot upgrades, palette/ink management and stamp application.
    // Opening it inside a facility pauses the chapter like the full map.
    public sealed partial class Chapter1MaintenanceShop : MonoBehaviour
    {
        ForestCombatWorld world;
        Chapter1RunBinding binding;
        IControlScope scope;
        int facility = -1;
        string message;
        public MaintenanceService Service { get; private set; }
        public bool IsOpen => scope != null;
        public int Facility => facility;

        public void Initialize(ForestCombatWorld owner, Chapter1RunBinding runBinding)
        {
            world = owner; binding = runBinding;
            Service = new MaintenanceService(binding.Run.Economy, binding.Run.Stash);
        }

        Chapter1MaintenanceFacility Facilities => world ? world.GetComponent<Chapter1MaintenanceFacility>() : null;

        public bool TryOpen()
        {
            var facilities = Facilities;
            if (IsOpen || !world || !world.Ready || binding.ShowingResult || !facilities) return false;
            var player = world.arena.Player;
            if (!player || player.Dead || world.arena.Controls.Current.SimulationPaused) return false;
            int room = facilities.RoomAt(player.Position);
            if (room < 0) return false;
            var next = world.arena.Controls.OpenScope("chapter1 maintenance facility");
            try { next.BlockInput(InputChannels.Combat | InputChannels.Interaction); next.PauseSimulation(); scope = next; }
            catch { next.Dispose(); throw; }
            facility = room; message = null;
            // The facility's ink stall is rolled on its first visit and kept for the run.
            Growth?.InkStock(StallKey, new System.Random(unchecked(binding.Run.Seed * 7919 + room)));
            return true;
        }

        public void Close() { scope?.Dispose(); scope = null; }

        void OnDestroy() => Close();

        public bool ExternalInput { get; set; }
        void OnDisable() => Close();

        void Update()
        {
            if (binding && binding.ShowingResult) { Close(); return; }
            if (ExternalInput) return;
            var input = world ? Deviant.Combat.PlayerControls.For(world.arena.Player) : null;
            if (!input) return;
            if (IsOpen)
            { if (input.TakeBack() || input.Take(Deviant.Input.Contracts.GameAction.Interact)) { input.SuppressGameplay(); Close(); } }
            else Deviant.Combat.PlayerControls.TryPerform(world.arena.Player, Deviant.Input.Contracts.GameAction.Interact, TryOpen);
        }

        public string SlotBlueprintKind(int slot)
        {
            var player = world.arena.Player;
            if (!player.IsWeaponSlotReady(slot)) return null;
            return Chapter1Rewards.BlueprintKindOf(world.arena.catalog.forms[player.weapons[slot].FormIndex].id);
        }

        // Each chapter 1 facility keeps its own stall for the run.
        public string StallKey => "chapter1:" + facility;
        public System.Collections.Generic.IReadOnlyList<InkOffer> InkStock =>
            Growth != null && facility >= 0 ? Growth.StockOf(StallKey) : System.Array.Empty<InkOffer>();

        // Buys one stall ink into the bag; a full bag or short gold refuses without charging.
        public bool TryBuyInk(int index, out string message)
        {
            if (Growth == null) { message = "잉크 판매대를 사용할 수 없습니다."; return false; }
            string bought = null;
            bool ok = GrowthTransaction(() => bought = Growth.BuyStockInk(StallKey, index).Part.name, out message);
            if (ok) message = bought + " 구매 / 가방에 넣었습니다.";
            return ok;
        }

        // A weapon stamp reconfigures the slot to its form and a skill stamp to its special variant, through the same
        // equipment path the inventory uses; the other half of the selection is kept (user decision 2026-11-03).
        public static bool ApplyStamp(Deviant.Combat.CombatPlayer player, RunStash stash, RunEconomy economy, int slot, System.Guid stampId, out string error)
        {
            if (!player || slot < 0 || slot >= player.weapons.Count || !player.IsWeaponSlotReady(slot)) { error = "도면을 장착하지 않은 슬롯입니다."; return false; }
            var forms = player.arena.catalog.forms; var weapon = player.weapons[slot];
            string kind = Chapter1Rewards.BlueprintKindOf(forms[weapon.FormIndex].id);
            return stash.TryApply(slot, stampId, kind, economy, stamp =>
            {
                int form = stamp.Kind == StampKind.Weapon ? System.Array.FindIndex(forms, f => f.id == stamp.FormId) : weapon.FormIndex;
                int special = stamp.Kind == StampKind.Skill ? stamp.Skill : weapon.SpecialIndex;
                if (form < 0) return "현재 무기에 연결되지 않은 스탬프입니다.";
                return player.TryReconfigure(slot, form, special, out _) ? null : "지금은 장비를 바꿀 수 없습니다.";
            }, out error);
        }

        public bool TryApplyStamp(int slot, System.Guid stampId, out string error)
        {
            if (!IsOpen || !world || !world.Ready || binding.ShowingResult) { error = "상점을 연 상태에서만 가능합니다."; return false; }
            return ApplyStamp(world.arena.Player, Service.Stash, Service.Economy, slot, stampId, out error);
        }

        public string StampName(RunStamp stamp) => Chapter1RewardPickup.StampLabel(world ? world.arena : null, stamp);

        void Report(bool done, string success, string error) => message = done ? success : error;

        public bool ExternalPresentation { get; set; }

        void OnGUI()
        {
            if (ExternalPresentation) return;
            if (!IsOpen) return;
            var oldFont = GUI.skin.font; int oldDepth = GUI.depth;
            try
            {
                if (world.diagnostics && world.diagnostics.uiFont) GUI.skin.font = world.diagnostics.uiFont;
                GUI.depth = -150;
                var economy = Service.Economy; var stash = Service.Stash;
                var area = new Rect((Screen.width - 560) / 2f, (Screen.height - 560) / 2f, 560, 560);
                GUI.Box(area, "");
                var title = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                var line = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                GUILayout.BeginArea(new Rect(area.x + 20, area.y + 14, area.width - 40, area.height - 28));
                GUILayout.Label("상점", title);
                GUILayout.Label($"골드 {economy.Gold}G   잉크 파츠 {stash.InkParts}   무기 스탬프 {stash.Count(StampKind.Weapon)}   스킬 스탬프 {stash.Count(StampKind.Skill)}", line);
                GUILayout.Space(8);

                GUILayout.Label("잉크 판매대", line);
                GUILayout.BeginHorizontal();
                var stock = InkStock;
                for (int i = 0; i < stock.Count; i++)
                {
                    var offer = stock[i];
                    string label = $"{offer.Part.name} ({ColorName(offer.Part.color)} {offer.Part.grade}등급) / {(offer.Sold ? "판매 완료" : offer.Price + "G")}";
                    GUI.enabled = !offer.Sold;
                    if (GUILayout.Button(label, GUILayout.Height(34))) TryBuyInk(i, out message);
                    GUI.enabled = true;
                }
                if (stock.Count == 0) GUILayout.Label("진열된 잉크 없음", line);
                GUILayout.EndHorizontal();

                GUILayout.Space(8);
                GUILayout.Label($"스탬프 적용 / 첫 적용 무료, 교체 회당 {economy.StampSwapCost}G (기존 스탬프는 사라짐)", line);
                for (int slot = 0; slot < RunStash.WeaponSlots; slot++)
                {
                    string kind = SlotBlueprintKind(slot);
                    var weaponStamp = stash.EquippedWeaponStamp(slot); var skillStamp = stash.EquippedSkillStamp(slot);
                    GUILayout.Label($"슬롯 {slot + 1} / {(kind == null ? "도면 없음" : Chapter1Rewards.BlueprintName(kind) + " 도면")} / {(weaponStamp == null ? "무기 스탬프 없음" : StampName(weaponStamp))} / {(skillStamp == null ? "스킬 스탬프 없음" : StampName(skillStamp))}", line);
                    GUILayout.BeginHorizontal();
                    foreach (var stamp in new System.Collections.Generic.List<RunStamp>(stash.Stamps))
                    {
                        if (stamp.Kind == StampKind.Weapon && stamp.BlueprintKind != kind) continue;
                        int cost = stash.CostOf(slot, stamp, economy);
                        if (GUILayout.Button($"{StampName(stamp)} / {(cost == 0 ? "무료" : cost + "G")}", GUILayout.Height(30)))
                            Report(TryApplyStamp(slot, stamp.Id, out var error), "스탬프를 적용했습니다.", error);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(6);
                if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, line);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(Deviant.Combat.PlayerControls.Prompt(world.arena.Player, "닫기 [{Interact} / {Cancel}]"), GUILayout.Height(36))) Close();
                GUILayout.EndArea();
            }
            finally { GUI.skin.font = oldFont; GUI.depth = oldDepth; GUI.enabled = true; }
        }
    }
}
