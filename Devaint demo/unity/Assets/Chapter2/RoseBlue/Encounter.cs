using System;
using System.Collections.Generic;

namespace Deviant.Chapter2.RoseBlue
{
    public enum Phase { Dormant, Introduction, Machine, FloorTransition, Duo, RevivingBlue, AbsorptionPush, Absorption, FusedBlue, Complete, RunEnded }
    public enum Target { Machine, Rose, Blue }
    public enum RevivalAttackPolicy { PreserveSpawned, ClearAllBossEffects }
    public enum Signal { LockCamera, RemoveEliteWithoutReward, CancelBossEffects, BeginIntroduction, BeginFloorCutscene, BeginAbsorptionPush, BeginAbsorptionCutscene, BeginMachine, BeginDuo, BeginFusedBlue, ReviveBlue, ReturnRose, BeginRevival, CancelUnspawnedAttacks, Finished, RunEnded, ExchangeExplosion, RoseStunned, BlueStunned }
    public readonly struct Notice
    {
        public readonly Signal Kind;
        public readonly int Generation;
        public readonly Target? Actor;
        public Notice(Signal kind, int generation, Target? actor = null) { Kind = kind; Generation = generation; Actor = actor; }
    }

    // Simulation-only authority. All damage received during one simulation step is resolved in
    // a single batch so simultaneous Rose/Blue deaths have a deterministic priority.
    // Cutscenes are explicit completion handshakes, never guessed fixed-delay coroutines.
    public sealed class Encounter
    {
        readonly Queue<Notice> notices = new Queue<Notice>();
        readonly RevivalAttackPolicy revivalPolicy;
        double revivalRemaining, pushRemaining, exchangeStunRemaining;
        Target exchangeVictim;
        public Phase Phase { get; private set; } = Phase.Dormant;
        public int Generation { get; private set; }
        public double MachineHp { get; private set; } = Tuning.MachineHp;
        public double RoseHp { get; private set; } = Tuning.RoseHp;
        public double BlueHp { get; private set; } = Tuning.BlueHp;
        public ExchangeVolley Volley { get; private set; }
        public bool ExchangeActive { get; private set; }
        public bool EliteRemoved { get; private set; }
        public bool Terminal => Phase == Phase.Complete || Phase == Phase.RunEnded;
        public bool Cutscene => Phase == Phase.Introduction || Phase == Phase.FloorTransition || Phase == Phase.Absorption;
        public Encounter(RevivalAttackPolicy revivalPolicy)
        {
            if (!Enum.IsDefined(typeof(RevivalAttackPolicy), revivalPolicy)) throw new ArgumentOutOfRangeException(nameof(revivalPolicy));
            this.revivalPolicy = revivalPolicy;
        }
        public static Encounter CreateApproved() => new Encounter(Tuning.RevivalKeepsSpawned ? RevivalAttackPolicy.PreserveSpawned : RevivalAttackPolicy.ClearAllBossEffects);
        void Emit(Signal signal, Target? actor = null) { notices.Enqueue(new Notice(signal, Generation, actor)); }
        public bool TryRead(out Notice notice)
        {
            if (notices.Count == 0) { notice = default; return false; }
            notice = notices.Dequeue(); return true;
        }
        public int Begin()
        {
            if (Phase != Phase.Dormant) throw new InvalidOperationException("Encounter already started; a new run needs a new encounter.");
            Generation++; Phase = Phase.Introduction; EliteRemoved = true;
            Emit(Signal.LockCamera); Emit(Signal.RemoveEliteWithoutReward); Emit(Signal.BeginIntroduction);
            return Generation;
        }
        public bool CompleteCutscene(int generation)
        {
            if (generation != Generation || !Cutscene) return false;
            switch (Phase)
            {
                case Phase.Introduction: Phase = Phase.Machine; Emit(Signal.BeginMachine); break;
                case Phase.FloorTransition: Phase = Phase.Duo; Emit(Signal.BeginDuo); break;
                case Phase.Absorption: BlueHp = Tuning.FusedBlueHp; Phase = Phase.FusedBlue; Emit(Signal.BeginFusedBlue); break;
            }
            Generation++; // A completion token can only be consumed once, including across phases.
            return true;
        }
        public bool CanDamage(Target target)
        {
            if (Phase == Phase.Machine) return target == Target.Machine;
            if (Phase == Phase.FusedBlue) return target == Target.Blue;
            if (Phase == Phase.RevivingBlue) return target == Target.Rose;
            if (Phase != Phase.Duo || target == Target.Machine) return false;
            return !ExchangeActive || exchangeStunRemaining > 0 && target == exchangeVictim;
        }
        public void DamageBatch(double machine, double rose, double blue, bool playerDied = false)
        {
            CheckAmount(machine); CheckAmount(rose); CheckAmount(blue);
            if (playerDied) { EndRun(); return; }
            if (Terminal) return;
            if (CanDamage(Target.Machine)) MachineHp = Math.Max(0, MachineHp - machine);
            if (CanDamage(Target.Rose)) RoseHp = Math.Max(Phase == Phase.RevivingBlue ? 1 : 0, RoseHp - rose);
            if (CanDamage(Target.Blue)) BlueHp = Math.Max(0, BlueHp - blue);
            ResolveDeaths();
        }
        void ResolveDeaths()
        {
            if (Phase == Phase.Machine && MachineHp <= 0)
                Transition(Phase.FloorTransition, Signal.BeginFloorCutscene);
            else if (Phase == Phase.Duo && (RoseHp <= 0 || BlueHp <= 0))
            {
                pushRemaining = Tuning.PushSeconds;
                Transition(Phase.AbsorptionPush, Signal.BeginAbsorptionPush);
            }
            else if (Phase == Phase.FusedBlue && BlueHp <= 0)
                Transition(Phase.Complete, Signal.Finished);
        }
        void Transition(Phase phase, Signal signal)
        {
            Phase = phase; Generation++; ExchangeActive = false; Volley = null; exchangeStunRemaining = 0;
            Emit(Signal.CancelBossEffects); Emit(signal);
        }
        public void EndRun()
        {
            if (Terminal) return;
            Transition(Phase.RunEnded, Signal.RunEnded);
        }
        public void Advance(double dt)
        {
            CheckAmount(dt);
            if (Terminal || Cutscene) return;
            if (Phase == Phase.RevivingBlue)
            {
                revivalRemaining -= dt;
                if (revivalRemaining <= 1e-9)
                {
                    BlueHp = Tuning.BlueHp; Phase = Phase.Duo;
                    Emit(Signal.ReviveBlue); Emit(Signal.ReturnRose);
                }
                return;
            }
            if (Phase == Phase.AbsorptionPush)
            {
                pushRemaining -= dt;
                if (pushRemaining <= 1e-9) { Phase = Phase.Absorption; Generation++; Emit(Signal.BeginAbsorptionCutscene); }
                return;
            }
            if (exchangeStunRemaining > 0)
            {
                exchangeStunRemaining = Math.Max(0, exchangeStunRemaining - dt);
                if (exchangeStunRemaining <= 1e-9) { exchangeStunRemaining = 0; ExchangeActive = false; }
            }
        }
        public void Stun(Target target)
        {
            if (!CanDamage(target) || target == Target.Machine) return;
            if (Phase == Phase.RevivingBlue && Tuning.RevivalControlImmune) return;
            Emit(Signal.CancelUnspawnedAttacks, target); // Partner cancellation is an explicit host policy.
        }
        public void HomingArrowHitsBlue()
        {
            if (Phase != Phase.FusedBlue) return;
            DamageBatch(0, 0, Tuning.HomingBossDamage);
            if (Phase != Phase.FusedBlue) return;
            Emit(Signal.CancelUnspawnedAttacks, Target.Blue); Emit(Signal.BlueStunned, Target.Blue);
        }
        public ExchangeVolley BeginExchange(double travelWidth, bool allowSurvivorDestruction = Tuning.ExchangeAllowSurvivorDestruction)
        {
            if (Phase != Phase.Duo || ExchangeActive) throw new InvalidOperationException("Exchange requires idle duo.");
            Volley = new ExchangeVolley(travelWidth, allowSurvivorDestruction); ExchangeActive = true; return Volley;
        }
        // Resolve projectile damage before movement/contact in the host's fixed-step contract.
        public void ResolveExchange()
        {
            if (Volley == null || !Volley.Finished || Phase != Phase.Duo) return;
            var result = Volley.Result; Volley = null;
            if (result == ExchangeResult.BothDestroyed || result == ExchangeResult.CenterExplosion)
            {
                if (result == ExchangeResult.CenterExplosion) Emit(Signal.ExchangeExplosion);
                ExchangeActive = !Tuning.ExchangeReleaseImmediately;
                return;
            }
            exchangeVictim = result == ExchangeResult.HitRose ? Target.Rose : Target.Blue;
            if (exchangeVictim == Target.Rose) { RoseHp = Math.Max(0, RoseHp - Tuning.ExchangeBlueImpactDamage); BlueHp = Math.Min(Tuning.BlueHp, BlueHp + Tuning.ExchangeHeal); }
            else { BlueHp = Math.Max(0, BlueHp - Tuning.ExchangeRoseImpactDamage); RoseHp = Math.Min(Tuning.RoseHp, RoseHp + Tuning.ExchangeHeal); }
            exchangeStunRemaining = Tuning.ExchangeStun;
            Emit(Signal.CancelUnspawnedAttacks);
            Emit(exchangeVictim == Target.Rose ? Signal.RoseStunned : Signal.BlueStunned);
            ResolveDeaths();
        }
        public void FinishEmptyExchange()
        {
            if (Volley != null || exchangeStunRemaining > 0) throw new InvalidOperationException("Exchange still active.");
            ExchangeActive = false;
        }
        internal static void CheckAmount(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        }
    }

    public enum ExchangeResult { None, BothDestroyed, CenterExplosion, HitRose, HitBlue }
    public sealed class ExchangeVolley
    {
        readonly double width;
        readonly bool allowSurvivorDestruction;
        double distance,age,survivorSpeed;
        public double Distance => distance;
        public double RoseArrowHp { get; private set; } = Tuning.ExchangeRoseHp;
        public double BlueOrbHp { get; private set; } = Tuning.ExchangeBlueHp;
        public ExchangeResult Result { get; private set; }
        public bool Finished => Result != ExchangeResult.None;
        public ExchangeVolley(double width, bool allowSurvivorDestruction)
        {
            Encounter.CheckAmount(width); if (width == 0) throw new ArgumentOutOfRangeException(nameof(width)); this.width = width;
            this.allowSurvivorDestruction = allowSurvivorDestruction;
        }
        public void DamageBatch(double roseArrow, double blueOrb)
        {
            Encounter.CheckAmount(roseArrow); Encounter.CheckAmount(blueOrb);
            if (Finished) return;
            if (!allowSurvivorDestruction && (RoseArrowHp == 0 || BlueOrbHp == 0)) return;
            RoseArrowHp = Math.Max(0, RoseArrowHp - roseArrow); BlueOrbHp = Math.Max(0, BlueOrbHp - blueOrb);
            if (RoseArrowHp == 0 && BlueOrbHp == 0) Result = ExchangeResult.BothDestroyed;
        }
        public void Advance(double dt)
        {
            Encounter.CheckAmount(dt); if (Finished) return;
            if(RoseArrowHp>0&&BlueOrbHp>0){
                double duration=Tuning.ExchangeMeetSeconds;
                double speed=width/(2*(duration+.5*Tuning.ExchangeAcceleration*duration*duration));
                distance+=speed*(dt+Tuning.ExchangeAcceleration*(age*dt+.5*dt*dt));
            }else{
                if(survivorSpeed==0)survivorSpeed=(width-distance)/Tuning.ExchangeSurvivorSeconds;
                distance+=survivorSpeed*dt;
            }
            age+=dt;
            if (RoseArrowHp > 0 && BlueOrbHp > 0 && distance >= width / 2) Result = ExchangeResult.CenterExplosion;
            else if (distance >= width) Result = RoseArrowHp > 0 ? ExchangeResult.HitBlue : ExchangeResult.HitRose;
        }
    }
}
