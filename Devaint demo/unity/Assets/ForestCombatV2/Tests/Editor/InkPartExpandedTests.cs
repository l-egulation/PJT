using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using ForestCombatV2.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class InkPartExpandedTests
    {
        [Serializable] sealed class Entry{public string id;}
        [Serializable] sealed class Profile{public string form;public float damage,reach;}
        [Serializable] sealed class Entries{public Entry[] parts;public Profile[] chargeProfiles;}
        [Test] public void RuntimeContainsEveryCandidateExceptDeferredArmorAndOwnsTrialTuning()
        {
            var text=Resources.Load<TextAsset>("RuntimeInkParts").text;
            var data=JsonUtility.FromJson<Entries>(text);Assert.That(data.parts.Length,Is.EqualTo(59));
            Assert.That(data.parts.Select(p=>p.id).Distinct().Count(),Is.EqualTo(59));
            Assert.That(InkPartCatalog.Implemented("g_armor"),Is.False);
            foreach(var p in data.parts)Assert.That(InkPartCatalog.Implemented(p.id),Is.True,p.id);
            StringAssert.Contains("\"waveSpeed\"",text);StringAssert.Contains("\"chargeProfiles\"",text);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {if(Application.isPlaying)yield return new ExitPlayMode();DummyTrainingTools.SelectNormalMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        static void Prepare(){DummyTrainingTools.Prepare();Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;}
        static void Tick(CombatArena.ManualSimulation sim,float seconds,bool held=false)
        {for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)sim.Step(.02f,new CombatInput(0,CombatButtons.None,held));}
        static void Equip(DummyTrainingRoom room,CombatArena arena,string part,int slot=0)
        {
            room.SetOpen(true);
            for(int i=0;i<6;i++)Assert.That(room.ApplyInkPart(slot,i,i==0?part:null,out var error),Is.True,error);
            room.SetOpen(false);
        }
        static void Reset(CombatArena.ManualSimulation sim,DummyTrainingRoom room,CombatArena arena,string part)
        {
            sim.Reset();room.SetOpen(true);
            for(int slot=0;slot<2;slot++)
            {Assert.That(arena.Player.TrySetChargeStamp(slot,false,out _),Is.True);for(int i=0;i<6;i++)Assert.That(room.ApplyInkPart(slot,i,null,out _),Is.True);}
            room.SetOpen(false);Equip(room,arena,part);arena.Player.health=100;
        }
        static CombatHitOrigin Origin(CombatArena arena,int slot=0,bool special=false,bool finisher=false,bool charge=false)
        {
            var w=arena.Player.weapons[slot];arena.TryBindActor(arena.Player,out var source);
            string form=arena.catalog.forms[w.FormIndex].id;
            string id=charge?"ink_charge_"+form:finisher?form+"_basic_"+(arena.catalog.forms[w.FormIndex].basics.Length-1):"ink_fixture";
            return new CombatHitOrigin(source,default,Guid.NewGuid(),0,id,Guid.Empty,CombatHitKind.Melee,
                stamp:new CombatAttackStamp(w.InstanceId,form,id,"trial",!special,-1,w.InkPalette.Read()),sourceFacing:1);
        }
        static void Hit(CombatArena arena,CombatActor enemy,float damage=10,int slot=0,bool special=false,bool finisher=false,bool charge=false)
        {Assert.That(enemy.Receive(damage,arena.Player,1,false,null,origin:Origin(arena,slot,special,finisher,charge)),Is.True);}
        static CombatCast Start(CombatArena arena,int slot=0,bool special=false,bool finisher=false)
        {
            var player=arena.Player;player.CancelAction();player.selected=slot;
            string id=finisher?arena.catalog.forms[player.Weapon.FormIndex].id+"_basic_"+(arena.catalog.forms[player.Weapon.FormIndex].basics.Length-1):"ink_trial_start";
            var data=new SkillData{id=id,name="test",activation=.1f,end=.5f,inputSpeed=0,vertical="normal",facing="windup_start",invulStart=Array.Empty<float>(),invulEnd=Array.Empty<float>(),hits=new[]{new HitData{time=.1f,active=.05f,damage=10,anchor="actor",targets="all",box=new BoxData{width=3,height=2,x=1.5f,y=1}}}};
            return player.currentCast=CombatCast.Create(arena,player,data,player.Weapon,!special);
        }
        static void Marks(CombatArena arena,DummyTrainingRoom room,CombatArena.ManualSimulation sim,CombatActor target,int count=3)
        {Equip(room,arena,"p_mark");for(int i=0;i<count;i++){Hit(arena,target,1);Tick(sim,.62f);}Assert.That(arena.ReadInkMarks(target),Is.EqualTo(count));}
        [UnityTest] public IEnumerator HitDefenseColdAndKillFamiliesUseActualAcceptedDamage()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();var target=room.Targets[2];
            using(var sim=arena.AcquireManualSimulation())
            {
                foreach(string id in new[]{"r_wave","r_finisher","y_haste","y_stride","y_cycle","y_kill","g_shield","g_counter","g_guard","g_overheal","g_thorns","g_burst","g_full","b_slow","b_zone","b_root","b_frost","b_shatter","b_wave","b_death","b_prison"})
                {
                    Reset(sim,room,arena,id);float nativeReady=0;
                    if(id=="g_counter"){arena.Player.health=50;arena.Player.Receive(10,target,1,false,null);}
                    if(id=="g_thorns"){arena.Player.Receive(10,target,1,false,null);sim.Step(.02f);Assert.That(target.health,Is.EqualTo(991),id);continue;}
                    if(id=="g_burst"){arena.Player.shield=15;arena.Player.guardUntil=arena.Now+10;}
                    if(id=="b_shatter"||id=="b_death")target.rootUntil=arena.Now+5;
                    if(id=="y_kill")target.health=5;
                    if(id=="b_death")
                    {target.Position=room.Targets[3].Position;target.health=5;}
                    if(id=="y_cycle"){arena.Player.weapons[1].Cooldown.Spend(arena.Now);nativeReady=arena.Player.weapons[1].Cooldown.NextReady;}
                    int hits=id=="g_shield"||id=="b_root"||id=="b_frost"?4:id=="y_haste"?3:1;
                    for(int i=0;i<hits;i++){Hit(arena,target,10,special:id=="g_burst"||id=="b_zone"||id=="b_prison",finisher:id=="r_finisher"||id=="y_cycle"||id=="g_guard"||id=="b_wave");sim.Step(.02f);if(id=="b_frost"&&i<3)Tick(sim,.6f);}
                    Tick(sim,.1f);
                    switch(id)
                    {
                        case "r_wave":Assert.That(target.health,Is.LessThanOrEqualTo(985),id);break;
                        case "r_finisher":Assert.That(target.health,Is.EqualTo(982),id);break;
                        case "y_haste":var haste=Start(arena,finisher:true);Assert.That(haste.EndTime-haste.StartedAt,Is.LessThan(.5f),id);break;
                        case "y_stride":case "y_kill":Assert.That(arena.InkMovementScale(arena.Player),Is.GreaterThan(1),id);break;
                        case "y_cycle":Assert.That(arena.Player.weapons[1].Cooldown.NextReady,Is.EqualTo(nativeReady-.5f).Within(.001),id);break;
                        case "g_shield":Assert.That(arena.ReadInkShield(),Is.EqualTo(8),id);arena.Player.selected=1;Assert.That(arena.ReadInkShield(),Is.EqualTo(8),id);break;
                        case "g_counter":Assert.That(arena.Player.health,Is.EqualTo(42.5f).Within(.001),id);break;
                        case "g_guard":arena.Player.Receive(4,target,1,false,null);Assert.That(arena.Player.health,Is.EqualTo(97),id);arena.Player.Receive(4,target,1,false,null);Assert.That(arena.Player.health,Is.EqualTo(93),id);break;
                        case "g_overheal":Assert.That(arena.ReadInkShield(),Is.EqualTo(2),id);arena.TryGetCursor(out var cursor);arena.Player.Receive(5,target,1,false,null);Assert.That(arena.Player.health,Is.EqualTo(97),id);var buffer=new CombatAcceptedHit[16];arena.TryRead(cursor,buffer,out var page);Assert.That(buffer[0].Absorbed,Is.EqualTo(2),id);break;
                        case "g_burst":Assert.That(target.health,Is.EqualTo(972),id);Assert.That(arena.Player.shield,Is.Zero,id);break;
                        case "g_full":Assert.That(target.health,Is.EqualTo(984),id);break;
                        case "b_slow":case "b_zone":case "b_shatter":case "b_wave":Assert.That(arena.InkMovementScale(target),Is.LessThan(1),id);break;
                        case "b_root":Assert.That(target.rootUntil,Is.GreaterThan(arena.Now),id);break;
                        case "b_frost":case "b_prison":Assert.That(target.stunUntil,Is.GreaterThan(arena.Now),id);break;
                        case "b_death":Assert.That(room.Targets[3].health,Is.LessThan(1000),id);break;
                    }
                }
                Reset(sim,room,arena,"y_stride");Hit(arena,target);sim.Step(.02f);Start(arena,1);Assert.That(arena.InkMovementScale(arena.Player),Is.EqualTo(1));
                Start(arena,0);Assert.That(arena.InkMovementScale(arena.Player),Is.GreaterThan(1));Tick(sim,3.1f);Assert.That(arena.InkMovementScale(arena.Player),Is.EqualTo(1));
            }
        }
        [UnityTest] public IEnumerator PurpleMarksArbitrateSnapshotTimersAndSpreadWithoutRecursion()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();var target=room.Targets[3];
            using(var sim=arena.AcquireManualSimulation())
            {
                foreach(string id in new[]{"p_mark","p_detonate","p_spread","p_heavy","p_echo","p_finish","p_link","p_drain","p_charge","p_timer"})
                {
                    Reset(sim,room,arena,id);
                    if(id=="p_echo"){Hit(arena,target,1);Tick(sim,.7f);Assert.That(arena.ReadInkMarks(target),Is.EqualTo(2),id);continue;}
                    if(id=="p_link"){Hit(arena,target,1);sim.Step(.02f);Assert.That(arena.ReadInkMarks(target),Is.EqualTo(1),id);Assert.That(arena.ReadInkMarks(room.Targets[4]),Is.EqualTo(1),id);continue;}
                    Marks(arena,room,sim,target);Equip(room,arena,id);float hp=target.health;
                    if(id=="p_spread")target.health=1;
                    Hit(arena,target,1,special:id=="p_detonate"||id=="p_heavy"||id=="p_charge",finisher:id=="p_finish",charge:id=="p_heavy"||id=="p_charge");sim.Step(.02f);
                    if(id=="p_spread"){Assert.That(arena.ReadInkMarks(room.Targets[4]),Is.EqualTo(1),id);Assert.That(arena.ReadInkMarks(room.Targets[5]),Is.EqualTo(1),id);continue;}
                    if(id=="p_timer")
                    {
                        Assert.That(arena.ReadInkMarks(target),Is.EqualTo(4),id);Equip(room,arena,"p_echo");Hit(arena,target,1);Tick(sim,.7f);
                        Assert.That(arena.ReadInkMarks(target),Is.EqualTo(6));Tick(sim,.9f);Assert.That(arena.ReadInkMarks(target),Is.EqualTo(2),"only four reserved marks consumed");continue;
                    }
                    Assert.That(target.health,Is.LessThan(hp-1),id);
                    if(id=="p_drain")Assert.That(arena.ReadInkMarks(target),Is.EqualTo(1),id);
                }
                Reset(sim,room,arena,"p_mark");Marks(arena,room,sim,target);
                room.SetOpen(true);Assert.That(room.ApplyInkPart(0,1,"p_detonate",out _),Is.True);Assert.That(room.ApplyInkPart(0,2,"p_drain",out _),Is.True);room.SetOpen(false);
                float before=target.health;arena.Player.health=50;Hit(arena,target,1,special:true);sim.Step(.02f);
                Assert.That(before-target.health,Is.EqualTo(25),"highest 8-per-mark explosion wins once, no double spend");Assert.That(arena.Player.health,Is.EqualTo(50),"losing drain cannot heal");
                foreach(string id in new[]{"y_cross","b_break","p_cross"})
                {
                    Reset(sim,room,arena,id);Hit(arena,target,1);sim.Step(.02f);Hit(arena,target,1,slot:1);sim.Step(.02f);
                    if(id=="y_cross"){var cast=Start(arena,1);Assert.That(cast.EndTime-cast.StartedAt,Is.LessThan(.5f),id);}
                    if(id=="b_break"){Assert.That(target.health,Is.EqualTo(992),id);Assert.That(target.rootUntil,Is.GreaterThan(arena.Now),id);}
                    if(id=="p_cross")Assert.That(arena.ReadInkMarks(target),Is.EqualTo(1),id);
                }
            }
        }
        [UnityTest] public IEnumerator ActualWalkingSeedsDodgeAndStartModifiers()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();var target=room.Targets[2];
            using(var sim=arena.AcquireManualSimulation())
            {
                Reset(sim,room,arena,"r_rage");arena.Player.health=30;arena.Player.Position=target.Position+Vector2.left;arena.Player.SyncFacing(1);Physics2D.SyncTransforms();Start(arena);Tick(sim,.2f);Assert.That(target.health,Is.EqualTo(987),"low health adds 30 percent to original damage");
                Reset(sim,room,arena,"y_special");Start(arena,special:true);Equip(room,arena,"y_special",1);var quick=Start(arena,1);Assert.That(quick.EndTime-quick.StartedAt,Is.LessThan(.5f));
                Reset(sim,room,arena,"g_seed");arena.Player.health=50;target.health=1;Hit(arena,target,1);sim.Step(.02f);Tick(sim,.8f);Assert.That(arena.Player.health,Is.EqualTo(50),"seed is not auto collected");arena.Player.Position=target.Position;Tick(sim,.1f);Assert.That(arena.Player.health,Is.EqualTo(54));
                foreach(string id in new[]{"y_wave","y_distance","b_distance","p_distance"})
                {
                    Reset(sim,room,arena,id);arena.Player.Position=new Vector2(0,1000);arena.Player.velocity=Vector2.zero;
                    float start=arena.Player.Position.x;
                    for(int i=0;i<200 && arena.Player.Position.x-start<6.5f;i++)
                    {target.Position=arena.Player.Position+Vector2.right;sim.Step(.02f,new CombatInput(1,CombatButtons.None));}
                    Assert.That(arena.Player.Position.x-start,Is.GreaterThan(6),id);
                    if(id=="y_distance"){var cast=Start(arena);Assert.That(cast.EndTime-cast.StartedAt,Is.LessThan(.5f),id);}
                    if(id=="b_distance")Assert.That(target.health,Is.LessThan(1000),id);
                    if(id=="p_distance")Assert.That(arena.ReadInkMarks(target),Is.GreaterThan(0),id);
                    if(id=="y_wave"){target.Position=arena.Player.Position+Vector2.right;Start(arena);Tick(sim,.2f);Assert.That(target.health,Is.LessThanOrEqualTo(992),id);}
                }
                Reset(sim,room,arena,"y_dodge");arena.Player.weapons[1].Cooldown.Spend(arena.Now);float ready=arena.Player.weapons[1].Cooldown.NextReady;
                sim.Step(.02f,new CombatInput(1,CombatButtons.Dash));arena.TryBindActor(target,out var attacker);
                var attack=new CombatHitOrigin(attacker,default,Guid.NewGuid(),0,"enemy",Guid.Empty,CombatHitKind.Melee);
                Assert.That(arena.Player.Receive(10,target,1,false,null,origin:attack),Is.False);Assert.That(arena.Player.Receive(10,target,1,false,null,origin:attack),Is.False);
                Assert.That(arena.Player.weapons[1].Cooldown.NextReady,Is.EqualTo(ready-.7f).Within(.001));
            }
        }
        [UnityTest] public IEnumerator FullChargeWaitsForReleaseCancelsOnDashAndRunsChargePartFamilies()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();var target=room.Targets[2];
            using(var sim=arena.AcquireManualSimulation())
            {
                foreach(string id in new[]{"r_blood","r_delay","r_range","y_refund","y_charge_move","y_recovery","g_charge_shield","g_heal","b_charge","b_charge_shatter","p_heavy","p_charge"})
                {
                    Reset(sim,room,arena,id);
                    if(id=="p_heavy"){Marks(arena,room,sim,target);Equip(room,arena,id);}
                    arena.Player.Position=target.Position+Vector2.left*(id=="r_range"?3.5f:1);arena.Player.SyncFacing(1);arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();
                    if(id=="g_heal")arena.Player.health=50;
                    if(id=="y_refund")target.health=1;
                    if(id=="b_charge_shatter")target.rootUntil=arena.Now+5;
                    room.SetOpen(true);Assert.That(arena.Player.TrySetChargeStamp(0,true,out var error),Is.True,error);room.SetOpen(false);
                    float before=target.health;int available=arena.Player.weapons[0].Cooldown.Available;
                    sim.Step(.02f,new CombatInput(0,CombatButtons.Weapon1Special,true));Assert.That(arena.Player.InkCharging,Is.True,id);
                    if(id=="r_blood")Assert.That(arena.Player.health,Is.EqualTo(94),id);
                    if(id=="y_charge_move")
                    {float fraction=arena.Player.InkChargeFraction;for(int i=0;i<15;i++)sim.Step(.02f,new CombatInput(1,CombatButtons.None,true));Assert.That(arena.Player.InkChargeFraction-fraction,Is.GreaterThan(.25f),id);}
                    Tick(sim,1.35f,true);Assert.That(arena.Player.InkChargeFraction,Is.EqualTo(1),id);Assert.That(target.health,Is.EqualTo(before),"holding full charge does not attack: "+id);
                    if(id=="g_charge_shield")Assert.That(arena.ReadInkShield(),Is.GreaterThan(0),id);
                    if(id=="b_charge")Assert.That(arena.InkMovementScale(target),Is.LessThan(1),id);
                    sim.Step(.02f);var release=arena.Player.currentCast;
                    if(id=="y_recovery")Assert.That(release.EndTime-release.StartedAt,Is.LessThan(.53f),id);
                    Tick(sim,.8f);
                    if(id!="y_charge_move")Assert.That(target.health,Is.LessThan(before),id);
                    if(id=="g_heal")Assert.That(arena.Player.health,Is.EqualTo(55),id);
                    if(id=="y_refund")Assert.That(arena.ReadInkChargeStore(0),Is.EqualTo(.15f).Within(.001),id);
                    if(id=="g_charge_shield")Assert.That(arena.ReadInkShield(),Is.Zero,id);
                    if(id=="p_heavy")Assert.That(arena.ReadInkMarks(target),Is.Zero,id);
                    if(id=="p_charge")Assert.That(arena.ReadInkMarks(target),Is.EqualTo(2),id);
                    Assert.That(arena.Player.weapons[0].Cooldown.Available,Is.EqualTo(available),"new stamp has no special cooldown: "+id);
                }
                Reset(sim,room,arena,"g_charge_shield");room.SetOpen(true);arena.Player.TrySetChargeStamp(0,true,out _);room.SetOpen(false);
                sim.Step(.02f,new CombatInput(0,CombatButtons.Weapon1Special,true));Tick(sim,.4f,true);Assert.That(arena.ReadInkShield(),Is.GreaterThan(0));
                sim.Step(.02f,new CombatInput(1,CombatButtons.Dash,true));Assert.That(arena.Player.InkCharging,Is.False);Assert.That(arena.Player.InkChargeFraction,Is.Zero);Assert.That(arena.ReadInkShield(),Is.Zero);
                Tick(sim,.6f);sim.Step(.02f,new CombatInput(0,CombatButtons.Weapon1Special,true));Tick(sim,.1f,true);sim.Step(.02f);Assert.That(arena.Player.InkCharging,Is.False,"early release cancels");
            }
        }
        [UnityTest] public IEnumerator EveryChargeProfileFiresRealDamageWithoutSpendingLegacyCooldown()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();var target=room.Targets[2];
            var profiles=JsonUtility.FromJson<Entries>(Resources.Load<TextAsset>("RuntimeInkParts").text).chargeProfiles;
            using(var sim=arena.AcquireManualSimulation())
            {
                foreach(var profile in profiles)
                {
                    Reset(sim,room,arena,null);room.SetOpen(true);
                    int form=Array.FindIndex(arena.catalog.forms,f=>f.id==profile.form);Assert.That(form,Is.GreaterThanOrEqualTo(0),profile.form);
                    Assert.That(room.ApplyEquipment(0,form,0,new int[5],out var error),Is.True,error);
                    Assert.That(arena.Player.TrySetChargeStamp(0,true,out error),Is.True,error);room.SetOpen(false);
                    Assert.That(arena.Player.weapons[0].Snapshot.ExperimentalChargeStamp,Is.True);
                    Assert.That(arena.Player.weapons[1].Snapshot.ExperimentalChargeStamp,Is.False);
                    arena.Player.Position=target.Position+Vector2.left;arena.Player.SyncFacing(1);Physics2D.SyncTransforms();
                    int available=arena.Player.weapons[0].Cooldown.Available;
                    sim.Step(.02f,new CombatInput(0,CombatButtons.Weapon1Special,true));Tick(sim,1.4f,true);
                    Assert.That(target.health,Is.EqualTo(1000),profile.form);sim.Step(.02f);Tick(sim,.3f);
                    Assert.That(target.health,Is.EqualTo(1000-profile.damage).Within(.001),profile.form);
                    Assert.That(arena.Player.weapons[0].Cooldown.Available,Is.EqualTo(available),profile.form);
                }
            }
        }
        [UnityTest] public IEnumerator ReducedWindupPreservesTheAuthoredBasicStepDistance()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();
            var floor=new GameObject("Ink accelerated basic fixture floor");floor.transform.position=new Vector2(-30,999.5f);
            floor.AddComponent<ArenaSurface>();floor.AddComponent<BoxCollider2D>().size=new Vector2(30,1);
            float baselineDistance=0,baselineDuration=0;
            using(var sim=arena.AcquireManualSimulation())
            {
                for(int trial=0;trial<2;trial++)
                {
                    Reset(sim,room,arena,null);room.SetOpen(true);
                    int form=Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword");
                    Assert.That(room.ApplyEquipment(1,form,0,new int[5],out var error),Is.True,error);
                    Assert.That(room.ApplyInkPart(1,0,trial==1?"y_special":null,out error),Is.True,error);room.SetOpen(false);
                    // Another weapon's actual special start arms 엇박자 준비 on weapon B.
                    Start(arena,0,special:true);arena.Player.CancelAction();arena.Player.selected=1;
                    arena.Player.Position=new Vector2(-30,1000);arena.Player.velocity=Vector2.zero;arena.Player.grounded=true;arena.Player.SyncFacing(1);Physics2D.SyncTransforms();
                    float start=arena.Player.Position.x;
                    sim.Step(.02f,new CombatInput(0,CombatButtons.Weapon2Basic));
                    var cast=arena.Player.currentCast;Assert.That(cast,Is.Not.Null);Assert.That(cast.Definition.Id,Is.EqualTo("base_sword_basic_0"));
                    float duration=cast.EndTime-cast.StartedAt;
                    for(int tick=0;tick<100 && arena.Player.currentCast;tick++)sim.Step(.02f);
                    Assert.That(arena.Player.currentCast,Is.Null,"basic must complete");
                    float distance=arena.Player.Position.x-start;
                    Assert.That(distance,Is.EqualTo(.5f).Within(.01f),"authored basic step remains 0.5 units");
                    if(trial==0){baselineDistance=distance;baselineDuration=duration;}
                    else{Assert.That(duration,Is.LessThan(baselineDuration));Assert.That(distance,Is.EqualTo(baselineDistance).Within(.001f));}
                }
            }
            Object.Destroy(floor);
        }
        [UnityTest] public IEnumerator BurnTicksPreserveDamageAttributionWithoutRepeatingNativePushOrStagger()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();
            using(var sim=arena.AcquireManualSimulation())
            {
                Reset(sim,room,arena,"r_burn");var enemy=arena.SpawnEnemy(0,new Vector2(30,1000));enemy.aggressive=false;enemy.maxHealth=enemy.health=1000;
                arena.TryBindActor(arena.Player,out var source);var w=arena.Player.weapons[0];
                var origin=new CombatHitOrigin(source,default,Guid.NewGuid(),0,"burn-start",Guid.Empty,CombatHitKind.Melee,
                    stamp:new CombatAttackStamp(w.InstanceId,"base_sword","base_sword_basic_0","test",true,-1,w.InkPalette.Read()),sourceFacing:1);
                Assert.That(enemy.Receive(1,arena.Player,1,true,null,origin:origin),Is.True);
                Assert.That(enemy.knockUntil,Is.GreaterThan(arena.Now),"native basic still gets its authored push");
                float nativeKnock=enemy.knockUntil,nativeStagger=enemy.staggerUntil;
                var pending=CombatCast.Create(arena,enemy,new SkillData{id="enemy-burn-regression",name="test",activation=10,end=11,
                    inputSpeed=0,vertical="normal",facing="windup_start",hits=Array.Empty<HitData>(),invulStart=Array.Empty<float>(),invulEnd=Array.Empty<float>()},w,false);
                enemy.currentCast=pending;arena.TryGetCursor(out var cursor);
                Tick(sim,1.05f);
                Assert.That(enemy.health,Is.EqualTo(996),"one original damage and one actual burn tick");
                Assert.That(enemy.knockUntil,Is.EqualTo(nativeKnock),"damage-only tick cannot add native action push");
                Assert.That(enemy.staggerUntil,Is.EqualTo(nativeStagger),"damage-only tick cannot refresh basic stagger");
                Assert.That(enemy.currentCast,Is.SameAs(pending),"burn cannot cancel the newly prepared enemy attack");
                var buffer=new CombatAcceptedHit[16];Assert.That(arena.TryRead(cursor,buffer,out var page),Is.True);
                var tick=buffer.Take(page.Count).Single(h=>h.Origin.Kind==CombatHitKind.InkPart);
                Assert.That(tick.Origin.ActionId,Is.EqualTo("base_sword_basic_0"));Assert.That(tick.Origin.ExecutionId,Is.EqualTo(origin.ExecutionId));
            }
        }
        [UnityTest] public IEnumerator OrdinaryEnemyMovementIsSlowedAndFreezeCancelsItsAttack()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();
            using(var sim=arena.AcquireManualSimulation())
            {
                Reset(sim,room,arena,null);arena.Player.Position=new Vector2(0,1000);
                var floor=new GameObject("Ink movement fixture floor");floor.transform.position=new Vector2(0,999.5f);floor.AddComponent<ArenaSurface>();floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);
                var enemy=arena.SpawnEnemy(0,new Vector2(6,1000));enemy.aggressive=true;enemy.maxHealth=enemy.health=1000;Physics2D.SyncTransforms();
                float x=enemy.Position.x;Tick(sim,.2f);float normal=Mathf.Abs(enemy.Position.x-x);Assert.That(normal,Is.GreaterThan(0));
                enemy.ResetEnemy();Equip(room,arena,"b_slow");Hit(arena,enemy,1);sim.Step(.02f);x=enemy.Position.x;Tick(sim,.2f);
                Assert.That(Mathf.Abs(enemy.Position.x-x),Is.LessThan(normal));
                enemy.spawn=arena.Player.Position+Vector2.right*1.5f;enemy.ResetEnemy();Physics2D.SyncTransforms();
                for(int i=0;i<100 && enemy.Telegraph<0;i++)sim.Step(.02f);
                Assert.That(enemy.Telegraph,Is.GreaterThanOrEqualTo(0),"ordinary enemy has started a real attack");
                Equip(room,arena,"b_prison");Hit(arena,enemy,1,special:true);sim.Step(.02f);
                Assert.That(enemy.stunUntil,Is.GreaterThan(arena.Now));Assert.That(enemy.Telegraph,Is.LessThan(0),"freeze cancels the pending attack, including boss-compatible stun semantics");
                Tick(sim,.2f);Assert.That(enemy.Telegraph,Is.LessThan(0));
            }
        }
    }
}
