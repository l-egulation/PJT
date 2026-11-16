namespace Deviant.Presentation.Contracts
{
    public readonly struct CharacterLocomotionInput
    {
        public string Form { get; }
        public float Now { get; }
        public float DeltaTime { get; }
        public bool Dead { get; }
        public bool Dashing { get; }
        public bool Approaching { get; }
        public bool Attacking { get; }
        public bool BasicAttack { get; }
        public int BasicIndex { get; }
        public bool Grounded { get; }
        public float VelocityY { get; }
        public int Axis { get; }
        public CharacterLocomotionInput(string form, float now, float deltaTime, bool dead, bool dashing, bool approaching,
            bool attacking, bool basicAttack, int basicIndex, bool grounded, float velocityY, int axis)
        {
            Form = form; Now = now; DeltaTime = deltaTime; Dead = dead; Dashing = dashing; Approaching = approaching;
            Attacking = attacking; BasicAttack = basicAttack; BasicIndex = basicIndex; Grounded = grounded; VelocityY = velocityY; Axis = axis;
        }
    }

    // Durations are read only on the same transitions that need them. The
    // adapter retains live clip lookup and the attack's captured landing value.
    public interface ICharacterLocomotionDurations
    {
        float LandingDuration { get; }
        float JumpStartDuration { get; }
        float FallStartDuration { get; }
    }

    public readonly struct CharacterLocomotionSnapshot
    {
        public string State { get; }
        public string Form { get; }
        public float Elapsed { get; }
        public float LastVelocity { get; }
        public float AirStart { get; }
        public float LandingUntil { get; }
        public bool PreviousGround { get; }
        public CharacterLocomotionSnapshot(string state, string form, float elapsed, float lastVelocity, float airStart, float landingUntil, bool previousGround)
        {
            State = state; Form = form; Elapsed = elapsed; LastVelocity = lastVelocity; AirStart = airStart; LandingUntil = landingUntil; PreviousGround = previousGround;
        }
    }
}
