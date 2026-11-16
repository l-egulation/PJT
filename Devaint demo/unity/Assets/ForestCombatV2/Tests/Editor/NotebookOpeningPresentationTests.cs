using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class NotebookOpeningPresentationTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        MonoBehaviour gui;
        Keyboard keyboard;
        void Set(string name, object value) => gui.GetType().GetField(name, Hidden).SetValue(gui, value);
        T Get<T>(string name) => (T)gui.GetType().GetField(name, Hidden).GetValue(gui);
        void Call(string name, params object[] args) => gui.GetType().GetMethod(name, Hidden).Invoke(gui, args);
        void Seek(float age, int page = 0, float pageAt = 0)
        {
            Set("notebookClock", age); Set("notebookPage", page); Set("notebookPageAt", pageAt); Set("notebookFrame", Time.frameCount);
            Call("ShowNotebookPage"); Call("TickNotebook", new object[] { null }); Canvas.ForceUpdateCanvases();
        }
        Graphic[] Leaves() => Get<RectTransform>("notebookBook").GetComponentsInChildren<Graphic>().Where(g => g.GetType().Name == "NotebookLeafGraphic").ToArray();
        IEnumerator Press()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < .06f;) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < .06f;) yield return null;
        }
        IEnumerator EscapeIgnored(float seconds)
        {
            int page = Get<int>("notebookPage");
            float clock = Get<float>("notebookClock");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < seconds;) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.That(Get<bool>("hubPaused"), Is.False, "ESC must not open pause over the prologue");
            Assert.That(Get<bool>("notebookOpen"), Is.True, "holding ESC must not skip the book");
            Assert.That(Get<int>("notebookPage"), Is.EqualTo(page), "ESC must not turn a story page");
            Assert.That(Get<float>("notebookClock"), Is.GreaterThan(clock), "book animation keeps advancing");
            Assert.That(Get<float>("escHeldSince"), Is.EqualTo(-1), "the gesture cannot leak into the playable opening");
            Call("HubBack");
            Assert.That(Get<bool>("hubPaused"), Is.False, "direct Back also respects the book");
        }
        [Test] public void EveryApprovedPageAndShaderIsRestorable()
        {
            var shader = Resources.Load<Shader>("NotebookOpening/NotebookPage");
            Assert.That(shader, Is.Not.Null); Assert.That(shader.isSupported, Is.True);
            foreach (string file in new[] { "cover", "liner", "paper", "desk", "written-0", "written-1", "written-2", "written-3", "written-4",
                "page-0-draft", "page-0-final", "page-1-draft", "page-1-final", "page-2-draft", "page-2-final" })
            {
                var t = Resources.Load<Texture2D>("NotebookOpening/" + file); Assert.That(t, Is.Not.Null, file);
                Assert.That(t.width, Is.GreaterThan(500), file); Assert.That(t.mipmapCount, Is.EqualTo(1), file);
                Assert.That(t.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), file);
            }
        }
        [UnityTest] public IEnumerator NotebookPreservesWritingAndPicturesThroughTurnsAndInput()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            var settings = InputSystem.settings; var background = settings.backgroundBehavior; var editorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            Assert.That(keyboard.enabled, Is.True);
            try
            {
                Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
                StartingVillageWorld village = null;
                for (int i = 0; i < 600 && !(village && village.Ready); i++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(village && village.Ready, Is.True);
                gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(x => x.GetType().Name == "TemporaryGameGui");
                Set("storyChecked", false); yield return null; yield return null;
                System.IO.Directory.CreateDirectory("../outputs/hub/capture");
                for (int i = 0; i < 120 && !Get<bool>("notebookOpen"); i++) yield return null;
                Assert.That(Get<bool>("notebookOpen"), Is.True, "real opening director starts the notebook");
                yield return null; yield return null;
                Seek(0);
                Assert.That(Leaves().Last().mainTexture.name, Is.EqualTo("cover"));
                StartingVillageCaptureTests.Capture(gui, village, "notebook-html-cover");
                yield return Press(); Assert.That(Get<int>("notebookPage"), Is.Zero, "advance during cover is consumed");
                yield return EscapeIgnored(.06f); yield return EscapeIgnored(1.15f);
                Seek(3.55f); StartingVillageCaptureTests.Capture(gui, village, "notebook-html-flutter");
                Seek(4.42f);
                var landing = Leaves().Last(g => g.mainTexture.name == "page-0-final");
                Assert.That(landing.material.GetFloat("_Reveal"), Is.Zero);
                Seek(4.7f);
                Assert.That(Leaves().Where(g => g.mainTexture.name == "page-0-final").All(g => g.material.GetFloat("_Reveal") == 0), Is.True);
                StartingVillageCaptureTests.Capture(gui, village, "notebook-html-written");
                Seek(5.52f);
                Assert.That(Leaves().Last().material.GetFloat("_Reveal"), Is.InRange(.45f, .55f));
                StartingVillageCaptureTests.Capture(gui, village, "notebook-html-reveal");
                Seek(6.3f); Assert.That(Leaves().Last().material.GetFloat("_Reveal"), Is.EqualTo(1));
                Assert.That(Get<List<Text>>("notebookLines")[2].color.a, Is.Zero, "lines reveal one at a time");
                yield return EscapeIgnored(.06f);
                yield return Press(); Assert.That(Get<int>("notebookPage"), Is.Zero, "first press reveals the unread text");
                Assert.That(Get<List<Text>>("notebookLines")[2].color.a, Is.EqualTo(1), $"clock={Get<float>("notebookClock")} pageAt={Get<float>("notebookPageAt")} opening={gui.GetType().GetProperty("OpeningActive").GetValue(gui)}");
                yield return Press(); Assert.That(Get<int>("notebookPage"), Is.EqualTo(1));
                Assert.That(Leaves().Any(g => g.mainTexture.name == "page-0-final" && g.material.GetFloat("_Reveal") == 1), Is.True, "outgoing art survives turn");
                yield return Press(); Assert.That(Get<int>("notebookPage"), Is.EqualTo(1), "press during turn cannot skip the incoming page");
                Seek(9.4f, 1, 9); StartingVillageCaptureTests.Capture(gui, village, "notebook-html-illustrated-turn");
                Seek(10.2f, 1, 9); Assert.That(Leaves().Last().material.GetFloat("_Reveal"), Is.Zero, "second page holds writing before art");
                Seek(13.3f, 1, 9); yield return EscapeIgnored(.06f); yield return EscapeIgnored(1.15f); yield return Press(); Assert.That(Get<int>("notebookPage"), Is.EqualTo(2));
                // The map: the red circle waits for the picture, then draws round the village at the road's end.
                var circle = Get<Image>("notebookCircle");
                Seek(16.7f, 2, 14); Assert.That(circle.enabled, Is.False, "circle waits until the map is in");
                Seek(16.86f, 2, 14); Assert.That(circle.enabled && circle.sprite.name == "red-circle-01", Is.True, "circle starts on its first frame");
                Seek(17.5f, 2, 14); Assert.That(circle.enabled && circle.sprite.name == "red-circle-06", Is.True, "circle holds its last frame");
                Assert.That(circle.transform.GetSiblingIndex(), Is.EqualTo(circle.transform.parent.childCount - 1), "circle is drawn over the page");
                var ring = circle.rectTransform.anchoredPosition;
                Assert.That(ring.x, Is.InRange(580f, 610f)); Assert.That(ring.y, Is.InRange(-272f, -243f));
                Assert.That(circle.rectTransform.sizeDelta.x, Is.InRange(50f, 64f));
                StartingVillageCaptureTests.Capture(gui, village, "notebook-html-map-circle");
                Seek(18.4f, 2, 14); yield return EscapeIgnored(.06f); yield return EscapeIgnored(1.15f); yield return Press();
                yield return EscapeIgnored(.06f);
                for (float at = Time.realtimeSinceStartup; Get<bool>("notebookOpen") && Time.realtimeSinceStartup - at < 3;) yield return null;
                Assert.That(Get<bool>("notebookOpen"), Is.False, "last page closes into the playable opening");
                Call("HubBack"); Assert.That(Get<bool>("hubPaused"), Is.True, "Back pauses again after the book closes");
                Call("HubBack"); Assert.That(Get<bool>("hubPaused"), Is.False);
                Call("ShowNotebook"); Assert.That(Get<int>("notebookPage"), Is.Zero); Seek(0);
                Assert.That(Leaves().Last().mainTexture.name, Is.EqualTo("cover"), "replay resets cover and reveal");
                Call("CloseNotebook"); Assert.That(Get<RectTransform>("notebookRoot").gameObject.activeSelf, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard); settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
                Chapter1ProfileHost.UseStore(null);
            }
            yield return new ExitPlayMode();
        }
    }
}
