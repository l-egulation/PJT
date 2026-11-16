using System;
using Deviant.Combat;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.World;

namespace Deviant.Game
{
    // The current composition registers this one map acceptance port. Candidate
    // proof is detached until Combat has reserved its idle reset boundary.
    sealed class ForestMovementValidation : ICombatMovementValidation
    {
        readonly CombatArena combat;
        readonly InkForestWorld forest;
        public ForestMovementValidation(CombatArena combat, InkForestWorld forest) { this.combat = combat; this.forest = forest; }
        public bool TryPrepare(LocomotionProfile candidate, out ICombatMovementValidationTransaction transaction, out string error)
        {
            transaction = null;
            if (!combat || !combat.Player || !forest || !forest.HasValidatedLayout)
            { error = "The active combat/validated forest owner is unavailable."; return false; }
            var expected = combat.Movement;
            var profile = UnityChapterModules.ProfileFor(combat.Player, candidate);
            if (!forest.TryPrepareLayoutProfile(profile, out var prepared, out error)) return false;
            transaction = new Transaction(combat, forest, expected, forest.Chapter, prepared); return true;
        }
        sealed class Transaction : ICombatMovementValidationTransaction
        {
            readonly CombatArena combat;
            readonly InkForestWorld forest;
            readonly LocomotionProfile expected;
            readonly ChapterSession chapter;
            ChapterSession.Revalidation prepared;
            public Transaction(CombatArena combat, InkForestWorld forest, LocomotionProfile expected, ChapterSession chapter, ChapterSession.Revalidation prepared)
            { this.combat = combat; this.forest = forest; this.expected = expected; this.chapter = chapter; this.prepared = prepared; }
            public bool TryCommit(out string error)
            {
                if (prepared == null || !combat || !combat.isActiveAndEnabled || !forest || !ReferenceEquals(combat.Movement, expected) ||
                    !ReferenceEquals(forest.Chapter, chapter) || !chapter.IsActive)
                { error = "The prepared combat/world movement owner is stale."; return false; }
                return prepared.TryCommit(out error);
            }
            public void Dispose() { prepared?.Dispose(); prepared = null; }
        }
    }
}
