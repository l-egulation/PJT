using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control.Contracts;
using Deviant.Integration;
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
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class NpcDialogueIntegrationTests
    {
        [UnityTest] public IEnumerator GameUsesNpcSessionPresenterAndView() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesNpcSessionPresenterAndView() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var sources = new GameObject("NpcDialogueSources"); sources.SetActive(false);
            var module = new GameObject("ModuleSource"); module.transform.SetParent(sources.transform, false);
            var floor = module.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
            var marker = new GameObject("NpcMarkerSource"); marker.transform.SetParent(sources.transform, false);
            var worldRoot = new GameObject("NpcChapterFixture"); worldRoot.transform.position = new Vector3(0, 1000, 0); var world = worldRoot.AddComponent<ChapterWorldHost>();
            var ownerRoot = new GameObject("NpcDialogueComposition"); var host = ownerRoot.AddComponent<NpcDialogueHost>();
            var canvasRoot = CreateView(out var view, out var next, out var cancel);
            GameObject events = null;
            if (!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
                events = new GameObject("DialogueFixtureEvents", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(canvasRoot.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            Assert.That(next.targetGraphic.raycastTarget && cancel.targetGraphic.raycastTarget, Is.True);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                    var definition = ChapterFixtures.Good();
                    Assert.That(world.TryOpen(new AuthoredLayoutProvider(definition), new LayoutRequest(definition.Id, definition.Version), UnityChapterModules.ProfileFor(player),
                        new Dictionary<string, GameObject> { { "floor-8", module } }, new Dictionary<string, GameObject> { { "marker", marker } }, out var error), Is.True, error);
                    Assert.That(world.Session.TryLoadModule("east", out error), Is.True, error);
                    Assert.That(world.TryGetWorldAnchor(world.Session.Id, "focus", out var point, out _), Is.True);
                    var npc = new Deviant.Definitions.NpcDefinition("fixture-npc", DialogueFixtures.Definition(), "npc");
                    var targets = new ChapterNpcTargets(world, world.Session.Id, new[] { new NpcPlacementBinding("marker-east", "focus", npc) }, () => player.Position, () => arena.CombatActive);
                    Assert.Throws<ArgumentException>(() => targets.ReplaceBindings(new[] { new NpcPlacementBinding("missing-placement", "focus", npc) }));
                    Assert.Throws<ArgumentException>(() => targets.ReplaceBindings(new[] { new NpcPlacementBinding("marker-east", "missing-anchor", npc) }));
                    var conditions = new DialogueFixtures.Conditions(); var text = new DialogueFixtures.Text();
                    foreach (bool pauseWorld in new[] { false, true })
                    {
                        player.Position = point + Vector2.right; player.velocity = Vector2.zero;
                        host.Configure(targets, conditions, new InteractionPolicy(2, false, false, true), view, text, arena.Controls,
                            new DialoguePresentationPolicy(InputChannels.Combat | InputChannels.Interaction, pauseWorld));
                        var candidates = host.Query(out error); Assert.That(candidates.Count, Is.EqualTo(1), error); var selected = candidates[0];
                        player.Position = point + Vector2.right * 20; Assert.That(host.TryInteract(selected, out _), Is.False, "Range must be rechecked after candidate display.");
                        player.Position = point + Vector2.right;
                        Assert.That(host.TryInteract(selected, out error), Is.True, error); var session = host.Current; var first = session.Read();
                        Assert.That(view.Displayed.Body, Is.EqualTo("fixture.a")); Assert.That(player.InputBlocked, Is.True);
                        Assert.That(arena.Controls.Current.SimulationPaused, Is.EqualTo(pauseWorld));
                        if (pauseWorld) Assert.Throws<InvalidOperationException>(() => driver.Step(.02f)); else driver.Step(.02f);
                        next.onClick.Invoke(); next.onClick.Invoke(); Assert.That(session.Read().Token.LineId, Is.EqualTo("b"));
                        host.Refresh(); Assert.That(view.Displayed.Body, Is.EqualTo("fixture.b"));
                        using (var external = arena.Controls.OpenScope("external modal fixture"))
                        {
                            var block = external.BlockInput(InputChannels.Dialogue | InputChannels.Combat); var pause = external.PauseSimulation();
                            next.onClick.Invoke(); Assert.That(session.Read().Token.LineId, Is.EqualTo("b"));
                            block.Dispose(); external.BlockInput(InputChannels.Combat);
                            next.onClick.Invoke(); host.Refresh(); next.onClick.Invoke();
                            Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Completed));
                            Assert.That(player.InputBlocked, Is.True); Assert.That(pause.IsActive, Is.True); Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                        }
                        Assert.That(player.InputBlocked, Is.False); Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                        Assert.That(world.Session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present), "Dialogue completion does not invent a world-removal/reward fact.");
                        Assert.That(host.TryInteract(selected, out error), Is.True, error); var disabled = host.Current;
                        view.gameObject.SetActive(false); host.Refresh();
                        Assert.That(disabled.Read().EndReason, Is.EqualTo(DialogueEndReason.ViewClosed)); Assert.That(player.InputBlocked, Is.False);
                        Assert.That(disabled.TryAdvance(first.Token, out _), Is.False);
                        Assert.That(host.TryInteract(selected, out error), Is.True, error); var lost = host.Current;
                        Assert.That(world.Session.TryUnloadModule("east", out error), Is.True, error); host.Refresh();
                        Assert.That(lost.Read().EndReason, Is.EqualTo(DialogueEndReason.TargetLost)); Assert.That(lost.Read().Status, Is.EqualTo(DialogueStatus.Cancelled));
                        Assert.That(player.InputBlocked, Is.False); Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                        Assert.That(world.Session.TryLoadModule("east", out error), Is.True, error);
                        Assert.That(world.Session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));
                        Assert.That(host.TryInteract(selected, out error), Is.True, error); cancel.onClick.Invoke(); host.Refresh();
                        Assert.That(host.Current.Read().EndReason, Is.EqualTo(DialogueEndReason.PlayerCancelled));
                    }
                    player.Position = point + Vector2.right; var lastCandidate = host.Query(out error)[0];
                    view.enabled = false;
                    Assert.That(host.TryInteract(lastCandidate, out _), Is.False, "A disabled view must fail before acquiring dialogue pause/input control.");
                    Assert.That(host.Current.Read().EndReason, Is.EqualTo(DialogueEndReason.PresentationFailed));
                    Assert.That(player.InputBlocked, Is.False); Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                    view.enabled = true;
                    Assert.That(host.TryInteract(lastCandidate, out error), Is.True, error); var closing = host.Current;
                    host.Close(DialogueEndReason.SceneChanged); Assert.That(closing.Read().EndReason, Is.EqualTo(DialogueEndReason.SceneChanged));
                    Assert.That(player.InputBlocked, Is.False); Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                    world.Close(); Assert.That(targets.ReadTargets().Count, Is.Zero);
                }
            }
            finally
            {
                host.Close(DialogueEndReason.OwnerDisposed); world.Close();
                Object.Destroy(ownerRoot); Object.Destroy(worldRoot); Object.Destroy(sources); Object.Destroy(canvasRoot); if (events) Object.Destroy(events);
            }
        }
        static GameObject CreateView(out DialogueView view, out UnityEngine.UI.Button next, out UnityEngine.UI.Button cancel)
        {
            var root = new GameObject("DialogueCanvasFixture", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var panel = new GameObject("DialoguePanelFixture", typeof(RectTransform)); panel.transform.SetParent(root.transform, false); panel.SetActive(false);
            ((RectTransform)panel.transform).sizeDelta = new Vector2(600, 240);
            var speaker = Label(panel.transform, "Speaker"); var body = Label(panel.transform, "Body");
            ((RectTransform)speaker.transform).anchoredPosition = new Vector2(0, 80);
            next = Button(panel.transform, "Advance"); cancel = Button(panel.transform, "Cancel");
            ((RectTransform)next.transform).anchoredPosition = new Vector2(80, -70); ((RectTransform)cancel.transform).anchoredPosition = new Vector2(-80, -70);
            view = panel.AddComponent<DialogueView>(); view.Configure(panel, speaker, body, next, cancel); return root;
        }
        static TMPro.TMP_Text Label(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            ((RectTransform)obj.transform).sizeDelta = new Vector2(500, 60);
            var text = obj.AddComponent<TMPro.TextMeshProUGUI>(); text.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"); text.raycastTarget = false; return text;
        }
        static UnityEngine.UI.Button Button(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button)); obj.transform.SetParent(parent, false);
            ((RectTransform)obj.transform).sizeDelta = new Vector2(100, 40);
            var button = obj.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = obj.GetComponent<UnityEngine.UI.Image>(); button.targetGraphic.raycastTarget = true; return button;
        }
    }
}
