using System;
using System.Collections.Generic;
using Deviant.Definitions;

namespace Deviant.World
{
    public readonly struct PropImpactKey : IEquatable<PropImpactKey>
    {
        public readonly Guid Execution;
        public readonly int HitIndex;
        public PropImpactKey(Guid execution, int hitIndex) { Execution = execution; HitIndex = hitIndex; }
        public bool Equals(PropImpactKey other) => Execution == other.Execution && HitIndex == other.HitIndex;
        public override bool Equals(object other) => other is PropImpactKey key && Equals(key);
        public override int GetHashCode() => Execution.GetHashCode() * 397 ^ HitIndex;
    }
    // The World owns this state. No colliders, combat HP, renderer or independent clock.
    public sealed class ReactivePendulum
    {
        readonly PendulumDefinition definition;
        readonly HashSet<PropImpactKey> impacts = new HashSet<PropImpactKey>();
        double angle, speed;
        public float Angle => (float)angle;
        public float AngularSpeed => (float)speed;
        public float OffsetX => definition.Length * (float)Math.Sin(angle);
        public float OffsetY => -definition.Length * (float)Math.Cos(angle);
        public int AcceptedAttacks { get; private set; }
        public ReactivePendulum(PendulumDefinition definition) { this.definition = definition ?? throw new ArgumentNullException(nameof(definition)); }
        public void Impulse(float horizontalSpeed)
        {
            if (float.IsNaN(horizontalSpeed) || float.IsInfinity(horizontalSpeed)) throw new ArgumentOutOfRangeException(nameof(horizontalSpeed));
            speed = Clamp(speed + horizontalSpeed * Math.Cos(angle) / definition.Length, definition.MaxAngularSpeed);
        }
        public bool Attack(PropImpactKey key, int direction)
        {
            if (key.Execution == Guid.Empty || direction != -1 && direction != 1) throw new ArgumentException("An actual execution and direction are required.");
            if (!impacts.Add(key)) return false;
            AcceptedAttacks++; Impulse(direction * definition.AttackImpulse); return true;
        }
        // The integration host releases dedup entries only after their actual melee cast ends.
        public void ForgetExecution(Guid execution) => impacts.RemoveWhere(key => key.Execution == execution);
        public void Advance(float delta)
        {
            LightingValidation.Nonnegative(delta, nameof(delta));
            if (delta > 30) throw new ArgumentOutOfRangeException(nameof(delta), "Split long simulation intervals explicitly.");
            if (delta == 0) return;
            int steps = Math.Max(1, (int)Math.Ceiling(delta * 120)); double dt = (double)delta / steps;
            double drag = Math.Exp(-definition.Damping * dt / 2);
            for (int i = 0; i < steps; i++)
            {
                speed = speed * drag - (double)definition.Gravity / definition.Length * Math.Sin(angle) * dt / 2;
                angle += speed * dt;
                if (Math.Abs(angle) > definition.MaxAngle) { angle = Clamp(angle, definition.MaxAngle); if (angle * speed > 0) speed = 0; }
                speed = Clamp((speed - (double)definition.Gravity / definition.Length * Math.Sin(angle) * dt / 2) * drag, definition.MaxAngularSpeed);
            }
        }
        public void Reset() { angle = speed = 0; impacts.Clear(); AcceptedAttacks = 0; }
        static double Clamp(double value, double limit) => Math.Max(-limit, Math.Min(limit, value));
    }
}
