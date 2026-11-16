using System;

namespace Deviant.Run
{
    public enum RunOutcome { Died, ChapterCleared }

    public readonly struct RunResult
    {
        public readonly RunOutcome Outcome;
        public readonly double ElapsedSeconds;
        public readonly int Kills;
        public readonly int Seed;
        public RunResult(RunOutcome outcome, double elapsedSeconds, int kills, int seed)
        { Outcome = outcome; ElapsedSeconds = elapsedSeconds; Kills = kills; Seed = seed; }
    }

    // One run from the start room to a death or chapter clear. Ends exactly once.
    public sealed class RunSession
    {
        public WeaponGrowth Growth {get;set;}
        // Sketch weapons, ink codex and kill-milestone offers (docs/design/equipment/sketch-weapons.md).
        public SketchRunState Sketch {get;set;}
        public int Seed { get; }
        public double StartedAt { get; private set; }
        bool clockStarted;
        double completedChapterSeconds;
        public int Chapter { get; private set; } = 1;
        public int Kills { get; private set; }
        public bool BossDefeated { get; private set; }
        public RunResult? Result { get; private set; }
        public bool Ended => Result.HasValue;
        public RunEconomy Economy { get; }
        public RunPerks Perks { get; }
        public RunStash Stash { get; } = new RunStash();
        // Departure snapshot from the starting village; null for development and training runs.
        public RunStartPlan Plan { get; }
        public Guid RunId { get; }
        // Bonus payout receipts of this run, keyed profileId:runId:unlockId.
        readonly System.Collections.Generic.HashSet<string> receipts = new System.Collections.Generic.HashSet<string>();
        public bool HasReceipt(string unlockId) => receipts.Contains(ReceiptKey(unlockId));
        string ReceiptKey(string unlockId) => (Plan?.ProfileId ?? "-") + ":" + RunId + ":" + unlockId;

        internal RunSession(int seed, double startedAt, EconomyData economy, RunStartPlan plan = null, int chapter = 1)
        {
            Chapter = chapter; Seed = seed; StartedAt = startedAt; Plan = plan; RunId = plan?.RunId ?? Guid.NewGuid();
            if (economy != null) Economy = new RunEconomy(economy);
            Perks = new RunPerks(plan != null && !plan.Training ? plan.Perks : RunPerkSettings.None, Economy, () => !Ended);
            GrantStartingGold();
        }

        // The highest owned starting-gold tier adds to the existing starting gold once per run; reloading scenes or resetting combat
        // keeps this session, so the receipt prevents any repeat. Training plans never pay.
        public bool GrantStartingGold()
        {
            if (Economy == null || Plan == null || Plan.Training || Plan.StartingGoldBonus <= 0) return false;
            if (!receipts.Add(ReceiptKey(ProgressionIds.StartingGoldReceipt))) return false;
            Economy.Earn(Plan.StartingGoldBonus);
            return true;
        }

        // Play time is measured on the chapter's own clock, which stops while the game is paused.
        public void StartClock(double now) { if (clockStarted || Ended) return; clockStarted = true; StartedAt = now; }
        public void RecordKill() { if (!Ended) Kills++; }
        // Magic pigment this run earned across its chapters, for the result screen (the profile keeps the real balance).
        public int Pigment { get; private set; }
        public void RecordPigment(int amount) { if (!Ended && amount > 0) Pigment += amount; }
        public void MarkBossDefeated() { if (!Ended) BossDefeated = true; }

        internal bool TryAdvanceChapter(double now)
        {
            if (Ended || Chapter != 1 || !BossDefeated) return false;
            completedChapterSeconds += Math.Max(0, now - StartedAt);
            Chapter = 2; BossDefeated = false; clockStarted = false;
            return true;
        }

        internal bool TryEnd(RunOutcome outcome, double now)
        {
            if (Ended) return false;
            Result = new RunResult(outcome, completedChapterSeconds + Math.Max(0, now - StartedAt), Kills, Seed);
            return true;
        }
    }

    // Owns the current run and its single settlement; scene loading and screens live in Unity adapters.
    public sealed class RunFlow
    {
        readonly Func<int> seedSource;
        readonly EconomyData economy;
        public RunSession Current { get; private set; }
        public event Action<RunSession> Started;
        public event Action<RunResult> Ended;

        public RunFlow(Func<int> seedSource, EconomyData economy = null)
        { this.seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource)); this.economy = economy; }

        readonly System.Collections.Generic.HashSet<Guid> startedPlans = new System.Collections.Generic.HashSet<Guid>();

        // A departure plan starts exactly one run; restarting it would pay its starting bonus again.
        public RunSession StartNewRun(double now, int? seed = null, RunStartPlan plan = null)
        {
            if (plan != null && !startedPlans.Add(plan.RunId)) throw new InvalidOperationException("This departure already started its run.");
            Current = new RunSession(seed ?? seedSource(), now, economy, plan);
            Started?.Invoke(Current);
            return Current;
        }

        // Debug scene entry starts a separate session without boss kills, clear rewards or a village plan.
        public RunSession StartDebugChapter(double now, int chapter)
        {
            if (chapter != 1 && chapter != 2) throw new ArgumentOutOfRangeException(nameof(chapter));
            Current = new RunSession(seedSource(), now, economy, chapter: chapter);
            Started?.Invoke(Current);
            return Current;
        }

        // Abandon play data without producing a death/clear settlement.
        public void DiscardCurrentRun()
        {
            Current = null;
            startedPlans.Clear();
        }

        public bool TryAdvanceChapter(double now) => Current != null && Current.TryAdvanceChapter(now);

        public bool TryDie(double now) => TryEnd(RunOutcome.Died, now);

        // The chapter is cleared only through the exit after the boss has fallen.
        public bool TryClearChapter(double now) => Current != null && Current.BossDefeated && TryEnd(RunOutcome.ChapterCleared, now);

        bool TryEnd(RunOutcome outcome, double now)
        {
            if (Current == null || !Current.TryEnd(outcome, now)) return false;
            Ended?.Invoke(Current.Result.Value);
            return true;
        }
    }
}
