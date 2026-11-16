using System;
using System.Collections.Generic;
using Deviant.World;
using Deviant.World.Contracts;
using UnityEngine;

namespace Deviant.Integration
{
    // Explicit composition surface, usable by the game or an isolated experiment.
    // It does not choose chapter transitions, generate content or move the player.
    public sealed class ChapterWorldHost : MonoBehaviour
    {
        UnityChapterModules binding;
        public ChapterSession Session { get; private set; }
        public int LiveViewCount => binding?.LiveViewCount ?? 0;
        public bool TryOpen(IChapterLayoutProvider provider, LayoutRequest request, LayoutBodyProfile profile,
            IDictionary<string, GameObject> modules, IDictionary<string, GameObject> markers, out string error)
        {
            error = null;
            if (Session != null) { error = "Close the current chapter explicitly before opening another."; return false; }
            UnityChapterModules candidate = null;
            try
            {
                candidate = new UnityChapterModules(transform, modules, markers);
                if (!WorldAssembler.TryOpen(provider, request, profile, candidate, candidate, out var session, out error))
                { candidate.Dispose(); return false; }
                binding = candidate; Session = session; return true;
            }
            catch (Exception exception) { candidate?.Dispose(); error = exception.Message; return false; }
        }
        public bool TryGetWorldAnchor(Guid expectedSession, string anchorId, out Vector2 position, out LayoutAnchorKind kind)
        {
            position = default; kind = default;
            if (Session == null || !Session.TryGetAnchor(expectedSession, anchorId, out var local, out kind)) return false;
            position = transform.TransformPoint(new Vector3(local.X, local.Y, 0)); return true;
        }
        public bool TryRevalidate(LayoutBodyProfile profile, out string error)
        {
            if (Session == null) { error = "No chapter session is open."; return false; }
            return Session.TryRevalidate(profile, binding, out error);
        }
        public void Close()
        {
            Session?.Dispose(); Session = null;
            binding?.Dispose(); binding = null;
        }
        void OnDestroy() { Close(); }
    }
}
