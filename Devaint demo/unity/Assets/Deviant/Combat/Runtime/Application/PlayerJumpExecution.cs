using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // The adapter supplies the existing rule velocities and simulation time.
    // Ground contact and valid dash/attack cancellation remain native concerns.
    public sealed class PlayerJumpExecution
    {
        public int Used { get; private set; }
        public float BufferedUntil { get; private set; } = -1;
        // Space hold is reconstructed from ordered edges, so replays stay deterministic.
        public bool Held { get; private set; }
        public bool CutArmed { get; private set; }
        // How many jumps the player has learned: 0 none, 1 ground only, 2 with the air jump. Unlearned
        // presses neither jump nor buffer (the opening teaches them one at a time).
        public int Limit { get; set; } = 2;
        public PlayerJumpSnapshot Read() => new PlayerJumpSnapshot(Used, BufferedUntil);
        public bool Press(float now, LocomotionProfile movement, out float velocity)
        {
            velocity = 0;
            if (Limit <= 0) return false;
            if (Used < Limit) { velocity = Used++ == 0 ? movement.FirstJumpVelocity : movement.SecondJumpVelocity; BufferedUntil = -1; return true; }
            BufferedUntil = now + movement.JumpBufferDuration; return false;
        }
        public bool Land(float now, LocomotionProfile movement, out float velocity)
        {
            Used = 0; velocity = 0;
            if (BufferedUntil >= now) return Press(now, movement, out velocity);
            BufferedUntil = -1; return false;
        }
        public void ClearBuffer() { BufferedUntil = -1; }
        public void ArmCut(bool armed) => CutArmed = armed;
        // Returns true once when the armed rise should be shortened. A release and
        // press in one tick is a new hold only if the earlier press was still held.
        public bool ObserveHold(bool pressed, bool released)
        {
            if (pressed || released) Held = pressed && (!released || Held);
            if (Held || !CutArmed) return false;
            CutArmed = false; return true;
        }
        public void Reset() { Used = 0; BufferedUntil = -1; Held = CutArmed = false; }
    }
}
