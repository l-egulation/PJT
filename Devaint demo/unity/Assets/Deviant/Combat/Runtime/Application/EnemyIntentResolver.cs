using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Decision only. The caller resolves the target, facing correction, ledge
    // query, common motor and attack execution in their existing order.
    public static class EnemyIntentResolver
    {
        public static EnemyIntent Resolve(EnemyObservation observation)
        {
            if (!observation.Aggressive || !observation.HasTarget || observation.Controlled || observation.Attacking) return default;
            float distance = Math.Abs(observation.DeltaX);
            int facing = observation.DeltaX < 0 ? -1 : 1;
            if (observation.Now >= observation.NextAttack && distance <= (observation.Profile == 1 ? 8 : 2.2f) && Math.Abs(observation.DeltaY) < 2)
                return new EnemyIntent(EnemyIntentKind.BeginAttack, facing, 0);
            if (distance < 10 && distance > (observation.Profile == 1 ? 5 : 1.5f))
                return new EnemyIntent(EnemyIntentKind.Chase, facing, observation.Profile == 2 ? 1.5f : 2.5f);
            return default;
        }
    }
}
