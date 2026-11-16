using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ForestCombatV2.Tests
{
    // The maintenance book's three tabs and the palette management spread at 960x540 into outputs/hub/capture for review
    // against the journal HTML (not a pixel test). Every visible glyph must exist in its font.
    public sealed class Chapter1MaintenanceGuiCaptureTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest] public IEnumerator CaptureTheMaintenanceScreensForReview()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            for(int frame=0;frame<900&&!Chapter1NativeEquipmentBookTests.WorldReady();frame++)yield return null;
            Assert.IsTrue(Chapter1NativeEquipmentBookTests.WorldReady());yield return null;yield return null;
            Chapter1NativeEquipmentBookTests.BindTestRig();
            Chapter1NativeEquipmentBookTests.CheckShop();
            yield return new ExitPlayMode();
        }

        // The combat HUD, the bag, the map, the pause menu and the death result in the journal style.
        [UnityTest] public IEnumerator CaptureTheCombatScreensForReview()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(world && world.Ready && world.GetComponent<Chapter1RunBinding>());) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Deviant.Inventory.Unity.InventoryRuntimeHost bag = null;
            for (int frame = 0; frame < 600 && (!bag || bag.Presenter == null); frame++) { yield return null; bag = Object.FindAnyObjectByType<Deviant.Inventory.Unity.InventoryRuntimeHost>(); }
            Assert.That(world && world.Ready && bag, Is.True);
            yield return null; yield return null;
            var offers = world.GetComponent<Chapter1StartingBlueprints>(); var player = world.arena.Player;
            player.Position = offers.OfferPosition(0); Assert.That(offers.TryChoose(0), Is.True);
            player.Position = offers.OfferPosition(1); Assert.That(offers.TryChoose(1), Is.True);
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            foreach (var id in new[] { "stat.speed", "stat.basic", "special.move", "stat.cooldown" }) bag.TryCollect(bag.SpawnDrop(id, player.Position, true));
            var run = world.GetComponent<Chapter1RunBinding>(); run.Run.Economy.Earn(240);
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            Refresh(gui); yield return null;
            Capture(world, gui, "combat-hud");
            bag.Presenter.Open(); Refresh(gui); yield return null;
            foreach (int slot in bag.Inventory.VisibleSlots(null)) if (bag.Inventory.BagAt(slot) != null) { bag.Presenter.SelectBag(slot); break; }
            Refresh(gui); yield return null;
            Capture(world, gui, "combat-bag");
            bag.Presenter.Close(); Refresh(gui);
            world.FullMap.SetOpen(true); Refresh(gui); yield return null; yield return null;
            Capture(world, gui, "combat-map");
            world.FullMap.SetOpen(false); Refresh(gui);
            bag.Presenter.TogglePause(); Refresh(gui); yield return null;
            Capture(world, gui, "combat-pause");
            bag.Presenter.TogglePause(); Refresh(gui);
            run.Host.Flow.TryDie(world.arena.Now); Refresh(gui); yield return null;
            Capture(world, gui, "combat-death");
            // The result says how far the run got (playtest 2026-11-04): explored rooms and the unmet boss.
            var stats = ((Text)gui.GetType().GetField("menuStats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gui)).text;
            StringAssert.Contains("탐험   ", stats); StringAssert.Contains(" / " + world.Data.rooms.Length + " 구역", stats);
            StringAssert.Contains("보스   만나지 못함", stats);
            yield return new ExitPlayMode();
        }

        // No lambda captures in the test enumerator: it survives the play mode domain reload, a closure would not.
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);

        internal static void Capture(ForestCombatWorld world, MonoBehaviour gui, string name, int width=960, int height=540)
        {
            var canvas = gui.GetComponent<Canvas>(); var camera = world.View;
            var mode = canvas.renderMode; var target = camera.targetTexture; var rect = camera.rect;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var fit = gui.GetType().GetMethod("FitToWorld");
            var bubbles = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b => b.GetType().Name == "MaintenanceBubbleView").ToArray();
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                foreach (var bubble in bubbles)
                {
                    var own = bubble.GetComponent<Canvas>(); own.renderMode = RenderMode.ScreenSpaceCamera; own.worldCamera = camera;
                    own.planeDistance = camera.nearClipPlane + .5f; bubble.SendMessage("Fit");
                }
                // Dynamic-font labels rasterise at the canvas scale, so rebuild them for the capture size.
                fit.Invoke(gui, null); foreach (var label in gui.GetComponentsInChildren<Text>()) label.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                foreach (var label in gui.GetComponentsInChildren<Text>())
                {
                    string text = System.Text.RegularExpressions.Regex.Replace(label.text, "<[^>]+>", "");
                    if (label.font.dynamic) label.font.RequestCharactersInTexture(text, label.fontSize, label.fontStyle);
                    foreach (char glyph in text)
                        Assert.That(char.IsWhiteSpace(glyph) || (label.font.dynamic ? label.font.GetCharacterInfo(glyph, out _, label.fontSize, label.fontStyle) : label.font.GetCharacterInfo(glyph, out _, label.font.fontSize)),
                            Is.True, $"missing glyph '{glyph}' in {name}: {label.text}");
                }
                camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/hub/capture");
                File.WriteAllBytes($"../outputs/hub/capture/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = null; camera.targetTexture = target; camera.rect = rect; RenderTexture.active = null;
                fit.Invoke(gui, null); RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
                foreach (var bubble in bubbles) { var own = bubble.GetComponent<Canvas>(); own.renderMode = RenderMode.ScreenSpaceOverlay; own.worldCamera = null; bubble.SendMessage("Fit"); }
            }
        }
    }
}
