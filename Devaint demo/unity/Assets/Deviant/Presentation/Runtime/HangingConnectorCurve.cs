using System;
using Deviant.Definitions;

namespace Deviant.Presentation
{
    public readonly struct ConnectorPoint
    {
        public readonly float X, Y;
        public ConnectorPoint(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) throw new ArgumentException("Finite connector endpoints are required.");
            X = x; Y = y;
        }
    }
    // Elastic-looking bow between fixed endpoints. This is a view state, not a
    // second simulation of the lantern: it cannot move the lamp or change impacts.
    public sealed class HangingConnectorCurve
    {
        readonly HangingConnectorDefinition definition;
        double bend;
        public float Bend => (float)bend;
        public HangingConnectorCurve(HangingConnectorDefinition definition) { this.definition = definition ?? throw new ArgumentNullException(nameof(definition)); }
        public void Advance(float delta, float angularSpeed)
        {
            LightingValidation.Nonnegative(delta, nameof(delta));
            if (float.IsNaN(angularSpeed) || float.IsInfinity(angularSpeed)) throw new ArgumentOutOfRangeException(nameof(angularSpeed));
            if (delta == 0) return;
            double target = Math.Max(-definition.MaxBend, Math.Min(definition.MaxBend, -(double)angularSpeed * definition.LagScale));
            bend = target + (bend - target) * Math.Exp(-(double)definition.Response * delta);
        }
        public ConnectorPoint Sample(ConnectorPoint start, ConnectorPoint end, float amount)
        {
            LightingValidation.Unit(amount, nameof(amount));
            if (amount == 0) return start;
            if (amount == 1) return end;
            double x = (double)end.X - start.X, y = (double)end.Y - start.Y, length = Math.Sqrt(x * x + y * y);
            if (length < 1e-7) return start;
            double bow = Math.Max(-length * .2, Math.Min(length * .2, bend)) * (4 * amount * (1 - amount));
            return new ConnectorPoint((float)(start.X + x * amount - y / length * bow), (float)(start.Y + y * amount + x / length * bow));
        }
        public void Reset() { bend = 0; }
    }
}
