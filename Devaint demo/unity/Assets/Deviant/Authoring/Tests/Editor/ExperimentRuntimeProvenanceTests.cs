using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using System.Globalization;
using System.Linq;
using Deviant.Combat;
using Deviant.CombatLab;
using Deviant.CombatLab.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class ExperimentRuntimeProvenanceTests
    {
        static readonly Guid First = new Guid("85c0ea98-6bcf-42bf-9028-0e3497a5edab"), Second = new Guid("6f5c7ca4-e029-4d8a-a1f5-5c18b41c0b5d");
        static ExperimentQuerySettings Queries(bool triggers = true, bool inside = true, bool sync = false, float offset = .01f) => new ExperimentQuerySettings(triggers, inside, sync, offset);
        static ExperimentRuntimeProvenance Provenance(IEnumerable<ExperimentCodeModule> modules, ExperimentQuerySettings queries = null)
            => new ExperimentRuntimeProvenance("6000.6.5f1", "WindowsEditor", "test-processor", 64, modules, queries ?? Queries());
        [Test] public void PureCanonicalCodeIdentityCopiesSortsAndIncludesEveryModuleMvidWithoutCultureDependence()
        {
            var modules = new[] { new ExperimentCodeModule("Deviant.Z", Second), new ExperimentCodeModule("Deviant.A", First) };
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); var first = Provenance(modules);
                modules[0] = new ExperimentCodeModule("Changed", Guid.NewGuid());
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                var same = Provenance(new[] { new ExperimentCodeModule("Deviant.A", First), new ExperimentCodeModule("Deviant.Z", Second) });
                Assert.That(first.ContentHash, Is.EqualTo(same.ContentHash)); Assert.That(first.CodeHash, Is.EqualTo(same.CodeHash));
                Assert.That(first.Modules.Select(module => module.Name), Is.EqualTo(new[] { "Deviant.A", "Deviant.Z" }));
                Assert.Throws<NotSupportedException>(() => ((IList<ExperimentCodeModule>)first.Modules)[0] = modules[0]);
                var changed = Provenance(new[] { new ExperimentCodeModule("Deviant.A", Second), new ExperimentCodeModule("Deviant.Z", Second) });
                Assert.That(changed.CodeHash, Is.Not.EqualTo(first.CodeHash)); Assert.That(first.Matches(changed), Is.False);
            }
            finally { CultureInfo.CurrentCulture = original; }
        }
        [Test] public void MissingModuleMetadataAndIncompleteOrInvalidSettingsNeverProduceFallbackBuildIdentity()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentCodeModule("runtime", Guid.Empty));
            Assert.Throws<ArgumentException>(() => Provenance(Array.Empty<ExperimentCodeModule>()));
            Assert.Throws<ArgumentException>(() => Provenance(new[] { new ExperimentCodeModule("runtime", First), new ExperimentCodeModule("runtime", Second) }));
            Assert.Throws<ArgumentException>(() => Queries(offset: float.NaN));
            Assert.Throws<ArgumentException>(() => Queries(offset: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExperimentRuntimeProvenance("Unity", "Editor", "CPU", 16, new[] { new ExperimentCodeModule("runtime", First) }, Queries()));
            Assert.That(typeof(ExperimentRuntimeProvenance).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name.StartsWith("UnityEngine") || assembly.Name.StartsWith("UnityEditor")), Is.False);
        }
        [Test] public void EveryConsumedQuerySettingChangesOnlyItsSettingsAndCombinedFingerprint()
        {
            var modules = new[] { new ExperimentCodeModule("runtime", First) }; var initial = Provenance(modules);
            foreach (var queries in new[] { Queries(triggers: false), Queries(inside: false), Queries(sync: true), Queries(offset: .02f) })
            {
                var changed = Provenance(modules, queries);
                Assert.That(changed.CodeHash, Is.EqualTo(initial.CodeHash)); Assert.That(changed.Queries.ContentHash, Is.Not.EqualTo(initial.Queries.ContentHash));
                Assert.That(changed.ContentHash, Is.Not.EqualTo(initial.ContentHash)); Assert.That(initial.Matches(changed), Is.False);
            }
        }
        [UnityTest] public IEnumerator ActualLabCaptureIncludesLoadedGameplayAndPhysicsModulesButNotTestOrAuthoringUiAssemblies()
        {
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var first = ExperimentRuntimeProvenanceCapture.Capture(arena); var second = ExperimentRuntimeProvenanceCapture.Capture(arena);
            Assert.That(first.ContentHash, Is.EqualTo(second.ContentHash)); Assert.That(first.Modules.All(module => module.ModuleVersionId != Guid.Empty), Is.True);
            foreach (string name in new[] { "Deviant.Combat", "Deviant.Combat.Contracts", "Deviant.Combat.Execution", "Deviant.Definitions", "Deviant.Equipment",
                "Deviant.Equipment.Contracts", "Deviant.CombatLab.Contracts", "Deviant.CombatLab.Editor", "Deviant.CombatLab.Unity", "UnityEngine.Physics2DModule" })
                Assert.That(first.Modules.Any(module => module.Name == name), Is.True, name);
            Assert.That(first.Modules.Any(module => module.Name.EndsWith(".Tests") || module.Name == "Deviant.Authoring.Editor" || module.Name == "Assembly-CSharp"), Is.False);
            Assert.That(first.Modules.Single(module => module.Name == "Deviant.Combat").ModuleVersionId, Is.EqualTo(typeof(CombatArena).Assembly.ManifestModule.ModuleVersionId));
            bool triggers = Physics2D.queriesHitTriggers, inside = Physics2D.queriesStartInColliders, sync = Physics2D.autoSyncTransforms;
            float offset = Physics2D.defaultContactOffset;
            try
            {
                Physics2D.queriesHitTriggers = !triggers;
                var changed = ExperimentRuntimeProvenanceCapture.Capture(arena);
                Assert.That(changed.CodeHash, Is.EqualTo(first.CodeHash)); Assert.That(changed.Queries.ContentHash, Is.Not.EqualTo(first.Queries.ContentHash));
                Physics2D.queriesHitTriggers = triggers; Physics2D.queriesStartInColliders = !inside;
                Assert.That(ExperimentRuntimeProvenanceCapture.Capture(arena).ContentHash, Is.Not.EqualTo(first.ContentHash));
                Physics2D.queriesStartInColliders = inside; Physics2D.autoSyncTransforms = !sync;
                Assert.That(ExperimentRuntimeProvenanceCapture.Capture(arena).ContentHash, Is.Not.EqualTo(first.ContentHash));
                Physics2D.autoSyncTransforms = sync; Physics2D.defaultContactOffset = offset * 2;
                Assert.That(ExperimentRuntimeProvenanceCapture.Capture(arena).ContentHash, Is.Not.EqualTo(first.ContentHash));
            }
            finally
            {
                Physics2D.queriesHitTriggers = triggers; Physics2D.queriesStartInColliders = inside; Physics2D.autoSyncTransforms = sync; Physics2D.defaultContactOffset = offset;
            }
            Assert.That(ExperimentRuntimeProvenanceCapture.Capture(arena).ContentHash, Is.EqualTo(first.ContentHash));
        }
        [UnityTest] public IEnumerator ActualQueryMismatchRejectsAllReplayAdmissionsBeforeLeaseResetAndPreservesPublishedState()
        {
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/provenance-actual");
            session.RecordManual(initial, .02f, new[] { default(CombatInput) });
            var selected = session.Scenario; var result = session.LastResult;
            using (var driver = arena.AcquireManualSimulation()) driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
            arena.Player.health = 42; arena.Player.SubmitInput(new CombatInput(1, CombatButtons.Jump));
            var cast = arena.Player.currentCast; var inputSession = arena.Player.InputSession; var input = arena.Player.CurrentInput;
            var position = arena.Player.Position; var velocity = arena.Player.velocity; float now = arena.Now; long tick = arena.SimulationTickCount;
            string versions = ExperimentSession.CaptureVersions(arena).ContentHash;
            int resetCount = 0; Action observe = () => resetCount++; arena.ResetCompleted += observe;
            bool triggers = Physics2D.queriesHitTriggers, inside = Physics2D.queriesStartInColliders, sync = Physics2D.autoSyncTransforms;
            float offset = Physics2D.defaultContactOffset;
            var changes = new Action[] { () => Physics2D.queriesHitTriggers = !triggers, () => Physics2D.queriesStartInColliders = !inside,
                () => Physics2D.autoSyncTransforms = !sync, () => Physics2D.defaultContactOffset = offset * 2 };
            try
            {
                foreach (var change in changes)
                {
                    change();
                    Assert.Throws<InvalidOperationException>(() => session.Select(selected));
                    Assert.Throws<InvalidOperationException>(() => session.RecordManual(initial, .02f, new[] { default(CombatInput) }));
                    Assert.Throws<InvalidOperationException>(() => session.Replay());
                    Assert.Throws<InvalidOperationException>(() => session.Compare(initial.Versions, initial.Versions));
                    Assert.That(arena.ManualSimulationActive, Is.False); Assert.That(resetCount, Is.Zero);
                    Assert.That(session.Scenario, Is.SameAs(selected)); Assert.That(session.LastResult, Is.SameAs(result));
                    Assert.That(arena.Player.currentCast, Is.SameAs(cast)); Assert.That(arena.Player.health, Is.EqualTo(42));
                    Assert.That(arena.Player.InputSession.Equals(inputSession), Is.True); Assert.That(arena.Player.CurrentInput.Pressed, Is.EqualTo(input.Pressed));
                    Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Player.velocity, Is.EqualTo(velocity));
                    Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick));
                    Assert.That(ExperimentSession.CaptureVersions(arena).ContentHash, Is.EqualTo(versions));
                    Physics2D.queriesHitTriggers = triggers; Physics2D.queriesStartInColliders = inside; Physics2D.autoSyncTransforms = sync; Physics2D.defaultContactOffset = offset;
                }
            }
            finally
            {
                arena.ResetCompleted -= observe;
                Physics2D.queriesHitTriggers = triggers; Physics2D.queriesStartInColliders = inside; Physics2D.autoSyncTransforms = sync; Physics2D.defaultContactOffset = offset;
            }
            session.Select(selected); Assert.That(session.Replay().Ticks.Count, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator DifferentCodeModuleVectorWithSameEngineStampRejectsActualSelectionAndReplay()
        {
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
            var owner = new ExperimentSession(arena); var initial = owner.CaptureInitial("lab/code-mismatch");
            owner.RecordManual(initial, .02f, new[] { default(CombatInput) }); var selected = owner.Scenario; var previous = owner.LastResult;
            var actual = initial.Provenance;
            var changedModules = actual.Modules.Select((module, index) => index == 0 ? new ExperimentCodeModule(module.Name, Guid.NewGuid()) : module);
            var changed = new ExperimentRuntimeProvenance(actual.UnityVersion, actual.Platform, actual.ProcessorType, actual.PointerBits, changedModules, actual.Queries);
            var incompatible = new ExperimentInitialState(initial.Id, changed, initial.Geometry, initial.Actors, initial.Weapons, initial.SelectedWeapon, initial.Versions);
            var scenario = new ExperimentScenario(incompatible, selected.Recording);
            string json = ExperimentScenarioSource.Serialize(scenario); var loaded = ExperimentScenarioSource.Deserialize(json, arena.catalog);
            arena.Player.health = 42; var position = arena.Player.Position; float now = arena.Now;
            Assert.Throws<InvalidOperationException>(() => owner.Select(loaded));
            Assert.Throws<InvalidOperationException>(() => owner.RecordManual(incompatible, .02f, new[] { default(CombatInput) }));
            Assert.That(owner.Scenario, Is.SameAs(selected)); Assert.That(owner.LastResult, Is.SameAs(previous));
            Assert.That(arena.ManualSimulationActive, Is.False); Assert.That(arena.Player.health, Is.EqualTo(42));
            Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Now, Is.EqualTo(now));
        }

        [UnityTest] public IEnumerator MissingRequiredProvenanceOrPriorSchemaRejectsWithoutOverwritingSource()
        {
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var session = new ExperimentSession(arena);
            var initial = session.CaptureInitial("lab/required-provenance"); session.RecordManual(initial, .02f, new[] { default(CombatInput) });
            string directory = Path.Combine(Path.GetTempPath(), "deviant-provenance-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "scenario.json"); var scenario = session.Scenario;
                ExperimentScenarioSource.Save(path, null, scenario, arena.catalog);
                string json = File.ReadAllText(path), missing = json.Replace("\"provenance\":", "\"unsupportedProvenance\":");
                Assert.That(missing, Is.Not.EqualTo(json)); Assert.Throws<ArgumentException>(() => ExperimentScenarioSource.Deserialize(missing, arena.catalog));
                string old = json.Replace("\"schemaVersion\": " + ExperimentScenarioSource.SchemaVersion, "\"schemaVersion\": 7"); Assert.That(old, Is.Not.EqualTo(json));
                Assert.Throws<ArgumentException>(() => ExperimentScenarioSource.Deserialize(old, arena.catalog));
                File.WriteAllText(path, missing); string original = File.ReadAllText(path);
                SourceFiles.Read(path, out var expected);
                Assert.Throws<ArgumentException>(() => ExperimentScenarioSource.Save(path, expected, scenario, arena.catalog));
                Assert.That(File.ReadAllText(path), Is.EqualTo(original)); Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
            }
            finally { Directory.Delete(directory, true); }
        }

        [UnityTest] public IEnumerator CapturedNumericOverridesDoNotInventACodeBuildChange()
        {
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var session = new ExperimentSession(arena); var initial = session.CaptureInitial("lab/data-only-provenance");
            var provenance = initial.Provenance; var data = arena.BasicSwordFirst.ToData(); data.damage += 1;
            Assert.That(arena.TryApplyBasicSwordFirst(data, false, out var error), Is.True, error);
            var captured = session.CaptureInitial("lab/data-only-provenance");
            Assert.That(captured.Provenance.ContentHash, Is.EqualTo(provenance.ContentHash)); Assert.That(captured.Versions.ContentHash, Is.Not.EqualTo(initial.Versions.ContentHash));
        }

        [UnityTest] public IEnumerator FreshPlayDomainUsesSameLoadedCodeFingerprintForUnchangedAssemblies()
        {
            EditorSceneManager.OpenScene(ExperimentGeometry.LabScene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            string code = ExperimentRuntimeProvenanceCapture.Capture(Object.FindAnyObjectByType<CombatArena>()).CodeHash;
            yield return new ExitPlayMode();
            yield return new EnterPlayMode(); yield return null;
            Assert.That(ExperimentRuntimeProvenanceCapture.Capture(Object.FindAnyObjectByType<CombatArena>()).CodeHash, Is.EqualTo(code));
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
