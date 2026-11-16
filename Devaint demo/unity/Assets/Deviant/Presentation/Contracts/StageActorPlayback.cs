using System;
using Deviant.Definitions;

namespace Deviant.Presentation.Contracts
{
    public readonly struct StagePoint
    {
        public float X { get; }
        public float Y { get; }
        public StagePoint(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y;
        }
    }
    public readonly struct StagePose
    {
        public StagePoint Position { get; }
        public float Depth { get; }
        public int Facing { get; }
        public bool Visible { get; }
        public StagePose(StagePoint position, float depth, int facing, bool visible)
        {
            if (float.IsNaN(depth) || float.IsInfinity(depth) || facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(depth));
            Position = position; Depth = depth; Facing = facing; Visible = visible;
        }
    }
    public enum StageMoveStatus { Playing, Completed, Cancelled, Failed }
    public interface IStageActorHandle : IDisposable
    {
        Guid SessionId { get; }
        string DefinitionVersion { get; }
        StageMoveStatus Status { get; }
        string Error { get; }
        StagePose Pose { get; }
        bool IsRetained { get; }
        void Poll(double elapsed);
        void Cancel();
    }
    public interface IStageActorScope : IDisposable
    {
        bool IsActive { get; }
        // Completed pose belongs to the scope until replacement or scope end.
        IStageActorHandle Play(StageMoveDefinition definition, StagePoint from, StagePoint to);
    }
    public interface IStageActorRequests
    {
        Guid SessionId { get; }
        bool IsAvailable { get; }
        bool IsOwned { get; }
        IStageActorScope OpenScope(string owner);
    }
}
