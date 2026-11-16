using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The sketch reward gauge under the health plaque (buff row moves down 8) and the pixel durability arc on each weapon disc.
    public sealed class SketchHudPixelMarksTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour gui, string id) => (T)gui.GetType().GetField(id, Hidden).GetValue(gui);
        [UnityTearDown] public IEnumerator Cleanup() { SketchRunBinding.DurabilityAsNumber = false; if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test] public void BankedRewardPromptPulsesOnlyForTheFirstFiveSeconds()
        {
            var type=System.Type.GetType("Deviant.Game.TemporaryGameGui, Assembly-CSharp");
            var method=type.GetMethod("RewardPromptGlowAlpha",BindingFlags.Static|BindingFlags.NonPublic);
            float Alpha(float age)=>(float)method.Invoke(null,new object[]{age});
            Assert.That(Alpha(0),Is.GreaterThan(0));Assert.That(Alpha(.25f),Is.LessThan(Alpha(0)));
            Assert.That(Alpha(4.99f),Is.GreaterThan(0));Assert.That(Alpha(5),Is.Zero);Assert.That(Alpha(20),Is.Zero);
        }
        static Chapter1RunBinding ReadyBinding()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var binding = Object.FindAnyObjectByType<Chapter1RunBinding>();
            return world && world.Ready && binding && binding.Sketch ? binding : null;
        }

        static IEnumerator TakeWeapon(SketchRunState state, int slot)
        {
            SketchWeapon got = null;
            state.DebugAddReward(); state.OpenBanked(); yield return null;
            for (int guard = 0; guard < 4 && state.PendingOffer != null; guard++)
            {
                int pick = 0;
                for (int i = 0; i < state.PendingOffer.Length; i++) if (!state.PendingOffer[i].IsInk) { pick = i; break; }
                var weapon = state.Choose(pick); if (weapon != null) got = weapon;
                yield return null; yield return null;
            }
            Assert.That(got, Is.Not.Null);
            state.Arsenal.Confirm(got.Id); state.Arsenal.Equip(got.Id, slot);
            yield return null; yield return null;
        }

        static Rect CanvasRect(MonoBehaviour gui, RectTransform rect)
        {
            var canvas = (RectTransform)Field<GameObject>(gui, "hud").transform;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 a = canvas.InverseTransformPoint(corners[0]), b = canvas.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        [UnityTest] public IEnumerator GaugeSitsUnderThePlaqueAndDurabilityRingsTheWeaponDisc()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            SceneManager.LoadScene(Chapter1MapSelector.ProceduralScene);
            for (int frame = 0; frame < 900 && !ReadyBinding(); frame++) yield return null;
            yield return null; yield return null;
            var binding = ReadyBinding(); Assert.That(binding, Is.Not.Null);
            var world = binding.World; var arena = world.arena; var state = binding.Sketch.State;
            foreach (var enemy in arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(g => g.GetType().Name == "TemporaryGameGui" && g.GetType().GetField("host", Hidden).GetValue(g) != null);

            yield return TakeWeapon(state, SketchArsenal.SlotA);
            yield return TakeWeapon(state, SketchArsenal.SlotB);
            // Weapon B: three slots, two red enhanced and one neutral, all empty.
            var paletteWeapon = state.Arsenal.Equipped(SketchArsenal.SlotB);
            arena.Sketch.DebugResizePalette(paletteWeapon.Id, 3);
            arena.Sketch.DebugSetSlotColor(paletteWeapon.Id, 0, "red"); arena.Sketch.DebugSetSlotColor(paletteWeapon.Id, 1, "red");
            arena.Sketch.DebugSetSlotColor(paletteWeapon.Id, 2, "neutral");
            int killed = 0;
            foreach (var enemy in arena.enemies)
            {
                if (killed >= 2) break;
                if (!enemy || enemy.Dead || Chapter1EnemyRole.Of(enemy) != Chapter1EnemyRole.Normal) continue;
                enemy.health = 0; killed++; yield return null; yield return null;
            }
            state.DebugAddReward();
            var used = typeof(SketchWeapon).GetProperty("DurabilityUsed");
            var a = state.Arsenal.Equipped(SketchArsenal.SlotA); var b = state.Arsenal.Equipped(SketchArsenal.SlotB);
            used.SetValue(a, a.MaxDurability * .38f); used.SetValue(b, b.MaxDurability * .8f);
            for (int i = 0; i < 4; i++) yield return null;
            gui.GetType().GetMethod("Refresh").Invoke(gui, null);

            // Gauge strip: a pixel image at whole game pixels (95, 89) on the 960×540 canvas, filled by the live gauge.
            var gauge = Field<RawImage>(gui, "rewardGaugeImage");
            Assert.That(gauge.gameObject.activeInHierarchy, Is.True);
            Assert.That(gauge.texture.filterMode, Is.EqualTo(FilterMode.Point));
            var strip = Field<Deviant.Presentation.Unity.RewardGaugeStrip>(gui, "rewardGauge");
            Assert.That(strip.Filled, Is.EqualTo(Mathf.FloorToInt(state.GaugeFraction * Deviant.Presentation.Unity.RewardGaugeStrip.BarLength)));
            var gaugeRect = CanvasRect(gui, gauge.rectTransform);
            Assert.That(gaugeRect.width, Is.EqualTo(Deviant.Presentation.Unity.RewardGaugeStrip.Width).Within(.01f)); Assert.That(gaugeRect.height, Is.EqualTo(Deviant.Presentation.Unity.RewardGaugeStrip.Height).Within(.01f));
            Assert.That(gaugeRect.xMin, Is.EqualTo(Mathf.Round(gaugeRect.xMin)).Within(.01f), "the strip sits on whole game pixels");
            var plaque = CanvasRect(gui, Field<RawImage>(gui, "healthPlaqueImage").rectTransform);
            // Plaque texture at (49, 32) 276×60 (drawn bottom edge y 86); strip at (95, 86) 250×17 (top 3 rows for the token pop).
            Assert.That(gaugeRect.xMin - plaque.xMin, Is.EqualTo(46).Within(.01f));
            Assert.That(Mathf.Abs(gaugeRect.center.y - plaque.center.y), Is.EqualTo(32.5f).Within(.01f));
            Assert.That(gui.GetType().GetProperty("RewardGaugeShown", Hidden).GetValue(gui), Is.True, "the buff row moves down 8 while the gauge shows");

            // Banking one more reward plays the strip flash (white bar, sweep, token pop, G keycap rim) and the yellow edge glow.
            state.DebugAddReward(); gui.GetType().GetMethod("Refresh").Invoke(gui, null);
            Assert.That(strip.FlashFrame, Is.GreaterThanOrEqualTo(0), "the strip flashes when a reward is banked");
            Assert.That(Field<RawImage>(gui, "rewardEdgeGlow").enabled, Is.True, "the screen edge glows yellow");
            var rewardPrompt=Field<GameObject>(gui,"bankedRewardPrompt");
            Assert.That(rewardPrompt.activeSelf,Is.True,"banked rewards advertise G on the combat HUD");
            Assert.That(Field<Text>(gui,"bankedRewardCaption").color,Is.EqualTo(Color.white));
            Assert.That(Field<Outline>(gui,"bankedRewardCaptionGlow").enabled,Is.True);
            var flashShots = new System.Collections.Generic.List<Texture2D>(); float flashStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - flashStart < 1.1f && flashShots.Count < 30) { flashShots.Add(Capture(world, gui, null)); yield return null; }
            for (int k = 0; k < flashShots.Count; k++) { File.WriteAllBytes("../outputs/combat-hud-unity/reward-flash-" + k.ToString("00") + ".png", flashShots[k].EncodeToPNG()); Object.Destroy(flashShots[k]); }
            gui.GetType().GetMethod("Refresh").Invoke(gui, null);
            Assert.That(strip.FlashFrame, Is.EqualTo(-1), "the flash ends");
            Assert.That(Field<RawImage>(gui, "rewardEdgeGlow").enabled, Is.False, "the glow fades out");

            typeof(Deviant.Presentation.Unity.RewardGaugeStrip).GetField("flashAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(strip,Time.unscaledTime-5.1f);
            gui.GetType().GetMethod("Refresh").Invoke(gui,null);
            Assert.That(rewardPrompt.activeSelf,Is.True,"white reward prompt remains while a reward is available");
            Assert.That(Field<Outline>(gui,"bankedRewardCaptionGlow").enabled,Is.False,"only the yellow light ends after five seconds");
            // Durability arcs: active in gauge mode, centred on whole pixels on the disc, behind the disc.
            var arcs = Field<RawImage[]>(gui, "durabilityArcImages"); var discs = Field<Image[]>(gui, "weaponCards");
            for (int w = 0; w < 2; w++)
            {
                Assert.That(arcs[w].gameObject.activeInHierarchy, Is.True);
                Assert.That(arcs[w].transform.GetSiblingIndex(), Is.LessThan(discs[w].transform.GetSiblingIndex()));
                var arcRect = CanvasRect(gui, arcs[w].rectTransform); var disc = CanvasRect(gui, discs[w].rectTransform);
                Assert.That(Vector2.Distance(arcRect.center, disc.center), Is.LessThanOrEqualTo(.71f));
                Assert.That(arcRect.xMin, Is.EqualTo(Mathf.Round(arcRect.xMin)).Within(.01f));
                Assert.That(arcRect.width, Is.EqualTo(Field<Deviant.Presentation.Unity.DurabilityArc[]>(gui, "durabilityArcs")[w].Size).Within(.01f));
            }
            // Empty palette slots show their own colour as a blob outline (enhanced colour, or ink for neutral).
            var pixelDrops = Field<Image[,]>(gui, "pixelDrops"); var drops = Field<Image[,]>(gui, "combatDrops");
            string[] expected = { "slot-empty-red", "slot-empty-red", "slot-empty-neutral" };
            for (int j = 0; j < 3; j++)
            {
                Assert.That(pixelDrops[1, j].enabled && pixelDrops[1, j].gameObject.activeInHierarchy, Is.True, "slot " + j);
                Assert.That(pixelDrops[1, j].sprite.name, Is.EqualTo(expected[j]));
                Assert.That(drops[1, j].enabled, Is.False, "the faint approved empty-slot art is replaced");
                var dropRect = CanvasRect(gui, pixelDrops[1, j].rectTransform);
                Assert.That(dropRect.width, Is.EqualTo(Deviant.Presentation.Unity.HudPixelKit.BlobWidth).Within(.01f)); Assert.That(dropRect.xMin, Is.EqualTo(Mathf.Round(dropRect.xMin)).Within(.01f));
                if (j > 0) Assert.That(dropRect.center.x - CanvasRect(gui, pixelDrops[1, j - 1].rectTransform).center.x, Is.EqualTo(Deviant.Presentation.Unity.HudPixelKit.BlobStep).Within(1.01f), "slots sit 22 game pixels apart");
            }
            Assert.That(drops[1, 3].gameObject.activeSelf, Is.False, "only the weapon's own three slots show");
            // Pixel weapon/skill icons: drawn 48×48 / 44×44 game pixels, centred on their discs on whole pixels, 2× textures.
            var icons = Field<Image[]>(gui, "weaponIcons"); var specials = Field<Image[]>(gui, "combatSpecial"); var specialDiscs = Field<Image[]>(gui, "combatSpecialDiscs");
            for (int w = 0; w < 2; w++)
            {
                foreach (var (image, disc, size) in new[] { (icons[w], discs[w], 48), (specials[w], specialDiscs[w], 44) })
                {
                    var r = CanvasRect(gui, image.rectTransform); var d = CanvasRect(gui, disc.rectTransform);
                    Assert.That(r.width, Is.EqualTo(size).Within(.01f), image.sprite.name); Assert.That(r.height, Is.EqualTo(size).Within(.01f));
                    Assert.That(r.xMin, Is.EqualTo(Mathf.Round(r.xMin)).Within(.01f)); Assert.That(r.yMin, Is.EqualTo(Mathf.Round(r.yMin)).Within(.01f));
                    Assert.That(Vector2.Distance(r.center, d.center), Is.LessThanOrEqualTo(.71f), "the icon is centred on its disc");
                    Assert.That(image.sprite.texture.width, Is.EqualTo(size * 2), "the pixel icon is the 2× nearest-neighbour export");
                }
            }
            Capture(world, gui, "sketch-hud-gauge-and-arcs");
            SketchRunBinding.DurabilityAsNumber = true; gui.GetType().GetMethod("Refresh").Invoke(gui, null);
            Assert.That(arcs[0].gameObject.activeSelf, Is.False, "number mode hides the arc");
            SketchRunBinding.DurabilityAsNumber = false; gui.GetType().GetMethod("Refresh").Invoke(gui, null);

            // Breaking weapon B by durability: the slot turns unarmed at once, a ghost of the weapon group wipes away on top
            // (behind its keycaps and palette) and the hand bursts. Both clean themselves up.
            // Writing the flash PNGs above takes most of a second inside one frame; start the break on a fresh frame.
            yield return null; yield return null;
            used.SetValue(b, b.MaxDurability - 1);
            Assert.That(state.Arsenal.ChargeDurability(b.Id, 50), Is.True, "the weapon breaks");
            Assert.That(gui.GetType().GetProperty("ActiveBreakGhosts").GetValue(gui), Is.EqualTo(1));
            var burst = Object.FindAnyObjectByType<Deviant.Presentation.Unity.WeaponBreakBurst>();
            Assert.That(burst, Is.Not.Null, "the hand burst plays");
            // Frames go to memory first (a PNG encode per frame is slower than the 60 ms effect step).
            var shots = new System.Collections.Generic.List<Texture2D>(); float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1.6f && shots.Count < 40) { shots.Add(Capture(world, gui, null)); yield return null; }
            for (int k = 0; k < shots.Count; k++) { File.WriteAllBytes("../outputs/combat-hud-unity/weapon-break-" + k.ToString("00") + ".png", shots[k].EncodeToPNG()); Object.Destroy(shots[k]); }
            for (int k = 0; k < 10; k++) yield return null;
            gui.GetType().GetMethod("Refresh").Invoke(gui, null);
            Assert.That(gui.GetType().GetProperty("ActiveBreakGhosts").GetValue(gui), Is.EqualTo(0), "the ghost is gone after the wipe");
            Assert.That(Object.FindAnyObjectByType<Deviant.Presentation.Unity.WeaponBreakBurst>(), Is.Null, "the burst is gone");
            // The broken slot is unarmed: the fist icon replaces the old 「맨손」 label.
            Assert.That(icons[1].enabled, Is.True); Assert.That(icons[1].sprite.name, Is.EqualTo("weapon-unarmed"));
            Assert.That(Field<Text[]>(gui, "combatUnarmedNames")[1].text, Is.Empty);
            Capture(world, gui, "sketch-hud-unarmed-fist");
            yield return new ExitPlayMode();
        }

        static Texture2D Capture(ForestCombatWorld world, MonoBehaviour gui, string name)
        {
            var canvas = gui.GetComponent<Canvas>(); var camera = world.View;
            var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; var target = camera.targetTexture; var rect = camera.rect;
            var rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                gui.GetType().GetMethod("FitToWorld").Invoke(gui, null);
                gui.GetType().GetMethod("Refresh").Invoke(gui, null);
                foreach (var label in gui.GetComponentsInChildren<Text>()) label.SetAllDirty(); Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/combat-hud-unity");
                if (name != null) File.WriteAllBytes("../outputs/combat-hud-unity/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = oldCamera; camera.targetTexture = target; camera.rect = rect; RenderTexture.active = null;
                gui.GetType().GetMethod("FitToWorld").Invoke(gui, null); RenderTexture.ReleaseTemporary(rt);
                if (name != null) Object.Destroy(image);
            }
            return name == null ? image : null;
        }
    }
}
