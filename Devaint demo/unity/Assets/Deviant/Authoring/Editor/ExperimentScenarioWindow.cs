using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using Deviant.CombatLab.Contracts;
using Deviant.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    public sealed class ExperimentScenarioWindow : EditorWindow
    {
        [Serializable] public sealed class InputBlock
        {
            public int ticks = 1, axis, climbAxis;
            public CombatButtons pressed, released, heldSpecials;
            public bool specialHeld;
        }
        [SerializeField] CombatArena arena;
        [SerializeField] string scenarioId = "lab/my-experiment", initialJson, scenarioJson, versionAJson, versionBJson, sourcePath, sourceHash;
        [SerializeField] bool hasSeed;
        [SerializeField] int seed;
        [SerializeField] string seedProvenance = "";
        [SerializeField] float stepSeconds = .02f;
        [SerializeField] List<InputBlock> blocks = new List<InputBlock> { new InputBlock() };
        ExperimentSession session;
        CombatArena sessionArena;
        string status = "Capture Initial records an explicit reset configuration, not current actions, velocity or status effects.", results = "";
        Label statusLabel, summaryLabel, resultLabel, versionsLabel;
        VisualElement inputRows;
        public ExperimentScenario SelectedScenario => Session.Scenario;
        public string SourcePath => sourcePath;
        public string SourceHash => sourceHash;
        public ExperimentReplayResult LastResult => Session.LastResult;
        public ExperimentReplayFailure LastFailure => session?.LastFailure;

        [MenuItem("Tools/deviant/Experiments/Reusable Lab Scenario")]
        public static void Open() => GetWindow<ExperimentScenarioWindow>("Lab Scenario");
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(new Label("Reusable Lab scenario · existing CombatV2Arena only"));
            scroll.Add(new HelpBox("Captures the actual static-box terrain signature, explicit player/enemy fixture keys, placement, facing, vitals, both weapon/ink configurations and applied definitions. Replay resets transient actions, velocity, statuses and charges; it is not a Run/world save. Same Editor/runtime only. Optional seed is provenance, never an invented RNG.", HelpBoxMessageType.Info));
            var target = new ObjectField("Lab runtime") { name = "runtime", objectType = typeof(CombatArena), allowSceneObjects = true, value = arena };
            target.RegisterValueChangedCallback(change => { arena = change.newValue as CombatArena; session = null; sessionArena = null; Refresh(); }); scroll.Add(target);
            var id = new TextField("Stable scenario ID") { name = "scenarioId", value = scenarioId }; id.RegisterValueChangedCallback(change => scenarioId = change.newValue); scroll.Add(id);
            var seedToggle = new Toggle("Explicit seed provenance") { name = "hasSeed", value = hasSeed }; seedToggle.RegisterValueChangedCallback(change => hasSeed = change.newValue); scroll.Add(seedToggle);
            var seedValue = new IntegerField("Seed (not applied to RNG)") { name = "seed", value = seed }; seedValue.RegisterValueChangedCallback(change => seed = change.newValue); scroll.Add(seedValue);
            var origin = new TextField("Seed source") { name = "seedProvenance", value = seedProvenance }; origin.RegisterValueChangedCallback(change => seedProvenance = change.newValue); scroll.Add(origin);
            scroll.Add(Button("captureInitial", "Capture Initial · no reset", () => CaptureInitial()));
            summaryLabel = new Label { name = "initialSummary" }; scroll.Add(summaryLabel);
            scroll.Add(new Label("Manual selected input blocks · each block repeats the exact sampled intent, including its edges."));
            var step = new FloatField("Seconds per common tick") { name = "stepSeconds", value = stepSeconds }; step.RegisterValueChangedCallback(change => stepSeconds = change.newValue); scroll.Add(step);
            inputRows = new VisualElement { name = "inputRows" }; scroll.Add(inputRows); DrawInputRows();
            scroll.Add(Button("addInputBlock", "Add input block", () => { blocks.Add(new InputBlock()); DrawInputRows(); }));
            scroll.Add(Button("recordManual", "Record manual selected inputs · actual replay/reset", () => RecordManual()));
            scroll.Add(Button("replay", "Replay selected scenario · restore prior reset configuration", () => Replay()));
            scroll.Add(Button("captureA", "Capture applied definitions A", () => CaptureVersion(true)));
            scroll.Add(Button("captureB", "Capture applied definitions B", () => CaptureVersion(false)));
            versionsLabel = new Label { name = "versions" }; scroll.Add(versionsLabel);
            scroll.Add(Button("compare", "Replay A/B · identical initial state, terrain and input", () => Compare()));
            scroll.Add(Button("loadScenario", "Load reusable Lab scenario", LoadDialog));
            scroll.Add(Button("saveScenario", "Save reusable Lab scenario", SaveDialog));
            statusLabel = new Label { name = "status" }; scroll.Add(statusLabel);
            resultLabel = new Label { name = "results" }; scroll.Add(resultLabel);
            Refresh();
        }
        Button Button(string name, string text, Action action) => new Button(() => Run(action)) { name = name, text = text };
        void Run(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is IOException || error is UnauthorizedAccessException)
            {
                status = error.Message;
                if (session?.LastFailure != null)
                {
                    var failure = session.LastFailure;
                    results = "FAILED attempt at tick " + failure.FaultedTick + " · completed ticks " + failure.CompletedTicks +
                        " · actual command outcomes " + failure.CommandOutcomes.Count + " · accepted facts before failure " + failure.AcceptedHitFacts.Count +
                        "\nPrior successful recording/result retained; this failed attempt is not a successful replay." +
                        (failure.ObservationError == null ? "" : "\nObservation error: " + failure.ObservationError);
                }
            }
            Refresh();
        }
        void OnEnable() { EditorApplication.playModeStateChanged += PlayState; }
        void OnDisable() { EditorApplication.playModeStateChanged -= PlayState; session = null; sessionArena = null; }
        void PlayState(PlayModeStateChange change) { session = null; sessionArena = null; results = ""; Refresh(); }
        Catalog CodecCatalog
        {
            get
            {
                if (arena && arena.catalog != null) return arena.catalog;
                var generated = Resources.Load<TextAsset>("MeleeCatalog");
                if (!generated) throw new InvalidOperationException("The generated canonical catalog is required to read complete scenario snapshots.");
                return JsonUtility.FromJson<Catalog>(generated.text);
            }
        }
        ExperimentSession Session
        {
            get
            {
                if (!arena) arena = UnityEngine.Object.FindAnyObjectByType<CombatArena>();
                if (session == null || sessionArena != arena)
                {
                    var replacement = new ExperimentSession(arena);
                    if (!string.IsNullOrEmpty(scenarioJson)) replacement.Select(ExperimentScenarioSource.Deserialize(scenarioJson, CodecCatalog));
                    session = replacement; sessionArena = arena;
                }
                return session;
            }
        }
        ExperimentSession CurrentSessionWithoutSelection()
        {
            if (!arena) arena = UnityEngine.Object.FindAnyObjectByType<CombatArena>();
            return session != null && sessionArena == arena ? session : new ExperimentSession(arena);
        }
        public void CaptureInitial()
        {
            var current = CurrentSessionWithoutSelection();
            var captured = current.CaptureInitial(scenarioId, hasSeed ? seed : (int?)null, hasSeed ? seedProvenance : "");
            string capturedJson = ExperimentScenarioSource.SerializeInitial(captured);
            initialJson = capturedJson;
            status = "Initial reset configuration captured without resetting the Lab. Record selected inputs to publish a complete scenario."; results = ""; Refresh();
        }
        public void SetManualInputs(float step, params CombatInput[] inputs)
        {
            if (inputs == null || inputs.Length == 0 || inputs.Length > ExperimentScenario.MaximumTicks) throw new ArgumentException("Select 1..4096 input ticks.");
            CombatInputRecording.ValidateStep(step);
            if (step < ExperimentScenario.MinimumStep || step > ExperimentScenario.MaximumStep) throw new ArgumentOutOfRangeException(nameof(step));
            stepSeconds = step;
            blocks = CompactInputs(inputs);
            if (inputRows != null) DrawInputRows();
            rootVisualElement.Q<FloatField>("stepSeconds")?.SetValueWithoutNotify(step);
        }
        public ExperimentReplayResult RecordManual()
        {
            if (string.IsNullOrEmpty(initialJson)) throw new InvalidOperationException("Capture Initial before recording selected inputs.");
            var initial = ExperimentScenarioSource.DeserializeInitial(initialJson, CodecCatalog);
            var current = CurrentSessionWithoutSelection();
            var result = current.RecordManual(initial, stepSeconds, ExpandInputs());
            string completedJson = ExperimentScenarioSource.Serialize(current.Scenario);
            session = current; sessionArena = arena; scenarioJson = completedJson; results = Describe("Recorded", result);
            status = "Published only successfully executed contiguous common ticks. Prior reset configuration restored; no product save or source definition was written."; Refresh(); return result;
        }
        public ExperimentReplayResult Replay()
        {
            var result = Session.Replay(); results = Describe("Replay", result);
            status = "Replay complete · actual Lab retained · prior technical reset configuration restored."; Refresh(); return result;
        }
        public void CaptureVersion(bool a)
        {
            // CaptureInitial also checks explicit Lab identity/geometry without mutation.
            var captured = CurrentSessionWithoutSelection().CaptureInitial(scenarioId).Versions;
            if (a) versionAJson = ExperimentScenarioSource.SerializeVersions(captured); else versionBJson = ExperimentScenarioSource.SerializeVersions(captured);
            status = "Captured applied definitions " + (a ? "A" : "B") + "; unapplied drafts are not read."; Refresh();
        }
        public (ExperimentReplayResult A, ExperimentReplayResult B) Compare()
        {
            if (string.IsNullOrEmpty(versionAJson) || string.IsNullOrEmpty(versionBJson)) throw new InvalidOperationException("Capture both applied definition versions first.");
            var pair = Session.Compare(ExperimentScenarioSource.DeserializeVersions(versionAJson, CodecCatalog), ExperimentScenarioSource.DeserializeVersions(versionBJson, CodecCatalog));
            results = Describe("A", pair.A) + "\n" + Describe("B", pair.B) + "\nSame input " + pair.A.Scenario.Recording.ContentHash +
                "\nResource traces " + (pair.A.ResourceTraceHash == pair.B.ResourceTraceHash ? "equal" : "DIFFER") + " · command traces " + (pair.A.CommandTraceHash == pair.B.CommandTraceHash ? "equal" : "DIFFER") +
                " · causal actor identities " + (pair.A.IdentityTraceHash == pair.B.IdentityTraceHash ? "equal" : "DIFFER") +
                "\nDefinition/geometry edits may legitimately change these observations; no differing outcome is hidden as a successful action.";
            status = "A/B complete · same captured terrain, initial actors, gear and inputs · prior configuration restored."; Refresh(); return pair;
        }
        public void LoadScenario(string path)
        {
            var loaded = ExperimentScenarioSource.Load(path, CodecCatalog, out var hash);
            string loadedJson = ExperimentScenarioSource.Serialize(loaded), loadedInitialJson = ExperimentScenarioSource.SerializeInitial(loaded.Initial);
            var current = CurrentSessionWithoutSelection();
            current.Select(loaded); // Runtime geometry/content checks precede replacing the old selection or touching Reset.
            session = current; sessionArena = arena;
            hasSeed = loaded.Initial.Seed.HasValue; seed = loaded.Initial.Seed ?? 0; seedProvenance = loaded.Initial.SeedProvenance;
            blocks = CompactInputs(loaded.Recording.Inputs);
            if (inputRows != null) DrawInputRows();
            rootVisualElement.Q<Toggle>("hasSeed")?.SetValueWithoutNotify(hasSeed);
            rootVisualElement.Q<IntegerField>("seed")?.SetValueWithoutNotify(seed);
            rootVisualElement.Q<TextField>("seedProvenance")?.SetValueWithoutNotify(seedProvenance);
            scenarioJson = loadedJson; initialJson = loadedInitialJson;
            sourcePath = Path.GetFullPath(path); sourceHash = hash; scenarioId = loaded.Initial.Id; stepSeconds = loaded.Recording.StepSeconds;
            rootVisualElement.Q<TextField>("scenarioId")?.SetValueWithoutNotify(scenarioId);
            rootVisualElement.Q<FloatField>("stepSeconds")?.SetValueWithoutNotify(stepSeconds);
            results = ""; status = "Loaded and validated complete scenario without resetting or mutating the Lab."; Refresh();
        }
        public string SaveScenario(string path, string expectedFileHash)
        {
            string hash = ExperimentScenarioSource.Save(path, expectedFileHash, Session.Scenario ?? throw new InvalidOperationException("Record or load a complete scenario first."), CodecCatalog);
            sourcePath = Path.GetFullPath(path); sourceHash = hash; status = "Reusable Lab scenario saved atomically; catalog/source files and Run state were not changed."; Refresh(); return hash;
        }
        static List<InputBlock> CompactInputs(IReadOnlyList<CombatInput> inputs)
        {
            var compacted = new List<InputBlock>();
            foreach (var input in inputs)
            {
                var last = compacted.Count == 0 ? null : compacted[compacted.Count - 1];
                if (last != null && last.axis == input.Axis && last.pressed == input.Pressed && last.released == input.Released && last.specialHeld == input.SpecialHeld && last.climbAxis == input.ClimbAxis && last.heldSpecials == input.HeldSpecials) last.ticks++;
                else compacted.Add(new InputBlock { ticks = 1, axis = input.Axis, pressed = input.Pressed, released = input.Released, specialHeld = input.SpecialHeld, climbAxis = input.ClimbAxis, heldSpecials = input.HeldSpecials });
            }
            return compacted;
        }
        CombatInput[] ExpandInputs()
        {
            if (blocks == null || blocks.Count == 0) throw new ArgumentException("Select at least one input block.");
            int count = 0;
            foreach (var block in blocks)
            {
                if (block == null || block.ticks <= 0 || block.ticks > ExperimentScenario.MaximumTicks || count > ExperimentScenario.MaximumTicks - block.ticks) throw new ArgumentException("The complete input selection must contain 1..4096 ticks.");
                count += block.ticks;
            }
            var inputs = new CombatInput[count]; int index = 0;
            foreach (var block in blocks)
            {
                var input = new CombatInput(block.axis, block.pressed, block.specialHeld, block.released, block.climbAxis, block.heldSpecials);
                for (int i = 0; i < block.ticks; i++) inputs[index++] = input;
            }
            return inputs;
        }
        void DrawInputRows()
        {
            inputRows.Clear();
            if (blocks == null) blocks = new List<InputBlock>();
            for (int index = 0; index < blocks.Count; index++)
            {
                var block = blocks[index]; int rowIndex = index; var row = new VisualElement { name = "inputBlock" + index };
                var count = new IntegerField("Ticks") { value = block.ticks }; count.RegisterValueChangedCallback(change => block.ticks = change.newValue); row.Add(count);
                var axis = new IntegerField("Axis (-1/0/1)") { value = block.axis }; axis.RegisterValueChangedCallback(change => block.axis = change.newValue); row.Add(axis);
                var pressed = new EnumFlagsField("Pressed each tick", block.pressed); pressed.RegisterValueChangedCallback(change => block.pressed = (CombatButtons)change.newValue); row.Add(pressed);
                var released = new EnumFlagsField("Released each tick", block.released); released.RegisterValueChangedCallback(change => block.released = (CombatButtons)change.newValue); row.Add(released);
                var held = new Toggle("Special held") { value = block.specialHeld }; held.RegisterValueChangedCallback(change => block.specialHeld = change.newValue); row.Add(held);
                row.Add(Button("removeInputBlock" + index, "Remove block", () => { blocks.RemoveAt(rowIndex); DrawInputRows(); })); inputRows.Add(row);
            }
        }
        void Refresh()
        {
            if (statusLabel == null) return;
            statusLabel.text = status; resultLabel.text = results;
            summaryLabel.text = string.IsNullOrEmpty(initialJson) ? "No initial reset configuration captured." : InitialDescription();
            versionsLabel.text = "A " + VersionDescription(versionAJson) + " · B " + VersionDescription(versionBJson);
        }
        string InitialDescription()
        {
            try
            {
                var initial = ExperimentScenarioSource.DeserializeInitial(initialJson, CodecCatalog);
                return initial.Id + " · " + initial.Geometry.Boxes.Count + " resolved boxes · terrain " + initial.Geometry.ContentHash.Substring(0, 12) +
                    "\n4 explicitly keyed actors · both weapon/ink slots · definitions " + initial.Versions.ContentHash.Substring(0, 12) +
                    "\nAll " + initial.Versions.ActionTracks.Count + " supported finite actions: " + string.Join(", ", initial.Versions.ActionTracks.Select(action => action.Id)) +
                    "\nAll " + initial.Versions.DirectSpecials.Count + " supported direct specials: " + string.Join(", ", initial.Versions.DirectSpecials.Select(action => action.Id)) +
                    "\nAll " + initial.Versions.ChargedSpecials.Count + " supported charged specials: " + string.Join(", ", initial.Versions.ChargedSpecials.Select(action => action.Id)) +
                    "\nAll " + initial.Versions.AuthoredSkills.Count + " authored skills · all " + initial.Versions.RangedActions.Actions.Count + " ranged actions" +
                    "\nFull motion registry: " + initial.Versions.MotionRegistry.Motions.Count + " clips / " + initial.Versions.MotionRegistry.Actions.Count + " action bindings · " + initial.Versions.MotionRegistry.ContentHash.Substring(0, 12) +
                    "\nFull special FrameTime sources: " + initial.Versions.SpecialFrames.Actions.Count + " complete typed policies · " + initial.Versions.SpecialFrames.ContentHash.Substring(0, 12) +
                    (string.IsNullOrEmpty(scenarioJson) ? "\nNo published input recording." : "\nComplete reusable scenario retained.");
            }
            catch (ArgumentException error) { return error.Message; }
        }
        string VersionDescription(string json)
        {
            if (string.IsNullOrEmpty(json)) return "not captured";
            try { return ExperimentScenarioSource.DeserializeVersions(json, CodecCatalog).ContentHash.Substring(0, 12); }
            catch (ArgumentException) { return "invalid captured definition"; }
        }
        static string Describe(string label, ExperimentReplayResult result) => label + " · " + result.Ticks.Count + " successful ticks · accepted hits " + result.AcceptedHits +
            " · health lost " + result.HealthLost.ToString("0.###") + " · first hit tick " + result.FirstHitTick + " · player Δx " + result.PlayerTravelX.ToString("0.###") +
            "\nObserved state trace " + result.TraceHash + " · accepted fact trace " + result.HitTraceHash +
            " · correlated starts " + result.AcceptedHitFacts.Count(hit => hit.HasStart) + "/" + result.AcceptedHitFacts.Count +
            "\nResource trace " + result.ResourceTraceHash + " · command trace " + result.CommandTraceHash +
            "\nCausal identity trace " + result.IdentityTraceHash + " · actual source/target/reflector relations " + result.HitIdentities.Count +
            "\nActual outcomes " + result.CommandOutcomes.Count + " · completed " + result.CommandOutcomes.Count(value => value.Outcome.State == CombatCommandState.Completed) +
            " · cancelled " + result.CommandOutcomes.Count(value => value.Outcome.State == CombatCommandState.Cancelled) +
            " · rejected/failed " + result.CommandOutcomes.Count(value => value.Outcome.State == CombatCommandState.Rejected || value.Outcome.State == CombatCommandState.Failed) +
            "\nFinal charges " + DescribeResources(result.Ticks[result.Ticks.Count - 1].Resources) +
            "\nFirst accepted hit comes from committed damage facts; command outcomes retain actual reason/detail and raw keys. Slot resource values come from their existing owners." +
            (result.AcceptedHitFacts.Count == 0 ? "" : FirstAcceptedHit(result.AcceptedHitFacts[0]));
        static string DescribeResources(PlayerWeaponResourcesReadSnapshot resources) => !resources.HasLoadout ? "no player loadout" :
            "slot " + resources.ActiveSlot + " · [0] " + resources.Slot0.FormId + "/" + resources.Slot0.SpecialId + " " + resources.Slot0.Available + "/" + resources.Slot0.Capacity +
            " period " + resources.Slot0.Period.ToString("R") + " ready " + resources.Slot0.NextReady.ToString("R") +
            " · [1] " + resources.Slot1.FormId + "/" + resources.Slot1.SpecialId + " " + resources.Slot1.Available + "/" + resources.Slot1.Capacity +
            " period " + resources.Slot1.Period.ToString("R") + " ready " + resources.Slot1.NextReady.ToString("R");
        static string FirstAcceptedHit(ExperimentHitCorrelation value)
        {
            var origin = value.Hit.Origin;
            return "\nFirst fact " + (value.SourceActorKey ?? "runtime actor") + " → " + (value.TargetActorKey ?? "runtime actor") +
                " · command " + origin.Command.Sequence + "/" + origin.Command.Kind + " execution " + origin.ExecutionId +
                " hit " + origin.HitIndex + "/" + (origin.HitId ?? "legacy index") + " effect " + origin.EffectId +
                " · admitted " + value.AdmittedAt.ToString("R") + " started " + (value.HasStart ? value.Start.Now.ToString("R") : "unbound") +
                " accepted " + value.Hit.Now.ToString("R") + " · start→hit " + value.StartToHit.ToString("R") + "s";
        }
        string SourceDirectory => string.IsNullOrEmpty(sourcePath) ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs")) : Path.GetDirectoryName(sourcePath);
        void LoadDialog()
        {
            string path = EditorUtility.OpenFilePanel("Load reusable Lab scenario", SourceDirectory, "json");
            if (!string.IsNullOrEmpty(path)) LoadScenario(path);
        }
        void SaveDialog()
        {
            string path = EditorUtility.SaveFilePanel("Save reusable Lab scenario", SourceDirectory, "lab-scenario.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            string expected = !string.IsNullOrEmpty(sourcePath) && string.Equals(Path.GetFullPath(path), sourcePath, StringComparison.OrdinalIgnoreCase) ? sourceHash : null;
            if (expected == null && File.Exists(path)) ExperimentScenarioSource.Load(path, CodecCatalog, out expected);
            SaveScenario(path, expected);
        }
    }
}
