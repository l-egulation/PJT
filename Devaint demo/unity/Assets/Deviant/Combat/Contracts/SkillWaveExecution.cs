namespace Deviant.Combat.Contracts
{
    public readonly struct SkillWaveSnapshot
    {
        public float X { get; }
        public float Y { get; }
        public float PreviousX { get; }
        public float PreviousY { get; }
        public float Travel { get; }
        public bool Dead { get; }
        public SkillWaveSnapshot(float x, float y, float previousX, float previousY, float travel, bool dead)
        { X = x; Y = y; PreviousX = previousX; PreviousY = previousY; Travel = travel; Dead = dead; }
    }

    // Cast activation, cancellation, motion, resources and time stay with the
    // cast. Native owns geometry, target callbacks and the actual renderer.
    public interface ISkillWaveExecutionHost
    {
        int Direction { get; }
        bool Blocked(float x, float y);
        bool Occluded(float x, float y);
        void CreateVisual(float x, float y);
        float Travel(float x, float y, int direction, float requested);
        void ApplyBox(float previousX, float previousY, float x, float y);
        void UpdateVisual(float x, float y);
        void DestroyVisual();
    }
}
