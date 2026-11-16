using System;
using Deviant.World.Contracts;
using UnityEngine;

namespace Deviant.World
{
    // Follow/subscription lifetime for the repeating prototype. The validated
    // terrain ChapterSession belongs to the world, not this gameplay generation.
    public sealed class PrototypeForestSession : IDisposable
    {
        readonly InkForestWorld world;
        readonly Action<InkForestChunk> prepare;
        readonly Transform previousTarget;
        Transform target;
        bool disposed;
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsActive => !disposed && world && world.IsSessionOwner(this);
        public bool HasValidatedChapterLayout => IsActive && world.HasValidatedLayout;
        public string ChapterLayoutLimitation => "This is the existing repeating terrain's technical movement proof, not a finite chapter, eight combat maps or required product routes.";
        public ChapterSession Chapter => IsActive ? world.Chapter : null;
        public StreamingWindow CurrentWindow => world ? world.CurrentWindow : null;
        public PrototypeForestSession(InkForestWorld world, Action<InkForestChunk> prepare)
        {
            this.world = world ? world : throw new ArgumentNullException(nameof(world));
            this.prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
            previousTarget = world.followTarget; world.AcquireSession(this);
            world.ChunkPopulated += prepare;
            try { world.VisitChunks(prepare); }
            catch { Dispose(); throw; }
        }
        public bool TryBindProfile(Guid expectedSession, LayoutBodyProfile profile, LayoutPoint existingEntry, out string error)
        {
            if (!IsActive || expectedSession != Id) { error = "Prototype forest follow lease is stale."; return false; }
            return world.TryBindLayoutProfile(profile, existingEntry, out error);
        }
        public bool TryFollow(Guid expectedSession, Transform candidate, out string error)
        {
            error = null;
            if (!IsActive || expectedSession != Id || !candidate) { error = "A current prototype forest session and explicit follow target are required."; return false; }
            if (!world.HasValidatedLayout) { error = "Bind the actual common movement profile before following the forest."; return false; }
            target = candidate; world.followTarget = candidate; return true;
        }
        public bool TryEnsure(Guid expectedSession, float x, out string error)
        {
            error = null;
            if (!IsActive || expectedSession != Id) { error = "Prototype forest session has ended or is stale."; return false; }
            try { world.EnsureChunks(x); return true; }
            catch (ArgumentOutOfRangeException exception) { error = exception.Message; return false; }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (!world) return;
            world.ChunkPopulated -= prepare;
            if (ReferenceEquals(world.followTarget, target) || !target && ReferenceEquals(world.followTarget, previousTarget)) world.followTarget = previousTarget;
            world.ReleaseSession(this);
        }
    }
}
