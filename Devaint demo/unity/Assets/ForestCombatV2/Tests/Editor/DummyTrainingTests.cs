using System.Collections;
using System.Linq;
using Deviant.Combat;
using ForestCombatV2.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class DummyTrainingTests
    {
        [Test] public void DamageWindowEndsTwoSecondsAfterLastHitAndNextHitStartsFresh()
        {
            var meter=new TrainingDamageMeter();meter.Add(100,10);meter.Add(200,11);
            Assert.That(meter.Total,Is.EqualTo(300));Assert.That(meter.Finished(12.99f),Is.False);
            Assert.That(meter.Dps(13),Is.EqualTo(100).Within(.001));
            Assert.That(meter.Dps(100),Is.EqualTo(100).Within(.001));
            meter.Add(60,100);Assert.That(meter.Total,Is.EqualTo(60));Assert.That(meter.Dps(102),Is.EqualTo(30));
            meter.Clear();Assert.That(meter.HasHits,Is.False);Assert.That(meter.Total,Is.Zero);
        }
        [Test] public void ZeroAndInvalidDamageCannotStartOrExtendTheWindow()
        {
            var meter=new TrainingDamageMeter();meter.Add(0,1);meter.Add(float.NaN,1);meter.Add(10,float.NaN);
            Assert.That(meter.HasHits,Is.False);meter.Add(20,2);meter.Add(0,3);
            Assert.That(meter.Finished(4),Is.True);Assert.That(meter.Dps(4),Is.EqualTo(10));
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
            DummyTrainingTools.SelectNormalMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        [Test] public void TrainingLaunchUsesTheMainSceneWithoutWritingOverridesToIt()
        {
            var bytes=System.IO.File.ReadAllBytes(DummyTrainingTools.ScenePath);
            try
            {
                DummyTrainingTools.Prepare();
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,Is.EqualTo(Chapter1MapSelector.ProceduralScene));
                Assert.That(Object.FindAnyObjectByType<ForestCombatWorld>().dummyTrainingOnly,Is.False,"training overrides belong to Play, not the saved main scene");
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,Is.False);
                CollectionAssert.AreEqual(bytes,System.IO.File.ReadAllBytes(DummyTrainingTools.ScenePath));
            }
            finally{DummyTrainingTools.SelectNormalMode();}
        }
        [UnityTest] public IEnumerator TrainingHasSixRealTargetsNearestResetAndIndependentEquipment()
        {
            DummyTrainingTools.Prepare();
            var authoredWorld=Object.FindAnyObjectByType<ForestCombatWorld>();
            authoredWorld.cameraVerticalOffsetPixels+=17;
            // Scene data survives EnterPlayMode's domain reload; iterator locals may not.
            var probe=new GameObject("Main-scene-training-sync-probe");
            probe.transform.position=new Vector3(authoredWorld.cameraVerticalOffsetPixels,0,0);
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=true;
            yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            Assert.That(world.gameObject.scene.path,Is.EqualTo(Chapter1MapSelector.ProceduralScene));
            var inheritedProbe=GameObject.Find("Main-scene-training-sync-probe");
            Assert.That(inheritedProbe,Is.Not.Null,"new main scene hierarchy is inherited");
            Assert.That(world.cameraVerticalOffsetPixels,Is.EqualTo(inheritedProbe.transform.position.x),"main scene configuration is inherited without a stale copy");
            var room=world.GetComponent<DummyTrainingRoom>();var arena=world.arena;
            Assert.That(room,Is.Not.Null);Assert.That(world.GetComponent<ClockExecutionerEncounter>(),Is.Null);
            Assert.That(world.GetComponent<Chapter1RunBinding>(),Is.Null,"training cannot grant run kills or boss rewards");
            Assert.That(room.Targets.Count,Is.EqualTo(6));Assert.That(room.Platforms.Count,Is.EqualTo(2));
            foreach(var p in room.Platforms)
            {Assert.That(p.bounds.max.y-room.Targets[2].spawn.y,Is.EqualTo(4).Within(.001));Assert.That(p.size.x,Is.EqualTo(5));}
            Assert.That(room.Targets[3].spawn.x-room.Targets[2].spawn.x,Is.EqualTo(12).Within(.001));
            Assert.That(room.Targets[4].spawn.x-room.Targets[3].spawn.x,Is.EqualTo(1).Within(.001));
            Assert.That(room.Targets[5].spawn.x-room.Targets[4].spawn.x,Is.EqualTo(1).Within(.001));
            Assert.That(world.diagnostics.showDebugUI,Is.True,"native hitbox renderer must remain enabled");
            bool boxes=arena.showBoxes;room.ToggleHitboxes();Assert.That(arena.showBoxes,Is.EqualTo(!boxes));
            room.ToggleHitboxes();Assert.That(arena.showBoxes,Is.EqualTo(boxes));
            Assert.That(world.useTeamSketchTerrain,Is.True,"training must retain the current village environment");
            Assert.That(world.FullMap,Is.Null,"training labels have no minimap overlay");
            Assert.That(arena.Player.GetComponent<CombatKeyboardInput>().ResetKeyEnabled,Is.False);
            room.SetOpen(true);Assert.That(arena.Controls.Current.SimulationPaused,Is.True);
            int form=System.Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword");
            Assert.That(room.ApplyEquipment(0,form,0,new[]{1,1,0,0,0},out var error),Is.True,error);
            var commonEquipment=new WeaponState();commonEquipment.StatSlots.CopyFrom(new[]{1,1,0,0,0});
            Assert.That(arena.Player.weapons[0].BasicBonus,Is.EqualTo(commonEquipment.BasicBonus).Within(.001),"training inherits current main equipment stacking");
            Assert.That(arena.Player.weapons[1].Count(1),Is.Zero);
            Assert.That(room.ApplyEquipment(0,999,0,new[]{2,2,2,2,2},out error),Is.False);
            Assert.That(arena.Player.weapons[0].Count(1),Is.EqualTo(2),"failed selection rolls stats back");
            room.SetOpen(false);Assert.That(arena.Controls.Current.SimulationPaused,Is.False);
            using(var manual=arena.AcquireManualSimulation())
            {
                room.Targets[2].Receive(100,arena.Player,1,true,null);
                room.Targets[3].Receive(200,arena.Player,1,false,null);
                manual.Step(1f/120);
                Assert.That(room.Targets[2].Meter.Total,Is.EqualTo(100));Assert.That(room.Targets[3].Meter.Total,Is.EqualTo(200));
                for(int i=0;i<250;i++)manual.Step(1f/120);
                Assert.That(room.Targets[2].Meter.Dps(arena.Now),Is.EqualTo(50).Within(.01));
                room.Targets[2].Restore();
                arena.Player.Position=room.Targets[2].Position+Vector2.left*1.3f;
                arena.Player.velocity=Vector2.zero;arena.Player.grounded=true;arena.Player.SyncFacing(1);Physics2D.SyncTransforms();
                manual.Step(1f/120,new Deviant.Combat.Contracts.CombatInput(0,Deviant.Combat.Contracts.CombatButtons.Basic));
                for(int i=0;i<100;i++)manual.Step(1f/120);
                Assert.That(room.Targets[2].Meter.Total,Is.GreaterThan(0),"real common sword attack must hit and be measured");
                Assert.That(room.Targets[2].Meter.Total,Is.EqualTo(1000-room.Targets[2].health).Within(.001));
            }
            arena.Player.Position=room.Targets[2].Position+Vector2.right*.7f;
            float before=arena.Now;
            var settings=InputSystem.settings;var background=settings.backgroundBehavior;var editorInput=settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard=null;
            try
            {
                settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard=InputSystem.AddDevice<Keyboard>();
                var adapter=arena.Player.GetComponent<CombatKeyboardInput>();
                Assert.That(adapter.Bindings.Weapon2Special,Is.EqualTo(Key.R));
                Assert.That(adapter.Bindings.DevelopmentReset,Is.EqualTo(Key.F5));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F5));InputSystem.Update();keyboard.MakeCurrent();
                Assert.That(keyboard.f5Key.wasPressedThisFrame,Is.True,"batch test must deliver the synthetic key to Play mode");
                adapter.SendMessage("Update");room.SendMessage("Update");
            }
            finally{if(keyboard!=null)InputSystem.RemoveDevice(keyboard);settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editorInput;}
            Assert.That(room.Targets[2].health,Is.EqualTo(1000));Assert.That(room.Targets[2].Meter.HasHits,Is.False);
            Assert.That(room.Targets[3].health,Is.EqualTo(800));Assert.That(room.Targets[3].Meter.Total,Is.EqualTo(200));
            Assert.That(arena.Now,Is.EqualTo(before),"local restore must not reset simulation or other targets");
            var data=JsonUtility.FromJson<DummyTrainingRoom.PartList>(Resources.Load<TextAsset>("TrainingInkParts").text);
            Assert.That(data.parts.Length,Is.EqualTo(60));Assert.That(data.parts.Select(p=>p.id).Distinct().Count(),Is.EqualTo(60));
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var output=System.IO.Path.GetFullPath("../outputs/dummy-training-preview");System.IO.Directory.CreateDirectory(output);
                for(int frame=0;frame<3;frame++)yield return null;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output,"targets.png"));
                for(int frame=0;frame<5;frame++)yield return null;
                room.SetOpen(true);
                for(int frame=0;frame<3;frame++)yield return null;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output,"equipment.png"));
                for(int frame=0;frame<5;frame++)yield return null;
                room.SetOpen(false);
            }
        }
    }
}
