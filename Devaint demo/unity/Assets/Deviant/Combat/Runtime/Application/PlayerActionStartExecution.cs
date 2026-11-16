using System;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // No new action/charge/resource owner: this resolves the existing live start
    // payload and admission order, then invokes the one Native create boundary.
    public static class PlayerActionStartExecution
    {
        public static CombatActionAttempt BeginBasic<T>(CombatCommandKey command, int index, float startAt, ref T host) where T : struct, IPlayerActionStartHost
        {
            if (host.Axis != 0) host.SyncFacing(host.Axis);
            host.BeginBasicCombo(index);
            var definition = host.BasicMelee(host.Form.id, index);
            var ranged = definition == null ? host.RangedBasic(host.Form.id, index) : null;
            BasicData basic = definition != null ? definition.ToData() : ranged != null ? ranged.ToBasicData() : host.Form.basics[index];
            float rate = host.WeaponSpeed;
            var timeline = definition == null ? null : MeleeActionDefinition.ForBasic(definition, rate, host.Form.inputSpeed);
            // JsonUtility can materialize absent inline DTOs as zero-valued objects.
            // A positive authored interval, rather than object presence, opts into the step.
            var advance = basic.advance != null && basic.advance.duration > 0 ? basic.advance : null;
            var data = new SkillData {
                kind = basic.kind, bomb = basic.bomb, ranged = basic.ranged,
                id = host.Form.id + "_basic_" + index, name = host.Form.name + " " + (index + 1), activation = basic.impact / rate,
                end = basic.duration / rate, inputSpeed = host.Form.inputSpeed, vertical = "normal", facing = "windup_start",
                invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                basicLungeDistance = timeline != null ? timeline.AdvanceDistance : advance != null ? advance.distance : basic.kind != "ranged" && host.Form.inputSpeed > 0 ? .65f : 0,
                basicLungeStart = timeline != null ? timeline.AdvanceStart : advance != null ? advance.start / rate : NativeMaximum(0, basic.impact - .04f) / rate,
                basicLungeDuration = timeline != null ? timeline.AdvanceDuration : (advance != null ? advance.duration : .08f) / rate,
                hits = new[] { new HitData { time = basic.impact / rate, damage = basic.damage, active = basic.active / rate, box = basic.box,
                    anchor = basic.kind == "bomb" || basic.kind == "ranged" ? "ranged_projectile" : "actor", targets = "all" } }
            };
            return CombatActionAttempt.Start(host.CreateBasic(data, command, startAt, index, definition, timeline, ranged?.CaptureBasicStart(data)), outcomeRecorded: true);
        }
        public static SkillData SpecialSource<T>(ref T host) where T : struct, IPlayerActionStartHost
        {
            var source = host.Special;
            return host.RangedSpecial(source.id)?.ToData() ?? host.AuthoredSpecial(source.id)?.ToData() ?? host.DirectSpecial(source.id)?.ToData() ?? host.ChargedMelee(source.id)?.ToData() ?? source;
        }
        public static bool GroundAvailable<T>(ref T host) where T : struct, IPlayerActionStartHost
            => (host.ActionTracks(host.Special.id)?.Execution.Vertical ?? SpecialSource(ref host).vertical) != "grounded_required" || host.ProbeGround();
        public static CombatActionAttempt BeginSpecial<T>(CombatCommandKey command, float startAt, ref T host) where T : struct, IPlayerActionStartHost
        {
            var tracks = host.ActionTracks(host.Special.id); var selectedRanged = tracks == null ? host.RangedSpecial(host.Special.id) : null;
            var source = selectedRanged?.ToData() ?? (tracks == null ? SpecialSource(ref host) : host.Special);
            if (tracks == null && source.ranged != null && source.ranged.returnDelay > 0 && host.ShurikenReturnPending()) return CombatActionAttempt.Reject(CombatCommandReason.ReturnPending);
            if (tracks == null && source.unavailable) { host.RecordUnavailableSpecial(); return CombatActionAttempt.Reject(CombatCommandReason.Unavailable); }
            if (!GroundAvailable(ref host)) return CombatActionAttempt.Reject(CombatCommandReason.GroundRequired);
            if ((tracks?.Execution.Facing ?? source.facing) == "windup_input_else_facing" && host.Axis != 0) host.SyncFacing(host.Axis);
            // Skills start without an enemy in reach. Movement may select one anchor later; area damage is not restricted to that anchor.
            host.ClearCombo(); host.ClearJumpBuffer(); host.ClearBufferedJump();
            var direct = host.DirectSpecial(source.id); var charged = host.ChargedMelee(source.id); var authored = host.AuthoredSpecial(source.id); var ranged = selectedRanged;
            return CombatActionAttempt.Start(host.CreateSpecial(source, command, startAt, ranged?.ContentHash ?? authored?.ContentHash ?? tracks?.ContentHash ?? direct?.ContentHash ?? charged?.ContentHash,
                direct?.Compile(), direct, charged, tracks, authored, ranged), outcomeRecorded: true);
        }
        static float NativeMaximum(float first, float second) => first > second ? first : second;
    }
}
