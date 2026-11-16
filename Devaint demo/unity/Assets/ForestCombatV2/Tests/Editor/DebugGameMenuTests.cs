using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Control.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace ForestCombatV2.Tests
{
    public sealed class DebugGameMenuTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { DevFeatures.ForceForTests(null); Chapter1ProfileHost.UseStore(null); if(Application.isPlaying) yield return new ExitPlayMode(); }
        static void SelectItem(DebugGameMenu menu,string prefix)
        {
            for(int i=0;i<256;i++)
            {
                if(menu.SelectedLabel!=null&&menu.SelectedLabel.StartsWith(prefix))return;
                menu.MoveSelection(1);
            }
            Assert.Fail("Console item not found: "+prefix);
        }
        static void ChooseItem(DebugGameMenu menu,string prefix){SelectItem(menu,prefix);Assert.IsTrue(menu.ExecuteSelected());}
        static void AssertSketchLocked(Deviant.Run.SketchEquipment equipment,System.Guid weapon,int index)
        { Assert.Throws<System.InvalidOperationException>(()=>equipment.Place(weapon,index,null)); }

        [UnityTest] public IEnumerator VillageSummitTravelUsesMenuAndKeepsPracticeRecords()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            StartingVillageWorld village=null;
            for(int i=0;i<600&&!(village&&village.Ready);i++){yield return null;village=Object.FindFirstObjectByType<StartingVillageWorld>();}
            Assert.IsTrue(village&&village.Ready);yield return null;yield return null;
            var gui=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.GetType().Name=="TemporaryGameGui");
            const System.Reflection.BindingFlags Private=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            gui.GetType().GetMethod("SkipOpening",Private).Invoke(gui,null);
            var active=gui.GetType().GetField("storyActive",Private);var advance=gui.GetType().GetMethod("AdvanceStory",Private);
            for(int i=0;i<30&&(bool)active.GetValue(gui);i++)advance.Invoke(gui,null);
            for(int i=0;i<300;i++){yield return null;village=Object.FindFirstObjectByType<StartingVillageWorld>();if(village&&village.Ready&&village.World.arena.Player)break;}
            for(int i=0;i<30;i++)yield return null;
            Assert.IsTrue(village.InteractionAllowed);Assert.IsTrue(village.SkyBuilt);
            var world=village.World;var player=world.arena.Player;var before=player.Position;
            var settings=InputSystem.settings;var oldBackground=settings.backgroundBehavior;var oldEditor=settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();InputSystem.EnableDevice(keyboard);
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G));InputSystem.Update();keyboard.MakeCurrent();
                Assert.IsTrue(keyboard.gKey.wasPressedThisFrame);
                typeof(StartingVillageWorld).GetMethod("Update",Private).Invoke(village,null);
                Assert.AreEqual(before,player.Position,"Legacy G must no longer warp the player");
            }
            finally{InputSystem.RemoveDevice(keyboard);settings.backgroundBehavior=oldBackground;settings.editorInputBehaviorInPlayMode=oldEditor;}
            var menu=world.GetComponent<DebugGameMenu>();Assert.IsTrue(menu.Open());
            using(var foreign=world.arena.Controls.OpenScope("summit travel external pause"))
            {
                foreign.PauseSimulation();Assert.IsFalse(menu.TryTravelToSkySummit());Assert.AreEqual(before,player.Position);
            }
            Assert.IsTrue(menu.TryTravelToSkySummit());Assert.IsTrue(village.SkyAssisted);
            Assert.Greater(village.SkyHeight,village.Sky.goalY);Assert.AreEqual(Vector2.zero,player.velocity);
            Assert.IsTrue(world.arena.Controls.Current.SimulationPaused);
            menu.Close();
            using(var driver=world.arena.AcquireManualSimulation())
                for(int i=0;i<240&&!village.SkyCleared;i++)driver.Step(1f/120);
            Assert.IsTrue(village.SkyCleared,"Closing the menu lets the summit arrival finish");
            var records=Chapter1ProfileHost.Service.Snapshot.SkyParkourRecords;
            Assert.AreEqual(1,records.Count);Assert.IsTrue(records[0].assisted);
            Assert.IsNull(Chapter1ProfileHost.Service.Snapshot.BestSkyParkour,"Debug travel never becomes a best record");
        }

        [UnityTest] public IEnumerator SketchEquipmentAndAllRunInksRemainAppliedAfterConsoleCloseAndReset()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            ForestCombatV2.Editor.DummyTrainingTools.SelectNormalMode();
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            UnityEngine.SceneManagement.SceneManager.LoadScene(Chapter1RunHost.ChapterScene);
            ForestCombatWorld world=null;MonoBehaviour gui=null;
            for(int i=0;i<600;i++)
            {
                world=Object.FindFirstObjectByType<ForestCombatWorld>();
                gui=Object.FindObjectsByType<MonoBehaviour>().FirstOrDefault(component=>component.GetType().Name=="TemporaryGameGui");
                var host=world&&world.arena?world.arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>():null;
                if(world&&world.Ready&&world.arena.Player&&world.arena.Sketch!=null&&host&&host.Presenter!=null&&gui&&
                    gui.GetComponentsInChildren<Transform>(true).Any(child=>child.name=="Inventory equipment book"))break;
                yield return null;
            }
            Assert.IsTrue(world&&world.Ready&&world.arena.Player&&world.arena.Sketch!=null&&gui);
            var arena=world.arena;
            Assert.IsNotNull(arena);
            var player=arena.Player;
            Assert.IsNotNull(player);
            player.UseTestInput=true;
            var equipment=arena.Sketch;
            Assert.IsNotNull(equipment,"The ready chapter exposes its sketch equipment.");
            foreach(var enemy in arena.enemies)if(enemy)enemy.aggressive=false;
            var guide=world.GetComponent<Chapter1FirstRunInkGuide>();
            if(guide){guide.FinishNotebook();guide.enabled=false;}
            if(gui)gui.GetType().GetMethod("DismissDepartureCard")?.Invoke(gui,null);
            Assert.AreSame(equipment,arena.Sketch,"Closing an initial guide keeps the chapter equipment.");
            int gold=equipment.Economy.Gold;
            var menu=world.GetComponent<DebugGameMenu>();Assert.IsNotNull(menu,"The runtime-loaded chapter installs the debug console.");
            Assert.IsFalse(menu.TryAddReward(), "closed console must not grant a reward");
            int banked = equipment.Banked, opened = equipment.State.OffersOpened;
            float gauge = equipment.State.Gauge, requirement = equipment.State.Requirement;
            Assert.IsTrue(menu.Open()); menu.SelectCategory(1);
            ChooseItem(menu, "보상 +1");
            Assert.AreEqual(banked + 1, equipment.Banked);
            Assert.That(menu.Status, Does.Contain("보상 +1 완료"));
            Assert.IsTrue(DebugGameMenu.IsOpen); Assert.IsTrue(arena.Controls.Current.SimulationPaused);
            Assert.IsFalse(equipment.PendingReward, "the console adds a banked reward without opening a modal");
            Assert.AreEqual(gauge, equipment.State.Gauge); Assert.AreEqual(requirement, equipment.State.Requirement);
            Assert.AreEqual(opened, equipment.State.OffersOpened);
            menu.Close(); Assert.AreEqual(banked + 1, equipment.Banked);
            Assert.IsFalse(menu.TryAddReward()); Assert.AreEqual(banked + 1, equipment.Banked);
            int sword=System.Array.FindIndex(arena.catalog.forms,definition=>definition.id=="base_sword");
            int hammer=System.Array.FindIndex(arena.catalog.forms,definition=>definition.id=="hammer");
            int rapier=System.Array.FindIndex(arena.catalog.forms,definition=>definition.id=="rapier");
            using(var driver=arena.AcquireManualSimulation())
            {
                Assert.IsTrue(menu.Open());menu.SelectCategory(2);
                ChooseItem(menu,"무기 형태:");yield return null;
                ChooseItem(menu,arena.catalog.forms[sword].name);yield return null;
                var first=equipment.Equipped(0);Assert.IsNotNull(first);Assert.IsTrue(first.Confirmed);
                menu.SelectWeapon(1);yield return null;
                ChooseItem(menu,"무기 형태:");yield return null;
                ChooseItem(menu,arena.catalog.forms[hammer].name);yield return null;
                var second=equipment.Equipped(1);Assert.IsNotNull(second);
                menu.Close();
                driver.Step(.02f,new Deviant.Combat.Contracts.CombatInput(pressed:Deviant.Combat.Contracts.CombatButtons.Weapon1Basic));
                Assert.IsNotNull(player.currentCast,"Open the console while a real sketch weapon is attacking.");
                Assert.IsTrue(menu.Open());menu.SelectCategory(2);menu.SelectWeapon(0);
                ChooseItem(menu,"무기 형태:");yield return null;
                ChooseItem(menu,arena.catalog.forms[rapier].name);yield return null;
                ChooseItem(menu,"스킬:");yield return null;
                ChooseItem(menu,arena.catalog.forms[rapier].specials[2].name);yield return null;
                Assert.IsNull(player.currentCast);
                Assert.AreSame(first,equipment.Equipped(0));Assert.AreEqual("rapier",first.Form);Assert.AreEqual(2,first.Skill);
                Assert.AreEqual(first.Id,player.weapons[0].InstanceId);Assert.AreEqual(rapier,player.weapons[0].FormIndex);
                Assert.AreEqual(2,player.weapons[0].SpecialIndex);
                menu.SelectWeapon(1);menu.SelectPaletteSlot(1);yield return null;
                var mount=second.Slots[1];var neighbor=second.Slots[0].Color;
                ChooseItem(menu,"색상 (선택 즉시 적용)");yield return null;
                ChooseItem(menu,"보라");yield return null;
                var ink=equipment.Catalog.inks.First(candidate=>candidate.trigger=="basic_attack"&&candidate.color=="purple"&&candidate.effect=="slow");
                menu.SelectInkSlot(1);
                ChooseItem(menu,"장착할 잉크:");yield return null;
                ChooseItem(menu,ink.name);yield return null;
                Assert.AreSame(ink,mount.Ink);Assert.AreEqual(ink.enhancedValue,mount.Value);
                Assert.AreEqual(neighbor,second.Slots[0].Color);
                Assert.IsTrue(menu.TryApplyPaletteColor("neutral",out var error),error);
                Assert.AreEqual(ink.baseValue,mount.Value,"Different colors use the base effect rather than refusing the ink.");
                foreach(var candidate in equipment.Catalog.inks)
                {
                    Assert.IsTrue(menu.TryApplyRunInk(candidate.id,out error),error);
                    Assert.AreSame(candidate,mount.Ink);
                }
                Assert.IsTrue(menu.TryApplyRunInk(ink.id,out error),error);
                Assert.IsTrue(menu.TryResizePalette(4,out error),error);Assert.AreSame(mount,second.Slots[1]);
                menu.SelectInkSlot(3);Assert.IsTrue(menu.TryApplyRunInk(ink.id,out error),error);
                Assert.IsFalse(menu.TryResizePalette(3,out error));Assert.AreEqual(4,second.Slots.Count);
                Assert.IsTrue(menu.TryApplyRunInk(null,out error),error);
                Assert.IsTrue(menu.TryResizePalette(3,out error),error);Assert.AreSame(mount,second.Slots[1]);
                menu.SelectInkSlot(1);Assert.IsFalse(menu.TryApplyRunInk("missing",out error));Assert.AreSame(ink,mount.Ink);
                Assert.IsFalse(menu.TryApplyEquipment(0,-1,0,out error));
                Assert.AreEqual(gold,equipment.Economy.Gold);Assert.AreEqual(0,equipment.RegisteredCount);
                AssertSketchLocked(equipment,second.Id,1);
                menu.Close();arena.ResetTest();yield return null;
                arena.SyncSketchLoadout();
                Assert.AreSame(first,equipment.Equipped(0));Assert.AreSame(second,equipment.Equipped(1));
                Assert.AreEqual(rapier,player.weapons[0].FormIndex);Assert.AreEqual(2,player.weapons[0].SpecialIndex);
                Assert.AreSame(ink,mount.Ink);Assert.IsFalse(menu.TryApplyRunInk(null,out error),"A closed console cannot edit the run.");
                driver.Step(.01f,new Deviant.Combat.Contracts.CombatInput(pressed:Deviant.Combat.Contracts.CombatButtons.Weapon1Basic));
                Assert.IsNotNull(player.currentCast);Assert.AreEqual(first.Id,player.currentCast.SourceWeaponId);
                Assert.IsFalse(arena.SimulationFaulted);
            }
        }

        [UnityTest] public IEnumerator MonsterPlacementUsesSceneNamesMouseEnterAndGamepadWhilePaused()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindFirstObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&!world.Ready;i++)yield return null;
            Assert.IsTrue(world.Ready);yield return null;
            var menu=world.GetComponent<DebugGameMenu>();Assert.IsTrue(menu.Open());
            menu.SelectCategory(4);
            var kind=world.Data.monsters.First(m=>m.id=="02_broom-stalker");
            ChooseItem(menu,"몬스터 종류:");yield return null;
            ChooseItem(menu,kind.name);yield return null;
            SelectItem(menu,"몬스터 종류:");StringAssert.Contains(kind.name,menu.SelectedLabel);
            ChooseItem(menu,"배치 위치 선택");
            Assert.IsTrue(world.arena.Controls.Current.SimulationPaused);
            int before=world.arena.enemies.Count;
            // Opening placement and submitting in the same frame must not create an actor.
            var update=typeof(DebugGameMenu).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            update.Invoke(menu,null);Assert.AreEqual(before,world.arena.enemies.Count);
            yield return null;

            var input=InputSystem.settings;
            var oldBackground=input.backgroundBehavior;var oldEditor=input.editorInputBehaviorInPlayMode;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();InputSystem.EnableDevice(keyboard);
            var mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();InputSystem.EnableDevice(mouse);
            var pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();InputSystem.EnableDevice(pad);
            try
            {
                var point=world.arena.Player.Position+Vector2.right*3+Vector2.up*.4f;
                Vector2 screen=Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(world.View,point);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});InputSystem.Update();mouse.MakeCurrent();
                update.Invoke(menu,null);
                Assert.Less(Vector2.Distance(point,menu.MonsterCursor),.01f,"Mouse movement uses the displayed camera rectangle");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));InputSystem.Update();keyboard.MakeCurrent();
                Assert.IsTrue(keyboard.enterKey.wasPressedThisFrame);update.Invoke(menu,null);
                Assert.AreEqual(before+1,world.arena.enemies.Count,menu.Status);
                var actor=(FieldMouseEnemy)world.arena.enemies.Last();
                Assert.AreEqual(kind.id,actor.Definition.id);Assert.IsFalse(actor.aggressive);
                Assert.AreEqual(point.x,actor.Position.x,.01f);StringAssert.Contains("배치 완료",menu.Status);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();

                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,buttons=1});InputSystem.Update();mouse.MakeCurrent();
                Assert.IsTrue(mouse.leftButton.wasPressedThisFrame);update.Invoke(menu,null);
                Assert.AreEqual(before+2,world.arena.enemies.Count,menu.Status);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});InputSystem.Update();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();pad.MakeCurrent();
                Assert.IsTrue(pad.buttonSouth.wasPressedThisFrame);update.Invoke(menu,null);
                Assert.AreEqual(before+3,world.arena.enemies.Count,menu.Status);
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();

                var cursor=menu.MonsterCursor;
                // Top toolbar and screen exterior cannot become click placement positions.
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width/2,Screen.height-40),buttons=1});InputSystem.Update();
                update.Invoke(menu,null);Assert.AreEqual(before+3,world.arena.enemies.Count);Assert.AreEqual(cursor,menu.MonsterCursor);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-10,-10)});InputSystem.Update();
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-10,-10),buttons=1});InputSystem.Update();
                update.Invoke(menu,null);Assert.AreEqual(before+3,world.arena.enemies.Count);Assert.AreEqual(cursor,menu.MonsterCursor);
                StringAssert.Contains("게임 화면 안",menu.Status);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});InputSystem.Update();

                var settings=world.GetComponent<Chapter1MonsterTestSettings>();
                Assert.IsFalse(settings.TryPlace("missing-species",point,out var invalid));Assert.IsNull(invalid);
                StringAssert.Contains("현재 씬",settings.Status);
                Assert.IsFalse(settings.TryPlace(kind.id,new Vector2(100000,100000),out invalid));Assert.IsNull(invalid);
                StringAssert.Contains("바닥",settings.Status);Assert.AreEqual(before+3,world.arena.enemies.Count);
                Assert.IsTrue(world.arena.Controls.Current.SimulationPaused);
                Assert.IsTrue(PlayerControls.For(world.arena.Player).MenuCaptured);
                menu.Back();Assert.IsTrue(DebugGameMenu.IsOpen);
                yield return null;
                ChooseItem(menu,"현재 테스트 몬스터 제거");yield return null;
                ChooseItem(menu,"확인");Assert.AreEqual(before,world.arena.enemies.Count,"Only console-created monsters are removed");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(pad);
                input.backgroundBehavior=oldBackground;input.editorInputBehaviorInPlayMode=oldEditor;
            }
        }

        [UnityTest] public IEnumerator MenuOwnsOnlyItsPauseAndInputScope()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindFirstObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&!world.Ready;i++)yield return null;
            Assert.IsTrue(world.Ready);
            var menu=world.GetComponent<DebugGameMenu>();Assert.IsNotNull(menu);
            var controls=world.arena.Controls;
            using(var foreign=controls.OpenScope("test existing pause"))
            {
                foreign.PauseSimulation();
                Assert.IsTrue(menu.Open());
                Assert.IsTrue(controls.Current.Blocks(InputChannels.Combat|InputChannels.Interaction));
                Assert.IsTrue(PlayerControls.For(world.arena.Player).MenuCaptured);
                menu.NavigateSelection(Vector2.right);Assert.AreEqual("플레이어·재화·전투",menu.SelectedLabel);
                menu.NavigateSelection(Vector2.down);Assert.AreEqual("로그라이트 해금",menu.SelectedLabel);
                menu.NavigateSelection(Vector2.left);Assert.AreEqual("장비·잉크",menu.SelectedLabel);
                Assert.IsTrue(menu.ExecuteSelected());
                menu.SelectCategory(1);SelectItem(menu,"목표 수량:");
                int gold=world.GetComponent<Chapter1RunBinding>().Run.Economy.Gold;
                menu.NavigateSelection(Vector2.right);menu.NavigateSelection(Vector2.down);
                Assert.AreEqual("골드 적용",menu.SelectedLabel,"Inline apply remains reachable after editing a value");
                Assert.IsTrue(menu.ExecuteSelected());Assert.AreEqual(gold+100,world.GetComponent<Chapter1RunBinding>().Run.Economy.Gold);
                menu.SelectCategory(2);
                menu.NavigateSelection(Vector2.right);Assert.That(menu.SelectedLabel,Does.StartWith("편집할 무기"),"Horizontal input edits tabs rather than leaving the control");
                menu.MoveSelection(1);Assert.IsTrue(menu.ExecuteSelected());
                yield return null;
                Assert.IsTrue(DebugGameMenu.IsOpen);
                var settings=InputSystem.settings;
                var oldBackground=settings.backgroundBehavior;var oldEditor=settings.editorInputBehaviorInPlayMode;
                settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();InputSystem.EnableDevice(keyboard);
                try
                {
                    void Escape()
                    {
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();keyboard.MakeCurrent();
                        Assert.IsTrue(keyboard.escapeKey.wasPressedThisFrame);
                        typeof(DebugGameMenu).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu,null);
                    }
                    Escape();Assert.IsTrue(DebugGameMenu.IsOpen,"Escape returns from the named choice list");
                    Escape();Assert.IsTrue(DebugGameMenu.IsOpen,"Escape returns from equipment to console root");
                    yield return null;Assert.AreEqual("이동·진행",menu.SelectedLabel);
                    Escape();Assert.IsFalse(DebugGameMenu.IsOpen,"Escape closes only at the console root");
                }
                finally
                {
                    InputSystem.RemoveDevice(keyboard);settings.backgroundBehavior=oldBackground;settings.editorInputBehaviorInPlayMode=oldEditor;
                }
                Assert.IsTrue(controls.Current.SimulationPaused);
            }
            Assert.IsFalse(controls.Current.SimulationPaused);
            DevFeatures.ForceForTests(false);Assert.IsFalse(menu.Open());
        }
    }
}
