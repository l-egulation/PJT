using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Run;
using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // Connects the run's SketchRunState to one chapter arena (docs/design/equipment/sketch-weapons.md,
    // docs/design/run/rewards.md): kill gauge, paused 3-choice offers, A/B slots on the player's two combat weapons,
    // and durability charged from accepted hits. Reward UI uses the adopted minimal screen; bag/shop remain temporary.
    public sealed partial class SketchRunBinding : MonoBehaviour
    {
        // Tab opens the sketch bag; G opens the next banked reward (rewards.md 받아 둔 보상, MR 371).
        public const Key PanelKey = Key.Tab, OpenRewardKey = Key.G;
        // Returns the open shop's facility key, or null when no shop is open (set by the chapter).
        public Func<string> OpenShopFacility { get; set; }
        // Player setting (sketch-weapons.md 내구도 표시): false = gauge under each weapon icon, true = remaining value.
        const string DurabilityPref = "deviant.sketch.durabilityAsNumber";
        // Read once and cached: the HUD asks every frame.
        static int durabilityAsNumber = -1;
        public static bool DurabilityAsNumber
        {
            get { if (durabilityAsNumber < 0) durabilityAsNumber = PlayerPrefs.GetInt(DurabilityPref, 0); return durabilityAsNumber == 1; }
            set { durabilityAsNumber = value ? 1 : 0; PlayerPrefs.SetInt(DurabilityPref, durabilityAsNumber); }
        }
        public SketchRunState State { get; private set; }
        public CombatArena Arena { get; private set; }
        bool panelOpen;
        public Func<bool> SharedBagOpen { get; set; }
        public Action<bool> SharedBagVisibility { get; set; }
        public Action<Guid, string> SharedBagSelection { get; set; }
        public bool PanelOpen
        {
            get => SharedBagOpen?.Invoke() ?? panelOpen;
            private set { panelOpen = value; SharedBagVisibility?.Invoke(value); }
        }
        public string Notice { get; private set; }

        RunSession run;
        int stage;
        // The run's permanent-unlock budget (shared rerolls etc.); null outside a run.
        public RunPerks Perks => run?.Perks;
        IControlScope pause;
        int syncedRevision = -1;
        bool ownsLoadout;
        // Sketch weapon last applied to each combat slot.
        readonly SketchWeapon[] occupants = new SketchWeapon[2];
        float lastNow = float.NaN;
        CombatOutcomeCursor cursor; bool cursorBound;
        readonly CombatAcceptedHit[] hits = new CombatAcceptedHit[128];
        readonly Dictionary<Guid, float> frameCharges = new Dictionary<Guid, float>();
        // After a weapon pick the screen offers "bag and continue" / "configure now".
        SketchWeapon justPicked; string justRegistered;
        Guid selectedWeapon; string selectedInk;
        float noticeUntil;
        Vector2 panelScroll;

        // Creates the run's sketch state on first use and starts the given stage (1, 2, ...).
        public static SketchRunBinding Attach(GameObject host, CombatArena arena, RunSession run, int stage)
        {
            // The sandbox uses the same combat equipment without progression/reward screens.
            if (!arena || run == null || run.Growth?.IsSandbox == true) return null;
            var binding = host.GetComponent<SketchRunBinding>() ?? host.AddComponent<SketchRunBinding>();
            binding.Arena = arena; binding.run = run;
            if (run.Growth != null) arena.MvpGrowth = run.Growth;
            arena.EnableSketchEquipment(run.Seed, stage);
            if (arena.Sketch != null) run.Sketch = arena.Sketch.State;
            else if (run.Sketch == null) run.Sketch = Create(arena, run);
            binding.State = run.Sketch;
            arena.SketchEquipment = true;
            if (arena.Sketch == null) binding.State.StartStage(stage);
            binding.stage = stage;
            // Permanent unlocks (docs/design/progression/roguelite-progression.md): locked inks stay out of every offer,
            // shelf and chest; the shop display unlock adds places to each new shelf. Training runs carry neither.
            if (run.Plan != null && !run.Plan.Training) binding.State.LockInks(run.Plan.LockedInkParts);
            binding.State.ExtraShopItems = run.Perks != null && run.Perks.IsActive ? run.Perks.Settings.ExtraShopItems : 0;
            binding.State.Arsenal.WeaponDestroyed -= binding.OnWeaponDestroyed;
            binding.State.Arsenal.WeaponDestroyed += binding.OnWeaponDestroyed;
            arena.PlayerAttackStarted += binding.OnAttackStarted;
            binding.InitializeFirstRunTutorials(stage);
            return binding;
        }

        static SketchRunState Create(CombatArena arena, RunSession run)
        {
            var forms = new Dictionary<string, string[]>();
            foreach (var form in arena.catalog.forms)
            {
                if (form == null || form.id == CombatArena.UnarmedFormId || form.specials == null) continue;
                var skills = form.specials.Where(s => s != null && !s.unavailable).Select(s => s.id).ToArray();
                if (skills.Length > 0) forms[form.id] = skills;
            }
            // Until the new 43-ink content reaches the runtime, the codex offers the existing colour inks.
            var parts = (run.Growth ?? arena.MvpGrowth)?.Data?.parts ?? Array.Empty<GrowthPart>();
            var colors = parts.ToDictionary(p => p.id, p => p.color);
            return new SketchRunState(forms, parts.Select(p => p.id).ToArray(), id => colors.TryGetValue(id, out var c) ? c : null, unchecked(run.Seed * 6151 + 29));
        }

        public void ReportKill(string role, float maxHealth)
        {
            if (State == null) return;
            State.RecordKill(role, maxHealth);
        }

        // The stage exit waits until every banked reward is chosen: it opens the next one and holds the exit.
        bool exitWaiting;
        public bool HoldExitForRewards()
        {
            if (State == null || !State.MustOpenBeforeExit) return false;
            exitWaiting = true;
            if (State.PendingOffer == null && justPicked == null && justRegistered == null) TryOpenBankedReward();
            Notice = "나가기 전에 받아 둔 보상을 모두 고르세요."; noticeUntil = Time.unscaledTime + 3f;
            return true;
        }

        public bool TryOpenBankedReward()
        {
            if (State == null || State.PendingOffer != null || justPicked != null || justRegistered != null) return false;
            return State.OpenBanked();
        }

        public bool OpenBossOffer() => State != null && State.OpenBossOffer();

        void OnDestroy()
        {
            CloseFirstRunTutorial();
            if (State != null) State.Arsenal.WeaponDestroyed -= OnWeaponDestroyed;
            if (Arena) Arena.PlayerAttackStarted -= OnAttackStarted;
            pause?.Dispose(); pause = null;
        }

        void Update()
        {
            if (State == null || !Arena || !Arena.Player) return;
            if (FirstRunTutorialShowing)
            {
                if (run.Ended || Arena.Player.Dead) CloseFirstRunTutorial();
                else return;
            }
            if (TryShowFirstRewardTutorial()) return;
            var keyboard = Keyboard.current;
            bool choosing = State.PendingOffer != null || justPicked != null || justRegistered != null;
            UpdateRewardInput();
            // G opens a banked reward only in play: not over the pause menu, map, sign window or another screen that holds the world.
            bool inPlay = !Arena.Controls.Current.SimulationPaused && !Arena.Controls.Current.Blocks(InputChannels.Interaction) && Time.timeScale > 0;
            var input = PlayerControls.For(Arena.Player);
            if (!choosing && !PanelOpen && !Arena.Player.Dead && inPlay && !(input && input.GameplaySuppressed) &&
                (input ? input.Take(GameAction.OpenReward) : keyboard != null && keyboard[OpenRewardKey].wasPressedThisFrame))
            { if (TryOpenBankedReward()) input?.SuppressGameplay(); }
            // Departure rewards open once each, in play on the first stage: the ink first, then the weapon
            // (roguelite-progression.md 출발 추가 보상, user decision 2026-11-13).
            if (stage == 1 && !choosing && inPlay && !Arena.Player.Dead && !run.Ended && run.Perks != null)
            {
                if (run.Perks.StartingInkRewardsRemaining > 0 && State.SupplyInks.Count > 0)
                { if (run.Perks.TryTakeStartingInkReward()) State.OpenStartingInkOffer(); }
                else if (run.Perks.StartingRewardsRemaining > 0 && run.Perks.TryTakeStartingReward()) State.OpenStartingOffer();
            }
            // At the exit the remaining banked rewards follow one another.
            if (exitWaiting && !choosing) { if (State.Banked > 0) TryOpenBankedReward(); else { exitWaiting = false; Notice = "받아 둔 보상을 모두 골랐습니다. 출구를 다시 사용하세요."; noticeUntil = Time.unscaledTime + 3f; } }
            UpdatePause();
            if (Arena.Sketch != null) return; // The adapter owns combat loadout, ink timers and durability.
            // Combat time only: the arena clock stops while any pause scope is open.
            float now = Arena.Now;
            if (!float.IsNaN(lastNow) && now > lastNow && !Arena.Controls.Current.SimulationPaused) State.Arsenal.Tick(now - lastNow);
            lastNow = now;
            ChargeDurability();
            if (syncedRevision != State.Arsenal.Revision) SyncLoadout();
        }

        string ShopFacility => OpenShopFacility?.Invoke();
        // An open shop already pauses combat with its own scope.
        bool ChoosingReward => State != null && (State.PendingOffer != null || justPicked != null || justRegistered != null);
        public bool RewardModalShown => ChoosingReward || State != null && Arena && Arena.Sketch == null && PanelOpen;
        bool ModalShown => State.PendingOffer != null || justPicked != null || justRegistered != null || PanelOpen && ShopFacility == null;

        void UpdatePause()
        {
            if (ModalShown && pause == null)
            {
                var next = Arena.Controls.OpenScope("sketch weapon choice");
                try { next.BlockInput(InputChannels.Combat | InputChannels.Interaction); next.PauseSimulation(); pause = next; }
                catch { next.Dispose(); throw; }
            }
            else if (!ModalShown && pause != null) { pause.Dispose(); pause = null; }
        }

        // ---- Loadout ----

        // Slot A/B of the arsenal drive the player's two combat weapons; an empty slot is the bare-hand form.
        void SyncLoadout()
        {
            syncedRevision = State.Arsenal.Revision;
            // The existing departure loadout (bare hands, or development blueprints) stays until the first sketch
            // weapon is equipped; from then on the arsenal owns both slots for the rest of the run.
            if (!ownsLoadout)
            {
                if (State.Arsenal.Equipped(0) == null && State.Arsenal.Equipped(1) == null) return;
                ownsLoadout = true;
            }
            var player = Arena.Player; var forms = Arena.catalog.forms;
            int unarmed = Array.FindIndex(forms, f => f != null && f.id == CombatArena.UnarmedFormId);
            for (int slot = 0; slot < 2; slot++)
            {
                var weapon = State.Arsenal.Equipped(slot);
                int form = weapon == null ? unarmed : Array.FindIndex(forms, f => f != null && f.id == weapon.Form);
                if (form < 0) continue;
                int special = weapon == null ? 0 : Math.Max(0, Array.FindIndex(forms[form].specials, s => s.id == weapon.Skill));
                var current = player.weapons[slot];
                bool ready = player.IsWeaponSlotReady(slot);
                if (occupants[slot] == weapon && current.FormIndex == form && current.SpecialIndex == special && ready) continue;
                // The leaving weapon keeps its remaining skill cooldown frozen in the bag.
                if (occupants[slot] != null && occupants[slot] != weapon) State.Arsenal.RememberSkillCooldown(occupants[slot].Id, current.Cooldown.Debt(Arena.Now));
                if (player.selected == slot) player.CancelAction();
                var skill = forms[form].specials[special];
                var configuration = new Deviant.Equipment.Contracts.WeaponConfiguration(new Deviant.Equipment.Contracts.WeaponSelection(form, special), skill.capacity, skill.cooldown);
                var result = player.weapons.Reconfigure(slot, configuration, Arena.Now);
                if (result.Status != Deviant.Equipment.Contracts.EquipmentCommandStatus.Succeeded) { Debug.LogWarning("Sketch weapon equip failed: " + result.Error); continue; }
                current = player.weapons[slot];
                current.StatSlots.Clear();
                current.Cooldown.Reset(skill.capacity, skill.cooldown);
                // A returning weapon resumes from the cooldown it had when it was put away.
                float left = weapon?.SkillCooldown ?? 0;
                if (left > 0 && current.Cooldown.Period > 0 && current.Cooldown.Spend(Arena.Now))
                    current.Cooldown.Refund(Mathf.Max(0, current.Cooldown.Period - left), Arena.Now);
                occupants[slot] = weapon;
                player.SetWeaponSlotReady(slot, true);
            }
            MirrorInks();
            // Only a break moves the current weapon; keys stay bound to their slots.
            if (reselectAfterBreak && State.Arsenal.Equipped(State.Arsenal.ActiveSlot) != null) player.selected = State.Arsenal.ActiveSlot;
            reselectAfterBreak = false;
        }

        // The existing ink-effect engine reads WeaponGrowth palettes of the two combat weapons; keep them equal to the
        // sketch weapons' inks (base values; the enhanced values of the new ink set are not in the runtime yet).
        readonly string[] mirrored = new string[2];
        void MirrorInks()
        {
            var growth = Arena.MvpGrowth ?? run.Growth;
            if (growth == null) return;
            for (int slot = 0; slot < 2; slot++)
            {
                var weapon = State.Arsenal.Equipped(slot);
                var inks = weapon == null ? Array.Empty<string>() : weapon.Slots.Select(s => s.Ink).ToArray();
                string key = (weapon?.Id.ToString() ?? "-") + ":" + string.Join(",", inks.Select(i => i ?? "-"));
                if (mirrored[slot] == key) continue;
                growth.MirrorPalette(Arena.Player.weapons[slot].InstanceId, inks);
                mirrored[slot] = key;
            }
        }

        // ---- Durability ----

        // Which sketch weapon started each attack execution. Projectiles, ink extra hits and other effects that keep
        // the attack's execution id are charged to that weapon even after it was swapped into the bag; a destroyed
        // source charges nothing. Hits with an execution never seen start fall back to the slot's current weapon.
        readonly Dictionary<Guid, SketchWeapon> executions = new Dictionary<Guid, SketchWeapon>();
        readonly Queue<Guid> executionOrder = new Queue<Guid>();
        void OnAttackStarted(CombatAttackStamp stamp, Guid execution)
        {
            if (State == null || !Arena || !Arena.Player || Arena.Sketch != null) return;
            var player = Arena.Player;
            int slot = stamp.WeaponId == player.weapons[0].InstanceId ? 0 : stamp.WeaponId == player.weapons[1].InstanceId ? 1 : -1;
            if (slot < 0) return;
            var weapon = ownsLoadout ? State.Arsenal.Equipped(slot) : null;
            State.Arsenal.RecordAttackStart(slot);
            if (weapon == null || executions.ContainsKey(execution)) return;
            executions[execution] = weapon; executionOrder.Enqueue(execution);
            while (executionOrder.Count > 512) executions.Remove(executionOrder.Dequeue());
        }

        // Actual HP removed (overkill excluded) is charged to the sketch weapon that started the hit's attack.
        void ChargeDurability()
        {
            if (!cursorBound) { cursorBound = Arena.HitEvents.TryGetCursor(out cursor); if (!cursorBound) return; }
            var player = Arena.Player;
            Guid slotA = player.weapons[0].InstanceId, slotB = player.weapons[1].InstanceId;
            var charges = frameCharges; charges.Clear();
            for (int pageIndex = 0; pageIndex < 8; pageIndex++)
            {
                if (!Arena.HitEvents.TryRead(cursor, hits, out var page)) { cursorBound = false; return; }
                cursor = page.Next;
                for (int i = 0; i < page.Count; i++)
                {
                    var hit = hits[i];
                    var id = hit.Origin.Stamp.WeaponId;
                    int slot = id == slotA ? 0 : id == slotB ? 1 : -1;
                    if (slot < 0 || !ownsLoadout) continue;
                    if (!executions.TryGetValue(hit.Origin.ExecutionId, out var weapon)) weapon = State.Arsenal.Equipped(slot);
                    float removed = Mathf.Max(0, hit.Before.Health - hit.After.Health);
                    if (weapon == null || removed <= 0) continue;
                    charges[weapon.Id] = (charges.TryGetValue(weapon.Id, out var sum) ? sum : 0) + removed;
                }
                if (page.Count < hits.Length) break;
            }
            // Every target of the frame's hits is applied before a weapon breaks.
            foreach (var charge in charges) State.Arsenal.ChargeDurability(charge.Key, charge.Value);
        }

        bool reselectAfterBreak;
        void OnWeaponDestroyed(SketchWeapon weapon, bool stored)
        {
            if (!stored) reselectAfterBreak = true;
            // Durability breaks only (a discard never raises this event): the weapon break gold unlock pays once per weapon.
            int gold = run?.Perks?.AwardWeaponBreak(weapon.Id) ?? 0;
            Notice = $"{FormName(weapon.Form)} 무기가 파괴되었습니다." + (stored ? " (가방)" : "") + (gold > 0 ? $" +{gold}G" : "");
            noticeUntil = Time.unscaledTime + 3f;
        }

        // ---- Names ----

        string FormName(string id) => Arena.catalog.forms.FirstOrDefault(f => f != null && f.id == id)?.name ?? id;
        string SkillName(string form, string skill) => Arena.catalog.forms.FirstOrDefault(f => f != null && f.id == form)?.specials?.FirstOrDefault(s => s.id == skill)?.name ?? skill;
        GrowthData InkData => (Arena ? Arena.MvpGrowth : null)?.Data ?? run.Growth?.Data;
        string InkName(string id) => Arena.Sketch?.Catalog.Find(id)?.name ?? InkData?.FindPart(id)?.name ?? id;
        static readonly Dictionary<string, string> ColorNames = new Dictionary<string, string>
        { ["red"] = "빨강", ["yellow"] = "노랑", ["green"] = "초록", ["blue"] = "파랑", ["purple"] = "보라" };
        static string ColorName(string c) => c == null ? "무색" : ColorNames.TryGetValue(c, out var n) ? n : c;
        string InkColor(string id) => Arena.Sketch?.Catalog.Find(id)?.color ?? InkData?.FindPart(id)?.color;
        string Palette(IReadOnlyList<string> enhance) => string.Join(" / ", enhance.Select(e => e == null ? "무색" : ColorName(e) + " 강화"));
        string WeaponLine(SketchWeaponDraft w) => $"{FormName(w.Form)} / {SkillName(w.Form, w.Skill)}\n{w.Slots}칸: {Palette(w.Enhance)}";

        // ---- Temporary IMGUI ----

        GUIStyle box, title, button;
        void Styles()
        {
            if (box != null) return;
            box = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(12, 12, 10, 10) };
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true };
        }

        // Keep transient messages below an active world announcement without moving the combat HUD.
        Rect NoticeRect()=>new Rect(Screen.width/2-220,ApprovedWorldNotice.HasTopNotice?Mathf.Max(60,Screen.height-100):60,440,40);

        void OnGUI()
        {
            if (State == null || !Arena) return;
            if (Arena.Sketch != null)
            {
                if (ChoosingReward) DrawRewardScreen();
                else if (Notice != null && Time.unscaledTime < noticeUntil)
                { Styles(); GUI.Box(NoticeRect(), Notice, box); }
                return; // Inventory and shop use the shared book.
            }
            Styles();
            // The gauge bar and banked-reward badge live in the combat HUD (TemporaryGameGui.RefreshSketchGauge).
            if (Notice != null && Time.unscaledTime < noticeUntil) GUI.Box(NoticeRect(), Notice, box);
            if (State.PendingOffer != null || justPicked != null || justRegistered != null)
            {
                DrawRewardScreen();
                if (PanelOpen) DrawPanel(compact: false);
            }
            else
            {
                string shop = ShopFacility;
                if (shop != null) DrawShop(shop);
                if (PanelOpen) DrawPanel(compact: shop != null);
            }
        }

        // Temporary shelf shown beside the shop book (rewards.md): three weapons and three inks (duplicates allowed) for gold.
        void DrawShop(string facility)
        {
            IReadOnlyList<SketchShopOffer> stock;
            try { stock = State.ShopStock(facility); }
            catch (InvalidOperationException e) { GUI.Box(new Rect(20, 20, 420, 60), "상점 진열 없음: " + e.Message, box); return; }
            var r = new Rect(20, 20, 440, 120 + stock.Count * 74);
            GUI.Box(r, "");
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16));
            GUILayout.Label($"상점 / 골드 {run.Economy?.Gold ?? 0}G", title);
            for (int i = 0; i < stock.Count; i++)
            {
                var o = stock[i]; int index = i;
                string text = o.Empty ? "(진열할 잉크 없음)" : o.Weapon != null ? WeaponLine(o.Weapon) + $"  내구도 {State.Arsenal.Rules.MaxDurability(o.Weapon.Slots):N0}" : $"잉크 {InkName(o.Ink)} / +{State.Arsenal.Rules.InkUsesPerAcquisition}회";
                GUILayout.BeginHorizontal(box);
                GUILayout.Label(text, GUILayout.Width(300));
                GUI.enabled = !o.Sold;
                if (GUILayout.Button(o.Sold ? "품절" : $"{o.Price}G", button, GUILayout.Height(48)))
                {
                    try
                    {
                        var weapon = Arena.Sketch != null ? Arena.Sketch.Buy(facility, index).Weapon?.Model : State.Buy(facility, index, run.Economy);
                        Notice = weapon != null ? $"{FormName(weapon.Form)} 구매 (미확정 / 가방)" : $"{InkName(o.Ink)} 잉크 부여 횟수 +{State.Arsenal.Rules.InkUsesPerAcquisition}회";
                        noticeUntil = Time.unscaledTime + 2.5f;
                    }
                    catch (InvalidOperationException e) { Notice = e.Message; noticeUntil = Time.unscaledTime + 2.5f; }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button($"무기 구성 열기 ({PanelKey})", button)) PanelOpen = !PanelOpen;
            GUILayout.EndArea();
        }

        void DrawPanel(bool compact = false)
        {
            var a = State.Arsenal;
            if (a.RemainingInkUses(selectedInk) == 0) selectedInk = null;
            var r = compact ? new Rect(Screen.width - 540, 20, 520, Screen.height - 40) : new Rect(Screen.width / 2 - 380, 40, 760, Screen.height - 80);
            GUI.Box(r, "");
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"무기 구성   변경 대기 {Mathf.Ceil(a.ChangeCooldownRemaining)}초", title);
            if (GUILayout.Button("닫기", button, GUILayout.Width(90))) PanelOpen = false;
            GUILayout.EndHorizontal();
            if (State.Banked > 0 && GUILayout.Button($"받아 둔 보상 열기 ({State.Banked}개) [{OpenRewardKey}]", button)) { PanelOpen = false; TryOpenBankedReward(); }
            if (GUILayout.Button("전투 중 내구도 표시: " + (DurabilityAsNumber ? "숫자" : "게이지"), button)) DurabilityAsNumber = !DurabilityAsNumber;
            for (int slot = 0; slot < 2; slot++)
            {
                var e = a.Equipped(slot);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{(slot == 0 ? "A (A/Q)" : "B (S/W)")}: " + (e == null ? "맨손" : $"{FormName(e.Form)} 내구도 {e.DurabilityLeft:N0}/{e.MaxDurability:N0}"), box);
                if (e != null && GUILayout.Button("해제", button, GUILayout.Width(80))) Try(() => a.Unequip(slot));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("잉크 도감 (골라서 칸을 누르세요): " + (!a.AvailableInks.Any() ? "없음" : ""), box);
            GUILayout.BeginHorizontal();
            foreach (var ink in a.AvailableInks.ToArray())
                if (GUILayout.Toggle(selectedInk == ink, $"{InkName(ink)} / {a.RemainingInkUses(ink)}회 (예약 {a.ReservedInkUses(ink)})", button)) selectedInk = ink; else if (selectedInk == ink) selectedInk = null;
            GUILayout.EndHorizontal();
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            foreach (var w in a.Weapons.ToArray())
            {
                var state = a.StateOf(w);
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{FormName(w.Form)} / {SkillName(w.Form, w.Skill)}  [{(state == SketchWeaponState.Unconfirmed ? "미확정" : state == SketchWeaponState.Equipped ? "장착" : "보관")}]  내구도 {w.DurabilityLeft:N0}/{w.MaxDurability:N0}" + (w.SkillCooldown > 0 ? $"  스킬 {w.SkillCooldown:0.0}초" : ""));
                GUILayout.BeginHorizontal();
                for (int i = 0; i < w.Slots.Count; i++)
                {
                    var s = w.Slots[i]; int index = i;
                    string label = (s.Enhance == null ? "무색" : ColorName(s.Enhance) + "강화") + "\n" + (s.Ink == null ? "비어 있음" : InkName(s.Ink) + (a.UsesEnhancedValue(s) ? " ★" : "")) + (w.Confirmed ? " 🔒" : "");
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = wasEnabled && !w.Confirmed;
                    if (GUILayout.Button(label, button, GUILayout.Height(56)))
                    {
                        if (s.Ink != null && !s.Fixed) Try(() => a.ClearInk(w.Id, index));
                        else if (selectedInk != null) Try(() => a.PlaceInk(w.Id, index, selectedInk));
                    }
                    GUI.enabled = wasEnabled;
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (state == SketchWeaponState.Unconfirmed && GUILayout.Button("구성 확정", button)) Try(() => a.Confirm(w.Id));
                if (state == SketchWeaponState.Stored)
                {
                    if (GUILayout.Button("A에 장착", button)) Try(() => a.Equip(w.Id, 0));
                    if (GUILayout.Button("B에 장착", button)) Try(() => a.Equip(w.Id, 1));
                }
                if (state != SketchWeaponState.Equipped && GUILayout.Button("버리기", button)) Try(() => a.Discard(w.Id));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void Try(Action action)
        {
            try { action(); }
            catch (InvalidOperationException e) { Notice = e.Message; noticeUntil = Time.unscaledTime + 2.5f; }
        }
    }
}
