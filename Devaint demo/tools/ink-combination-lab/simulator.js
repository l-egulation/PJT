(function(root){
'use strict';
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v)),dist=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);
const unit=v=>{const n=Math.hypot(v.x,v.y);return n?{x:v.x/n,y:v.y/n}:{x:1,y:0};};
class Simulator{
 constructor(catalog,experiment,options={}){
  this.catalog=catalog;this.experiment=experiment;this.model=experiment.model;
  this.parts=new Map(catalog.parts.map(p=>[p.id,p]));
  this.options={...experiment.defaults,...options};
  this.options.chainLimit=clamp(Number(this.options.chainLimit)||0,0,6);
  this.options.internalCooldown=clamp(Number(this.options.internalCooldown)||0,0,10);
  this.options.bossCcScale=clamp(Number(this.options.bossCcScale)||0,0,1);
  if(!experiment.scenarios[this.options.scenario])throw Error('Unknown scenario');
  if(!['hit','manual'].includes(this.options.markMode))throw Error('Unknown mark mode');
  this.growth=options.growthConfig||null;
  if(this.growth){const safety=this.growth.tuning.proposedSafety;Object.assign(this.options,{marksPerHit:1,markCap:5,markDuration:8,marksOnExtra:false,markMode:'hit',markConsume:'all',allowDuplicates:true,modifierPolicy:'sum',chainLimit:safety.chainDepth,bossCcScale:safety.bossControlDurationAndDisplacementScale,ccRecovery:safety.hardControlRecoverySeconds});}
  this.loadout=this.growth?this.growth.slots.map(slots=>slots.map(s=>s?.partId||null)):options.loadout||[[],[]];
  this.loadout=this.loadout.map(ids=>[...ids]);
  if(this.loadout.length!==2||this.loadout.some(ids=>ids.length>6||ids.some(id=>!(this.growth&&id===null)&&!this.parts.has(id))))throw Error('Invalid loadout');
  if(!this.options.allowDuplicates&&this.loadout.some(ids=>new Set(ids).size!==ids.length))throw Error('Duplicate parts require the experiment option');
  this.profiles=(this.options.profiles||experiment.defaults.profiles).map(id=>experiment.profiles.find(w=>w.id===id));
  if(this.profiles.length!==2||this.profiles.some(w=>!w))throw Error('Invalid weapon profile');
  this.reset();
 }
 reset(){
  this.time=0;this.active=0;this.switchEpoch=0;this.busyUntil=0;this.cooldowns=[0,0];this.lastTargets=[null,null];
  this.player={x:this.model.playerX,y:this.model.playerY,hp:this.model.playerHealth,facing:{x:1,y:0}};
  this.buffs=[];this.shields=[];this.events=[];this.queue=[];this.scheduled=[];this.partReady=new Map();this.mvpDisplacement=new Map();this.actionEvents=0;this.selectedTarget=null;
  this.stats={damage:0,primaryDamage:0,extraDamage:0,hits:0,kills:0,detonations:0,healed:0,blocked:0,received:0,chainLimited:0,procs:{}};
  const scenario=this.experiment.scenarios[this.options.scenario];
  this.enemies=scenario.positions.map(([x,y],id)=>({id,x,y,spawn:{x,y},hp:scenario.health,maxHp:scenario.health,boss:scenario.boss,dead:false,life:1,marks:[],status:{},nextAttack:1,nextSkill:2,intent:null,ccReady:0,respawnAt:Infinity}));
  this.record('reset',{});return this;
 }
 record(type,data){
  const e={type,time:this.time,active:this.active,...data};this.events.push(e);
  if(this.events.length>this.model.eventLogLimit)this.events.shift();return e;
 }
 alive(){return this.enemies.filter(e=>!e.dead);}
 nearest(origin=this.player,excluded=null,range=Infinity){return this.alive().filter(e=>e.id!==excluded&&dist(e,origin)<=range).sort((a,b)=>dist(a,origin)-dist(b,origin)||a.id-b.id)[0];}
 target(){return this.alive().find(e=>e.id===this.selectedTarget)||this.nearest();}
 aim(){const target=this.target();if(this.options.autoAim&&target)this.player.facing=unit({x:target.x-this.player.x,y:target.y-this.player.y});}
 context(owner,target,extra={}){return{owner,target,damage:this.profiles[owner].basicDamage,baseDamage:this.profiles[owner].basicDamage*(this.growth?.attackMultiplier||1),depth:0,rangeScale:1+this.modifier('reach',owner),radiusScale:1+this.modifier('radius',owner),origin:{x:this.player.x,y:this.player.y},facing:{...this.player.facing},...extra};}
 canAct(){return this.player.hp>0&&this.time+1e-9>=this.busyUntil;}
 attack(kind='basic',owner=this.active){
  if(![0,1].includes(owner)||!['basic','skill'].includes(kind)||!this.canAct())return false;
  if(kind==='skill'&&this.cooldowns[owner]>1e-9)return false;
  if(owner!==this.active)return this.switchWeapon(owner,kind);
  this.actionEvents=0;this.mvpDisplacement.clear();this.aim();this.performAttack(kind,owner);this.flush();return true;
 }
 switchWeapon(incoming=1-this.active,kind='basic'){
  if(!this.canAct()||![0,1].includes(incoming)||incoming===this.active)return false;
  if(kind==='skill'&&this.cooldowns[incoming]>1e-9)return false;
  this.actionEvents=0;this.mvpDisplacement.clear();this.aim();const outgoing=this.active;
  this.trigger('switch',this.context(outgoing,this.target(),{incoming}));this.flush();
  this.active=incoming;this.switchEpoch++;this.record('switch',{owner:outgoing,incoming});
  this.performAttack(kind,incoming);this.flush();return true;
 }
 performAttack(kind,owner){
  const w=this.profiles[owner],skill=kind==='skill';
  const power=this.modifier('power',owner),reach=(skill?w.skillReach:w.basicReach)*(1+this.modifier('reach',owner));
  const radius=(skill?w.skillRadius:w.basicRadius)*(1+this.modifier('radius',owner,skill));
  const damage=(skill?w.skillDamage:w.basicDamage)*(this.growth?.attackMultiplier||1)*(1+power);
  const ctx=this.context(owner,this.target(),{damage,rangeScale:reach/(skill?w.skillReach:w.basicReach),radiusScale:radius/(skill?w.skillRadius:w.basicRadius),kind});
  // Snapshot before hit-trigger buffs. One-shot effects are consumed even on a miss.
  this.buffs=this.buffs.filter(b=>!((b.owner===owner||b.owner===-1)&&b.once&&(!b.skillOnly||skill)));
  this.busyUntil=this.time+(skill?w.skillDuration:w.basicDuration);
  if(skill)this.cooldowns[owner]=w.cooldown;
  this.record('attack',{owner,kind,damage,reach,radius,origin:ctx.origin,facing:ctx.facing});
  for(const e of this.inFront(ctx.origin,ctx.facing,reach,radius))this.enqueueHit(e,damage,ctx);
 }
 inFront(origin,facing,reach,width){
  return this.alive().filter(e=>{const x=e.x-origin.x,y=e.y-origin.y,along=x*facing.x+y*facing.y,cross=Math.abs(x*facing.y-y*facing.x);return along>=0&&along<=reach+this.model.targetRadius&&cross<=width+this.model.targetRadius;});
 }
 around(origin,radius,excluded=null){return this.alive().filter(e=>e.id!==excluded&&dist(e,origin)<=radius+this.model.targetRadius);}
 enqueueHit(target,damage,ctx){
  if(!target||target.dead||damage<=0||!Number.isFinite(damage))return;
  if(ctx.depth>this.options.chainLimit){this.stats.chainLimited++;return;}
  if(this.queue.length>=this.model.eventBudget){this.stats.chainLimited++;return;}
  const life=target.life;this.queue.push(()=>{if(!target.dead&&target.life===life)this.hit(target,damage,ctx);});
 }
 flush(){
  while(this.queue.length&&this.actionEvents<this.model.eventBudget){this.actionEvents++;this.queue.shift()();}
  if(this.queue.length){this.stats.chainLimited+=this.queue.length;this.queue=[];this.record('limit',{});}
 }
 hit(e,damage,ctx){
  if(e.dead)return;const actual=Math.min(e.hp,damage),extra=ctx.depth>0;
  const eligible=(this.growth&&extra?[]:e.marks).filter(m=>m.until>this.time&&m.owner!==ctx.owner&&m.epoch<this.switchEpoch);
  const consumed=eligible.length?(this.options.markConsume==='one'?eligible.slice(0,1):eligible):[];
  if(consumed.length)e.marks=e.marks.filter(m=>!consumed.includes(m));
  e.hp=Math.max(0,e.hp-actual);const killed=e.hp<=0;if(killed){e.dead=true;e.intent=null;e.respawnAt=this.time+this.model.respawnDelay;}
  this.stats.damage+=actual;this.stats[extra?'extraDamage':'primaryDamage']+=actual;this.stats.hits++;
  this.lastTargets[ctx.owner]=e.id;
  const hitctx={...ctx,target:e,damage,actualDamage:actual};
  this.record('hit',{owner:ctx.owner,part:ctx.part,targetId:e.id,life:e.life,damage:actual,extra,depth:ctx.depth,x:e.x,y:e.y});
  this.trigger('hit',hitctx);
  if(consumed.length){
   this.stats.detonations++;this.record('detonate',{owner:ctx.owner,targetId:e.id,count:consumed.length,x:e.x,y:e.y});
   this.trigger('detonate',{...hitctx,marksConsumed:consumed.length});
   const detonationDamage=this.growth?ctx.baseDamage*consumed.length*this.growth.tuning.mark.coefficientPerConsumedMark:this.options.detonationDamage;
   if(detonationDamage>0&&!killed)this.enqueueHit(e,detonationDamage,{...hitctx,depth:ctx.depth+1,part:'experiment_mark_damage'});
  }
  if(!killed&&this.options.markMode==='hit'&&(!extra||this.options.marksOnExtra))this.addMarks(e.id,this.options.marksPerHit,ctx.owner);
  if(killed){this.stats.kills++;this.record('kill',{owner:ctx.owner,targetId:e.id,x:e.x,y:e.y});this.trigger('kill',hitctx);}
 }
 addMarks(id,count=1,owner=this.active){
  const e=this.enemies.find(t=>t.id===id);if(!e||e.dead)return;
  if(this.growth)for(const mark of e.marks)mark.until=this.time+this.options.markDuration;
  for(let i=0;i<Math.min(count,this.options.markCap);i++){
   if(e.marks.length>=this.options.markCap)e.marks.shift();
   e.marks.push({owner,epoch:this.switchEpoch,until:this.time+this.options.markDuration});
  }
  this.record('mark',{owner,targetId:e.id,count:e.marks.length,x:e.x,y:e.y});
 }
 trigger(trigger,ctx){
  let remainingLeech=this.growth?.tuning.proposedSafety.lifeStealFractionCap??Infinity;
  this.loadout[ctx.owner].map((id,slot)=>({p:this.parts.get(id),slot})).filter(x=>x.p).sort((a,b)=>a.p.id.localeCompare(b.p.id)||a.slot-b.slot).forEach(({p,slot})=>{
   if(p.trigger!==trigger)return;
   const row=this.growth?.slots[ctx.owner][slot];
   const key=`${ctx.owner}:${slot}:${p.id}`+(row?.cooldownScope==='instance_and_target'?`:${ctx.target?.id}:${ctx.target?.life}`:'');
   if((this.partReady.get(key)||0)>this.time+1e-9)return;
   if(p.effect.kind==='conditional_hit'&&(!ctx.target||ctx.target.hp>ctx.target.maxHp*p.effect.params.threshold))return;
   this.partReady.set(key,this.time+(row?.internalCooldownSeconds??this.options.internalCooldown));
   this.stats.procs[p.id]=(this.stats.procs[p.id]||0)+1;
   this.record('proc',{owner:ctx.owner,part:p.id,targetId:ctx.target?.id,trigger});
   let effectParams=row?.params;if(this.growth&&p.effect.kind==='lifesteal'){const ratio=Math.min(effectParams.ratio,remainingLeech);remainingLeech-=ratio;effectParams={...effectParams,ratio};}
   this.apply(row?{...p,effect:{...p.effect,params:effectParams}}:p,{...ctx,part:p.id,instance:`${ctx.owner}:${slot}:${p.id}`});
  });
 }
 apply(p,ctx){
  const a=p.effect.params,e=ctx.target,owner=ctx.owner,kind=p.effect.kind;
  if(!a)throw Error('Missing structured parameters: '+p.id);
  const incoming=ctx.incoming??owner,global=this.growth?(p.effect.weaponScope==='incoming_weapon'?incoming:owner):-1;
  const base=ctx.baseDamage,extra=(t,damage)=>this.enqueueHit(t,damage,{...ctx,depth:ctx.depth+1});
  const radial=(center,radius,exclude)=>this.around(center,radius*(ctx.radiusScale||1),exclude);
  const buff=(stat,amount,options={})=>this.addBuff(ctx,stat,amount,{duration:a.duration,...options});
  const control=(t,type)=>this.control(t,type,a.duration,ctx,{amount:a.amount,origin:{...ctx.origin}});
  this.record('effect',{owner,part:p.id,kind,targetId:e?.id,x:e?.x??ctx.origin.x,y:e?.y??ctx.origin.y});
  switch(kind){
   case 'extra_hit':case 'conditional_hit':extra(e,ctx.damage*a.ratio);break;
   case 'line_hit':if(e)for(const t of this.inFront(e,ctx.facing,a.range*ctx.rangeScale,a.width*ctx.radiusScale))if(t.id!==e.id)extra(t,ctx.damage*a.ratio);break;
   case 'next_attack_power':buff('power',a.amount,{owner:this.growth?global:(p.trigger==='switch'?incoming:global),once:true});break;
   case 'outgoing_area_hit':for(const t of this.inFront(ctx.origin,ctx.facing,a.range*ctx.rangeScale,a.radius*ctx.radiusScale))extra(t,base*a.ratio);break;
   case 'delayed_single_hit':{
    const target=this.enemies.find(t=>t.id===this.lastTargets[owner]);if(!target||target.dead)break;
    if(this.scheduled.length>=this.model.scheduledLimit){this.stats.chainLimited++;break;}
    const life=target.life;this.scheduled.push({at:this.time+a.delay,run:()=>{if(!target.dead&&target.life===life)extra(target,base*a.ratio);}});break;
   }
   case 'detonation_extra_hit':extra(e,base*a.ratio);break;
   case 'detonation_area_hit':if(e)for(const t of radial(e,a.radius,e.id))extra(t,base*a.ratio);break;
   case 'single_ricochet':if(e)extra(this.nearest(e,e.id,a.range*ctx.rangeScale),base*a.ratio);break;
   case 'move_speed':buff('move',a.amount,{owner:global});break;
   case 'stack_move_speed':buff('move',a.amount,{owner:global,cap:a.cap});break;
   case 'directional_move_speed':buff('move',a.amount,{owner:global,direction:a.direction,anchor:a.direction==='backward'?null:{x:e?.x??ctx.origin.x,y:e?.y??ctx.origin.y},targetId:a.direction==='toward'?e?.id:null,facing:{...ctx.facing}});break;
   case 'heal':this.heal(a.amount,ctx);break;
   case 'shield':case 'stack_shield':this.addShield(a,ctx);break;
   case 'lifesteal':this.heal((ctx.actualDamage||0)*a.ratio,ctx);break;
   case 'missing_health_heal':this.heal((this.maxHealth()-this.player.hp)*a.ratio,ctx);break;
   case 'temporary_max_health':buff('maxHealth',a.amount,{owner:global});break;
   case 'max_health_heal':this.heal(this.maxHealth()*a.ratio,ctx);break;
   case 'next_attack_reach':buff('reach',a.amount,{owner:p.trigger==='switch'?incoming:owner,once:true});break;
   case 'cooldown_reduce':this.reduceCooldown(p.trigger==='switch'?incoming:owner,a.seconds,ctx);break;
   case 'area_radius_buff':buff('radius',a.amount,{owner:p.trigger==='detonate'?global:owner});break;
   case 'next_skill_radius':buff('radius',a.amount,{owner:incoming,once:true,skillOnly:true});break;
   case 'both_cooldown_reduce':for(const w of [0,1])this.reduceCooldown(w,a.seconds,ctx);break;
   case 'stack_area_radius':buff('radius',a.amount,{owner:global,cap:a.cap});break;
   case 'slow':case 'stun':case 'root':case 'disarm':if(e&&!e.dead)control(e,kind);break;
   case 'knockback':if(e&&!e.dead)this.displace(e,ctx.facing,a.distance,ctx);break;
   case 'pull':{
    const center=p.trigger==='kill'?e:ctx.origin;if(center)for(const t of radial(center,a.radius,p.trigger==='kill'?e.id:null)){
     const d=dist(t,center);this.displace(t,unit({x:center.x-t.x,y:center.y-t.y}),Math.min(a.distance,d),ctx);
    }break;
   }
   case 'fear':case 'silence':for(const t of radial(ctx.origin,a.radius))if((t.x-ctx.origin.x)*ctx.facing.x+(t.y-ctx.origin.y)*ctx.facing.y>=0)control(t,kind);break;
   case 'blind':if(e)for(const t of radial(e,a.radius,e.id))control(t,'blind');break;
   default:throw Error('Unsupported effect: '+kind);
  }
 }
 addBuff(ctx,stat,amount,options){
  const owner=options.owner??ctx.owner,key=`${ctx.instance}:${stat}:${owner}`;
  let b=this.buffs.find(b=>b.key===key&&b.until>this.time);
  if(!b){b={key,stat,owner,amount,stacks:0,part:ctx.part,sourceOwner:ctx.owner,...options};this.buffs.push(b);}
  else Object.assign(b,options);
  b.stacks=options.cap?Math.min(options.cap,b.stacks+1):1;b.amount=amount*b.stacks;
  b.until=options.once?Infinity:this.time+(options.duration||0);
 }
 modifier(stat,owner=this.active,skill=false,direction=null){
  const values=this.buffs.filter(b=>b.stat===stat&&b.until>this.time&&(b.owner===-1||b.owner===owner)&&(!b.skillOnly||skill)).filter(b=>{
   if(!b.direction)return true;if(!direction)return false;
   let v;if(b.direction==='backward')v={x:-b.facing.x,y:-b.facing.y};
   else{const anchor=b.targetId!=null?this.enemies.find(e=>e.id===b.targetId&&!e.dead):b.anchor;if(!anchor)return false;v={x:anchor.x-this.player.x,y:anchor.y-this.player.y};if(b.direction==='away'){v.x=-v.x;v.y=-v.y;}}
   const u=unit(v);return u.x*direction.x+u.y*direction.y>0.1;
  }).map(b=>b.amount);
  const value=this.options.modifierPolicy==='sum'?values.reduce((a,b)=>a+b,0):Math.max(0,...values);
  return this.growth&&stat==='move'?Math.min(value,this.growth.tuning.proposedSafety.movementSpeedBonusCap):value;
 }
 maxHealth(){return Math.round(this.model.playerHealth*(1+this.modifier('maxHealth'))*1000)/1000;}
 moveSpeed(direction=null){return this.model.moveSpeed*(1+this.modifier('move',this.active,false,direction));}
 heal(amount,ctx){if(this.player.hp<=0)return;const gain=Math.max(0,Math.min(amount,this.maxHealth()-this.player.hp));this.player.hp+=gain;this.stats.healed+=gain;this.record('heal',{owner:ctx.owner,part:ctx.part,amount:gain});}
 addShield(a,ctx){
  let s=this.shields.find(s=>s.key===ctx.instance&&s.until>this.time);
  if(!s){s={key:ctx.instance,amount:0,part:ctx.part};this.shields.push(s);}
  s.amount=a.cap?Math.min(a.cap,s.amount+a.amount):a.amount;s.until=this.time+a.duration;
 }
 shieldAmount(){return this.shields.filter(s=>s.until>this.time).reduce((n,s)=>n+s.amount,0);}
 takeDamage(amount){
  if(this.player.hp<=0)return;let rest=amount;
  for(const s of this.shields.filter(s=>s.until>this.time).sort((a,b)=>a.until-b.until)){const n=Math.min(rest,s.amount);s.amount-=n;rest-=n;this.stats.blocked+=n;}
  const actual=Math.min(this.player.hp,rest);this.player.hp-=actual;this.stats.received+=actual;
  this.record('received',{amount:actual,absorbed:amount-rest});
 }
 reduceCooldown(owner,seconds,ctx){const n=Math.min(seconds,this.cooldowns[owner]);this.cooldowns[owner]-=n;this.record('cooldown',{owner,part:ctx.part,amount:n});}
 control(e,type,duration,ctx,extra={}){
  const hard=['stun','root','fear'].includes(type);
  const scale=e.boss?this.options.bossCcScale:1;if(scale<=0||(hard&&e.ccReady>this.time))return;
  const until=this.time+duration*scale,current=this.hasStatus(e,type)?e.status[type]:null;
  e.status[type]={until:Math.max(until,current?.until||0),...extra};
  if(type==='slow')e.status[type].amount=Math.max(extra.amount||0,current?.amount||0);
  if(hard)e.ccReady=this.options.ccRecovery>0?until+this.options.ccRecovery:0;
  this.record('cc',{owner:ctx.owner,part:ctx.part,targetId:e.id,kind:type,until,x:e.x,y:e.y});
 }
 displace(e,direction,distance,ctx){const scale=e.boss?this.options.bossCcScale:1,key=e.id+':'+e.life,used=this.mvpDisplacement.get(key)||0,amount=this.growth?Math.min(distance*scale,Math.max(0,this.growth.tuning.proposedSafety.combinedDisplacementCapPerTargetPerSourceAction-used)):distance*scale,before={x:e.x,y:e.y};e.x=clamp(e.x+direction.x*amount,.4,this.model.worldWidth-.4);e.y=clamp(e.y+direction.y*amount,.4,this.model.worldHeight-.4);if(this.growth)this.mvpDisplacement.set(key,used+dist(before,e));this.record('displace',{owner:ctx.owner,part:ctx.part,targetId:e.id,x:e.x,y:e.y});}
 hasStatus(e,type){return(e.status[type]?.until||0)>this.time;}
 enemySpeed(e){return this.model.enemyMoveSpeed*(1-(this.hasStatus(e,'slow')?e.status.slow.amount:0));}
 advance(seconds,move={x:0,y:0}){
  if(!Number.isFinite(seconds)||seconds<0)throw Error('Invalid time');
  let left=Math.min(seconds,60);
  while(left>1e-9){const dt=Math.min(this.model.maxStep,left);left-=dt;this.step(dt,move);}
 }
 step(dt,move){
  this.time+=dt;this.actionEvents=0;
  this.cooldowns=this.cooldowns.map(n=>Math.max(0,n-dt));
  this.buffs=this.buffs.filter(b=>b.until>this.time+1e-9);this.shields=this.shields.filter(s=>s.until>this.time+1e-9&&s.amount>0);
  this.player.hp=Math.min(this.player.hp,this.maxHealth());
  if(this.player.hp>0&&(move.x||move.y)){const dir=unit(move),speed=this.moveSpeed(dir);this.player.x=clamp(this.player.x+dir.x*speed*dt,.4,this.model.worldWidth-.4);this.player.y=clamp(this.player.y+dir.y*speed*dt,.4,this.model.worldHeight-.4);this.player.facing=dir;}
  const due=this.scheduled.filter(s=>s.at<=this.time+1e-9);this.scheduled=this.scheduled.filter(s=>s.at>this.time+1e-9);for(const task of due)task.run();this.flush();
  for(const e of this.enemies){
   e.marks=e.marks.filter(m=>m.until>this.time);
   if(e.dead){if(this.options.respawn&&e.respawnAt<=this.time){e.dead=false;e.life++;e.hp=e.maxHp;e.x=e.spawn.x;e.y=e.spawn.y;e.marks=[];e.status={};e.intent=null;e.ccReady=0;e.nextAttack=this.time+1;e.nextSkill=this.time+2;}continue;}
   if(this.options.enemyAI&&this.player.hp>0)this.enemyStep(e,dt);
  }
 }
 enemyStep(e,dt){
  const stunned=this.hasStatus(e,'stun');
  if(e.intent&&this.time>=e.intent.at){
   const intent=e.intent;e.intent=null;
   const blocked=stunned||(intent.kind==='skill'?this.hasStatus(e,'silence'):this.hasStatus(e,'disarm'));
   if(!blocked&&dist(this.player,intent.aim)<=this.model.enemyHitRadius){this.takeDamage(intent.damage);this.record('enemy_hit',{targetId:e.id,kind:intent.kind,x:e.x,y:e.y});}
  }
  if(stunned)return;
  if(!this.hasStatus(e,'root')){
   const fear=this.hasStatus(e,'fear'),origin=fear?e.status.fear.origin:this.player;
   const direction=unit({x:fear?e.x-origin.x:origin.x-e.x,y:fear?e.y-origin.y:origin.y-e.y});
   if(fear||dist(e,this.player)>this.model.enemyAttackRange*.85){const speed=this.enemySpeed(e);e.x=clamp(e.x+direction.x*speed*dt,.4,this.model.worldWidth-.4);e.y=clamp(e.y+direction.y*speed*dt,.4,this.model.worldHeight-.4);}
  }
  if(e.intent||this.hasStatus(e,'blind'))return;
  const d=dist(e,this.player);let kind=null;
  if(d<=this.model.enemySkillRange&&e.nextSkill<=this.time&&!this.hasStatus(e,'silence'))kind='skill';
  else if(d<=this.model.enemyAttackRange&&e.nextAttack<=this.time&&!this.hasStatus(e,'disarm'))kind='basic';
  if(kind){e.intent={kind,aim:{x:this.player.x,y:this.player.y},at:this.time+this.model.enemyWindup,damage:kind==='skill'?this.model.enemySkillDamage:this.model.enemyAttackDamage};if(kind==='skill')e.nextSkill=this.time+this.model.enemySkillCooldown;else e.nextAttack=this.time+this.model.enemyAttackCooldown;this.record('enemy_aim',{targetId:e.id,kind});}
 }
}
const api={Simulator};if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.InkSimulator=api;
})(typeof globalThis!=='undefined'?globalThis:this);
