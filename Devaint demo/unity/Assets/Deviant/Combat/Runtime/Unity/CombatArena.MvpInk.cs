using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Run;
using Deviant.Equipment.Contracts;
using Deviant.Combat.Contracts;
using UnityEngine;
namespace Deviant.Combat
{
 // 색상 잉크 파츠 53종(docs/development/data/catalogs/ink-color-parts.md)의 런 성장 경로. 수치는 WeaponGrowthMvp.json 의 슬롯 레벨 값을 쓴다.
 public partial class CombatArena
 {
  public WeaponGrowth MvpGrowth {get;set;}
  // True in runs whose weapons and inks come from sketch-weapon choices: the old palette, stamp and ink-stall
  // screens step aside for the sketch bag and shelf (docs/design/equipment/sketch-weapons.md).
  public bool SketchEquipment {get;set;}
  // 보여 주기 전용 알림: 잉크 파츠가 발동하거나 표식이 터진 순간을 이펙트 쪽에 알린다. 규칙·수치는 바꾸지 않는다
  public event Action<MvpInkCue> MvpInkCued;
  public float ReadMvpBuff(string stat){mvpBuffs.RemoveAll(b=>b.Until<=Now);return mvpBuffs.Where(b=>b.Stat==stat).Sum(b=>b.Amount);}
  void CueMvp(MvpInkCue cue){if(MvpInkCued==null)return;try{MvpInkCued(cue);}catch(Exception e){Debug.LogException(e);}}
  // 지속 피해·전기장·반격처럼 적중 트리거와 표식을 만들지 않는 잉크 피해의 행동 ID. 처치 트리거는 그대로 발동한다.
  const string MvpEffectAction="mvp_effect";
  sealed class MvpBuff {public Guid Weapon,Instance;public string Stat;public float Amount,Until;public bool Once;}
  sealed class MvpField {public InkHit Cause;public InkPartMount Mount;public Vector2 Position;public float Radius,Until,Damage;public string PartId;}
  readonly List<MvpBuff> mvpBuffs=new List<MvpBuff>();readonly Dictionary<string,float> mvpReady=new Dictionary<string,float>();readonly Dictionary<Guid,InkPartMount> mvpMounts=new Dictionary<Guid,InkPartMount>();
  readonly Dictionary<Guid,float> mvpMoved=new Dictionary<Guid,float>();
  readonly List<MvpField> mvpFields=new List<MvpField>();readonly HashSet<long> mvpDashHits=new HashSet<long>();
  Guid mvpActive;int mvpLife=-1;bool mvpWasDashing;float mvpLastMoveAt=float.NegativeInfinity,mvpDashEndedAt=float.NegativeInfinity;
  long mvpFocusTarget=-1,mvpFocusPrevious=-1;int mvpFocusStacks,mvpFocusPreviousStacks;float mvpFocusAt;Guid mvpFocusExecution,mvpFocusSwitchExecution;
  readonly HashSet<Guid> mvpSplashEffects=new HashSet<Guid>();
  // 무지개 표식: 쌓인 순서대로 빨·주·노·초·파·남·보 색이 붙고, 터트릴 때는 역순으로 하나씩 터진다.
  public static readonly Color[] MvpMarkColors={new Color32(229,57,53,255),new Color32(251,140,0,255),new Color32(253,216,53,255),new Color32(67,160,71,255),new Color32(30,136,229,255),new Color32(57,73,171,255),new Color32(142,36,170,255)};
  sealed class MvpMarkChain {public InkHit Cause;public GrowthSlot[] Slots;public InkPartMount Mount;public int Next,Total;public float At,Damage;public bool First=true, SpreadBoosted;}
  readonly List<MvpMarkChain> mvpChains=new List<MvpMarkChain>();
  // 질주 충전의 에너지 기. 슬롯 인스턴스마다 쌓이고 마지막 이동 뒤 resetSeconds가 지나면 사라진다.
  sealed class MvpCharge {public int Stacks;public float Reset;}
  readonly Dictionary<Guid,MvpCharge> mvpCharges=new Dictionary<Guid,MvpCharge>();
  void ResetMvpTransient(){mvpChains.Clear();mvpCharges.Clear();mvpBuffs.Clear();mvpReady.Clear();mvpMounts.Clear();mvpMoved.Clear();mvpFields.Clear();mvpDashHits.Clear();mvpActive=Guid.Empty;mvpWasDashing=false;mvpLastMoveAt=mvpDashEndedAt=float.NegativeInfinity;mvpFocusTarget=mvpFocusPrevious=-1;mvpFocusStacks=mvpFocusPreviousStacks=0;mvpFocusExecution=mvpFocusSwitchExecution=Guid.Empty;mvpSplashEffects.Clear();mvpLife=Player?Player.LifeVersion:-1;}
  void CheckMvpLife(){if(Player&&mvpLife!=Player.LifeVersion)ResetMvpTransient();}
  static long MvpLifeKey(CombatActor actor)=>((long)actor.ActorId<<32)|(uint)actor.LifeVersion;
  float MvpBuffValue(Guid weapon,string stat){mvpBuffs.RemoveAll(b=>b.Until<=Now);return mvpBuffs.Where(b=>b.Weapon==weapon&&b.Stat==stat).Sum(b=>b.Amount);}
  InkPartMount MvpMount(GrowthSlot slot){if(!mvpMounts.TryGetValue(slot.InstanceId,out var m))mvpMounts[slot.InstanceId]=m=new InkPartMount(slot.Part.id,slot.Part.duplicate);return m;}
  float MvpReference(WeaponState weapon){var basics=catalog.forms[weapon.FormIndex].basics;if(MvpGrowth.Data.basicReferencePolicy!="mean_basic_combo_hit")throw new InvalidOperationException("기본 공격 기준값 정책 미정");return basics.Average(b=>b.damage)*MvpGrowth.AttackMultiplier;}
  Guid MvpActiveWeapon=>mvpActive!=Guid.Empty?mvpActive:Player?Player.weapons[0].InstanceId:Guid.Empty;
  // 상시 효과는 마지막으로 실제 공격을 시작한 무기의 팔레트만 본다.
  static float MvpSum(GrowthSlot[] slots,string kind,Func<GrowthValues,float> value)=>slots.Where(s=>s.Part!=null&&s.Part.trigger=="passive"&&s.Part.kind==kind).Sum(s=>value(s.Part.levels[s.Level]));
  static GrowthSlot MvpFirst(GrowthSlot[] slots,string kind)=>slots.FirstOrDefault(s=>s.Part!=null&&s.Part.kind==kind);
  // 표식은 어느 한 무기에라도 "표식 터트릴 시"·"색 표식이 터질 때" 파츠나 색 번짐이 있을 때만 원타격에 묻는다.
  bool MvpMarksEnabled()=>Player&&Player.weapons.Any(w=>MvpGrowth.SnapshotSlots(w.InstanceId).Any(s=>s.Part!=null&&(s.Part.trigger=="mark_burst"||s.Part.trigger=="mark_pop"||s.Part.kind=="mark_spread")));
  // JsonUtility and float health arithmetic can differ by one ULP at the inclusive threshold.
  bool MvpAtHealthThreshold(float threshold)
  {float required=Player.maxHealth*threshold;return Player.health>=required||Mathf.Approximately(Player.health,required);}
  void BeginMvpAction(InkAction action)
  {
   if(Sketch!=null||MvpGrowth==null||action.Weapon==null)return;CheckMvpLife();var incoming=action.Weapon;var outgoing=MvpActiveWeapon;
   if(outgoing!=incoming.InstanceId)SwitchMvpWeapon(InkWeapon(outgoing),incoming);
   mvpActive=incoming.InstanceId;var id=incoming.InstanceId;
   var slots=action.MvpSlots=MvpGrowth.SnapshotSlots(id);action.MvpBasicReference=MvpReference(incoming);
   action.MvpPower=MvpBuffValue(id,"power");if(action.MvpPower>0)CueMvp(new MvpInkCue(null,null,"action","power_release",Player,Player.Center,Player.Position,Player.facing,0,0,null));
   float power=action.MvpPower+(action.Basic?Player.RecoveryCancelDamageBonus:0);
   foreach(var s in slots.Where(s=>s.Part!=null&&s.Part.kind=="kill_stack_attack_power")){var v=s.Part.levels[s.Level];power+=Mathf.Min(v.cap,MvpGrowth.KillStacks(s.InstanceId)*v.amountPerKill);}
   if(ReadInkShield()+Player.shield>0)power+=MvpSum(slots,"shielded_attack_power",v=>v.amount);
   foreach(var s in slots.Where(s=>s.Part!=null&&s.Part.kind=="high_health_attack_power"))if(MvpAtHealthThreshold(s.Part.levels[s.Level].threshold))action.MvpHighHealthPower+=s.Part.levels[s.Level].amount;
   if(Now-mvpLastMoveAt<=.2f||Player.Dashing)power+=MvpBuffValue(id,"moving_power");
   // 농묵: 공격 피해를 올리고 받는 피해도 올린다(받는 피해는 MvpIncomingDamageScale).
   power+=MvpSum(slots,"risky_attack_power",v=>v.amount);
   // 내달린 획: 대시 중이거나 대시가 끝난 뒤 duration 안에 시작한 공격의 피해를 올리고 준비 구간을 줄인다.
   foreach(var s in slots.Where(s=>s.Part!=null&&s.Part.kind=="dash_followup"))
   {var v=s.Part.levels[s.Level];if(MvpDashedWithin(v.duration)){power+=v.amount;action.MvpWindupReduction=Mathf.Max(action.MvpWindupReduction,v.windupReduction);}}
   action.MvpBasePower=power;
   action.DamageScale*=MvpGrowth.AttackMultiplier*(1+power+action.MvpHighHealthPower);
   action.MvpAllGeometryScale=1+MvpSum(slots,"all_geometry",v=>v.amount);
   action.RangeScale*=1+MvpBuffValue(id,"reach")+MvpSum(slots,"attack_reach",v=>v.amount);
   action.MvpRadiusScale=1+MvpBuffValue(id,"radius")+MvpSum(slots,"area_radius",v=>v.amount);
   action.MvpAttackSpeed=MvpBuffValue(id,"attack_speed")+MvpSum(slots,"attack_and_move_speed",v=>v.attackSpeed);
   mvpBuffs.RemoveAll(b=>b.Weapon==id&&b.Once);
  }
  internal float MvpMoveBonus(WeaponState weapon)
  {CheckMvpLife();var slots=MvpGrowth.SnapshotSlots(weapon.InstanceId);return Mathf.Min(MvpGrowth.Data.movementSpeedBonusCap,MvpBuffValue(weapon.InstanceId,"move")+slots.Where(s=>s.Part!=null&&s.Part.trigger=="passive").Sum(s=>s.Part.levels[s.Level].moveSpeed));}
  // 집중 공격: 같은 몬스터를 연달아 때린 수만큼 원타격 피해를 올린다. 중첩은 맞히기 전에 적용하고 맞힌 뒤 센다.
  // 결정타: 맞히기 직전 체력이 기준 이하인 몬스터에게 원타격 피해를 올린다. 두 증가는 더한다.
  internal float MvpTargetDamage(CombatHitOrigin origin,CombatActor target,float damage)
  {
   if(Sketch!=null||MvpGrowth==null||!target||origin.Kind==CombatHitKind.InkPart||origin.Stamp.InkPalette==null||!inkStarts.TryGetValue(origin.Stamp.InkPalette,out var action)||action.MvpSlots==null)return damage;
   float scale=1+MvpSum(action.MvpSlots,"all_geometry",v=>v.ratio);var execute=MvpFirst(action.MvpSlots,"execute_damage");
   if(execute!=null){var e=execute.Part.levels[execute.Level];if(target.health<=target.maxHealth*e.threshold)scale+=e.amount;}
   var focus=MvpFirst(action.MvpSlots,"focus_stack_damage");
   if(focus!=null&&mvpFocusTarget==MvpLifeKey(target)){var v=focus.Part.levels[focus.Level];if(Now-mvpFocusAt<=v.resetSeconds)scale+=Mathf.Min(mvpFocusStacks,v.maxStacks)*v.amountPerStack;}
   // 밑줄 긋기: 이 무기의 특수가 밑줄 그은 몬스터에게 같은 무기의 기본 공격이 주는 피해를 올린다.
   var state=TargetInk(target);
   if(action.Basic&&action.Weapon!=null&&state.MvpUnderlineUntil>Now&&state.MvpUnderlineWeapon==action.Weapon.InstanceId)scale+=state.MvpUnderlineAmount;
   // 약점 찌르기: 속박·기절된 몬스터에게 주는 원타격 피해를 올린다.
   if(target.rootUntil>Now||target.stunUntil>Now)scale+=MvpSum(action.MvpSlots,"controlled_damage",v=>v.amount);
   float highHealth=action.MvpSlots.Where(s=>s.Part!=null&&s.Part.kind=="high_health_attack_power"&&MvpAtHealthThreshold(s.Part.levels[s.Level].threshold)).Sum(s=>s.Part.levels[s.Level].amount);
   return damage*scale*(1+action.MvpBasePower+highHealth)/(1+action.MvpBasePower+action.MvpHighHealthPower);
  }
  // 대시 중이거나 대시가 끝난 지 seconds 안인가. 이번 단계에서 막 끝나 StepMvp가 아직 보지 못한 대시도 지금 끝난 것으로 본다.
  bool MvpDashedWithin(float seconds){if(Player.Dashing||mvpWasDashing)return true;return Now-mvpDashEndedAt<=seconds+.00001f;}
  // 되갚는 잉크: 저장한 피해를 이 무기의 다음 원타격이 처음 맞힌 몬스터에게 한 번 더한다. 공격 배율을 다시 곱하지 않는다.
  internal float MvpRetaliation(CombatHitOrigin origin,CombatActor target)
  {
   if(Sketch!=null||MvpGrowth==null||!target||target.isDecoy||origin.Kind==CombatHitKind.InkPart||origin.ActionId==MvpEffectAction||origin.Stamp.InkPalette==null||!inkStarts.TryGetValue(origin.Stamp.InkPalette,out var action)||action.MvpSlots==null)return 0;
   var buff=mvpBuffs.FirstOrDefault(b=>b.Weapon==origin.Stamp.WeaponId&&b.Stat=="retaliate"&&b.Until>Now);if(buff==null)return 0;
   mvpBuffs.Remove(buff);CueMvp(new MvpInkCue(null,"green","hit","retaliation_release",target,target.Center,target.Position,origin.SourceFacing,0,0,null));return buff.Amount;
  }
  // 농묵: 마지막으로 공격한 무기의 팔레트가 받는 피해를 올린다. 보호막에 막히기 전 피해에 곱한다.
  internal float MvpIncomingDamageScale()
  {if(Sketch!=null||MvpGrowth==null||!Player)return 1;CheckMvpLife();return 1+MvpSum(MvpGrowth.SnapshotSlots(MvpActiveWeapon),"risky_attack_power",v=>v.damageTaken);}
  // 겹쳐 쓰기: 이 잉크를 낀 무기 자신의 특수 최대 충전 수를 늘린다. 충전은 무기 자원이라 마지막 공격 무기와 관계없이 유지하며,
  // 바뀔 때는 남은 회복 부담 비율을 유지한다(Reconfigure).
  void SyncMvpSkillCharges()
  {
   if(Sketch!=null)return;
   foreach(var w in Player.weapons)
   {
    if(w==null||w.FormIndex<0||w.FormIndex>=catalog.forms.Length)continue;var specials=catalog.forms[w.FormIndex].specials;
    if(specials==null||w.SpecialIndex<0||w.SpecialIndex>=specials.Length||specials[w.SpecialIndex]==null)continue;
    int extra=(int)MvpGrowth.SnapshotSlots(w.InstanceId).Where(s=>s.Part!=null&&s.Part.kind=="extra_skill_charge").Sum(s=>s.Part.levels[s.Level].count);
    int want=Mathf.Max(1,specials[w.SpecialIndex].capacity)+Mathf.Max(0,extra);
    float reduction=MvpSum(MvpGrowth.SnapshotSlots(w.InstanceId),"extra_skill_charge",v=>v.rechargeReduction);
    float period=w.CooldownPeriod(specials[w.SpecialIndex].cooldown)*(1-Mathf.Clamp(reduction,0,.8f))*(1-AugmentSkillCooldownReduction);
    if(w.Cooldown.Capacity!=want || !Mathf.Approximately(w.Cooldown.Period,period))w.Cooldown.Reconfigure(want,period,Now);
   }
  }
  // 옮겨 쓰기: 바라보는 쪽으로 최대 distance만큼 순간 이동(벽 앞에서 멈춤)하고 지나간 자리의 몬스터를 한 번씩 맞힌다.
  void MvpSlide(InkHit cause,InkPartMount mount,GrowthValues v)
  {
   var weapon=InkWeapon(cause.Hit.Origin.Stamp.WeaponId);if(weapon==null||!Player||Player.Dead||!(v.distance>0))return;
   Vector2 from=Player.Position;int facing=Player.facing<0?-1:1;Player.TeleportToward(from+Vector2.right*facing*v.distance);Vector2 to=Player.Position;
   CueMvp(new MvpInkCue(mount.PartId,"yellow","switch","switch_slide",Player,Player.Center,from,facing,0,0,null));
   var sweep=new Bounds(new Vector3((from.x+to.x)/2,Player.Center.y),new Vector3(Mathf.Abs(to.x-from.x)+Player.size.x,Player.size.y));float damage=MvpReference(weapon)*v.ratio;
   foreach(var e in enemies.ToArray())
    if(e&&!e.Dead&&!e.isDecoy&&e.HurtOverlaps(sweep))DealInk(InkSyntheticHit(InkOrigin(weapon,weapon.InkPalette.Read(),"mvp_slide",false),e),mount,damage,1);
  }
  // 밑줄 긋기는 이 무기 특수 스킬의 원타격에서만 긋는다.
  bool MvpPrimarySpecial(InkHit hit)=>hit.Hit.Origin.Kind!=CombatHitKind.InkPart&&hit.Hit.Origin.ActionId!=MvpEffectAction&&hit.Hit.Origin.Stamp.InkPalette!=null&&inkStarts.TryGetValue(hit.Hit.Origin.Stamp.InkPalette,out var a)&&!a.Basic;
  // 한 공격이 여러 마리를 맞혀도 집중 대상은 바뀌지 않는다. 다음 공격이 집중 대상 없이 다른 몬스터만 맞혔을 때 옮겨 간다.
  void CountMvpFocus(CombatActor target,GrowthSlot[] slots,Guid execution)
  {
   var focus=MvpFirst(slots,"focus_stack_damage");float reset=focus!=null?focus.Part.levels[focus.Level].resetSeconds:2;long key=MvpLifeKey(target);
   if(Now-mvpFocusAt>reset){mvpFocusTarget=-1;mvpFocusStacks=0;}
   if(key!=mvpFocusTarget)
   {
    if(execution==mvpFocusSwitchExecution&&key==mvpFocusPrevious){mvpFocusTarget=mvpFocusPrevious;mvpFocusStacks=mvpFocusPreviousStacks;mvpFocusPrevious=-1;}
    else if(execution==mvpFocusExecution)return;
    else{mvpFocusPrevious=mvpFocusTarget;mvpFocusPreviousStacks=mvpFocusStacks;mvpFocusSwitchExecution=execution;mvpFocusTarget=key;mvpFocusStacks=0;}
   }
   mvpFocusExecution=execution;mvpFocusStacks=Mathf.Min(mvpFocusStacks+1,99);mvpFocusAt=Now;
  }
  internal void SwitchMvpWeapon(WeaponState outgoing,WeaponState incoming)
  {if(Sketch!=null||MvpGrowth==null||outgoing==null||incoming==null||outgoing==incoming)return;CheckMvpLife();var cause=InkSyntheticHit(InkOrigin(outgoing,outgoing.InkPalette.Read(),"mvp_switch",false),Player);MvpTrigger("switch",cause,incoming.InstanceId);FlushInkHits();mvpActive=incoming.InstanceId;}
  // 휘몰아치기: 이 잉크를 낀 무기의 공격이 첫 타격·효과를 낸 뒤 다른 무기 공격으로 이어지면 그 첫 공격의 준비 구간을 줄인다. 쿨타임과 무관하다.
  internal float MvpSwitchWindupReduction(WeaponState outgoing)
  {if(Sketch!=null||MvpGrowth==null||outgoing==null)return 0;return MvpGrowth.SnapshotSlots(outgoing.InstanceId).Where(s=>s.Part!=null&&s.Part.trigger=="switch").Select(s=>s.Part.levels[s.Level].switchWindupReduction).DefaultIfEmpty(0).Max();}
  internal float MvpGeometry(CombatHitOrigin origin,bool radius)
  {if(Sketch!=null||MvpGrowth==null||origin.Stamp.InkPalette==null||!inkStarts.TryGetValue(origin.Stamp.InkPalette,out var action))return 1;return (radius?action.MvpRadiusScale:action.RangeScale)*action.MvpAllGeometryScale;}
  float MvpHitReference(InkHit hit)=>hit.Hit.Origin.Stamp.InkPalette!=null&&inkStarts.TryGetValue(hit.Hit.Origin.Stamp.InkPalette,out var action)&&action.MvpSlots!=null?action.MvpBasicReference:MvpReference(InkWeapon(hit.Hit.Origin.Stamp.WeaponId));
  GrowthSlot[] MvpSlotsFor(InkHit hit,string trigger)=>trigger!="switch"&&hit.Hit.Origin.Stamp.InkPalette!=null&&inkStarts.TryGetValue(hit.Hit.Origin.Stamp.InkPalette,out var snap)&&snap.MvpSlots!=null?snap.MvpSlots:MvpGrowth.SnapshotSlots(hit.Hit.Origin.Stamp.WeaponId);
  // 적중 트리거·표식과 무관한 잉크 피해. 발동한 무기를 기록해 처치 트리거가 그 무기의 팔레트로 돌아간다.
  void DealMvpEffect(InkHit cause,CombatActor target,InkPartMount mount,float damage)
  {
   var weapon=InkWeapon(cause.Hit.Origin.Stamp.WeaponId);if(weapon==null||!target||target.Dead)return;
   DealInk(InkSyntheticHit(InkOrigin(weapon,weapon.InkPalette.Read(),MvpEffectAction,false),target),mount,damage,MvpGrowth.Data.chainDepth);
  }
  // slots를 주면 그 팔레트로 발동한다(표식 연쇄는 터트린 순간의 팔레트를 유지한다). markIndex는 "색 표식이 터질 때"의 순번이다.
  void MvpTrigger(string trigger,InkHit hit,Guid incoming=default,bool firstHit=false,GrowthSlot[] slots=null,int markIndex=0)
  {
   var weaponId=hit.Hit.Origin.Stamp.WeaponId;slots=slots??MvpSlotsFor(hit,trigger);
   // 독폭탄은 같은 타격에서 독을 새로 거는 파츠보다 먼저 터트린다. 슬롯 순서로 결과가 바뀌지 않는다.
   foreach(var slot in slots.OrderBy(s=>s.Part!=null&&s.Part.kind=="poison_burst"?0:1))
   {
    var part=slot.Part;if(part==null||part.trigger!=trigger||part.condition=="first_hit_per_life"&&!firstHit||part.condition=="special"&&!MvpPrimarySpecial(hit))continue;
    if(trigger=="mark_pop"&&part.markIndex!=markIndex)continue;
    // 덧칠은 자기 추가타를 다시 만들지 않는다. 다른 파츠의 추가타에는 반응한다.
    if(part.kind=="extra_hit"&&hit.Hit.Origin.EffectId==MvpMount(slot).InstanceId)continue;
    // 독이 없는 몬스터를 맞히면 독폭탄은 쿨타임을 쓰지 않는다.
    if(part.kind=="poison_burst"&&ReadMvpPoison(hit.Target)==0)continue;
    // N회마다 세는 수는 런 전체에서 이어진다(WeaponGrowth 소유). 플레이어 리셋으로 지우지 않는다.
    if(part.every>1&&MvpGrowth.CountTrigger(slot.InstanceId,trigger)%part.every!=0)continue;
    string key=slot.InstanceId.ToString()+(part.targetCooldown?":"+hit.Target.ActorId+":"+hit.TargetLife:"");
    if(mvpReady.TryGetValue(key,out float ready)&&Now+.00001f<ready)continue;
    float cooldown=part.eliteCooldown>0&&hit.Target&&(hit.Target.isBoss||hit.Target.isElite)?part.eliteCooldown:part.cooldown;
    if(part.kind!="stun")mvpReady[key]=Now+cooldown;
    var v=part.levels[slot.Level];var mount=MvpMount(slot);Guid beneficiary=part.scope=="incoming_weapon"&&incoming!=Guid.Empty?incoming:weaponId;
    CueMvp(new MvpInkCue(part.id,part.color,trigger,part.kind,hit.Target,MvpCuePoint(hit),trigger=="kill"||trigger=="bind_end"||part.kind=="electric_field"?hit.Target.Position:Player.Position,hit.Hit.Origin.SourceFacing,v.duration,0,null));
    switch(part.kind)
    {
     case "extra_hit":if(hit.Hit.Origin.InkGeneration<MvpGrowth.Data.chainDepth)DealInk(hit,mount,hit.Hit.RequestedDamage*v.ratio,hit.Hit.Origin.InkGeneration+1);break;
     case "burst_extra_hit":if(hit.Hit.Origin.InkGeneration<MvpGrowth.Data.chainDepth)DealInk(hit,mount,MvpHitReference(hit)*v.ratio,hit.Hit.Origin.InkGeneration+1);break;
     case "dash_hit":DealInk(hit,mount,MvpHitReference(hit)*v.ratio,1);break;
     // 지속·장판·추가타로 처치해도 터진다. 이 파츠 자신의 튄 잉크로 처치한 경우만 다시 터지지 않는다.
     case "kill_area_hit":if(!mvpSplashEffects.Contains(hit.Hit.Origin.EffectId)){mvpSplashEffects.Add(mount.InstanceId);foreach(var e in InkNearby(hit.Target.Center,v.radius,hit.Target))DealInk(Retarget(hit,e),mount,MvpHitReference(hit)*v.ratio,MvpGrowth.Data.chainDepth);}break;
     case "area_hit":float blast=MvpHitReference(hit)*v.ratio;foreach(var e in InkNearby(hit.Target.Center,v.radius))DealMvpEffect(hit,e,mount,blast);break;
     case "next_attack_power":MvpAddBuff(slot,beneficiary,"power",v.amount,0,true);break;
     case "kill_stack_attack_power":MvpGrowth.AddKillStack(slot.InstanceId);break;
     case "attack_and_move_speed":MvpAddBuff(slot,beneficiary,"attack_speed",v.attackSpeed,v.duration,false);MvpAddBuff(slot,beneficiary,"move",v.moveSpeed,v.duration,false);break;
     case "moving_attack_power":MvpAddBuff(slot,beneficiary,"move",v.moveSpeed,v.duration,false);MvpAddBuff(slot,beneficiary,"moving_power",v.amount,v.duration,false);break;
     case "area_radius":MvpAddBuff(slot,beneficiary,"radius",v.amount,v.duration,false);break;
     case "electric_field":SpawnMvpField(hit,mount,part.id,hit.Target.Position,v);break;
     case "heal":HealInk(v.amount);break;
     case "shield":AddInkShield(mount,v.amount,v.duration,MvpGrowth.Data.shieldCap);break;
     case "homing_counter":DealMvpEffect(hit,hit.Target,mount,MvpHitReference(hit)*v.ratio);break;
     case "cooldown_reduce":
      if(part.scope=="both_weapons")foreach(var w in Player.weapons)w.Cooldown.Refund(v.seconds,Now);else InkWeapon(beneficiary)?.Cooldown.Refund(v.seconds,Now);break;
     case "bind":if(MvpControl(hit,mount,v.duration,false))DealMvpEffect(hit,hit.Target,mount,MvpHitReference(hit)*v.ratio);break;
     case "stun":if(MvpControl(hit,mount,v.duration,true))mvpReady[key]=Now+cooldown;break;
     case "poison":AddMvpPoison(hit,mount,v);break;
     // 끊어 치기: 이 잉크를 낀 무기의 기본 공격 원타격이 맞으면 그 공격의 회복 구간을 다른 무기 기본 공격으로 끊을 수 있다.
     case "hit_recovery_cancel":var cast=Player.currentCast;if(hit.Hit.Origin.Kind!=CombatHitKind.InkPart&&cast&&cast.isBasic&&cast.ExecutionId==hit.Hit.Origin.ExecutionId){cast.HitCancelUnlocked=true;cast.HitCancelDamageBonus=Mathf.Max(cast.HitCancelDamageBonus,v.amount);}break;
     case "cc_count_hit":int states=MvpControlCount(hit.Target);if(states>0)DealMvpEffect(hit,hit.Target,mount,MvpHitReference(hit)*v.ratioPerState*states);break;
     case "chain_lightning":
      {float bolt=MvpHitReference(hit)*v.ratio;foreach(var e in InkNearby(hit.Target.Center,v.radius,hit.Target,Mathf.Max(1,(int)v.count))){CueMvp(new MvpInkCue(part.id,part.color,trigger,"chain_lightning_hit",e,e.Center,e.Position,hit.Hit.Origin.SourceFacing,0,0,null));DealMvpEffect(hit,e,mount,bolt);}break;}
     // 단단한 껍질: 시간이 지나도 사라지지 않고 다음 피격 한 번에 남은 양과 관계없이 사라지는 보호막
     case "hit_guard_shield":AddInkShield(mount,v.amount,float.PositiveInfinity,MvpGrowth.Data.shieldCap,true);break;
     // 독폭탄: 남은 틱 수 × 틱 피해 × 중첩 수를 한 번에 주고 독을 지운다.
     case "poison_burst":
      {var st=TargetInk(hit.Target);int ticks=st.MvpPoisonInterval>0&&st.MvpPoisonNext<st.MvpPoisonUntil?Mathf.FloorToInt((st.MvpPoisonUntil-st.MvpPoisonNext-.0001f)/st.MvpPoisonInterval)+1:0;
       float total=ticks*st.MvpPoisonDamage*st.MvpPoisonStacks*v.ratio;st.MvpPoisonStacks=0;st.MvpPoisonUntil=0;if(total>0)DealMvpEffect(hit,hit.Target,mount,total);break;}
     case "underline_mark":{var st=TargetInk(hit.Target);st.MvpUnderlineWeapon=weaponId;st.MvpUnderlineUntil=Now+v.duration;st.MvpUnderlineAmount=v.amount;break;}
     case "switch_slide":MvpSlide(hit,mount,v);break;
     // 되갚는 잉크: 받은 체력 피해(반격 원본의 요청 피해)의 ratio배를 이 무기의 다음 원타격에 저장한다. 다시 저장하면 새 값으로 바꾼다.
     case "stored_retaliation":MvpAddBuff(slot,weaponId,"retaliate",hit.Hit.RequestedDamage*v.ratio,v.duration,false);break;
     // 앞당긴 획: 바꿔 든 무기의 첫 공격 1회의 사거리·범위를 함께 늘린다.
     case "next_attack_geometry":MvpAddBuff(slot,beneficiary,"reach",v.amount,0,true);MvpAddBuff(slot,beneficiary,"radius",v.amount,0,true);break;
     case "focus_stack_damage":case "execute_damage":break; // MvpTargetDamage owns these primary-hit damage effects.
     case "mark_spread":case "mark_chain_spread":break; // HandleMvpHit and the mark chain own these.
     default:throw new InvalidOperationException($"MVP 지원하지 않는 발동 효과: {part.id} ({part.kind})");
    }
   }
  }
  void MvpAddBuff(GrowthSlot slot,Guid weapon,string stat,float amount,float duration,bool once){mvpBuffs.RemoveAll(b=>b.Instance==slot.InstanceId&&b.Stat==stat);mvpBuffs.Add(new MvpBuff{Instance=slot.InstanceId,Weapon=weapon,Stat=stat,Amount=amount,Until=once?float.PositiveInfinity:Now+duration,Once=once});}
  // 속박은 이동만, 기절은 시전 중인 공격까지 끊는다. 보스는 지속시간만 줄이고 같은 대상은 회복구간 동안 다시 걸지 않는다.
  bool MvpControl(InkHit hit,InkPartMount mount,float duration,bool stun)
  {
   var target=hit.Target;if(!target||target.Dead||!target.AllowsControlEffects)return false;var state=TargetInk(target);if(Now<(stun?state.MvpStunReady:state.MvpBindReady))return false;
   // 보스 내부 쿨타임(기절 12초·속박 8초) 중이면 부여 실패다. 잉크 회복 구간도 쓰지 않는다.
   if(stun?!target.TryAcceptBossStun():!target.TryAcceptBossRoot())return false;
   float end=Now+duration*(target.isBoss?MvpGrowth.Data.bossControlDurationAndDisplacementScale:1);
   if(stun){target.stunUntil=Mathf.Max(target.stunUntil,end);target.CancelAttack();}
   else{target.rootUntil=Mathf.Max(target.rootUntil,end);state.MvpBindUntil=end;state.MvpBindCause=hit;}
   // 속박과 기절은 회복 구간을 따로 센다. 한쪽이 걸렸다는 이유로 다른 파츠를 막지 않는다.
   if(stun)state.MvpStunReady=end+MvpGrowth.Data.hardControlRecoverySeconds;else state.MvpBindReady=end+MvpGrowth.Data.hardControlRecoverySeconds;return true;
  }
  int MvpControlCount(CombatActor target)
  {var state=TargetInk(target);return (target.rootUntil>Now?1:0)+(target.stunUntil>Now?1:0)+(state.MvpPoisonUntil>Now&&state.MvpPoisonStacks>0?1:0)+(InkSlow(target)>0?1:0);}
  // 독은 다시 맞히면 지속시간만 갱신하고, 슬롯 강화 단계가 허용하는 최대 중첩까지만 쌓는다.
  void AddMvpPoison(InkHit hit,InkPartMount mount,GrowthValues v)
  {
   if(!hit.Target||hit.Target.Dead)return;var state=TargetInk(hit.Target);bool active=state.MvpPoisonUntil>Now&&state.MvpPoisonStacks>0;
   // 독은 몬스터마다 하나다. 걸 때마다 이 파츠의 최대 중첩까지 1중첩을 더하고, 다른 파츠가 더 쌓은 중첩은 줄이지 않는다.
   state.MvpPoisonStacks=active?Mathf.Max(state.MvpPoisonStacks,Mathf.Min(Mathf.Max(1,(int)v.maxStacks),state.MvpPoisonStacks+1)):1;
   if(!active)state.MvpPoisonNext=Now+v.interval;
   state.MvpPoisonUntil=Now+v.duration;state.MvpPoisonInterval=v.interval;
   float damage=MvpHitReference(hit)*v.ratio;
   if(!active || damage>=state.MvpPoisonDamage){state.MvpPoisonDamage=damage;state.MvpPoisonCause=hit;state.MvpPoisonMount=mount;}
  }
  void SpawnMvpField(InkHit cause,InkPartMount mount,string partId,Vector2 feet,GrowthValues v)
  {mvpFields.Add(new MvpField{Cause=cause,Mount=mount,PartId=partId,Position=feet,Radius=v.radius,Until=Now+v.duration,Damage=MvpHitReference(cause)*v.ratio});}
  public int ReadMvpPoison(CombatActor target){if(!target)return 0;if(Sketch!=null)return 0;var s=TargetInk(target);return s.MvpPoisonUntil>Now?s.MvpPoisonStacks:0;}
  public IEnumerable<(Vector2 feet,float radius,float until,string partId)> ReadMvpFields(){foreach(var f in mvpFields)yield return (f.Position,f.Radius,f.Until,f.PartId);}
  // 이동한 실제 가로 거리로 전기 발자국을 남긴다. 신난다의 "이동 중" 판정도 여기서 갱신한다.
  internal void MvpMoved(float distance)
  {
   if(distance<=0)return;CheckMvpLife();mvpLastMoveAt=Now;var weapon=InkWeapon(MvpActiveWeapon);if(weapon==null)return;
   foreach(var slot in MvpGrowth.SnapshotSlots(weapon.InstanceId))
   {
    if(slot.Part==null||slot.Part.trigger!="move")continue;var v=slot.Part.levels[slot.Level];
    float total=(mvpMoved.TryGetValue(slot.InstanceId,out var d)?d:0)+distance;
    // 질주 충전: 이동 거리마다 몸에 에너지 기를 하나씩 쌓는다. 최대에 이르면 남는 거리를 버린다.
    if(slot.Part.kind=="move_charge")
    {
     if(!mvpCharges.TryGetValue(slot.InstanceId,out var charge))mvpCharges[slot.InstanceId]=charge=new MvpCharge();charge.Reset=v.resetSeconds;int cap=Mathf.Max(1,(int)v.maxStacks);
     while(v.distance>0&&total>=v.distance&&charge.Stacks<cap){total-=v.distance;charge.Stacks++;CueMvp(new MvpInkCue(slot.Part.id,slot.Part.color,"move","move_charge",Player,Player.Center,Player.Position,Player.facing,0,charge.Stacks,null));}
     mvpMoved[slot.InstanceId]=charge.Stacks>=cap?0:total;continue;
    }
    var cause=InkSyntheticHit(InkOrigin(weapon,weapon.InkPalette.Read(),"mvp_move",false),Player);
    while(v.distance>0&&total>=v.distance){total-=v.distance;CueMvp(new MvpInkCue(slot.Part.id,slot.Part.color,"move",slot.Part.kind,Player,Player.Position,Player.Position,Player.facing,v.duration,0,null));SpawnMvpField(cause,MvpMount(slot),slot.Part.id,Player.Position,v);}
    mvpMoved[slot.InstanceId]=total;
   }
  }
  void StepMvp()
  {
   if(Sketch!=null||MvpGrowth==null||!Player)return;CheckMvpLife();StepMaintenanceAugments();SyncMvpSkillCharges();
   foreach(var chain in mvpChains.ToArray())if(StepMvpMarkChain(chain))mvpChains.Remove(chain);
   foreach(var pair in mvpCharges.ToArray())if(pair.Value.Stacks>0&&Now-mvpLastMoveAt>pair.Value.Reset){pair.Value.Stacks=0;mvpMoved.Remove(pair.Key);}
   for(int i=mvpFields.Count-1;i>=0;i--)
   {
    var f=mvpFields[i];if(Now>=f.Until||!f.Cause.Source||f.Cause.Source.LifeVersion!=f.Cause.SourceLife){mvpFields.RemoveAt(i);continue;}
    foreach(var e in InkNearby(f.Position,f.Radius)){var state=TargetInk(e);if(Now+.00001f<state.MvpFieldReadyAt)continue;state.MvpFieldReadyAt=Now+.5f;DealMvpEffect(f.Cause,e,f.Mount,f.Damage);}
   }
   var active=MvpGrowth.SnapshotSlots(MvpActiveWeapon);var concussion=MvpFirst(active,"stunned_dot");
   foreach(var e in enemies.ToArray())
   {
    if(!e||e.Dead||e.isDecoy)continue;var state=TargetInk(e);
    if(state.MvpPoisonStacks>0&&state.MvpPoisonUntil>Now&&Now+.00001f>=state.MvpPoisonNext){state.MvpPoisonNext+=state.MvpPoisonInterval;DealMvpEffect(state.MvpPoisonCause,e,state.MvpPoisonMount,state.MvpPoisonDamage*state.MvpPoisonStacks);}
    if(state.MvpPoisonUntil<=Now)state.MvpPoisonStacks=0;
    if(e.Dead)continue;
    if(concussion!=null&&e.stunUntil>Now&&Now+.00001f>=state.MvpStunTickAt)
    {var v=concussion.Part.levels[concussion.Level];var weapon=InkWeapon(MvpActiveWeapon);state.MvpStunTickAt=Now+v.interval;
     if(weapon!=null)DealMvpEffect(InkSyntheticHit(InkOrigin(weapon,weapon.InkPalette.Read(),"mvp_stun",false),e),e,MvpMount(concussion),MvpReference(weapon)*v.ratio);}
    if(state.MvpBindUntil>0&&Now>=state.MvpBindUntil){var cause=state.MvpBindCause;state.MvpBindUntil=0;if(cause.HasValue&&cause.Value.Valid)MvpTrigger("bind_end",cause.Value);}
   }
   bool dashing=Player.Dashing;if(dashing&&!mvpWasDashing)mvpDashHits.Clear();if(!dashing&&mvpWasDashing)mvpDashEndedAt=Now;mvpWasDashing=dashing;
   if(dashing)
   {
    var weapon=InkWeapon(MvpActiveWeapon);
    if(weapon!=null&&MvpGrowth.SnapshotSlots(weapon.InstanceId).Any(s=>s.Part!=null&&s.Part.trigger=="dash"))
    {
     var body=new Bounds(Player.Center,Player.size);
     foreach(var e in enemies.ToArray())
      if(e&&!e.Dead&&!e.isDecoy&&e.HurtOverlaps(body)&&mvpDashHits.Add(MvpLifeKey(e)))
       MvpTrigger("dash",InkSyntheticHit(InkOrigin(weapon,weapon.InkPalette.Read(),"mvp_dash",false),e));
    }
   }
   FlushInkHits();
  }
  // 표식이 쌓일 때마다 그 몬스터의 표식 전체 유지시간을 갱신한다. 최대 개수에 이르면 시간만 갱신한다.
  void AddMvpMark(CombatActor target,Guid weapon,Guid execution)
  {var state=TargetInk(target);float until=Now+MvpGrowth.Data.markDuration;foreach(var m in state.Marks)m.Until=until;if(state.Marks.Count<MvpGrowth.Data.markCap)state.Marks.Add(new Mark{Weapon=weapon,Action=execution,Until=until});}
  void StartMvpMarkChain(InkHit hit,int count)
  {
   var slots=MvpSlotsFor(hit,"mark_burst");float bonus=MvpSum(slots,"mark_damage_bonus",v=>v.amount);
   var chain=new MvpMarkChain{Cause=hit,Slots=slots,Mount=new InkPartMount("mvp_mark_damage",true),Next=count,Total=count,At=Now,Damage=MvpHitReference(hit)*MvpGrowth.Data.markCoefficient*(1+bonus)};
   if(!StepMvpMarkChain(chain))mvpChains.Add(chain);
  }
  // 쌓인 역순으로 하나씩 터트린다. 첫 표식 피해만 추가타(적중 트리거 발동)이고 나머지는 적중 트리거를 발동하지 않는다.
  // 몬스터가 죽으면 멈추고, 번지는 표식이 있으면 가까운 몬스터로 옮겨 이어 간다. 마지막 빨강이 터지면 "표식 터트릴 시" 파츠가 발동한다. 끝나면 true.
  bool StepMvpMarkChain(MvpMarkChain c)
  {
   while(c.Next>0&&Now+.00001f>=c.At)
   {
    var target=c.Cause.Target;
    if(!target||target.Dead||target.LifeVersion!=c.Cause.TargetLife)
    {
     var spread=MvpFirst(c.Slots,"mark_chain_spread");
     var next=spread!=null&&target?InkNearby(target.Center,spread.Part.levels[spread.Level].radius,target,1).FirstOrDefault():null;
     if(!next)return true;
     CueMvp(new MvpInkCue(spread.Part.id,spread.Part.color,"mark_burst","mark_chain_spread",next,next.Center,next.Position,c.Cause.Hit.Origin.SourceFacing,0,c.Next,null));
     if(!c.SpreadBoosted){c.Damage*=1+spread.Part.levels[spread.Level].amount;c.SpreadBoosted=true;}
     c.Cause=Retarget(c.Cause,next);target=next;
    }
    int index=c.Next;
    CueMvp(new MvpInkCue(null,null,"mark_pop","mark_pop",target,target.Center,target.Position,c.Cause.Hit.Origin.SourceFacing,0,index,null));
    if(c.First){c.First=false;DealInk(c.Cause,c.Mount,c.Damage,1);}else DealMvpEffect(c.Cause,target,c.Mount,c.Damage);
    if(!target.Dead)MvpTrigger("mark_pop",c.Cause,default,false,c.Slots,index);
    c.Next--;c.At+=MvpGrowth.Data.markPopInterval;
    if(c.Next>0)continue;
    if(target.Dead)return true;
    CueMvp(new MvpInkCue(null,null,"mark_burst","mark_burst",target,target.Center,target.Position,c.Cause.Hit.Origin.SourceFacing,0,c.Total,c.Slots.Where(x=>x.Part!=null&&x.Part.trigger=="mark_burst").Select(x=>x.Part.color).Distinct().ToArray()));
    MvpTrigger("mark_burst",c.Cause,default,false,c.Slots);
    return true;
   }
   return c.Next<=0;
  }
  // 체력바 옆 숫자: 연쇄가 터지는 동안은 남은 표식 수, 아니면 쌓인 표식 수. 숫자 색은 MvpMarkColors[수-1]이다.
  public int ReadMvpMarkDisplay(CombatActor target)
  {if(!target)return 0;int chain=0;foreach(var c in mvpChains)if(c.Cause.Target==target&&c.Next>0)chain=Mathf.Max(chain,c.Next);return chain>0?chain:ReadInkMarks(target);}
  public int ReadMvpCharge(){int n=0;foreach(var c in mvpCharges.Values)n+=c.Stacks;return n;}
  // 질주 충전: 이 잉크를 낀 무기의 원타격이 맞으면 쌓인 기를 모두 방출해 기 하나당 추가타 피해를 준다.
  void ReleaseMvpCharge(InkHit hit)
  {
   foreach(var s in MvpSlotsFor(hit,"hit"))
   {
    if(s.Part==null||s.Part.kind!="move_charge"||!mvpCharges.TryGetValue(s.InstanceId,out var charge)||charge.Stacks<=0)continue;
    int n=charge.Stacks;charge.Stacks=0;mvpMoved.Remove(s.InstanceId);var v=s.Part.levels[s.Level];
    CueMvp(new MvpInkCue(s.Part.id,s.Part.color,"hit","move_charge_release",hit.Target,MvpCuePoint(hit),hit.Target.Position,hit.Hit.Origin.SourceFacing,0,n,null));
    DealInk(hit,MvpMount(s),MvpHitReference(hit)*v.ratio*n,1);
   }
  }
  Vector2 MvpCuePoint(InkHit hit)=>hit.Hit.Contact.HasPoint?new Vector2(hit.Hit.Contact.X,hit.Hit.Contact.Y):hit.Target?hit.Target.Center:Vector2.zero;
  void HandleMvpHit(InkHit hit)
  {
   CheckMvpLife();var state=TargetInk(hit.Target);var actionId=hit.Hit.Origin.ActionId;
   if(actionId=="ink_counter"){MvpTrigger("damaged",hit);return;}
   bool effect=actionId==MvpEffectAction;bool primary=!effect&&hit.Hit.Origin.Kind!=CombatHitKind.InkPart;Guid weapon=hit.Hit.Origin.Stamp.WeaponId;
   if(!effect)
   {
    if(primary&&hit.Hit.Origin.Stamp.InkPalette!=null&&inkStarts.TryGetValue(hit.Hit.Origin.Stamp.InkPalette,out var powered)&&powered.MvpPower>0)CueMvp(new MvpInkCue(null,null,"hit","power_hit",hit.Target,MvpCuePoint(hit),hit.Target.Position,hit.Hit.Origin.SourceFacing,0,0,null));
    // 선제 제압의 "처음 때릴 때"는 이 파츠를 낀 무기가 처음 맞힌 때다. 다른 무기의 선타는 기회를 쓰지 않는다.
    bool first=primary&&!state.MvpHitOnce&&MvpSlotsFor(hit,"hit").Any(s=>s.Part!=null&&s.Part.condition=="first_hit_per_life");if(first)state.MvpHitOnce=true;
    MvpTrigger("hit",hit,default,first);
   }
   if(primary)
   {
    CountMvpFocus(hit.Target,MvpSlotsFor(hit,"hit"),hit.Hit.Origin.ExecutionId);
    // 표식을 쌓은 무기가 아닌 무기의 원타격이면 남은 표식을 모두 터트린다. 터트린 뒤 이 무기의 표식 1개를 새로 남긴다.
    if(state.Marks.Any(m=>m.Weapon!=weapon)){int count=state.Marks.Count;state.Marks.Clear();StartMvpMarkChain(hit,count);}
    if(MvpMarksEnabled())
    {
     if(!hit.Target.Dead)AddMvpMark(hit.Target,weapon,hit.Hit.Origin.ExecutionId);
     // 색 번짐: 이 잉크를 낀 무기의 원타격이 가까운 다른 몬스터에게도 표식을 묻힌다. 묻히기만 하고 터트리지 않는다.
     var spread=MvpFirst(MvpSlotsFor(hit,"hit"),"mark_spread");
     if(spread!=null){var v=spread.Part.levels[spread.Level];foreach(var e in InkNearby(hit.Target.Center,v.radius,hit.Target,Mathf.Max(1,(int)v.count)))AddMvpMark(e,weapon,hit.Hit.Origin.ExecutionId);}
    }
    ReleaseMvpCharge(hit);
   }
   if(hit.Hit.Before.Health>0&&hit.Hit.After.Health<=0&&!state.KillProcessed){state.KillProcessed=true;MvpTrigger("kill",hit);}
  }
 }
}
namespace Deviant.Combat
{
 // 잉크 파츠 이펙트용 신호. PartId·Color 는 파츠 발동일 때만 있고, power_release·power_hit·mark_burst 는 파츠와 별개인 순간이다.
 // Point 는 맞은 자리(없으면 대상 가슴), Origin 은 발밑 기준점(플레이어·쓰러진 대상), Facing 은 맞은 방향(+1 오른쪽).
 public readonly struct MvpInkCue
 {
  public string PartId{get;} public string Color{get;} public string Trigger{get;} public string Kind{get;}
  public CombatActor Target{get;} public Vector2 Point{get;} public Vector2 Origin{get;} public int Facing{get;}
  public float Duration{get;} public int Count{get;} public string[] Colors{get;}
  public MvpInkCue(string partId,string color,string trigger,string kind,CombatActor target,Vector2 point,Vector2 origin,int facing,float duration,int count,string[] colors)
  {PartId=partId;Color=color;Trigger=trigger;Kind=kind;Target=target;Point=point;Origin=origin;Facing=facing<0?-1:1;Duration=duration;Count=count;Colors=colors;}
 }
}
