using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        readonly struct PlayerStartHost : IPlayerActionStartHost
        {
            readonly CombatPlayer player;
            public PlayerStartHost(CombatPlayer player) { this.player = player; }
            public int Axis => player.axis;
            public void SyncFacing(int direction) => player.SyncFacing(direction);
            public void BeginBasicCombo(int index) => player.combo.BeginBasic(index);
            public FormData Form => player.Form;
            public int SpecialIndex => player.Weapon.SpecialIndex;
            public float WeaponSpeed => player.BasicSpeed;
            public BasicMeleeDefinition BasicMelee(string formId, int index) => player.arena.GetBasicMelee(formId, index);
            public RangedActionDefinition RangedBasic(string form, int index) => player.arena.GetRangedBasic(form, index);
            public RangedActionDefinition RangedSpecial(string id) => player.arena.GetRangedAction(id);
            public SkillData Special => player.Special;
            public ActionTrackDefinition ActionTracks(string id) => player.arena.GetActionTracks(id);
            public DirectMeleeSpecialDefinition DirectSpecial(string id) => player.arena.GetDirectMeleeSpecial(id);
            public ChargedMeleeDefinition ChargedMelee(string id) => player.arena.GetChargedMelee(id);
            public AuthoredSkillDefinition AuthoredSpecial(string id) => player.arena.GetAuthoredSkill(id);
            public bool ShurikenReturnPending() => player.ShurikenReturnPending(player.Weapon);
            public bool ProbeGround() => player.ProbeGround();
            public void RecordUnavailableSpecial() => player.arena.Record("특수 공격 설계 대기");
            public void ClearCombo() => player.combo.Clear();
            public void ClearJumpBuffer() => player.jumps.ClearBuffer();
            public void ClearBufferedJump() => player.ClearBufferedJump(CombatCommandReason.Interrupted);
            public Guid CreateBasic(SkillData data, CombatCommandKey command, float startAt, int index, BasicMeleeDefinition definition, MeleeActionDefinition timeline, RangedActionDefinition ranged)
            {
                var frame = definition == null ? null : player.arena.GetMeleeFrame(data.id, true);
                if (frame?.Mode == MeleeTimingMode.FrameTime)
                {
                    timeline = frame.CompileForStart(player.BasicSpeed);
                    data.activation = timeline.Impact; data.end = timeline.End; data.hits[0] = timeline.CreateHit();
                    data.basicLungeDistance = timeline.AdvanceDistance; data.basicLungeStart = timeline.AdvanceStart; data.basicLungeDuration = timeline.AdvanceDuration;
                    definition = frame.Action;
                }
                float extraRecovery = index == player.Form.basics.Length - 1
                    ? player.arena.TestLastBasicRecovery(player.Form.id) : 0;
                if (extraRecovery > 0)
                {
                    timeline = timeline?.WithExtraRecovery(extraRecovery);
                    data.end = timeline != null ? timeline.End : data.end + extraRecovery;
                }
                player.ReplaceForWeaponSwitch();
                player.currentCast = CombatCast.Create(player.arena, player, data, player.Weapon, true, startAt, definition?.ContentHash ?? ranged?.ContentHash, timeline,
                    frame?.Mode == MeleeTimingMode.FrameTime ? frame : null, command: command, rangedDefinition: ranged, windupScale: player.SwitchWindupScale);
                return player.currentCast.ExecutionId;
            }
            public Guid CreateSpecial(SkillData source, CombatCommandKey command, float startAt, string version, MeleeActionDefinition timeline,
                DirectMeleeSpecialDefinition direct, ChargedMeleeDefinition charged, ActionTrackDefinition tracks, AuthoredSkillDefinition authored, RangedActionDefinition ranged)
            {
                var frame = direct == null ? null : player.arena.GetMeleeFrame(source.id, false);
                if (frame?.Mode == MeleeTimingMode.FrameTime)
                {
                    direct = frame.Direct; source = direct.ToData(); timeline = frame.CompileForStart(1); version = direct.ContentHash;
                }
                var specialFrame = player.arena.GetSpecialFrame(source.id);
                if (specialFrame != null)
                {
                    tracks = specialFrame.Finite; authored = specialFrame.Authored; version = specialFrame.ContentHash;
                    source = authored?.ToData() ?? tracks.ToData();
                }
                // Admission checks have passed. Cancel pending basic hits and its presentation
                // before replacing the cast; do not invoke the normal completion/queue path.
                if (player.currentCast && player.currentCast.isBasic) player.CancelAttack();
                player.ReplaceForWeaponSwitch();
                player.currentCast = CombatCast.Create(player.arena, player, source, player.Weapon, false, startAt, version, timeline,
                    frameTiming: frame?.Mode == MeleeTimingMode.FrameTime ? frame : null,
                    specialDefinition: direct, chargedDefinition: charged, actionTracks: tracks, command: command, authored: authored, rangedDefinition: ranged, specialFrame: specialFrame,
                    windupScale: player.SwitchWindupScale);
                return player.currentCast.ExecutionId;
            }
        }
    }
}
