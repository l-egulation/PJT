using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using Deviant.Inventory.Unity;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock.LowLevel;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class GamepadWorldInteractionTests
    {
        ForestCombatWorld world;
        Gamepad pad;
        Keyboard keyboard;
        bool sony;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        CombatPlayer Player => world.arena.Player;
        LocalInput Input => PlayerControls.For(Player);
        MonoBehaviour Gui => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(value => value.GetType().Name == "TemporaryGameGui");
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        static void PrepareWorld(ForestCombatWorld preparing)=>preparing.proceduralSeed=42;

        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            background = InputSystem.settings.backgroundBehavior; editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            ForestCombatWorld.PreparingEditorPlay+=PrepareWorld;
            try
            {
                SceneManager.LoadScene(Chapter1MapSelector.ProceduralScene);
                yield return null;
            }
            finally { ForestCombatWorld.PreparingEditorPlay-=PrepareWorld; }
            world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int i = 0; i < 600 && !(world && world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            for (int i = 0; i < 120 && (!world.arena.GetComponent<InventoryRuntimeHost>() || world.arena.GetComponent<InventoryRuntimeHost>().Presenter == null || !Gui); i++) yield return null;
            Assert.That(world.arena.GetComponent<InventoryRuntimeHost>(), Is.Not.Null, "The production scene must install its inventory/UI host.");
            Assert.That(Gui, Is.Not.Null);
            world.arena.enabled = false;
            if (world.arena.Sketch == null)
                Assert.That(world.GetComponent<Chapter1StartingBlueprints>().ApplyPreselected(new[] { "sword", "blunt" }), Is.True);
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
            var sketch = world.GetComponent<SketchRunBinding>();
            if (sketch) typeof(SketchRunBinding).GetMethod("CloseFirstRunTutorial", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sketch, null);
            foreach (var enemy in world.arena.enemies) if (enemy) enemy.aggressive = false;
            for (int i = 0; i < 5; i++) yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }
        void Device(bool playStation)
        {
            sony = playStation; pad = (Gamepad)InputSystem.AddDevice(sony ? "DualSenseGamepadHID" : "Gamepad"); pad.MakeCurrent();
            Input.Source.SetDevices(pad);
        }
        void Queue(params GamepadButton[] buttons)
        {
            if (!sony)
            {
                var state = new GamepadState(); foreach (var button in buttons) state = state.WithButton(button);
                InputSystem.QueueStateEvent(pad, state); return;
            }
            var native = new DualSenseHIDInputReport { leftStickX = 128, leftStickY = 128, rightStickX = 128, rightStickY = 128, buttons0 = 8 };
            foreach (var button in buttons)
                switch (button)
                {
                    case GamepadButton.South: native.buttons0 |= 0x20; break;
                    case GamepadButton.East: native.buttons0 |= 0x40; break;
                    case GamepadButton.West: native.buttons0 |= 0x10; break;
                    case GamepadButton.North: native.buttons0 |= 0x80; break;
                    case GamepadButton.LeftShoulder: native.buttons1 |= 1; break;
                    case GamepadButton.RightShoulder: native.buttons1 |= 2; break;
                    case GamepadButton.Select: native.buttons1 |= 0x10; break;
                    case GamepadButton.Start: native.buttons1 |= 0x20; break;
                    case GamepadButton.DpadUp: native.buttons0 = (byte)(native.buttons0 & 0xf0); break;
                    case GamepadButton.DpadRight: native.buttons0 = (byte)((native.buttons0 & 0xf0) | 2); break;
                    case GamepadButton.DpadDown: native.buttons0 = (byte)((native.buttons0 & 0xf0) | 4); break;
                    case GamepadButton.DpadLeft: native.buttons0 = (byte)((native.buttons0 & 0xf0) | 6); break;
                    default: throw new ArgumentOutOfRangeException(nameof(button));
                }
            InputSystem.QueueStateEvent(pad, native);
        }
        IEnumerator Press(params GamepadButton[] buttons) { Queue(buttons); yield return null; yield return null; }
        void Place(Vector2 point)
        {
            Player.Position = point; Player.velocity = Vector2.zero; Physics2D.SyncTransforms(); Player.grounded = Player.ProbeGround();
        }
        static void Tick(MonoBehaviour behaviour) =>
            behaviour.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(behaviour, null);
        static string Caption(KeycapMarker marker) => marker.GetComponentsInChildren<Text>(true).FirstOrDefault()?.text;

        [UnityTest] public IEnumerator ShopOpeningPressMustBeReleasedBeforeBuying()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); Input.Source.SetDevices(keyboard);
            var shop = world.GetComponent<Chapter1MaintenanceShop>();
            var room = world.Data.rooms.First(r => Chapter1MaintenanceFacility.IsFacility(r.kind));
            Place(new Vector2(room.x + room.w / 2, -(room.y + room.entryY)));
            world.arena.Sketch.Economy.DebugSetGold(2000);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
            Assert.That(shop.TryOpen(), Is.True);
            Gui.GetType().GetMethod("Refresh").Invoke(Gui, null);
            var stock = world.arena.Sketch.Shop("chapter1:" + shop.Facility);
            int gold = world.arena.Sketch.Economy.Gold;
            Assert.That(stock[0].Sold, Is.False, "Opening F must not buy the default weapon in Refresh.");
            yield return null; yield return null;
            Assert.That(stock[0].Sold, Is.False, "Holding the opening key must not buy.");
            Assert.That(world.arena.Sketch.Economy.Gold, Is.EqualTo(gold));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null; yield return null;
            Assert.That(stock[0].Sold, Is.True, "A separate F press purchases the selected item.");
            Assert.That(world.arena.Sketch.Economy.Gold, Is.EqualTo(gold - stock[0].Price));
        }

        [UnityTest] public IEnumerator SealDropClearsOverhangWithTheWholeBody()
        {
            const float origin = 10000;
            var root = new GameObject("Seal drop regression geometry");
            void Solid(string name, Vector2 center, Vector2 size)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform);
                go.transform.position = center; go.AddComponent<ArenaSurface>(); go.AddComponent<BoxCollider2D>().size = size;
            }
            Solid("floor", new Vector2(origin + 4, -.5f), new Vector2(14, 1));
            Solid("overhanging side wall", new Vector2(origin - 1, 7), new Vector2(2, 6));
            Physics2D.SyncTransforms();
            var site = new StageGimmickSite { DropLeft = origin, DropRight = origin + 8, DropTop = 10, DropBottom = -1 };
            var size = new Vector2(1.8f, 2); var offset = Vector2.up;
            Assert.That(world.arena.SolidOverlap(new Vector2(origin + .2f, 10), size), Is.True,
                "The previous centre-ray placement at floor + 9 embeds shoulders in the wall.");
            Assert.That(Chapter1SealChallenge.TryResolveDrop(world.arena, site, origin + .2f, size, offset, 1, out var floor, out var start), Is.True);
            Assert.That(start.y, Is.LessThan(3), "Spawn below the overhang instead of inside it.");
            for (float y = start.y; y >= floor.y; y -= .05f)
                Assert.That(world.arena.SolidOverlap(new Vector2(start.x, y) + offset, size), Is.False);
            Assert.That(floor.y, Is.EqualTo(.02f).Within(.001f));
            var actor = new GameObject("Falling body").AddComponent<CombatActor>();
            actor.transform.SetParent(root.transform); actor.Configure(world.arena, start, size, offset, false);
            for (int i = 0; i < 180; i++) actor.PhysicsStep(1f / 60, 0, false);
            Assert.That(actor.grounded, Is.True, "The production motor must land, not remain suspended beside the wall.");
            Assert.That(actor.Position.y, Is.EqualTo(0).Within(.04f));
            Object.Destroy(root); yield return null;
        }

        [UnityTest] public IEnumerator DualSenseStartsAndClearsWavesThenSelectsAndTakesTheRewardWithOnlyAPad()
        {
            Device(true); yield return Press();
            var seal = world.GetComponent<Chapter1SealChallenge>(); Assert.That(seal.Prepare(), Is.True);
            world.Warp(seal.Room.id); Place(seal.TabletPosition);
            yield return Press(GamepadButton.RightShoulder);
            Assert.That(seal.State, Is.EqualTo(Chapter1SealChallenge.ChallengeState.Active));
            Assert.That(seal.GatesShut, Is.True); Assert.That(Caption(seal.Key), Is.EqualTo("R1"));
            // Keep R1 held through all three pages. Kill actual spawned actors and
            // advance the real arena clock, rather than assigning challenge state.
            int[] observed = new int[3];
            for (int frame = 0; frame < 300 && seal.State == Chapter1SealChallenge.ChallengeState.Active; frame++)
            {
                for (int tick = 0; tick < 10; tick++) world.arena.SendMessage("FixedUpdate");
                seal.SendMessage("Update");
                foreach (var enemy in seal.Spawned.ToArray())
                {
                    if (!enemy || enemy.Dead) continue;
                    observed[seal.Page]++; enemy.aggressive = false;
                    enemy.Receive(10000, Player, 1, true, null, true);
                }
                yield return null;
            }
            Assert.That(observed, Is.EqualTo(Chapter1SealChallenge.PageSizes));
            Assert.That(seal.State, Is.EqualTo(Chapter1SealChallenge.ChallengeState.Succeeded)); Assert.That(seal.GatesShut, Is.False);
            Place(seal.RewardChest.Position + Vector2.left * .5f); yield return null; yield return null;
            Assert.That(seal.RewardChest.IsOpen, Is.False, "Holding the start input cannot also open its later reward.");
            Assert.That(Caption(seal.ChestKey), Is.EqualTo("R1"));
            yield return Press(); yield return Press(GamepadButton.RightShoulder);
            var augments = world.GetComponent<AugmentRewardChoice>();
            for (int i = 0; i < 10 && !Field<GameObject>(Gui, "augmentPanel").activeInHierarchy; i++) yield return null;
            Assert.That(seal.RewardChest.IsOpen && augments.IsOpen, Is.True); Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True);
            var cards = Field<Button[]>(Gui, "augmentCards"); var take = Field<Button>(Gui, "augmentTake");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[0].gameObject));
            Assert.That(Field<int>(Gui, "augmentPick"), Is.EqualTo(0), "The approved UI selects the focused card.");
            Assert.That(take.interactable, Is.True);
            yield return Press(); yield return Press(GamepadButton.DpadRight);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[1].gameObject));
            string choice = augments.Offer[1].id; int before = augments.Augments.Owned.Count;
            Assert.That(Field<int>(Gui, "augmentPick"), Is.EqualTo(1)); Assert.That(take.interactable, Is.True); Assert.That(augments.IsOpen, Is.True);
            var inventory = world.arena.GetComponent<InventoryRuntimeHost>();
            yield return Press(); yield return Press(GamepadButton.LeftShoulder); Assert.That(inventory.Presenter.IsOpen, Is.False);
            yield return Press(); yield return Press(GamepadButton.Select); Assert.That(world.FullMap.IsOpen, Is.False);
            yield return Press(); yield return Press(GamepadButton.East); Assert.That(augments.IsOpen, Is.True); Assert.That(Player.Dashing, Is.False);
            yield return Press(); yield return Press(GamepadButton.Start); Assert.That(augments.IsOpen, Is.True); Assert.That(inventory.Presenter.Paused, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[1].gameObject));
            yield return Press(); yield return Press(GamepadButton.South);
            Assert.That(augments.IsOpen, Is.False); Assert.That(augments.Augments.Has(choice), Is.True); Assert.That(augments.Augments.Owned.Count, Is.EqualTo(before + 1));
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False); Assert.That(inventory.Presenter.IsOpen, Is.False);
            world.arena.SendMessage("FixedUpdate"); Assert.That(Player.jumps.Used, Is.Zero, "Confirm must not leak into a gameplay jump.");
            yield return null; Assert.That(augments.Augments.Owned.Count, Is.EqualTo(before + 1), "A held confirm never takes the reward twice.");
        }

        [UnityTest] public IEnumerator AugmentArrowSelectionAndSingleFImmediatelyAcquireTheFocusedCard()
        {
            keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();Input.Source.SetDevices(keyboard);
            var choice=world.GetComponent<AugmentRewardChoice>();
            for(int round=0;round<2;round++)
            {
                Assert.That(choice.OpenChoice(),Is.True);
                for(int i=0;i<10&&!Field<GameObject>(Gui,"augmentPanel").activeInHierarchy;i++)yield return null;
                yield return null;
                var cards=Field<Button[]>(Gui,"augmentCards");
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(cards[0].gameObject));
                Assert.That(Field<int>(Gui,"augmentPick"),Is.Zero,"Every new offer starts ready on the first card.");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));yield return null;yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(cards[1].gameObject));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftArrow));yield return null;yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(cards[0].gameObject));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));yield return null;yield return null;
                string id=choice.Offer[1].id;int before=choice.Augments.Owned.Count;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));yield return null;yield return null;
                Assert.That(choice.IsOpen,Is.False,"F on the card must acquire without moving to the lower button.");
                Assert.That(choice.Augments.Has(id),Is.True);
                Assert.That(choice.Augments.Owned.Count,Is.EqualTo(before+1));
                yield return null;yield return null;
                Assert.That(choice.Augments.Owned.Count,Is.EqualTo(before+1),"Holding F cannot acquire twice.");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            }
        }

        [UnityTest] public IEnumerator GenericPadOpensTheLockedChestAndHonorsInventoryAndInteractionBlocks()
        {
            Device(false); yield return Press();
            var keys = world.GetComponent<Chapter1KeyChest>(); keys.Prepare(); Assert.That(keys.Chest, Is.Not.Null);
            Place(keys.Chest.Position + Vector2.left * .5f);
            using (var scope = world.arena.Controls.OpenScope("interaction-only test"))
            {
                scope.BlockInput(InputChannels.Interaction);
                yield return Press(GamepadButton.RightShoulder); Assert.That(keys.ChestKey.Visible, Is.False);
                Assert.That(keys.TryOpenChest(), Is.False);
            }
            keys.GrantKeyForVerification(); yield return null; yield return null;
            Assert.That(keys.Opened, Is.False, "Input held during a restriction must not become a new press.");
            yield return Press(); yield return Press(GamepadButton.LeftShoulder);
            var inventory = world.arena.GetComponent<InventoryRuntimeHost>(); Assert.That(inventory.Presenter.IsOpen, Is.True);
            yield return Press(); yield return Press(GamepadButton.RightShoulder); Assert.That(keys.Opened, Is.False);
            yield return Press(); yield return Press(GamepadButton.East); Assert.That(inventory.Presenter.IsOpen, Is.False);
            world.arena.enemies.Clear();
            yield return Press(); yield return Press(GamepadButton.RightShoulder);
            Assert.That(keys.Opened && keys.Chest.IsOpen, Is.True); Assert.That(keys.HasKey, Is.False); Assert.That(keys.LastReward, Is.EqualTo("choice"));
            Assert.That(world.GetComponent<Chapter1RunBinding>().Run.Sketch.PendingIsEvent, Is.True, "the key opens the ink-or-blueprint choice");
            Assert.That(Caption(keys.ChestKey), Is.EqualTo("RB"));
            int reward = keys.LastAmount; yield return null; yield return null; Assert.That(keys.LastAmount, Is.EqualTo(reward));
        }

        // F both opens the locked chest (Interact) and takes the focused reward card. When the chest's Update runs before the
        // reward binding's in the same frame, that one press must not also take the chest's first card and close the choice.
        [UnityTest] public IEnumerator KeyboardFThatOpensTheLockedChestShowsItsChoiceInsteadOfTakingTheFirstCard()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); Input.Source.SetDevices(keyboard);
            var keys = world.GetComponent<Chapter1KeyChest>(); keys.Prepare(); Assert.That(keys.Chest, Is.Not.Null);
            var binding = world.GetComponent<SketchRunBinding>(); var sketch = world.GetComponent<Chapter1RunBinding>().Run.Sketch;
            Assert.That(binding, Is.Not.Null);
            for (int guard = 0; guard < 8 && sketch.PendingOffer != null; guard++) sketch.Choose(0);
            yield return null; yield return null;
            world.arena.enemies.Clear();
            Place(keys.Chest.Position + Vector2.left * .5f);
            keys.GrantKeyForVerification();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
            // The chest and the binding share the world object, so SendMessage would run both; call each Update on its own.
            Tick(keys);
            Assert.That(keys.Opened, Is.True); Assert.That(keys.LastReward, Is.EqualTo("choice"));
            var offer = sketch.PendingOffer;
            Assert.That(offer, Is.Not.Null); Assert.That(sketch.PendingIsEvent, Is.True);
            Tick(binding);
            Assert.That(sketch.PendingOffer, Is.SameAs(offer), "The F that opened the chest must not also take its first card.");
            yield return null; yield return null;
            Assert.That(sketch.PendingOffer, Is.SameAs(offer), "Holding that F must not take a card either.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null; yield return null;
            Assert.That(sketch.PendingOffer, Is.Null, "A new F press takes the focused card.");
        }

        [UnityTest] public IEnumerator KeyboardInteractOverrideAndDisplayedBindingUseTheSharedAction()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); Input.Source.SetDevices(keyboard);
            Player.GetComponent<CombatKeyboardInput>().Bindings.Interact = Key.T;
            var seal = world.GetComponent<Chapter1SealChallenge>(); seal.Prepare(); world.Warp(seal.Room.id); Place(seal.TabletPosition);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null; yield return null;
            Assert.That(seal.State, Is.EqualTo(Chapter1SealChallenge.ChallengeState.Dormant));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T)); yield return null; yield return null;
            Assert.That(seal.State, Is.EqualTo(Chapter1SealChallenge.ChallengeState.Active)); Assert.That(Caption(seal.Key), Is.EqualTo("T"));
        }

        [UnityTest] public IEnumerator OneInteractionPressIsConsumedOnlyAfterAWorldActionSucceeds()
        {
            Device(false); yield return Press();
            var seal = world.GetComponent<Chapter1SealChallenge>(); seal.Prepare(); world.Warp(seal.Room.id);
            Place(seal.TabletPosition + Vector2.right * 5);
            Queue(GamepadButton.RightShoulder); InputSystem.Update(); seal.SendMessage("Update");
            Assert.That(seal.State, Is.EqualTo(Chapter1SealChallenge.ChallengeState.Dormant));
            Assert.That(PlayerControls.TryPerform(Player, GameAction.Interact, () => true), Is.True, "An out-of-reach candidate cannot steal Interact.");
            yield return Press(); Place(seal.TabletPosition);
            Queue(GamepadButton.RightShoulder); InputSystem.Update(); seal.SendMessage("Update");
            Assert.That(seal.State, Is.EqualTo(Chapter1SealChallenge.ChallengeState.Active));
            Assert.That(PlayerControls.TryPerform(Player, GameAction.Interact, () => true), Is.False, "The successful world action owns this press.");
            yield return null;
        }
    }
}
