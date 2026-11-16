using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // A spent skill charge in chapter 1: a grey wedge over the whole weapon card and the seconds left until the skill is back.
    public sealed class Chapter1SkillCooldownHudTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(MonoBehaviour gui, string name) => (T)gui.GetType().GetField(name, Hidden).GetValue(gui);
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);
        // Handed out of the helper coroutines through statics: the test enumerator survives the play mode domain reload.
        static float elapsed;

        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator UsingASkillShowsItsCooldownOnTheWeaponCardUntilItIsReady()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready && world.GetComponent<Chapter1RunBinding>()); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready); yield return null; yield return null;
            // Runs now depart unarmed and take weapons from sketch rewards (docs/design/equipment/sketch-weapons.md),
            // so the start-room blueprints are already complete. Equip two sketch weapons the way a reward does.
            var player = world.arena.Player; var sketch = world.arena.Sketch;
            Assert.That(sketch, Is.Not.Null, "chapter runs use sketch weapons");
            Assert.IsTrue(world.GetComponent<Chapter1StartingBlueprints>().Complete, "the unarmed departure offers no start-room blueprints");
            for (int w = 0; w < 2; w++) Assert.That(sketch.DebugConfigureWeapon(w, "base_sword", 0), Is.Not.Null);
            world.arena.SyncSketchLoadout(); yield return null;
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            player.maxHealth = player.health = 100000; player.UseTestInput = true;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            int slot = player.weapons.ActiveSlot;
            var shade = Field<Image[]>(gui, "weaponShades")[slot]; var seconds = Field<Text[]>(gui, "weaponSeconds")[slot];
            Refresh(gui);
            Assert.IsFalse(shade.enabled, "a ready skill has no shade"); Assert.AreEqual("", seconds.text);

            // Stand beside the nearest enemy, facing it, so target-seeking specials have someone to hit.
            Deviant.Combat.TestEnemy target = null;
            foreach (var e in world.arena.enemies) if (e && !e.Dead && (!target || (e.Position - player.Position).sqrMagnitude < (target.Position - player.Position).sqrMagnitude)) target = e;
            player.Position = target.Position + Vector2.left * 1.2f; player.facing = 1; Physics2D.SyncTransforms(); yield return null;
            var cooldown = player.weapons[slot].Cooldown; Assert.AreEqual(slot, player.weapons.ActiveSlot);
            // The HUD reads the weapon's own charge pool, whatever spent it: spend its only charge as a skill does.
            Assert.IsTrue(cooldown.Spend(world.arena.Now));
            Assert.AreEqual(0, cooldown.Available);
            Refresh(gui); yield return null;
            float left = cooldown.NextReady - world.arena.Now;
            Assert.IsTrue(shade.enabled); Assert.That(shade.fillAmount, Is.EqualTo(left / cooldown.Period).Within(.05f));
            Assert.AreEqual(Mathf.CeilToInt(left).ToString(), seconds.text);
            Assert.IsFalse(Field<Image[]>(gui, "weaponShades")[1 - slot].enabled, "the other weapon keeps its own cooldown");
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "skill-cooldown-start");

            yield return WaitUntilLeft(world, cooldown, 2.1f);
            Refresh(gui); Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "skill-cooldown-mid");
            Assert.That(shade.fillAmount, Is.LessThan(.5f));
            yield return WaitUntilLeft(world, cooldown, .55f);
            Refresh(gui); Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "skill-cooldown-end");
            StringAssert.StartsWith("0.", seconds.text, "under a second shows tenths");
            yield return WaitUntilLeft(world, cooldown, 0);
            // The pool takes the charge back on its next combat tick.
            for (int f = 0; f < 60 && cooldown.Available == 0; f++) yield return null;
            Refresh(gui);
            Assert.AreEqual(1, cooldown.Available); Assert.IsFalse(shade.enabled); Assert.AreEqual("", seconds.text);
            Assert.AreEqual(Color.white, Field<Image[]>(gui, "weaponIcons")[slot].color, "the weapon art is back to full");
        }

        static IEnumerator WaitUntilLeft(ForestCombatWorld world, Deviant.Combat.ChargePool cooldown, float seconds)
        {
            for (elapsed = 0; elapsed < 20 && cooldown.Available == 0 && cooldown.NextReady - world.arena.Now > seconds; elapsed += Time.deltaTime) yield return null;
            Assert.Less(elapsed, 20f, "the cooldown kept running");
        }
    }
}
