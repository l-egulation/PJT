using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Run
{
    // One finished climb of the village's sky path (docs/design/world/village/starting-village.md 「하늘길」). at: DateTime.ToBinary of the
    // local time; assisted: zone warps, flight or the practice return were used (kept, but never the best).
    [Serializable] public sealed class SkyParkourRecord
    {
        public long at; public float seconds; public int falls, hits; public bool assisted;
        public SkyParkourRecord Clone() => new SkyParkourRecord { at = at, seconds = seconds, falls = falls, hits = hits, assisted = assisted };
    }

    // Saved form of the permanent profile. Only stable content ids are stored, never UI indices or weapon instance ids.
    [Serializable] public sealed class ProfileData
    {
        public const string CurrentFormat = "deviant-profile";
        public const int CurrentVersion = 1;
        public string format = CurrentFormat;
        public int version = CurrentVersion;
        // Zero identifies a save written before sketch equipment. Kept separate from the file format version.
        public int equipmentRevision;
        public string profileId;
        public long revision;
        public long pigment;
        public int progressionRevision;
        public bool chapter1BossFirstClear;
        public string[] unlocks = Array.Empty<string>();
        // Explicit console overrides survive reload without fabricating story clears or buying prerequisites.
        public string[] debugUnlockOverrides = Array.Empty<string>();
        public string[] seenStories = Array.Empty<string>();
        // Runs whose pigment/boss settlement has been written; guards against settling the same run twice.
        public string[] settledRuns = Array.Empty<string>();
        // runsCleared counts runs that ended cleared (the last chapter, currently chapter 2); chapter1Clears counts every
        // run that beat the chapter-1 boss and left through its exit, whether or not the run went on to clear.
        public int runsStarted, runsCleared, runsDied, chapter1Clears;
        // The furthest chapter whose boss this profile has beaten (user decision 2026-11-04): each chapter's first clear
        // gives one village palette piece and ends that run at home; a run may go past it to the next chapter.
        public int chaptersCleared;
        // Sky path records, newest first, at most SkyParkourRecordLimit.
        public SkyParkourRecord[] skyParkourRecords = Array.Empty<SkyParkourRecord>();
        public const int SkyParkourRecordLimit = 50;

        public ProfileData Clone() => new ProfileData
        {
            format = format, version = version, equipmentRevision = equipmentRevision, profileId = profileId, revision = revision, pigment = pigment, progressionRevision = progressionRevision,
            chapter1BossFirstClear = chapter1BossFirstClear, unlocks = (string[])unlocks.Clone(), seenStories = (string[])seenStories.Clone(),
            debugUnlockOverrides = (string[])(debugUnlockOverrides ?? Array.Empty<string>()).Clone(),
            settledRuns = (string[])settledRuns.Clone(), runsStarted = runsStarted, runsCleared = runsCleared, runsDied = runsDied, chapter1Clears = chapter1Clears, chaptersCleared = chaptersCleared,
            skyParkourRecords = (skyParkourRecords ?? Array.Empty<SkyParkourRecord>()).Select(r => r.Clone()).ToArray()
        };

        public static ProfileData CreateNew(string profileId) => new ProfileData { profileId = profileId, equipmentRevision = 2 };

        public static bool MigrateProgression(ProfileData data, ProgressionCatalog catalog)
        {
            if (data == null || data.progressionRevision >= catalog.Data.revision || data.unlocks == null ||
                data.unlocks.Distinct().Count() != data.unlocks.Length) return false;
            var retired = (catalog.Data.retiredUnlocks ?? Array.Empty<RetiredProgressionUnlock>()).ToDictionary(u => u.id);
            var freeOverrides = new HashSet<string>(data.debugUnlockOverrides ?? Array.Empty<string>());
            long refund = data.unlocks.Where(id => retired.ContainsKey(id) && !freeOverrides.Contains(id)).Sum(id => (long)retired[id].refund);
            data.pigment = checked(data.pigment + refund);
            data.unlocks = data.unlocks.Where(id => !retired.ContainsKey(id)).ToArray();
            data.debugUnlockOverrides = (data.debugUnlockOverrides ?? Array.Empty<string>()).Where(id => !retired.ContainsKey(id)).ToArray();
            data.progressionRevision = catalog.Data.revision;
            return true;
        }

        // first_run_settled: a run ended in a death or a return (a boss clear implies one); chapter1_boss_first_clear;
        // chapter2_boss_first_clear: the chapter-2 boss was beaten once (chaptersCleared reached 2).
        public bool Reached(string gate) =>
            gate == ProgressionIds.FirstRunGate ? runsDied + runsCleared > 0 || chapter1BossFirstClear
            : gate == ProgressionIds.BossGate ? chapter1BossFirstClear
            : gate == ProgressionIds.Chapter2Gate && chaptersCleared >= 2;

        // Profiles saved before a tier gained a lower tier (starting_gold_100 before starting_gold_50) own the lower
        // tiers of the same effect implicitly. Returns true when ids were added.
        public static bool FillLowerTiers(ProfileData data, ProgressionCatalog catalog)
        {
            if (data?.unlocks == null) return false;
            var owned = data.unlocks.ToList(); bool added = false;
            for (int i = 0; i < owned.Count; i++)
            {
                if ((data.debugUnlockOverrides ?? Array.Empty<string>()).Contains(owned[i])) continue;
                var unlock = catalog.Find(owned[i]);
                foreach (var lower in (unlock?.prerequisites ?? Array.Empty<string>()).Select(catalog.Find))
                    if (lower != null && lower.effect.kind == unlock.effect.kind && !owned.Contains(lower.id)) { owned.Add(lower.id); added = true; }
            }
            if (added) data.unlocks = owned.ToArray();
            return added;
        }

        public static List<string> Validate(ProfileData data, ProgressionCatalog catalog)
        {
            var errors = new List<string>();
            void Need(bool ok, string message) { if (!ok) errors.Add(message); }
            if (data == null) { errors.Add("profile is missing"); return errors; }
            Need(data.format == CurrentFormat, "unknown profile format " + data.format);
            Need(data.version == CurrentVersion, "unsupported profile version " + data.version);
            Need(Guid.TryParse(data.profileId, out var id) && id != Guid.Empty, "profile id must be a GUID");
            Need(data.revision >= 0 && data.pigment >= 0 && data.runsStarted >= 0 && data.runsCleared >= 0 && data.runsDied >= 0 && data.chapter1Clears >= 0 && data.chaptersCleared >= 0, "negative counter");
            var unlocks = data.unlocks ?? Array.Empty<string>();
            Need(unlocks.Distinct().Count() == unlocks.Length, "duplicate unlock id");
            var overrides = data.debugUnlockOverrides ?? Array.Empty<string>();
            Need(overrides.Distinct().Count() == overrides.Length && overrides.All(id => unlocks.Contains(id) && catalog.Contains(id)), "invalid debug unlock overrides");
            foreach (var u in unlocks)
            {
                var unlock = catalog.Find(u);
                Need(unlock != null, "unknown unlock id " + u);
                if (!overrides.Contains(u) && unlock?.prerequisites != null) foreach (var pre in unlock.prerequisites) Need(unlocks.Contains(pre), u + " is missing prerequisite " + pre);
                if (unlock != null && !overrides.Contains(u)) Need(data.Reached(unlock.requiredClear), u + " exists before " + unlock.requiredClear);
            }
            var stories = data.seenStories ?? Array.Empty<string>();
            Need(stories.All(s => !string.IsNullOrEmpty(s)) && stories.Distinct().Count() == stories.Length, "invalid story ids");
            var runs = data.settledRuns ?? Array.Empty<string>();
            Need(runs.All(r => Guid.TryParse(r, out _)) && runs.Distinct().Count() == runs.Length, "invalid settled run ids");
            var sky = data.skyParkourRecords ?? Array.Empty<SkyParkourRecord>();
            Need(sky.Length <= SkyParkourRecordLimit && sky.All(r => r != null && r.seconds >= 0 && r.falls >= 0 && r.hits >= 0), "invalid sky path records");
            return errors;
        }
    }

    // Read-only view handed to screens; mutation goes through ProgressionService commands.
    public sealed class ProfileSnapshot
    {
        public string ProfileId { get; }
        public long Revision { get; }
        public long Pigment { get; }
        public bool BossFirstClear { get; }
        public bool FirstRunSettled { get; }
        public bool ArtisanWorkshopOpen => FirstRunSettled && HasSeen("artisan-first-run");
        public IReadOnlyCollection<string> Unlocks { get; }
        public IReadOnlyCollection<string> SeenStories { get; }
        public int RunsStarted { get; }
        public int RunsCleared { get; }
        public int RunsDied { get; }
        public int Chapter1Clears { get; }
        public int ChaptersCleared { get; }
        // Newest first (copies); the best is the fastest record without assistance.
        public IReadOnlyList<SkyParkourRecord> SkyParkourRecords { get; }
        public SkyParkourRecord BestSkyParkour => SkyParkourRecords.Where(r => !r.assisted).OrderBy(r => r.seconds).FirstOrDefault();
        internal ProfileSnapshot(ProfileData d)
        {
            ProfileId = d.profileId; Revision = d.revision; Pigment = d.pigment; BossFirstClear = d.chapter1BossFirstClear;
            FirstRunSettled = d.Reached(ProgressionIds.FirstRunGate);
            Unlocks = Array.AsReadOnly(d.unlocks.ToArray()); SeenStories = Array.AsReadOnly(d.seenStories.ToArray());
            RunsStarted = d.runsStarted; RunsCleared = d.runsCleared; RunsDied = d.runsDied; Chapter1Clears = d.chapter1Clears; ChaptersCleared = d.chaptersCleared;
            SkyParkourRecords = Array.AsReadOnly((d.skyParkourRecords ?? Array.Empty<SkyParkourRecord>()).Select(r => r.Clone()).ToArray());
        }
        public bool Has(string unlockId) => Unlocks.Contains(unlockId);
        public bool HasSeen(string storyId) => SeenStories.Contains(storyId);
    }
}
