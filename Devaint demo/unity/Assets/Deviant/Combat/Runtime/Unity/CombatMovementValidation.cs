using System;
using Deviant.Definitions;

namespace Deviant.Combat
{
    // Composition supplies map-dependent acceptance. Combat owns only the one
    // explicit binding and the reset boundary; it does not know world layouts.
    public interface ICombatMovementValidation
    {
        bool TryPrepare(LocomotionProfile candidate, out ICombatMovementValidationTransaction transaction, out string error);
    }
    public interface ICombatMovementValidationTransaction : IDisposable
    {
        // Recheck the prepared owner's exact session/geometry/profile before
        // committing. Failure leaves both combat and map proof untouched.
        bool TryCommit(out string error);
    }
    public partial class CombatArena
    {
        MovementValidationBinding movementValidation;
        public IDisposable BindMovementValidation(ICombatMovementValidation validation)
        {
            if (validation == null) throw new ArgumentNullException(nameof(validation));
            if (movementValidation != null) throw new InvalidOperationException("This runtime already has an explicit movement validation owner.");
            return movementValidation = new MovementValidationBinding(this, validation);
        }
        sealed class MovementValidationBinding : IDisposable
        {
            CombatArena arena;
            public readonly ICombatMovementValidation Validation;
            public MovementValidationBinding(CombatArena arena, ICombatMovementValidation validation) { this.arena = arena; Validation = validation; }
            public void Dispose()
            {
                if (arena && ReferenceEquals(arena.movementValidation, this)) arena.movementValidation = null;
                arena = null;
            }
        }
    }
}
