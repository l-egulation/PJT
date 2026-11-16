using System;

namespace Deviant.Input.Contracts
{
    [Flags]
    public enum GamepadFeedbackCapabilities { None = 0, Rumble = 1, AdaptiveTriggers = 2 }
    public enum TriggerFeedbackMode { Off, Resistance, Vibration }

    public readonly struct TriggerFeedback : IEquatable<TriggerFeedback>
    {
        public TriggerFeedbackMode Mode { get; }
        public int Start { get; }
        public int Strength { get; }
        public int Frequency { get; }
        public TriggerFeedback(TriggerFeedbackMode mode, int start = 0, int strength = 0, int frequency = 0)
        {
            if ((uint)mode > (uint)TriggerFeedbackMode.Vibration || start < 0 || start > 9 || strength < 0 || strength > 8 ||
                frequency < 0 || frequency > 255 || mode == TriggerFeedbackMode.Vibration && strength > 0 && frequency == 0)
                throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = strength == 0 ? TriggerFeedbackMode.Off : mode; Start = start; Strength = strength; Frequency = frequency;
        }
        public bool Equals(TriggerFeedback other) => Mode == other.Mode && Start == other.Start && Strength == other.Strength && Frequency == other.Frequency;
        public override bool Equals(object obj) => obj is TriggerFeedback other && Equals(other);
        public override int GetHashCode() => ((int)Mode * 397 ^ Start) * 397 ^ Strength * 31 ^ Frequency;
    }

    public readonly struct GamepadFeedbackState : IEquatable<GamepadFeedbackState>
    {
        public float Low { get; }
        public float High { get; }
        public TriggerFeedback Left { get; }
        public TriggerFeedback Right { get; }
        public GamepadFeedbackState(float low, float high, TriggerFeedback left = default, TriggerFeedback right = default)
        {
            if (float.IsNaN(low) || float.IsInfinity(low) || low < 0 || low > 1 || float.IsNaN(high) || float.IsInfinity(high) || high < 0 || high > 1)
                throw new ArgumentOutOfRangeException(nameof(low));
            Low = low; High = high; Left = left; Right = right;
        }
        public bool Equals(GamepadFeedbackState other) => Low == other.Low && High == other.High && Left.Equals(other.Left) && Right.Equals(other.Right);
        public override bool Equals(object obj) => obj is GamepadFeedbackState other && Equals(other);
        public override int GetHashCode() => Low.GetHashCode() ^ High.GetHashCode() ^ Left.GetHashCode() ^ Right.GetHashCode();
    }
}
