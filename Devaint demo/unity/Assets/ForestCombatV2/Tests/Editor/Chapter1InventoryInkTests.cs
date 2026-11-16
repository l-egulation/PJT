using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Inventory;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ForestCombatV2.Tests
{
    // The bag equips, replaces and destroys colour inks during a paused run.
    public sealed class Chapter1InventoryInkTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);
        static void Back(MonoBehaviour gui) => gui.GetType().GetMethod("Back").Invoke(gui, null);

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        // Clicks the bag cell that shows this colour ink. A replaced ink comes back as a new object with the same id.
        static void SelectInk(MonoBehaviour gui, GrowthInk ink)
        {
            Refresh(gui);
            NativeButton(gui,"Owned item "+ink.Id).onClick.Invoke(); Refresh(gui);
        }
        static Button NativeButton(MonoBehaviour gui,string name)=>Field<GameObject>(gui,"bag").GetComponentsInChildren<Button>().Single(b=>b.name==name);
        static Button PaletteSlot(MonoBehaviour gui,int index)=>NativeButton(gui,"Palette slot "+index);

        // Lambdas live here, outside the test enumerator: its closures would not survive the play mode domain reload.
        static void CheckEquipSwapAndDestroy(MonoBehaviour gui, WeaponGrowth growth, System.Guid weapon)
        {
            var palette = growth.Equipped(weapon);
            int index = palette.Slots.ToList().FindIndex(s => s.Part == null);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "the departure palette starts with empty slots");
            string color = palette.Slots[index].Color == "colorless" ? "red" : palette.Slots[index].Color;
            var parts = growth.Data.parts.Where(p => p.color == color && !palette.Slots.Any(s => s.Part == p)).ToArray();
            var first = growth.GrantInk(parts[0].id); var second = growth.GrantInk(parts[1].id); var third = growth.GrantInk(parts[2].id);
            int gold = growth.Economy.Gold;

            SelectInk(gui, first); PaletteSlot(gui, index).onClick.Invoke(); Refresh(gui);
            NativeButton(gui,"Book primary").onClick.Invoke(); Refresh(gui);
            Assert.That(palette.Slots[index].Part, Is.SameAs(first.Part), "equipped outside the facility");
            Assert.That(growth.Inks.Any(i => i.Id == first.Id), Is.False);
            SelectInk(gui, second); PaletteSlot(gui, index).onClick.Invoke(); Refresh(gui);
            NativeButton(gui,"Book primary").onClick.Invoke(); Refresh(gui);
            Assert.That(palette.Slots[index].Part, Is.SameAs(second.Part), "replaced");
            Assert.That(growth.Inks.Any(i => i.Id == first.Id), Is.True, "the replaced ink returns to the bag");
            Assert.That(growth.Economy.Gold, Is.EqualTo(gold), "replacement is free");
            SelectInk(gui, first); PaletteSlot(gui, index).onClick.Invoke(); Refresh(gui);
            NativeButton(gui,"Book primary").onClick.Invoke(); Refresh(gui);
            Assert.That(palette.Slots[index].Part, Is.SameAs(first.Part), "swapping back again works");

            int count = growth.Inks.Count;
            SelectInk(gui, third); var destroy = NativeButton(gui,"Book destroy");
            Assert.That(destroy.gameObject.activeSelf && destroy.interactable, Is.True, "a stored colour ink can be destroyed from the bag");
            destroy.onClick.Invoke(); Refresh(gui);
            Assert.That(growth.Inks.Count, Is.EqualTo(count), "the first press only arms the destroy");
            NativeButton(gui,"Book primary").onClick.Invoke(); Refresh(gui);
            Assert.That(growth.Inks.Any(i => i.Id == third.Id), Is.False, "the second press destroys it");
        }

        [UnityTest] public IEnumerator TheBagEquipsSwapsAndDestroysColourInksOutsideTheFacility()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(world && world.Ready && world.GetComponent<Chapter1RunBinding>());) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            yield return null; yield return null;
            var offers = world.GetComponent<Chapter1StartingBlueprints>(); var player = world.arena.Player;
            player.Position = offers.OfferPosition(0); Assert.That(offers.TryChoose(0), Is.True);
            player.Position = offers.OfferPosition(1); Assert.That(offers.TryChoose(1), Is.True);
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            yield return null;

            Chapter1MaintenanceShop shop = null; MonoBehaviour gui = null;
            for (int frame = 0; frame < 300 && !(shop && gui); frame++)
            {
                yield return null; shop = world.GetComponent<Chapter1MaintenanceShop>();
                gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            }
            Assert.That(shop, Is.Not.Null, "maintenance shop"); Assert.That(gui, Is.Not.Null, "bound play GUI");
            Assert.That(shop.Growth, Is.Not.Null, "the run has colour ink growth");
            Assert.That(shop.TryOpen(), Is.False, "the starting room is not a facility");
            var host = Object.FindAnyObjectByType<Deviant.Inventory.Unity.InventoryRuntimeHost>();
            host.Presenter.Open(); Refresh(gui);
            yield return null;
            Assert.That(Field<GameObject>(gui, "bag").activeInHierarchy, Is.True);
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            var enemy = world.arena.enemies.First(e => e && !e.Dead && e.gameObject.activeInHierarchy);
            var position = enemy.Position;
            var health = player.health;
            var tick = world.arena.SimulationTickCount;
            CheckEquipSwapAndDestroy(gui, shop.Growth, player.weapons[0].InstanceId);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(world.arena.SimulationTickCount, Is.EqualTo(tick));
            Assert.That(enemy.Position, Is.EqualTo(position));
            Assert.That(player.health, Is.EqualTo(health));

            NativeButton(gui, "Close book").onClick.Invoke();
            Refresh(gui);
            Assert.That(host.Presenter.IsOpen, Is.False);
            Assert.That(Field<GameObject>(gui, "bag").activeInHierarchy, Is.False);
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(world.arena.SimulationTickCount, Is.GreaterThan(tick));

            Back(gui);
            Assert.That(host.Presenter.Paused, Is.True, "Esc routes to the pause menu");
            Assert.That(Time.timeScale, Is.Zero);
            tick = world.arena.SimulationTickCount;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(world.arena.SimulationTickCount, Is.EqualTo(tick));
            Back(gui);
            Assert.That(host.Presenter.Paused, Is.False);
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
            yield return new ExitPlayMode();
        }
    }
}
