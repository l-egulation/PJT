using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class RoguelitePerkGuiTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        static RunSession StartRun()
        {
            var p = ProfileData.CreateNew(Guid.NewGuid().ToString()); p.runsDied = 1; p.chapter1BossFirstClear = true; p.chaptersCleared = 2; p.pigment = 100000;
            p.seenStories = new[] { "opening", "artisan-first-run", "artisan-visit-1", "artisan-visit-2", "village-ink-reward-1", "village-ink-reward-2", "village-executioner-note" };
            Chapter1ProfileHost.UseStore(new MemoryProfileStore(p)); var service = Chapter1ProfileHost.Service;
            foreach (var u in service.Catalog.Unlocks) Assert.That(service.TryPurchase(u.id, out _), Is.True, u.id);
            var host = Chapter1RunHost.Ensure(); host.StartNewRun(service.PlanUnarmedRun()); return host.Flow.Current;
        }
        static ForestCombatWorld Bound(RunSession run)
        {
            var w = Object.FindAnyObjectByType<ForestCombatWorld>();
            return w && w.Ready && w.GetComponent<Chapter1RunBinding>()?.Run == run ? w : null;
        }
        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
        static void Refresh(MonoBehaviour gui) => gui.GetType().GetMethod("Refresh").Invoke(gui, null);
        static void BookTab(MonoBehaviour gui, string book, int tab)
        { var b = Field<object>(gui, book); b.GetType().GetField("Tab").SetValue(b, tab); b.GetType().GetField("Key").SetValue(b, null); Refresh(gui); }
        static Button Button(MonoBehaviour gui, string name) => gui.GetComponentsInChildren<Button>().First(b => b.name == name);
        static void Capture(MonoBehaviour gui, ForestCombatWorld world, string name) =>
            typeof(Chapter1ControlsSignTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { gui, world.arena.ViewCamera, name });

        // Permanent unlocks reach the sketch-weapon run (roguelite-progression.md): the departure bonus opens as a 3-weapon
        // offer with the shared reroll, the shop display unlock adds places to the sketch shop, and its refresh buttons
        // spend the same budget; a durability break pays the weapon break gold once.
        [UnityTest] public IEnumerator BonusRewardRerollAndExpandedSketchShopWorkThroughTheActualGui()
        {
            EditorSceneManager.playModeStartScene = null; EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var run = StartRun(); ForestCombatWorld world = null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (!(world = Bound(run)) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world, Is.Not.Null, "the planned run's world must finish loading");
            MonoBehaviour gui = null;
            for (int f = 0; f < 180 && !(gui = Gui()); f++) yield return null;
            Assert.That(gui, Is.Not.Null, "the inventory GUI is created after the world becomes ready");
            var guide = world.GetComponent<Chapter1FirstRunInkGuide>(); if (guide) { guide.FinishNotebook(); guide.enabled = false; }
            gui.GetType().GetMethod("DismissDepartureCard")?.Invoke(gui, null);
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            SketchRunBinding binding = null;
            for (int f = 0; f < 240 && !(binding && binding.State?.PendingIsStart == true); f++) { yield return null; binding = world.GetComponent<SketchRunBinding>(); }
            Assert.That(binding, Is.Not.Null); Assert.That(binding.State.PendingIsStartInk, Is.True, "the departure ink reward opens before the weapon");
            var inkOffer = binding.State.PendingOffer;
            Assert.That(inkOffer.Length, Is.EqualTo(3)); Assert.That(inkOffer.All(c => c.IsInk), Is.True);
            Assert.That(run.Perks.StartingInkRewardsRemaining, Is.Zero);
            Capture(gui, world, "roguelite-bonus-ink");
            Assert.That(binding.TryChooseReward(0), Is.True); binding.ContinueReward();
            Assert.That(binding.State.Arsenal.Registered(inkOffer[0].Ink), Is.True, "the picked ink joins the run codex");
            for (int f = 0; f < 120 && binding.State.PendingIsStart != true; f++) yield return null;
            Assert.That(binding.State.PendingIsStart && !binding.State.PendingIsStartInk, Is.True, "the departure weapon follows the ink");
            var offer = binding.State.PendingOffer;
            Assert.That(offer.Length, Is.EqualTo(3)); Assert.That(offer.All(c => !c.IsInk && c.Weapon.Slots == 4), Is.True);
            Assert.That(run.Perks.StartingRewardsRemaining, Is.Zero);
            Capture(gui, world, "roguelite-bonus-reward");
            int rerolls = run.Perks.RerollsRemaining;
            Assert.That(binding.TryRerollReward(), Is.True);
            Assert.That(run.Perks.RerollsRemaining, Is.EqualTo(rerolls - 1)); Assert.That(binding.State.PendingOffer, Is.Not.SameAs(offer));
            Assert.That(binding.State.PendingIsStart, Is.True, "a reroll keeps the departure offer's rule");
            Assert.That(binding.TryChooseReward(1), Is.True); binding.ContinueReward(); yield return null;
            Assert.That(binding.State.PendingOffer, Is.Null);
            var picked = binding.State.Arsenal.Weapons.Single();
            Assert.That(binding.State.WeaponsPicked, Is.Zero, "the bonus stays outside the normal weapon guarantee");
            int gold = run.Economy.Gold;
            Assert.That(binding.State.Arsenal.ChargeDurability(picked.Id, picked.MaxDurability + 1), Is.True);
            Assert.That(run.Economy.Gold, Is.EqualTo(gold + run.Perks.Settings.WeaponBreakGold), "a durability break pays the unlock's gold");
            Assert.That(run.Perks.Settings.WeaponBreakGold, Is.GreaterThan(0));

            var room = world.Data.rooms.First(r => Chapter1MaintenanceFacility.IsFacility(r.kind));
            world.arena.Player.Position = new Vector2(room.x + room.w / 2f, -(room.y + room.entryY));
            var shop = world.GetComponent<Chapter1MaintenanceShop>(); Assert.That(shop.TryOpen(), Is.True);
            run.Economy.Earn(100000); yield return null; Refresh(gui); yield return null;
            var equipment = world.arena.Sketch;
            string key = "chapter1:" + shop.Facility;
            var stock = equipment.Shop(key);
            Assert.That(stock.Count(c => c.Weapon != null), Is.EqualTo(5), "3 weapons + 2 from the display unlock");
            Assert.That(stock.Count(c => c.Weapon == null), Is.EqualTo(5), "3 inks + 2 from the display unlock");
            Button(gui, "Shop item 1").onClick.Invoke(); yield return null; Refresh(gui);
            Button(gui, "Buy selected shop item").onClick.Invoke(); yield return null;
            Assert.That(equipment.Shop(key)[1].Sold, Is.True);
            Capture(gui, world, "roguelite-expanded-sketch-shop");
            Button(gui, "Continue shop receipt").onClick.Invoke(); yield return null; Refresh(gui);
            rerolls = run.Perks.RerollsRemaining;
            var unsoldWeapon = equipment.Shop(key)[0].Weapon.CompositionKey;
            Button(gui, "Reroll weapons").onClick.Invoke(); yield return null;
            Assert.That(run.Perks.RerollsRemaining, Is.EqualTo(rerolls - 1));
            Assert.That(equipment.Shop(key)[1].Sold, Is.True, "a bought place stays sold");
            Assert.That(equipment.Shop(key)[0].Weapon.CompositionKey, Is.Not.EqualTo(unsoldWeapon));
            Button(gui, "Reroll inks").onClick.Invoke(); yield return null;
            Assert.That(run.Perks.RerollsRemaining, Is.EqualTo(rerolls - 2));
        }
    }
}
