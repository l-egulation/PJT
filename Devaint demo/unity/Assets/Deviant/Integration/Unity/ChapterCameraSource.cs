using System;
using Deviant.Presentation.Contracts;
using UnityEngine;

namespace Deviant.Integration
{
    // Re-evaluate the semantic anchor in its original chapter session. Reusing a
    // view or opening a new chapter cannot redirect an old framing request.
    public sealed class ChapterCameraSource : ICameraPositionSource
    {
        readonly ChapterWorldHost host;
        readonly Guid session;
        readonly string anchorId;
        readonly Vector3 offset;
        public ChapterCameraSource(ChapterWorldHost host, Guid session, string anchorId, Vector3 offset)
        {
            if (!host || session == Guid.Empty || string.IsNullOrWhiteSpace(anchorId)) throw new ArgumentException("A chapter host, session and anchor are required.");
            // Reuse the public finite-position validation for the binding offset.
            new CameraPosition(offset.x, offset.y, offset.z);
            this.host = host; this.session = session; this.anchorId = anchorId; this.offset = offset;
        }
        public CameraPosition Read()
        {
            if (!host || !host.TryGetWorldAnchor(session, anchorId, out var point, out _)) throw new InvalidOperationException("Chapter camera anchor is unavailable or belongs to an ended session.");
            return new CameraPosition(point.x + offset.x, point.y + offset.y, offset.z);
        }
    }
}
