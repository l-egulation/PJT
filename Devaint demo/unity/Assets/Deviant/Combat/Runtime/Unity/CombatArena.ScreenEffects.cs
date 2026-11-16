using System;
using System.Collections.Generic;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        readonly HashSet<ScreenEffectScope> screenEffectScopes = new HashSet<ScreenEffectScope>();
        public bool CombatScreenEffectsAllowed => screenEffectScopes.Count == 0;
        internal event Action CombatScreenEffectsSuppressed;

        // Encounter presentation owns the screen while this lease lives. Damage and world VFX keep their clocks.
        public IDisposable SuppressCombatScreenEffects()
        {
            var scope = new ScreenEffectScope(this);
            screenEffectScopes.Add(scope);
            if (screenEffectScopes.Count == 1)
            {
                CombatScreenEffectsSuppressed?.Invoke();
            }
            return scope;
        }

        sealed class ScreenEffectScope : IDisposable
        {
            CombatArena owner;
            public ScreenEffectScope(CombatArena owner) { this.owner = owner; }
            public void Dispose()
            {
                if (owner) owner.screenEffectScopes.Remove(this);
                owner = null;
            }
        }
    }
}
