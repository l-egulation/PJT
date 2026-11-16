using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Presentation.Unity;
using Deviant.World;
using UnityEngine;

namespace Deviant.Integration
{
    public readonly struct LanternImpact
    {
        public readonly int ActorId;
        public readonly Vector2 Point, Normal;
        public LanternImpact(int actorId, Vector2 point, Vector2 normal)
        { ActorId = actorId; Point = point; Normal = normal; }
    }
    // Explicitly attached by a composition root, never searches for another arena.
    // No collider or damage receiver is created for this non-blocking prop.
    [DefaultExecutionOrder(900)]
    public sealed class ReactiveLanternBridge : MonoBehaviour
    {
        CombatArena arena;
        PendulumDefinition definition;
        readonly HashSet<int> touching = new HashSet<int>();
        readonly HashSet<Guid> executions = new HashSet<Guid>();
        readonly HashSet<Guid> live = new HashSet<Guid>();
        readonly List<Guid> ended = new List<Guid>();
        IDisposable observation;
        Vector2 contactSize;
        Transform swing;
        LightEmitterView emitter;
        HangingConnectorView connector;
        public ReactivePendulum State { get; private set; }
        public int BodyContacts { get; private set; }
        public bool SampleOverflow { get; private set; }
        public event Action<LanternImpact> BodyImpacted;
        public event Action<LanternImpact> MeleeImpacted;
        public void Bind(CombatArena owner, PendulumDefinition profile, Transform swingingRoot, Vector2 size, LightEmitterView light, HangingConnectorView connection = null)
        {
            if (arena) throw new InvalidOperationException("The lantern already has an arena.");
            if (!owner || !swingingRoot || profile == null || size.x <= 0 || size.y <= 0) throw new ArgumentException("A complete lantern binding is required.");
            LightingValidation.Positive(size.x, nameof(size)); LightingValidation.Positive(size.y, nameof(size));
            arena = owner; definition = profile; swing = swingingRoot; contactSize = size; emitter = light;
            connector = connection;
            State = new ReactivePendulum(profile);
            observation = arena.ObserveEnvironmentContacts();
            arena.SimulationCompleted += Step; arena.ResetCompleted += ResetState; arena.Deactivated += ResetState;
            ResetState();
        }
        public Vector2 ContactCenter => (Vector2)transform.position + new Vector2(State.OffsetX, State.OffsetY);
        void ResetState()
        {
            State?.Reset(); touching.Clear(); executions.Clear(); BodyContacts = 0; SampleOverflow = false;
            Apply();
            connector?.ResetVisual();
        }
        void Step(CombatCompletedStep step)
        {
            if (!isActiveAndEnabled || !arena || State == null) return;
            var bounds = new Rect(ContactCenter - contactSize / 2, contactSize);
            foreach (var sample in arena.CompletedEnvironmentBodies) Contact(sample, bounds);
            SampleOverflow |= arena.EnvironmentSampleOverflow;
            foreach (var sample in arena.CompletedEnvironmentAttacks)
            {
                if (sample.Origin.Kind != CombatHitKind.Melee || !sample.Origin.IsSpecified) continue;
                var box = new Rect(sample.X - sample.Width / 2, sample.Y - sample.Height / 2, sample.Width, sample.Height);
                if (!box.Overlaps(bounds)) continue;
                // Same WorldSolid/one-way distinction as combat. Check an exposed point
                // inside the actual overlap rather than raycasting to a distant sprite pivot.
                var point = new Vector2(Mathf.Clamp(sample.OriginX, Mathf.Max(box.xMin, bounds.xMin), Mathf.Min(box.xMax, bounds.xMax)),
                    Mathf.Clamp(sample.OriginY, Mathf.Max(box.yMin, bounds.yMin), Mathf.Min(box.yMax, bounds.yMax)));
                if (arena.Occluded(new Vector2(sample.OriginX, sample.OriginY), point)) continue;
                if (State.Attack(new PropImpactKey(sample.Origin.ExecutionId, sample.Origin.HitIndex), sample.Direction))
                {
                    var contact = new Vector2(sample.Direction > 0 ? bounds.xMin : bounds.xMax, point.y);
                    MeleeImpacted?.Invoke(new LanternImpact(sample.Origin.EffectiveSource.ActorId, contact, new Vector2(-sample.Direction, 0)));
                }
                executions.Add(sample.Origin.ExecutionId);
            }
            live.Clear(); foreach (var cast in arena.casts) if (cast) live.Add(cast.ExecutionId);
            ended.Clear(); foreach (var id in executions) if (!live.Contains(id)) ended.Add(id);
            foreach (var id in ended) { State.ForgetExecution(id); executions.Remove(id); }
            State.Advance(step.Delta); Apply(); connector?.Advance(step.Delta, State.AngularSpeed); emitter?.Sample(step.Now);
        }
        void Contact(EnvironmentBodySweep sample, Rect prop)
        {
            Vector2 from = new Vector2(sample.FromX, sample.FromY), to = new Vector2(sample.ToX, sample.ToY);
            var size = new Vector2(sample.Width, sample.Height);
            var expanded = new Rect(prop.position - size / 2, prop.size + size);
            bool endInside = expanded.Contains(to);
            if (!touching.Contains(sample.ActorId) && !expanded.Contains(from) && sample.Delta > 0 && TrySweptPoint(from, to, expanded, out float entry) && !arena.Occluded(from, to))
            {
                float speed = (to.x - from.x) / sample.Delta;
                if (Mathf.Abs(speed) > .001f)
                {
                    State.Impulse(speed * definition.BodyImpulseScale); BodyContacts++;
                    var at = Vector2.Lerp(from, to, entry);
                    var point = new Vector2(Mathf.Clamp(at.x, prop.xMin, prop.xMax), Mathf.Clamp(at.y, prop.yMin, prop.yMax));
                    BodyImpacted?.Invoke(new LanternImpact(sample.ActorId, point, new Vector2(-Mathf.Sign(speed), 0)));
                }
            }
            if (endInside) touching.Add(sample.ActorId); else touching.Remove(sample.ActorId);
        }
        public static bool SweptPoint(Vector2 from, Vector2 to, Rect box)
        { return TrySweptPoint(from, to, box, out _); }
        static bool TrySweptPoint(Vector2 from, Vector2 to, Rect box, out float enter)
        {
            enter = 0; float exit = 1;
            return Axis(from.x, to.x - from.x, box.xMin, box.xMax, ref enter, ref exit) &&
                Axis(from.y, to.y - from.y, box.yMin, box.yMax, ref enter, ref exit);
        }
        static bool Axis(float start, float delta, float low, float high, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < 1e-7f) return start >= low && start <= high;
            float a = (low - start) / delta, b = (high - start) / delta;
            enter = Mathf.Max(enter, Mathf.Min(a, b)); exit = Mathf.Min(exit, Mathf.Max(a, b)); return enter <= exit;
        }
        void Apply() { if (swing && State != null) swing.localRotation = Quaternion.Euler(0, 0, State.Angle * Mathf.Rad2Deg); }
        void OnDisable() { observation?.Dispose(); observation = null; if (State != null) ResetState(); }
        void OnEnable() { if (State != null) { observation = arena.ObserveEnvironmentContacts(); ResetState(); } }
        void OnDestroy()
        {
            observation?.Dispose(); observation = null;
            if (!arena) return;
            arena.SimulationCompleted -= Step; arena.ResetCompleted -= ResetState; arena.Deactivated -= ResetState;
        }
    }
}
