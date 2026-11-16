using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class SequenceSourceAuthoringTests
    {
        string directory, path;
        SequenceSourceWindow window;
        static SequenceSourceStepDocument External(string id, string kind, string binding) => new SequenceSourceStepDocument { id = id, kind = kind, bindingId = binding, timeout = 3 };
        static SequenceSourceLeafDocument ExternalLeaf(string id, string kind, string binding) => new SequenceSourceLeafDocument { id = id, kind = kind, bindingId = binding, timeout = 3 };
        public static SequenceSourceDocument Fixture() => new SequenceSourceDocument {
            schemaVersion = 1, id = "fixture.sequence.source", requiredAnchors = new[] { SequenceSourcePreview.EntryAnchor, SequenceSourcePreview.GoalAnchor, SequenceSourcePreview.FocusAnchor },
            hasSkip = true, skip = new SequenceSourceSkipDocument { endpointId = SequenceSourcePreview.SkipEndpointId, timeout = 3 }, steps = new[] {
                External("dialogue", "Dialogue", SequenceSourcePreview.DialogueBindingId),
                new SequenceSourceStepDocument { id = "parallel", kind = "ParallelAll", children = new[] {
                    ExternalLeaf("cue", "Cue", SequenceSourcePreview.CueBindingId), new SequenceSourceLeafDocument { id = "camera", kind = "Camera", bindingId = SequenceSourcePreview.CameraBindingId, duration = .25 },
                    ExternalLeaf("fade", "Fade", SequenceSourcePreview.FadeBindingId), ExternalLeaf("stage", "StageMove", SequenceSourcePreview.StageBindingId)
                } },
                new SequenceSourceStepDocument { id = "tail", kind = "Wait", duration = .125 }
            }
        };
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "deviant-sequence-source-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "sequence.json"); File.WriteAllText(path, JsonUtility.ToJson(Fixture(), true));
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (window) window.Close(); window = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test] public void EveryExistingKindUsesSemanticFactoriesAndPreservesDefinitionHash()
        {
            var document = Fixture(); var source = SequenceSourceCompiler.Compile(document);
            var existing = new SequenceDefinition(document.id, new[] {
                SequenceStep.Dialogue("dialogue", SequenceSourcePreview.DialogueBindingId, 3), SequenceStep.ParallelAll("parallel",
                    SequenceStep.Cue("cue", SequenceSourcePreview.CueBindingId, 3), SequenceStep.Camera("camera", SequenceSourcePreview.CameraBindingId, .25),
                    SequenceStep.Fade("fade", SequenceSourcePreview.FadeBindingId, 3), SequenceStep.StageMove("stage", SequenceSourcePreview.StageBindingId, 3)), SequenceStep.Wait("tail", .125)
            }, document.requiredAnchors, new SequenceSkipDefinition(SequenceSourcePreview.SkipEndpointId, 3));
            Assert.That(source.ContentHash, Is.EqualTo(existing.ContentHash));
            document.steps[1].children[0].bindingId = "changed"; document.requiredAnchors[0] = "changed";
            Assert.That(source.Definition.Steps[1].Children[0].BindingId, Is.EqualTo(SequenceSourcePreview.CueBindingId));
            var reloaded = SequenceSourceJson.Compile(JsonUtility.ToJson(source.ToDocument())); Assert.That(reloaded.ContentHash, Is.EqualTo(source.ContentHash));
            var reordered = source.ToDocument(); Array.Reverse(reordered.requiredAnchors); Assert.That(SequenceSourceCompiler.Compile(reordered).ContentHash, Is.EqualTo(source.ContentHash));
            Array.Reverse(reordered.steps[1].children); Assert.That(SequenceSourceCompiler.Compile(reordered).ContentHash, Is.Not.EqualTo(source.ContentHash));
            var noSkip = source.ToDocument(); noSkip.hasSkip = false; noSkip.skip = null;
            Assert.That(SequenceSourceJson.Compile(JsonUtility.ToJson(noSkip)).Definition.Skip, Is.Null, "Disabled optional skip must survive Unity JSON null-object serialization.");
        }
        [Test] public void FlatParallelLeafSchemaSerializesJsonAndEditorUndoWithoutRecursiveFields()
        {
            Assert.That(typeof(SequenceSourceLeafDocument).GetField("children").FieldType, Is.EqualTo(typeof(SequenceSourceNestedDocument[])));
            Assert.That(typeof(SequenceSourceNestedDocument).GetField("children"), Is.Null);
            Assert.That(typeof(SequenceSourceStepDocument).GetField("children").FieldType, Is.EqualTo(typeof(SequenceSourceLeafDocument[])));
            var source = SequenceSourceCompiler.Compile(Fixture());
            var draft = ScriptableObject.CreateInstance<SequenceSourceDraft>(); draft.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                draft.document = source.ToDocument();
                string json = JsonUtility.ToJson(draft.document, true);
                var fresh = SequenceSourceJson.Compile(json); Assert.That(fresh.ContentHash, Is.EqualTo(source.ContentHash));
                Assert.That(fresh.Definition.Steps[1].Children.Count, Is.EqualTo(4));
                Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "Edit flat sequence child");
                draft.document.steps[1].children[1].duration = .5; Undo.FlushUndoRecordObjects();
                Assert.That(SequenceSourceCompiler.Compile(draft.document).ContentHash, Is.Not.EqualTo(source.ContentHash));
                Undo.PerformUndo(); Assert.That(SequenceSourceCompiler.Compile(draft.document).ContentHash, Is.EqualTo(source.ContentHash));
                var nested = SequenceSourceJson.ReadDocument(json); nested.steps[1].children[0].kind = "ParallelAll";
                Assert.Throws<ArgumentException>(() => SequenceSourceCompiler.Compile(nested));
                var hiddenChildren = source.ToDocument(); hiddenChildren.steps[1].children[0].children = new[] { new SequenceSourceNestedDocument { kind = "Wait" } };
                Assert.Throws<ArgumentException>(() => SequenceSourceJson.Compile(JsonUtility.ToJson(hiddenChildren)));
                const string deepJson = "{\"schemaVersion\":1,\"id\":\"fixture.deep\",\"requiredAnchors\":[],\"steps\":[{\"id\":\"group\",\"kind\":\"ParallelAll\",\"children\":[{\"id\":\"leaf\",\"kind\":\"Wait\",\"duration\":1,\"children\":[{\"kind\":\"Wait\",\"children\":[{}]}]}]}]}";
                Assert.Throws<ArgumentException>(() => SequenceSourceJson.Compile(deepJson));
                LogAssert.NoUnexpectedReceived();
            }
            finally { Undo.ClearUndo(draft); Object.DestroyImmediate(draft); }
        }
        [Test] public void InvalidSchemaKindsFieldsIdsTimesGroupsAndAnchorsRejectWholeSource()
        {
            foreach (Action<SequenceSourceDocument> mutate in new Action<SequenceSourceDocument>[] {
                d => d.schemaVersion = 0, d => d.steps[0].kind = "dialogue", d => d.steps[0].kind = "123", d => d.steps = Array.Empty<SequenceSourceStepDocument>(),
                d => d.steps[0].bindingId = "", d => d.steps[0].timeout = 0, d => d.steps[0].duration = 1, d => d.steps[2].timeout = 1,
                d => d.steps[1].children = Array.Empty<SequenceSourceLeafDocument>(), d => d.steps[1].children[0].kind = "ParallelAll",
                d => d.steps[1].children[0].id = "dialogue", d => d.steps[2].id = "dialogue", d => d.requiredAnchors = null,
                d => d.requiredAnchors[1] = d.requiredAnchors[0], d => d.skip.timeout = 0, d => d.skip.endpointId = "", d => d.steps[2].duration = double.NaN,
                d => d.hasSkip = false, d => d.skip = null
            })
            { var document = Fixture(); mutate(document); Assert.Catch<ArgumentException>(() => SequenceSourceCompiler.Compile(document)); }
            Assert.Throws<ArgumentException>(() => SequenceSourceJson.Compile("{\"schemaVersion\":99}"));
        }
        [Test] public void AtomicSaveReloadGeneratedTextAssetAndHashConflictPreserveSources()
        {
            var original = SequenceSource.Load(path); var candidate = original.Compiled.ToDocument(); candidate.steps[2].duration = .5;
            var saved = SequenceSource.Save(original, candidate); var fresh = SequenceSource.Load(path);
            Assert.That(fresh.FileHash, Is.EqualTo(saved.FileHash)); Assert.That(fresh.Compiled.ContentHash, Is.EqualTo(saved.Compiled.ContentHash));
            Assert.That(SequenceSource.Save(fresh, fresh.Compiled.ToDocument()).FileHash, Is.EqualTo(fresh.FileHash));
            string generated = Path.Combine(directory, "generated.json"); SequenceSource.Export(fresh, generated); Assert.That(File.ReadAllBytes(generated), Is.EqualTo(File.ReadAllBytes(path)));
            var asset = new TextAsset(File.ReadAllText(generated)); try { Assert.That(SequenceSourceJson.Compile(asset).ContentHash, Is.EqualTo(saved.Compiled.ContentHash)); } finally { Object.DestroyImmediate(asset); }
            string before = File.ReadAllText(path); candidate.steps[1].children[0].id = "dialogue"; Assert.Throws<ArgumentException>(() => SequenceSource.Save(fresh, candidate)); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            File.AppendAllText(path, "\n"); string external = File.ReadAllText(path); Assert.Throws<IOException>(() => SequenceSource.Save(fresh, fresh.Compiled.ToDocument())); Assert.That(File.ReadAllText(path), Is.EqualTo(external));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void ActualHostPinsExecutionAcceptsNextVersionAndRejectsInvalidOrMissingReferences()
        {
            var owner = new GameObject("SequenceSourceHostFixture"); var host = owner.AddComponent<SequenceHost>(); var clock = new Clock(); var bindings = new WaitBindings();
            var source = SequenceSourceCompiler.Compile(new SequenceSourceDocument { schemaVersion = 1, id = "fixture.wait", requiredAnchors = Array.Empty<string>(), steps = new[] { new SequenceSourceStepDocument { id = "hold", kind = "Wait", duration = 1 } } });
            using (var controls = new ControlSession())
            try
            {
                Assert.That(host.TryConfigureSource(new SequenceSourceBinding(source, bindings, controls, new SequenceControlPolicy(InputChannels.None, false), clock), out var error), Is.True, error);
                Assert.That(host.TryStartSource(out error), Is.True, error); var first = host.Current; var oldId = first.Read().ExecutionId;
                var document = source.ToDocument(); document.steps[0].duration = 2; var next = SequenceSourceCompiler.Compile(document);
                bool reentered = false; bindings.OnValidate = () => reentered = host.TryStartSource(out _);
                Assert.That(host.TryApplySource(next, out error), Is.True, error); Assert.That(reentered, Is.False); bindings.OnValidate = null; Assert.That(first.Read().DefinitionVersion, Is.EqualTo(source.ContentHash));
                var missing = source.ToDocument(); missing.requiredAnchors = new[] { "missing" }; Assert.That(host.TryApplySource(SequenceSourceCompiler.Compile(missing), out _), Is.False);
                var badAsset = new TextAsset("{\"schemaVersion\":99}"); try { Assert.That(host.TryApplySource(badAsset, out _), Is.False); } finally { Object.DestroyImmediate(badAsset); }
                Assert.That(host.SourceBinding.Source.ContentHash, Is.EqualTo(next.ContentHash)); clock.Time = 1; host.Refresh(); Assert.That(first.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                Assert.That(host.TryStartSource(out error), Is.True, error); Assert.That(host.Current.Read().DefinitionVersion, Is.EqualTo(next.ContentHash)); Assert.That(host.Current.TryCancel(oldId, out _), Is.False);
                clock.Time = 2; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Running)); clock.Time = 3; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed));
            }
            finally { host.Close(); Object.DestroyImmediate(owner); }
        }
        [Test] public void PrivateActualBindingsExecuteAllKindsCleanupSkipAndPreserveStartVersion()
        {
            var source = SequenceSourceCompiler.Compile(Fixture());
            using (var preview = new SequenceSourcePreview(source))
            {
                Assert.That(preview.TryStart(out var error), Is.True, error); var active = preview.Host.Current;
                var document = source.ToDocument(); document.steps[2].duration = .5; var next = SequenceSourceCompiler.Compile(document);
                Assert.That(preview.Host.TryApplySource(next, out error), Is.True, error); Assert.That(active.Read().DefinitionVersion, Is.EqualTo(source.ContentHash));
                Assert.That(active.Read().StepId, Is.EqualTo("dialogue")); preview.SelectDialogue(0); preview.AdvanceDialogue();
                Assert.That(preview.CommandCount, Is.EqualTo(1)); Assert.That(active.Read().StepId, Is.EqualTo("parallel")); Assert.That(preview.StageOwned, Is.True);
                preview.Advance(.25); Assert.That(preview.ActiveCues, Is.Zero); preview.Advance(.25); Assert.That(active.Read().StepId, Is.EqualTo("tail"));
                Assert.That(preview.Host.TryApplySource(next, out _), Is.False, "Retained stage output is not stolen or bypassed by candidate preflight."); preview.Advance(.125);
                Assert.That(active.Read().Status, Is.EqualTo(SequenceStatus.Completed)); Assert.That(preview.StageOwned, Is.False); Assert.That(preview.FadeRequests, Is.Zero);
                Assert.That(preview.TryStart(out error), Is.True, error); Assert.That(preview.Host.Current.Read().DefinitionVersion, Is.EqualTo(next.ContentHash)); preview.Skip();
                Assert.That(preview.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Skipped)); Assert.That(preview.SkipCount, Is.EqualTo(1)); Assert.That(preview.CommandCount, Is.EqualTo(1), "Skip never executes dialogue choices.");
                Assert.That(preview.ControlState.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(preview.ControlState.SimulationPaused, Is.False);
            }
        }
        [Test] public void CapturedSpriteOutputsStayInExplicitPrivateRootAndRootLossNeverFallsBack()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("SequenceExplicitPrivateCueRoot");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var texture = new Texture2D(2, 2); var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2);
            var definition = new PresentationCueDefinition("fixture.sequence.private-root-cue", new[] { "fixture.frame" }, new[] { 1f }, new[] { 0f }, new[] { 0f });
            using (var factory = new Deviant.Presentation.Unity.SpriteCueFactory(outputRoot: root.transform))
            try
            {
                var catalog = new Deviant.Presentation.Unity.SequenceCueCatalog(factory);
                catalog.Publish(Deviant.Presentation.Unity.SequenceCueMedia.Sprite(definition, new[] { sprite }));
                var captured = catalog.Capture(new[] { definition.Id });
                Deviant.Presentation.Contracts.ICueView output = null;
                captured.Begin(definition, new Deviant.Presentation.Contracts.CueAnchor(1, 2, 1), result => output = result.View);
                Assert.That(output, Is.Not.Null); Assert.That(root.transform.childCount, Is.EqualTo(1));
                var child = root.transform.GetChild(0).gameObject;
                Assert.That(child.scene, Is.EqualTo(scene)); Assert.That(child.activeSelf, Is.False);
                output.Sample(0); Assert.That(child.GetComponent<SpriteRenderer>().sprite, Is.SameAs(sprite));
                output.Dispose(); Assert.That(!child || !child.activeSelf, Is.True);
                Object.DestroyImmediate(root);
                Assert.Throws<InvalidOperationException>(() => catalog.Capture(new[] { definition.Id }));
                Assert.Throws<InvalidOperationException>(() => captured.Begin(definition, new Deviant.Presentation.Contracts.CueAnchor(0, 0, 1), result => { }));
                Assert.That(GameObject.Find(definition.Id), Is.Null, "Lost explicit output roots must never redirect output into the active gameplay scene.");
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowEditsOrderGroupsUndoSaveReloadAndRealPreview()
        {
            window = ScriptableObject.CreateInstance<SequenceSourceWindow>(); window.Show(); window.CreateGUI(); window.LoadFile(path); yield return null;
            var root = window.rootVisualElement; string before = File.ReadAllText(path);
            root.Q<DoubleField>("step-2-duration").value = .5; Assert.That(root.Q<Label>("state").text, Does.Contain("Unsaved")); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null; Assert.That(window.Document.steps[2].duration, Is.EqualTo(.125));
            root.Q<DoubleField>("step-2-duration").value = .5; Click("save"); Click("reload"); Assert.That(SequenceSource.Load(path).Compiled.ContentHash, Is.EqualTo(window.CompileCandidate().ContentHash));
            Click("startPreview"); Assert.That(window.Preview.Texture.IsCreated(), Is.True); Assert.That(window.Preview.StageTexture.IsCreated(), Is.True);
            Click("choosePreview"); Click("advanceDialogue"); window.Preview.Advance(.5); window.Preview.Advance(.5); Assert.That(window.Preview.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed));
            root.Q<TextField>("step-1-child-0-binding").value = "missing"; string accepted = window.Preview.Host.SourceBinding.Source.ContentHash;
            Assert.Throws<InvalidOperationException>(() => window.ApplyPreview()); Assert.That(window.Preview.Host.SourceBinding.Source.ContentHash, Is.EqualTo(accepted));
            root.Q<TextField>("step-1-child-0-binding").value = SequenceSourcePreview.CueBindingId; root.Q<DoubleField>("step-2-duration").value = 1; Click("applyPreview"); Click("nextPreview");
            Assert.That(window.Preview.Host.Current.Read().DefinitionVersion, Is.EqualTo(window.CompileCandidate().ContentHash)); Click("cancelPreview"); Assert.That(window.Preview.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Cancelled));
        }
        [UnityTest] public IEnumerator GameSourcePreviewDoesNotMutateRun() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabSourcePreviewDoesNotMutateRun() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation())
            {
                float now = arena.Now; var position = arena.Player.Position; int hits = arena.Player.ReceivedHitCount; float health = arena.enemies[0].health; long revision = arena.Controls.Current.Revision;
                using (var preview = new SequenceSourcePreview(SequenceSourceCompiler.Compile(Fixture())))
                {
                    Assert.That(preview.TryStart(out var error), Is.True, error); preview.SelectDialogue(0); preview.AdvanceDialogue(); preview.Advance(.5); preview.Advance(.125);
                    Assert.That(preview.Host.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed)); Assert.That(preview.CommandCount, Is.EqualTo(1));
                    window = ScriptableObject.CreateInstance<SequenceSourceWindow>(); window.Show(); window.CreateGUI(); window.LoadFile(path); window.BindRuntime(preview.Host);
                    window.rootVisualElement.Q<DoubleField>("step-2-duration").value = .5; Click("applyNext"); Assert.That(preview.TryStart(out error), Is.True, error);
                    Assert.That(preview.Host.Current.Read().DefinitionVersion, Is.EqualTo(window.CompileCandidate().ContentHash)); preview.Cancel();
                }
                Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Player.ReceivedHitCount, Is.EqualTo(hits));
                Assert.That(arena.enemies[0].health, Is.EqualTo(health)); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision));
            }
        }
        void Click(string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button, Is.Not.Null, name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class WaitBindings : ISequenceBindings
        {
            public Action OnValidate;
            public bool Validate(SequenceDefinition definition, out string error) { OnValidate?.Invoke(); error = definition.RequiredAnchors.Count == 0 ? null : "missing reference"; return error == null; }
            public ISequenceBindingScope OpenScope(Guid execution, SequenceDefinition definition) => new Scope();
            sealed class Scope : ISequenceBindingScope { public bool IsActive { get; private set; } = true; public ISequenceOperation Begin(SequenceStep step) => throw new InvalidOperationException(); public ISequenceOperation BeginSkip(SequenceSkipDefinition skip) => throw new InvalidOperationException(); public void Dispose() { IsActive = false; } }
        }
    }
}
