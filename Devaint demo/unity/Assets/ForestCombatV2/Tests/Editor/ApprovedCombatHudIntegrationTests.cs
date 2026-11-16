using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ApprovedCombatHudIntegrationTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour gui, string id) => (T)gui.GetType().GetField(id, Hidden).GetValue(gui);
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator ApprovedArtTracksLiveHealthEquipmentPalettesAndScreens()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            {
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready); yield return null; yield return null;
            var player = world.arena.Player;
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            var offers = world.GetComponent<Chapter1StartingBlueprints>();
            for (int i = 0; i < 2; i++) { player.Position = offers.OfferPosition(i); Assert.IsTrue(offers.TryChoose(i)); }
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(g => g.GetType().Name == "TemporaryGameGui" && g.GetType().GetField("host", Hidden).GetValue(g) != null);
            var input = Deviant.Combat.PlayerControls.For(player);
            input.Source.LoadBindingOverrides("{\"bindings\":[]}");
            player.health = 88; player.maxHealth = 100; Refresh(gui);
            var keys = Field<Text[,]>(gui, "combatKeys");
            Assert.That(new[] { keys[0,0].text, keys[1,0].text, keys[0,1].text, keys[1,1].text }, Is.EqualTo(new[] { "A", "S", "Q", "W" }));
            var basic = input.Source.GetAction(Deviant.Input.Contracts.GameAction.Weapon1Basic);
            int binding = Enumerable.Range(0, basic.bindings.Count).Single(i => basic.bindings[i].path == "<Keyboard>/a");
            input.Source.OverrideBinding(Deviant.Input.Contracts.GameAction.Weapon1Basic, binding, "<Keyboard>/z");
            Refresh(gui); Assert.That(keys[0,0].text, Is.EqualTo("Z"), "the HUD follows current settings instead of a fixed ASQW label");
            input.Source.LoadBindingOverrides("{\"bindings\":[]}"); Refresh(gui);

            Assert.AreEqual("health-fill-red", Field<Image>(gui, "healthFill").sprite.name);
            Assert.That(Field<Image>(gui, "healthFill").fillAmount, Is.EqualTo(.88f).Within(.001f));
            Assert.AreEqual("88", Field<Text>(gui, "healthText").text);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Font>("Assets/Deviant/UI/Fonts/Pretendard-Bold.ttf"), Field<Text>(gui, "healthText").font, "The approved typeface must not silently fall back to the pixel font.");
            var fillRect = Field<Image>(gui, "healthFill").rectTransform;
            Assert.That(fillRect.anchoredPosition.x + fillRect.sizeDelta.x, Is.EqualTo(495).Within(.5f), "a full bar reaches the plaque track's right end even after a layout edit shrank the fill");
            player.health = 110; player.maxHealth = 110; Refresh(gui);
            var current = Field<Text>(gui, "healthText"); var max = Field<Text>(gui, "healthMax");
            Assert.AreEqual(TextAnchor.UpperRight, current.alignment);
            float currentRight = current.rectTransform.anchoredPosition.x + current.rectTransform.sizeDelta.x * current.rectTransform.localScale.x;
            Assert.That(currentRight, Is.LessThan(max.rectTransform.anchoredPosition.x), "3-digit health ends before the slash");
            Assert.That(current.preferredWidth * current.rectTransform.localScale.x, Is.LessThan(current.rectTransform.sizeDelta.x * current.rectTransform.localScale.x), "the value fits its right-aligned box");
            Assert.That(current.rectTransform.anchoredPosition.y, Is.EqualTo(max.rectTransform.anchoredPosition.y).Within(.01f), "both numbers share one top line");
            player.health = 88; player.maxHealth = 100; Refresh(gui);
            Assert.IsTrue((bool)gui.GetType().GetProperty("RunPaletteShowing").GetValue(gui));
            var icons = Field<Image[]>(gui, "weaponIcons");
            Assert.That(icons[0].rectTransform.sizeDelta.x, Is.EqualTo(70).Within(.01f));
            Assert.That(icons[1].rectTransform.sizeDelta, Is.EqualTo(icons[0].rectTransform.sizeDelta));
            var artwork = Field<RectTransform>(gui, "combatArtwork");
            Assert.That(artwork.InverseTransformPoint(icons[1].transform.position).x - artwork.InverseTransformPoint(icons[0].transform.position).x, Is.EqualTo(280).Within(.01f));
            Assert.AreEqual("hero-portrait", gui.GetComponentsInChildren<Image>().First(i => i.sprite && i.sprite.name == "hero-portrait").sprite.name);
            Assert.AreEqual("minimap-frame-compact", gui.GetComponentsInChildren<Image>().First(i => i.name == "Minimap").sprite.name);
            Capture(world, gui, "approved-hud-and-departure", 1920, 1080);
            // A real charge pool remains independent of which weapon is selected.
            var pool = player.weapons[0].Cooldown; Assert.IsTrue(pool.Spend(world.arena.Now)); Refresh(gui);
            Assert.IsTrue(Field<Image[]>(gui, "weaponShades")[0].enabled);
            Assert.AreEqual("ring-cooldown", Field<Image[]>(gui, "weaponShades")[0].sprite.name);
            Assert.IsFalse(Field<Image[]>(gui, "weaponShades")[1].enabled);
            // Read the currently implemented catalog; changing equipment never changes HUD geometry.
            player.MaintenanceSite = () => true;
            int launcher = System.Array.FindIndex(world.arena.catalog.forms, f => f.id == "launcher");
            Assert.That(launcher, Is.GreaterThanOrEqualTo(0));
            string[] art = { "weapon-launcher", "weapon-launcher-laser", "weapon-launcher-shotgun", "weapon-launcher-grenade" };
            string[] skillIds = { "launcher_basic_special", "launcher_range_special", "launcher_move_special", "launcher_followup_special" };
            string[] skillArt = { "launcher-ball", "launcher-laser", "launcher-shotgun", "launcher-grenade" };
            float[] periods = { 6, 8, 7, 9 };
            player.UseTestInput = true; player.selected = 0;
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(player.TryReconfigure(0, launcher, i, out var error), error); Refresh(gui);
                // Maintenance preserves previous cooldown debt. Each independent fixture starts ready.
                player.Weapon.Cooldown.Reset(player.Special.capacity, player.Weapon.CooldownPeriod(player.Special.cooldown));
                Assert.AreEqual(art[i], icons[0].sprite.name); Assert.That(icons[0].rectTransform.sizeDelta.x, Is.EqualTo(70).Within(.01f));
                Assert.AreEqual(skillIds[i], player.Special.id);
                Assert.AreEqual(skillArt[i], Field<Image[]>(gui, "combatSpecial")[0].sprite.name);
                // Every equipped skill still fires the same basic ball without consuming skill cooldown.
                player.QueueTestInput(basic: true); yield return Advance(world, .45f); Refresh(gui);
                Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1));
                if (i == 0)
                {
                    player.specialHeld = true; player.QueueTestInput(special: true);
                    yield return Advance(world, .35f); Refresh(gui);
                    Assert.IsTrue(Field<Image[]>(gui, "combatProgress")[0].enabled, "real charge drives the HUD ring");
                    Assert.That(Field<Image[]>(gui, "combatProgress")[0].fillAmount, Is.InRange(.2f, .8f));
                    Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1), "charging has not spent cooldown yet");
                    Assert.IsFalse(Field<Image[]>(gui, "weaponShades")[0].enabled);
                    Capture(world, gui, "launcher-charge-live", 1920, 1080);
                    yield return Advance(world, .8f); player.specialHeld = false;
                }
                else { player.QueueTestInput(special: true); yield return Advance(world, 1.2f); }
                Refresh(gui);
                Assert.That(player.Weapon.Cooldown.Available, Is.Zero);
                Assert.That(player.Weapon.Cooldown.Period, Is.EqualTo(periods[i]).Within(.001f));
                Assert.IsTrue(Field<Image[]>(gui, "weaponShades")[0].enabled);
                Assert.IsFalse(Field<Image[]>(gui, "combatProgress")[0].enabled);
                Assert.IsFalse(Field<Image[]>(gui, "weaponShades")[1].enabled);
                Capture(world, gui, "launcher-skill-" + i + "-cooldown", 1920, 1080);
            }
            var host = (InventoryRuntimeHost)gui.GetType().GetField("host", Hidden).GetValue(gui);
            Field<Button>(gui, "bagHud").onClick.Invoke(); Assert.IsTrue(host.Presenter.IsOpen); gui.GetType().GetMethod("Back").Invoke(gui, null);
            gui.GetComponentsInChildren<Button>().First(b => b.name == "Pause").onClick.Invoke(); Assert.IsTrue(host.Presenter.Paused); gui.GetType().GetMethod("Back").Invoke(gui, null);
            gui.GetComponentsInChildren<Button>().First(b => b.name == "Minimap").onClick.Invoke(); Assert.IsTrue(world.FullMap.IsOpen); gui.GetType().GetMethod("Back").Invoke(gui, null);
            Assert.IsFalse(world.FullMap.IsOpen);
            yield return new WaitForSecondsRealtime(3.6f); Refresh(gui);
            Assert.IsFalse((bool)gui.GetType().GetProperty("RunPaletteShowing").GetValue(gui));
            Capture(world, gui, "approved-combat-hud-1920", 1920, 1080);
            // Exercise real expanded palettes, including the colourless slot; no saved profile is changed.
            var growth = world.GetComponent<Chapter1RunBinding>().Run.Growth;
            var drops = Field<Image[,]>(gui, "combatDrops");
            for (int count = 4; count <= 5; count++)
            {
                for (int w = 0; w < 2; w++)
                {
                    string[] colours = { "colorless", "purple", "green", "yellow", "red" };
                    var palette = growth.GrantPalette(colours.Take(count).ToArray()); growth.Bind(palette.Id, player.weapons[w].InstanceId);
                }
                gui.GetType().GetField("runPaletteShownFor", Hidden).SetValue(gui, null); Refresh(gui);
                var reveal = Field<Image[,]>(gui, "runPaletteDrops");
                for (int w = 0; w < 2; w++) for (int i = 0; i < 5; i++)
                {
                    Assert.AreEqual(i < count, drops[w, i].gameObject.activeSelf);
                    Assert.AreEqual(i < count, reveal[w, i].gameObject.activeSelf);
                    if (i < count) Assert.AreNotEqual("state-unavailable", drops[w, i].sprite.name);
                }
                Capture(world, gui, "approved-palette-" + count + "-slots", 1920, 1080);
                // The HUD follows actual mounted ink, not just the palette's fixed colour.
                for (int w = 0; w < 2; w++)
                {
                    var drop = drops[w, 1]; var weaponId = player.weapons[w].InstanceId;
                    Assert.AreEqual("backing-ink-purple", drop.sprite.name);
                    Assert.That(drop.color.a, Is.EqualTo(.4f).Within(.001f));
                    var firstInk = growth.GrantInk("ink3_purple_05"); growth.Insert(weaponId, 1, firstInk.Id); Refresh(gui);
                    Assert.AreSame(Resources.Load<Sprite>("ApprovedItemIcons/ink3_purple_05"), drop.sprite);
                    Assert.AreEqual(Color.white, drop.color);
                    var replacement = growth.GrantInk("ink3_purple_01"); growth.Insert(weaponId, 1, replacement.Id); Refresh(gui);
                    Assert.AreSame(Resources.Load<Sprite>("ApprovedItemIcons/ink3_purple_01"), drop.sprite);
                    Assert.AreEqual(Color.white, drop.color);
                    Capture(world, gui, "approved-hud-mounted-ink-" + count + "-weapon-" + w, 1920, 1080);
                    growth.Destroy(firstInk.Id); var detached = growth.Detach(weaponId, 1); Refresh(gui);
                    Assert.AreEqual("backing-ink-purple", drop.sprite.name);
                    Assert.That(drop.color.a, Is.EqualTo(.4f).Within(.001f));
                    growth.Destroy(detached.Id);
                }
            }
            // Non-integer window sizes use the native viewport's centred safe frame.
            Capture(world, gui, "approved-hud-layout-1280", 1280, 720);
        }
        static IEnumerator Advance(ForestCombatWorld world, float seconds)
        {
            float end = world.arena.Now + seconds, deadline = Time.realtimeSinceStartup + 15;
            while (world.arena.Now < end)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return new WaitForFixedUpdate();
            }
            }
        }
        static void Capture(ForestCombatWorld world, MonoBehaviour gui, string name, int width, int height)
        {
            var canvas = gui.GetComponent<Canvas>(); var camera = world.View;
            var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; var target = camera.targetTexture; var rect = camera.rect;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                gui.GetType().GetMethod("FitToWorld").Invoke(gui, null);
                Assert.That(canvas.scaleFactor, Is.EqualTo(Stage1VillageExperiment.VillagePixelViewport.CalculateDisplayRect(width, height).width / 960f).Within(.001f));
                foreach (var label in gui.GetComponentsInChildren<Text>()) label.SetAllDirty(); Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/combat-hud-unity"); File.WriteAllBytes("../outputs/combat-hud-unity/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = oldCamera; camera.targetTexture = target; camera.rect = rect; RenderTexture.active = null;
                gui.GetType().GetMethod("FitToWorld").Invoke(gui, null); RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
            }
        }
    }
}
