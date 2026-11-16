using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.CombatLab.Contracts
{
    // Explicit reset configuration for the existing Lab, not a running-world save.
    public sealed class ExperimentBox
    {
        public string Key { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public bool OneWay { get; }
        public bool Pit { get; }
        public int Layer { get; }
        public ExperimentBox(string key, float x, float y, float width, float height, bool oneWay, bool pit, int layer)
        {
            Key = ExperimentHash.Key(key); X = ExperimentHash.Finite(x); Y = ExperimentHash.Finite(y);
            Width = ExperimentHash.Positive(width); Height = ExperimentHash.Positive(height);
            if (layer < 0 || layer > 31 || pit && oneWay) throw new ArgumentException("Invalid Lab box role or layer.");
            OneWay = oneWay; Pit = pit; Layer = layer;
        }
        internal void Write(BinaryWriter writer)
        { writer.Write(Key); writer.Write(X); writer.Write(Y); writer.Write(Width); writer.Write(Height); writer.Write(OneWay); writer.Write(Pit); writer.Write(Layer); }
    }

    public sealed class ExperimentGeometry
    {
        public const string LabScene = "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity";
        public const string Kind = "lab-resolved-static-boxes-v1";
        public IReadOnlyList<ExperimentBox> Boxes { get; }
        public string ContentHash { get; }
        public ExperimentGeometry(IEnumerable<ExperimentBox> boxes)
        {
            var copy = boxes?.OrderBy(box => box?.Key, StringComparer.Ordinal).ToArray();
            if (copy == null || copy.Length == 0 || copy.Length > 256 || copy.Any(box => box == null) || copy.Select(box => box.Key).Distinct(StringComparer.Ordinal).Count() != copy.Length || !copy.Any(box => !box.Pit))
                throw new ArgumentException("The Lab requires 1..256 uniquely keyed resolved boxes, including a surface.");
            Boxes = Array.AsReadOnly(copy);
            ContentHash = ExperimentHash.Compute(writer => { writer.Write(Kind); writer.Write(LabScene); writer.Write(copy.Length); foreach (var box in copy) box.Write(writer); });
        }
    }

    public sealed class ExperimentActorInitial
    {
        public string Key { get; }
        public int Order { get; }
        public int Profile { get; }
        public bool IsPlayer => Profile == -1;
        public float X { get; }
        public float Y { get; }
        public float SpawnX { get; }
        public float SpawnY { get; }
        public int Facing { get; }
        public float Maximum { get; }
        public float Health { get; }
        public float Shield { get; }
        public float GuardSeconds { get; }
        public float GuardReduction { get; }
        public bool Aggressive { get; }
        public bool IsBoss { get; }
        public float Width { get; }
        public float Height { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float InactiveKnockSpeed { get; }
        public ExperimentActorInitial(string key, int order, int profile, float x, float y, float spawnX, float spawnY, int facing,
            float maximum, float health, float shield, float guardSeconds, float guardReduction, bool aggressive, bool isBoss,
            float width, float height, float offsetX, float offsetY, float inactiveKnockSpeed)
        {
            Key = ExperimentHash.Key(key);
            if (order < 0 || order > 3 || profile < -1 || profile > 2 || facing != -1 && facing != 1 || profile == -1 && (aggressive || isBoss)) throw new ArgumentException("Unsupported Lab actor fixture configuration.");
            Order = order; Profile = profile; Facing = facing;
            X = ExperimentHash.Finite(x); Y = ExperimentHash.Finite(y); SpawnX = ExperimentHash.Finite(spawnX); SpawnY = ExperimentHash.Finite(spawnY);
            Maximum = ExperimentHash.Positive(maximum); Health = ExperimentHash.Nonnegative(health); Shield = ExperimentHash.Nonnegative(shield);
            GuardSeconds = ExperimentHash.Nonnegative(guardSeconds); GuardReduction = ExperimentHash.Nonnegative(guardReduction);
            if (Health > Maximum || GuardReduction > 1 || Shield > 0 && GuardSeconds <= 0) throw new ArgumentException("Initial vitals must be an explicit valid reset configuration.");
            Aggressive = aggressive; IsBoss = isBoss; Width = ExperimentHash.Positive(width); Height = ExperimentHash.Positive(height);
            OffsetX = ExperimentHash.Finite(offsetX); OffsetY = ExperimentHash.Finite(offsetY);
            InactiveKnockSpeed = ExperimentHash.Finite(inactiveKnockSpeed);
        }
        internal void Write(BinaryWriter writer)
        {
            writer.Write(Key); writer.Write(Order); writer.Write(Profile); writer.Write(X); writer.Write(Y); writer.Write(SpawnX); writer.Write(SpawnY); writer.Write(Facing);
            writer.Write(Maximum); writer.Write(Health); writer.Write(Shield); writer.Write(GuardSeconds); writer.Write(GuardReduction); writer.Write(Aggressive); writer.Write(IsBoss);
            writer.Write(Width); writer.Write(Height); writer.Write(OffsetX); writer.Write(OffsetY); writer.Write(InactiveKnockSpeed);
        }
    }

    public sealed class ExperimentWeaponInitial
    {
        public string FormId { get; }
        public string SpecialId { get; }
        public IReadOnlyList<int> StatSlots { get; }
        public ExperimentWeaponInitial(string formId, string specialId, IEnumerable<int> statSlots)
        {
            FormId = ExperimentHash.Key(formId); SpecialId = ExperimentHash.Key(specialId);
            var copy = statSlots?.ToArray();
            if (copy == null || copy.Length != 5 || copy.Any(value => value < 0 || value > 4)) throw new ArgumentException("Exactly five supported stat ink slots are required.");
            StatSlots = Array.AsReadOnly(copy);
        }
        internal void Write(BinaryWriter writer)
        { writer.Write(FormId); writer.Write(SpecialId); foreach (int slot in StatSlots) writer.Write(slot); }
    }

    public sealed class ExperimentVersions
    {
        public string CatalogHash { get; }
        public string MediaHash { get; }
        public LocomotionProfile Movement { get; }
        public IReadOnlyList<BasicMeleeDefinition> Basics { get; }
        public BasicMeleeDefinition FixedSword { get; }
        public FixedAttackMotion Motion { get; }
        public MeleeFrameDefinition Frame { get; }
        public IReadOnlyList<ActionTrackDefinition> ActionTracks { get; }
        public IReadOnlyList<DirectMeleeSpecialDefinition> DirectSpecials { get; }
        public IReadOnlyList<ChargedMeleeDefinition> ChargedSpecials { get; }
        public IReadOnlyList<AuthoredSkillDefinition> AuthoredSkills { get; }
        public RangedActionCatalog RangedActions { get; }
        public MotionSourceRegistry MotionRegistry { get; }
        public MeleeFrameCatalog MeleeFrames { get; }
        public ActionSpecialFrameCatalog SpecialFrames { get; }
        public string MotionBindingHash => SpecialFrames.BindingHash(MotionRegistry, Motion, MeleeFrames);
        public string ContentHash { get; }
        public ExperimentVersions(string catalogHash, string mediaHash, LocomotionProfile movement, IEnumerable<BasicMeleeDefinition> basics,
            FixedAttackMotion motion, MeleeFrameDefinition frame, IEnumerable<ActionTrackDefinition> actionTracks, IEnumerable<DirectMeleeSpecialDefinition> directSpecials,
            IEnumerable<ChargedMeleeDefinition> chargedSpecials, IEnumerable<AuthoredSkillDefinition> authoredSkills,
            RangedActionCatalog rangedActions, MotionSourceRegistry motionRegistry, MeleeFrameCatalog meleeFrames, ActionSpecialFrameCatalog specialFrames)
        {
            CatalogHash = ExperimentHash.Hash(catalogHash); MediaHash = ExperimentHash.Hash(mediaHash);
            Movement = movement ?? throw new ArgumentNullException(nameof(movement)); Motion = motion ?? throw new ArgumentNullException(nameof(motion));
            var copy = basics?.OrderBy(action => action?.Id, StringComparer.Ordinal).ToArray();
            if (copy == null || copy.Length != 17 || copy.Any(action => action == null) || copy.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("The complete 17 existing Lab melee basic definitions are required.");
            FixedSword = copy.SingleOrDefault(action => action.Id == FixedAttackMotion.FirstSwordAction) ?? throw new ArgumentException("The fixed first sword definition is missing.");
            if (frame != null && (frame.SourceAction.ContentHash != FixedSword.ContentHash || frame.SourceMotion.ContentHash != motion.ContentHash)) throw new ArgumentException("Frame timing requires its captured fixed action and motion.");
            var tracks = actionTracks?.OrderBy(action => action?.Id, StringComparer.Ordinal).ToArray();
            if (tracks == null || tracks.Length != ActionTrackDefinition.SupportedIds.Count || tracks.Any(action => action == null) ||
                !tracks.Select(action => action.Id).SequenceEqual(ActionTrackDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Every supported finite action definition is required exactly once.");
            var direct = directSpecials?.OrderBy(action => action?.Id, StringComparer.Ordinal).ToArray();
            if (direct == null || direct.Length != DirectMeleeSpecialDefinition.SupportedIds.Count || direct.Any(action => action == null) ||
                !direct.Select(action => action.Id).SequenceEqual(DirectMeleeSpecialDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Every supported direct melee special definition is required exactly once.");
            DirectSpecials = Array.AsReadOnly(direct);
            var charged = chargedSpecials?.OrderBy(action => action?.Id, StringComparer.Ordinal).ToArray();
            if (charged == null || charged.Length != ChargedMeleeDefinition.SupportedIds.Count || charged.Any(action => action == null) ||
                !charged.Select(action => action.Id).SequenceEqual(ChargedMeleeDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Every supported charged melee definition is required exactly once.");
            ChargedSpecials = Array.AsReadOnly(charged);
            var authored = authoredSkills?.OrderBy(action => action?.Id, StringComparer.Ordinal).ToArray();
            if (authored == null || authored.Length != AuthoredSkillDefinition.SupportedIds.Count || authored.Any(action => action == null) ||
                !authored.Select(action => action.Id).SequenceEqual(AuthoredSkillDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Every supported authored skill definition is required exactly once.");
            AuthoredSkills = Array.AsReadOnly(authored);
            RangedActions = rangedActions ?? throw new ArgumentNullException(nameof(rangedActions));
            MotionRegistry = motionRegistry ?? throw new ArgumentNullException(nameof(motionRegistry));
            MeleeFrames = meleeFrames ?? throw new ArgumentNullException(nameof(meleeFrames));
            if (MeleeFrames.Actions.Count != 20 || copy.Any(action => MeleeFrames.Find(new MotionActionKey(action.Id, MotionActionKind.Basic))?.NumericAction.ContentHash != action.ContentHash) ||
                direct.Any(action => MeleeFrames.Find(new MotionActionKey(action.Id, MotionActionKind.Special))?.NumericDirect.ContentHash != action.ContentHash))
                throw new ArgumentException("The complete melee frame state must match all captured 17 basic and three direct numeric definitions.");
            SpecialFrames = specialFrames ?? throw new ArgumentNullException(nameof(specialFrames));
            if (SpecialFrames.Actions.Count != ActionTrackDefinition.SupportedIds.Count + AuthoredSkillDefinition.SupportedIds.Count || tracks.Any(action => SpecialFrames.Find(action.Id)?.NumericFinite?.ContentHash != action.ContentHash) ||
                authored.Any(action => SpecialFrames.Find(action.Id)?.NumericAuthored?.ContentHash != action.ContentHash))
                throw new ArgumentException("The complete special frame state must match all captured original four finite and nineteen authored numeric definitions.");
            if (MotionRegistry.Motion(FixedAttackMotion.FirstSwordId)?.DelegatedSource != FixedBodyMotion.FirstSourcePath)
                throw new ArgumentException("The motion registry must delegate first-sword timing to the separately captured canonical source.");
            var firstPalette = MotionRegistry.Palette(FixedAttackMotion.FirstSwordId);
            if (firstPalette == null || Motion.Frames.Any(frameId => !firstPalette.Frames.Contains(frameId)))
                throw new ArgumentException("The captured first-sword source must use the registry's delegated known media palette.");
            Basics = Array.AsReadOnly(copy); Frame = frame; ActionTracks = Array.AsReadOnly(tracks);
            ContentHash = ExperimentHash.Compute(writer => {
                writer.Write("lab-versions-v7"); writer.Write(CatalogHash); writer.Write(MediaHash); writer.Write(Movement.ContentHash);
                foreach (var action in copy) { writer.Write(action.Id); writer.Write(action.ContentHash); }
                writer.Write(Motion.ContentHash); writer.Write(frame?.ContentHash ?? "");
                writer.Write(tracks.Length);
                foreach (var action in tracks) { writer.Write(action.Id); writer.Write(action.SourceVersion); writer.Write(action.ContentHash); }
                writer.Write(direct.Length);
                foreach (var action in direct) { writer.Write(action.Id); writer.Write(action.FormId); writer.Write(action.SourceVersion); writer.Write(action.ContentHash); }
                writer.Write(charged.Length);
                foreach (var action in charged) { writer.Write(action.Id); writer.Write(action.FormId); writer.Write(action.SourceVersion); writer.Write(action.ContentHash); }
                writer.Write(authored.Length);
                foreach (var action in authored) { writer.Write(action.Id); writer.Write(action.FormId); writer.Write(action.SourceVersion); writer.Write(action.ContentHash); }
                writer.Write(RangedActions.Actions.Count);
                foreach (var action in RangedActions.Actions.OrderBy(action => action.Id, StringComparer.Ordinal))
                { writer.Write(action.Id); writer.Write(action.FormId); writer.Write(action.Selection); writer.Write(action.IsBasic); writer.Write(action.SourceVersion); writer.Write(action.ContentHash); }
                writer.Write(MotionBindingHash);
                writer.Write(MeleeFrames.ContentHash);
                writer.Write(SpecialFrames.ContentHash);
            });
        }
    }

    public sealed class ExperimentInitialState
    {
        public const string PlayerKey = "lab/player";
        public static string EnemyKey(int index) => index == 0 ? "lab/enemy/melee-tester" : index == 1 ? "lab/enemy/ranged-tester" : index == 2 ? "lab/enemy/boss-dummy" : throw new ArgumentOutOfRangeException(nameof(index));
        public string Id { get; }
        public ExperimentRuntimeProvenance Provenance { get; }
        public ExperimentGeometry Geometry { get; }
        public IReadOnlyList<ExperimentActorInitial> Actors { get; }
        public IReadOnlyList<ExperimentWeaponInitial> Weapons { get; }
        public int SelectedWeapon { get; }
        public ExperimentVersions Versions { get; }
        public int? Seed { get; }
        public string SeedProvenance { get; }
        public string ContentHash { get; }
        public ExperimentInitialState(string id, ExperimentRuntimeProvenance provenance, ExperimentGeometry geometry, IEnumerable<ExperimentActorInitial> actors,
            IEnumerable<ExperimentWeaponInitial> weapons, int selectedWeapon, ExperimentVersions versions, int? seed = null, string seedProvenance = "")
        {
            Id = ExperimentHash.Key(id); Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance)); Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            Versions = versions ?? throw new ArgumentNullException(nameof(versions));
            var actorCopy = actors?.OrderBy(actor => actor?.Order).ToArray(); var weaponCopy = weapons?.ToArray();
            if (actorCopy == null || actorCopy.Length != 4 || actorCopy.Any(actor => actor == null)) throw new ArgumentException("The explicit player and three Lab enemy fixture keys are required.");
            for (int i = 0; i < actorCopy.Length; i++)
                if (actorCopy[i].Order != i || actorCopy[i].Profile != i - 1 || actorCopy[i].Key != (i == 0 ? PlayerKey : EnemyKey(i - 1))) throw new ArgumentException("Actor fixture key, profile or ordering does not match the fixed Lab.");
            if (weaponCopy == null || weaponCopy.Length != 2 || weaponCopy.Any(weapon => weapon == null) || selectedWeapon < 0 || selectedWeapon > 1) throw new ArgumentException("Both occupied weapon configurations and an active slot are required.");
            if (seed.HasValue ? string.IsNullOrWhiteSpace(seedProvenance) : !string.IsNullOrEmpty(seedProvenance)) throw new ArgumentException("An optional seed is provenance only and requires its explicit origin; no RNG is created or seeded.");
            Seed = seed; SeedProvenance = seed.HasValue ? ExperimentHash.Key(seedProvenance) : "";
            Actors = Array.AsReadOnly(actorCopy); Weapons = Array.AsReadOnly(weaponCopy); SelectedWeapon = selectedWeapon;
            ContentHash = ExperimentHash.Compute(writer => {
                writer.Write("lab-initial-v2"); writer.Write(Id); writer.Write(Provenance.ContentHash); writer.Write(Geometry.ContentHash); writer.Write(Versions.ContentHash);
                foreach (var actor in actorCopy) actor.Write(writer); writer.Write(SelectedWeapon); foreach (var weapon in weaponCopy) weapon.Write(writer);
                writer.Write(Seed.HasValue); if (Seed.HasValue) writer.Write(Seed.Value); writer.Write(SeedProvenance);
            });
        }
    }

    public sealed class ExperimentScenario
    {
        public const int MaximumTicks = 4096;
        public const float MinimumStep = .0001f, MaximumStep = 1;
        public ExperimentInitialState Initial { get; }
        public CombatInputRecording Recording { get; }
        public string ContentHash { get; }
        public ExperimentScenario(ExperimentInitialState initial, CombatInputRecording recording)
        {
            Initial = initial ?? throw new ArgumentNullException(nameof(initial)); Recording = recording ?? throw new ArgumentNullException(nameof(recording));
            if (recording.ScenarioId != initial.Id || recording.TickCount > MaximumTicks || recording.StepSeconds < MinimumStep || recording.StepSeconds > MaximumStep)
                throw new ArgumentException("A Lab scenario requires matching identity, 1..4096 input ticks and a step in [0.0001,1] seconds.");
            ContentHash = ExperimentHash.Compute(writer => { writer.Write("lab-experiment-v1"); writer.Write(initial.ContentHash); writer.Write(recording.ContentHash); });
        }
    }

    static class ExperimentHash
    {
        internal static string Key(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 512 && value.IndexOfAny(new[] { '\r', '\n', '\0' }) < 0 ? value : throw new ArgumentException("A bounded, single-line stable key is required.");
        internal static float Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) ? value == 0 ? 0 : value : throw new ArgumentException("Scenario numbers must be finite.");
        internal static float Nonnegative(float value) => Finite(value) >= 0 ? value : throw new ArgumentException("Scenario values must be nonnegative.");
        internal static float Positive(float value) => Finite(value) > 0 ? value : throw new ArgumentException("Scenario dimensions and maximum health must be positive.");
        internal static string Hash(string value) => value != null && value.Length == 64 && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f') ? value : throw new ArgumentException("A canonical SHA-256 content hash is required.");
        internal static string Compute(Action<BinaryWriter> write)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) write(writer);
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
