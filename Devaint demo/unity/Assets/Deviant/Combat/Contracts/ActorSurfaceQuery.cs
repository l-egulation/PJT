using System;
using System.Collections.Generic;

namespace Deviant.Combat.Contracts
{
    public readonly struct ActorBodyFrame
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public int Facing { get; }
        public float CenterX => X + OffsetX * Facing;
        public float CenterY => Y + OffsetY;
        public ActorBodyFrame(float x, float y, float width, float height, float offsetX, float offsetY, int facing)
        {
            if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width <= 0 || height <= 0 || !Finite(offsetX) || !Finite(offsetY) || facing != -1 && facing != 1)
                throw new ArgumentException("An actor body requires finite coordinates, positive size and a signed facing.");
            X = x; Y = y; Width = width; Height = height; OffsetX = offsetX; OffsetY = offsetY; Facing = facing;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    public readonly struct ActorBoxCast
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float Distance { get; }
        public ActorBoxCast(float x, float y, float width, float height, float directionX, float directionY, float distance)
        {
            if (!ActorBodyFrame.Finite(x) || !ActorBodyFrame.Finite(y) || !ActorBodyFrame.Finite(width) || width <= 0 || !ActorBodyFrame.Finite(height) || height <= 0 ||
                !ActorBodyFrame.Finite(directionX) || !ActorBodyFrame.Finite(directionY) || !ActorBodyFrame.Finite(distance) || distance < 0 ||
                directionX == 0 && directionY == 0)
                throw new ArgumentException("A body cast requires finite positive geometry, a nonzero direction and nonnegative distance.");
            X = x; Y = y; Width = width; Height = height; DirectionX = directionX; DirectionY = directionY; Distance = distance;
        }
    }
    public readonly struct ActorSurfaceContact
    {
        public float Distance { get; }
        public float NormalX { get; }
        public float NormalY { get; }
        public float Top { get; }
        public bool OneWay { get; }
        public bool Ignored { get; }
        public ActorSurfaceContact(float distance, float normalX, float normalY, float top, bool oneWay, bool ignored)
        {
            if (!ActorBodyFrame.Finite(distance) || !ActorBodyFrame.Finite(normalX) || !ActorBodyFrame.Finite(normalY) || !ActorBodyFrame.Finite(top)) throw new ArgumentException("Surface contacts require finite values.");
            Distance = distance; NormalX = normalX; NormalY = normalY; Top = top; OneWay = oneWay; Ignored = ignored;
        }
    }
    public interface IActorSurfaceQuery
    {
        // Current ArenaSurface contacts in native query order. No scene object
        // escapes this boundary; ignored is the actor's current binding fact.
        IReadOnlyList<ActorSurfaceContact> BoxCast(ActorBoxCast request);
    }
    public readonly struct ActorAxisMotion
    {
        public float DeltaX { get; }
        public float DeltaY { get; }
        public bool Blocked { get; }
        public bool Skipped { get; }
        public ActorAxisMotion(float deltaX, float deltaY, bool blocked, bool skipped)
        {
            if (!ActorBodyFrame.Finite(deltaX) || !ActorBodyFrame.Finite(deltaY)) throw new ArgumentException("Axis motion must remain finite.");
            DeltaX = deltaX; DeltaY = deltaY; Blocked = blocked; Skipped = skipped;
        }
    }
}
