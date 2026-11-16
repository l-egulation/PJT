using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Run
{
    public enum UnlockState { Available, Owned, LockedUntilFirstRun, LockedUntilBossClear, LockedUntilPieces, PricePending, MissingPrerequisite, NotEnoughPigment, Unknown, LockedUntilChapter2Clear }

    public readonly struct UnlockStatus
    {
        public readonly ProgressionUnlock Unlock;
        public readonly UnlockState State;
        public readonly string MissingPrerequisite;
        public UnlockStatus(ProgressionUnlock unlock, UnlockState state, string missing = null) { Unlock = unlock; State = state; MissingPrerequisite = missing; }
        public bool CanBuy => State == UnlockState.Available;
    }

    // What a new run takes from the profile at the moment of departure. Later purchases do not change it.
    public sealed class RunStartPlan
    {
        public string ProfileId { get; }
        public Guid RunId { get; }
        public IReadOnlyList<string> Blueprints { get; }
        public bool Unarmed => Blueprints.Count == 0;
        public int PaletteSlots { get; }
        public bool Wildcard1 { get; }
        public bool Wildcard2 { get; }
        public int StartingGoldBonus { get; }
        public int DestroyedInkGoldBonus { get; }
        // Permanent character upgrades: attack percent added to the maintenance attack multiplier, flat max health.
        public int AttackBonusPercent { get; }
        public int MaxHealthBonus { get; }
        // Shared bag slots in all from the bag unlock (0: no tier owned, the run keeps its base bag).
        public int BagSlots { get; }
        // Permanent walk/run speed percent, added to the yellow inks' movement buffs.
        public int MoveSpeedBonusPercent { get; }
        // Dash charges sharing one recovery (1 without the extra-dash unlock).
        public int DashCharges { get; }
        // Colour ink parts whose village unlock was not owned at departure; drops, draws and the altar skip them.
        // Training runs keep every part.
        public IReadOnlyCollection<string> LockedInkParts { get; }
        public IReadOnlyCollection<string> LockedAugments { get; }
        public RunPerkSettings Perks { get; }
        public bool Training { get; }
        internal RunStartPlan(string profileId, Guid runId, string[] blueprints, int slots, bool w1, bool w2, int gold, int destroyed, int attack, int health, bool training,
            int bagSlots = 0, int moveSpeed = 0, int dashCharges = ProgressionIds.BaseDashCharges, IEnumerable<string> lockedInkParts = null,
            RunPerkSettings perks = null, IEnumerable<string> lockedAugments = null)
        {
            ProfileId = profileId; RunId = runId; Blueprints = Array.AsReadOnly(blueprints); PaletteSlots = slots; Wildcard1 = w1; Wildcard2 = w2;
            StartingGoldBonus = gold; DestroyedInkGoldBonus = destroyed; AttackBonusPercent = attack; MaxHealthBonus = health; Training = training;
            BagSlots = bagSlots; MoveSpeedBonusPercent = moveSpeed; DashCharges = Math.Max(ProgressionIds.BaseDashCharges, dashCharges);
            LockedInkParts = Array.AsReadOnly(training || lockedInkParts == null ? Array.Empty<string>() : lockedInkParts.Distinct().ToArray());
            Perks = training ? RunPerkSettings.None : perks ?? RunPerkSettings.None;
            LockedAugments = Array.AsReadOnly(training || lockedAugments == null ? Array.Empty<string>() : lockedAugments.Distinct().ToArray());
        }
        public bool Wildcard(int weaponSlot) => weaponSlot == 1 ? Wildcard1 : weaponSlot == 2 && Wildcard2;
    }

    // The only writer of the permanent profile. Every command re-checks its conditions at execution time and either
    // commits the change together with a successful save or leaves the profile untouched.
    public sealed class ProgressionService
    {
        const int SettledRunMemory = 64;
        public ProgressionCatalog Catalog { get; }
        readonly IProfileStore store;
        ProfileData profile;
        bool busy, pigmentDirty;
        bool migratedDuringLoad;
        readonly Dictionary<Guid, HashSet<string>> awardedKills = new Dictionary<Guid, HashSet<string>>();
        readonly HashSet<Guid> chapterOneCleared = new HashSet<Guid>();
        public ProfileSnapshot Snapshot { get; private set; }
        public string LoadWarning { get; }
        public string LastSaveError { get; private set; }
        public string LastDebugNotice { get; private set; }
        public event Action Changed;

        public ProgressionService(ProgressionCatalog catalog, IProfileStore store, Func<Guid> newId = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            NewId = newId ?? Guid.NewGuid;
            profile = store.Load(LoadValidation, out var warning);
            LoadWarning = warning;
            if (profile == null)
            {
                profile = ProfileData.CreateNew(NewId().ToString());
                // A brand-new profile is written immediately so its id stays stable; a damaged file is left in place.
                if (warning == null) TrySave(profile);
            }
            else if (migratedDuringLoad)
            {
                profile.revision++;
                TrySave(profile);
            }
            // Profiles saved before chapter1Clears existed: every cleared run and the first boss clear passed chapter 1.
            profile.chapter1Clears = Math.Max(profile.chapter1Clears, Math.Max(profile.runsCleared, profile.chapter1BossFirstClear ? 1 : 0));
            // Profiles saved before chaptersCleared existed (it loads as 0): a chapter-1 clear reached chapter 1, a cleared run
            // chapter 2. Every settled clear now records chaptersCleared itself, and a first chapter-1 clear also counts as a
            // cleared run, so a profile that already has the field must keep it; otherwise each load gave chapter 2's piece.
            if (profile.chaptersCleared == 0)
                profile.chaptersCleared = profile.runsCleared > 0 ? 2 : profile.chapter1Clears > 0 ? 1 : 0;
            Snapshot = new ProfileSnapshot(profile);
        }

        readonly Func<Guid> NewId;
        string ValidationError(ProfileData data) { var errors = ProfileData.Validate(data, Catalog); return errors.Count == 0 ? null : string.Join("; ", errors); }
        // A saved profile may predate a lower tier; it is filled in before the profile is checked.
        string LoadValidation(ProfileData data)
        {
            migratedDuringLoad = ProfileData.MigrateProgression(data, Catalog);
            ProfileData.FillLowerTiers(data, Catalog); return ValidationError(data);
        }

        bool TrySave(ProfileData data, bool reset = false)
        {
            try { if (reset) store.Reset(data); else store.Save(data); LastSaveError = null; return true; }
            catch (Exception error) { LastSaveError = error.Message; return false; }
        }

        // Applies a change to a copy, saves it, and only then makes it current.
        bool Commit(Action<ProfileData> change)
        {
            if (busy) return false;
            busy = true;
            try
            {
                var next = profile.Clone();
                change(next);
                next.revision = profile.revision + 1;
                var problem = ValidationError(next);
                if (problem != null) { LastSaveError = problem; return false; }
                if (!TrySave(next)) return false;
                profile = next; Snapshot = new ProfileSnapshot(profile); pigmentDirty = false;
            }
            finally { busy = false; }
            Changed?.Invoke();
            return true;
        }

        // Debug callers must request confirmation before invoking these commands. Commit validates the whole profile
        // and publishes it only after a successful save, just like normal progression changes.
        public bool DebugSetPigment(long value)
        {
            if (value < 0) { LastSaveError = "negative pigment"; return false; }
            return Commit(p => p.pigment = value);
        }

        public bool DebugSetTier(string firstId, int tier, bool force = false)
        {
            LastDebugNotice = null;
            var first = Catalog.Find(firstId);
            if (first == null || Catalog.FirstTier(first) != first) { LastSaveError = "Unknown tier group"; return false; }
            var line = new List<ProgressionUnlock>();
            for (var u = first; u != null; u = Catalog.NextTier(u)) line.Add(u);
            if (tier < 0 || tier > line.Count) { LastSaveError = "Tier out of range"; return false; }
            var ids = new HashSet<string>(line.Select(u => u.id));
            string notice = null;
            bool saved = Commit(p =>
            {
                var target = line.Take(tier).ToArray();
                p.unlocks = p.unlocks.Where(id => !ids.Contains(id)).Concat(target.Select(u => u.id)).ToArray();
                p.debugUnlockOverrides = (p.debugUnlockOverrides ?? Array.Empty<string>()).Where(id => !ids.Contains(id) && p.unlocks.Contains(id))
                    .Concat(force ? target.Select(u => u.id) : Enumerable.Empty<string>()).Distinct().ToArray();
                if (force)
                {
                    var missing = target.SelectMany(u => DebugMissingConditions(p, u)).Distinct().ToArray();
                    notice = missing.Length == 0 ? "임의 설정 완료했습니다. 재화는 차감하지 않았습니다."
                        : "미충족 조건: " + string.Join(" · ", missing) + " — 조건을 무시하고 임의 설정 완료했습니다. 재화는 차감하지 않았습니다.";
                }
            });
            if (saved) LastDebugNotice = notice ?? "설정 저장 완료";
            return saved;
        }
        IEnumerable<string> DebugMissingConditions(ProfileData p, ProgressionUnlock unlock)
        {
            string Gate(string id) => id == ProgressionIds.FirstRunGate ? "첫 런 종료" : id == ProgressionIds.BossGate ? "1챕터 보스 처치" : id == ProgressionIds.Chapter2Gate ? "2챕터 보스 처치" : id;
            var building = Catalog.Building(unlock.building);
            foreach (var gate in new[] { unlock.requiredClear, building?.requiredClear }.Where(g => !string.IsNullOrEmpty(g)).Distinct())
                if (!p.Reached(gate)) yield return Gate(gate);
            int pieces = unlock.openAtPieces;
            if (Math.Min(Catalog.PalettePieces, p.chaptersCleared) < pieces) yield return "마을 팔레트 조각 " + pieces + "개";
            foreach (var pre in unlock.prerequisites ?? Array.Empty<string>())
                if (!p.unlocks.Contains(pre)) yield return "선행 해금: " + (Catalog.Find(pre)?.name ?? pre);
            if (!Catalog.PriceKnown(unlock)) yield return unlock.name + " 가격 미확정";
            else if (p.pigment < unlock.cost) yield return Catalog.Data.currency.displayName + " " + unlock.cost + "개 (보유 " + p.pigment + ")";
        }

        public bool DebugSetUnlock(string id, bool owned)
        {
            if (!Catalog.Contains(id)) { LastSaveError = "unknown unlock: " + id; return false; }
            if (profile.unlocks.Contains(id) == owned) return true;
            return Commit(p => { p.unlocks = owned ? p.unlocks.Concat(new[] { id }).ToArray() : p.unlocks.Where(existing => existing != id).ToArray();
                p.debugUnlockOverrides = (p.debugUnlockOverrides ?? Array.Empty<string>()).Where(p.unlocks.Contains).ToArray(); });
        }

        // Village palette pieces gathered: one per chapter whose boss was beaten for the first time (chapter 1 gives the
        // first, chapter 2 the second, ... chapter 5 the fifth), at most the catalog's pieces (user decision 2026-11-04).
        public int PalettePieces => Math.Min(Catalog.PalettePieces, profile.chaptersCleared);
        public int ChaptersCleared => profile.chaptersCleared;

        // Pieces a station's building waits for (0 when it is open from the start or is no building's station).
        public int PiecesFor(string station) => Catalog.BuildingAt(station)?.openAtPieces ?? 0;
        // A station opens with its pieces and, for the supply office, after the first settled run (before any piece).
        public bool StationOpen(string station) => PalettePieces >= PiecesFor(station) && GateReached(Catalog.BuildingAt(station)?.requiredClear);
        // Whether the station waits for anything at all, so its opening plays once when it comes.
        public bool StationGated(string station) => PiecesFor(station) > 0 || !string.IsNullOrEmpty(Catalog.BuildingAt(station)?.requiredClear);
        // The condition on a station's name card (it fades out while the lock breaks); empty for a station that never waits.
        public string StationNeed(string station)
        {
            var gate = Catalog.BuildingAt(station)?.requiredClear;
            if (!string.IsNullOrEmpty(gate) && (PiecesFor(station) == 0 || !GateReached(gate)))
                return gate == ProgressionIds.FirstRunGate ? "첫 런을 마치면 열림" : "1챕터 보스 처치 필요";
            return PiecesFor(station) > 0 ? $"{PiecesFor(station)}챕터 클리어 필요" : "";
        }
        bool GateReached(string gate) => string.IsNullOrEmpty(gate) || profile.Reached(gate);

        // A boss exit leads on to the next chapter; only the last chapter this build has ends a cleared run and goes home.
        // A death goes home from any chapter (user decision 2026-11-13, replacing the 2026-11-04 first-clear return).
        // The chapter's palette piece is still recorded when its boss falls for the first time.
        public const int LastChapter = 2;
        public bool ChapterClearEndsRun(int chapter) => chapter >= LastChapter;
        public bool ChapterOneClearEndsRun => ChapterClearEndsRun(1);

        public UnlockStatus StatusOf(string unlockId)
        {
            var unlock = Catalog.Find(unlockId);
            if (unlock == null) return new UnlockStatus(null, UnlockState.Unknown);
            if (profile.unlocks.Contains(unlockId)) return new UnlockStatus(unlock, UnlockState.Owned);
            if (!profile.Reached(unlock.requiredClear))
                return new UnlockStatus(unlock, unlock.requiredClear == ProgressionIds.FirstRunGate ? UnlockState.LockedUntilFirstRun
                    : unlock.requiredClear == ProgressionIds.Chapter2Gate ? UnlockState.LockedUntilChapter2Clear : UnlockState.LockedUntilBossClear);
            // A building's pieces only open its village station; the workshop items follow their own prerequisite tree
            // (user decision 2026-11-13), so only an unlock's own openAtPieces waits for pieces.
            if (PalettePieces < unlock.openAtPieces) return new UnlockStatus(unlock, UnlockState.LockedUntilPieces);
            if (!Catalog.PriceKnown(unlock)) return new UnlockStatus(unlock, UnlockState.PricePending);
            var missing = (unlock.prerequisites ?? Array.Empty<string>()).FirstOrDefault(p => !profile.unlocks.Contains(p));
            if (missing != null) return new UnlockStatus(unlock, UnlockState.MissingPrerequisite, missing);
            if (profile.pigment < unlock.cost) return new UnlockStatus(unlock, UnlockState.NotEnoughPigment);
            return new UnlockStatus(unlock, UnlockState.Available);
        }

        // Pigment payment, unlock and save happen together; repeated clicks or stale callbacks find the unlock owned.
        public bool TryPurchase(string unlockId, out UnlockState result)
        {
            var status = StatusOf(unlockId);
            result = status.State;
            if (!status.CanBuy) return false;
            int cost = status.Unlock.cost;
            bool ok = Commit(p => { p.pigment -= cost; p.unlocks = p.unlocks.Concat(new[] { unlockId }).ToArray(); });
            result = ok ? UnlockState.Owned : status.State;
            return ok;
        }

        // One kill grants pigment once per run, even if a development reset revives the same enemy.
        public int AwardPigment(Guid runId, string killKey, string role, bool training)
        {
            if (training || runId == Guid.Empty || string.IsNullOrEmpty(killKey)) return 0;
            int amount = Catalog.PigmentFor(role);
            if (amount <= 0) return 0;
            if (!awardedKills.TryGetValue(runId, out var kills)) awardedKills[runId] = kills = new HashSet<string>();
            if (!kills.Add(killKey)) return 0;
            profile.pigment += amount; pigmentDirty = true; Snapshot = new ProfileSnapshot(profile);
            Changed?.Invoke();
            return amount;
        }

        public bool HasUnsavedPigment => pigmentDirty;

        // Persists pigment gathered since the last save. Failures keep the amount in memory for the next attempt.
        public bool FlushPigment()
        {
            if (!pigmentDirty) return true;
            var next = profile.Clone(); next.revision = profile.revision + 1;
            if (!TrySave(next)) return false;
            profile = next; pigmentDirty = false; Snapshot = new ProfileSnapshot(profile);
            return true;
        }

        public RunStartPlan PlanRun(IReadOnlyList<string> blueprints, bool training = false)
        {
            var available = Catalog.Data.startLoadout.availableBlueprints;
            if (blueprints == null || blueprints.Count != Catalog.Data.startLoadout.selectedCount || blueprints.Distinct().Count() != blueprints.Count || !blueprints.All(available.Contains))
                throw new ArgumentException("Choose two different basic blueprints.");
            return BuildRunPlan(blueprints, training);
        }

        // Village departures no longer grant blueprints. The same permanent upgrades still apply.
        public RunStartPlan PlanUnarmedRun(bool training = false) => BuildRunPlan(Array.Empty<string>(), training);

        RunStartPlan BuildRunPlan(IReadOnlyList<string> blueprints, bool training)
        {
            bool Owns(string id) => profile.unlocks.Contains(id);
            int slots = Math.Max(Catalog.BaseSlots, Catalog.Highest(ProgressionIds.PaletteSlotCount, Owns, e => e.value));
            bool Wild(int weapon) => Catalog.Unlocks.Any(u => u.effect.kind == ProgressionIds.FirstSlotWildcard && u.effect.weaponSlot == weapon && Owns(u.id));
            int Perk(string kind) => Catalog.Highest(kind, Owns, e => e.value);
            var perks = new RunPerkSettings(Perk(ProgressionIds.SharedRerollCount), Perk(ProgressionIds.RecoveryExtraUses), Perk(ProgressionIds.ReviveCount),
                Catalog.Unlocks.Where(u => u.effect.kind == ProgressionIds.ReviveCount && Owns(u.id)).Select(u => u.effect.healthPercent).DefaultIfEmpty(50).Max(),
                Perk(ProgressionIds.WeaponBreakGold), Perk(ProgressionIds.ShopExtraItems), Perk(ProgressionIds.StartingRewardCount), Perk(ProgressionIds.KillGoldBonusPercent),
                Perk(ProgressionIds.StartingInkRewardCount));
            var plan = new RunStartPlan(profile.profileId, NewId(), blueprints.ToArray(), slots, Wild(1), Wild(2),
                Catalog.Highest(ProgressionIds.StartingGoldBonus, Owns, e => e.amount), Catalog.Highest(ProgressionIds.DestroyedInkGoldBonus, Owns, e => e.amount),
                Catalog.Highest(ProgressionIds.AttackBonusPercent, Owns, e => e.value), Catalog.Highest(ProgressionIds.MaxHealthBonus, Owns, e => e.value), training,
                Catalog.Highest(ProgressionIds.BagSlotCount, Owns, e => e.value), Catalog.Highest(ProgressionIds.MoveSpeedBonusPercent, Owns, e => e.value),
                Math.Max(ProgressionIds.BaseDashCharges, Catalog.Highest(ProgressionIds.DashCharges, Owns, e => e.value)), Catalog.LockedInkParts(Owns), perks, Catalog.LockedAugments(Owns));
            if (!training) Commit(p => p.runsStarted++);
            return plan;
        }

        // Leaving chapter 1 through the boss exit: opens the village unlocks and counts one chapter-1 clear per run,
        // without settling the run that continues into chapter 2.
        public bool RecordChapterOneClear(Guid runId, bool training)
        {
            if (training || runId == Guid.Empty || chapterOneCleared.Contains(runId) || profile.settledRuns.Contains(runId.ToString())) return false;
            if (!Commit(p => { p.chapter1BossFirstClear = true; p.chapter1Clears++; p.chaptersCleared = Math.Max(p.chaptersCleared, 1); })) return false;
            chapterOneCleared.Add(runId);
            return true;
        }

        // Settles a finished run once: records the first chapter-1 boss clear and the run count. Pigment is already saved.
        // A cleared run passed chapter 1; it counts here only if leaving chapter 1 was not already recorded.
        // chapter: the chapter whose boss ended a cleared run (1 when chapter 1's exit brought it home, 2 for chapter 2).
        public bool SettleRun(Guid runId, RunOutcome outcome, bool training, int chapter = 1)
        {
            if (training || runId == Guid.Empty) return false;
            string id = runId.ToString();
            if (profile.settledRuns.Contains(id)) return false;
            return Commit(p =>
            {
                if (outcome == RunOutcome.ChapterCleared)
                {
                    p.chapter1BossFirstClear = true; p.runsCleared++; if (!chapterOneCleared.Contains(runId)) p.chapter1Clears++;
                    p.chaptersCleared = Math.Max(p.chaptersCleared, Math.Max(1, chapter));
                }
                else p.runsDied++;
                p.settledRuns = p.settledRuns.Concat(new[] { id }).Skip(Math.Max(0, p.settledRuns.Length + 1 - SettledRunMemory)).ToArray();
            });
        }

        // Starts a fresh profile (new id, no pigment, unlocks or story records). The recovery backup is also replaced with the fresh profile.
        public bool ResetProfile()
        {
            if (busy) return false;
            var fresh = ProfileData.CreateNew(NewId().ToString());
            fresh.revision = profile.revision + 1;
            if (!TrySave(fresh, reset: true)) return false;
            profile = fresh; pigmentDirty = false; awardedKills.Clear(); chapterOneCleared.Clear(); Snapshot = new ProfileSnapshot(profile);
            Changed?.Invoke();
            return true;
        }

        // A finished sky path climb; the newest SkyParkourRecordLimit are kept.
        public bool RecordSkyParkour(SkyParkourRecord record)
        {
            if (record == null || record.seconds < 0 || record.falls < 0 || record.hits < 0) return false;
            return Commit(p => p.skyParkourRecords = new[] { record.Clone() }.Concat(p.skyParkourRecords ?? Array.Empty<SkyParkourRecord>())
                .Take(ProfileData.SkyParkourRecordLimit).ToArray());
        }

        public bool MarkStorySeen(string storyId)
        {
            if (string.IsNullOrEmpty(storyId) || profile.seenStories.Contains(storyId)) return false;
            return Commit(p => p.seenStories = p.seenStories.Concat(new[] { storyId }).ToArray());
        }
    }
}
