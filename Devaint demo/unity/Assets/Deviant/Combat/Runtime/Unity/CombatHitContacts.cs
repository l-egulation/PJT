using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public static class CombatHitContacts
    {
        public static CombatHitContact At(Vector2 point, Vector2 incoming, HitAnchorKind kind)
            => new CombatHitContact(point.x, point.y, incoming.x, incoming.y, kind);
        public static CombatHitContact Radial(Vector2 center, Vector2 contact)
            => At(contact, contact - center, HitAnchorKind.RadialRepresentative);
    }
}
