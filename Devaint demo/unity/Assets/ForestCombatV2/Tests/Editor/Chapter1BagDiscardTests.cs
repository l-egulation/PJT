using System.Collections;
using System.Linq;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1BagDiscardTests
    {
        [UnityTest] public IEnumerator DiscardLeavesTheSelectedInkAtTheFeetUntilThePlayerStepsOffAndReturns()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            var setup = Object.FindAnyObjectByType<ForestCombatWorld>();
            Assert.That(setup, Is.Not.Null, "chapter 1 scene world in edit mode");
            setup.proceduralSeed = 42;
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            InventoryRuntimeHost bag = null;
            for (int frame = 0; frame < 600 && (!bag || bag.Presenter == null); frame++) { yield return null; bag = Object.FindAnyObjectByType<InventoryRuntimeHost>(); }
            Assert.That(world && world.Ready && bag, Is.True);
            var player = world.arena.Player;
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }

            Assert.That(bag.Presenter.DiscardSelected(), Is.False, "nothing selected");
            var pickup = bag.SpawnDrop("stat.speed", player.Position, true);
            Assert.That(bag.TryCollect(pickup), Is.True);
            Assert.That(bag.Inventory.Used, Is.EqualTo(1));
            // No lambda may capture a local here: captured locals do not survive EnterPlayMode's domain reload.
            int filled = -1;
            foreach (int slot in bag.Inventory.VisibleSlots(null)) if (filled < 0 && bag.Inventory.BagAt(slot) != null) filled = slot;
            bag.Presenter.SelectBag(filled);
            Assert.That(bag.Presenter.DiscardSelected(), Is.True, bag.Presenter.Message);
            Assert.That(bag.Inventory.Used, Is.Zero);
            Assert.That(bag.Presenter.SelectedBag, Is.EqualTo(-1));

            var dropped = bag.Drops.Single();
            Assert.That(dropped.Item.Definition.Id, Is.EqualTo("stat.speed"));
            Assert.That(Mathf.Abs(dropped.transform.position.x - player.Position.x), Is.LessThan(.6f), "left at the player's feet");
            Assert.That(dropped.WaitingForPlayerToLeave, Is.True);
            float waitUntil = world.arena.Now + 1f;
            for (int frame = 0; frame < 2000 && world.arena.Now < waitUntil; frame++) yield return null;
            Assert.That(world.arena.Now, Is.GreaterThanOrEqualTo(waitUntil), "a full second of game time passed");
            Assert.That(bag.Inventory.Used, Is.Zero, "standing on it does not pick it straight back up");

            var feet = player.Position;
            player.Position = feet + Vector2.left * 4; Physics2D.SyncTransforms();
            for (int frame = 0; frame < 30 && dropped.WaitingForPlayerToLeave; frame++) yield return null;
            Assert.That(dropped.WaitingForPlayerToLeave, Is.False, "stepping off re-arms the pickup");
            for (int frame = 0; frame < 2000 && bag.Inventory.Used == 0; frame++)
            { player.Position = (Vector2)dropped.transform.position - (player.Center - player.Position); yield return null; }
            Assert.That(bag.Inventory.Used, Is.EqualTo(1), "walking back onto it picks it up");
            yield return new ExitPlayMode();
        }

        // Destroy removes the selected bag ink for good after a second press; nothing is dropped or paid.
        [UnityTest] public IEnumerator DestroyNeedsASecondPressAndLeavesNothingBehind()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            InventoryRuntimeHost bag = null;
            for (int frame = 0; frame < 600 && (!bag || bag.Presenter == null); frame++) { yield return null; bag = Object.FindAnyObjectByType<InventoryRuntimeHost>(); }
            Assert.That(world && world.Ready && bag, Is.True);
            var player = world.arena.Player;
            foreach (var enemy in world.arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            Assert.That(bag.Presenter.DestroySelected(), Is.False, "nothing selected");
            Assert.That(bag.TryCollect(bag.SpawnDrop("stat.speed", player.Position, true)), Is.True);
            Assert.That(bag.TryCollect(bag.SpawnDrop("stat.speed", player.Position, true)), Is.True);
            int first = -1, second = -1;
            foreach (int slot in bag.Inventory.VisibleSlots(null)) if (bag.Inventory.BagAt(slot) != null) { if (first < 0) first = slot; else if (second < 0) second = slot; }
            int gold = world.GetComponent<Chapter1RunBinding>().Run.Economy?.Gold ?? 0;

            bag.Presenter.SelectBag(first);
            Assert.That(bag.Presenter.DestroySelected(), Is.False, "the first press only arms");
            Assert.That(bag.Presenter.DestroyArmed, Is.True);
            Assert.That(bag.Inventory.Used, Is.EqualTo(2));
            bag.Presenter.SelectBag(second);
            Assert.That(bag.Presenter.DestroyArmed, Is.False, "another selection disarms");
            bag.Presenter.SelectBag(first);
            Assert.That(bag.Presenter.DestroySelected(), Is.False);
            Assert.That(bag.Presenter.DestroySelected(), Is.True, bag.Presenter.Message);
            Assert.That(bag.Inventory.Used, Is.EqualTo(1), "one ink is gone");
            Assert.That(bag.Inventory.BagAt(first), Is.Null, "its slot is free");
            Assert.That(bag.Drops.Count(d => d), Is.Zero, "nothing is left on the ground");
            Assert.That(world.GetComponent<Chapter1RunBinding>().Run.Economy?.Gold ?? 0, Is.EqualTo(gold), "no currency for destroying");
            Assert.That(bag.Presenter.SelectedBag, Is.EqualTo(-1));
            yield return new ExitPlayMode();
        }
    }
}
