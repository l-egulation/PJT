using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace ForestCombatV2.Tests
{
 public sealed class InkRedesignRuntimeTests
 {
  static float oldMin,oldMax;static bool varianceChanged;
  [UnityTearDown] public IEnumerator Cleanup(){if(varianceChanged){CombatDamageVariance.Settings.minimum=oldMin;CombatDamageVariance.Settings.maximum=oldMax;varianceChanged=false;}if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
  static CombatArena Create()
  {
   oldMin=CombatDamageVariance.Settings.minimum;oldMax=CombatDamageVariance.Settings.maximum;varianceChanged=true;CombatDamageVariance.Settings.minimum=CombatDamageVariance.Settings.maximum=1;
   var a=new GameObject("Ink redesign arena").AddComponent<CombatArena>();a.loadCharacterArt=false;
   var content=CombatContentLoader.Load(null,false);content.Profile=CombatCompositionProfile.Game;
   content.Camera=new GameObject("Camera").AddComponent<Camera>();content.Loadout=Deviant.Equipment.WeaponResources.CreateLoadout();
   content.PlayerSize=new Vector2(.9375f,2);content.PlayerOffset=new Vector2(.1875f,1);
   content.PlayerSpawn=new Vector2(0,100);content.Enemies=new[]{new CombatEnemySpawn("Target",0,new Vector2(1,100),new Vector2(1,2),Vector2.up,100000,false,false,1)};
   Assert.IsTrue(a.Initialize(content));a.Player.UseTestInput=true;
   a.MvpGrowth=new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text),new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text)));
   a.MvpGrowth.Economy.Earn(100000);return a;
  }
  static void Equip(CombatArena a,int slot,int level,params string[] ids)
  {
   var g=a.MvpGrowth;var w=a.Player.weapons[slot].InstanceId;var p=g.GrantPalette(Enumerable.Repeat("colorless",5).ToArray());g.Bind(p.Id,w);
   for(int i=0;i<ids.Length;i++){g.Insert(w,i,g.GrantInk(ids[i]).Id);for(int j=0;j<level;j++)g.Upgrade(w,i);}
  }
  static void Hit(CombatArena a,int slot=0,float damage=1,float healthAtImpact=-1)
  {
   var w=a.Player.weapons[slot];a.TryBindActor(a.Player,out var source);var execution=Guid.NewGuid();
   var stamp=new CombatAttackStamp(w.InstanceId,"base_sword","ink_test","test",true,-1,w.InkPalette.Read());
   var action=typeof(CombatArena).GetMethod("BeginInkAction",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(a,new object[]{stamp,execution,(CombatActor)a.Player});
   if(healthAtImpact>=0)a.Player.health=a.Player.maxHealth*healthAtImpact;
   var origin=new CombatHitOrigin(source,default,execution,0,"ink_test",Guid.Empty,CombatHitKind.Melee,stamp:stamp,sourceFacing:1);
   if(healthAtImpact>=0)
   {
    var slots=(GrowthSlot[])action.GetType().GetField("MvpSlots").GetValue(action);
    float calculated=(float)typeof(CombatArena).GetMethod("InkOriginalDamage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(a,new object[]{origin,damage,a.enemies[0]});
    Assert.That(calculated,Is.EqualTo(healthAtImpact>=.8f?17.5f:10).Within(.01),$"hp={a.Player.health}/{a.Player.maxHealth}, level={slots[0].Level}, threshold={slots[0].Part.levels[slots[0].Level].threshold}, amount={slots[0].Part.levels[slots[0].Level].amount}, snapshotHigh={Value(action,"MvpHighHealthPower")}, scale={Value(action,"DamageScale")}");
   }
   Assert.IsTrue(a.enemies[0].Receive(damage,a.Player,1,true,null,origin:origin));
  }
  static void Tick(CombatArena.ManualSimulation sim,float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/.01f);i++)sim.Step(.01f);}
  static object Action(CombatCast cast)=>typeof(CombatCast).GetField("InkAction",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cast);
  static float Value(object obj,string field)=>(float)obj.GetType().GetField(field).GetValue(obj);
  [Test]public void UniformGeometryScalesAllEdgesExactlyOnce()
  {
   var plain=CombatCast.ScaleHitBox(1,.5f,3,2,1,1);var scaled=CombatCast.ScaleHitBox(1,.5f,3,2,1,1,1.5f);
   Assert.That(scaled.X-scaled.Width/2,Is.EqualTo((plain.X-plain.Width/2)*1.5f));
   Assert.That(scaled.X+scaled.Width/2,Is.EqualTo((plain.X+plain.Width/2)*1.5f));
   Assert.That(scaled.Y-scaled.Height/2,Is.EqualTo((plain.Y-plain.Height/2)*1.5f));
   Assert.That(scaled.Y+scaled.Height/2,Is.EqualTo((plain.Y+plain.Height/2)*1.5f));
  }
  [UnityTest]public IEnumerator TargetCooldownAndChargeRecoveryUseNewRules()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();var a=Create();var target=a.enemies[0];
   using(var sim=a.AcquireManualSimulation())
   {
    Equip(a,0,5,"ink3_purple_03");Hit(a);Tick(sim,.02f);Assert.That(target.stunUntil,Is.GreaterThan(a.Now));
    Tick(sim,4.8f);Hit(a);Tick(sim,.02f);Assert.That(target.stunUntil,Is.LessThan(a.Now),"normal target waits five seconds");
    Tick(sim,.3f);Hit(a);Tick(sim,.02f);Assert.That(target.stunUntil,Is.GreaterThan(a.Now),"normal target may be stunned again");
    Equip(a,0,5,"ink3_purple_03");target.isElite=true;Tick(sim,3);Hit(a);Tick(sim,.02f);float until=target.stunUntil;Assert.That(until,Is.GreaterThan(a.Now));
    Tick(sim,6);Hit(a);Tick(sim,.02f);Assert.That(target.stunUntil,Is.EqualTo(until),"elite uses twenty seconds");
    Tick(sim,14.1f);Hit(a);Tick(sim,.02f);Assert.That(target.stunUntil,Is.GreaterThan(a.Now));
    Equip(a,0,0,"ink3_blue_09");Tick(sim,.02f);var w=a.Player.weapons[0];var skill=a.catalog.forms[w.FormIndex].specials[w.SpecialIndex];
    Assert.AreEqual(skill.capacity+1,w.Cooldown.Capacity);Assert.That(w.Cooldown.Period,Is.EqualTo(w.CooldownPeriod(skill.cooldown)*.9f).Within(.001));
    w.Cooldown.Spend(a.Now);Tick(sim,.5f);float oldRemaining=w.Cooldown.NextReady-a.Now,oldPeriod=w.Cooldown.Period;
    for(int i=0;i<5;i++)a.MvpGrowth.Upgrade(w.InstanceId,0);Tick(sim,.01f);
    Assert.That(w.Cooldown.Period,Is.EqualTo(w.CooldownPeriod(skill.cooldown)*.75f).Within(.001));
    Assert.That((w.Cooldown.NextReady-a.Now)/w.Cooldown.Period,Is.EqualTo((oldRemaining-.01f)/oldPeriod).Within(.003));
    Assert.IsFalse(a.SimulationFaulted);
   }
  }
  [UnityTest]public IEnumerator TransferredMarksGainTheirBonusOnceAndFullConditionUsesImpactHealth()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();var a=Create();var target=a.enemies[0];
   using(var sim=a.AcquireManualSimulation())
   {
    Equip(a,0,5,"ink3_green_06");a.Player.health=a.Player.maxHealth;
    float hp=target.health;Hit(a,0,10,.79f);Tick(sim,.01f);Assert.That(hp-target.health,Is.EqualTo(10).Within(.01));
    hp=target.health;Hit(a,0,10,.8f);Tick(sim,.01f);Assert.That(hp-target.health,Is.EqualTo(17.5f).Within(.01));
    Equip(a,0,0,"ink3_red_07");Equip(a,1,5,"ink3_yellow_09");
    var second=a.SpawnEnemy(0,target.Position+Vector2.right);var third=a.SpawnEnemy(0,target.Position+Vector2.right*2);
    second.aggressive=third.aggressive=false;second.maxHealth=second.health=100000;third.maxHealth=third.health=100000;
    for(int i=0;i<4;i++){Hit(a,0,.001f);Tick(sim,.01f);}
    target.health=second.health=.01f;int transfers=0;a.MvpInkCued+=c=>{if(c.Kind=="mark_chain_spread"&&c.Count>0)transfers++;};
    hp=third.health;Hit(a,1,.001f);Tick(sim,.5f);
    float reference=a.catalog.forms[a.Player.weapons[1].FormIndex].basics.Average(b=>b.damage);
    Assert.AreEqual(2,transfers);Assert.That(hp-third.health,Is.EqualTo(2*reference*.15f*1.6f).Within(.02),"second transfer must not multiply by 1.6 again");
   }
  }
  [UnityTest]public IEnumerator WeakerPoisonPreservesExistingStacksAndStrongerTickUntilExpiry()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();var a=Create();var target=a.enemies[0];
   using(var sim=a.AcquireManualSimulation())
   {
    Equip(a,0,5,"ink3_purple_05");for(int i=0;i<3;i++){Hit(a);Tick(sim,.01f);}Assert.AreEqual(3,a.ReadMvpPoison(target));
    Equip(a,1,0,"ink3_purple_05");Hit(a,1);Tick(sim,.01f);Assert.AreEqual(3,a.ReadMvpPoison(target),"lower cap must not erase existing stacks");
    float before=target.health;Tick(sim,.5f);float reference=a.catalog.forms[a.Player.weapons[0].FormIndex].basics.Average(b=>b.damage);
    Assert.That(before-target.health,Is.EqualTo(reference*.1f*3).Within(.01),"lower damage application retains stronger source");
    Tick(sim,3.1f);Assert.AreEqual(0,a.ReadMvpPoison(target));Hit(a,1);Tick(sim,.01f);Assert.AreEqual(1,a.ReadMvpPoison(target));
    before=target.health;Tick(sim,.5f);reference=a.catalog.forms[a.Player.weapons[1].FormIndex].basics.Average(b=>b.damage);
    Assert.That(before-target.health,Is.EqualTo(reference*.06f).Within(.01),"expiry clears previous strongest source");
   }
  }
  [UnityTest]public IEnumerator OverpaintDoesNotTriggerItselfOrMultiplyRangeDamageTwice()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();var a=Create();var target=a.enemies[0];
   using(var sim=a.AcquireManualSimulation())
   {
    Equip(a,0,5,"ink3_red_01");float hp=target.health;Hit(a,0,10);Tick(sim,.02f);
    Assert.That(hp-target.health,Is.EqualTo(15).Within(.01),"one 50 percent extra hit, no self-generated 25 percent hit");
    Equip(a,0,5,"ink3_blue_01","ink3_red_01");hp=target.health;Hit(a,0,10);Tick(sim,.02f);
    Assert.That(hp-target.health,Is.EqualTo(19.5f).Within(.01),"13 primary plus 6.5 extra; the extra is not multiplied by range again");
   }
  }
  [UnityTest]public IEnumerator GeometryAndRecoveryCancelApplyToTheAdmittedAction()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();var a=Create();var p=a.Player;
   using(var sim=a.AcquireManualSimulation())
   {
    int form=Array.FindIndex(a.catalog.forms,f=>f.id=="base_sword");p.Reconfigure(0,form,0);p.Reconfigure(1,form,0);p.selected=0;
    Equip(a,0,5,"ink3_blue_01");Hit(a,0,10);Tick(sim,.01f);Assert.That(100000-a.enemies[0].health,Is.EqualTo(13).Within(.001),"primary hit gets 30 percent");
    sim.Step(.005f,new CombatInput(pressed:CombatButtons.Weapon1Basic));Assert.That(Value(Action(p.currentCast),"MvpAllGeometryScale"),Is.EqualTo(1.5f));p.CancelAction();
    Equip(a,0,5,"ink3_yellow_07");p.Position=new Vector2(0,100);a.enemies[0].Position=new Vector2(1,100);p.velocity=a.enemies[0].velocity=Vector2.zero;Physics2D.SyncTransforms();
    sim.Step(.005f,new CombatInput(pressed:CombatButtons.Weapon1Basic));var first=p.currentCast;
    sim.Step(.005f,new CombatInput(pressed:CombatButtons.Weapon2Basic));
    for(int i=0;i<300&&p.selected==0;i++)sim.Step(.005f);
    Assert.AreEqual(1,p.selected);Assert.IsNotNull(p.currentCast);Assert.AreNotSame(first,p.currentCast);
    Assert.That(Value(Action(p.currentCast),"DamageScale"),Is.EqualTo(1.35f).Within(.001),"only the admitted other-weapon basic gets the bonus");
    p.CancelAction();sim.Step(.005f,new CombatInput(pressed:CombatButtons.Weapon2Basic));Assert.That(Value(Action(p.currentCast),"DamageScale"),Is.EqualTo(1).Within(.001),"bonus is not carried into a later attack");
    Assert.IsFalse(a.SimulationFaulted);
   }
  }
 }
}
