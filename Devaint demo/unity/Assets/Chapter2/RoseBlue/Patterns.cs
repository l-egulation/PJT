using System;
using System.Collections.Generic;

namespace Deviant.Chapter2.RoseBlue
{
    public enum Pattern { LayerShots, TrapSnipe, MachineAndPlatforms, StunAndInk, RootAndShots, Exchange, Swap, Homing, Bounce, ArrowRain, SequenceBlasts, ExpandingFan }
    public enum CueKind { Tell, LockTarget, Spawn, Damage, Activate, Whiten, Explode, Remove, Swap }
    public readonly struct Cue
    {
        public readonly double At;
        public readonly CueKind Kind;
        public readonly string Channel;
        public readonly int Index;
        public readonly bool Independent;
        public readonly string Group;
        public Cue(double at, CueKind kind, string channel, int index = 0, bool independent = false, string group = null)
        { At = at; Kind = kind; Channel = channel; Index = index; Independent = independent; Group = group; }
    }
    public sealed class PatternClock
    {
        readonly List<Cue> remaining;
        readonly HashSet<string> spawned = new HashSet<string>();
        public double Age { get; private set; }
        public double RoseAge { get; private set; }
        public double BlueAge { get; private set; }
        double realAge;
        readonly Dictionary<string, double> groupSpawnedAt = new Dictionary<string, double>();
        readonly Dictionary<string, double> groupScheduledAt = new Dictionary<string, double>();
        public bool Finished => remaining.Count == 0;
        public bool HasSpawned(string group) => spawned.Contains(group);
        public bool WillSpawn(string channel) => remaining.Exists(c => c.Kind == CueKind.Spawn && c.Channel == channel);
        public bool HasPendingAttack => remaining.Exists(c => c.Kind == CueKind.Tell || c.Kind == CueKind.LockTarget || c.Kind == CueKind.Spawn || c.Kind == CueKind.Damage || c.Kind == CueKind.Explode
            || c.Channel == "swap-finished" || c.Channel == "sequence-finished" || c.Channel == "platform-rain");
        public PatternClock(IEnumerable<Cue> schedule)
        {
            remaining = new List<Cue>(schedule);
            // Stable insertion sort: at equal timestamps, source order defines spawn-before-impact.
            for (int i = 1; i < remaining.Count; i++)
            {
                var item = remaining[i]; int j = i - 1;
                while (j >= 0 && remaining[j].At > item.At) { remaining[j + 1] = remaining[j]; j--; }
                remaining[j + 1] = item;
            }
        }
        public List<Cue> Advance(double dt)
        {
            Encounter.CheckAmount(dt); Age += dt;
            var due = new List<Cue>();
            while (remaining.Count > 0 && remaining[0].At <= Age + 1e-9)
            {
                var cue = remaining[0]; remaining.RemoveAt(0);
                if (cue.Kind == CueKind.Spawn && cue.Group != null) spawned.Add(cue.Group);
                due.Add(cue);
            }
            return due;
        }
        // Each caster keeps its own motion progress. Spawned independent hazards then use real combat time;
        // changing the caster's slow never changes a projectile already in flight or restarts its fuse.
        public List<Cue> Advance(double dt, double roseScale, double blueScale, Func<Cue, int> caster)
        {
            Encounter.CheckAmount(dt); Encounter.CheckAmount(roseScale); Encounter.CheckAmount(blueScale);
            realAge += dt; RoseAge += dt * roseScale; BlueAge += dt * blueScale;
            Age += dt * Math.Min(roseScale, blueScale);
            var due = new List<Cue>();
            for (int i = 0; i < remaining.Count;)
            {
                var cue = remaining[i]; int source = caster(cue);
                double age = source == 1 ? RoseAge : source == 2 ? BlueAge : Age;
                bool ready = cue.At <= age + 1e-9;
                if (cue.Independent && cue.Group != null && groupSpawnedAt.TryGetValue(cue.Group, out double started))
                    ready = realAge - started + 1e-9 >= cue.At - groupScheduledAt[cue.Group];
                if (!ready) { i++; continue; }
                remaining.RemoveAt(i); due.Add(cue);
                if (cue.Kind == CueKind.Spawn && cue.Group != null)
                { spawned.Add(cue.Group); groupSpawnedAt[cue.Group] = realAge; groupScheduledAt[cue.Group] = cue.At; }
            }
            return due;
        }
        public void InterruptCaster()
        {
            remaining.RemoveAll(c => !c.Independent || c.Group == null || !spawned.Contains(c.Group));
        }
        public void InterruptCaster(Predicate<Cue> belongsToCaster)
        { remaining.RemoveAll(c => belongsToCaster(c) && (!c.Independent || c.Group == null || !spawned.Contains(c.Group))); }
        public void CancelHazard(string group)
        {
            remaining.RemoveAll(c => c.Group == group); spawned.Remove(group);
        }
        public void EndPhase() { remaining.Clear(); spawned.Clear(); }
    }

    public static class Patterns
    {
        // Integration supplies unresolved timing explicitly; no implicit balancing default.
        public static List<Cue> Build(Pattern pattern, double layerBlueFireDelay = double.NaN)
        {
            var c = new List<Cue>();
            switch (pattern)
            {
                case Pattern.LayerShots:
                    Encounter.CheckAmount(layerBlueFireDelay);
                    c.Add(new Cue(0, CueKind.Tell, "layer-pair"));
                    for (int i = 0; i < (int)Tuning.Phase1ArrowCount; i++) c.Add(new Cue(Tuning.Phase1ShotTell + i * Tuning.Phase1ShotInterval, CueKind.Spawn, "rose-arrow", i));
                    c.Add(new Cue(Tuning.Phase1ShotTell + layerBlueFireDelay, CueKind.Spawn, "blue-orb")); break;
                case Pattern.TrapSnipe:
                    c.Add(new Cue(0, CueKind.Tell, "all-platforms"));
                    c.Add(new Cue(Tuning.TrapTell, CueKind.Spawn, "traps", 0, false, "traps"));
                    c.Add(new Cue(Tuning.TrapTell + Tuning.TrapLife, CueKind.Remove, "traps", 0, true, "traps"));
                    double scope = Tuning.TrapTell + Tuning.SniperDelay;
                    c.Add(new Cue(scope, CueKind.Tell, "sniper-track"));
                    c.Add(new Cue(scope + Tuning.SniperTrack, CueKind.LockTarget, "sniper"));
                    c.Add(new Cue(scope + Tuning.SniperTrack + Tuning.SniperLock, CueKind.Damage, "sniper")); break;
                case Pattern.SequenceBlasts:
                    for(int i=0;i<6;i++){
                        c.Add(new Cue(i*Tuning.SequenceFlashGap,CueKind.Tell,"sequence-flash",i));
                        c.Add(new Cue(i*Tuning.SequenceFlashGap+Tuning.SequenceFlashLife,CueKind.Activate,"sequence-hide",i));
                    }
                    double remembered=5*Tuning.SequenceFlashGap+Tuning.SequenceFlashLife;
                    c.Add(new Cue(remembered+Tuning.SequenceMoveSeconds,CueKind.Tell,"sequence-warning"));
                    double firstBlast=remembered+Tuning.SequenceMoveSeconds+Tuning.SequenceWarningSeconds;
                    c.Add(new Cue(firstBlast+5*Tuning.SequenceBlastGap+Tuning.SequenceBlastLife,CueKind.Activate,"sequence-finished"));
                    for(int i=0;i<6;i++)c.Add(new Cue(firstBlast+i*Tuning.SequenceBlastGap,CueKind.Spawn,"sequence-blast",i));
                    break;
                case Pattern.MachineAndPlatforms:
                    c.Add(new Cue(0, CueKind.Tell, "machine-platform"));
                    c.Add(new Cue(Tuning.MachineBurstTell, CueKind.Damage, "machine-burst"));
                    c.Add(new Cue(Tuning.MachineBurstTell, CueKind.Tell, "other-platforms"));
                    double rain = Tuning.MachineBurstTell + Tuning.PlatformRainTell;
                    c.Add(new Cue(rain, CueKind.Spawn, "platform-rain", 0, false, "platform-rain"));
                    c.Add(new Cue(rain, CueKind.Damage, "platform-rain", 0, true, "platform-rain"));
                    c.Add(new Cue(rain + Tuning.PlatformRainTick, CueKind.Damage, "platform-rain", 1, true, "platform-rain"));
                    c.Add(new Cue(rain + Tuning.PlatformRainLife, CueKind.Remove, "platform-rain", 0, true, "platform-rain")); break;
                case Pattern.StunAndInk:
                    // Both bosses stay where they stand: Rose aims during the tell while Blue charges,
                    // then the stun arrow and the ink fireball leave together.
                    c.Add(new Cue(0, CueKind.Tell, "stun-and-ink"));
                    // The ink charge sheet lasts half a second, so it starts half a second before the release.
                    c.Add(new Cue(Math.Max(0, Tuning.StunArrowTell - .5), CueKind.Tell, "ink-charge"));
                    c.Add(new Cue(Tuning.StunArrowTell, CueKind.Spawn, "stun-arrow"));
                    c.Add(new Cue(Tuning.StunArrowTell, CueKind.Spawn, "ink-ball")); break;
                case Pattern.RootAndShots:
                    c.Add(new Cue(Tuning.RootTell - Tuning.BlueDropSeconds, CueKind.LockTarget, "root-zone"));
                    c.Add(new Cue(0, CueKind.Tell, "root-and-shots"));
                    c.Add(new Cue(Tuning.RootTell, CueKind.Spawn, "root-zone", 0, false, "root-zone"));
                    for (int i = 0; i < (int)Tuning.DuoShotCount; i++) c.Add(new Cue(Tuning.RootTell + i * Tuning.DuoShotInterval, CueKind.Spawn, "duo-arrow", i));
                    c.Add(new Cue(Tuning.RootTell,CueKind.Activate,"blue-return"));
                    c.Add(new Cue(Tuning.RootTell + Tuning.RootLife, CueKind.Remove, "root-zone", 0, true, "root-zone")); break;
                case Pattern.Swap:
                    c.Add(new Cue(0,CueKind.Swap,"duo"));
                    c.Add(new Cue(0,CueKind.Tell,"swap-flanks"));
                    c.Add(new Cue(Tuning.SwapTell,CueKind.Spawn,"swap-flanks"));
                    // Finish the flanks before starting the normal rest.
                    c.Add(new Cue(Tuning.SwapAreaEnd,CueKind.Activate,"swap-finished"));
                    break;
                case Pattern.Exchange:
                    c.Add(new Cue(0, CueKind.Swap, "duo"));
                    c.Add(new Cue(0, CueKind.Tell, "exchange"));
                    c.Add(new Cue(Tuning.ExchangeTell, CueKind.Spawn, "exchange-pair"));
                    break;
                case Pattern.Homing:
                    c.Add(new Cue(0, CueKind.Tell, "homing"));
                    c.Add(new Cue(Tuning.HomingTell, CueKind.Spawn, "homing", 0, false, "homing"));
                    c.Add(new Cue(Tuning.HomingTell + Tuning.HomingLife, CueKind.Activate, "homing-exit", 0, true, "homing")); break;
                case Pattern.Bounce:
                    for (int i = 0; i < (int)Tuning.BounceCount; i++)
                    {
                        string g = "bounce-" + i;
                        c.Add(new Cue(i * Tuning.BounceTell, CueKind.Tell, "bounce", i));
                        c.Add(new Cue((i + 1) * Tuning.BounceTell, CueKind.Spawn, "bounce", i, false, g));
                        c.Add(new Cue(Tuning.BounceWhiteAt, CueKind.Whiten, "bounce", i, true, g));
                        c.Add(new Cue(Tuning.BounceExplodeAt, CueKind.Explode, "bounce", i, true, g));
                        c.Add(new Cue(Tuning.BounceExplodeAt + Tuning.ExplosionActive, CueKind.Remove, "bounce", i, true, g));
                    }
                    break;
                case Pattern.ExpandingFan:
                    c.Add(new Cue(0, CueKind.Tell, "fan-area"));
                    c.Add(new Cue(Tuning.FanTell, CueKind.Spawn, "fan-area", 0, false, "fan-area"));
                    double formation = Tuning.FanTell + Tuning.FanExpandSeconds;
                    for (int i = 0; i < (int)Tuning.FanCount; i++)
                        c.Add(new Cue(formation + i * Tuning.FanCreateGap, CueKind.Tell, "fan-arrow", i));
                    double fire = formation + (Tuning.FanCount - 1) * Tuning.FanCreateGap;
                    double[] intervals = { 0, .5, .1, .5, .1, .1 };
                    for (int i = 0; i < (int)Tuning.FanCount; i++) {
                        fire += intervals[i];
                        c.Add(new Cue(fire, CueKind.Spawn, "fan-arrow", i));
                    }
                    break;
                case Pattern.ArrowRain:
                    c.Add(new Cue(0, CueKind.Tell, "rain-start"));
                    for (int i = 0; i < (int)Tuning.RainCount; i++)
                    {
                        string g = "rain-" + i;
                        double offset = i * Tuning.RainLandingSpan / (Tuning.RainCount - 1);
                        double spawn = Tuning.RainTell + offset;
                        double blast = Tuning.RainTell + Tuning.RainFall + Tuning.RainLandingSpan + Tuning.RainFuse + offset;
                        c.Add(new Cue(spawn, CueKind.Spawn, "rain-arrow", i, false, g));
                        c.Add(new Cue(spawn + Tuning.RainFall, CueKind.Activate, "rain-land", i, true, g));
                        c.Add(new Cue(blast, CueKind.Explode, "rain", i, true, g));
                        c.Add(new Cue(blast + Tuning.ExplosionActive, CueKind.Remove, "rain", i, true, g));
                    }
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(pattern));
            }
            return c;
        }
    }
}
