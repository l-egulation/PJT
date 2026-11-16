using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
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
 public sealed class WeaponGrowthMvpCombatTests
 {
  [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();DummyTrainingTools.SelectNormalMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
  sealed class Fixture
  {
   public CombatArena Arena;public CombatPlayer Player;public DummyTrainingRoom Room;public TrainingDummy Target;public WeaponGrowth Growth;public CombatActorHandle Source;
   public void Equip(int weapon,string color,params string[] parts)
   {var p=Growth.GrantPalette(Enumerable.Repeat(color,Math.Max(3,parts.Length)).ToArray());Growth.Bind(p.Id,Player.weapons[weapon].InstanceId);for(int i=0;i<parts.Length;i++)Growth.Insert(Player.weapons[weapon].InstanceId,i,Growth.GrantInk(parts[i]).Id);}
   // 실제 기본 공격과 같은 무기 도장을 단 원타격 1회
   public bool Hit(CombatActor target,int weapon=0,float damage=1){var w=Player.weapons[weapon];var origin=new CombatHitOrigin(Source,default,Guid.NewGuid(),0,"mvp_fixture",Guid.Empty,CombatHitKind.Melee,stamp:new CombatAttackStamp(w.InstanceId,"base_sword","mvp_fixture","test",true,-1,w.InkPalette.Read()),sourceFacing:1);return target.Receive(damage,Player,1,true,null,origin:origin);}
   public bool HitAs(CombatActor target,Guid execution,int weapon=0,float damage=1){var w=Player.weapons[weapon];var origin=new CombatHitOrigin(Source,default,execution,0,"mvp_fixture",Guid.Empty,CombatHitKind.Melee,stamp:new CombatAttackStamp(w.InstanceId,"base_sword","mvp_fixture","test",true,-1,w.InkPalette.Read()),sourceFacing:1);return target.Receive(damage,Player,1,true,null,origin:origin);}
   public void EquipMixed(int weapon,string[] colors,params string[] parts){var p=Growth.GrantPalette(colors);Growth.Bind(p.Id,Player.weapons[weapon].InstanceId);for(int i=0;i<parts.Length;i++)if(parts[i]!=null)Growth.Insert(Player.weapons[weapon].InstanceId,i,Growth.GrantInk(parts[i]).Id);}
   public TrainingDummy Near(float dx){foreach(var t in Room.Targets)if(t&&t!=Target&&!t.Dead){t.Position=Target.Position+Vector2.right*dx;Physics2D.SyncTransforms();return t;}return null;}
   public T Field<T>(string name)=>(T)typeof(CombatArena).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Arena);
   public void Switch()=>typeof(CombatPlayer).GetMethod("SwitchNow",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Player,new object[]{default(CombatCommandKey)});
   // 실제 공격처럼 행동 시작(BeginInkAction)을 거친 원타격 1회. 공격 피해·준비 구간 같은 행동 단위 잉크 효과를 확인할 때 쓴다
   public bool Attack(CombatActor target,int weapon=0,bool basic=true,float damage=10){var w=Player.weapons[weapon];var exec=Guid.NewGuid();var stamp=new CombatAttackStamp(w.InstanceId,"base_sword","mvp_fixture","test",basic,-1,w.InkPalette.Read());typeof(CombatArena).GetMethod("BeginInkAction",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Arena,new object[]{stamp,exec,(CombatActor)Player});var origin=new CombatHitOrigin(Source,default,exec,0,"mvp_fixture",Guid.Empty,CombatHitKind.Melee,stamp:stamp,sourceFacing:1);return target.Receive(damage,Player,1,basic,null,origin:origin);}
   // 체력을 채우고 한 번 공격해 깎인 양을 잰다
   public float Loss(CombatActor target,int weapon=0,bool basic=true,float damage=10){target.health=target.maxHealth;float before=target.health;Attack(target,weapon,basic,damage);return before-target.health;}
   // 수동 시뮬레이션에서는 시험 입력 대신 대시 명령을 바로 실행한다
   public void Dash(){typeof(CombatPlayer).GetMethod("TryDash",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Player,new object[]{default(CombatCommandKey)});Assert.IsTrue(Player.Dashing,"the dash starts");}
   public Guid SlotInk(int weapon,int index)=>Growth.Equipped(Player.weapons[weapon].InstanceId).Slots[index].InstanceId;
   // 실제 런처럼 잉크 파츠 이펙트를 붙인다(훈련장에는 없다). 신호를 받은 뒤 Shown·SheetAt 으로 띄운 시트를 확인한다
   public Chapter1InkPartFx Fx(){var fx=Arena.gameObject.AddComponent<Chapter1InkPartFx>();fx.Initialize(Arena);return fx;}
  }
  // 띄운 시트의 자리(첫 장을 그린 뒤). 없으면 null
  static Vector2? SheetAt(Chapter1InkPartFx fx,string sheet)
  {
   typeof(Chapter1InkPartFx).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fx,null);
   var body=fx.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r=>r.gameObject.name=="InkFx "+sheet);
   return body?(Vector2?)(Vector2)body.transform.position:null;
  }
  // 플레이 모드 진입은 테스트 본문에서 직접 yield 해야 한다. 그 뒤 이 함수로 훈련장 대상과 성장을 묶는다
  static Fixture Make()
  {
   var world=Object.FindAnyObjectByType<ForestCombatWorld>();var f=new Fixture{Arena=world.arena,Room=world.GetComponent<DummyTrainingRoom>()};f.Player=f.Arena.Player;f.Target=f.Room.Targets[2];
   f.Growth=new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text),new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text)));f.Arena.MvpGrowth=f.Growth;
   f.Player.CancelAction();f.Player.selected=0;f.Player.facing=1;f.Target.Position=f.Player.Position+Vector2.right*1.1f;Physics2D.SyncTransforms();f.Arena.TryBindActor(f.Player,out f.Source);
   return f;
  }
  // 분사·손대중 획의 기존 radius/range 조합. 통합 범위 증폭의 uniform 확장은 InkRedesignRuntimeTests에서 검사한다.
  [Test]public void RadiusScaleKeepsTheFrontEdgeWhileRangeScaleExtendsIt()
  {
   var plain=CombatCast.ScaleHitBox(1,0,2,1,1,1);var wide=CombatCast.ScaleHitBox(1,0,2,1,1,1.2f);var far=CombatCast.ScaleHitBox(1,0,2,1,1.15f,1);
   Assert.That(plain.X+plain.Width/2,Is.EqualTo(2).Within(1e-5));Assert.That(wide.X+wide.Width/2,Is.EqualTo(2).Within(1e-5),"radius keeps the front edge");
   Assert.That(wide.Width,Is.EqualTo(2.4f).Within(1e-5));Assert.That(wide.Height,Is.EqualTo(1.2f).Within(1e-5));
   Assert.That(far.X+far.Width/2,Is.EqualTo(2.3f).Within(1e-5),"range extends the front edge");
  }
  // 집중 공격: 한 공격이 여러 마리를 맞혀도 집중 대상과 중첩이 유지되고, 다음 공격이 다른 몬스터만 맞히면 옮겨 간다
  [UnityTest]public IEnumerator FocusStacksKeepTheirTargetThroughAMultiTargetAttack()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();var other=f.Near(-.4f);Assert.IsNotNull(other);
   f.Equip(0,"red","ink3_red_02");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<3;i++){f.HitAs(f.Target,Guid.NewGuid());driver.Step(.02f);}
    Assert.AreEqual(3,f.Field<int>("mvpFocusStacks"));
    var sweep=Guid.NewGuid();f.HitAs(other,sweep);f.HitAs(f.Target,sweep);driver.Step(.02f);
    Assert.AreEqual(4,f.Field<int>("mvpFocusStacks"),"hitting a second monster in the same attack does not reset the focus");
    f.HitAs(other,Guid.NewGuid());driver.Step(.02f);
    Assert.AreEqual(1,f.Field<int>("mvpFocusStacks"),"a later attack that hits only another monster moves the focus");
   }
  }
  // 처치 폭발: 독(지속 피해)으로 처치해도 처치 트리거가 발동해 주변에 튄다. 반경 6이라 옛 반경 2 바깥(4.5)도 맞는다
  [UnityTest]public IEnumerator KillSplashFiresWhenPoisonFinishesTheMonster()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();var near=f.Near(4.5f);Assert.IsNotNull(near);
   f.EquipMixed(0,new[]{"purple","purple","red","red"},"ink3_purple_05",null,"ink3_red_05");var fx=f.Fx();
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    f.Hit(f.Target,0,.01f);driver.Step(.02f);Assert.AreEqual(1,f.Arena.ReadMvpPoison(f.Target));
    f.Target.health=.05f;float nearBefore=near.health;
    for(int i=0;i<60&&!f.Target.Dead;i++)driver.Step(.02f);driver.Step(.02f);
    Assert.IsTrue(f.Target.Dead,"poison finishes the target");Assert.That(near.health,Is.LessThan(nearBefore),"the poison kill still splashes");
    // 가시 대신 반경 6 잉크 폭발 시트가 쓰러진 몬스터 몸통(발밑 + 바닥 28px)에서 터진다
    var at=SheetAt(fx,"ink-splash_red");Assert.IsTrue(at.HasValue,"the kill splash plays the wide ink burst sheet");
    Assert.That(Vector2.Distance(at.Value,f.Target.Position+Vector2.up*(28/32f)),Is.LessThan(.01f),"the burst is centred on the fallen monster");
    Assert.IsFalse(SheetAt(fx,"burst-thorn_red").HasValue,"no thorn rises for the kill splash");
   }
  }
  // 먹물 늪과 선제 제압은 회복 구간을 따로 써서 같은 팔레트에서 서로 막지 않는다
  [UnityTest]public IEnumerator BindAndStunDoNotBlockEachOther()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"purple","ink3_purple_01","ink3_purple_03");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    f.Hit(f.Target);driver.Step(.02f);
    Assert.That(f.Target.rootUntil,Is.GreaterThan(f.Arena.Now),"bound");Assert.That(f.Target.stunUntil,Is.GreaterThan(f.Arena.Now),"and stunned on the same first hit");
   }
  }
  // 선제 제압의 첫 적중은 이 파츠를 낀 무기의 첫 적중이다. 다른 무기로 먼저 때려도 기회가 남는다
  [UnityTest]public IEnumerator FirstHitStunWaitsForTheWeaponThatCarriesIt()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"red","ink3_red_01");f.Equip(1,"purple","ink3_purple_03");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    f.Hit(f.Target,0);driver.Step(.02f);Assert.That(f.Target.stunUntil,Is.LessThanOrEqualTo(f.Arena.Now));
    f.Hit(f.Target,1);driver.Step(.02f);Assert.That(f.Target.stunUntil,Is.GreaterThan(f.Arena.Now),"the stun weapon's first hit stuns");
    for(int i=0;i<100;i++)driver.Step(.02f);f.Hit(f.Target,1);driver.Step(.02f);Assert.That(f.Target.stunUntil,Is.LessThanOrEqualTo(f.Arena.Now),"five-second per-target cooldown");
   }
  }
  // 티끌 모아 태산의 적중 수는 런 내내 이어진다. 플레이어 일시 상태 초기화로 지우지 않는다
  [UnityTest]public IEnumerator HitCountersSurviveTransientReset()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"green","ink3_green_05");f.Player.health=f.Player.maxHealth-20;
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<30;i++){f.Hit(f.Target);driver.Step(.02f);}
    typeof(CombatArena).GetMethod("ResetMvpTransient",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Arena,null);
    for(int i=0;i<20;i++){f.Hit(f.Target);driver.Step(.02f);}
    Assert.That(f.Player.health,Is.EqualTo(f.Player.maxHealth-16).Within(.001),"the 50th hit across the reset heals");
   }
  }
  static System.Collections.Generic.List<CombatAcceptedHit> Read(CombatArena arena,ref CombatOutcomeCursor cursor)
  {var result=new System.Collections.Generic.List<CombatAcceptedHit>();var buffer=new CombatAcceptedHit[128];while(true){Assert.IsTrue(arena.HitEvents.TryRead(cursor,buffer,out var page));cursor=page.Next;result.AddRange(buffer.Take(page.Count));if(page.Count<buffer.Length)return result;}}
  static float Reference(Fixture f,int weapon)=>f.Arena.catalog.forms[f.Player.weapons[weapon].FormIndex].basics.Average(b=>b.damage)*f.Growth.AttackMultiplier;
  // 무지개 표식: 최대 7개, 다른 무기로 맞히면 쌓인 역순으로 0.08초마다 하나씩 터지고 "표식 터트릴 시" 파츠는 마지막 빨강에서 발동한다
  [UnityTest]public IEnumerator RainbowMarksCapAtSevenAndBurstInReverseOneByOne()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(1,"red","ink3_red_04");
   var cues=new System.Collections.Generic.List<MvpInkCue>();f.Arena.MvpInkCued+=cues.Add;
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<9;i++){f.Hit(f.Target,0);driver.Step(.02f);}
    Assert.AreEqual(7,f.Arena.ReadInkMarks(f.Target),"seven colours at most");Assert.AreEqual(7,f.Arena.ReadMvpMarkDisplay(f.Target));
    f.Arena.TryGetCursor(out var cursor);f.Hit(f.Target,1);driver.Step(.01f);
    Assert.AreEqual(1,cues.Count(c=>c.Kind=="mark_pop"),"one mark pops at a time");Assert.AreEqual(7,cues.First(c=>c.Kind=="mark_pop").Count,"the last stacked colour pops first");
    Assert.AreEqual(6,f.Arena.ReadMvpMarkDisplay(f.Target),"the number counts down while the chain runs");
    Assert.IsFalse(cues.Any(c=>c.Kind=="mark_burst"),"burst parts wait for the red mark");
    for(int i=0;i<40;i++)driver.Step(.02f);
    CollectionAssert.AreEqual(new[]{7,6,5,4,3,2,1},cues.Where(c=>c.Kind=="mark_pop").Select(c=>c.Count).ToArray());
    int last=cues.FindLastIndex(c=>c.Kind=="mark_pop"),burst=cues.FindIndex(c=>c.Kind=="mark_burst"),thorn=cues.FindIndex(c=>c.Kind=="burst_extra_hit");
    Assert.That(burst,Is.GreaterThan(last));Assert.That(thorn,Is.GreaterThan(burst));Assert.AreEqual(7,cues[burst].Count);
    float per=Reference(f,1)*f.Growth.Data.markCoefficient;
    Assert.AreEqual(7,Read(f.Arena,ref cursor).Count(h=>h.Origin.Kind==CombatHitKind.InkPart&&Mathf.Abs(h.RequestedDamage-per)<.001f),"each mark deals 15% of the reference");
    Assert.AreEqual(1,f.Arena.ReadInkMarks(f.Target),"the bursting weapon leaves its own mark");
   }
   f.Arena.MvpInkCued-=cues.Add;
  }
  // 붉은 낙관: 표식 하나당 피해만 50% 올린다. 혼자서는 표식을 켜지 않는다
  [UnityTest]public IEnumerator RedSealRaisesEachMarkPop()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(1,"red","ink3_red_07");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    f.Hit(f.Target,0);driver.Step(.02f);Assert.AreEqual(0,f.Arena.ReadInkMarks(f.Target),"the seal alone does not turn marks on");
    f.Equip(1,"red","ink3_red_07","ink3_red_04");
    f.Hit(f.Target,0);driver.Step(.02f);f.Hit(f.Target,0);driver.Step(.02f);
    f.Arena.TryGetCursor(out var cursor);f.Hit(f.Target,1);for(int i=0;i<20;i++)driver.Step(.02f);
    float per=Reference(f,1)*f.Growth.Data.markCoefficient*1.25f;
    Assert.AreEqual(2,Read(f.Arena,ref cursor).Count(h=>h.Origin.Kind==CombatHitKind.InkPart&&Mathf.Abs(h.RequestedDamage-per)<.001f));
   }
  }
  // 번개 표식은 노랑(3번째) 표식이 터질 때만 발동한다. 표식이 2개면 그 색에 닿지 않는다
  [UnityTest]public IEnumerator ColourMarkPartsNeedTheirColourToBeReached()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();var near=f.Near(1f);Assert.IsNotNull(near);
   f.Equip(1,"yellow","ink3_yellow_08");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    float nearHp=near.health;
    for(int i=0;i<2;i++){f.Hit(f.Target,0);driver.Step(.02f);}
    f.Hit(f.Target,1);for(int i=0;i<30;i++)driver.Step(.02f);
    Assert.That(near.health,Is.EqualTo(nearHp).Within(.0001),"two marks never reach yellow");
    for(int i=0;i<3;i++){f.Hit(f.Target,0);driver.Step(.02f);}
    Assert.AreEqual(3,f.Arena.ReadInkMarks(f.Target));
    f.Hit(f.Target,1);for(int i=0;i<30;i++)driver.Step(.02f);
    Assert.That(near.health,Is.LessThan(nearHp),"the yellow mark sends lightning to a neighbour");
   }
  }
  // 몬스터가 죽으면 연쇄가 멈추고 "표식 터트릴 시" 파츠도 발동하지 않는다
  [UnityTest]public IEnumerator MarkChainStopsWhenTheMonsterDies()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();var near=f.Near(1f);Assert.IsNotNull(near);
   f.Equip(1,"red","ink3_red_04");
   var cues=new System.Collections.Generic.List<MvpInkCue>();f.Arena.MvpInkCued+=cues.Add;
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<4;i++){f.Hit(f.Target,0,.001f);driver.Step(.02f);}
    f.Target.health=.01f;float nearHp=near.health;
    f.Hit(f.Target,1,.001f);for(int i=0;i<30;i++)driver.Step(.02f);
    Assert.IsTrue(f.Target.Dead,"the first pop finishes the target");
    Assert.AreEqual(1,cues.Count(c=>c.Kind=="mark_pop"),"the chain stops with the target");
    Assert.IsFalse(cues.Any(c=>c.Kind=="mark_burst"||c.Kind=="burst_extra_hit"),"burst parts need the red mark");
    Assert.That(near.health,Is.EqualTo(nearHp).Within(.0001));
   }
   f.Arena.MvpInkCued-=cues.Add;
  }
  // 번지는 표식: 연쇄 중 몬스터가 죽으면 남은 표식이 가까운 몬스터로 옮겨 가 끝까지 터진다
  [UnityTest]public IEnumerator SpreadingMarksCarryTheChainToANeighbour()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();var near=f.Near(1f);Assert.IsNotNull(near);
   f.EquipMixed(1,new[]{"yellow","red","red"},"ink3_yellow_09","ink3_red_04");
   var cues=new System.Collections.Generic.List<MvpInkCue>();f.Arena.MvpInkCued+=cues.Add;
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    // 람다가 잡는 지역 변수는 플레이 모드 진입(도메인 다시 불러오기) 뒤 블록 안에서 만든다
    var neighbour=near;
    for(int i=0;i<4;i++){f.Hit(f.Target,0,.001f);driver.Step(.02f);}
    f.Target.health=.01f;float nearHp=near.health;
    f.Hit(f.Target,1,.001f);for(int i=0;i<30;i++)driver.Step(.02f);
    Assert.IsTrue(f.Target.Dead);Assert.IsTrue(cues.Any(c=>c.Kind=="mark_chain_spread"&&c.Target==neighbour));
    CollectionAssert.AreEqual(new[]{4,3,2,1},cues.Where(c=>c.Kind=="mark_pop").Select(c=>c.Count).ToArray());
    Assert.IsTrue(cues.Any(c=>c.Kind=="mark_burst"&&c.Target==neighbour),"the chain finishes on the neighbour");
    Assert.That(near.health,Is.LessThan(nearHp));
   }
   f.Arena.MvpInkCued-=cues.Add;
  }
  // 색 번짐: 혼자서도 표식을 켜고, 가까운 몬스터 한 마리에게 표식을 더 묻힌다. 묻히기만으로는 터지지 않는다
  [UnityTest]public IEnumerator ColourSpreadAloneMarksAndSpreadsToANeighbour()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();var near=f.Near(1f);Assert.IsNotNull(near);
   f.Equip(0,"blue","ink3_blue_08");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    f.Hit(f.Target,0);driver.Step(.02f);
    Assert.AreEqual(1,f.Arena.ReadInkMarks(f.Target));Assert.AreEqual(1,f.Arena.ReadInkMarks(near),"the neighbour gets a mark too");
    float nearHp=near.health;f.Hit(f.Target,1);for(int i=0;i<20;i++)driver.Step(.02f);
    Assert.AreEqual(1,f.Arena.ReadInkMarks(f.Target),"weapon B burst weapon A's mark and left its own");
    Assert.AreEqual(1,f.Arena.ReadInkMarks(near),"weapon B has no spread part");Assert.That(near.health,Is.EqualTo(nearHp).Within(.0001));
   }
  }
  // 질주 충전: 3unit마다 기를 하나(최대 3) 쌓고, 원타격이 맞으면 기 하나당 기준 피해 15%로 방출한다. 2초 멈추면 사라진다
  [UnityTest]public IEnumerator MoveChargeReleasesStoredEnergyOnTheNextHit()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"yellow","ink3_yellow_10");var moved=typeof(CombatArena).GetMethod("MvpMoved",BindingFlags.Instance|BindingFlags.NonPublic);
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    moved.Invoke(f.Arena,new object[]{2.9f});Assert.AreEqual(0,f.Arena.ReadMvpCharge());
    moved.Invoke(f.Arena,new object[]{.2f});Assert.AreEqual(1,f.Arena.ReadMvpCharge());
    moved.Invoke(f.Arena,new object[]{9f});Assert.AreEqual(3,f.Arena.ReadMvpCharge(),"three at most");
    f.Arena.TryGetCursor(out var cursor);f.Hit(f.Target,0);driver.Step(.02f);
    Assert.AreEqual(0,f.Arena.ReadMvpCharge(),"the hit spends all the energy");
    float expected=Reference(f,0)*.15f*3;
    Assert.IsTrue(Read(f.Arena,ref cursor).Any(h=>h.Origin.Kind==CombatHitKind.InkPart&&Mathf.Abs(h.RequestedDamage-expected)<.001f),"three charges release 45% of the reference");
    moved.Invoke(f.Arena,new object[]{3.1f});Assert.AreEqual(1,f.Arena.ReadMvpCharge());
    for(int i=0;i<110;i++)driver.Step(.02f);
    Assert.AreEqual(0,f.Arena.ReadMvpCharge(),"energy fades after two seconds standing still");
   }
  }
  // 단단한 껍질: 체력이 깎이면 시간 제한 없는 보호막 10, 다음 피격 한 번에 사라진다. 쿨타임 20초
  [UnityTest]public IEnumerator HardShellBlocksOneHitThenWaitsForItsCooldown()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"green","ink3_green_08");f.Player.guardUntil=0;
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    Assert.IsTrue(f.Player.Receive(1,f.Target,-1,false,null));driver.Step(.02f);
    Assert.That(f.Arena.ReadInkShield(),Is.EqualTo(10).Within(.001),"a real health loss grows the shell");
    for(int i=0;i<150;i++)driver.Step(.02f);
    Assert.That(f.Arena.ReadInkShield(),Is.EqualTo(10).Within(.001),"the shell has no time limit");
    float hp=f.Player.health;f.Player.Receive(3,f.Target,-1,false,null);driver.Step(.02f);
    Assert.That(f.Arena.ReadInkShield(),Is.Zero,"one hit breaks the shell");Assert.That(f.Player.health,Is.EqualTo(hp).Within(.001),"the shell absorbed it");
    for(int i=0;i<60;i++)driver.Step(.02f);
    f.Player.Receive(1,f.Target,-1,false,null);driver.Step(.02f);
    Assert.That(f.Arena.ReadInkShield(),Is.Zero,"twenty-second cooldown");
   }
  }
  // 독폭탄: 독에 걸린 몬스터를 맞히면 남은 독 피해를 한 번에 주고, 쿨타임 2초 동안은 독이 다시 쌓인다
  [UnityTest]public IEnumerator PoisonBombBurstsTheRestOfThePoisonOnItsCooldown()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"purple","ink3_purple_05","ink3_purple_09");var fx=f.Fx();
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    // 람다가 잡는 지역 변수는 플레이 모드 진입(도메인 다시 불러오기) 뒤 블록 안에서 만든다
    float tick=Reference(f,0)*.08f;
    f.Hit(f.Target,0,.01f);driver.Step(.02f);Assert.AreEqual(1,f.Arena.ReadMvpPoison(f.Target),"no poison yet, so the bomb keeps its cooldown");
    Assert.IsFalse(SheetAt(fx,"ink-splash_purple").HasValue,"no burst before poison");
    f.Arena.TryGetCursor(out var cursor);f.Hit(f.Target,0,.01f);driver.Step(.02f);
    Assert.IsTrue(Read(f.Arena,ref cursor).Any(h=>h.Origin.Kind==CombatHitKind.InkPart&&Mathf.Abs(h.RequestedDamage-tick*5)<.001f),"the five remaining ticks land at once");
    // 독폭탄은 플레이어 몸이 아니라 독이 터진 몬스터 몸통에서 펑 터진다
    var at=SheetAt(fx,"ink-splash_purple");Assert.IsTrue(at.HasValue,"the poison bomb plays its burst sheet");
    Assert.That(Vector2.Distance(at.Value,f.Target.Position+Vector2.up*(28/32f)),Is.LessThan(.01f),"the burst sits on the poisoned monster");
    Assert.That(Vector2.Distance(at.Value,f.Player.Position),Is.GreaterThan(.5f),"not around the player");
    Assert.AreEqual(1,f.Arena.ReadMvpPoison(f.Target),"the same hit poisons again after the burst");
    f.Arena.TryGetCursor(out cursor);f.Hit(f.Target,0,.01f);driver.Step(.02f);
    Assert.IsFalse(Read(f.Arena,ref cursor).Any(h=>h.Origin.Kind==CombatHitKind.InkPart&&h.RequestedDamage>tick*2),"no second burst inside two seconds");
   }
  }
  // 보랏빛 낙인은 보라(7번째) 표식에서 독을 1중첩씩 쌓고, 사약이 다시 걸어도 쌓인 중첩을 줄이지 않는다. 쪽빛 족쇄는 남색 표식에서 속박한다
  [UnityTest]public IEnumerator VioletAndIndigoMarksStackPoisonAndBind()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"purple","ink3_purple_05");f.Equip(1,"purple","ink3_purple_08","ink3_purple_07");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<7;i++){f.Hit(f.Target,0,.001f);driver.Step(.02f);}
    Assert.AreEqual(1,f.Arena.ReadMvpPoison(f.Target));
    f.Hit(f.Target,1,.001f);for(int i=0;i<5;i++)driver.Step(.02f);
    Assert.AreEqual(2,f.Arena.ReadMvpPoison(f.Target),"the violet mark adds a stack beyond 사약's own limit");
    Assert.That(f.Target.rootUntil,Is.GreaterThan(f.Arena.Now),"the indigo mark binds");
    for(int i=0;i<25;i++)driver.Step(.02f);
    for(int i=0;i<7;i++){f.Hit(f.Target,0,.001f);driver.Step(.02f);}
    Assert.AreEqual(2,f.Arena.ReadMvpPoison(f.Target),"사약 refreshes without lowering the stacks");
    f.Hit(f.Target,1,.001f);for(int i=0;i<5;i++)driver.Step(.02f);
    Assert.AreEqual(3,f.Arena.ReadMvpPoison(f.Target));
   }
  }
  static void PrepareScene(){DummyTrainingTools.Prepare();Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;}
  [UnityTest]public IEnumerator SwitchShieldUsesTheCurrentGrowthCapIncludingExistingNativeShield()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   Assert.That(f.Growth.Data.shieldCap,Is.EqualTo(50),"the existing safety limit moved into current growth data");
   f.Growth.Data.shieldCap=10;f.Player.shield=4;f.Equip(0,"green","ink3_green_02");
   using(var driver=f.Arena.AcquireManualSimulation())
   {f.Switch();f.Player.CancelAction();Assert.That(f.Arena.ReadInkShield(),Is.EqualTo(6).Within(.001),"native four plus new six reaches the current cap ten");
    for(int i=0;i<260;i++)driver.Step(.02f);Assert.That(f.Arena.ReadInkShield(),Is.Zero,"the five-second shield still expires");}
  }
  [UnityTest]public IEnumerator RealHitsCapMarksAndExplicitSwitchGrantsShieldBeforeAutomaticAttack()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"green","ink3_green_02");f.Equip(1,"red","ink3_red_04");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<9;i++){Assert.IsTrue(f.Hit(f.Target));driver.Step(.02f);}
    Assert.AreEqual(7,f.Arena.ReadInkMarks(f.Target));
    f.Switch();
    Assert.AreEqual(1,f.Player.selected);Assert.That(f.Arena.ReadInkShield(),Is.EqualTo(18).Within(.001));Assert.IsNotNull(f.Player.currentCast,"explicit switch starts automatic B attack after outgoing effects");
    float before=f.Target.health;for(int i=0;i<120;i++)driver.Step(.02f);Assert.That(f.Target.health,Is.LessThan(before));Assert.AreEqual(1,f.Arena.ReadInkMarks(f.Target));
   }
  }
  // 표식은 "표식 터트릴 시" 파츠가 어느 무기에도 없으면 쌓이지 않는다
  [UnityTest]public IEnumerator MarksNeedAMarkBurstPartOnEitherWeapon()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"red","ink3_red_01");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<3;i++){f.Hit(f.Target);driver.Step(.02f);}
    Assert.AreEqual(0,f.Arena.ReadInkMarks(f.Target),"no mark burst part, no marks");
    f.Equip(1,"blue","ink3_blue_05");
    for(int i=0;i<3;i++){f.Hit(f.Target);driver.Step(.02f);}
    Assert.AreEqual(3,f.Arena.ReadInkMarks(f.Target),"a burst part on the other weapon turns marking on for both");
   }
  }
  // 이펙트용 신호(MvpInkCued)는 규칙을 바꾸지 않고, 교체·표식 터트리기·터트리기 파츠 발동을 순서대로 알린다
  [UnityTest]public IEnumerator InkCuesReportSwitchMarkBurstAndPartColours()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"green","ink3_green_02");f.Equip(1,"red","ink3_red_04");
   var cues=new System.Collections.Generic.List<MvpInkCue>();f.Arena.MvpInkCued+=cues.Add;
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<3;i++){Assert.IsTrue(f.Hit(f.Target));driver.Step(.02f);}
    Assert.AreEqual(3,f.Arena.ReadInkMarks(f.Target));
    f.Switch();
    Assert.That(f.Arena.ReadInkShield(),Is.EqualTo(18).Within(.001),"신호를 구독해도 보호막 값은 그대로");
    for(int i=0;i<120;i++)driver.Step(.02f);
   }
   f.Arena.MvpInkCued-=cues.Add;
   var shield=cues.FindIndex(c=>c.Kind=="shield"&&c.PartId=="ink3_green_02"&&c.Trigger=="switch");
   var burst=cues.FindIndex(c=>c.Kind=="mark_burst");
   var thorn=cues.FindIndex(c=>c.Kind=="burst_extra_hit"&&c.PartId=="ink3_red_04");
   Assert.That(shield,Is.GreaterThanOrEqualTo(0),"교체 때 보호막 신호");Assert.That(burst,Is.GreaterThan(shield),"표식 터트리기 신호는 교체 뒤 자동 공격에서");Assert.That(thorn,Is.GreaterThan(burst),"터지는 잉크는 표식 터트리기 다음");
   Assert.AreEqual(3,cues[burst].Count);CollectionAssert.AreEqual(new[]{"red"},cues[burst].Colors);Assert.AreEqual(f.Target,cues[burst].Target);
   Assert.That(cues[shield].Duration,Is.GreaterThan(0));
  }
  // 사약: 다시 맞히면 시간만 갱신하고, 최대 중첩은 슬롯 강화 단계로만 늘어난다. 독 피해는 시간이 지나며 들어온다
  [UnityTest]public IEnumerator PoisonRefreshesAndStacksOnlyWithSlotLevel()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"purple","ink3_purple_05");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    f.Hit(f.Target);f.Hit(f.Target);driver.Step(.02f);
    Assert.AreEqual(1,f.Arena.ReadMvpPoison(f.Target),"level 0 allows one stack");
    float before=f.Target.health;for(int i=0;i<40;i++)driver.Step(.02f);
    Assert.That(f.Target.health,Is.LessThan(before),"poison ticks without new hits");
    f.Growth.Economy.Earn(1000);f.Growth.Upgrade(f.Player.weapons[0].InstanceId,0);f.Growth.Upgrade(f.Player.weapons[0].InstanceId,0);
    f.Hit(f.Target);f.Hit(f.Target);driver.Step(.02f);
    Assert.AreEqual(2,f.Arena.ReadMvpPoison(f.Target),"+2 allows two stacks");
    for(int i=0;i<200;i++)driver.Step(.02f);
    Assert.AreEqual(0,f.Arena.ReadMvpPoison(f.Target),"poison ends after its duration");
   }
  }
  // 먹물 늪은 속박(이동만 금지)과 함께 피해를 주고, 잔류 폭발은 속박이 풀릴 때 주변을 때린다
  [UnityTest]public IEnumerator BindDealsDamageAndBindEndExplodesAround()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   TrainingDummy near=null;foreach(var t in f.Room.Targets)if(t&&t!=f.Target&&!t.Dead){near=t;break;}
   Assert.IsNotNull(near,"a second training target");near.Position=f.Target.Position+Vector2.right*1f;Physics2D.SyncTransforms();
   f.Equip(0,"purple","ink3_purple_01","ink3_purple_02");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    float before=f.Target.health;f.Hit(f.Target,0,1);driver.Step(.02f);
    Assert.That(f.Target.rootUntil,Is.GreaterThan(f.Arena.Now),"bound");Assert.That(f.Target.stunUntil,Is.LessThanOrEqualTo(f.Arena.Now),"bind is not stun");
    Assert.That(before-f.Target.health,Is.GreaterThan(1.2f),"bind adds damage on top of the 1-point hit");
    float nearBefore=near.health;for(int i=0;i<60;i++)driver.Step(.02f);
    Assert.That(near.health,Is.LessThan(nearBefore),"the bind ending explodes around the target");
   }
  }
  // 티끌 모아 태산: 몬스터를 50번 때려야 회복한다. 때리지 않으면 회복이 오르지 않는다
  [UnityTest]public IEnumerator HealingNeedsFiftyHits()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"green","ink3_green_05");f.Player.health=f.Player.maxHealth-20;
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<120;i++)driver.Step(.02f);
    Assert.That(f.Player.health,Is.EqualTo(f.Player.maxHealth-20).Within(.001),"no healing without hits");
    for(int i=0;i<49;i++){f.Hit(f.Target);driver.Step(.02f);}
    Assert.That(f.Player.health,Is.EqualTo(f.Player.maxHealth-20).Within(.001));
    f.Hit(f.Target);driver.Step(.02f);
    Assert.That(f.Player.health,Is.EqualTo(f.Player.maxHealth-16).Within(.001),"the 50th hit heals 4");
   }
  }
  // 전기 장판: 표식을 터트린 자리에 3초 전기장을 깔고, 같은 몬스터는 0.5초마다 한 번만 맞는다
  [UnityTest]public IEnumerator MarkBurstLaysAnElectricFieldThatTicks()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(1,"yellow","ink3_yellow_05");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    for(int i=0;i<3;i++){f.Hit(f.Target,0);driver.Step(.02f);}
    f.Hit(f.Target,1);for(int i=0;i<10;i++)driver.Step(.02f);
    Assert.AreEqual(1,f.Arena.ReadMvpFields().Count(),"one field once the red mark pops");
    float before=f.Target.health;for(int i=0;i<50;i++)driver.Step(.02f);
    Assert.That(f.Target.health,Is.LessThan(before),"the field damages the target standing in it");
    for(int i=0;i<150;i++)driver.Step(.02f);
    Assert.AreEqual(0,f.Arena.ReadMvpFields().Count(),"the field ends after three seconds");
   }
  }
  // 범위 잉크는 몸 중심이 아니라 켜진 피격 영역의 가장 가까운 점까지 잰다. 피격 영역이 꺼져 있으면 몸 중심을 쓴다
  [UnityTest]public IEnumerator AreaInkMeasuresToTheNearestHurtBoxPoint()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   var t=f.Target;t.ConfigureHurtbox(new Vector2(1,6),new Vector2(0,3));var feet=t.Position;
   Assert.That(Vector2.Distance(t.Center,feet),Is.GreaterThan(.75f),"the body centre lies outside a feet-level field");
   Assert.IsTrue(Nearby(f,feet,.75f).Contains(t),"a feet-level field reaches the tall hurt box");
   Assert.IsTrue(Nearby(f,feet+new Vector2(1.2f,4),.75f).Contains(t),"0.7 beside the box edge, high up, is in reach");
   Assert.IsFalse(Nearby(f,feet+new Vector2(1.3f,4),.75f).Contains(t),"0.8 beside the box edge is out of reach");
   var sweep=new Bounds(feet+new Vector2(0,5.5f),new Vector3(.4f,.4f,0));
   Assert.IsTrue(t.HurtOverlaps(sweep),"a box sweep near the top of the hurt box overlaps it");
   t.hurtCollider.enabled=false;
   Assert.IsFalse(Nearby(f,feet,.75f).Contains(t),"a disabled hurt box falls back to the body centre");
   Assert.IsTrue(Nearby(f,feet+Vector2.up*.5f,.75f).Contains(t),"the body-centre fallback still reaches it");
   Assert.IsFalse(t.HurtOverlaps(sweep),"the fallback body box at the centre does not reach the top");
   yield return null;
  }
  static System.Collections.Generic.List<TestEnemy> Nearby(Fixture f,Vector2 at,float radius)
   =>(System.Collections.Generic.List<TestEnemy>)typeof(CombatArena).GetMethod("InkNearby",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Arena,new object[]{at,radius,null,0});
  // 피해 무작위 폭을 끄고 본문을 실행한다. 비율 비교를 정확히 하려는 것이다
  // 플레이 모드 진입의 도메인 재로드가 람다가 잡은 지역 변수를 지우므로 using 범위로 쓴다
  sealed class NoVarianceScope:IDisposable{readonly float min,max;public NoVarianceScope(){var v=CombatDamageVariance.Settings;min=v.minimum;max=v.maximum;v.minimum=v.maximum=1;}public void Dispose(){var v=CombatDamageVariance.Settings;v.minimum=min;v.maximum=max;}}
  static IDisposable NoVariance()=>new NoVarianceScope();
  // 농묵: 공격 피해 +25%, 받는 피해 +15%. 밑줄 긋기: 특수가 맞힌 몬스터에게 같은 무기의 기본 공격 피해 +25%
  [UnityTest]public IEnumerator RiskyInkAndUnderlineRaiseDamage()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.Equip(0,"red","ink3_red_09","ink3_red_10");f.Player.guardUntil=0;
   using(var driver=f.Arena.AcquireManualSimulation())using(NoVariance())
   {
    float plain=f.Loss(f.Target,1);float risky=f.Loss(f.Target,0);
    Assert.That(risky/plain,Is.EqualTo(1.3f).Within(.01f),"농묵 raises attack damage by 30%");
    float hp=f.Player.health;f.Player.Receive(10,f.Target,-1,false,null);driver.Step(.02f);
    Assert.That(hp-f.Player.health,Is.EqualTo(11.5f).Within(.01f),"농묵 raises damage taken by 15%");
    f.Loss(f.Target,0,false);driver.Step(.02f);
    float underlined=f.Loss(f.Target,0);
    // 대상별 증가(밑줄)는 결정타·집중 공격처럼 공격 전체 배율(농묵)과 곱한다
    Assert.That(underlined/plain,Is.EqualTo(1.3f*1.15f).Within(.01f),"the special underlines the target for this weapon's basic attacks");
    Assert.That(f.Loss(f.Target,1)/plain,Is.EqualTo(1f).Within(.01f),"the other weapon's basic attacks are not raised");
    for(int i=0;i<210;i++)driver.Step(.02f);
    Assert.That(f.Loss(f.Target,0)/plain,Is.EqualTo(1.3f).Within(.01f),"the underline lasts four seconds");
   }
  }
  // 내달린 획: 대시가 끝난 뒤 0.5초 안의 공격은 피해 +30%·준비 구간 −30%. 약점 찌르기: 속박·기절된 몬스터에게 원타격 피해 +25%
  [UnityTest]public IEnumerator DashFollowupAndWeakSpotRaiseDamage()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.EquipMixed(0,new[]{"yellow","purple","purple"},"ink3_yellow_11","ink3_purple_11");
   using(var driver=f.Arena.AcquireManualSimulation())using(NoVariance())
   {
    float plain=f.Loss(f.Target,1);
    Assert.That(f.Loss(f.Target,0)/plain,Is.EqualTo(1f).Within(.01f),"no bonus without a dash or control");
    f.Target.rootUntil=f.Arena.Now+5;
    Assert.That(f.Loss(f.Target,0)/plain,Is.EqualTo(1.15f).Within(.01f),"약점 찌르기 on a bound monster");
    f.Target.rootUntil=0;f.Target.stunUntil=0;
    f.Dash();for(int i=0;i<60&&(i<2||f.Player.Dashing);i++)driver.Step(.02f);
    Assert.IsFalse(f.Player.Dashing,"the dash ended");
    Assert.That(f.Loss(f.Target,0)/plain,Is.EqualTo(1.2f).Within(.01f),"내달린 획 right after the dash");
    for(int i=0;i<30;i++)driver.Step(.02f);
    Assert.That(f.Loss(f.Target,0)/plain,Is.EqualTo(1f).Within(.01f),"the window closes after half a second");
   }
  }
  // 옮겨 쓰기: 바꿀 때 앞으로 미끄러지며 지나간 몬스터를 맞힌다. 앞당긴 획: 바꿔 든 무기의 첫 공격 사거리·범위 +45%
  [UnityTest]public IEnumerator SwitchSlideAndWidenedNextAttack()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   f.EquipMixed(0,new[]{"yellow","blue","blue"},"ink3_yellow_12","ink3_blue_11");
   using(var driver=f.Arena.AcquireManualSimulation())
   {
    float x=f.Player.Position.x,hp=f.Target.health;
    f.Switch();
    Assert.That(f.Player.Position.x-x,Is.GreaterThan(1.5f),"the switch slides the player forward");
    Assert.That(f.Target.health,Is.LessThan(hp),"the slide hits the monster it passes");
    var cast=f.Player.currentCast;Assert.IsNotNull(cast,"the incoming weapon attacks automatically");
    var action=typeof(CombatCast).GetField("InkAction",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cast);Assert.IsNotNull(action);
    Assert.That((float)action.GetType().GetField("RangeScale").GetValue(action),Is.EqualTo(1.45f).Within(.001f),"reach +45% on the first attack");
    Assert.That((float)action.GetType().GetField("MvpRadiusScale").GetValue(action),Is.EqualTo(1.45f).Within(.001f),"radius +45% on the first attack");
    Assert.That(f.Arena.ReadMvpBuff("reach"),Is.Zero,"the widening is spent by that attack");
   }
  }
  // 되갚는 잉크: 받은 피해 200%를 다음 원타격에 더한다. 갑옷 깁기: 처치 10회마다 보호막. 정리 정돈: 처치하면 특수 쿨타임 −0.5초.
  // 겹쳐 쓰기: 이 무기 특수의 최대 충전 +1. 넘어뜨리기: 대시로 부딪힌 몬스터를 기절시킨다
  [UnityTest]public IEnumerator RetaliationKillShieldCooldownChargesAndDashStun()
  {
   PrepareScene();yield return new EnterPlayMode();yield return null;var f=Make();
   int baseCapacity=f.Player.weapons[1].Cooldown.Capacity;
   f.EquipMixed(0,new[]{"green","blue","purple"},"ink3_green_09","ink3_blue_09","ink3_purple_10");
   f.EquipMixed(1,new[]{"green","blue","blue"},"ink3_green_10","ink3_blue_10");
   f.Player.guardUntil=0;
   using(var driver=f.Arena.AcquireManualSimulation())using(NoVariance())
   {
    driver.Step(.02f);
    Assert.AreEqual(baseCapacity+1,f.Player.weapons[0].Cooldown.Capacity,"겹쳐 쓰기 adds a charge to its own weapon");
    Assert.AreEqual(baseCapacity,f.Player.weapons[1].Cooldown.Capacity,"the other weapon keeps its charges");
    float plain=f.Loss(f.Target,0);
    f.Player.Receive(5,f.Target,-1,false,null);driver.Step(.02f);
    Assert.That(f.Loss(f.Target,0)-plain,Is.EqualTo(7.5f).Within(.01f),"1.5 times the health lost is added once");
    Assert.That(f.Loss(f.Target,0)-plain,Is.EqualTo(0f).Within(.01f),"the stored damage is spent");
    var w1=f.Player.weapons[1];w1.Cooldown.Spend(f.Arena.Now);float ready=w1.Cooldown.NextReady;
    var shieldInk=f.SlotInk(1,0);for(int i=0;i<9;i++)f.Growth.CountTrigger(shieldInk,"kill");
    f.Target.health=f.Target.maxHealth;f.Attack(f.Target,1,true,f.Target.maxHealth*2);driver.Step(.02f);
    Assert.IsTrue(f.Target.Dead);
    Assert.That(f.Arena.ReadInkShield(),Is.EqualTo(5).Within(.001f),"the tenth kill grants the shield");
    Assert.That(ready-w1.Cooldown.NextReady,Is.EqualTo(.4f).Within(.001f),"the kill shortens the killer's special cooldown");
    // 대시 파츠는 마지막으로 공격한 무기(무기 1)의 팔레트를 본다
    var other=f.Near(1.1f);Assert.IsNotNull(other);f.Loss(other,0,true,1);other.health=other.maxHealth;other.Position=f.Player.Position+Vector2.right*1.1f;Physics2D.SyncTransforms();f.Player.facing=1;
    f.Dash();for(int i=0;i<30&&!(other.stunUntil>f.Arena.Now);i++)driver.Step(.02f);
    Assert.That(other.stunUntil,Is.GreaterThan(f.Arena.Now),"the dash stuns the monster it runs into");
   }
  }
 }
}
