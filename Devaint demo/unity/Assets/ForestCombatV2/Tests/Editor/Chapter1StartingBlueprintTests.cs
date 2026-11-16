using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1StartingBlueprintTests
    {
        static readonly string[] Forms = { "base_sword", "hammer", "launcher" };

        [UnityTest] public IEnumerator SecondDrawingStartsItsOwnComboAfterFirstWeaponAttack()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++)
            { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var player = world.arena.Player;
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            player.Position = offer.OfferPosition(0);
            Assert.That(offer.TryChoose(0), Is.True);
            using (var driver = world.arena.AcquireManualSimulation())
            {
                player.Position = offer.OfferPosition(1);
                driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Basic));
                Assert.That(player.currentCast, Is.Not.Null);
                for (int tick = 0; tick < 240 && player.currentCast; tick++) driver.Step(1f / 120);
                Assert.That(player.currentCast, Is.Null);
                player.Position = offer.OfferPosition(1);
                Assert.That(offer.TryChoose(1), Is.True);
                Assert.That(player.selected, Is.Zero, "Equipping the second drawing keeps weapon 1 in hand.");
                driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Weapon2Basic));
                Assert.That(player.currentCast, Is.Not.Null);
                Assert.That(player.selected, Is.EqualTo(1));
                Assert.That(player.BasicIndex, Is.Zero, "A newly equipped drawing must start at its first hit.");
                Assert.That(player.currentCast.ImpactTime, Is.EqualTo(.21f).Within(.00001f));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator StartingHammerUsesApprovedCatalogTimingAndLastHitRecovery()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++)
            { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var player = world.arena.Player;
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            player.Position = offer.OfferPosition(1);
            Assert.That(offer.TryChoose(1), Is.True);
            using (var driver = world.arena.AcquireManualSimulation())
            {
                driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Basic));
                var first = player.currentCast;
                Assert.That(first, Is.Not.Null);
                float active = first.Definition.Hits[0].Active;
                Assert.That(first.ImpactTime, Is.EqualTo(.21f).Within(.00001f));
                Assert.That(active, Is.EqualTo(.1f).Within(.00001f));
                Assert.That(first.MotionDuration - first.ImpactTime - active, Is.EqualTo(.06f).Within(.00001f));
                Assert.That(first.MotionDuration, Is.EqualTo(.37f).Within(.00001f));
                Assert.That(first.Definition.Hits[0].Damage, Is.EqualTo(17));
                for (int tick = 0; tick < 200 && player.currentCast == first; tick++)
                    driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Basic));
                var second = player.currentCast;
                Assert.That(second && second != first, Is.True);
                Assert.That(player.BasicIndex, Is.EqualTo(1));
                Assert.That(second.StartedAt, Is.GreaterThanOrEqualTo(first.EndTime - .00001f));
                Assert.That(second.ImpactTime, Is.EqualTo(.29f).Within(.00001f));
                Assert.That(second.MotionDuration, Is.EqualTo(.59f).Within(.00001f));
                Assert.That(second.Definition.Hits[0].Damage, Is.EqualTo(28));
            }
            yield return new ExitPlayMode();
        }

        // World interaction moved from E to F (12e180b9); the key comes from the bindings.
        [UnityTest] public IEnumerator InteractRequiresANewPressAndNeverAutoCollectsOnContact()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int i = 0; i < 600 && (!world || !world.Ready); i++)
            { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            var player = world.arena.Player;
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard = null;
            try
            {
                // The batch Editor has no focused Game View for this synthetic device.
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                var interact = Deviant.Combat.CombatKeyboardBindings.For(player).Interact;
                player.Position = offer.OfferPosition(0);
                yield return null;
                Assert.That(offer.SelectedCount, Is.Zero, "Touching a drawing must not choose it.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(interact));
                for (int frame = 0; frame < 10 && offer.SelectedCount == 0; frame++) yield return null;
                Assert.That(offer.SelectedCount, Is.EqualTo(1));
                player.Position = offer.OfferPosition(1);
                yield return null; yield return null;
                Assert.That(offer.SelectedCount, Is.EqualTo(1), "Holding the interact key must not choose another drawing.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(interact));
                for (int frame = 0; frame < 10 && !offer.Complete; frame++) yield return null;
                Assert.That(offer.Complete, Is.True);
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
            }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator EveryOrderedPairEquipsTwoBaseWeaponsAndRemovesTheThird()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            for (int first = 0; first < 3; first++) for (int second = 0; second < 3; second++)
            {
                if (first == second) continue;
                if (first != 0 || second != 1)
                    yield return SceneManager.LoadSceneAsync(Chapter1MapSelector.ProceduralScene);
                var world = Object.FindAnyObjectByType<ForestCombatV2.ForestCombatWorld>();
                for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++)
                { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
                Assert.That(world && world.Ready, Is.True);
                var offer = world.GetComponent<Chapter1StartingBlueprints>();
                var player = world.arena.Player;
                Assert.That(offer, Is.Not.Null);
                Assert.That(offer.RemainingCount, Is.EqualTo(3));
                Assert.That(player.IsWeaponSlotReady(0) || player.IsWeaponSlotReady(1), Is.False);
                Assert.That(offer.TryChoose(-1) || offer.TryChoose(3), Is.False);
                Assert.That(offer.TryChoose(first), Is.False, "Distant choices must not be acquired.");
                player.Position = offer.OfferPosition(first);
                Assert.That(offer.TryChoose(first), Is.True);
                Assert.That(player.IsWeaponSlotReady(0), Is.True);
                Assert.That(player.IsWeaponSlotReady(1), Is.False);
                Assert.That(world.arena.catalog.forms[player.weapons[0].FormIndex].id, Is.EqualTo(Forms[first]));
                Assert.That(offer.TryChoose(first), Is.False, "One drawing cannot occupy both slots.");
                Assert.That(offer.RemainingCount, Is.EqualTo(2));
                world.arena.ResetTest();
                Assert.That(offer.SelectedCount, Is.EqualTo(1), "Combat reset must keep an unfinished selection.");
                player.Position = offer.OfferPosition(second);
                Assert.That(offer.TryChoose(second), Is.True);
                Assert.That(offer.Complete, Is.True);
                Assert.That(offer.RemainingCount, Is.Zero);
                for (int index = 0; index < 3; index++) Assert.That(offer.IsOffered(index), Is.False);
                for (int slot = 0; slot < 2; slot++)
                {
                    Assert.That(player.IsWeaponSlotReady(slot), Is.True);
                    Assert.That(world.arena.catalog.forms[player.weapons[slot].FormIndex].id, Is.EqualTo(Forms[slot == 0 ? first : second]));
                    Assert.That(player.weapons[slot].SpecialIndex, Is.Zero);
                    Assert.That(player.weapons[slot].StatSlots.All(value => value == 0), Is.True);
                }
                Assert.That(player.selected, Is.Zero, "The run starts holding weapon 1 after both drawings are chosen.");
                using (var driver = world.arena.AcquireManualSimulation())
                {
                    driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Basic));
                    Assert.That(player.currentCast, Is.Not.Null, "The newly selected weapon must be usable.");
                    player.CancelAction();
                    driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Swap));
                    Assert.That(player.selected, Is.EqualTo(1), "Weapon 2 is ready to swap to.");
                }
                world.arena.ResetTest();
                Assert.That(offer.Complete && offer.RemainingCount == 0, Is.True);
                Assert.That(offer.TryChoose(3 - first - second), Is.False);
                Assert.That(world.arena.catalog.forms[player.weapons[0].FormIndex].id, Is.EqualTo(Forms[first]));
                Assert.That(world.arena.catalog.forms[player.weapons[1].FormIndex].id, Is.EqualTo(Forms[second]));
            }
            yield return new ExitPlayMode();
        }

        // The village departure equips both blueprints at once; the run still starts holding weapon 1.
        [UnityTest] public IEnumerator PreselectedBlueprintsStartTheRunHoldingWeaponOne()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int i = 0; i < 600 && (!world || !world.Ready); i++)
            { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            var player = world.arena.Player;
            Assert.That(offer.ApplyPreselected(new[] { "blunt", "ranged" }), Is.True);
            Assert.That(offer.Complete, Is.True);
            Assert.That(player.selected, Is.Zero, "The run must start holding weapon 1, not the last equipped weapon 2.");
            Assert.That(world.arena.catalog.forms[player.Weapon.FormIndex].id, Is.EqualTo("hammer"));
            world.arena.ResetTest();
            Assert.That(player.selected, Is.Zero, "A combat reset keeps weapon 1 active.");
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator UnchosenWeaponsCannotAttackOrSwapButMovementStillWorks()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int i = 0; i < 600 && (!world || !world.Ready); i++)
            { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var player = world.arena.Player;
            using (var driver = world.arena.AcquireManualSimulation())
            {
                float startX = player.Position.x;
                foreach (var buttons in new[] { CombatButtons.Basic, CombatButtons.Special, CombatButtons.Swap })
                {
                    driver.Step(1f / 120, new CombatInput(1, buttons));
                    Assert.That(player.currentCast, Is.Null);
                    Assert.That(player.selected, Is.Zero);
                }
                Assert.That(player.Position.x, Is.GreaterThan(startX));
                Assert.That(world.arena.PlayerShotCount, Is.Zero);
                var offer = world.GetComponent<Chapter1StartingBlueprints>();
                player.Position = offer.OfferPosition(0);
                Assert.That(offer.TryChoose(0), Is.True);
                driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Swap));
                Assert.That(player.selected, Is.Zero, "The unchosen second slot must remain unavailable.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator MapPauseInteractionBlockAndDeathCannotConsumeAnOffer()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int i = 0; i < 600 && (!world || !world.Ready); i++)
            { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            var player = world.arena.Player;
            player.Position = offer.OfferPosition(0);
            world.FullMap.SetOpen(true);
            Assert.That(offer.TryChoose(0), Is.False);
            world.FullMap.SetOpen(false);
            using (var scope = world.arena.Controls.OpenScope("blocked blueprint interaction"))
            {
                scope.BlockInput(InputChannels.Interaction);
                Assert.That(offer.TryChoose(0), Is.False);
            }
            player.health = 0;
            Assert.That(offer.TryChoose(0), Is.False);
            Assert.That(offer.SelectedCount, Is.Zero);
            world.arena.ResetTest();
            player.Position = offer.OfferPosition(0);
            Assert.That(offer.TryChoose(0), Is.True);
            yield return new ExitPlayMode();
        }
    }
}
