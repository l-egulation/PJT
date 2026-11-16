using System;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    // Canonical Editor-only reusable Lab reset fixture, never a Run/world save.
    public static class ExperimentScenarioSource
    {
        public const int SchemaVersion = 9;
        public const int MaximumFileBytes = 1024 * 1024;
        public const string Kind = "CombatLabExperimentScenario";
        [Serializable] sealed class FileData
        {
            public int schemaVersion;
            public string kind, contentHash;
            public InitialData initial;
            public InputData recording;
        }
        [Serializable] sealed class InitialData
        {
            public string id, scenePath, geometryKind, resetPolicy, contentHash, geometryHash;
            public ProvenanceData provenance;
            public BoxData[] boxes;
            public ActorData[] actors;
            public WeaponData[] weapons;
            public int selectedWeapon;
            public bool hasSeed;
            public int seed;
            public string seedProvenance;
            public VersionData versions;
        }
        [Serializable] sealed class ProvenanceData
        {
            public string kind, unityVersion, platform, processorType, codeHash, queryHash, contentHash;
            public int pointerBits;
            public ModuleData[] modules;
            public QueryData queries;
        }
        [Serializable] sealed class ModuleData { public string name, mvid; }
        [Serializable] sealed class QueryData
        {
            public bool queriesHitTriggers, queriesStartInColliders, autoSyncTransforms;
            public float defaultContactOffset;
        }
        [Serializable] sealed class BoxData { public string key; public float x, y, width, height; public bool oneWay, pit; public int layer; }
        [Serializable] sealed class ActorData
        {
            public string key;
            public int order, profile, facing;
            public float x, y, spawnX, spawnY, maximum, health, shield, guardSeconds, guardReduction, width, height, offsetX, offsetY, inactiveKnockSpeed;
            public bool aggressive, isBoss;
        }
        [Serializable] sealed class WeaponData { public string formId, specialId; public int[] statSlots; }
        [Serializable] sealed class VersionData
        {
            public string catalogHash, mediaHash, contentHash, movementHash, motionHash, frameHash;
            public LocomotionData movement;
            public BasicDataRecord[] basics;
            public AttackMotionDocument motion;
            public FrameData frame;
            public ActionData[] actionTracks;
            public DirectData[] directSpecials;
            public ChargedData[] chargedSpecials;
            public AuthoredData[] authoredSkills;
            public RangedActionSnapshotData[] rangedActions;
            public MotionRegistryDocument motionRegistry;
            public string motionRegistryHash, motionBindingHash;
            public MeleeFrameCatalogDocument meleeFrames;
            public string meleeFrameHash;
            public SpecialFrameCatalogDocument specialFrames;
            public string specialFrameHash;
        }
        [Serializable] sealed class BasicDataRecord { public string id, contentHash; public BasicData values; }
        [Serializable] sealed class ActionData { public string id, sourceVersion, contentHash; public SkillData values; }
        [Serializable] sealed class DirectData { public string id, formId, sourceVersion, contentHash; public SkillData values; }
        [Serializable] sealed class ChargedData { public string id, formId, sourceVersion, contentHash; public SkillData values; }
        [Serializable] sealed class AuthoredData { public string id, formId, sourceVersion, contentHash; public SkillData values; }
        [Serializable] sealed class FrameData { public StepData[] frames; public MarkerData[] markers; }
        [Serializable] sealed class StepData { public string id; public float seconds; }
        [Serializable] sealed class MarkerData { public string id, reference; public int anchor; public float offset; }
        [Serializable] sealed class InputData { public string scenarioId, contentHash; public float stepSeconds; public SampleData[] inputs; }
        [Serializable] sealed class SampleData { public int axis, pressed, released; public bool specialHeld; public int climbAxis, heldSpecials; }

        public static ExperimentScenario Load(string path, Catalog catalog, out string fileHash) => Deserialize(SourceFiles.Read(path, out fileHash, MaximumFileBytes), catalog);
        public static string Save(string path, string expectedFileHash, ExperimentScenario scenario, Catalog catalog)
        {
            string json = Serialize(scenario);
            CheckCatalog(scenario.Initial.Versions.CatalogHash, catalog);
            if (expectedFileHash == null) return SourceFiles.Create(path, json);
            string previous = SourceFiles.Read(path, out var actual, MaximumFileBytes);
            if (actual != expectedFileHash) throw new IOException("Scenario changed outside this window. Reload before saving; no file was overwritten.");
            Deserialize(previous, catalog); // An unrelated or incompatible existing destination is never overwritten.
            return SourceFiles.Save(path, expectedFileHash, json, MaximumFileBytes);
        }
        public static string Serialize(ExperimentScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            return Json(new FileData { schemaVersion = SchemaVersion, kind = Kind, contentHash = scenario.ContentHash,
                initial = Encode(scenario.Initial), recording = Encode(scenario.Recording) });
        }
        public static ExperimentScenario Deserialize(string json, Catalog catalog)
        {
            ValidateText(json);
            var data = JsonUtility.FromJson<FileData>(json);
            if (data == null || data.schemaVersion != SchemaVersion || data.kind != Kind) throw new ArgumentException("Unsupported Lab scenario kind or schema.");
            var scenario = new ExperimentScenario(Decode(data.initial, catalog), Decode(data.recording));
            Match(data.contentHash, scenario.ContentHash, "scenario"); return scenario;
        }
        // EditorWindow state survives a domain reload without inventing input ticks.
        public static string SerializeInitial(ExperimentInitialState initial) => Json(Encode(initial ?? throw new ArgumentNullException(nameof(initial))));
        public static ExperimentInitialState DeserializeInitial(string json, Catalog catalog) { ValidateText(json); return Decode(JsonUtility.FromJson<InitialData>(json), catalog); }
        public static string SerializeVersions(ExperimentVersions versions) => Json(Encode(versions ?? throw new ArgumentNullException(nameof(versions))));
        public static ExperimentVersions DeserializeVersions(string json, Catalog catalog) { ValidateText(json); return Decode(JsonUtility.FromJson<VersionData>(json), catalog); }
        static string Json(object value)
        {
            string json = JsonUtility.ToJson(value, true) + "\n"; ValidateText(json); return json;
        }
        static void ValidateText(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new ArgumentException("A Lab scenario document must be nonempty and at most 1MiB.");
        }
        static InitialData Encode(ExperimentInitialState initial) => new InitialData {
            id = initial.Id, provenance = Encode(initial.Provenance), scenePath = ExperimentGeometry.LabScene, geometryKind = ExperimentGeometry.Kind,
            resetPolicy = "technical-reset-initial-config-v1", contentHash = initial.ContentHash, geometryHash = initial.Geometry.ContentHash,
            boxes = initial.Geometry.Boxes.Select(box => new BoxData { key = box.Key, x = box.X, y = box.Y, width = box.Width, height = box.Height, oneWay = box.OneWay, pit = box.Pit, layer = box.Layer }).ToArray(),
            actors = initial.Actors.Select(actor => new ActorData { key = actor.Key, order = actor.Order, profile = actor.Profile, x = actor.X, y = actor.Y,
                spawnX = actor.SpawnX, spawnY = actor.SpawnY, facing = actor.Facing, maximum = actor.Maximum, health = actor.Health, shield = actor.Shield,
                guardSeconds = actor.GuardSeconds, guardReduction = actor.GuardReduction, aggressive = actor.Aggressive, isBoss = actor.IsBoss,
                width = actor.Width, height = actor.Height, offsetX = actor.OffsetX, offsetY = actor.OffsetY, inactiveKnockSpeed = actor.InactiveKnockSpeed }).ToArray(),
            weapons = initial.Weapons.Select(weapon => new WeaponData { formId = weapon.FormId, specialId = weapon.SpecialId, statSlots = weapon.StatSlots.ToArray() }).ToArray(),
            selectedWeapon = initial.SelectedWeapon, hasSeed = initial.Seed.HasValue, seed = initial.Seed ?? 0, seedProvenance = initial.SeedProvenance, versions = Encode(initial.Versions)
        };
        static ProvenanceData Encode(ExperimentRuntimeProvenance provenance) => new ProvenanceData {
            kind = ExperimentRuntimeProvenance.Kind, unityVersion = provenance.UnityVersion, platform = provenance.Platform,
            processorType = provenance.ProcessorType, pointerBits = provenance.PointerBits,
            modules = provenance.Modules.Select(module => new ModuleData { name = module.Name, mvid = module.ModuleVersionId.ToString("D") }).ToArray(),
            queries = new QueryData { queriesHitTriggers = provenance.Queries.QueriesHitTriggers, queriesStartInColliders = provenance.Queries.QueriesStartInColliders,
                autoSyncTransforms = provenance.Queries.AutoSyncTransforms, defaultContactOffset = provenance.Queries.DefaultContactOffset },
            codeHash = provenance.CodeHash, queryHash = provenance.Queries.ContentHash, contentHash = provenance.ContentHash
        };
        static ExperimentRuntimeProvenance Decode(ProvenanceData data)
        {
            if (data == null || data.kind != ExperimentRuntimeProvenance.Kind || data.modules == null || data.modules.Length == 0 || data.modules.Any(module => module == null) || data.queries == null)
                throw new ArgumentException("The complete loaded Editor code and consumed query provenance is required.");
            var modules = data.modules.Select(module => {
                if (!Guid.TryParseExact(module.mvid, "D", out var mvid)) throw new ArgumentException("A real canonical module MVID is required.");
                return new ExperimentCodeModule(module.name, mvid);
            });
            var queries = new ExperimentQuerySettings(data.queries.queriesHitTriggers, data.queries.queriesStartInColliders, data.queries.autoSyncTransforms, data.queries.defaultContactOffset);
            var provenance = new ExperimentRuntimeProvenance(data.unityVersion, data.platform, data.processorType, data.pointerBits, modules, queries);
            Match(data.codeHash, provenance.CodeHash, "loaded code"); Match(data.queryHash, queries.ContentHash, "physics query settings");
            Match(data.contentHash, provenance.ContentHash, "runtime provenance"); return provenance;
        }
        static VersionData Encode(ExperimentVersions versions) => new VersionData {
            catalogHash = versions.CatalogHash, mediaHash = versions.MediaHash, contentHash = versions.ContentHash, movementHash = versions.Movement.ContentHash,
            movement = versions.Movement.ToData(), motionHash = versions.Motion.ContentHash, motion = versions.Motion.ToDocument(), frameHash = versions.Frame?.ContentHash ?? "",
            basics = versions.Basics.Select(action => new BasicDataRecord { id = action.Id, contentHash = action.ContentHash, values = action.ToData() }).ToArray(),
            frame = versions.Frame == null ? null : new FrameData {
                frames = versions.Frame.Timeline.Frames.Select(frame => new StepData { id = frame.Id, seconds = frame.Seconds }).ToArray(),
                markers = versions.Frame.Timeline.Markers.OrderBy(marker => marker.Id, StringComparer.Ordinal).Select(marker => new MarkerData { id = marker.Id, anchor = (int)marker.Anchor, reference = marker.Reference, offset = marker.Offset }).ToArray()
            },
            actionTracks = versions.ActionTracks.Select(action => new ActionData { id = action.Id, sourceVersion = action.SourceVersion, contentHash = action.ContentHash, values = action.ToData() }).ToArray(),
            directSpecials = versions.DirectSpecials.Select(action => new DirectData { id = action.Id, formId = action.FormId, sourceVersion = action.SourceVersion, contentHash = action.ContentHash, values = action.ToData() }).ToArray(),
            chargedSpecials = versions.ChargedSpecials.Select(action => new ChargedData { id = action.Id, formId = action.FormId, sourceVersion = action.SourceVersion, contentHash = action.ContentHash, values = action.ToData() }).ToArray(),
            authoredSkills = versions.AuthoredSkills.Select(action => new AuthoredData { id = action.Id, formId = action.FormId, sourceVersion = action.SourceVersion, contentHash = action.ContentHash, values = action.ToData() }).ToArray(),
            rangedActions = versions.RangedActions.ToSnapshot(), motionRegistry = versions.MotionRegistry.ToDocument(), motionRegistryHash = versions.MotionRegistry.ContentHash, motionBindingHash = versions.MotionBindingHash,
            meleeFrames = versions.MeleeFrames.ToDocument(), meleeFrameHash = versions.MeleeFrames.ContentHash,
            specialFrames = versions.SpecialFrames.ToDocument(), specialFrameHash = versions.SpecialFrames.ContentHash
        };
        static InputData Encode(CombatInputRecording recording) => new InputData {
            scenarioId = recording.ScenarioId, contentHash = recording.ContentHash, stepSeconds = recording.StepSeconds,
            inputs = recording.Inputs.Select(input => new SampleData { axis = input.Axis, pressed = (int)input.Pressed, released = (int)input.Released, specialHeld = input.SpecialHeld, climbAxis = input.ClimbAxis, heldSpecials = (int)input.HeldSpecials }).ToArray()
        };
        static ExperimentInitialState Decode(InitialData data, Catalog catalog)
        {
            if (data == null || data.boxes == null || data.actors == null || data.weapons == null || data.boxes.Any(box => box == null) || data.actors.Any(actor => actor == null) || data.weapons.Any(weapon => weapon == null))
                throw new ArgumentException("The complete Lab geometry, actor and weapon reset configuration is required.");
            if (!data.hasSeed && data.seed != 0) throw new ArgumentException("A seed value must explicitly declare its provenance.");
            if (data.scenePath != ExperimentGeometry.LabScene || data.geometryKind != ExperimentGeometry.Kind || data.resetPolicy != "technical-reset-initial-config-v1")
                throw new ArgumentException("Only the explicit static-box Lab and technical reset policy are supported.");
            var geometry = new ExperimentGeometry(data.boxes.Select(box => new ExperimentBox(box.key, box.x, box.y, box.width, box.height, box.oneWay, box.pit, box.layer)));
            Match(data.geometryHash, geometry.ContentHash, "geometry");
            var actors = data.actors.Select(actor => new ExperimentActorInitial(actor.key, actor.order, actor.profile, actor.x, actor.y, actor.spawnX, actor.spawnY, actor.facing,
                actor.maximum, actor.health, actor.shield, actor.guardSeconds, actor.guardReduction, actor.aggressive, actor.isBoss, actor.width, actor.height, actor.offsetX, actor.offsetY, actor.inactiveKnockSpeed));
            var initial = new ExperimentInitialState(data.id, Decode(data.provenance), geometry, actors, data.weapons.Select(weapon => new ExperimentWeaponInitial(weapon.formId, weapon.specialId, weapon.statSlots)),
                data.selectedWeapon, Decode(data.versions, catalog), data.hasSeed ? data.seed : (int?)null, data.seedProvenance);
            Match(data.contentHash, initial.ContentHash, "initial state"); return initial;
        }
        static ExperimentVersions Decode(VersionData data, Catalog catalog)
        {
            if (data == null || data.basics == null || data.basics.Length != 17 || data.basics.Any(action => action == null) ||
                data.actionTracks == null || data.actionTracks.Length != ActionTrackDefinition.SupportedIds.Count || data.actionTracks.Any(action => action == null) ||
                !data.actionTracks.Select(action => action.id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(ActionTrackDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Every supported Lab definition version is required exactly once.");
            if (data.directSpecials == null || data.directSpecials.Length != DirectMeleeSpecialDefinition.SupportedIds.Count || data.directSpecials.Any(action => action == null) ||
                !data.directSpecials.Select(action => action.id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(DirectMeleeSpecialDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Every supported direct melee special snapshot is required exactly once.");
            if (data.chargedSpecials == null || data.chargedSpecials.Length != ChargedMeleeDefinition.SupportedIds.Count || data.chargedSpecials.Any(action => action == null) ||
                !data.chargedSpecials.Select(action => action.id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(ChargedMeleeDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Every supported charged melee snapshot is required exactly once.");
            CheckCatalog(data.catalogHash, catalog); // Canonical identity context validates before any snapshot compiler or runtime mutation.
            if (data.authoredSkills == null || data.authoredSkills.Length != AuthoredSkillDefinition.SupportedIds.Count || data.authoredSkills.Any(action => action == null) ||
                !data.authoredSkills.Select(action => action.id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(AuthoredSkillDefinition.SupportedIds.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Every supported authored skill snapshot is required exactly once.");
            if (!LocomotionProfile.TryCreate(data.movement, out var movement, out var error)) throw new ArgumentException(error);
            Match(data.movementHash, movement.ContentHash, "locomotion");
            var basics = data.basics.Select(action => {
                if (!BasicMeleeDefinition.TryCreate(action.id, action.values, out var compiled, out var reason)) throw new ArgumentException(reason);
                Match(action.contentHash, compiled.ContentHash, "basic action"); return compiled;
            }).ToArray();
            var motion = new FixedAttackMotion(data.motion); Match(data.motionHash, motion.ContentHash, "fixed motion");
            MeleeFrameDefinition frame = null;
            if (!string.IsNullOrEmpty(data.frameHash))
            {
                if (data.frame?.frames == null || data.frame.markers == null || data.frame.frames.Any(step => step == null) || data.frame.markers.Any(marker => marker == null)) throw new ArgumentException("Complete frame timing is required for frame mode.");
                var timeline = new FrameTimeline(data.frame.frames.Select(step => new TimelineFrame(step.id, step.seconds)),
                    data.frame.markers.Select(marker => new TimelineMarker(marker.id, (TimelineAnchor)marker.anchor, marker.reference, marker.offset)));
                frame = MeleeFrameCompiler.CompileFirst(basics.Single(action => action.Id == FixedAttackMotion.FirstSwordAction), motion, timeline,
                    catalog.forms.Single(form => form.id == "base_sword").inputSpeed);
                Match(data.frameHash, frame.ContentHash, "frame timing");
            }
            else if (data.frame != null && (data.frame.frames?.Length > 0 || data.frame.markers?.Length > 0)) throw new ArgumentException("Fixed mode cannot contain hidden frame timing.");
            var tracks = data.actionTracks.Select(action => {
                var compiled = new ActionTrackDefinition(action.values);
                if (compiled.Id != action.id) throw new ArgumentException("Sword special identity does not match its source.");
                Match(action.sourceVersion, compiled.SourceVersion, "sword special metadata"); Match(action.contentHash, compiled.ContentHash, "sword special tracks"); return compiled;
            }).ToArray();
            var direct = data.directSpecials.Select(action => {
                if (!DirectMeleeSpecialDefinition.TryCreate(action.formId, action.values, out var compiled, out var directError)) throw new ArgumentException(directError);
                if (compiled.Id != action.id) throw new ArgumentException("Direct melee special identity does not match its complete source snapshot.");
                Match(action.sourceVersion, compiled.SourceVersion, "direct special policy"); Match(action.contentHash, compiled.ContentHash, "direct special values"); return compiled;
            }).ToArray();
            var charged = data.chargedSpecials.Select(action => {
                if (!ChargedMeleeDefinition.TryCreate(action.formId, action.values, out var compiled, out var chargedError)) throw new ArgumentException(chargedError);
                if (compiled.Id != action.id) throw new ArgumentException("Charged melee identity does not match its complete source snapshot.");
                Match(action.sourceVersion, compiled.SourceVersion, "charged special policy"); Match(action.contentHash, compiled.ContentHash, "charged special values"); return compiled;
            }).ToArray();
            var authored = data.authoredSkills.Select(action => {
                if (!AuthoredSkillDefinition.TryCreate(action.formId, action.values, out var compiled, out var authoredError)) throw new ArgumentException(authoredError);
                if (compiled.Id != action.id) throw new ArgumentException("Authored skill identity does not match its complete source snapshot.");
                Match(action.sourceVersion, compiled.SourceVersion, "authored skill policy"); Match(action.contentHash, compiled.ContentHash, "authored skill values"); return compiled;
            }).ToArray();
            var ranged = RangedActionCatalog.FromSnapshot(data.rangedActions);
            if (!RangedActionCatalog.TryCreate(catalog, out var currentRanged, out var rangedError) || !currentRanged.TryReplace(ranged.Actions, out ranged, out rangedError))
                throw new ArgumentException(rangedError);
            var registry = new MotionSourceRegistry(data.motionRegistry, catalog); Match(data.motionRegistryHash, registry.ContentHash, "motion registry");
            var meleeFrames = new MeleeFrameCatalog(data.meleeFrames, catalog, registry, motion, frame, basics, direct);
            Match(data.meleeFrameHash, meleeFrames.ContentHash, "complete melee frame state");
            var specialFrames = new ActionSpecialFrameCatalog(data.specialFrames, catalog, registry, motion, tracks, authored);
            Match(data.specialFrameHash, specialFrames.ContentHash, "complete special frame state");
            var versions = new ExperimentVersions(data.catalogHash, data.mediaHash, movement, basics, motion, frame, tracks, direct, charged, authored, ranged, registry, meleeFrames, specialFrames);
            Match(data.motionBindingHash, versions.MotionBindingHash, "combined delegated motion binding");
            Match(data.contentHash, versions.ContentHash, "definition versions"); return versions;
        }
        static CombatInputRecording Decode(InputData data)
        {
            if (data?.inputs == null || data.inputs.Length == 0 || data.inputs.Length > ExperimentScenario.MaximumTicks || data.inputs.Any(input => input == null)) throw new ArgumentException("A scenario needs 1..4096 complete input ticks.");
            var recording = new CombatInputRecording(data.scenarioId, data.stepSeconds, data.inputs.Select(input => new CombatInput(input.axis, (CombatButtons)input.pressed, input.specialHeld, (CombatButtons)input.released, input.climbAxis, (CombatButtons)input.heldSpecials)).ToArray());
            Match(data.contentHash, recording.ContentHash, "input recording"); return recording;
        }
        static void CheckCatalog(string expectedHash, Catalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(catalog)))).Replace("-", "").ToLowerInvariant();
                Match(expectedHash, hash, "canonical catalog context");
            }
        }
        static void Match(string expected, string actual, string kind)
        { if (!string.Equals(expected, actual, StringComparison.Ordinal)) throw new ArgumentException("Lab " + kind + " content hash does not match its values."); }
    }
}
