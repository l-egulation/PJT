using System;

namespace Deviant.Presentation.Contracts
{
    public readonly struct CameraPosition
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public CameraPosition(float x, float y, float z)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z)) throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y; Z = z;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    public interface ICameraPositionSource
    {
        // Read on the presentation thread. This source has no camera/actor/time
        // mutation authority; a missing anchor should throw an explicit failure.
        CameraPosition Read();
    }
    public sealed class FixedCameraPosition : ICameraPositionSource
    {
        readonly CameraPosition position;
        public FixedCameraPosition(CameraPosition position) { this.position = position; }
        public CameraPosition Read() => position;
    }
    public enum CameraRequestStatus { Active, Completed, Cancelled, Failed }
    public interface ICameraHandle : IDisposable
    {
        Guid SessionId { get; }
        long Sequence { get; }
        int Priority { get; }
        CameraRequestStatus Status { get; }
        string Error { get; }
        void Complete();
        void Cancel();
    }
    public interface ICameraScope : IDisposable
    {
        bool IsActive { get; }
        ICameraHandle Request(int priority, ICameraPositionSource source);
    }
    public interface ICameraRequests
    {
        Guid SessionId { get; }
        int ActiveCount { get; }
        ICameraScope OpenScope(string owner);
    }
    public readonly struct CameraSelection
    {
        public CameraPosition Position { get; }
        public Guid SessionId { get; }
        public long? RequestSequence { get; }
        public CameraSelection(CameraPosition position, Guid session, long? sequence)
        { Position = position; SessionId = session; RequestSequence = sequence; }
    }
}
