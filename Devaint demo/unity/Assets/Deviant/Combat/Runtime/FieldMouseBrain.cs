using System;
using UnityEngine;

namespace Deviant.Combat
{
    public enum FieldMouseState { Idle, Chase, LastKnown, SearchWait, Return, Prepare, Active, Recovery, Dead }

    [Serializable]
    public sealed class FieldMouseDefinition
    {
        public string id, sourceSha256;
        public bool hitStaggerImmune, immovable, noPlayerHitReaction;
        public float maxHP, damage, walkSpeed, senseX, senseY, sensePeriod, pathPeriod;
        public float leash, reengageTargetLimit, searchWait, stagger, epsilon, cliffMargin;
        public float startGap, startHeight, prepare, active, recovery, interval;
        public float minimumStartDistance, brake, chargeDistance, chargeSpeed;
        public float moveCycle, deathDuration;
        public float[] bodySize, bodyOffset, attackSize, attackOffset, attackFrameDurations;
        public float[] hurtSize, hurtOffset;
        public static FieldMouseDefinition Load()
        {
            var text = Resources.Load<TextAsset>("MousePilot/Definition");
            if (!text) throw new InvalidOperationException("Run export_field_mouse.py before starting the mouse pilot.");
            var d = JsonUtility.FromJson<FieldMouseDefinition>(text.text);
            if (d == null || d.id != "01_field_mouse" || d.bodySize.Length != 2 || d.attackFrameDurations.Length != 8)
                throw new InvalidOperationException("Invalid field mouse definition.");
            return d;
        }
    }

    public struct MouseObservation
    {
        public Vector2 position, spawn, targetPosition;
        public bool detected, visible, pathToTarget, canAttack, insideStartDistance;
        public bool pathToLastKnown, atLastKnown, returnPath, atSpawn, canReengage;
    }

    // Decision state only. Geometry and Unity movement live in FieldMouseEnemy.
    public sealed class FieldMouseBrain
    {
        public readonly FieldMouseDefinition definition;
        public FieldMouseState State { get; private set; }
        public double StartedAt { get; private set; }
        public double NextAttackAt { get; private set; }
        public double RecoveryUntil { get; private set; }
        public double StaggerUntil, StunUntil, RootUntil;
        public double WaitStartedAt { get; private set; }
        public Vector2 LastKnown { get; private set; }
        public int Facing { get; private set; } = 1;
        public int Execution { get; private set; }
        public bool ReturnPending { get; private set; }
        public bool Executing { get; private set; }
        public bool AttackRequested { get; private set; }
        public float DesiredVelocity { get; private set; }
        bool engaged, returnedFromPursuit;
        double cancelledExecutingAt = double.NegativeInfinity;
        readonly float targetFacingDeadzone;
        public FieldMouseBrain(FieldMouseDefinition d, float targetFacingDeadzone = 0)
        { definition = d; this.targetFacingDeadzone = targetFacingDeadzone; Reset(); }
        void FaceTarget(float deltaX) { if (Mathf.Abs(deltaX) > targetFacingDeadzone) Facing = deltaX < 0 ? -1 : 1; }
        public void Reset()
        {
            State = FieldMouseState.Idle; StartedAt = double.NegativeInfinity; NextAttackAt = 0;
            RecoveryUntil = StaggerUntil = StunUntil = RootUntil = 0;
            WaitStartedAt = double.NaN; ReturnPending = Executing = AttackRequested = engaged = returnedFromPursuit = false;
            Execution = 0; Facing = 1; DesiredVelocity = 0; cancelledExecutingAt = double.NegativeInfinity;
        }
        public void Die()
        { Executing = AttackRequested = false; State = FieldMouseState.Dead; DesiredVelocity = 0; }
        public void Interrupt(double now)
        {
            AttackRequested = false;
            if (Executing) { cancelledExecutingAt = now; Executing = false; }
            if (State != FieldMouseState.Dead) State = now < RecoveryUntil ? FieldMouseState.Recovery : FieldMouseState.Idle;
            DesiredVelocity = 0;
            // Neither S+I nor an existing recovery deadline is reset.
        }
        public void StopIntoRecovery(double now)
        {
            if (Executing) { Executing = false; RecoveryUntil = now + definition.recovery; }
            AttackRequested = false; DesiredVelocity = 0;
            if (State != FieldMouseState.Dead) State = FieldMouseState.Recovery;
        }
        public void LeashReached(double now)
        {
            if (State == FieldMouseState.Dead) return;
            AttackRequested = false;
            if (Executing || cancelledExecutingAt == now)
            {
                Executing = false;
                if (now >= RecoveryUntil) RecoveryUntil = now + definition.recovery;
            }
            ReturnPending = true; engaged = false; DesiredVelocity = 0;
            State = now < RecoveryUntil ? FieldMouseState.Recovery : FieldMouseState.Return;
        }
        public bool ActiveDuring(double from, double to)
        { return Executing && Math.Max(from, StartedAt + definition.prepare) < Math.Min(to, StartedAt + definition.prepare + definition.active); }
        public int AttackFrame(double now)
        {
            double age = Math.Max(0, now - StartedAt), cursor = 0;
            for (int i = 0; i < definition.attackFrameDurations.Length; i++)
            { cursor += definition.attackFrameDurations[i]; if (age < cursor) return i; }
            return 7;
        }
        void Phase(double now)
        {
            if (!Executing) return;
            if (now >= StartedAt + definition.prepare + definition.active + definition.brake)
            {
                Executing = false; RecoveryUntil = StartedAt + definition.prepare + definition.active + definition.brake + definition.recovery;
                State = now < RecoveryUntil ? FieldMouseState.Recovery : FieldMouseState.Idle;
            }
            else State = now >= StartedAt + definition.prepare ? FieldMouseState.Active : FieldMouseState.Prepare;
        }
        public void AdvanceTime(double now) { Phase(now); }
        public void DelayMotion(double seconds)
        {
            if (!(seconds > 0)) return;
            if (Executing) StartedAt += seconds;
            else if (State == FieldMouseState.Recovery) RecoveryUntil += seconds;
        }
        public void BeginAttack(double now)
        {
            if (!AttackRequested) throw new InvalidOperationException("A field mouse attack requires the current observed attack intent.");
            AttackRequested = false; StartedAt = now; NextAttackAt = now + definition.interval; Execution++;
            Executing = true; State = FieldMouseState.Prepare; WaitStartedAt = double.NaN;
        }
        public void Look(int direction) { if (!Executing && direction != 0) Facing = direction < 0 ? -1 : 1; }
        void Walk(float delta, double now)
        {
            if (delta == 0) return;
            Facing = delta < 0 ? -1 : 1;
            if (now >= RootUntil) DesiredVelocity = Facing * definition.walkSpeed;
        }
        void Wait(double now)
        {
            if (double.IsNaN(WaitStartedAt)) WaitStartedAt = now;
            State = FieldMouseState.SearchWait;
            if (now - WaitStartedAt >= definition.searchWait)
            { State = FieldMouseState.Return; engaged = false; ReturnPending = true; }
        }
        public void Tick(double now, MouseObservation o, bool enabled)
        {
            DesiredVelocity = 0; AttackRequested = false;
            if (State == FieldMouseState.Dead) return;
            Phase(now);
            if (Math.Abs(o.position.x - o.spawn.x) >= definition.leash) LeashReached(now);
            if (now < StaggerUntil || now < StunUntil) return;
            if (Executing || now < RecoveryUntil) return;
            if (ReturnPending) State = FieldMouseState.Return;
            if (!enabled) return;

            if (State == FieldMouseState.Return)
            {
                if (o.canReengage)
                { ReturnPending = false; engaged = true; WaitStartedAt = double.NaN; State = FieldMouseState.Chase; }
                else
                {
                    if (o.atSpawn) { ReturnPending = false; returnedFromPursuit = true; State = FieldMouseState.Idle; WaitStartedAt = double.NaN; }
                    else if (o.returnPath) Walk(o.spawn.x - o.position.x, now);
                    return;
                }
            }
            if (!engaged)
            {
                // Arrival must not turn an abandoned, unreachable target into a fresh chase.
                if (!o.detected || returnedFromPursuit && !o.canReengage) { State = FieldMouseState.Idle; return; }
                returnedFromPursuit = false; engaged = true; State = FieldMouseState.Chase;
            }
            if (o.visible)
            {
                LastKnown = o.targetPosition;
                if (o.canAttack && now >= NextAttackAt)
                {
                    FaceTarget(o.targetPosition.x - o.position.x);
                    AttackRequested = true; WaitStartedAt = double.NaN; return;
                }
                if (o.pathToTarget)
                {
                    WaitStartedAt = double.NaN; State = FieldMouseState.Chase;
                    if (o.insideStartDistance || Mathf.Abs(o.targetPosition.x - o.position.x) <= targetFacingDeadzone) FaceTarget(o.targetPosition.x - o.position.x);
                    else Walk(o.targetPosition.x - o.position.x, now);
                }
                else Wait(now);
            }
            else
            {
                State = FieldMouseState.LastKnown;
                if (o.atLastKnown || !o.pathToLastKnown || !double.IsNaN(WaitStartedAt)) Wait(now);
                else Walk(LastKnown.x - o.position.x, now);
            }
        }
    }
}
