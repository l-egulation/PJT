using System.Collections;
using System.Linq;
using Deviant.Chapter2.Editor;
using Deviant.Combat.Contracts;
using Deviant.Inventory.Unity;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2Tests
    {
        static void ExecuteDebugItem(DebugGameMenu menu, string prefix)
        {
            menu.NavigateSelection(Vector2.zero); // Refresh rows just as the next UI/input frame does.
            for (int i = 0; i < 256; i++)
            {
                if (menu.SelectedLabel != null && menu.SelectedLabel.StartsWith(prefix))
                { Assert.IsTrue(menu.ExecuteSelected()); return; }
                menu.MoveSelection(1);
            }
            Assert.Fail("Console item not found: " + prefix);
        }

        [UnityTest] public IEnumerator DebugStageButtonsCancelThenTravelBetweenChaptersAndVillage()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.ChapterScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            ForestCombatWorld first = null;
            for (int i = 0; i < 600 && !(first && first.Ready); i++)
            { yield return null; first = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(first && first.Ready);
            var old = Chapter1RunHost.Instance.Flow.Current;
            var menu = first.GetComponent<DebugGameMenu>();
            Assert.IsTrue(menu.Open()); menu.SelectCategory(0);
            ExecuteDebugItem(menu, "2챕터로 이동"); menu.Back();
            Assert.IsTrue(DebugGameMenu.IsOpen);
            Assert.AreSame(old, Chapter1RunHost.Instance.Flow.Current);
            ExecuteDebugItem(menu, "2챕터로 이동"); ExecuteDebugItem(menu, "확인하고 적용");
            Chapter2World second = null;
            for (int i = 0; i < 600 && !(second && second.Ready); i++)
            { yield return null; second = Object.FindAnyObjectByType<Chapter2World>(); }
            Assert.IsTrue(second && second.Ready);
            Assert.IsFalse(DebugGameMenu.IsOpen);
            Assert.IsFalse(second.arena.Controls.Current.SimulationPaused);
            Assert.AreEqual(2, second.SourceRun.Chapter);
            Assert.AreNotSame(old, second.SourceRun);
            Assert.IsFalse(second.SourceRun.BossDefeated);
            Assert.IsFalse(old.Ended);
            menu = second.arena.GetComponent<DebugGameMenu>();
            Assert.IsTrue(menu.Open()); menu.SelectCategory(0);
            ExecuteDebugItem(menu, "1챕터로 이동"); ExecuteDebugItem(menu, "확인하고 적용");
            first = null;
            for (int i = 0; i < 600 && !(first && first.Ready); i++)
            { yield return null; first = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(first && first.Ready);
            Assert.AreEqual(1, Chapter1RunHost.Instance.Flow.Current.Chapter);
            Assert.IsFalse(DebugGameMenu.IsOpen);
            Assert.IsFalse(first.arena.Controls.Current.SimulationPaused);
            menu = first.GetComponent<DebugGameMenu>();
            Assert.IsTrue(menu.Open()); menu.SelectCategory(0);
            ExecuteDebugItem(menu, "마을로 이동"); ExecuteDebugItem(menu, "확인하고 적용");
            for (int i = 0; i < 600 && !Object.FindAnyObjectByType<StartingVillageWorld>(); i++) yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<StartingVillageWorld>());
            Assert.IsFalse(DebugGameMenu.IsOpen);
            Chapter1ProfileHost.UseStore(null);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator ChapterTwoKeepsApprovedWeaponSpeedWhenReconfiguredAndReset()
        {
            yield return new EnterPlayMode();
            Chapter2World world=null;
            float deadline=Time.realtimeSinceStartup+30;
            do { yield return null;world=Object.FindAnyObjectByType<Chapter2World>(); }
            while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            Assert.That(world&&world.Ready,Is.True);
            var arena=world.arena;var player=arena.Player;
            foreach(var trial in new[]{("whip",1.4f),("hammer",1f),("greatsword",.8f),("launcher",1f)})
            {
                int form=System.Array.FindIndex(arena.catalog.forms,f=>f.id==trial.Item1);
                world.WarpToRoom("M");
                Assert.That(player.TryReconfigure(0,form,0,out var error),Is.True,error);
                Assert.That(player.weapons[0].TestFormSpeedScale,Is.EqualTo(trial.Item2).Within(.00001f),trial.Item1);
                arena.ResetTest();
                Assert.That(player.weapons[0].TestFormSpeedScale,Is.EqualTo(trial.Item2).Within(.00001f),"reset "+trial.Item1);
            }
            yield return new ExitPlayMode();
        }
        [Test] public void JumpFramingWaitsForLandingOnAnotherLevel()
        {
            var f=new Chapter2CameraFraming();float view=f.Step(0,true,0,true);
            for(int i=0;i<=120;i++){float t=i/120f;Assert.That(f.Step(6*4*t*(1-t),false,view),Is.EqualTo(view),"double-jump-height arc must hold");}
            Assert.That(f.Step(0,true,view),Is.EqualTo(view),"same floor landing holds");
            Assert.That(f.Step(3,false,view),Is.EqualTo(view),"reaching a higher ledge in the air is not landing");
            Assert.That(f.Step(3,true,view),Is.EqualTo(6.7f).Within(.001f),"only actual landing changes height");
            Assert.That(f.Step(30,false,6.7f),Is.GreaterThan(6.7f),"long wall climb stays visible");
        }
        [Test] public void FallsAndShaftsLeadTheVerticalMove()
        {
            const float halfHeight=8.4375f;
            var f=new Chapter2CameraFraming();float view=f.Step(0,0,true,0,true);
            Assert.That(f.Step(-.4f,-5,false,view),Is.EqualTo(view),"stepping off inside the jump band holds");
            float fall=f.Step(-3,-20,false,view);
            Assert.That(f.Tracking,Is.True);
            Assert.That(fall,Is.LessThan(Chapter2CameraFraming.FallingLimit(-3)),"falling leads below the feet");
            Assert.That(-3-(fall-halfHeight),Is.GreaterThan(halfHeight-Chapter2CameraFraming.GroundOffset),"a fall shows more ground below than standing");
            Assert.That(f.Step(-2.5f,5,false,fall),Is.EqualTo(fall),"a mid-fall double jump does not swing the view back up");
            Assert.That(f.Step(-10,0,true,fall),Is.EqualTo(-10+Chapter2CameraFraming.GroundOffset).Within(.001f),"landing restores standing framing");
            Assert.That(f.Tracking,Is.False);
            f=new Chapter2CameraFraming();view=f.Step(0,0,true,0,true);
            float shaft=f.Step(7,10,false,view);
            Assert.That(shaft,Is.GreaterThan(Chapter2CameraFraming.RisingLimit(7)),"climbing above the double-jump band leads upward");
            Assert.That(f.Step(6.5f,-6,false,shaft),Is.EqualTo(shaft),"wall sliding between wall jumps does not swing the view down");
        }
        [Test] public void ApprovedWaterwayAtlasUsesNativePixels()
        {
            var atlas=Resources.Load<Texture2D>(Chapter2WaterwayArt.Root+"WaterwayRuntimeAtlas");
            Assert.That(atlas,Is.Not.Null);Assert.That(atlas.width,Is.EqualTo(1024));Assert.That(atlas.height%32,Is.Zero);
            Assert.That(atlas.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(atlas.mipmapCount,Is.EqualTo(1));
            foreach(var key in new[]{"wall","arch","portal-view","city","window-frame","grille","platform-middle"})Assert.That(Resources.Load<Texture2D>(Chapter2WaterwayArt.Root+key),Is.Not.Null,key);
        }
        [SetUp] public void PrepareNativeScene()
        {
            if(Application.isPlaying)return;EditorSceneManager.playModeStartScene=null;var name=TestContext.CurrentContext.Test.MethodName;
            if(name!=nameof(NativeSceneInitializesMovementScopesMaintenanceAndBossExit)&&name!=nameof(ExistingChapterTwoActionLoadsPlayableScene)&&name!=nameof(ChapterTwoKeepsApprovedWeaponSpeedWhenReconfiguredAndReset))return;
            // The wall-kick check (WallKickPair) needs a 5~6u gap between two tall walls, which only some seeds build (about 35 of 60
            // since the chapter 2 seal and chest rooms, 2026-11-13); seed 3 has one.
            Chapter2Tools.Create();var world=Object.FindAnyObjectByType<Chapter2World>();Assert.That(world,Is.Not.Null);world.seed=3;
            if(name==nameof(ExistingChapterTwoActionLoadsPlayableScene))EditorSceneManager.OpenScene(Chapter1RunHost.ChapterScene);
        }
        [Test] public void ClearAndHealOwnershipIsOncePerAuthoredRoom()
        {
            var state=new Chapter2RoomState();int count=0;state.Cleared+=_=>count++;state.Register("F01","a");state.Register("F01","b");state.Register("F02","c");Assert.That(state.Defeat("F02","a"),Is.False);state.Defeat("F01","a");Assert.That(count,Is.Zero);state.Defeat("F01","b");state.Defeat("F01","b");Assert.That(count,Is.EqualTo(1));Assert.That(state.IsCleared("F01"),Is.True);Assert.That(state.IsCleared("F02"),Is.False);Assert.That(state.CanEngage("F01","F02"),Is.False);Assert.That(state.CanEngage("F01",null),Is.False);
        }
        [UnityTest] public IEnumerator NativeSceneInitializesMovementScopesMaintenanceAndBossExit()
        {
            yield return new EnterPlayMode();
            Chapter2World world=null;
            float deadline=Time.realtimeSinceStartup+30;
            do
            {
                yield return null;
                world=Object.FindAnyObjectByType<Chapter2World>();
            }while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            Assert.That(world,Is.Not.Null,"Chapter2 scene startup");
            Assert.That(world.Ready,Is.True);
            Assert.That(world.GetComponent<Chapter2WaterwayBackground>(),Is.Not.Null);
            Assert.That(world.GetComponent<Chapter2WaterwayArt>(),Is.Not.Null);
            Assert.That(world.Layout,Is.Not.Null);
            Assert.That(world.Layout.rooms,Is.Not.Null);
            Assert.That(world.Layout.rooms.Length,Is.EqualTo(16));
            // 일반방 12개 × 6~10 + 엘리트 + 보스 3(블록 맵 원형 기준)
            Assert.That(world.Actors.Count,Is.GreaterThan(80));yield return null;yield return null;AssertCommonGui(world);
            // 일반 적은 방 밖에서도 남아 싸운다(사용자 결정 2026-11-09). 맵 전체 목록이 아니라 정비소 방 안에 적이 없는지를 본다.
            world.WarpToRoom("M");Assert.That(world.CurrentRoom,Is.EqualTo("M"));var maintenanceRoom=world.Layout.Room("M").Bounds;
            foreach(var enemy in world.arena.enemies)Assert.That(maintenanceRoom.Contains(enemy.Position),Is.False,"maintenance holds no enemies: "+enemy.name);Assert.That(world.TryOpenMaintenance(),Is.True);Assert.That(world.arena.Controls.Current.SimulationPaused,Is.True);yield return null;yield return null;Assert.That(float.IsNaN(world.sceneCamera.transform.position.x),Is.False);world.CloseMaintenance();Assert.That(world.arena.Controls.Current.SimulationPaused,Is.False);
            world.WarpToRoom("F01");Assert.That(world.arena.enemies.Count,Is.GreaterThan(0));var first=world.arena.enemies[0];world.WarpToRoom("F02");Assert.That(first.aggressive,Is.True,"normal enemies keep fighting across rooms");Assert.That(first.hurtCollider.enabled,Is.True);Assert.That(world.arena.enemies.Contains(first),Is.True);
            world.WarpToRoom("S");var player=world.arena.Player;using(var manual=world.arena.AcquireManualSimulation()){
                for(int i=0;i<12;i++)manual.Step(1f/120);world.CameraStep(0,true);float ground=player.Position.y,cameraY=world.sceneCamera.transform.position.y,maxCameraDrift=0;
                manual.Step(1f/120,new CombatInput(pressed:CombatButtons.Jump));world.CameraStep(1f/120,false);float highest=player.Position.y;
                for(int i=0;i<120;i++){manual.Step(1f/120);world.CameraStep(1f/120,false);highest=Mathf.Max(highest,player.Position.y);maxCameraDrift=Mathf.Max(maxCameraDrift,Mathf.Abs(world.sceneCamera.transform.position.y-cameraY));}
                Assert.That(highest-ground,Is.GreaterThan(2.8f).And.LessThan(3.3f));Assert.That(maxCameraDrift,Is.LessThan(.0001f),"actual Chapter2 camera must hold through ordinary jump");
                manual.Step(1f/120,new CombatInput(pressed:CombatButtons.Jump));world.CameraStep(1f/120,false);
                for(int i=0;i<20;i++){manual.Step(1f/120);world.CameraStep(1f/120,false);}
                manual.Step(1f/120,new CombatInput(pressed:CombatButtons.Jump));world.CameraStep(1f/120,false);
                for(int i=0;i<180;i++){manual.Step(1f/120);world.CameraStep(1f/120,false);maxCameraDrift=Mathf.Max(maxCameraDrift,Mathf.Abs(world.sceneCamera.transform.position.y-cameraY));}
                Assert.That(maxCameraDrift,Is.LessThan(.0001f),"actual Chapter2 double jump and landing must hold");
                world.CameraStep(0,true);float cameraX=world.sceneCamera.transform.position.x;player.SyncFacing(-player.facing);world.CameraStep(1f/60,false);Assert.That(world.sceneCamera.transform.position.x,Is.EqualTo(cameraX).Within(.0001f),"turning in place must not pan the view");
                Debug.Log("CHAPTER2_CAMERA_CHECK singleAndDoubleJumpDriftPx="+maxCameraDrift*32+"; turning holds; native physics used");
            }
            var carry=Chapter2Carry.Capture(world.arena);var restored=carry.CreateLoadout().Read();Assert.That(restored.ActiveSlot,Is.EqualTo(player.Loadout.ActiveSlot));for(int i=0;i<2;i++)Assert.That(restored.Weapons[i].InstanceId,Is.EqualTo(player.Loadout.Weapons[i].InstanceId));
            AssertWallKicks(world);
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                foreach(var id in new[]{"S","M","E","F01"}){world.WarpToRoom(id);Capture(world.sceneCamera,id);}
            AssertDeadPlayerDoesNotReceiveClearHealing(world);
            world.WarpToRoom("B");Assert.That(world.Boss.EnteredRoom,Is.True);
            for(int i=0;i<600&&!world.Boss.Started;i++)yield return null;
            Assert.That(world.Boss.Started,Is.True);Assert.That(world.arena.Player.Position.x,Is.EqualTo(world.Boss.WalkInTargetX).Within(.2f));Assert.That(world.TryUseExit(),Is.False);Assert.That(world.Boss.Model.Phase,Is.EqualTo(Deviant.Chapter2.RoseBlue.Phase.Introduction));yield return new ExitPlayMode();
        }
        // Keep LINQ captures out of the iterator: EnterPlayMode reloads the domain.
        static void AssertWallKicks(Chapter2World world)
        {
            var player=world.arena.Player;
            var pair=WallKickPair(world.Layout,player.size.y+1);
            var traversal=world.GetComponent<Chapter1TraversalPresentation>();Assert.That(traversal,Is.Not.Null);
            using(var manual=world.arena.AcquireManualSimulation())for(int side=-1;side<=1;side+=2)
            {
                player.ResetPlayer();player.SyncFacing(side);float face=side<0?pair.left.x+pair.left.w:pair.right.x;
                player.Position=new Vector2(face-side*(player.size.x/2+player.offset.x+.02f),pair.bottom+2.5f);Physics2D.SyncTransforms();
                Assert.That(player.Blocked(player.Position,side),Is.False,"wall fixture starts outside solid terrain, side "+side);
                for(int i=0;i<8;i++)manual.Step(1f/120,new CombatInput(axis:side,climbAxis:1));
                Assert.That(player.WallClimbing,Is.True,"climb side "+side);
                // The wall itself and the final stand-up draw in the approved climb-v4 art, as in chapter 1.
                world.arena.Presentation.Draw(1f/120);traversal.RefreshPresentation();
                Assert.That(traversal.CurrentMotion,Is.EqualTo("wall_contact"));
                Assert.That(AssetDatabase.GetAssetPath(player.visual.sprite),Does.Contain("climb-v4/ink/"),"climb side "+side);
                float beforeKick=player.Center.x;
                manual.Step(1f/120,new CombatInput(axis:side,pressed:CombatButtons.Jump,climbAxis:1));
                Assert.That(player.WallJumpInFlight,Is.True);Assert.That(player.WallJumpHeading,Is.EqualTo(-side));Assert.That(player.velocity.x*side,Is.LessThan(0));
                Assert.That((player.Center.x-beforeKick)*side,Is.LessThan(0),"kick physically leaves the wall, side "+side);
                Assert.That(player.Blocked(player.Position,player.facing),Is.False,"kick remains outside solid terrain");
            }
            player.ResetPlayer();
            typeof(Deviant.Combat.CombatPlayer).GetProperty("WallLedgeStartedAt").SetValue(player,world.arena.Now);
            world.arena.Presentation.Draw(1f/120);traversal.RefreshPresentation();
            Assert.That(traversal.CurrentMotion,Is.EqualTo("wall_ledge"));
            Assert.That(AssetDatabase.GetAssetPath(player.visual.sprite),Does.Contain("climb-v4/ink/"),"ledge stand-up");
            player.ResetPlayer();traversal.RefreshPresentation();
        }
        static bool WallKickAirClear(C2Layout layout,C2Surface left,C2Surface right,float bottom,float height)
        {
            // Surface rectangles tile a union: an individual rectangle's edge may be buried
            // inside another solid. Reserve air for both the body and its initial climb/kick.
            var air=new Rect(left.x+left.w+.003f,bottom+2.5f,right.x-left.x-left.w-.006f,height);
            return !layout.surfaces.Any(surface=>surface.Bounds.Overlaps(air));
        }
        static (C2Surface left,C2Surface right,float bottom) WallKickPair(C2Layout layout,float height)
        {
            return (from left in layout.surfaces from right in layout.surfaces
                let gap=right.x-left.x-left.w let bottom=Mathf.Max(left.y,right.y) let top=Mathf.Min(left.y+left.h,right.y+right.h)
                where !left.oneWay&&!right.oneWay&&gap>=4.99f&&gap<=6.01f&&top-bottom>8
                    &&WallKickAirClear(layout,left,right,bottom,height)
                select (left,right,bottom)).First();
        }
        [Test] public void CommonGameMenuRetainsTitleVillageAndChapterTwoBuildEntry()
        {
            Chapter2Tools.Create();Assert.That(EditorApplication.ExecuteMenuItem("Tools/deviant/Chapter2"),Is.True);Assert.That(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),Is.EqualTo(Chapter2EntryBridge.ScenePath));Assert.That(EditorApplication.ExecuteMenuItem("Tools/deviant/Game/Open Common Game"),Is.True);
            Assert.That(EditorSceneManager.GetActiveScene().path,Is.EqualTo(Chapter1RunHost.TitleScene));var start=AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);Assert.That(start==""||start==Chapter1RunHost.TitleScene,Is.True);
            var scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray();Assert.That(scenes,Does.Contain(Chapter1RunHost.VillageScene));Assert.That(scenes,Does.Contain(Chapter2EntryBridge.ScenePath));
            Assert.That(ForestCombatV2.Editor.Chapter1PlayBuildSetup.BuildScenes,Does.Contain(Chapter2EntryBridge.ScenePath));
            EditorSceneManager.playModeStartScene=null;
        }
        static void AssertCommonGui(Chapter2World world)
        {
            bool found=false;foreach(var view in Object.FindObjectsByType<InventoryPanelView>(FindObjectsSortMode.None))if(view.GetType().Name=="TemporaryGameGui")found=true;
            Assert.That(found,Is.True,"Use current common game GUI");Assert.That(world.ExternalPresentation,Is.True);Assert.That(world.FullMap.MapTexture,Is.Not.Null);Assert.That(world.FullMap.MapData.rooms.Length,Is.EqualTo(16));
            Assert.That(world.FullMap.PlayerMapPoint.x,Is.GreaterThanOrEqualTo(0));Assert.That(world.FullMap.PlayerMapPoint.y,Is.GreaterThanOrEqualTo(0));
        }
        static void AssertDeadPlayerDoesNotReceiveClearHealing(Chapter2World world)
        {
            Assert.That(world.Economy.RoomClearHealth("elite"),Is.GreaterThan(0));world.arena.Player.health=0;
            world.Rooms.Defeat("E","E:elite");Assert.That(world.Rooms.IsCleared("E"),Is.True);Assert.That(world.arena.Player.Dead,Is.True,"A simultaneous final kill must not revive the player");world.arena.Player.ResetPlayer();
        }
        static void ConfigureCarriedSelections(ForestCombatWorld source)
        {
            int primary=System.Array.FindIndex(source.arena.catalog.forms,f=>f.id=="whip"),secondary=System.Array.FindIndex(source.arena.catalog.forms,f=>f.id=="hammer");
            Assert.That(primary,Is.GreaterThan(0));Assert.That(secondary,Is.GreaterThan(0));source.arena.Player.Reconfigure(0,primary,1);source.arena.Player.Reconfigure(1,secondary,1);
        }
        static void Capture(Camera camera,string id)
        {
            var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(960,540,24);var texture=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();var pixels=texture.GetPixels32();Assert.That(pixels.Select(c=>(int)c.r<<16|(int)c.g<<8|c.b).Distinct().Count(),Is.GreaterThan(20),"Native terrain/actors render");string dir=System.Environment.GetEnvironmentVariable("C2_CAPTURE_DIR");if(!string.IsNullOrEmpty(dir)){System.IO.Directory.CreateDirectory(dir);System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir,"chapter2-"+id+".png"),texture.EncodeToPNG());}}
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
        }
        [UnityTest] public IEnumerator ExistingChapterTwoActionLoadsPlayableScene()
        {
            yield return new EnterPlayMode();yield return null;
            float sourceDeadline=Time.realtimeSinceStartup+60;ForestCombatWorld source=null;InventoryRuntimeHost bag=null;
            do{yield return null;source=Object.FindAnyObjectByType<ForestCombatWorld>();if(source&&source.arena)bag=source.arena.GetComponent<InventoryRuntimeHost>();}while((!source||!source.Ready||!bag||bag.Inventory==null)&&Time.realtimeSinceStartup<sourceDeadline);
            Assert.That(source&&source.Ready&&bag&&bag.Inventory!=null,Is.True,"Existing chapter one ready");
            source.arena.Player.health=57;source.arena.Player.SetWeaponSlotReady(0,true);source.arena.Player.SetWeaponSlotReady(1,true);ConfigureCarriedSelections(source);source.arena.Player.weapons[0].StatSlots[0]=1;
            var item=bag.Grant(bag.Content.items[0].id);Assert.That(item,Is.Not.Null);var old=source.arena.Player.Loadout;var host=Chapter1RunHost.Ensure();for(int i=0;i<2;i++){var id=source.arena.Player.Loadout.Weapons[i].InstanceId;if(host.Flow.Current.Growth.Equipped(id)==null)host.Flow.Current.Growth.EquipStartPalette(id,3,false,new System.Random(42));}host.Flow.Current.MarkBossDefeated();var continuedRun=host.Flow.Current;var economy=continuedRun.Economy;economy.Earn(123);int gold=economy.Gold;
            yield return null;yield return null;Assert.That(Chapter2EntryBridge.TryEnterFromChapterOne(),Is.True);Assert.That(Chapter2EntryBridge.TryEnterFromChapterOne(),Is.False,"transition accepted once");Assert.That(continuedRun.Ended,Is.False);float deadline=Time.realtimeSinceStartup+30;Chapter2World world=null;
            do{yield return null;world=Object.FindAnyObjectByType<Chapter2World>();}while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            Assert.That(world&&world.Ready,Is.True);Assert.That(world.gameObject.scene.path,Is.EqualTo(Chapter2EntryBridge.ScenePath));Assert.That(world.SourceRun,Is.SameAs(continuedRun));Assert.That(continuedRun.Chapter,Is.EqualTo(2));Assert.That(world.Economy,Is.SameAs(economy));Assert.That(world.Economy.Gold,Is.EqualTo(gold));Assert.That(Chapter1RunHost.Instance.ChapterTwoPending,Is.False);
            InventoryRuntimeHost continued=null;do{yield return null;continued=world.arena.GetComponent<InventoryRuntimeHost>();}while((!continued||continued.Inventory==null)&&Time.realtimeSinceStartup<deadline);yield return null;
            for(int i=0;i<2;i++){Assert.That(world.arena.Player.Loadout.Weapons[i].InstanceId,Is.EqualTo(old.Weapons[i].InstanceId));Assert.That(world.arena.Player.Loadout.Weapons[i].Selection.FormIndex,Is.EqualTo(old.Weapons[i].Selection.FormIndex));Assert.That(world.arena.Player.Loadout.Weapons[i].Selection.SpecialIndex,Is.EqualTo(old.Weapons[i].Selection.SpecialIndex));}Assert.That(world.arena.Player.weapons[0].StatSlots[0],Is.EqualTo(1));Assert.That(world.arena.Player.health,Is.EqualTo(57));Assert.That(continued.Inventory.Owns(item),Is.True);Assert.That(world.arena.MvpGrowth.Equipped(old.Weapons[0].InstanceId).Slots.Count,Is.EqualTo(3));yield return new ExitPlayMode();
        }
        // A village plan's permanent max-health tiers raise chapter 2's fresh player too; carried health stays as it was.
        [UnityTest] public IEnumerator PlannedMaxHealthBonusCarriesIntoChapterTwo()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());var profile=Chapter1ProfileHost.Service;
            var warmup=profile.PlanRun(new[]{"sword","blunt"}).RunId;for(int i=0;i<3;i++)profile.AwardPigment(warmup,"boss"+i,"boss",false);Assert.That(profile.SettleRun(warmup,Deviant.Run.RunOutcome.Died,false),Is.True);
            Assert.That(profile.TryPurchase("max_health_bonus_1",out _),Is.True);Assert.That(profile.TryPurchase("max_health_bonus_2",out _),Is.True);
            var host=Chapter1RunHost.Ensure();host.StartNewRun(profile.PlanRun(new[]{"sword","blunt"}));var run=host.Flow.Current;
            float deadline=Time.realtimeSinceStartup+60;ForestCombatWorld source=null;
            bool Bound(ForestCombatWorld w){var b=w?w.GetComponent<Chapter1RunBinding>():null;return w&&w.Ready&&b&&b.Run==run&&run.Growth!=null&&run.Growth.Palettes.Count==2&&w.arena.Player.maxHealth>100;}
            do{yield return null;source=Object.FindAnyObjectByType<ForestCombatWorld>();}while(!Bound(source)&&Time.realtimeSinceStartup<deadline);
            Assert.That(source.arena.Player.maxHealth,Is.EqualTo(110),"chapter 1 starts with tiers 1-2 (+10)");
            source.arena.Player.health=57;run.MarkBossDefeated();
            yield return null;yield return null;Assert.That(Chapter2EntryBridge.TryEnterFromChapterOne(),Is.True);
            Chapter2World world=null;do{yield return null;world=Object.FindAnyObjectByType<Chapter2World>();}while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            Assert.That(world&&world.Ready,Is.True);Assert.That(world.SourceRun,Is.SameAs(run));
            Assert.That(world.arena.Player.maxHealth,Is.EqualTo(110),"the bonus carries into chapter 2");Assert.That(world.arena.Player.health,Is.EqualTo(57),"carried health is not refilled");
            // Keep the memory profile alive while Chapter2World.OnDestroy flushes it.
            // Clearing it first recreates a Profile host during scene shutdown.
            yield return new ExitPlayMode();Chapter1ProfileHost.UseStore(null);
        }
    }
}
