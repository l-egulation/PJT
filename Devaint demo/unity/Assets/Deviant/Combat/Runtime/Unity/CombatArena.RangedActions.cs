using System;
using System.Collections.Generic;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        RangedActionCatalog rangedActions;
        public IReadOnlyList<RangedActionDefinition> RangedActions => rangedActions?.Actions;
        public RangedActionDefinition GetRangedAction(string id) => rangedActions?.Read(id);
        public RangedActionDefinition GetRangedBasic(string form, int index) => rangedActions?.Basic(form, index);
        bool InitializeRangedActions()
        {
            if (!RangedActionCatalog.TryCreate(catalog, out var candidate, out var error)) { Debug.LogError("Invalid ranged action catalog: " + error); return false; }
            rangedActions = candidate; return true;
        }
        // Publication never rewrites equipment recovery debt or a running cast.
        // Every source group and current closed policy validates before mutation.
        public bool TryApplyRangedActions(IReadOnlyList<RangedActionDefinition> candidate, bool resetScenario, out string error)
        {
            if (!Player || rangedActions == null) { error = "Combat runtime is not initialized."; return false; }
            if (!rangedActions.TryReplace(candidate, out var next, out error)) return false;
            rangedActions = next;
            if (resetScenario) ResetTest();
            return true;
        }
    }
}
