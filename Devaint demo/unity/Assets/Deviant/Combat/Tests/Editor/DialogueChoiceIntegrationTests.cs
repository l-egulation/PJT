using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative;
using Deviant.Narrative.Contracts;
using Deviant.Narrative.Tests;
using Deviant.UI.Presentation;
using Deviant.UI.Unity;
using Deviant.World;
using Deviant.World.Contracts;
using Deviant.World.Tests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class DialogueChoiceIntegrationTests
    {
        sealed class Layout : IDisposable
        {
            public readonly GameObject Root, Panel;
            public readonly DialogueView View;
            public readonly TMPro.TMP_Text Speaker, Body;
            public readonly Button Advance, Cancel;
            public readonly Button[] Choices;
            public readonly TMPro.TMP_Text[] Labels;
            public Layout(int slots)
            {
                Root = new GameObject("ChoiceCanvasFixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)); Root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Panel = new GameObject("ChoicePanel", typeof(RectTransform)); Panel.transform.SetParent(Root.transform, false); Panel.SetActive(false);
                Speaker = Label(Panel.transform, "Speaker"); Body = Label(Panel.transform, "Body"); Advance = Button("Advance"); Cancel = Button("Cancel");
                Choices = new Button[slots]; Labels = new TMPro.TMP_Text[slots];
                for (int i = 0; i < slots; i++) { Choices[i] = Button("Choice" + i); Labels[i] = Label(Choices[i].transform, "ChoiceText" + i); }
                View = Panel.AddComponent<DialogueView>(); View.Configure(Panel, Speaker, Body, Advance, Cancel, Choices, Labels);
            }
            Button Button(string name)
            {
                var owner = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); owner.transform.SetParent(Panel.transform, false);
                return owner.GetComponent<Button>();
            }
            static TMPro.TMP_Text Label(Transform parent, string name)
            {
                var owner = new GameObject(name, typeof(RectTransform)); owner.transform.SetParent(parent, false);
                var text = owner.AddComponent<TMPro.TextMeshProUGUI>(); text.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"); text.raycastTarget = false; return text;
            }
            public void Dispose() { Object.DestroyImmediate(Root); }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameSelectsConditionalBranchesThroughTheExistingHostAndView() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabSelectsConditionalBranchesThroughTheExistingHostAndView() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var sources = new GameObject("ChoiceChapterSources"); sources.SetActive(false);
            var module = new GameObject("ChoiceFloor"); module.transform.SetParent(sources.transform, false); var floor = module.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
            var marker = new GameObject("ChoiceMarker"); marker.transform.SetParent(sources.transform, false);
            var worldRoot = new GameObject("ChoiceChapter"); worldRoot.transform.position = new Vector3(0, 1000, 0); var world = worldRoot.AddComponent<ChapterWorldHost>();
            var owner = new GameObject("ChoiceHost"); var host = owner.AddComponent<NpcDialogueHost>();
            using (var layout = new Layout(3))
            {
                try
                {
                    using (var driver = arena.AcquireManualSimulation())
                    {
                        driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                        var definition = ChapterFixtures.Good(); Assert.That(world.TryOpen(new AuthoredLayoutProvider(definition), new LayoutRequest(definition.Id, definition.Version), UnityChapterModules.ProfileFor(player),
                            new Dictionary<string, GameObject> { { "floor-8", module } }, new Dictionary<string, GameObject> { { "marker", marker } }, out var error), Is.True, error);
                        Assert.That(world.Session.TryLoadModule("east", out error), Is.True, error); Assert.That(world.TryGetWorldAnchor(world.Session.Id, "focus", out var point, out _), Is.True);
                        player.Position = point + Vector2.right; player.velocity = Vector2.zero;
                        var npc = new NpcDefinition("fixture-npc", DialogueChoiceFixtures.Definition()); var targets = new ChapterNpcTargets(world, world.Session.Id,
                            new[] { new NpcPlacementBinding("marker-east", "focus", npc) }, () => player.Position, () => arena.CombatActive);
                        var conditions = new DialogueFixtures.Conditions { Rejected = "left-enabled" };
                        host.Configure(targets, conditions, new InteractionPolicy(2, false, false, true), layout.View, new DialogueFixtures.Text(), arena.Controls,
                            new DialoguePresentationPolicy(InputChannels.Combat | InputChannels.Interaction, true));
                        var selected = host.Query(out _)[0]; Assert.That(host.TryInteract(selected, out error), Is.True, error);
                        var session = host.Current; var initial = session.Read(); var initialDisplay = layout.View.Displayed;
                        Assert.That(layout.Labels[0].text, Is.EqualTo("fixture.choice.left")); Assert.That(layout.Labels[1].text, Is.EqualTo("fixture.choice.right"));
                        Assert.That(layout.Advance.interactable || layout.Choices[0].interactable || layout.Choices[2].gameObject.activeSelf, Is.False);
                        Assert.That(layout.Choices[1].interactable, Is.True); layout.Advance.onClick.Invoke(); layout.Choices[0].onClick.Invoke(); Assert.That(session.Read(), Is.SameAs(initial));
                        conditions.Rejected = "right-enabled"; layout.Choices[1].onClick.Invoke(); Assert.That(session.Read(), Is.SameAs(initial), "The enabled-looking button must still recheck its condition at selection.");
                        host.Refresh(); var changed = session.Read(); Assert.That(layout.Choices[0].interactable, Is.True); Assert.That(layout.Choices[1].interactable, Is.False);
                        Assert.That(changed.Token.LineId, Is.EqualTo("choose")); Assert.That(session.TrySelectChoice(initial.Token, "left", out _), Is.False);
                        targets.ReplaceBindings(new[] { new NpcPlacementBinding("marker-east", "focus", new NpcDefinition("fixture-npc", DialogueChoiceFixtures.Definition("-next"))) });
                        using (var modal = arena.Controls.OpenScope("fixture modal"))
                        {
                            var block = modal.BlockInput(InputChannels.Dialogue | InputChannels.Combat); var pause = modal.PauseSimulation();
                            layout.Choices[0].onClick.Invoke(); Assert.That(session.Read().Token.LineId, Is.EqualTo("choose")); block.Dispose(); var other = modal.BlockInput(InputChannels.Combat);
                            layout.Choices[0].onClick.Invoke(); layout.Choices[0].onClick.Invoke(); Assert.That(session.Read().Token.LineId, Is.EqualTo("left"));
                            Assert.That(session.Read().DefinitionVersion, Is.EqualTo(initial.DefinitionVersion)); host.Refresh(); Assert.That(layout.View.Displayed.Body, Is.EqualTo("fixture.left"));
                            Assert.That(layout.Advance.interactable, Is.True); Assert.That(layout.Choices[0].gameObject.activeSelf || layout.Choices[1].gameObject.activeSelf, Is.False);
                            layout.Advance.onClick.Invoke(); host.Refresh(); layout.Advance.onClick.Invoke(); Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Completed));
                            Assert.That(other.IsActive && pause.IsActive && player.InputBlocked, Is.True); Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                        }
                        Assert.That(player.InputBlocked || arena.Controls.Current.SimulationPaused, Is.False); Assert.That(world.Session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));
                        Assert.That(host.TryInteract(selected, out _), Is.False, "Starting another dialogue must reject the earlier NPC version.");
                        Assert.That(host.TryInteract(host.Query(out _)[0], out error), Is.True, error); var next = host.Current;
                        layout.View.Unbind(initialDisplay.BindingId); Assert.That(layout.View.TryApply(initialDisplay), Is.False); Assert.That(next.Read().Token.LineId, Is.EqualTo("choose"));
                        Assert.That(next.TrySelectChoice(initial.Token, "left", out _), Is.False);
                        Assert.That(world.Session.TryUnloadModule("east", out error), Is.True, error); host.Refresh();
                        Assert.That(next.Read().EndReason, Is.EqualTo(DialogueEndReason.TargetLost)); Assert.That(layout.View.BindingId, Is.EqualTo(Guid.Empty));
                        layout.Choices[0].onClick.Invoke(); Assert.That(next.Read().Status, Is.EqualTo(DialogueStatus.Cancelled)); Assert.That(player.InputBlocked || arena.Controls.Current.SimulationPaused, Is.False);
                        // Arena reset emits ordinary diagnostic logs; unexpected error logs still fail the Unity test.
                    }
                }
                finally { host.Close(DialogueEndReason.OwnerDisposed); world.Close(); Object.DestroyImmediate(owner); Object.DestroyImmediate(worldRoot); Object.DestroyImmediate(sources); }
            }
        }
        [UnityTest] public IEnumerator AButtonCallbackCannotApplyItsOldClickToTheNextChoiceLine() => ExerciseOldClick(false);
        [UnityTest] public IEnumerator AButtonCallbackCannotApplyItsOldClickToAReplacementSession() => ExerciseOldClick(true);
        static IEnumerator ExerciseOldClick(bool replaceSession)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var definition = new DialogueDefinition("fixture-callback-choices", "a", new[] {
                new DialogueLine("a", "speaker", "body-a", choices: new[] { new DialogueChoice("same", "label-a", "b") }),
                new DialogueLine("b", "speaker", "body-b", choices: new[] { new DialogueChoice("same", "label-b", "end") }),
                new DialogueLine("end", "speaker", "body-end") });
            var targets = new DialogueFixtures.Targets { Npc = new NpcDefinition("fixture-npc", definition) };
            using (var layout = new Layout(1)) using (var controls = new ControlSession())
            using (var service = new InteractionService(targets, new DialogueFixtures.Conditions(), new InteractionPolicy(2, false, false, true)))
            {
                DialoguePresenter presenter = null; bool changed = false;
                layout.Choices[0].onClick.AddListener(() => {
                    if (changed) return; changed = true;
                    if (replaceSession)
                    {
                        presenter.Dispose(); Assert.That(service.TryStart(service.Query(out _)[0], out var next, out _), Is.True);
                        presenter = new DialoguePresenter(next, layout.View, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, false));
                    }
                    else { Assert.That(service.Current.TrySelectChoice(service.Current.Read().Token, "same", out _), Is.True); presenter.Refresh(); }
                });
                layout.View.Configure(layout.Panel, layout.Speaker, layout.Body, layout.Advance, layout.Cancel, layout.Choices, layout.Labels);
                try
                {
                    Assert.That(service.TryStart(service.Query(out _)[0], out var first, out _), Is.True);
                    presenter = new DialoguePresenter(first, layout.View, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, false));
                    layout.Choices[0].onClick.Invoke(); Assert.That(service.Current.Read().Token.LineId, Is.EqualTo(replaceSession ? "a" : "b"), "The in-flight callback belongs to the old displayed token/binding.");
                    layout.Choices[0].onClick.Invoke(); Assert.That(service.Current.Read().Token.LineId, Is.EqualTo(replaceSession ? "b" : "end"));
                }
                finally { presenter?.Dispose(); }
                Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None)); LogAssert.NoUnexpectedReceived();
            }
        }
        [UnityTest] public IEnumerator InsufficientOrAliasedChoiceSlotsFailExplicitlyAndOldLayoutsCanBeRebound()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var layout = new Layout(1)) using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture external"))
            using (var service = new InteractionService(DialogueChoiceFixtures.Targets(), new DialogueFixtures.Conditions(), new InteractionPolicy(2, false, false, true)))
            {
                var other = external.BlockInput(InputChannels.Interaction); Assert.That(service.TryStart(service.Query(out _)[0], out var session, out _), Is.True);
                using (var presenter = new DialoguePresenter(session, layout.View, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, true)))
                {
                    Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.PresentationFailed)); Assert.That(presenter.IsActive, Is.False); Assert.That(layout.Panel.activeSelf, Is.False);
                    Assert.That(other.IsActive, Is.True); Assert.That(controls.Current.SimulationPaused, Is.False); Assert.That(layout.View.BindingId, Is.EqualTo(Guid.Empty));
                }
                Assert.Throws<ArgumentException>(() => layout.View.Configure(layout.Panel, layout.Speaker, layout.Body, layout.Advance, layout.Cancel, new[] { layout.Advance }, layout.Labels));
                Assert.Throws<ArgumentException>(() => layout.View.Configure(layout.Panel, layout.Speaker, layout.Body, layout.Advance, layout.Cancel, new[] { layout.Choices[0], layout.Choices[0] }, new[] { layout.Labels[0], layout.Labels[0] }));
                // A larger injected layout is explicit; no truncation or auto-generated product layout.
                var buttonOwner = new GameObject("ExtraChoice", typeof(RectTransform), typeof(Image), typeof(Button)); buttonOwner.transform.SetParent(layout.Panel.transform, false);
                var label = new GameObject("ExtraLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)); label.transform.SetParent(buttonOwner.transform, false);
                var buttons = new[] { layout.Choices[0], buttonOwner.GetComponent<Button>() }; var labels = new[] { layout.Labels[0], label.GetComponent<TMPro.TMP_Text>() };
                layout.View.Configure(layout.Panel, layout.Speaker, layout.Body, layout.Advance, layout.Cancel, buttons, labels); var saved = buttons[1]; buttons[1] = null; labels[1] = null;
                Assert.That(service.TryStart(service.Query(out _)[0], out var next, out _), Is.True);
                using (var presenter = new DialoguePresenter(next, layout.View, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, false)))
                {
                    saved.onClick.Invoke(); Assert.That(next.Read().Token.LineId, Is.EqualTo("right")); presenter.Refresh(); layout.Cancel.onClick.Invoke(); Assert.That(next.Read().EndReason, Is.EqualTo(DialogueEndReason.PlayerCancelled));
                }
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
