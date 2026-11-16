using Deviant.Presentation.Contracts;

namespace Deviant.Presentation
{
    // Embed privately and mutate the field directly; only Read copies facts.
    // Clip/Sprite binding, attack age and the caller's clocks remain outside.
    public struct CharacterLocomotionExecution
    {
        string previousState, previousForm;
        float elapsed, lastVelocity, airStart, landingUntil;
        // Inverted storage makes a default value start with previousGround=true.
        bool previousAirborne;
        public float Elapsed => elapsed;
        public CharacterLocomotionSnapshot Read() => new CharacterLocomotionSnapshot(previousState, previousForm, elapsed, lastVelocity, airStart, landingUntil, !previousAirborne);
        public void Reset()
        {
            previousState = previousForm = null;
            elapsed = lastVelocity = landingUntil = 0; previousAirborne = false;
            // The existing reset deliberately leaves airStart untouched.
        }
        public string Advance<T>(in CharacterLocomotionInput input, ref T durations) where T : struct, ICharacterLocomotionDurations
        {
            if (previousForm != input.Form) { previousState = null; previousForm = input.Form; }
            bool jumped = !input.Grounded && input.VelocityY > lastVelocity + 1;
            if (jumped || !previousAirborne && !input.Grounded) airStart = input.Now;
            if (previousAirborne && input.Grounded) landingUntil = input.Now + durations.LandingDuration;
            string state;
            if (input.Dead) state = "death";
            else if (input.Dashing || input.Approaching) state = "dash";
            else if (input.Attacking) state = AttackState(input.BasicAttack ? input.BasicIndex + 1 : 1);
            else if (!input.Grounded)
            {
                if (input.VelocityY > 0)
                    state = input.Now - airStart < durations.JumpStartDuration ? "jump_start" : input.VelocityY > 2 ? "jump_rise" : "jump_apex";
                else if (input.VelocityY >= -2) state = "jump_apex";
                else state = previousState == "fall_loop" || previousState == "fall_start" && elapsed >= durations.FallStartDuration ? "fall_loop" : "fall_start";
            }
            else state = input.Axis != 0 ? "run" : input.Now < landingUntil ? "land" : "idle";
            if (previousState != state || jumped) elapsed = 0; else elapsed += input.DeltaTime;
            previousState = state; previousAirborne = !input.Grounded; lastVelocity = input.VelocityY;
            return state;
        }
        static string AttackState(int number)
        {
            // Current basic sets use these four names; do not allocate them per Draw.
            switch (number)
            {
                case 1: return "attack1";
                case 2: return "attack2";
                case 3: return "attack3";
                case 4: return "attack4";
                default: return "attack" + number;
            }
        }
    }
}
