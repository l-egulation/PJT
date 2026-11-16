using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative;
using Deviant.Narrative.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class DialogueSourceAuthoringTests
    {
        const string Speaker = "fixture.source.speaker", Body = "fixture.source.body", NewBody = "fixture.source.body.next", End = "fixture.source.end";
        const string Choice = "fixture.source.choice", Alternate = "fixture.source.alternate", Condition = "fixture.source.condition", Command = "fixture.source.command";
        string directory, sourcePath, referencePath;
        readonly List<DialogueSourceWindow> windows = new List<DialogueSourceWindow>();
        static DialogueSourceDocument Document() => new DialogueSourceDocument {
            schemaVersion = 1, id = "fixture.source.dialogue", entryId = "choose", lines = new[] {
                new DialogueSourceLineDocument { id = "choose", speakerKey = Speaker, textKey = Body, choices = new[] {
                    new DialogueSourceChoiceDocument { id = "commit", textKey = Choice, nextId = "end", conditionId = Condition, commandId = Command },
                    new DialogueSourceChoiceDocument { id = "leave", textKey = Alternate, nextId = "end" }
                } },
                new DialogueSourceLineDocument { id = "end", speakerKey = Speaker, textKey = End }
            }
        };
        static DialogueReferenceCatalogDocument References(string suffix = "") => new DialogueReferenceCatalogDocument {
            schemaVersion = 1,
            texts = new[] {
                new DialogueTextDocument { key = Speaker, kind = "Speaker", text = "TECHNICAL SPEAKER" + suffix },
                new DialogueTextDocument { key = Body, kind = "Line", text = "TECHNICAL ORIGINAL LINE" + suffix },
                new DialogueTextDocument { key = NewBody, kind = "Line", text = "TECHNICAL CANDIDATE LINE" + suffix },
                new DialogueTextDocument { key = End, kind = "Line", text = "TECHNICAL END" + suffix },
                new DialogueTextDocument { key = Choice, kind = "Choice", text = "TECHNICAL COMMAND" + suffix },
                new DialogueTextDocument { key = Alternate, kind = "Choice", text = "TECHNICAL NO COMMAND" + suffix }
            }, conditionIds = new[] { Condition }, commandIds = new[] { Command }
        };
        static CompiledDialogueSource Compile(DialogueSourceDocument document = null, string suffix = "")
            => DialogueSourceCompiler.Compile(document ?? Document(), new DialogueReferenceCatalog(References(suffix)));
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "deviant-dialogue-source-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            sourcePath = Path.Combine(directory, "dialogue.json"); referencePath = Path.Combine(directory, "references.json");
            File.WriteAllText(sourcePath, JsonUtility.ToJson(Document(), true)); File.WriteAllText(referencePath, JsonUtility.ToJson(References(), true));
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var window in windows) if (window) window.Close(); windows.Clear();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test] public void SharedCompilerRetainsExistingSemanticsAndCopiesTypedReferences()
        {
            var source = Document(); var catalogDocument = References(); var catalog = new DialogueReferenceCatalog(catalogDocument);
            var compiled = DialogueSourceCompiler.Compile(source, catalog);
            var existing = new DialogueDefinition(source.id, source.entryId, new[] {
                new DialogueLine("choose", Speaker, Body, choices: new[] { new DialogueChoice("commit", Choice, "end", Condition, Command), new DialogueChoice("leave", Alternate, "end") }),
                new DialogueLine("end", Speaker, End)
            });
            Assert.That(compiled.Definition.ContentHash, Is.EqualTo(existing.ContentHash));
            source.lines[0].choices[0].commandId = "changed"; catalogDocument.texts[0].text = "changed";
            Assert.That(compiled.Definition.Find("choose").Choices[0].CommandId, Is.EqualTo(Command));
            Assert.That(compiled.References.TryReadText(Speaker, out var text), Is.True); Assert.That(text, Is.EqualTo("TECHNICAL SPEAKER"));
            var roundtrip = DialogueSourceJson.Compile(JsonUtility.ToJson(compiled.ToDocument()), JsonUtility.ToJson(catalog.ToDocument()));
            Assert.That(roundtrip.ContentHash, Is.EqualTo(compiled.ContentHash));
            var reordered = compiled.ToDocument(); Array.Reverse(reordered.lines);
            Assert.That(DialogueSourceCompiler.Compile(reordered, catalog).Definition.ContentHash, Is.EqualTo(existing.ContentHash));
            Array.Reverse(reordered.lines.Single(line => line.id == "choose").choices);
            Assert.That(DialogueSourceCompiler.Compile(reordered, catalog).Definition.ContentHash, Is.Not.EqualTo(existing.ContentHash));
        }
        [Test] public void SchemaTypedCatalogAndEveryExistingBadGraphAreRejectedAsAWhole()
        {
            var references = new DialogueReferenceCatalog(References());
            foreach (Action<DialogueSourceDocument> invalid in new Action<DialogueSourceDocument>[] {
                d => d.schemaVersion = 99, d => d.lines[0].id = "end", d => d.entryId = "missing",
                d => d.lines[0].choices[0].nextId = "missing", d => d.lines[1].nextId = "choose",
                d => d.lines = d.lines.Concat(new[] { new DialogueSourceLineDocument { id = "unreachable", speakerKey = Speaker, textKey = End } }).ToArray(),
                d => d.lines[0].nextId = "end", d => d.lines[0].choices[1].id = "commit",
                d => d.lines[0].speakerKey = Body, d => d.lines[0].textKey = Choice,
                d => d.lines[0].choices[0].textKey = Body, d => d.lines[0].conditionId = "missing.condition",
                d => d.lines[0].choices[0].commandId = "missing.command"
            })
            { var document = Document(); invalid(document); Assert.Throws<ArgumentException>(() => DialogueSourceCompiler.Compile(document, references)); }
            var wrongSchema = References(); wrongSchema.schemaVersion = 0; Assert.Throws<ArgumentException>(() => new DialogueReferenceCatalog(wrongSchema));
            var duplicate = References(); duplicate.texts[1].key = Speaker; Assert.Throws<ArgumentException>(() => new DialogueReferenceCatalog(duplicate));
            var wrongKind = References(); wrongKind.texts[0].kind = "speaker"; Assert.Throws<ArgumentException>(() => new DialogueReferenceCatalog(wrongKind));
        }
        [Test] public void AtomicSourceSaveFreshReloadGeneratedTextAssetAndConflictsUseOneCompiler()
        {
            var snapshot = DialogueSource.Load(sourcePath, referencePath); var candidate = snapshot.Compiled.ToDocument(); candidate.lines.Single(l => l.id == "choose").textKey = NewBody;
            var saved = DialogueSource.Save(snapshot, candidate); var fresh = DialogueSource.Load(sourcePath, referencePath);
            Assert.That(fresh.FileHash, Is.EqualTo(saved.FileHash)); Assert.That(fresh.Compiled.ContentHash, Is.EqualTo(saved.Compiled.ContentHash));
            Assert.That(DialogueSource.Save(fresh, fresh.Compiled.ToDocument()).FileHash, Is.EqualTo(fresh.FileHash));
            string generatedPath = Path.Combine(directory, "generated.json"); DialogueSource.Export(fresh, generatedPath);
            Assert.That(File.ReadAllBytes(generatedPath), Is.EqualTo(File.ReadAllBytes(sourcePath)));
            var source = new TextAsset(File.ReadAllText(generatedPath)); var catalog = new TextAsset(File.ReadAllText(referencePath));
            try { Assert.That(DialogueSourceJson.Compile(source, catalog).ContentHash, Is.EqualTo(fresh.Compiled.ContentHash)); }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(catalog); }
            string before = File.ReadAllText(sourcePath); var bad = fresh.Compiled.ToDocument(); bad.lines[0].nextId = "missing";
            Assert.Throws<ArgumentException>(() => DialogueSource.Save(fresh, bad)); Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(before));
            File.AppendAllText(sourcePath, "\n"); string external = File.ReadAllText(sourcePath);
            Assert.Throws<IOException>(() => DialogueSource.Save(fresh, candidate)); Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(external));
            fresh = DialogueSource.Load(sourcePath, referencePath); File.AppendAllText(referencePath, "\n");
            Assert.Throws<IOException>(() => DialogueSource.Save(fresh, candidate)); Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(external));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void ActualHostPinsStartTextAndDefinitionAdoptsNextCandidateAndRejectsInvalidReplacement()
        {
            var root = new GameObject("FixtureSourceHost"); var host = root.AddComponent<NpcDialogueHost>();
            var view = new View(); var conditions = new Conditions(); var original = Compile(); var targets = new Targets(original.Definition);
            int calls = 0; var result = DialogueCommandStatus.Succeeded;
            using (var controls = new ControlSession())
            using (var commands = new DialogueCommandBindings(new Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>> {
                { Command, (context, request) => { calls++; return new DialogueCommandResult(result, result == DialogueCommandStatus.Succeeded ? null : "fixture outcome"); } }
            }))
            try
            {
                host.Configure(targets, conditions, new InteractionPolicy(0, true, false, true), view, new DialogueSourceText(original.References), controls,
                    new DialoguePresentationPolicy(InputChannels.None, false), commands, new DialogueSourceBinding(Targets.NpcId, original));
                var staleCandidate = host.Query(out _)[0]; Assert.That(host.TryInteract(staleCandidate, out var error), Is.True, error);
                var first = host.Current; var oldToken = first.Read().Token; var candidateDocument = original.ToDocument(); candidateDocument.lines.Single(line => line.id == "end").textKey = NewBody;
                var candidate = Compile(candidateDocument, " NEXT");
                Assert.That(host.TryApplySource(candidate, out error), Is.True, error); host.Refresh();
                Assert.That(first.Read().DefinitionVersion, Is.EqualTo(original.Definition.ContentHash)); Assert.That(view.Display.Body, Is.EqualTo("TECHNICAL ORIGINAL LINE"));
                Assert.That(host.SourceBinding.Source.ContentHash, Is.EqualTo(candidate.ContentHash));
                var bad = candidate.ToDocument(); bad.lines[0].choices[0].nextId = "missing";
                var badAsset = new TextAsset(JsonUtility.ToJson(bad)); var referenceAsset = new TextAsset(JsonUtility.ToJson(References()));
                try { Assert.That(host.TryApplySource(badAsset, referenceAsset, out error), Is.False); }
                finally { Object.DestroyImmediate(badAsset); Object.DestroyImmediate(referenceAsset); }
                Assert.That(host.SourceBinding.Source.ContentHash, Is.EqualTo(candidate.ContentHash));
                var unknownCommand = candidate.ToDocument(); unknownCommand.lines[0].choices[0].commandId = "fixture.source.unregistered-command";
                var expandedReferences = References(); expandedReferences.commandIds = expandedReferences.commandIds.Concat(new[] { "fixture.source.unregistered-command" }).ToArray();
                var unsupported = DialogueSourceCompiler.Compile(unknownCommand, new DialogueReferenceCatalog(expandedReferences));
                Assert.That(host.TryApplySource(unsupported, out _), Is.False); Assert.That(host.SourceBinding.Source.ContentHash, Is.EqualTo(candidate.ContentHash));
                conditions.Enabled = false; Assert.That(first.TrySelectChoice(oldToken, "commit", out _), Is.False); Assert.That(calls, Is.Zero);
                first.Poll(); Assert.That(first.Read().Token.Revision, Is.GreaterThan(oldToken.Revision)); conditions.Enabled = true; host.Refresh();
                Assert.That(first.TrySelectChoice(oldToken, "commit", out _), Is.False); Assert.That(calls, Is.Zero);
                Assert.That(first.TrySelectChoice(first.Read().Token, "commit", out error), Is.True, error); Assert.That(calls, Is.EqualTo(1)); host.Refresh();
                Assert.That(view.Display.Body, Is.EqualTo("TECHNICAL END"), "Running presenter retains its original text snapshot too.");
                Assert.That(first.TryAdvance(first.Read().Token, out _), Is.True); host.Refresh();
                Assert.That(host.TryInteract(staleCandidate, out _), Is.False, "An old candidate cannot select the accepted new definition.");
                Assert.That(host.TryInteract(host.Query(out _)[0], out error), Is.True, error); var next = host.Current; host.Refresh();
                Assert.That(next.Read().DefinitionVersion, Is.EqualTo(candidate.Definition.ContentHash)); Assert.That(view.Display.Body, Is.EqualTo("TECHNICAL ORIGINAL LINE NEXT"));
                Assert.That(next.TrySelectChoice(oldToken, "commit", out _), Is.False); Assert.That(calls, Is.EqualTo(1));
                result = DialogueCommandStatus.Failed; Assert.That(next.TrySelectChoice(next.Read().Token, "commit", out _), Is.False);
                Assert.That(next.Read().EndReason, Is.EqualTo(DialogueEndReason.CommandFailed)); Assert.That(calls, Is.EqualTo(2)); Assert.That(commands.ActiveScopeCount, Is.Zero);
            }
            finally { host.Close(DialogueEndReason.OwnerDisposed); Object.DestroyImmediate(root); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowEditsEdgesChoicesUndoSaveReloadAndRealUguiPreviewWithoutCommandsEscaping()
        {
            var window = ScriptableObject.CreateInstance<DialogueSourceWindow>(); windows.Add(window); window.name = "DialogueSourceFixtureWindow"; window.Show(); window.CreateGUI(); window.LoadFiles(sourcePath, referencePath); yield return null;
            var root = window.rootVisualElement; string before = File.ReadAllText(sourcePath);
            root.Q<DropdownField>("text-0").value = NewBody; root.Q<TextField>("choiceId-0-1").value = "leave-edited";
            Assert.That(root.Q<Label>("state").text, Does.Contain("Unsaved")); Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(before));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Assert.That(window.Document.lines.Single(line => line.id == "choose").choices[1].id, Is.EqualTo("leave"));
            root.Q<DropdownField>("text-0").value = NewBody; Click(window, "save"); Click(window, "reload");
            Assert.That(window.Document.lines.Single(line => line.id == "choose").textKey, Is.EqualTo(NewBody));
            Assert.That(DialogueSource.Load(sourcePath, referencePath).Compiled.ContentHash, Is.EqualTo(window.CompileCandidate().ContentHash));
            Click(window, "startPreview"); yield return null;
            Assert.That(window.Preview.View.Displayed.Body, Is.EqualTo("TECHNICAL CANDIDATE LINE")); Assert.That(window.Preview.View.Displayed.Choices.Count, Is.EqualTo(2));
            Assert.That(window.Preview.View.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == "TECHNICAL COMMAND"), Is.True);
            Assert.That(window.Preview.Texture.IsCreated(), Is.True); Assert.That(window.Preview.ControlState.SimulationPaused, Is.False); Assert.That(window.Preview.CommandLog, Is.Empty);
            var first = window.Preview.Current; string version = first.Read().DefinitionVersion; var oldToken = first.Read().Token;
            root.Q<TextField>("choiceNext-0-0").value = "missing"; Assert.That(root.Q<Button>("save").enabledSelf, Is.False); Assert.That(root.Q<Button>("applyPreview").enabledSelf, Is.False);
            Assert.Throws<ArgumentException>(() => window.ApplyPreview()); Assert.That(first.Read().DefinitionVersion, Is.EqualTo(version));
            root.Q<TextField>("choiceNext-0-0").value = "end"; root.Q<DropdownField>("text-0").value = Body; Click(window, "applyPreview");
            Assert.That(first.Read().DefinitionVersion, Is.EqualTo(version));
            root.Q<Toggle>("preview-condition-" + Condition).value = false; Assert.That(window.Preview.View.Displayed.Choices[0].IsAvailable, Is.False);
            root.Q<Toggle>("preview-condition-" + Condition).value = true; Assert.That(first.TrySelectChoice(oldToken, "commit", out _), Is.False);
            Click(window, "previewChoice-0"); Assert.That(window.Preview.CommandLog, Is.EqualTo(new[] { Command })); Assert.That(first.Read().Token.LineId, Is.EqualTo("end"));
            Click(window, "previewAdvance"); Click(window, "nextPreview");
            Assert.That(window.Preview.Current.Read().DefinitionVersion, Is.Not.EqualTo(version)); Assert.That(window.Preview.View.Displayed.Body, Is.EqualTo("TECHNICAL ORIGINAL LINE"));
            Assert.That(window.Preview.ControlState.Blocked, Is.EqualTo(InputChannels.None));
        }
        [UnityTest] public IEnumerator GamePreviewCannotMutateRunAndActualHostUsesGeneratedCandidate() => ExerciseRuntime("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabPreviewCannotMutateRunAndActualHostUsesGeneratedCandidate() => ExerciseRuntime("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator ExerciseRuntime(string scenePath)
        {
            LegacySceneFixture.Open(scenePath); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation())
            {
                float time = arena.Now; var position = arena.Player.Position; int hits = arena.Player.ReceivedHitCount; float health = arena.enemies[0].health; var control = arena.Controls.Current;
                using (var preview = new DialogueSourcePreview(Compile()))
                {
                    Assert.That(preview.TryStart(out var error), Is.True, error); preview.SetCondition(Condition, false); preview.SetCondition(Condition, true); preview.Select(0); preview.Advance();
                    Assert.That(preview.CommandLog, Is.EqualTo(new[] { Command })); Assert.That(preview.Current.Read().Status, Is.EqualTo(DialogueStatus.Completed));
                }
                Assert.That(arena.Now, Is.EqualTo(time)); Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Player.ReceivedHitCount, Is.EqualTo(hits));
                Assert.That(arena.enemies[0].health, Is.EqualTo(health)); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(control.Revision));
                var source = new TextAsset(JsonUtility.ToJson(Document())); var catalog = new TextAsset(JsonUtility.ToJson(References()));
                try
                {
                    var compiled = DialogueSourceJson.Compile(source, catalog);
                    using (var preview = new DialogueSourcePreview(compiled))
                    {
                        Assert.That(preview.TryStart(out var error), Is.True, error); var active = preview.Current; var document = compiled.ToDocument(); document.lines.Single(line => line.id == "choose").textKey = NewBody;
                        var nextSource = new TextAsset(JsonUtility.ToJson(document));
                        try { Assert.That(preview.Host.TryApplySource(nextSource, catalog, out error), Is.True, error); }
                        finally { Object.DestroyImmediate(nextSource); }
                        Assert.That(active.Read().DefinitionVersion, Is.EqualTo(compiled.Definition.ContentHash)); preview.Select(1); preview.Advance();
                        Assert.That(preview.TryStart(out error), Is.True, error); Assert.That(preview.View.Displayed.Body, Is.EqualTo("TECHNICAL CANDIDATE LINE"));
                        preview.Select(1); preview.Advance();
                        var window = ScriptableObject.CreateInstance<DialogueSourceWindow>(); windows.Add(window); window.name = "RuntimeDialogueSourceFixtureWindow";
                        window.Show(); window.CreateGUI(); window.LoadFiles(sourcePath, referencePath); window.BindRuntime(preview.Host, DialogueSourcePreview.NpcId);
                        var runtimeField = window.rootVisualElement.Q<UnityEditor.UIElements.ObjectField>("runtime"); Assert.That(runtimeField.value, Is.SameAs(preview.Host));
                        window.rootVisualElement.Q<TextField>("npcId").value = DialogueSourcePreview.NpcId;
                        window.rootVisualElement.Q<DropdownField>("text-0").value = Body;
                        Click(window, "applyNext"); Assert.That(preview.TryStart(out error), Is.True, error);
                        Assert.That(preview.View.Displayed.Body, Is.EqualTo("TECHNICAL ORIGINAL LINE"));
                        Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(JsonUtility.ToJson(Document(), true)), "Runtime apply never saves.");
                    }
                }
                finally { Object.DestroyImmediate(source); Object.DestroyImmediate(catalog); }
                Assert.That(arena.Now, Is.EqualTo(time)); Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(control.Revision));
            }
        }
        static void Click(DialogueSourceWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button, Is.Not.Null, name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        sealed class Conditions : INarrativeConditions
        {
            public bool Enabled = true;
            public bool Evaluate(string id, Guid world, string placement) => Enabled;
        }
        sealed class Targets : IInteractionTargets
        {
            public const string NpcId = "fixture.source.npc";
            readonly Guid world = Guid.NewGuid(); readonly NpcDefinition npc;
            public Targets(DialogueDefinition definition) { npc = new NpcDefinition(NpcId, definition); }
            NpcTarget Read() => new NpcTarget(world, "fixture.source.placement", npc, 0, false);
            public IReadOnlyList<NpcTarget> ReadTargets() => new[] { Read() };
            public bool TryRead(Guid session, string placement, out NpcTarget target) { target = session == world && placement == "fixture.source.placement" ? Read() : null; return target != null; }
        }
        sealed class View : IDialogueView
        {
            Guid binding; public DialogueDisplay Display;
            public event Action<DialogueIntent> Input;
            public bool TryBind(Guid id, out string error) { binding = id; error = null; return true; }
            public bool TryApply(DialogueDisplay display) { Display = display; return display.BindingId == binding; }
            public void Unbind(Guid id) { if (binding == id) { binding = Guid.Empty; Display = null; } }
            public void Emit(DialogueIntent intent) => Input?.Invoke(intent);
        }
    }
}
