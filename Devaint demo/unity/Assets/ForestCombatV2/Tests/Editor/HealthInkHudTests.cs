using System.Collections;
using System.Linq;
using System.Reflection;
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
    // 전투 HUD 체력 게이지의 회복 붓질·보호막 칠·피해 지우기 연결(docs/development/presentation/ui/combat-hud-approved.md).
    public sealed class HealthInkHudTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetField(name, Hidden).GetValue(gui);
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);

        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator HealShieldAndDamageDrawOverTheRealGauge()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True); yield return null; yield return null;
            var player = world.arena.Player;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && Field<InventoryRuntimeHost>(b, "host"));
            var fill = Field<Image>(gui, "healthFill");
            using (world.arena.AcquireManualSimulation())
            {
                player.maxHealth = 100; player.health = 46; player.shield = 0; Refresh(gui);
                yield return new WaitForSecondsRealtime(1.5f); Refresh(gui);
                var image = Field<RawImage>(gui, "healthPlaqueImage");
                var plaque = Field<Deviant.Presentation.Unity.HealthPlaque>(gui, "healthPlaque");
                Assert.That(image.transform.parent, Is.SameAs(Field<RectTransform>(gui, "combatHealthRoot")), "the pixel plaque rides the hit shake");
                Assert.That(image.enabled && image.gameObject.activeInHierarchy, Is.True);
                Assert.That(fill.gameObject.activeSelf, Is.False, "the painted plaque is replaced");
                var portrait = Field<RectTransform>(gui, "combatHealthRoot").GetComponentsInChildren<Image>().Single(i => i.sprite && i.sprite.name == "hero-portrait");
                Assert.That(portrait.gameObject.activeInHierarchy, Is.True, "the approved portrait stays");
                Assert.That(portrait.transform.GetSiblingIndex(), Is.GreaterThan(image.transform.GetSiblingIndex()), "the portrait is drawn in front of the plaque");

                player.health = 72; Refresh(gui);
                Assert.That(fill.fillAmount, Is.EqualTo(.46f).Within(.001f), "the gauge waits for the brush");
                Assert.That(plaque.Ink.Animating, Is.True);
                Assert.That(plaque.Ink.GainAmount, Is.EqualTo(26));
                yield return new WaitForSecondsRealtime(.12f); Refresh(gui);
                Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "health-ink-heal-1920", 1920, 1080);
                yield return new WaitForSecondsRealtime(1.4f); Refresh(gui);
                Assert.That(fill.fillAmount, Is.EqualTo(.72f).Within(.001f), "the dried stroke folds into the gauge");
                Assert.That(plaque.Ink.Animating, Is.False);

                player.shield = 12; player.guardUntil = world.arena.Now + 30; Refresh(gui);
                Assert.That(plaque.Ink.Animating, Is.True, "a new shield is painted");
                yield return new WaitForSecondsRealtime(.6f); Refresh(gui);
                Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "health-ink-shield-1920", 1920, 1080);
                player.shield = 0; Refresh(gui);
                yield return new WaitForSecondsRealtime(1.2f); Refresh(gui);
                Assert.That(plaque.Ink.Animating, Is.False, "the broken shield unravels and clears");

                player.health = 50; Refresh(gui);
                Assert.That(fill.fillAmount, Is.EqualTo(.5f).Within(.001f), "damage drops the gauge at once");
                yield return new WaitForSecondsRealtime(.3f); Refresh(gui);
                Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "health-ink-damage-1920", 1920, 1080);
                yield return new WaitForSecondsRealtime(1.2f); Refresh(gui);
                Assert.That(plaque.Ink.Animating, Is.False);
            }
        }
    }
}
