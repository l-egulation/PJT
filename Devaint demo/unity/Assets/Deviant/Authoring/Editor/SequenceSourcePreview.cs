using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative;
using Deviant.Narrative.Contracts;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using Deviant.World;
using Deviant.World.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Authoring
{
    public sealed class SequenceSourcePreview : IDisposable
    {
        public const string DialogueBindingId = "fixture.sequence.dialogue", CueBindingId = "fixture.sequence.sprite", CameraBindingId = "fixture.sequence.camera";
        public const string FadeBindingId = "fixture.sequence.fade", StageBindingId = "fixture.sequence.move", SkipEndpointId = "fixture.sequence.skip";
        public const string EntryAnchor = "fixture.sequence.entry", GoalAnchor = "fixture.sequence.goal", FocusAnchor = "fixture.sequence.focus";
        const string Speaker = "fixture.sequence.speaker", Body = "fixture.sequence.line", End = "fixture.sequence.end", Choice = "fixture.sequence.choice", Command = "fixture.sequence.command";
        readonly DialogueSourcePreview surface;
        readonly ControlSession controls = new ControlSession();
        readonly Clock clock = new Clock();
        readonly Endpoint endpoint = new Endpoint();
        readonly ChapterWorldHost world;
        readonly SequenceHost host;
        readonly SpriteCueFactory spriteFactory;
        readonly CuePlayer cues;
        readonly CameraOutputBinding cameraOutput;
        readonly Camera stageCamera;
        readonly StageActorOutputBinding stage;
        readonly FadeOutputBinding fade;
        readonly DialogueCommandBindings commands;
        readonly RenderTexture stageTexture;
        readonly Texture2D fixtureTexture;
        readonly Sprite fixtureSprite;
        readonly GameObject owner;
        int commandCount;
        bool disposed;
        public SequenceHost Host => host;
        public RenderTexture Texture => surface.Texture;
        public RenderTexture StageTexture => stageTexture;
        public ChapterSequenceBindings Bindings { get; }
        public double Time => clock.Time;
        public int CommandCount => commandCount;
        public int SkipCount => endpoint.Calls;
        public int ActiveCues => cues.ActiveCount;
        public bool StageOwned => stage.IsOwned;
        public int FadeRequests => fade.RequestCount;
        public ControlState ControlState => controls.Current;
        public SequenceSourcePreview(CompiledSequenceSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            try
            {
                var dialogue = DialogueSourceCompiler.Compile(new DialogueSourceDocument { schemaVersion = 1, id = "fixture.sequence.preview-dialogue", entryId = "choose", lines = new[] {
                    new DialogueSourceLineDocument { id = "choose", speakerKey = Speaker, textKey = Body, choices = new[] { new DialogueSourceChoiceDocument { id = "continue", textKey = Choice, nextId = "end", commandId = Command } } },
                    new DialogueSourceLineDocument { id = "end", speakerKey = Speaker, textKey = End }
                } }, new DialogueReferenceCatalog(new DialogueReferenceCatalogDocument { schemaVersion = 1, conditionIds = Array.Empty<string>(), commandIds = new[] { Command }, texts = new[] {
                    new DialogueTextDocument { key = Speaker, kind = "Speaker", text = "TECHNICAL SEQUENCE SPEAKER" }, new DialogueTextDocument { key = Body, kind = "Line", text = "TECHNICAL SEQUENCE DIALOGUE" },
                    new DialogueTextDocument { key = End, kind = "Line", text = "TECHNICAL SEQUENCE END" }, new DialogueTextDocument { key = Choice, kind = "Choice", text = "TECHNICAL CONTINUE (fixture receipt only)" }
                } }));
                surface = new DialogueSourcePreview(dialogue);
                owner = Root("SequenceSourcePrivateComposition"); host = owner.AddComponent<SequenceHost>();
                var sources = Root("SequenceSourcePrivateSources"); sources.SetActive(false);
                var module = new GameObject("TechnicalFloor"); module.transform.SetParent(sources.transform, false); var collider = module.AddComponent<BoxCollider2D>(); collider.size = new Vector2(8, 1); collider.offset = new Vector2(4, -.5f);
                var marker = new GameObject("TechnicalMarker"); marker.transform.SetParent(sources.transform, false);
                world = Root("SequenceSourcePrivateChapter").AddComponent<ChapterWorldHost>();
                var layout = new ChapterLayout("fixture.sequence.chapter", "1", new LayoutOrigin(LayoutOriginKind.Authored, "fixture.sequence.author", "1", "1"), EntryAnchor,
                    new[] { new LayoutModule("fixture.module", "fixture.floor", new LayoutPoint(0, 0)) },
                    new[] { new LayoutAnchor(EntryAnchor, "fixture.module", new LayoutPoint(1, 0)), new LayoutAnchor(GoalAnchor, "fixture.module", new LayoutPoint(7, 0)), new LayoutAnchor(FocusAnchor, "fixture.module", new LayoutPoint(4, 1), LayoutAnchorKind.Binding) },
                    new[] { new LayoutConnection("fixture.path", EntryAnchor, GoalAnchor) }, new[] { new LayoutPlacement("fixture.npc-placement", "fixture.marker", "fixture.module", new LayoutPoint(3, 0)) }, new[] { GoalAnchor, FocusAnchor });
                if (!world.TryOpen(new AuthoredLayoutProvider(layout), new LayoutRequest(layout.Id, layout.Version, 0), new LayoutBodyProfile(.9375f, 2, .1875f, 1, 5, "fixture.sequence.body"),
                    new Dictionary<string, GameObject> { { "fixture.floor", module } }, new Dictionary<string, GameObject> { { "fixture.marker", marker } }, out var error)) throw new InvalidOperationException(error);
                if (!world.Session.TryLoadModule("fixture.module", out error)) throw new InvalidOperationException(error);
                fixtureTexture = new Texture2D(8, 8) { name = "TechnicalSequencePreview", hideFlags = HideFlags.HideAndDontSave };
                fixtureTexture.SetPixels(Enumerable.Repeat(new Color(.25f, .8f, .85f), 64).ToArray()); fixtureTexture.Apply();
                fixtureSprite = Sprite.Create(fixtureTexture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 8); fixtureSprite.hideFlags = HideFlags.HideAndDontSave;
                var stageObject = Root("TechnicalStageSprite", typeof(SpriteRenderer)); stageObject.GetComponent<SpriteRenderer>().sprite = fixtureSprite;
                stage = owner.AddComponent<StageActorOutputBinding>(); stage.Configure(stageObject.GetComponent<SpriteRenderer>(), new StagePose(new StagePoint(1, 0), 0, 1, false));
                var cueRoot = Root("SequenceSourcePrivateCueOutput"); spriteFactory = new SpriteCueFactory(outputRoot: cueRoot.transform);
                var spriteCue = new PresentationCueDefinition("fixture.sequence.cue", new[] { "fixture.sequence.frame" }, new[] { .25f }, new[] { 0f }, new[] { 0f }); spriteFactory.Publish(spriteCue, new[] { fixtureSprite });
                cues = new CuePlayer(spriteFactory);
                var cueCatalog = new SequenceCueCatalog(spriteFactory); cueCatalog.Publish(SequenceCueMedia.Sprite(spriteCue, new[] { fixtureSprite }));
                stageTexture = new RenderTexture(760, 220, 24) { name = "SequenceSourceStagePreview", hideFlags = HideFlags.HideAndDontSave }; stageTexture.Create();
                stageCamera = Root("SequenceSourceStageCamera", typeof(Camera)).GetComponent<Camera>(); stageCamera.orthographic = true; stageCamera.orthographicSize = 2.5f; stageCamera.aspect = 760f / 220;
                stageCamera.transform.position = new Vector3(4, 1, -10); stageCamera.targetTexture = stageTexture; stageCamera.clearFlags = CameraClearFlags.SolidColor; stageCamera.backgroundColor = new Color(.08f, .11f, .13f); stageCamera.scene = surface.PreviewScene; stageCamera.cullingMask = ~(1 << 5);
                cameraOutput = stageCamera.gameObject.AddComponent<CameraOutputBinding>();
                var stageImage = new GameObject("SequencePrivateStageSurface", typeof(RectTransform), typeof(RawImage)) { hideFlags = HideFlags.HideAndDontSave, layer = 5 };
                stageImage.transform.SetParent(surface.View.transform, false); stageImage.transform.SetAsFirstSibling();
                var stageRect = stageImage.GetComponent<RectTransform>(); stageRect.anchorMin = Vector2.zero; stageRect.anchorMax = Vector2.one; stageRect.offsetMin = stageRect.offsetMax = Vector2.zero;
                stageImage.GetComponent<RawImage>().texture = stageTexture; stageImage.GetComponent<RawImage>().raycastTarget = false;
                var panel = surface.View.transform; var imageRoot = new GameObject("SequencePrivateFade", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.HideAndDontSave, layer = 5 };
                imageRoot.transform.SetParent(panel, false); var rect = imageRoot.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = imageRoot.GetComponent<Image>(); image.raycastTarget = false; fade = owner.AddComponent<FadeOutputBinding>(); fade.Configure(image, default);
                commands = new DialogueCommandBindings(new Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>> { { Command, (context, request) => { commandCount++; return new DialogueCommandResult(DialogueCommandStatus.Succeeded); } } });
                var targets = new ChapterNpcTargets(world, world.Session.Id, new[] { new NpcPlacementBinding("fixture.npc-placement", FocusAnchor, new NpcDefinition("fixture.sequence.npc", dialogue.Definition)) }, () => new Vector2(4, 1), () => false);
                Bindings = new ChapterSequenceBindings(world, world.Session.Id, targets, new Conditions(), new InteractionPolicy(0, true, false, true), surface.View, new DialogueSourceText(dialogue.References), controls,
                    new DialoguePresentationPolicy(InputChannels.None, false), cues, cueCatalog, cameraOutput.Requests,
                    new[] { new SequenceDialogueBinding(DialogueBindingId, "fixture.npc-placement") }, new[] { new SequenceCueBinding(CueBindingId, FocusAnchor, spriteCue.Id, 1) },
                    new[] { new SequenceCameraBinding(CameraBindingId, FocusAnchor, new CameraPosition(0, 0, -10), 10) }, endpoint, fade,
                    new[] { new SequenceFadeBinding(FadeBindingId, new FadeDefinition("fixture.sequence.fade-definition", 0, 0, 0, 0, .6f, .25, false), 10) },
                    new[] { new SequenceStageActorBinding(StageBindingId, "fixture.stage", EntryAnchor, GoalAnchor, new StageMoveDefinition("fixture.sequence.move-definition", .5, 1, 0)) },
                    new Dictionary<string, IStageActorRequests> { { "fixture.stage", stage } }, commands);
                if (!host.TryConfigureSource(new SequenceSourceBinding(source, Bindings, controls, new SequenceControlPolicy(InputChannels.None, false), clock), out error)) throw new InvalidOperationException(error);
            }
            catch { Dispose(); throw; }
        }
        GameObject Root(string name, params Type[] components)
        {
            var root = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave }; SceneManager.MoveGameObjectToScene(root, surface.PreviewScene); return root;
        }
        public bool TryStart(out string error) { Check(); bool started = host.TryStartSource(out error); Refresh(); return started; }
        public void Advance(double seconds)
        {
            Check(); if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || double.IsInfinity(clock.Time + seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            clock.Time += seconds; Refresh();
        }
        public void SelectDialogue(int choice)
        {
            Check(); var buttons = surface.View.GetComponentsInChildren<Button>(true).Where(button => button.gameObject.name.StartsWith("Choice", StringComparison.Ordinal)).ToArray();
            if (choice < 0 || choice >= buttons.Length) throw new ArgumentOutOfRangeException(nameof(choice));
            if (buttons[choice].isActiveAndEnabled && buttons[choice].interactable) buttons[choice].onClick.Invoke(); Refresh();
        }
        public void AdvanceDialogue() { Check(); var button = surface.View.GetComponentsInChildren<Button>(true).Single(item => item.gameObject.name == "Advance"); if (button.isActiveAndEnabled && button.interactable) button.onClick.Invoke(); Refresh(); }
        public void Cancel()
        {
            Check(); var current = host.Current; if (current == null) throw new InvalidOperationException("No execution.");
            if (!current.TryCancel(current.Read().ExecutionId, out var error)) throw new InvalidOperationException(error); Refresh();
        }
        public void Skip()
        {
            Check(); var current = host.Current; if (current == null) throw new InvalidOperationException("No execution.");
            if (!current.TrySkip(current.Read().ExecutionId, out var error)) throw new InvalidOperationException(error); Refresh();
        }
        public void Refresh() { Check(); host.Refresh(); stage.ApplyNow(); fade.ApplyNow(); cameraOutput.ApplyNow(); stageCamera.Render(); surface.Refresh(); }
        void Check() { if (disposed) throw new ObjectDisposedException(nameof(SequenceSourcePreview)); }
        public void Dispose()
        {
            if (disposed) return; disposed = true; if (host) host.Close(); commands?.Dispose(); cues?.Dispose(); spriteFactory?.Dispose();
            if (world) world.Close(); controls.Dispose(); surface?.Dispose();
            if (stageTexture) { stageTexture.Release(); Object.DestroyImmediate(stageTexture); }
            if (fixtureSprite) Object.DestroyImmediate(fixtureSprite); if (fixtureTexture) Object.DestroyImmediate(fixtureTexture);
        }
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class Conditions : INarrativeConditions { public bool Evaluate(string id, Guid world, string placement) => true; }
        sealed class Endpoint : ISequenceSkipEndpoint
        {
            public int Calls;
            public bool Validate(string id, out string error) { error = id == SkipEndpointId ? null : "Only the explicitly named technical skip endpoint is registered in this preview."; return error == null; }
            public ISequenceOperation Begin(Guid execution, string id) { if (!Validate(id, out var error)) throw new InvalidOperationException(error); Calls++; return new Completed(); }
            sealed class Completed : ISequenceOperation { public SequenceOperationStatus Status => SequenceOperationStatus.Completed; public string Error => null; public void Poll(double elapsed) { } public void Dispose() { } }
        }
    }
}
