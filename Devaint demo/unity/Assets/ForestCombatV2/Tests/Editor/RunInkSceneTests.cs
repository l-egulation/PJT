using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Inventory.Unity;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class RunInkSceneTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        static void Click(MonoBehaviour gui, string name)
        {
            var button = gui.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c && c.GetType().FullName == "UnityEngine.UI.Button" && c.name == name && c.gameObject.activeInHierarchy);
            Assert.That(button, Is.Not.Null, "active UI button: " + name);
            Assert.That((bool)button.GetType().GetProperty("interactable").GetValue(button), Is.True, name);
            ((UnityEvent)button.GetType().GetProperty("onClick").GetValue(button)).Invoke();
        }
        static bool SceneReady(ForestCombatWorld world, MonoBehaviour gui, InventoryRuntimeHost host)
        {
            if (!world || !world.Ready || !world.arena || !world.arena.Player) return false;
            if (world.arena.Sketch == null || !gui || !host || host.Presenter == null) return false;
            return gui.GetComponentsInChildren<Transform>(true).Any(t => t && t.name == "Inventory equipment book");
        }
        static void AssertLocked(SketchEquipment equipment, Guid weapon, string ink)
        { Assert.That(equipment.Weapons.Single(w => w.Id == weapon).Slots.All(s => s.Fixed), Is.True); Assert.Throws<InvalidOperationException>(() => equipment.Place(weapon, 0, ink)); }
        [UnityTest] public IEnumerator ChapterOneRewardConfiguresAndEquipsARealWeaponThroughTheInventoryButtons()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ForestCombatV2.Editor.DummyTrainingTools.SelectNormalMode();
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            Debug.Log("RunInkScene: loading chapter");
            SceneManager.LoadScene(Chapter1RunHost.ChapterScene);
            ForestCombatWorld world = null; MonoBehaviour gui = null; InventoryRuntimeHost host = null;
            float deadline = Time.realtimeSinceStartup + 60;
            while (Time.realtimeSinceStartup < deadline)
            {
                world = Object.FindAnyObjectByType<ForestCombatWorld>();
                gui = Object.FindObjectsByType<MonoBehaviour>().FirstOrDefault(c => c && c.GetType().FullName == "Deviant.Game.TemporaryGameGui");
                host = world && world.arena ? world.arena.GetComponent<InventoryRuntimeHost>() : null;
                if (SceneReady(world, gui, host)) break;
                yield return null;
            }
            Assert.That(world && world.Ready && gui && host, Is.True, "actual chapter and GUI initialized");
            var arena = world.arena;
            Assert.That(arena, Is.Not.Null, "chapter combat arena");
            var equipment = arena.Sketch;
            Assert.That(equipment, Is.Not.Null, "chapter sketch equipment");
            Debug.Log("RunInkScene: chapter ready");
            arena.Player.UseTestInput = true;
            Assert.That(equipment.RegisteredCount, Is.Zero); Assert.That(equipment.Weapons, Is.Empty);
            for (int i = 0; i < 20; i++) equipment.RecordKill(false);
            Assert.That(equipment.PendingReward, Is.False); Assert.That(equipment.OpenReward(), Is.True);
            Debug.Log("RunInkScene: reward requested");
            yield return null; yield return null;
            Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
            int offerIndex = equipment.RewardChoices().ToList().FindIndex(choice => choice.Weapon != null);
            var offered = equipment.RewardChoices()[offerIndex].Weapon;
            var rewardBinding = world.GetComponent<Chapter1RunBinding>().Sketch;
            Assert.That(rewardBinding.TryChooseReward(offerIndex), Is.True); yield return null;
            Debug.Log("RunInkScene: reward selected");
            rewardBinding.ConfigureReward(); yield return null;
            Debug.Log("RunInkScene: inventory opened");
            Assert.That(host.Presenter.IsOpen, Is.True);
            Click(gui, "Confirm sketch"); yield return null;
            Debug.Log("RunInkScene: configuration confirmed");
            Assert.That(offered.Confirmed, Is.True); Assert.That(offered.Slots.All(s => s.Ink == null && s.Fixed), Is.True);
            Assert.That(gui.GetComponentsInChildren<Transform>(true).Any(t => t && t.name == "Fill confirmed empty run ink" && t.gameObject.activeInHierarchy), Is.False);
            Click(gui, "Equip sketch 0"); yield return null;
            Debug.Log("RunInkScene: equipped");
            Assert.That(equipment.Equipped(0), Is.SameAs(offered)); Assert.That(arena.Player.weapons[0].InstanceId, Is.EqualTo(offered.Id));
            host.Presenter.Close(); yield return null; yield return null;
            Assert.That(arena.Controls.Current.SimulationPaused, Is.False, "closing configuration releases only the inventory/reward pauses");
            string ink = equipment.Catalog.inks[0].id; equipment.Register(ink);
            AssertLocked(equipment, offered.Id, ink);
            var rewardInkMethod = rewardBinding.GetType().GetMethod("InkItem", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rewardInkMethod, Is.Not.Null);
            var iconMethod = gui.GetType().GetMethod("RunInkIcon", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(iconMethod, Is.Not.Null, "color-mapped ink icon resolver");
            foreach (var definition in equipment.Catalog.inks)
            {
                var card = (SketchRewardChoiceScreen.Item)rewardInkMethod.Invoke(rewardBinding, new object[] { definition.id });
                Assert.That(card.ArtPath, Is.EqualTo(definition.icon), "main reward screen color icon: " + definition.id);
                Assert.That(card.Detail, Is.EqualTo(definition.description));
                var icon = iconMethod.Invoke(gui, new object[] { definition }) as Sprite;
                Assert.That(icon, Is.Not.Null, definition.id);
                Assert.That(icon.name, Is.EqualTo("ink-flat-" + definition.color), definition.id);
            }
        }
    }
}
