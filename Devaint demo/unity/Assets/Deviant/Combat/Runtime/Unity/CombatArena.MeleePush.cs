using System;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        // Capture the budget before damage callbacks. Later switching/movement cannot
        // move this hit's stopping point or turn an out-of-range hit into a pull.
        internal float MeleePushDistanceLimit(CombatActor source, CombatActor target, CombatHitOrigin origin, int direction)
        {
            float overlap = EnemyHitReaction.BasicPushOverlap;
            if (overlap <= 0 || source != Player || !target.hurtCollider || target.isPlayer || target.isBoss || target.isDecoy ||
                (direction != 1 && direction != -1) ||
                (origin.Kind != CombatHitKind.Melee && origin.Kind != CombatHitKind.Independent)) return float.PositiveInfinity;
            string form = origin.Stamp.FormId;
            float reach = BasicMeleeFront(form);
            if (float.IsPositiveInfinity(reach)) return reach;
            reach *= PlayerReachScale;
            // Use this attack's captured range/area modifiers, without starting an
            // action or consuming a pending buff on the other weapon.
            if (origin.Stamp.InkPalette != null && inkStarts.TryGetValue(origin.Stamp.InkPalette, out var action))
                reach *= action.RangeScale * action.MvpRadiusScale * action.MvpAllGeometryScale;
            for (int slot = 0; slot < 2; slot++)
            {
                if (!Player.IsWeaponSlotReady(slot)) continue;
                var weapon = Player.weapons[slot];
                if (weapon.InstanceId == origin.Stamp.WeaponId) continue;
                int index = weapon.FormIndex;
                if (index < 0 || index >= catalog.forms.Length) continue;
                // A ranged/empty slot has no basic melee definition and contributes
                // no boundary. The other melee's numeric base reach is conservative.
                reach = Mathf.Min(reach, BasicMeleeFront(catalog.forms[index].id));
            }
            var bounds = target.hurtCollider.bounds;
            float near = direction > 0 ? bounds.min.x : -bounds.max.x;
            float edge = source.Position.x * direction + reach;
            return Mathf.Max(0, edge - Mathf.Min(overlap, bounds.size.x) - near);
        }

        float BasicMeleeFront(string form)
        {
            if (string.IsNullOrEmpty(form)) return float.PositiveInfinity;
            int index = Array.FindIndex(catalog.forms, entry => entry.id == form);
            if (index < 0) return float.PositiveInfinity;
            float reach = float.PositiveInfinity;
            for (int hit = 0; hit < catalog.forms[index].basics.Length; hit++)
            {
                var definition = GetBasicMelee(form, hit);
                if (definition == null) return float.PositiveInfinity;
                reach = Mathf.Min(reach, definition.OffsetX + definition.Width * .5f);
            }
            return reach;
        }
    }
}
