using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Narrative.Contracts;
using Deviant.World;
using UnityEngine;

namespace Deviant.Integration
{
    public sealed class NpcPlacementBinding
    {
        public string PlacementId { get; }
        public string AnchorId { get; }
        public NpcDefinition Definition { get; }
        public NpcPlacementBinding(string placementId, string anchorId, NpcDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(placementId) || string.IsNullOrWhiteSpace(anchorId) || definition == null) throw new ArgumentException("NPC placement, semantic anchor and definition are required.");
            PlacementId = placementId; AnchorId = anchorId; Definition = definition;
        }
    }
    public sealed class ChapterNpcTargets : IInteractionTargets
    {
        readonly ChapterWorldHost host;
        readonly Guid worldSession;
        readonly Func<Vector2> actorPoint;
        readonly Func<bool> inCombat;
        NpcPlacementBinding[] bindings;
        public ChapterNpcTargets(ChapterWorldHost host, Guid worldSession, NpcPlacementBinding[] bindings, Func<Vector2> actorPoint, Func<bool> inCombat)
        {
            if (!host || worldSession == Guid.Empty) throw new ArgumentException("A chapter session is required.");
            this.host = host; this.worldSession = worldSession; this.actorPoint = actorPoint ?? throw new ArgumentNullException(nameof(actorPoint));
            this.inCombat = inCombat ?? throw new ArgumentNullException(nameof(inCombat)); ReplaceBindings(bindings);
        }
        public void ReplaceBindings(NpcPlacementBinding[] value)
        {
            if (value == null || value.Any(b => b == null) || value.Select(b => b.PlacementId).Distinct(StringComparer.Ordinal).Count() != value.Length)
                throw new ArgumentException("NPC placement bindings must be unique and non-null.", nameof(value));
            if (!host || host.Session == null || !host.Session.IsActive || host.Session.Id != worldSession) throw new InvalidOperationException("NPC bindings require their original active chapter session.");
            foreach (var binding in value)
                if (!host.Session.Layout.Placements.Any(p => p.Id == binding.PlacementId) || !host.TryGetWorldAnchor(worldSession, binding.AnchorId, out _, out _))
                    throw new ArgumentException("NPC binding references an unknown placement/anchor: " + binding.PlacementId, nameof(value));
            bindings = (NpcPlacementBinding[])value.Clone();
        }
        public IReadOnlyList<NpcTarget> ReadTargets()
        {
            var result = new List<NpcTarget>();
            foreach (var binding in bindings) if (TryRead(worldSession, binding.PlacementId, out var target)) result.Add(target);
            return result.AsReadOnly();
        }
        public bool TryRead(Guid expectedWorld, string placementId, out NpcTarget target)
        {
            target = null;
            if (expectedWorld != worldSession || !host || host.Session == null || host.Session.Id != worldSession || !host.Session.IsActive) return false;
            var binding = bindings.FirstOrDefault(b => b.PlacementId == placementId);
            var placement = host.Session.Layout.Placements.FirstOrDefault(p => p.Id == placementId);
            if (binding == null || placement == null || !host.Session.IsModuleLoaded(placement.ModuleId) || host.Session.State.ReadPresence(placementId) != PlacementPresence.Present ||
                !host.TryGetWorldAnchor(worldSession, binding.AnchorId, out var point, out _)) return false;
            target = new NpcTarget(worldSession, placementId, binding.Definition, Vector2.Distance(actorPoint(), point), inCombat()); return true;
        }
    }
}
