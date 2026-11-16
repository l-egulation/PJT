using System;
using System.Collections.Generic;

namespace Deviant.Combat.Contracts
{
    public readonly struct ActorOverlapBox
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public ActorOverlapBox(float x, float y, float width, float height)
        {
            if (!ActorBodyFrame.Finite(x) || !ActorBodyFrame.Finite(y) || !ActorBodyFrame.Finite(width) || !ActorBodyFrame.Finite(height) || width <= 0 || height <= 0) throw new ArgumentException("An overlap requires finite positive geometry.");
            X = x; Y = y; Width = width; Height = height;
        }
    }
    public readonly struct ActorSurfaceBounds
    {
        public float MinX { get; }
        public float MinY { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public float MaxZ { get; }
        public bool OneWay { get; }
        public ActorSurfaceBounds(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, bool oneWay)
        {
            if (!ActorBodyFrame.Finite(minX) || !ActorBodyFrame.Finite(minY) || !ActorBodyFrame.Finite(minZ) || !ActorBodyFrame.Finite(maxX) || !ActorBodyFrame.Finite(maxY) || !ActorBodyFrame.Finite(maxZ) || minX > maxX || minY > maxY || minZ > maxZ) throw new ArgumentException("A surface requires finite ordered bounds.");
            MinX = minX; MinY = minY; MinZ = minZ; MaxX = maxX; MaxY = maxY; MaxZ = maxZ; OneWay = oneWay;
        }
    }
    public readonly struct ActorRayCast
    {
        public float X { get; }
        public float Y { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float Distance { get; }
        public ActorRayCast(float x, float y, float directionX, float directionY, float distance)
        {
            if (!ActorBodyFrame.Finite(x) || !ActorBodyFrame.Finite(y) || !ActorBodyFrame.Finite(directionX) || !ActorBodyFrame.Finite(directionY) || directionX == 0 && directionY == 0 || !ActorBodyFrame.Finite(distance) || distance < 0) throw new ArgumentException("A ray requires finite origin, nonzero direction and nonnegative distance.");
            X = x; Y = y; DirectionX = directionX; DirectionY = directionY; Distance = distance;
        }
    }
    public readonly struct ActorRayContact
    {
        public float Y { get; }
        public float NormalY { get; }
        public bool Ignored { get; }
        public ActorRayContact(float y, float normalY, bool ignored)
        {
            if (!ActorBodyFrame.Finite(y) || !ActorBodyFrame.Finite(normalY)) throw new ArgumentException("A floor ray observation must remain finite.");
            Y = y; NormalY = normalY; Ignored = ignored;
        }
    }
    public interface IActorPlacementQuery : IActorSurfaceQuery
    {
        IReadOnlyList<ActorSurfaceBounds> OverlapBox(ActorOverlapBox request);
        IReadOnlyList<ActorRayContact> Raycast(ActorRayCast request);
    }
    public readonly struct ActorTeleportMotion
    {
        public float X { get; }
        public float Y { get; }
        public bool Applied { get; }
        public bool Skipped { get; }
        public ActorTeleportMotion(float x, float y, bool applied, bool skipped)
        {
            if (!ActorBodyFrame.Finite(x) || !ActorBodyFrame.Finite(y)) throw new ArgumentException("Teleport output must remain finite.");
            X = x; Y = y; Applied = applied; Skipped = skipped;
        }
    }
}
