using System.Collections;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Deviant.Inventory.Tests
{
    public class InventoryIntegrationTests
    {
        CombatArena arena;
        InventoryRuntimeHost host;
        CombatArena.ManualSimulation simulation;
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode();
            arena = Object.FindAnyObjectByType<CombatArena>();
            float deadline = Time.realtimeSinceStartup + 10;
            while (!host || host.Inventory == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                host = arena.GetComponent<InventoryRuntimeHost>(); yield return null;
            }
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            simulation = arena.AcquireManualSimulation(); arena.Player.UseTestInput = true;
            var installedView = arena.GetComponentInChildren<TemporaryInventoryView>();
            if (installedView) Object.Destroy(installedView.gameObject);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { simulation?.Dispose(); Time.timeScale = 1; if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        void Advance(float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .02f); i++) { simulation.Step(.02f); host.Advance(); } }
        [UnityTest] public IEnumerator DeathDropsImmediatelyExactlyOnceAndResetClearsDrops()
        {
            var enemy = arena.enemies[0]; host.Advance();
            enemy.Receive(10000, arena.Player, 1, true, null); host.Advance();
            Assert.That(enemy.bodyCollider.enabled || enemy.hurtCollider.enabled, Is.False);
            Assert.That(enemy.Receive(1, arena.Player, 1, true, null), Is.False);
            Assert.That(host.Drops.Count, Is.EqualTo(2));
            Advance(.4f); Assert.That(host.Drops.Count, Is.EqualTo(2));
            Advance(.3f); Assert.That(host.Drops.Count, Is.EqualTo(2));
            Advance(.8f); Assert.That(host.Drops.Count, Is.EqualTo(2));
            arena.ResetTest(); Assert.That(host.Drops.Count, Is.Zero); Assert.That(enemy.bodyCollider.enabled && enemy.hurtCollider.enabled, Is.True);
            enemy.Receive(10000, arena.Player, 1, true, null); host.Advance(); arena.ResetTest(); Advance(1);
            Assert.That(host.Drops.Count, Is.Zero); yield return null;
        }
        [UnityTest] public IEnumerator ContactAcquiresAfterLandingOnlyAndDuplicatesStaySeparate()
        {
            var at = arena.Player.Position;
            var first = host.SpawnDrop("stat.basic", at); var second = host.SpawnDrop("stat.basic", at + Vector2.right * .05f);
            Assert.That(host.TryCollect(first), Is.False); Advance(.4f); Assert.That(host.Inventory.Used, Is.Zero);
            Advance(.3f); Assert.That(host.Inventory.Used, Is.EqualTo(2)); Assert.That(host.Drops.Count, Is.Zero);
            Assert.That(host.Inventory.BagAt(0).InstanceId, Is.Not.EqualTo(host.Inventory.BagAt(1).InstanceId));
            Assert.That(host.TryCollect(first), Is.False); yield return null;
        }
        [UnityTest] public IEnumerator FullBagKeepsPickupAndManualExchangeLeavesOldInkOnGround()
        {
            for (int i = 0; i < host.Inventory.Capacity; i++) host.Inventory.TryAcquire(new InkInstance(new InkDefinition("stat.basic", "Basic", "", InkKind.Stat, 1)), out _);
            var old = host.Inventory.BagAt(0); var drop = host.SpawnDrop("stat.speed", arena.Player.Position);
            Advance(.7f); Assert.That(host.Inventory.Used, Is.EqualTo(5)); Assert.That(drop.Collected, Is.False);
            Assert.That(host.PendingExchange, Is.SameAs(drop)); host.Presenter.SelectBag(0);
            Assert.That(host.ExchangeSelected(), Is.True); Assert.That(host.Inventory.BagAt(0).Definition.Id, Is.EqualTo("stat.speed"));
            Assert.That(host.Drops.Count, Is.EqualTo(1)); Assert.That(host.Drops[0].Item, Is.SameAs(old));
            yield return null;
        }
        [UnityTest] public IEnumerator RealStatsFormAndSpecialFollowPhysicalItemsAndRejectCombatEdit()
        {
            host.Inventory.TryAcquire(new InkInstance(host.Content.Find("stat.basic").CreateDefinition()), out _);
            Assert.That(host.Inventory.TryEquip(0, 0, 1, out _), Is.True); Assert.That(arena.Player.weapons[0].BasicBonus, Is.EqualTo(.2f));
            host.Inventory.TryAcquire(new InkInstance(host.Content.Find("form.whip").CreateDefinition()), out _);
            Assert.That(host.Inventory.TryEquip(0, 0, 0, out _), Is.True); Assert.That(arena.catalog.forms[arena.Player.weapons[0].FormIndex].id, Is.EqualTo("whip"));
            host.Inventory.TryAcquire(new InkInstance(host.Content.Find("special.range").CreateDefinition()), out _);
            arena.enemies[0].aggressive = true;
            Assert.That(host.Inventory.TryEquip(0, 0, 6, out _), Is.False); Assert.That(host.Inventory.BagAt(0), Is.Not.Null);
            arena.enemies[0].aggressive = false;
            Assert.That(host.Inventory.TryEquip(0, 0, 6, out _), Is.True); Assert.That(arena.Player.weapons[0].SpecialIndex, Is.EqualTo(1));
            arena.ResetTest(); Assert.That(host.Inventory.Used, Is.Zero); Assert.That(arena.Player.weapons[0].BasicBonus, Is.Zero);
            Assert.That(arena.catalog.forms[arena.Player.weapons[0].FormIndex].id, Is.EqualTo("base_sword")); yield return null;
        }
        [UnityTest] public IEnumerator DeadCollectorCannotConsumeAndMissingTableProducesNoDrop()
        {
            var drop = host.SpawnDrop("stat.basic", arena.Player.Position + Vector2.right * 8); Advance(.7f);
            arena.Player.health = 0; Assert.That(host.TryCollect(drop), Is.False); Assert.That(drop.Collected, Is.False);
            arena.ResetTest(); var enemy = arena.enemies[0]; enemy.gameObject.AddComponent<InkDropSource>().tableKey = "absent";
            enemy.Receive(10000, arena.Player, 1, true, null); host.Advance(); Advance(.8f);
            Assert.That(host.Drops.Count, Is.Zero); yield return null;
        }
        [UnityTest] public IEnumerator DebugBaseLoadoutSurvivesResetWhilePhysicalInksAreRemoved()
        {
            int shield = System.Array.FindIndex(arena.catalog.forms, form => form.id == "shield");
            arena.Player.Reconfigure(0, shield, 0); arena.Player.weapons[0].StatSlots[1] = 3; host.Advance();
            host.Inventory.TryAcquire(new InkInstance(host.Content.Find("stat.basic").CreateDefinition()), out _);
            Assert.That(host.Inventory.TryEquip(0, 0, 1, out _), Is.True);
            arena.ResetTest(); Assert.That(arena.Player.weapons[0].FormIndex, Is.EqualTo(shield));
            Assert.That(arena.Player.weapons[0].StatSlots[0], Is.Zero); Assert.That(arena.Player.weapons[0].StatSlots[1], Is.EqualTo(3));
            yield return null;
        }
        [UnityTest] public IEnumerator TemporaryViewUsesSameCommandsAndIAndEscapeOwnOnlyInventory()
        {
            var view = new GameObject("Inventory view").AddComponent<TemporaryInventoryView>(); view.transform.SetParent(arena.transform); view.Bind(host);
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.EnableDevice(keyboard); keyboard.MakeCurrent();
            // Register edge tracking before the first synthetic transition (Input System 1.19).
            bool initialTab = keyboard.tabKey.wasPressedThisFrame, initialEscape = keyboard.escapeKey.wasPressedThisFrame;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); InputSystem.Update();
                Assert.That(keyboard.tabKey.isPressed, Is.True, "Synthetic input must be active in the batch test environment.");
                Assert.That(keyboard.tabKey.wasPressedThisFrame, Is.True, "Inventory uses a new press, not a held key.");
                Assert.That(Keyboard.current, Is.SameAs(keyboard));
                host.GetComponent<InventoryKeyboardInput>().SendMessage("Update");
                Assert.That(host.Presenter.IsOpen, Is.True); Assert.That(arena.Player.InputBlocked, Is.True);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                host.Inventory.TryAcquire(new InkInstance(host.Content.Find("stat.speed").CreateDefinition()), out _);
                view.bagButtons[0].onClick.Invoke(); view.equipButton.onClick.Invoke(); view.Refresh();
                Assert.That(arena.Player.weapons[0].Speed, Is.EqualTo(1.1f)); Assert.That(host.Inventory.Used, Is.Zero);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update(); host.GetComponent<InventoryKeyboardInput>().SendMessage("Update");
                Assert.That(host.Presenter.IsOpen, Is.False); Assert.That(arena.Player.InputBlocked, Is.False);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
                host.Presenter.Tick(Time.unscaledTimeAsDouble); view.Refresh(); Assert.That(view.toastRoot.activeSelf, Is.True);
                arena.ResetTest(); view.Refresh(); Assert.That(view.toastRoot.activeSelf, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
            yield return null;
        }
    }
}
