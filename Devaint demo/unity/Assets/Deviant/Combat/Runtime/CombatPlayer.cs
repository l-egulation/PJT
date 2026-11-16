using System.Collections.Generic;
using UnityEngine;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;

namespace Deviant.Combat
{
    public partial class CombatPlayer : CombatActor
    {
        public WeaponLoadoutView weapons { get; private set; }
        public event System.Action Died;
        public System.Func<float> RunRevival { get; set; }
        public bool TryReviveFromRun()
        {
            if (!Dead || RunRevival == null) return false;
            float restored = RunRevival();
            if (!(restored > 0) || float.IsNaN(restored) || float.IsInfinity(restored)) return false;
            health = Mathf.Min(maxHealth, restored);
            CancelAction(); ClearHitMotionLocks(); ClearInputState();
            if (visual) visual.color = Color.white;
            arena.Record("부활 · 최대 체력의 50%"); return true;
        }
        // Every player heal goes through here so the heal sound plays when health actually rises. Back-to-back heals
        // (per-hit lifesteal, two sources in one frame) share one sound within HealSoundInterval of combat time.
        public const float HealSoundInterval = .5f;
        float lastHealSound = float.NegativeInfinity;
        public float Heal(float amount)
        {
            float before = health;
            health = Mathf.Min(maxHealth, health + Mathf.Max(0, amount) * (arena ? arena.AugmentHealingScale : 1));
            float restored = health - before;
            if (restored > 0 && !Dead && arena && (arena.Now < lastHealSound || arena.Now - lastHealSound >= HealSoundInterval))
            { lastHealSound = arena.Now; arena.Audio?.EmitSelected("sfx_player_heal", Position); }
            return restored;
        }
        public event System.Action WeaponOrderSwapped;
        public bool TrySwapWeaponOrder()
        {
            if (!arena || Dead || InputBlocked || arena.Controls.Current.SimulationPaused || Time.timeScale == 0 ||
                !IsWeaponSlotReady(0) || !IsWeaponSlotReady(1) || currentCast || InkCharging || Dashing ||
                arena.Now < stunUntil || arena.Now < staggerUntil || PortalPhase != CheckpointPhase.None) return false;
            // Sketch-weapon runs swap what A and B hold once one weapon is equipped (A bare hands, B weapon -> A weapon,
            // B bare hands); the sketch arsenal owns the slots and the arena rebuilds the two combat weapons from it.
            if (arena.Sketch != null)
            {
                if (!arena.Sketch.CanSwapOrder) return false;
                actionArbiter.ClearReservations(CombatCommandReason.Cleared);
                ClearInputState();
                // Swap the combat instances first (the current one stays current), then the arsenal; the loadout sync
                // then finds every instance already in place and replaces nothing.
                weapons.SwapSlots();
                arena.Sketch.SwapOrder();
                arena.SyncSketchLoadout();
                return true;
            }
            // Discard slot-addressed reservations before changing their meaning. Do not
            // recreate weapons: cooldowns, palettes and outstanding effects keep their identity.
            actionArbiter.ClearReservations(CombatCommandReason.Cleared);
            ClearInputState();
            weapons.SwapSlots();
            WeaponOrderSwapped?.Invoke();
            return true;
        }
        public void BindLoadout(Deviant.Equipment.Contracts.IWeaponLoadout loadout)
        {
            if (weapons != null || arena != null) throw new System.InvalidOperationException("Bind the explicit equipment owner once before configuring the player.");
            weapons = new WeaponLoadoutView(loadout ?? throw new System.ArgumentNullException(nameof(loadout)));
        }
        public int selected { get => weapons.ActiveSlot; set => weapons.SelectSlot(value); }
        public Deviant.Equipment.Contracts.WeaponLoadoutSnapshot Loadout => weapons.Snapshot;
        public WeaponState Weapon => weapons.ActiveWeapon;
        // 숨죽이기 은신이 풀린 뒤(공격으로 깸 또는 시간 만료) ExitSeconds 동안 기본공격만 빨라진다.
        public float StealthExitHasteScale
        {
            get
            {
                var rule = arena ? arena.StealthAmbush : null;
                if (rule == null || rule.ExitBasicSpeedBonus <= 0 || rule.ExitSeconds <= 0) return 1;
                float now = arena.Now;
                bool broken = now < stealthExitHasteUntil;
                bool expired = stealthUntil > 0 && now >= stealthUntil && now < stealthUntil + rule.ExitSeconds;
                return broken || expired ? 1 + rule.ExitBasicSpeedBonus : 1;
            }
        }
        public float BasicSpeed => arena ? arena.RunBasicSpeedScale(Weapon.Speed, StealthExitHasteScale, arena.AugmentBasicSpeedScale) : Weapon.Speed * StealthExitHasteScale;
        int readyWeaponSlots = 3;
        public bool IsWeaponSlotReady(int slot) => slot >= 0 && slot < 2 && (readyWeaponSlots & (1 << slot)) != 0;
        // A scene may reserve the fixed equipment pair until its starting choices
        // are granted. Ordinary combat compositions start with both slots ready.
        public void SetWeaponSlotReady(int slot, bool ready)
        {
            if (slot < 0 || slot >= 2) throw new System.ArgumentOutOfRangeException(nameof(slot));
            if (IsWeaponSlotReady(slot) == ready) return;
            if (arena && slot == selected) { CancelAction(); ClearInputState(); }
            if (ready) readyWeaponSlots |= 1 << slot; else readyWeaponSlots &= ~(1 << slot);
        }
        public FormData Form { get { return arena.catalog.forms[Weapon.FormIndex]; } }
        public SkillData Special { get { return Form.specials[Weapon.SpecialIndex]; } }
        public readonly PlayerJumpExecution jumps = new PlayerJumpExecution();
        PlayerActionArbiter actionArbiter;
        // Set only while an admitted other-weapon start replaces or follows the previous action.
        WeaponSwitchStart weaponSwitchStart;
        WeaponState weaponSwitchFrom;
        // After the outgoing action's first hit/effect the new first attack's windup shrinks by
        // the global rate and the outgoing weapon's switch ink, multiplied together.
        float SwitchWindupScale => weaponSwitchStart == WeaponSwitchStart.CancelAfterHit || weaponSwitchStart == WeaponSwitchStart.Chain
            ? (1 - arena.WeaponSwitch.AfterHitWindupReduction) * (1 - arena.MvpSwitchWindupReduction(weaponSwitchFrom)) : 1;
        void ReplaceForWeaponSwitch() { if (weaponSwitchStart != WeaponSwitchStart.None && currentCast) CancelAttack(); }
        readonly PlayerDashExecution dash = new PlayerDashExecution();
        // Existing scene/test accessors submit continuous samples to the same ingress.
        // A key still held toward the wall a kick left counts as no input until it is released.
        public int axis { get => kickHeldAxis != 0 && CurrentInput.Axis == kickHeldAxis ? 0 : CurrentInput.Axis;
            set => SetContinuousInput(value, CurrentInput.SpecialHeld); }
        public bool specialHeld { get => CurrentInput.IsSpecialHeld(selected); set => SetContinuousInput(axis, value); }
        public PlayerDashSnapshot Dash => dash.Read();
        public bool Dashing { get { return dash.IsDashing(arena.Now); } }
        public System.Func<bool> EncounterProtection;
        public System.Func<bool> JumpAdmission;
        public System.Func<Vector2?> FallDestination;
        public bool Invulnerable { get { return (EncounterProtection?.Invoke() ?? false) || PortalPhase == CheckpointPhase.Appearing || arena.Now < respawnProtect || arena.Now < arena.PlayerHitProtectUntil || dash.IsInvulnerable(arena.Now, arena.Movement) || currentCast && currentCast.Invulnerable; } }
        public float DashRemaining { get { return dash.Remaining(arena.Now); } }
        // Dash charges sharing one recovery; the village extra-dash unlock raises it to 2 at departure.
        public int DashCharges { get => dash.MaxCharges; set => dash.MaxCharges = value; }
        public int DashAvailable => dash.Available(arena.Now);
        public float ApproachRemaining { get { foreach(var c in arena.casts) if(c && c.owner==this && c.RecastTimeFor(Weapon)>0) return c.RecastTimeFor(Weapon); return 0; } }
        // Scenes that allow maintenance only at a facility supply this check instead of the combat-free rule.
        public System.Func<bool> MaintenanceSite;
        public bool EditingAllowed { get { return arena.TestConfigurationOpen && arena.Controls.Current.SimulationPaused || !currentCast && !Dashing && (MaintenanceSite != null ? MaintenanceSite() : !arena.CombatActive); } }
        public string MaintenanceUnavailableReason => MaintenanceSite != null ? "정비소에서만 잉크를 장착·해제할 수 있습니다." : "전투 중이거나 동작 중에는 정비할 수 없습니다.";
        public bool FallProtected => arena.Now < respawnProtect;
        float respawnProtect;
        PlayerComboExecution combo = PlayerComboExecution.Create();
        PlayerTickExecution tick;
        readonly SafeGroundHistory floors = new SafeGroundHistory();
        public float LastFallTime { get; private set; } = -1;
        public float LastRestoreTime { get; private set; } = -1;
        public Vector2 LastRestorePosition { get; private set; }
        public int BasicIndex { get { return combo.BasicIndex; } }
        bool legacyInputBlocked;
        public bool InputBlocked
        {
            get => legacyInputBlocked || arena != null && arena.Controls != null && arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat);
            set
            {
                if (legacyInputBlocked != value) DiscardInput(value ? CombatInputDiscardReason.Blocked : CombatInputDiscardReason.Cleared);
                legacyInputBlocked = value;
            }
        }
        public bool UseTestInput;
        public Vector2 spawn;

        public void Step(float dt)
        {
            if (StepCheckpoint())
            {
                for (int i=0;i<weapons.Count;i++) weapons[i].Cooldown.Tick(arena.Now);
                ResolveShurikenReturns();
                return;
            }
            ExpireBufferedJump();
            CompleteDashCommand();
            var host = new PlayerTickHost(this);
            try { tick.Step(dt, ref host); }
            finally { CompleteDashCommand(); }
        }
        public void Landed()
        {
            if (jumps.Land(arena.Now, arena.Movement, out float vy))
            {
                VelocityY = vy; BeginJumpApex(); jumps.ArmCut(true); grounded = false;
                WallLedgeStartedAt = float.NegativeInfinity;
                arena.Audio?.EmitSelected("sfx_player_jump", Position); // A buffered press lands as a first jump.
                var key = bufferedJump; bufferedJump = default;
                var execution = System.Guid.NewGuid();
                arena.RecordCommand(key, CombatCommandState.Started, executionId: execution);
                arena.RecordCommand(key, CombatCommandState.Completed, executionId: execution);
            }
            else ClearBufferedJump(CombatCommandReason.BufferExpired);
        }
        void ApplyJumpRelease(CombatInput input)
        {
            if (jumps.ObserveHold(input.Has(CombatButtons.Jump), (input.Released & CombatButtons.Jump) != 0) && JumpApexArmed)
                ScaleJumpRise(arena.Movement.JumpReleaseVelocityScale);
        }
        public float AirJumpStartedAt { get; private set; } = float.NegativeInfinity;
        CombatActionAttempt TryJump(CombatCommandKey command)
        {
            if (!Abilities.Has(MovementAbilities.Jump)) return CombatActionAttempt.Reject(CombatCommandReason.Unavailable);
            if(JumpAdmission!=null&&!JumpAdmission())return CombatActionAttempt.Reject(CombatCommandReason.Unavailable);
            if (currentCast && !currentCast.isBasic) return CombatActionAttempt.Reject(CombatCommandReason.Busy);
            if (WallLedgeClimbing) return CombatActionAttempt.Reject(CombatCommandReason.Busy);
            if (TryWallJump()) return CombatActionAttempt.Start(System.Guid.NewGuid(), immediate: true);
            if (TryBufferWallJump(command)) return CombatActionAttempt.Buffer();
            if (!jumps.Press(arena.Now, arena.Movement, out float vy))
            {
                ClearBufferedJump(CombatCommandReason.Replaced);
                bufferedJump = command;
                return CombatActionAttempt.Buffer();
            }
            ClearBufferedJump(CombatCommandReason.Replaced);
            bool cancelDash = Dashing;
            CancelAction(CombatCommandReason.Priority);
            VelocityY = vy; BeginJumpApex(); grounded = false; tick.HadGround = false;
            WallLedgeStartedAt = float.NegativeInfinity;
            if (jumps.Used == 2) AirJumpStartedAt = arena.Now;
            // A new jump is a new intent: the key held through a wall kick steers again.
            kickHeldAxis = 0;
            // Dash-cancel jumps keep their full carry arc.
            jumps.ArmCut(!cancelDash);
            if (cancelDash) dash.BeginJumpCarry(arena.Now, arena.Movement);
            // The first jump has its own cloth flick; the second also gets the puff of air it steps on (DoubleJumpPuff).
            if (jumps.Used == 1) arena.Audio?.EmitSelected("sfx_player_jump", Position);
            if (jumps.Used == 2) { arena.Audio?.EmitSelected("sfx_player_double_jump", Position); DoubleJumpPuff.Spawn(arena, Position, visual); }
            return CombatActionAttempt.Start(System.Guid.NewGuid(), immediate: true);
        }
        public event System.Action DashStarted;
        public System.Func<bool> DashAdmission;
        CombatActionAttempt TryDash(CombatCommandKey command)
        {
            if (!Abilities.Has(MovementAbilities.Dash)) return CombatActionAttempt.Reject(CombatCommandReason.Unavailable);
            if(DashAdmission!=null&&!DashAdmission())return CombatActionAttempt.Reject(CombatCommandReason.Unavailable);
            if (!dash.CanStart(arena.Now)) return CombatActionAttempt.Reject(CombatCommandReason.Cooldown);
            CancelAction(CombatCommandReason.Priority);
            int direction = axis == 0 ? facing : axis;
            SyncFacing(direction);
            dash.Begin(arena.Now, facing, arena.Movement);
            arena.Audio?.EmitSelected("sfx_player_dash", Position);
            DashStarted?.Invoke();
            VelocityY = 0; jumps.ArmCut(false); jumps.ClearBuffer(); ClearBufferedJump(CombatCommandReason.Interrupted);
            dashCommand = command; dashExecution = System.Guid.NewGuid();
            return CombatActionAttempt.Start(dashExecution);
        }
        public void CancelAction(CombatCommandReason reservationReason = CombatCommandReason.Interrupted)
        {
            CompleteDashCommand();
            CancelWallMovement(CombatCommandReason.Interrupted);
            CancelAttack(); dash.Cancel(arena.Now); CancelDashCommand(resettingCommands ? CombatCommandReason.Reset : CombatCommandReason.Interrupted); actionArbiter.ClearReservations(reservationReason);
            combo.Clear();
        }
        CombatActionAttempt BeginBasic(CombatCommandKey command, int index, float startAt = -1)
        {
            if (!IsWeaponSlotReady(selected)) return CombatActionAttempt.Reject(CombatCommandReason.Unavailable);
            var host = new PlayerStartHost(this);
            return PlayerActionStartExecution.BeginBasic(command, index, startAt, ref host);
        }
        SkillData SpecialSource()
        {
            var host = new PlayerStartHost(this);
            return PlayerActionStartExecution.SpecialSource(ref host);
        }
        bool SpecialGroundAvailable()
        {
            if(Weapon.ExperimentalChargeStamp)return true;
            var host = new PlayerStartHost(this);
            return PlayerActionStartExecution.GroundAvailable(ref host);
        }
        CombatActionAttempt BeginSpecial(CombatCommandKey command, float startAt = -1)
        {
            if (!IsWeaponSlotReady(selected)) return CombatActionAttempt.Reject(CombatCommandReason.Unavailable);
            var host = new PlayerStartHost(this);
            if(Weapon.ExperimentalChargeStamp)return BeginInkCharge(command);
            return PlayerActionStartExecution.BeginSpecial(command, startAt, ref host);
        }
        public void AttackFinished(CombatCast cast)
        {
            if (currentCast != cast) return;
            cast.EndCommand(CombatCommandState.Completed);
            currentCast = null;
            actionArbiter.ActionFinished(cast.isBasic, cast.EndTime);
        }
        CombatActionAttempt SwitchNow(CombatCommandKey command)
        {
            if (!IsWeaponSlotReady(1 - selected)) return CombatActionAttempt.Reject(CombatCommandReason.Unavailable);
            foreach(var c in arena.casts) if(c && c.owner==this) c.ForgetApproach();
            if(arena.MvpGrowth!=null)arena.SwitchMvpWeapon(Weapon,weapons[1-selected]);
            weapons.SelectSlot(1 - selected); combo.Clear();
            arena.Record("weapon slot " + (selected + 1));
            if (arena.Sketch != null) return CombatActionAttempt.Start(System.Guid.NewGuid(), immediate: true);
            if(arena.MvpGrowth!=null)return BeginBasic(command,0);
            return CombatActionAttempt.Start(System.Guid.NewGuid(), immediate: true);
        }
        public void Reconfigure(int slot, int form, int special) => TryReconfigure(slot, form, special, out _);
        public bool TryReconfigure(int slot, int form, int special, out string error)
        {
            error = null;
            if (arena == null || !EditingAllowed) { error = "Equipment maintenance is unavailable."; return false; }
            var forms = arena.catalog?.forms;
            if (slot < 0 || slot >= weapons.Count || forms == null || form < 0 || form >= forms.Length || forms[form]?.specials == null ||
                special < 0 || special >= forms[form].specials.Length || forms[form].specials[special] == null)
            { error = "Equipment selection does not exist in the current catalog."; return false; }
            var skill = forms[form].specials[special]; Deviant.Equipment.Contracts.WeaponConfiguration configuration;
            try { configuration = new Deviant.Equipment.Contracts.WeaponConfiguration(new Deviant.Equipment.Contracts.WeaponSelection(form, special), skill.capacity, skill.cooldown); }
            catch (System.ArgumentException exception) { error = exception.Message; return false; }
            var result = weapons.Reconfigure(slot, configuration, arena.Now);
            if (result.Status != Deviant.Equipment.Contracts.EquipmentCommandStatus.Succeeded) { error = result.Error; return false; }
            var weapon = weapons[slot];
            weapon.TestFormSpeedScale = arena.TestBasicSpeed(forms[form].id);
            if(weapon.ExperimentalChargeStamp && InkPartCatalog.Profile(forms[form].id)==null)weapon.ExperimentalChargeStamp=false;
            weapon.Cooldown.Reconfigure(skill.capacity, weapon.CooldownPeriod(skill.cooldown), arena.Now);
            foreach(var c in arena.casts) if(c && c.owner==this && c.RecastTimeFor(weapon)>0) c.ForgetApproach();
            combo.Clear(); return true;
        }
        public void QueueTestInput(bool basic = false, bool special = false, bool dash = false, bool jump = false, bool swap = false)
        {
            var buttons = (basic ? CombatButtons.Basic : CombatButtons.None) | (special ? CombatButtons.Special : CombatButtons.None)
                | (dash ? CombatButtons.Dash : CombatButtons.None) | (jump ? CombatButtons.Jump : CombatButtons.None) | (swap ? CombatButtons.Swap : CombatButtons.None);
            SubmitInput(new CombatInput(axis, buttons, CurrentInput.SpecialHeld, climbAxis: CurrentInput.ClimbAxis, heldSpecials: CurrentInput.HeldSpecials));
        }
        CombatActionAttempt TryApproachRecast(CombatCommandKey command)
        {
            foreach(var c in arena.casts.ToArray()) if(c && c.owner==this && c.TryApproachRecast(Weapon, command))
                return CombatActionAttempt.Start(c.ExecutionId, outcomeRecorded: true);
            return CombatActionAttempt.Reject(CombatCommandReason.Unavailable);
        }
        public void StartApproach(CombatCast cast) { CancelAction(CombatCommandReason.Priority); currentCast=cast; }
        public void Die()
        {
            if (TryReviveFromRun()) return;
            ClearCheckpoint();
            ClearShurikenReturns();
            ClearBufferedJump(CombatCommandReason.Dead);
            CancelAction(); DiscardInput(CombatInputDiscardReason.Dead, true); arena.RemoveOwnedEffects(this);
            arena.Record("RUN ENDED - test profile only. F5: new test run");
            Died?.Invoke();
        }
        public void Fall()
        {
            var host = new PlayerFallHost(this);
            PlayerFallRecoveryExecution.Fall(ref host);
        }
        public void ResetPlayer()
        {
            ResetInputSession(false);
            ResetPlayerInInputSession();
        }
        internal void ResetPlayerInInputSession()
        {
            ClearCheckpoint();
            ClearShurikenReturns();
            ResetActor(spawn); jumps.Reset(); floors.Clear(); tick.HadGround = false;
            AirJumpStartedAt = float.NegativeInfinity;
            CancelWallMovement(CombatCommandReason.Reset);
            dash.Reset(); CancelDashCommand(CombatCommandReason.Reset); ClearBufferedJump(CombatCommandReason.Reset); respawnProtect = 0;
            CancelAction(); DiscardInput(CombatInputDiscardReason.Reset, true);
            LastFallTime=LastRestoreTime=-1;
            foreach (var w in weapons)
            {
                var s = arena.catalog.forms[w.FormIndex].specials[w.SpecialIndex];
                w.Cooldown.Reset(s.capacity, w.CooldownPeriod(s.cooldown));
            }
            if (visual) visual.color = Color.white;
            resettingCommands = false;
        }
    }
}
