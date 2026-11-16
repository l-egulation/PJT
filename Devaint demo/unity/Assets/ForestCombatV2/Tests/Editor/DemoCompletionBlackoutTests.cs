using System;
using System.Linq;
using System.Reflection;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Integration;
using Deviant.Inventory.Unity;
using Deviant.Tutorial.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // Recreates the exit's completed black hold and the actual GUI's result handoff,
    // without requiring a boss fight or the complete stage-media download.
    public sealed class DemoCompletionBlackoutTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root, menu;
        MonoBehaviour gui;
        Chapter2World chapter;
        PlayerDeathPresentation output;
        DemoCompletionPanel demo;
        Canvas resultCanvas, black;
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);

        [SetUp] public void Setup()
        {
            root = new GameObject("Result blackout fixture");
            var dormant = new GameObject("Dormant chapter"); dormant.transform.SetParent(root.transform); dormant.SetActive(false);
            var arena = dormant.AddComponent<CombatArena>();
            output = dormant.AddComponent<PlayerDeathPresentation>();
            chapter = dormant.AddComponent<Chapter2World>();
            Set(chapter, "resultScope", new ResultScope());
            Set(chapter, "<Complete>k__BackingField", true);
            var host = dormant.AddComponent<InventoryRuntimeHost>(); Set(host, "<Arena>k__BackingField", arena);
            var guiType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Deviant.Game.TemporaryGameGui")).First(t => t != null);
            var node = new GameObject("Actual result GUI", typeof(RectTransform), typeof(Canvas)); node.transform.SetParent(root.transform);
            resultCanvas = node.GetComponent<Canvas>(); resultCanvas.renderMode = RenderMode.ScreenSpaceOverlay; resultCanvas.sortingOrder = 100;
            gui = (MonoBehaviour)node.AddComponent(guiType);
            Set(gui, "host", host); Set(gui, "chapter2", chapter);
            menu = new GameObject("Existing death result", typeof(RectTransform), typeof(Image)); menu.transform.SetParent(node.transform, false);
            menu.GetComponent<RectTransform>().sizeDelta = new Vector2(536, 312);
            Set(gui, "menu", menu);
            demo = DemoCompletionPanel.Create(node.transform, () => {}, () => {}); Set(gui, "demoCompletion", demo);
            var veil = new GameObject("Retained clear blackout", typeof(RectTransform), typeof(Canvas)); veil.transform.SetParent(root.transform);
            black = veil.GetComponent<Canvas>(); black.renderMode = RenderMode.ScreenSpaceOverlay; black.sortingOrder = 110;
            var fill = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); fill.transform.SetParent(veil.transform, false);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero; fill.color = Color.black; fill.raycastTarget = false;
            Set(output, "overlay", black);
            var sequence = output.GetType().GetField("sequence", Hidden).GetValue(output);
            sequence.GetType().GetMethod("BeginClear").Invoke(sequence, null);
        }

        [TearDown] public void Cleanup()
        {
            if (gui) Set(gui, "host", null);
            if (output) Set(output, "overlay", null);
            if (root) Object.DestroyImmediate(root);
        }

        void CompleteBlackHold()
        {
            var sequence = output.GetType().GetField("sequence", Hidden).GetValue(output);
            sequence.GetType().GetMethod("Advance").Invoke(sequence, new object[] { 3f, 0f });
            Assert.That(output.ReadyForResult, Is.True); Assert.That(output.FadeOpacity, Is.EqualTo(1));
            Assert.That(output.OverlayOrder, Is.EqualTo(110));
        }

        void Handoff(GameObject visible) => gui.GetType().GetMethod("RevealVisibleResult", Hidden).Invoke(gui, new object[] { visible });

        [TestCase(true)] [TestCase(false)]
        public void ClearDemoAndExistingDeathResultRenderAboveTheRetainedBlack(bool useDemo)
        {
            menu.SetActive(!useDemo); demo.gameObject.SetActive(useDemo);
            CompleteBlackHold();
            Assert.That(ReadCentre().maxColorComponent, Is.LessThan(.01f), "The exit's completed fade initially covers the result.");
            Handoff(useDemo ? demo.gameObject : menu);
            Assert.That(output.OverlayOrder, Is.LessThan(resultCanvas.sortingOrder), "The active result must rise above the completed exit blackout.");
            Assert.That(ReadCentre().maxColorComponent, Is.GreaterThan(.03f), "The result is visible rather than a full black image.");
        }

        [Test] public void IncompleteBlackHoldIsNotRevealedEarly()
        {
            Handoff(demo.gameObject); Assert.That(output.ReadyForResult, Is.False); Assert.That(output.OverlayOrder, Is.EqualTo(110));
        }
        [Test] public void GameplayBeforeResultDoesNotLowerTheBlackout()
        {
            CompleteBlackHold(); Set(chapter, "resultScope", null); Handoff(demo.gameObject); Assert.That(output.OverlayOrder, Is.EqualTo(110));
        }
        [Test] public void AnUnrelatedPanelCannotRevealTheResult()
        {
            CompleteBlackHold(); Handoff(root); Assert.That(output.OverlayOrder, Is.EqualTo(110));
        }

        Color ReadCentre()
        {
            var camera = new GameObject("Layer proof camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            var target = new RenderTexture(960, 540, 24); target.Create(); camera.targetTexture = target;
            var previous = RenderTexture.active;
            try
            {
                foreach (var canvas in new[] { resultCanvas, black }) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                var colour = image.GetPixel(480, 270); Object.DestroyImmediate(image); return colour;
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); }
        }

        sealed class ResultScope : IControlScope
        {
            public bool IsActive => true;
            public IControlLease BlockInput(InputChannels channels) => throw new NotSupportedException();
            public IControlLease PauseSimulation() => throw new NotSupportedException();
            public void Dispose() {}
        }
    }
}
