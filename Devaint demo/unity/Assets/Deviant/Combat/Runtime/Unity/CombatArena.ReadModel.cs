using System;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        ArenaCombatReadModel readModel;
        public ICombatReadModel ReadModel => readModel ?? (readModel = new ArenaCombatReadModel(this));

        // Native references enter only through this binding adapter. Existing
        // player/enemy/decoy membership is the registration authority.
        public bool TryBindActor(CombatActor actor, out CombatActorHandle handle)
        {
            handle = default;
            if (!ReadModel.TryGetSession(out var session) || !actor || actor.arena != this || actor.ActorId <= 0 ||
                !ReferenceEquals(FindReadActor(actor.ActorId), actor)) return false;
            handle = new CombatActorHandle(session, actor.ActorId);
            return true;
        }

        CombatActor FindReadActor(int actorId)
        {
            if (actorId <= 0) return null;
            if (Player && Player.arena == this && Player.ActorId == actorId) return Player;
            for (int i = 0; i < enemies.Count; i++)
            {
                var actor = enemies[i];
                if (actor && actor.arena == this && actor.ActorId == actorId) return actor;
            }
            for (int i = 0; i < decoys.Count; i++)
            {
                var actor = decoys[i];
                if (actor && actor.arena == this && actor.ActorId == actorId) return actor;
            }
            return null;
        }

        void ReleaseReadModel() => readModel?.Dispose();
        ActorReadSnapshot CaptureActor(CombatActorHandle handle, CombatActor actor, CombatCast action = null)
        {
            var player = ReferenceEquals(actor, Player) ? Player : null;
            var weapon = player ? player.Weapon : null;
            var form = player ? player.Form : null;
            var current = action ? action : actor.currentCast;
            var presentation = new CharacterPresentationReadSnapshot(player ? player.axis : 0, player ? player.BasicIndex : -1,
                Movement?.DashDuration ?? 0, current, current && current.isBasic, current && current.Approaching,
                current ? current.DashVisualDuration : 0, current ? current.PresentationImpactTime : 0, current ? current.PresentationMotionDuration : 0,
                current && !(current.SpecialFrame?.UsesFrameTime ?? false) && (current.ChargedMelee != null || !current.isBasic && current.DirectMeleeSpecial == null && !current.UsesCompiledTracks),
                player ? player.AppearanceOverride ?? MotionSourceRegistry.ConversionForm(form.id,weapon.SpecialIndex) : form?.id, player && (player.WallClimbing || player.WallJumpInFlight));
            return new ActorReadSnapshot(handle, SimulationTickCount, Now, player,
                actor.Kinematics, actor.Vitals, actor.Statuses, action ? action.ReadAction() : actor.currentCast ? actor.currentCast.ReadAction() : default,
                player ? player.Dash : default, player ? player.jumps.Read() : default,
                weapon != null ? weapon.InstanceId : Guid.Empty, form?.id, form?.name, presentation,
                player ? new PlayerWeaponResourcesReadSnapshot(player.selected, CaptureWeaponCharges(player.weapons[0]), CaptureWeaponCharges(player.weapons[1])) : default);
        }
        WeaponChargeReadSnapshot CaptureWeaponCharges(WeaponState weapon)
        {
            var form = catalog.forms[weapon.FormIndex]; var charges = weapon.Cooldown.Snapshot;
            if(weapon.ExperimentalChargeStamp)return new WeaponChargeReadSnapshot(weapon.InstanceId,form.id,"ink_charge_"+form.id,1,Player.InkCharging && Player.Weapon==weapon?0:1,0,0);
            return new WeaponChargeReadSnapshot(weapon.InstanceId, form.id, form.specials[weapon.SpecialIndex].id,
                charges.Capacity, charges.Available, charges.Period, charges.NextReady);
        }

        sealed class ArenaCombatReadModel : ICombatReadModel, IDisposable
        {
            CombatArena arena;
            public ArenaCombatReadModel(CombatArena arena) { this.arena = arena; }
            public bool TryGetSession(out CombatInputSession session)
            {
                session = default;
                if (!arena || arena.CompositionProfile == CombatCompositionProfile.Unconfigured || !arena.Player ||
                    arena.Player.arena != arena || arena.Player.ActorId <= 0) return false;
                return arena.Player.TryReadInputSession(out session);
            }
            public bool TryReadPlayer(CombatInputSession session, out ActorReadSnapshot snapshot)
                => TryReadActor(new CombatActorHandle(session, session.ActorId), out snapshot);
            public bool TryReadActor(CombatActorHandle handle, out ActorReadSnapshot snapshot)
            {
                snapshot = default;
                if (!TryGetSession(out var current) || !current.Equals(handle.Session)) return false;
                var actor = arena.FindReadActor(handle.ActorId);
                if (!actor) return false;
                snapshot = arena.CaptureActor(handle, actor);
                return true;
            }
            public void Dispose() { arena = null; }
        }
    }
    public partial class CombatPlayer
    {
        internal bool TryReadInputSession(out CombatInputSession session)
        {
            session = inputIngress != null ? inputIngress.Session : default;
            return inputIngress != null && session.ActorId == ActorId;
        }
    }
}
