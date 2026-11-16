using System.Collections;
using System.IO;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Inventory.Tests
{
    public class InventoryBootstrapTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator ActualSceneStartupInstallsInventoryAndViewReplacementRetainsItems()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode();
            var arena = Object.FindAnyObjectByType<CombatArena>();
            float deadline = Time.realtimeSinceStartup + 5;
            InventoryRuntimeHost host = null;
            while (host == null || host.Inventory == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Scene startup must install inventory without manually attaching components.");
                host = arena.GetComponent<InventoryRuntimeHost>(); yield return null;
            }
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            Assert.That(arena.GetComponent("InventoryGameBinding"), Is.Not.Null);
            var item = new InkInstance(host.Content.Find("stat.basic").CreateDefinition()); host.Inventory.TryAcquire(item, out _);
            host.Inventory.TryAcquire(new InkInstance(item.Definition), out _);
            host.Presenter.Open();
            yield return null;
            var view = arena.GetComponentInChildren<TemporaryInventoryView>();
            Assert.That(view.window.activeSelf, Is.True); Assert.That(view.bagButtons.Length, Is.EqualTo(15));
            CaptureView(view);
            yield return null; yield return null;
            Object.Destroy(view.gameObject); yield return null;
            var replacement = new GameObject("Replacement GUI").AddComponent<TemporaryInventoryView>();
            replacement.transform.SetParent(arena.transform); replacement.Bind(host); host.Presenter.Open(); replacement.Refresh();
            Assert.That(host.Inventory.BagAt(0), Is.SameAs(item)); Assert.That(host.Inventory.Used, Is.EqualTo(2));
            replacement.bagButtons[0].onClick.Invoke(); replacement.equipButton.onClick.Invoke();
            Assert.That(arena.Player.weapons[0].BasicBonus, Is.EqualTo(.2f));
            Assert.That(host.Inventory.EquippedAt(0, 1), Is.SameAs(item));
        }
        static void CaptureView(TemporaryInventoryView view)
        {
            var canvas = view.GetComponent<Canvas>(); var camera = Camera.main;
            var target = new RenderTexture(1600, 900, 24); var previous = RenderTexture.active;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../inventory-preview.png")), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Object.Destroy(image); Object.Destroy(target);
            }
        }
    }
}
