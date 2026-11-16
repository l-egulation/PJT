using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class InventoryItemHitAreaTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator IconGapAndCaptionSelectTheSameItemWithoutHittingItsNeighbour()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var geometry in new[] {
                new float[] { 84, 137, 78, 88, 91 }, new float[] { 84, 137, 64, 88, 91 },
                new float[] { 112, 137, 78, 88, 91 }, new float[] { 80, 133, 60, 80, 88 },
                new float[] { 55, 112, 46, 44, 53 }, new float[] { 100.5f, 165.5f, 64.8f, 64.8f, 75 },
                new float[] { 126, 137.8f, 64.8f, 64.8f, 83 }, new float[] { 72.5f, 87.4f, 61.25f, 43.4f, 54.4f } })
                yield return CheckCard(geometry[0], geometry[1], geometry[2], geometry[3], geometry[4]);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator RebuiltSelectionWashIsMeasuredBeforeTheFirstRender()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var root = new GameObject("Selection render test", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var type = TypeCache.GetTypesDerivedFrom<Deviant.Inventory.Unity.InventoryPanelView>().Single(t => t.FullName == "Deviant.Game.TemporaryGameGui");
                var gui = root.AddComponent(type);
                var create = type.GetMethod("LiveButton", BindingFlags.Instance | BindingFlags.NonPublic);
                // Navigation rebuilds the active tab and its selected item together.
                // Render immediately after creation, without giving either component a LateUpdate.
                foreach (string title in new[] { "가방", "제작", "장비", "잉크" })
                {
                    var button = (Button)create.Invoke(gui, new object[] { "Rebuilt selected button", title, root.transform, 30f, 30f, 84f, 137f, (Action)(() => { }), true, true, 15 });
                    var wash = button.transform.Find("Approved selection").GetComponent<RawImage>();
                    Assert.That(wash.enabled, Is.False, "a newly built wash must not expose its default 100x100 rectangle");
                    Canvas.ForceUpdateCanvases();
                    Assert.That(wash.enabled, Is.True, "selection is available on the first render");
                    AssertWashMesh(wash);
                    Assert.That(wash.rectTransform.rect.height, Is.LessThan(50));
                    Assert.That(wash.rectTransform.rect.width, Is.LessThan(100));
                    button.GetComponentInChildren<Text>().text = "여러 줄로 길게 표시되는 잉크 이름";
                    Canvas.ForceUpdateCanvases();
                    float wide = wash.rectTransform.rect.width;
                    button.GetComponentInChildren<Text>().text = "검";
                    Canvas.ForceUpdateCanvases();
                    Assert.That(wash.rectTransform.rect.width, Is.LessThan(wide), "current text is measured before rendering, without a LateUpdate");
                    button.gameObject.SetActive(false);
                    Assert.That(wash.enabled, Is.False);
                    button.gameObject.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(wash.enabled, Is.True);
                    AssertWashMesh(wash);
                    Object.DestroyImmediate(button.gameObject);
                }
            }
            finally { Object.DestroyImmediate(root); }
            yield return new ExitPlayMode();
        }

        static void AssertWashMesh(RawImage wash)
        {
            var mesh = wash.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, "the first frame must contain drawable highlight geometry");
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), "an enabled image without geometry still produces a blank frame");
            Assert.That(mesh.bounds.size.x, Is.EqualTo(wash.rectTransform.rect.width).Within(.01f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(wash.rectTransform.rect.height).Within(.01f));
        }

        static IEnumerator CheckCard(float width, float height, float iconWidth, float iconHeight, float labelTop)
        {
            var root = new GameObject("Item hit area test", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var events = new GameObject("Item test events", typeof(EventSystem));
            var texture = new Texture2D(40, 70);
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var type = TypeCache.GetTypesDerivedFrom<Deviant.Inventory.Unity.InventoryPanelView>().Single(t => t.FullName == "Deviant.Game.TemporaryGameGui");
                var gui = root.AddComponent(type);
                var create = type.GetMethod("LiveItemButton", BindingFlags.Instance | BindingFlags.NonPublic);
                int selected = -1;
                var button = (Button)create.Invoke(gui, new object[] { "Item", "여러 줄로 표시되는 잉크 이름", texture, root.transform, 20f, 20f, width, height, iconWidth, iconHeight, labelTop, (Action)(() => selected = 0), true, 13 });
                var neighbour = (Button)create.Invoke(gui, new object[] { "Neighbour", "장비", texture, root.transform, 23f + width, 20f, width, height, iconWidth, iconHeight, labelTop, (Action)(() => selected = 1), false, 13 });
                yield return null;
                Canvas.ForceUpdateCanvases();
                var label = button.GetComponentInChildren<Text>().rectTransform;
                var icon = button.GetComponentsInChildren<RawImage>().Single(i => i.texture == texture).rectTransform;
                Assert.That(label.GetComponent<Text>().alignment, Is.EqualTo(TextAnchor.UpperCenter));
                Assert.That(icon.TransformPoint(icon.rect.center).x, Is.EqualTo(label.TransformPoint(label.rect.center).x).Within(.01f));
                var raycaster = root.GetComponent<GraphicRaycaster>();
                foreach (var local in new[] { new Vector2(width / 2, -iconHeight / 2), new Vector2(1, -1), new Vector2(width - 1, -(iconHeight + labelTop) / 2), new Vector2(width / 2, -labelTop - 8), new Vector2(width - 1, -height + 1) })
                {
                    var world = button.transform.TransformPoint(local);
                    var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { position = RectTransformUtility.WorldToScreenPoint(null, world), button = PointerEventData.InputButton.Left };
                    var hits = new List<RaycastResult>();
                    raycaster.Raycast(pointer, hits);
                    Assert.That(hits, Is.Not.Empty, "icon, gap, caption and card corners must be clickable");
                    var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                    Assert.That(target, Is.EqualTo(button.gameObject));
                    selected = -1;
                    ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
                    Assert.That(selected, Is.Zero);
                }
                var outside = button.transform.TransformPoint(new Vector2(width + 4, -height / 2));
                var nextPointer = new PointerEventData(events.GetComponent<EventSystem>()) { position = RectTransformUtility.WorldToScreenPoint(null, outside) };
                var nextHits = new List<RaycastResult>();raycaster.Raycast(nextPointer, nextHits);
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(nextHits[0].gameObject), Is.EqualTo(neighbour.gameObject));
            }
            finally { Object.DestroyImmediate(root);Object.DestroyImmediate(events);Object.DestroyImmediate(texture); }
        }
    }
}
