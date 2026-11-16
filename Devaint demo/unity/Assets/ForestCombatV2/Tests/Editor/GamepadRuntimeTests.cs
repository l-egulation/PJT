using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Input.Contracts;
using Deviant.Inventory.Unity;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class GamepadRuntimeTests
    {
        Gamepad pad;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        void Devices()
        {
            background = InputSystem.settings.backgroundBehavior; editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Chapter1ProfileHost.UseStore(new MemoryProfileStore()); pad = InputSystem.AddDevice<Gamepad>(); pad.MakeCurrent();
        }
        void State(GamepadState state) => InputSystem.QueueStateEvent(pad, state);
        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
        static T Field<T>(MonoBehaviour owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        [UnityTest] public IEnumerator TitleOpeningVillageWalkingAndStationBackWorkWithOnlyAPad()
        {
            EditorSceneManager.OpenScene("Assets/ForestCombatV2/Scenes/Chapter1Title.unity");
            yield return new EnterPlayMode(); Devices();
            yield return null; yield return null;
            Assert.That(Gui(), Is.Not.Null);
            var title = Field<MonoBehaviour>(Gui(), "wellTitle");
            State(new GamepadState().WithButton(GamepadButton.South));
            yield return null; yield return null;
            StartingVillageWorld village = Object.FindAnyObjectByType<StartingVillageWorld>();
            bool transitioning = title && (bool)title.GetType().GetProperty("Transitioning").GetValue(title);
            Assert.That(village || transitioning, Is.True, "Pad Submit must start the title transition before waiting for the village.");
            State(default);
            // The well title intentionally animates for 0.855 real seconds.
            // Headless frame counts can finish long before that elapsed time.
            float deadline = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < deadline && !(village && village.Ready))
            { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True, "The title's focused start button must accept pad Submit.");
            State(default); yield return null; yield return null;
            var gui = Gui(); Assert.That(gui, Is.Not.Null);
            State(new GamepadState().WithButton(GamepadButton.Start));
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < 1.25f;) yield return null;
            State(default); yield return null; yield return null;
            Assert.That(Field<bool>(gui, "openingActive"), Is.False);
            Assert.That(Field<bool>(gui, "hubPaused"), Is.False);
            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("opening"), Is.True);
            var player = village.World.arena.Player; float start = player.Position.x;
            State(new GamepadState { leftStick = Vector2.right });
            for (float at = Time.realtimeSinceStartup; Time.realtimeSinceStartup - at < .3f;) yield return null;
            State(default); yield return null;
            Assert.That(player.Position.x, Is.GreaterThan(start + .3f));
            player.Position = village.Find(StartingVillageWorld.RecordDesk).Position; player.velocity = Vector2.zero; Physics2D.SyncTransforms();
            yield return null;
            State(new GamepadState().WithButton(GamepadButton.RightShoulder));
            for (int frame = 0; frame < 12 && !Field<GameObject>(gui, "hubOpen"); frame++) yield return null;
            Assert.That(Field<GameObject>(gui, "hubOpen"), Is.Not.Null, "The pad Interact action opens a village station.");
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
            Assert.That(Field<GameObject>(gui, "hubOpen"), Is.Null); Assert.That(player.Dashing, Is.False);
        }

        [UnityTest] public IEnumerator OpenTutorialPromptsFollowPadAndKeyboardWithoutRestartingTheLesson()
        {
            EditorSceneManager.OpenScene(Chapter1RunHost.VillageScene);
            yield return new EnterPlayMode(); Devices();
            {
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready && Gui()); frame++)
            { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready && Gui(), Is.True);
            var gui = Gui(); const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            gui.GetType().GetMethod("SkipOpening", flags).Invoke(gui, null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                PlayerControls.For(village.World.arena.Player).Source.SetDevices(keyboard, pad);
                gui.GetType().GetMethod("ShowBanner", flags).Invoke(gui, new object[] { new[] { "Space", "↑" }, "입력 안내" });
                var ghost = village.FindGhost("jump");
                gui.GetType().GetMethod("ShowGhostLesson", flags).Invoke(gui, new object[] { ghost, ghost.keys, ghost.text });
                float bannerAt = Field<float>(gui, "bannerShown"), lessonAt = Field<float>(gui, "ghostLessonAt");
                State(new GamepadState { leftStick = Vector2.right }); yield return null; yield return null;
                var banner = Field<RectTransform>(gui, "bannerKeys"); var caps = Field<RectTransform>(gui, "ghostKeys");
                Assert.That(banner.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Does.Contain("A").And.Contain("LS/D-Pad 위"));
                Assert.That(caps.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Does.Contain("A"));
                Assert.That(Field<float>(gui, "bannerShown"), Is.EqualTo(bannerAt));
                Assert.That(Field<float>(gui, "ghostLessonAt"), Is.EqualTo(lessonAt));
                State(default); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return null; yield return null;
                Assert.That(caps.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Does.Contain("Space"));
                Assert.That(banner.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Does.Contain("↑"));
                Assert.That(Field<float>(gui, "ghostLessonAt"), Is.EqualTo(lessonAt), "switching devices must not restart the recorded demonstration");
                gui.GetType().GetMethod("CloseLesson", flags).Invoke(gui, null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                gui.GetType().GetMethod("ShowLesson", flags, null, new[] { typeof(string[]), typeof(string) }, null)
                    .Invoke(gui, new object[] { new[] { "Space" }, "점프" });
                float cardAt = Field<float>(gui, "lessonShown");
                State(new GamepadState { leftStick = Vector2.left }); yield return null; yield return null;
                Assert.That(Field<RectTransform>(gui, "lessonKeys").GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Does.Contain("A"));
                Assert.That(Field<float>(gui, "lessonShown"), Is.EqualTo(cardAt));
                // Attack lessons use action IDs, so new defaults and live rebinds cannot leave QWER on screen.
                var input = PlayerControls.For(village.World.arena.Player);
                input.Source.LoadBindingOverrides("{\"bindings\":[]}");
                State(default); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return null; yield return null;
                gui.GetType().GetMethod("CloseLesson", flags).Invoke(gui, null);
                gui.GetType().GetMethod("ShowLesson", flags, null, new[] { typeof(string[]), typeof(string) }, null)
                    .Invoke(gui, new object[] { new[] { "Weapon1Basic", "Weapon2Basic", "Weapon1Special", "Weapon2Special" }, "공격" });
                var attackCaps = Field<RectTransform>(gui, "lessonKeys");
                Assert.That(attackCaps.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Is.EqualTo(new[] { "A", "S", "Q", "W" }));
                var attackAt = Field<float>(gui, "lessonShown");
                var basic = input.Source.GetAction(GameAction.Weapon1Basic);
                int binding = Enumerable.Range(0, basic.bindings.Count).Single(i => basic.bindings[i].path == "<Keyboard>/a");
                input.Source.OverrideBinding(GameAction.Weapon1Basic, binding, "<Keyboard>/z");
                yield return null; yield return null;
                Assert.That(attackCaps.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Is.EqualTo(new[] { "Z", "S", "Q", "W" }));
                Assert.That(Field<float>(gui, "lessonShown"), Is.EqualTo(attackAt));
                var punch = village.FindGhost("punch");
                Assert.That(punch.keys, Is.EqualTo(new[] { "Weapon1Basic" }));
                gui.GetType().GetMethod("CloseLesson", flags).Invoke(gui, null);
                gui.GetType().GetMethod("ShowGhostLesson", flags).Invoke(gui, new object[] { punch, punch.keys, punch.text });
                var attackGhost = Field<RectTransform>(gui, "ghostKeys");
                Assert.That(attackGhost.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Does.Contain("Z"));

            }
            finally { if (keyboard.added) InputSystem.RemoveDevice(keyboard); }
            }
        }

        [UnityTest] public IEnumerator ChapterOnePadChoosesDrawingsAttacksAndOperatesBagPauseAndMap()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode(); Devices();
            ForestCombatWorld world = null; InventoryRuntimeHost bag = null;
            for (int frame = 0; frame < 600 && !(world && world.Ready && bag && bag.Presenter != null && Gui()); frame++)
            { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); if (world) bag = world.arena.GetComponent<InventoryRuntimeHost>(); }
            Assert.That(world && world.Ready && bag && Gui(), Is.True);
            world.arena.enabled = false;
            var player = world.arena.Player; var offer = world.GetComponent<Chapter1StartingBlueprints>();
            PlayerControls.For(player).Source.SetDevices(pad);
            player.Position = offer.OfferPosition(0); Physics2D.SyncTransforms(); yield return null;
            State(new GamepadState().WithButton(GamepadButton.RightShoulder));
            for (int frame = 0; frame < 12 && offer.SelectedCount == 0; frame++) yield return null;
            Assert.That(offer.SelectedCount, Is.EqualTo(1));
            player.Position = offer.OfferPosition(1); Physics2D.SyncTransforms(); yield return null; yield return null;
            Assert.That(offer.SelectedCount, Is.EqualTo(1), "Holding Interact never selects a second drawing.");
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.RightShoulder));
            for (int frame = 0; frame < 12 && !offer.Complete; frame++) yield return null;
            Assert.That(offer.Complete, Is.True);
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.North)); yield return null;
            world.arena.SendMessage("FixedUpdate");
            Assert.That(player.currentCast, Is.Not.Null); Assert.That(player.selected, Is.EqualTo(1));
            State(default); yield return null; world.arena.ResetTest(); yield return null;
            State(new GamepadState().WithButton(GamepadButton.East)); yield return null;
            world.arena.SendMessage("FixedUpdate");
            Assert.That(player.Dashing, Is.True, "East is Dash during gameplay, not a request to open Pause.");
            Assert.That(bag.Presenter.Paused, Is.False);
            State(default); yield return null; world.arena.ResetTest(); yield return null;
            State(new GamepadState().WithButton(GamepadButton.LeftShoulder));
            for (int frame = 0; frame < 12 && !bag.Presenter.IsOpen; frame++) yield return null;
            Assert.That(bag.Presenter.IsOpen, Is.True); Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
            Assert.That(Gui().GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Does.Contain("B").And.Contain("LB/RB").And.Contain("LS/D-Pad"));
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
            Assert.That(bag.Presenter.IsOpen, Is.False); Assert.That(player.Dashing, Is.False);
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.Start)); yield return null; yield return null;
            Assert.That(bag.Presenter.Paused, Is.True);
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.South)); yield return null; yield return null;
            Assert.That(bag.Presenter.Paused, Is.False, "Submit activates the focused resume button.");
            world.arena.SendMessage("FixedUpdate"); Assert.That(player.jumps.Used, Is.Zero);
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.Select)); yield return null; yield return null;
            Assert.That(world.FullMap.IsOpen, Is.True);
            Assert.That(Gui().GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text), Does.Contain("현재 위치 [X]").And.Contain("전체 보기 [Y]"));
            float zoom = world.FullMap.Viewport.Zoom;
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.RightShoulder)); yield return null;
            Assert.That(world.FullMap.Viewport.Zoom, Is.GreaterThan(zoom));
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
            Assert.That(world.FullMap.IsOpen, Is.False); Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
        }

        [UnityTest] public IEnumerator BankedRewardOpensWithStickAndInventoryPromptsFollowDeviceWhileOpen()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();Devices();
            ForestCombatWorld world=null;InventoryRuntimeHost bag=null;
            for(int frame=0;frame<600&&!(world&&world.Ready&&bag&&Gui());frame++)
            {yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();if(world)bag=world.arena.GetComponent<InventoryRuntimeHost>();}
            Assert.That(world&&world.Ready&&bag&&Gui(),Is.True);
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
            Chapter1ProfileHost.Service.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
            world.arena.enabled=false;
            var sketch=world.GetComponent<Chapter1RunBinding>().Sketch;
            var input=PlayerControls.For(world.arena.Player);
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                input.Source.SetDevices(keyboard,pad);
                sketch.State.RecordKill("elite",200);
                Assert.That(sketch.State.Banked,Is.GreaterThan(0));
                State(new GamepadState{rightStick=Vector2.up});yield return null;yield return null;
                Assert.That(sketch.State.PendingOffer,Is.Not.Null,"RS up opens the banked reward");
                Assert.That(world.arena.Controls.Current.SimulationPaused,Is.True);
                State(default);yield return null;
                State(new GamepadState().WithButton(GamepadButton.LeftShoulder));yield return null;yield return null;
                Assert.That(bag.Presenter.IsOpen,Is.True);
                var root=Gui().GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Approved core content");
                var padLabels=root.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t=>t.text).ToArray();
                Assert.That(padLabels,Does.Contain("A").And.Contain("B").And.Contain("RS").And.Contain("LS/D-Pad"));
                var pending=sketch.State.PendingOffer;
                State(default);yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G));yield return null;yield return null;
                var keys=root.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t=>t.text).ToArray();
                Assert.That(keys,Does.Contain("F").And.Contain("Esc").And.Contain("W / S"));
                Assert.That(sketch.State.PendingOffer,Is.SameAs(pending),"device changes and G in the bag preserve the offer");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                State(new GamepadState{rightStick=Vector2.up});yield return null;yield return null;
                Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t=>t.text),Does.Contain("A"));
                Assert.That(bag.Presenter.IsOpen,Is.True,"scrolling never opens another reward");
                State(default);yield return null;
                State(new GamepadState().WithButton(GamepadButton.East));yield return null;yield return null;
                Assert.That(bag.Presenter.IsOpen,Is.False);Assert.That(sketch.State.PendingOffer,Is.SameAs(pending));
                State(default);yield return null;
                State(new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
                Assert.That(sketch.State.PendingOffer,Is.Null,"South acquires the selected reward");
                Assert.That(world.arena.Controls.Current.SimulationPaused,Is.False);
            }
            finally {if(keyboard.added)InputSystem.RemoveDevice(keyboard);}
        }

        [UnityTest] public IEnumerator ChapterTwoUsesTheSamePadMenuAndMovementBindings()
        {
            EditorSceneManager.OpenScene(Deviant.Chapter2.Chapter2EntryBridge.ScenePath);
            yield return new EnterPlayMode(); Devices();
            Deviant.Chapter2.Chapter2World world = null; InventoryRuntimeHost bag = null;
            for (int frame = 0; frame < 600 && !(world && world.Ready && bag && Gui()); frame++)
            { yield return null; world = Object.FindAnyObjectByType<Deviant.Chapter2.Chapter2World>(); if (world && world.Ready) bag = world.arena.GetComponent<InventoryRuntimeHost>(); }
            Assert.That(world && world.Ready && bag && Gui(), Is.True); world.arena.enabled = false;
            var input = PlayerControls.For(world.arena.Player); input.Source.SetDevices(pad);
            State(new GamepadState { leftStick = Vector2.right }); yield return null; world.arena.SendMessage("FixedUpdate");
            Assert.That(world.arena.Player.axis, Is.EqualTo(1)); Assert.That(input.Label(GameAction.Jump), Is.EqualTo("A"));
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.Select)); yield return null; yield return null;
            Assert.That(world.FullMap.IsOpen, Is.True);
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
            Assert.That(world.FullMap.IsOpen, Is.False); Assert.That(world.arena.Player.Dashing, Is.False);
            State(default); yield return null;
            State(new GamepadState().WithButton(GamepadButton.Start)); yield return null; yield return null;
            Assert.That(bag.Presenter.Paused, Is.True);
        }
    }
}
